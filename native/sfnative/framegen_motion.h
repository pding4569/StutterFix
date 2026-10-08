// Included inside namespace outside, research builds only. No full-picture CPU readback.
// Each source submits 9 x 256 samples and reads 396 bytes only after an EVENT is ready.
struct MotionDecision { int reason=0,textured=0,agree=0,veto=0; double same=0,warp=0; };
class ImageMotionGate {
    struct Read { ComPtr<ID3D11Buffer> gpu,cpu; ComPtr<ID3D11UnorderedAccessView> uav; ComPtr<ID3D11Query> ready; unsigned long long sequence=0; };
    struct Result { unsigned long long sequence=0; MotionDecision value; };
    std::array<Read,3> reads;
    std::array<Result,3> results;
    ComPtr<ID3D11ComputeShader> shader;
    ComPtr<ID3D11DeviceContext> deferred;
    ComPtr<ID3D11Buffer> constants;
    ComPtr<ID3D11SamplerState> sampler;
public:
    // 0 pending, 1 untextured/uncertain, 2 image mismatch, 3 camera agreement.
    static MotionDecision classify(const UINT* data) {
        MotionDecision r; r.reason=1;
        auto correlation=[](double n,double a,double b,double aa,double bb,double ab) {
            return (n*ab-a*b)/sqrt(std::max(1.,(n*aa-a*a)*(n*bb-b*b)));
        };
        for(int patch=0;patch<9;patch++) {
            auto s=data+patch*11; double n=s[8]; if(n<200) continue;
            double variance=(n*s[3]-double(s[0])*s[0])/(n*n);
            double oldVariance=(n*s[4]-double(s[1])*s[1])/(n*n);
            if(std::min(variance,oldVariance)<36) continue;
            double same=correlation(n,s[0],s[1],s[3],s[4],s[6]);
            double warp=correlation(n,s[0],s[2],s[3],s[5],s[7]);
            ++r.textured; r.same+=same; r.warp+=warp;
            if(same>.9 && same>warp+.02) ++r.veto;
            if(warp>.9 && warp>same+.02) ++r.agree;
        }
        if(r.textured) { r.same/=r.textured; r.warp/=r.textured; }
        if(r.veto) r.reason=2;
        else if(r.agree>=2 && r.warp>r.same+.01) r.reason=3;
        return r;
    }
    void initialize(ID3D11Device* device) {
        if(shader) return;
        const char* code=R"(
cbuffer Data:register(b0) { float4 oldCamera; float4 camera; float4 info; };
Texture2D oldTex:register(t0); Texture2D newTex:register(t1);
SamplerState linearSampler:register(s0); RWStructuredBuffer<uint> output:register(u0);
groupshared uint sums[11];
float2 rotate(float2 p,float a) { float s,c; sincos(a,s,c); return float2(c*p.x-s*p.y,s*p.x+c*p.y); }
float2 source(float2 uv) {
    float2 world=rotate(float2((uv.x-.5)*info.x,.5-uv.y)*2*camera.z,camera.w)+camera.xy;
    float2 q=rotate(world-oldCamera.xy,-oldCamera.w)/(2*oldCamera.z);
    return float2(.5+q.x/info.x,.5-q.y);
}
uint gray(Texture2D tex,float2 uv) { if(info.y>.5) uv.y=1-uv.y; return uint(round(saturate(dot(tex.SampleLevel(linearSampler,uv,0).rgb,float3(.299,.587,.114)))*255)); }
[numthreads(16,16,1)] void CS(uint3 group:SV_GroupID,uint3 thread:SV_GroupThreadID,uint index:SV_GroupIndex) {
    if(index==0) for(uint k=0;k<11;k++) sums[k]=0;
    GroupMemoryBarrierWithGroupSync();
    float2 uv=.2+.2*(group.xy+(thread.xy+.5)/16.);
    uint a=gray(newTex,uv),b=gray(oldTex,uv),c=gray(oldTex,source(uv));
    InterlockedAdd(sums[0],a); InterlockedAdd(sums[1],b); InterlockedAdd(sums[2],c);
    InterlockedAdd(sums[3],a*a); InterlockedAdd(sums[4],b*b); InterlockedAdd(sums[5],c*c);
    InterlockedAdd(sums[6],a*b); InterlockedAdd(sums[7],a*c); InterlockedAdd(sums[8],1);
    InterlockedAdd(sums[9],uint(abs(int(a)-int(b)))); InterlockedAdd(sums[10],uint(abs(int(a)-int(c))));
    GroupMemoryBarrierWithGroupSync();
    if(index==0) for(uint k=0;k<11;k++) output[(group.y*3+group.x)*11+k]=sums[k];
})";
        ComPtr<ID3DBlob> bytes,errors;
        HRESULT hr=D3DCompile(code,strlen(code),nullptr,nullptr,nullptr,"CS","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&bytes,&errors);
        if(FAILED(hr)) throw std::runtime_error(errors?static_cast<const char*>(errors->GetBufferPointer()):"motion shader compile failure");
        check(device->CreateComputeShader(bytes->GetBufferPointer(),bytes->GetBufferSize(),nullptr,&shader));
        check(device->CreateDeferredContext(0,&deferred));
        D3D11_BUFFER_DESC cb{}; cb.ByteWidth=48; cb.Usage=D3D11_USAGE_DEFAULT; cb.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        check(device->CreateBuffer(&cb,nullptr,&constants));
        D3D11_SAMPLER_DESC sd{}; sd.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR; sd.AddressU=sd.AddressV=sd.AddressW=D3D11_TEXTURE_ADDRESS_BORDER; sd.MaxLOD=D3D11_FLOAT32_MAX;
        check(device->CreateSamplerState(&sd,&sampler));
        for(auto& r:reads) {
            D3D11_BUFFER_DESC bd{}; bd.ByteWidth=9*11*sizeof(UINT); bd.Usage=D3D11_USAGE_DEFAULT; bd.BindFlags=D3D11_BIND_UNORDERED_ACCESS; bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED; bd.StructureByteStride=sizeof(UINT);
            check(device->CreateBuffer(&bd,nullptr,&r.gpu));
            D3D11_UNORDERED_ACCESS_VIEW_DESC ud{}; ud.Format=DXGI_FORMAT_UNKNOWN; ud.ViewDimension=D3D11_UAV_DIMENSION_BUFFER; ud.Buffer.NumElements=99;
            check(device->CreateUnorderedAccessView(r.gpu.Get(),&ud,&r.uav));
            bd.Usage=D3D11_USAGE_STAGING; bd.BindFlags=bd.MiscFlags=bd.StructureByteStride=0; bd.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
            check(device->CreateBuffer(&bd,nullptr,&r.cpu));
            D3D11_QUERY_DESC q{D3D11_QUERY_EVENT,0}; check(device->CreateQuery(&q,&r.ready));
        }
    }
    void collect(ID3D11DeviceContext* immediate) {
        for(auto& r:reads) {
            if(!r.sequence) continue;
            HRESULT hr=immediate->GetData(r.ready.Get(),nullptr,0,D3D11_ASYNC_GETDATA_DONOTFLUSH);
            if(hr==S_FALSE) continue; check(hr);
            D3D11_MAPPED_SUBRESOURCE mapped{};
            hr=immediate->Map(r.cpu.Get(),0,D3D11_MAP_READ,D3D11_MAP_FLAG_DO_NOT_WAIT,&mapped);
            if(hr==DXGI_ERROR_WAS_STILL_DRAWING) continue; check(hr);
            auto value=classify(static_cast<const UINT*>(mapped.pData)); immediate->Unmap(r.cpu.Get(),0);
            results[r.sequence%results.size()]={r.sequence,value}; r.sequence=0;
        }
    }
    bool submit(ID3D11Device* device,ID3D11DeviceContext* immediate,unsigned long long sequence,const Slot& before,const Slot& after,unsigned width,unsigned height) {
        initialize(device); collect(immediate);
        Read* r=nullptr; for(auto& candidate:reads) if(!candidate.sequence) {r=&candidate;break;}
        if(!r) return false; // No waiting and no overwriting an in-flight read.
        float data[12]; memcpy(data,before.packet.pose.camera,16); memcpy(data+4,after.packet.pose.camera,16);
        data[8]=float(width)/height; data[9]=float(after.packet.flip); data[10]=data[11]=0;
        deferred->ClearState(); deferred->UpdateSubresource(constants.Get(),0,nullptr,data,0,0);
        auto cb=constants.Get(); deferred->CSSetConstantBuffers(0,1,&cb); auto sm=sampler.Get(); deferred->CSSetSamplers(0,1,&sm);
        ID3D11ShaderResourceView* views[]={before.images[0].view.Get(),after.images[0].view.Get()}; deferred->CSSetShaderResources(0,2,views);
        auto uav=r->uav.Get(); deferred->CSSetUnorderedAccessViews(0,1,&uav,nullptr); deferred->CSSetShader(shader.Get(),nullptr,0); deferred->Dispatch(3,3,1);
        ID3D11UnorderedAccessView* none=nullptr; deferred->CSSetUnorderedAccessViews(0,1,&none,nullptr);
        deferred->CopyResource(r->cpu.Get(),r->gpu.Get()); deferred->End(r->ready.Get());
        ComPtr<ID3D11CommandList> list; check(deferred->FinishCommandList(FALSE,&list)); immediate->ExecuteCommandList(list.Get(),TRUE);
        r->sequence=sequence; return true;
    }
    MotionDecision decision(ID3D11DeviceContext* immediate,unsigned long long sequence) {
        collect(immediate); auto& r=results[sequence%results.size()]; return r.sequence==sequence?r.value:MotionDecision{};
    }
};
// Triangle bound for every point/phase of the known-camera interpolation, physical pixels.
// Reject the entire source interval: clipping only a late generated frame would cause a return.
inline double motionBound(const Pose& a,const Pose& b,unsigned w,unsigned h) {
    double z=std::min(a.camera[2],b.camera[2]); if(z<=.00001) return std::numeric_limits<double>::infinity();
    double radius=hypot(w,h)*.5;
    return hypot(b.camera[0]-a.camera[0],b.camera[1]-a.camera[1])*h/(2*z)+
        radius*fabs(b.camera[2]-a.camera[2])/z+2*radius*sin(fabs(angleDifference(a.camera[3],b.camera[3]))*.5)*std::max(a.camera[2],b.camera[2])/z;
}
struct MotionRecord {
    int frame,reason=0,textured=0,agree=0,veto=0; double song,bound,same=0,warp=0;
    unsigned long long reprojected=0,repeated=0,pending=0;
};
