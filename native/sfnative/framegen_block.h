// Research only. GPU block matching between TWO KNOWN images; no camera prediction.
// All GPU work is ordered through the existing protected D3D11 immediate context.
class BlockFlow {
    struct Image { Texture texture; ComPtr<ID3D11UnorderedAccessView> target; };
    std::array<Image,2> gray,flow;
    ComPtr<ID3D11DeviceContext> commands;
    ComPtr<ID3D11ComputeShader> reduce,match,metric;
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    ComPtr<ID3D11Buffer> constants,counters,counterRead;
    ComPtr<ID3D11UnorderedAccessView> counterTarget;
    ComPtr<ID3D11SamplerState> linear;
    unsigned width=0,height=0,smallW=0,smallH=0,blocksW=0,blocksH=0;
    unsigned long long sequence=0;
    struct Data { float info[4],grid[4],boxes[4][4]; };
    static const char* code() { return R"(
cbuffer Data:register(b0) { float4 info; float4 grid; float4 boxes[4]; };
// info: width,height,phase,worldFlip. grid: smallW,smallH,blocksW,blocksH.
Texture2D<float4> oldWorld:register(t0),newWorld:register(t1),oldScreen:register(t2),newScreen:register(t3);
Texture2D<float4> forwardFlow:register(t4),backwardFlow:register(t5);
Texture2D<float> grayA:register(t6),grayB:register(t7);
SamplerState linearClamp:register(s0);
RWTexture2D<float> reduced:register(u0); RWTexture2D<float4> vectors:register(u1);
RWStructuredBuffer<uint> totals:register(u2);
float2 worldUV(float2 uv) { if(info.w>.5) uv.y=1-uv.y; return uv; }
float3 worldColor(Texture2D<float4> image,float2 uv) { return image.SampleLevel(linearClamp,worldUV(uv),0).rgb; }
[numthreads(8,8,1)] void Reduce(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(grid.xy))) return;
    float2 uv=(id.xy+.5)/grid.xy,offset=2./info.xy;
    float3 c=worldColor(oldWorld,uv+offset)+worldColor(oldWorld,uv-offset)+worldColor(oldWorld,uv+offset*float2(1,-1))+worldColor(oldWorld,uv+offset*float2(-1,1));
    reduced[id.xy]=dot(c*.25,float3(.299,.587,.114));
}
float errorAt(float values[16],int2 origin,int2 delta) {
    if(any(origin+delta<0) || any(origin+delta+6>=int2(grid.xy))) return 1000;
    float sum=0; [unroll] for(int k=0;k<16;k++) {
        float b=grayB.Load(int3(origin+delta+int2(k%4,k/4)*2,0)); float d=values[k]-b; sum+=d*d;
    } return sum/16;
}
[numthreads(8,8,1)] void Match(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(grid.zw))) return;
    int2 origin=int2(id.xy)*8; float values[16],mean=0,squares=0;
    [unroll] for(int k=0;k<16;k++) { float a=grayA.Load(int3(min(origin+int2(k%4,k/4)*2,int2(grid.xy)-1),0)); values[k]=a; mean+=a; squares+=a*a; }
    float variance=squares/16-(mean/16)*(mean/16);
    if(variance<.0004) { vectors[id.xy]=0; return; }
    int2 best=0; float cost=errorAt(values,origin,best),zero=cost;
    // Exhaust the coarse grid before refining: a local hill climb misses even
    // exact translations of detailed textures. Exact zero also stays EXACT zero.
    if(cost>1e-8) {
        [loop] for(int y=-6;y<=6;y+=2) [loop] for(int x=-6;x<=6;x+=2) {
            int2 d=int2(x,y); float c=errorAt(values,origin,d);
            if(c<cost-1e-7) { cost=c; best=d; }
        }
        int2 center=best;
        [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++) {
            int2 d=center+int2(x,y); float c=errorAt(values,origin,d);
            if(c<cost-1e-7) { cost=c; best=d; }
        }
    }
    float xm=errorAt(values,origin,best-int2(1,0)),xp=errorAt(values,origin,best+int2(1,0));
    float ym=errorAt(values,origin,best-int2(0,1)),yp=errorAt(values,origin,best+int2(0,1));
    float2 curvature=float2(xm+xp-2*cost,ym+yp-2*cost);
    float2 fractional=cost>1e-8 ? clamp(float2(xm-xp,ym-yp)/(2*max(curvature,1e-6)),-.5,.5) : 0;
    float confidence=cost<.008 && (all(best==0) || cost<zero*.85) ? 1 : 0;
    vectors[id.xy]=float4(best+fractional,confidence,cost);
}
bool inside(float2 uv,float4 b) { return b.z>b.x && b.w>b.y && all(uv>=b.xy) && all(uv<=b.zw); }
float4 flowAt(Texture2D<float4> f,float2 uv) { return f.Load(int3(clamp(int2(uv*grid.xy)/8,0,int2(grid.zw)-1),0)); }
float3 closest(float2 uv) { return info.z<.5 ? oldScreen.SampleLevel(linearClamp,uv,0).rgb : newScreen.SampleLevel(linearClamp,uv,0).rgb; }
float difference(float3 a,float3 b) { return max(max(abs(a.r-b.r),abs(a.g-b.g)),abs(a.b-b.b)); }
float3 colorAt(float2 uv,out bool moving) {
    moving=false; float3 fallback=closest(uv);
    if(info.z<=0 || info.z>=1) return fallback;
    // Preserve both true-frame planet envelopes, including cached trail/glow bounds.
    [unroll] for(int k=0;k<4;k++) if(inside(uv,boxes[k])) return fallback;
    float3 a=worldColor(oldWorld,uv),b=worldColor(newWorld,uv);
    float3 sa=oldScreen.SampleLevel(linearClamp,uv,0).rgb,sb=newScreen.SampleLevel(linearClamp,uv,0).rgb;
    if(difference(a,sa)>2.5/255. || difference(b,sb)>2.5/255.) return fallback; // True-frame UI.
    if(max(max(a.r,a.g),a.b)<1./255. && max(max(b.r,b.g),b.b)<1./255.) return fallback; // Fixed black border.
    float4 f=flowAt(forwardFlow,uv),r=flowAt(backwardFlow,uv);
    float4 checkR=flowAt(backwardFlow,uv+f.xy/grid.xy);
    if(f.z<.5 || r.z<.5 || checkR.z<.5 || length(f.xy+checkR.xy)>1.25) return fallback;
    float2 oldUV=uv-info.z*f.xy/grid.xy,newUV=uv-(1-info.z)*r.xy/grid.xy;
    if(any(oldUV<0) || any(oldUV>1) || any(newUV<0) || any(newUV>1)) return fallback;
    float3 ca=worldColor(oldWorld,oldUV),cb=worldColor(newWorld,newUV);
    if(difference(ca,cb)>.16) return fallback; // Occlusion/effects: no double image.
    moving=length(f.xy)>.05 || length(r.xy)>.05;
    return lerp(ca,cb,info.z);
}
struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
V VS(uint id:SV_VertexID) { V v;v.uv=float2((id<<1)&2,id&2);v.position=float4(v.uv*float2(2,-2)+float2(-1,1),0,1);return v; }
float4 PS(V v):SV_TARGET { bool moving;return float4(colorAt(v.uv,moving),1); }
groupshared uint counts[4];
[numthreads(16,16,1)] void Metric(uint3 id:SV_GroupThreadID,uint index:SV_GroupIndex) {
    if(index==0) { counts[0]=counts[1]=counts[2]=counts[3]=0; } GroupMemoryBarrierWithGroupSync();
    float2 uv=(id.xy+.5)/16; bool moving;float3 c=colorAt(uv,moving);
    bool a=difference(c,oldScreen.SampleLevel(linearClamp,uv,0).rgb)>3./255.;
    bool b=difference(c,newScreen.SampleLevel(linearClamp,uv,0).rgb)>3./255.;
    if(a) InterlockedAdd(counts[0],1);if(b) InterlockedAdd(counts[1],1);
    if(a && b) InterlockedAdd(counts[2],1);if(moving) InterlockedAdd(counts[3],1);
    GroupMemoryBarrierWithGroupSync();
    if(index==0) { InterlockedAdd(totals[0],1); if(counts[0]>=4 && counts[1]>=4 && counts[3]>=4) InterlockedAdd(totals[1],1);
        InterlockedAdd(totals[2],counts[2]); InterlockedAdd(totals[3],counts[3]); }
}
)"; }
    void bind(const Data& data,const Slot& a,const Slot& b) {
        commands->ClearState(); commands->UpdateSubresource(constants.Get(),0,nullptr,&data,0,0);
        auto cb=constants.Get(); commands->CSSetConstantBuffers(0,1,&cb);commands->PSSetConstantBuffers(0,1,&cb);
        auto sampler=linear.Get();commands->CSSetSamplers(0,1,&sampler);commands->PSSetSamplers(0,1,&sampler);
        ID3D11ShaderResourceView* views[]={a.images[0].view.Get(),b.images[0].view.Get(),a.images[1].view.Get(),b.images[1].view.Get(),flow[0].texture.view.Get(),flow[1].texture.view.Get()};
        commands->PSSetShaderResources(0,6,views);commands->CSSetShaderResources(0,6,views);
    }
    void execute(ID3D11DeviceContext* immediate) { ComPtr<ID3D11CommandList> list;check(commands->FinishCommandList(FALSE,&list));immediate->ExecuteCommandList(list.Get(),TRUE); }
public:
    void initialize(ID3D11Device* device,unsigned w,unsigned h) {
        if(vertex) return; width=w;height=h;smallW=(w+7)/8;smallH=(h+7)/8;blocksW=(smallW+7)/8;blocksH=(smallH+7)/8;
        auto compile=[&](const char* entry,const char* profile) {ComPtr<ID3DBlob> result,errors;HRESULT hr=D3DCompile(code(),strlen(code()),nullptr,nullptr,nullptr,entry,profile,D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&result,&errors);if(FAILED(hr)) throw std::runtime_error(errors?static_cast<const char*>(errors->GetBufferPointer()):"block shader compile failed");return result;};
        auto vs=compile("VS","vs_5_0"),ps=compile("PS","ps_5_0"),cs=compile("Reduce","cs_5_0"),ms=compile("Match","cs_5_0"),qs=compile("Metric","cs_5_0");
        check(device->CreateVertexShader(vs->GetBufferPointer(),vs->GetBufferSize(),nullptr,&vertex));check(device->CreatePixelShader(ps->GetBufferPointer(),ps->GetBufferSize(),nullptr,&pixel));
        check(device->CreateComputeShader(cs->GetBufferPointer(),cs->GetBufferSize(),nullptr,&reduce));check(device->CreateComputeShader(ms->GetBufferPointer(),ms->GetBufferSize(),nullptr,&match));check(device->CreateComputeShader(qs->GetBufferPointer(),qs->GetBufferSize(),nullptr,&metric));
        check(device->CreateDeferredContext(0,&commands));
        D3D11_BUFFER_DESC bd{};bd.ByteWidth=sizeof(Data);bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&bd,nullptr,&constants));
        D3D11_SAMPLER_DESC sd{};sd.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR;sd.AddressU=sd.AddressV=sd.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;sd.MaxLOD=D3D11_FLOAT32_MAX;check(device->CreateSamplerState(&sd,&linear));
        auto image=[&](Image& out,unsigned iw,unsigned ih,DXGI_FORMAT format) {D3D11_TEXTURE2D_DESC td{};td.Width=iw;td.Height=ih;td.MipLevels=td.ArraySize=1;td.Format=format;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_UNORDERED_ACCESS|D3D11_BIND_SHADER_RESOURCE;check(device->CreateTexture2D(&td,nullptr,&out.texture.texture));check(device->CreateShaderResourceView(out.texture.texture.Get(),nullptr,&out.texture.view));check(device->CreateUnorderedAccessView(out.texture.texture.Get(),nullptr,&out.target));};
        for(auto& g:gray) image(g,smallW,smallH,DXGI_FORMAT_R32_FLOAT);for(auto& f:flow) image(f,blocksW,blocksH,DXGI_FORMAT_R32G32B32A32_FLOAT);
        bd.ByteWidth=16;bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_UNORDERED_ACCESS;bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;bd.StructureByteStride=4;UINT zeros[4]{};D3D11_SUBRESOURCE_DATA initial{zeros,0,0};check(device->CreateBuffer(&bd,&initial,&counters));
        D3D11_UNORDERED_ACCESS_VIEW_DESC ud{};ud.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;ud.Buffer.NumElements=4;check(device->CreateUnorderedAccessView(counters.Get(),&ud,&counterTarget));
        bd.Usage=D3D11_USAGE_STAGING;bd.CPUAccessFlags=D3D11_CPU_ACCESS_READ;bd.BindFlags=bd.MiscFlags=bd.StructureByteStride=0;check(device->CreateBuffer(&bd,nullptr,&counterRead));
    }
    Data data(const Slot& a,const Slot& b,double phase) const {
        Data d{{float(width),float(height),float(std::clamp(phase,0.,1.)),float(b.packet.flip)},{float(smallW),float(smallH),float(blocksW),float(blocksH)},{}};
        memcpy(d.boxes[0],a.packet.textures+3,16);memcpy(d.boxes[1],a.packet.pulse,16);memcpy(d.boxes[2],b.packet.textures+3,16);memcpy(d.boxes[3],b.packet.pulse,16);return d;
    }
    void prepare(ID3D11Device* device,ID3D11DeviceContext* immediate,const Slot& a,const Slot& b,unsigned w,unsigned h) {
        initialize(device,w,h);auto d=data(a,b,0);bind(d,a,b);
        ID3D11ShaderResourceView* none[8]{};ID3D11UnorderedAccessView* noTarget=nullptr;
        commands->PSSetShaderResources(0,8,none);commands->CSSetShaderResources(4,2,none);
        commands->CSSetShader(reduce.Get(),nullptr,0);
        for(int i=0;i<2;i++) {auto view=(i?b:a).images[0].view.Get();commands->CSSetShaderResources(0,1,&view);auto target=gray[i].target.Get();commands->CSSetUnorderedAccessViews(0,1,&target,nullptr);commands->Dispatch((smallW+7)/8,(smallH+7)/8,1);commands->CSSetUnorderedAccessViews(0,1,&noTarget,nullptr);}
        commands->CSSetShader(match.Get(),nullptr,0);
        for(int i=0;i<2;i++) {ID3D11ShaderResourceView* views[]={gray[i].texture.view.Get(),gray[1-i].texture.view.Get()};commands->CSSetShaderResources(6,2,views);auto target=flow[i].target.Get();commands->CSSetUnorderedAccessViews(1,1,&target,nullptr);commands->Dispatch((blocksW+7)/8,(blocksH+7)/8,1);commands->CSSetUnorderedAccessViews(1,1,&noTarget,nullptr);}
        commands->CSSetShaderResources(0,8,none);execute(immediate);sequence=b.sequence;
    }
    void draw(ID3D11DeviceContext* immediate,ID3D11RenderTargetView* target,const Slot& a,const Slot& b,double phase,Query* timing,bool measure) {
        if(sequence!=b.sequence) phase=phase<.5?0:1;
        auto d=data(a,b,phase);bind(d,a,b);
        if(timing) {commands->Begin(timing->disjoint.Get());commands->End(timing->begin.Get());}
        D3D11_VIEWPORT vp{0,0,float(width),float(height),0,1};commands->RSSetViewports(1,&vp);commands->OMSetRenderTargets(1,&target,nullptr);commands->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);commands->VSSetShader(vertex.Get(),nullptr,0);commands->PSSetShader(pixel.Get(),nullptr,0);commands->Draw(3,0);commands->OMSetRenderTargets(0,nullptr,nullptr);
        if(timing) {commands->End(timing->end.Get());commands->End(timing->disjoint.Get());}
        if(measure) {auto uav=counterTarget.Get();commands->CSSetUnorderedAccessViews(2,1,&uav,nullptr);commands->CSSetShader(metric.Get(),nullptr,0);commands->Dispatch(1,1,1);ID3D11UnorderedAccessView* empty=nullptr;commands->CSSetUnorderedAccessViews(2,1,&empty,nullptr);}
        execute(immediate);
    }
    void save(ID3D11DeviceContext* immediate,const std::wstring& path) {
        if(!vertex) return; // CPU readback AFTER worker stop only, never in a performance interval.
        immediate->CopyResource(counterRead.Get(),counters.Get());D3D11_MAPPED_SUBRESOURCE mapped{};check(immediate->Map(counterRead.Get(),0,D3D11_MAP_READ,0,&mapped));UINT v[4];memcpy(v,mapped.pData,sizeof(v));immediate->Unmap(counterRead.Get(),0);
        FILE* f=nullptr;_wfopen_s(&f,(path+L"/block-flow.txt").c_str(),L"wb");if(f) {fprintf(f,"generated=%u new_picture=%u both_changed_samples=%u moving_samples=%u samples_per_frame=256 threshold=3/255 minimum_samples=4 window=5..45\n",v[0],v[1],v[2],v[3]);fclose(f);}
    }
};
