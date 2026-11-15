// Offline WARP fixture: production drfull.cpp + original stripped DXBC versus
// independently compiled named arithmetic. No Unity process, swapchain or install.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <d3d11_1.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <array>
#include <vector>
#include <fstream>
#include <string>
#include <stdexcept>
#include <iostream>
#include <filesystem>
#include <cstring>
#include <algorithm>
#include <cmath>
#include <atomic>
#include <thread>
#include <cstdlib>
#include <new>
std::atomic<uint64_t> nativeHeapCalls{0};
void* operator new(size_t bytes){nativeHeapCalls.fetch_add(1,std::memory_order_relaxed);if(void* p=std::malloc(bytes?bytes:1))return p;throw std::bad_alloc();}
void operator delete(void* p) noexcept {std::free(p);}
void operator delete(void* p,size_t) noexcept {std::free(p);}
#include "../../native/sfnative/drfull.h"
using Microsoft::WRL::ComPtr;
using namespace drfull;
void check(HRESULT hr){if(FAILED(hr))throw hr;}
void require(bool b,const char* msg){if(!b)throw std::runtime_error(msg);}
struct Reader {
    std::ifstream f;
    explicit Reader(const char* path):f(path,std::ios::binary){require(bool(f),"case file missing");}
    template<class T>T pod(){T v{};f.read(reinterpret_cast<char*>(&v),sizeof(v));require(bool(f),"truncated cases");return v;}
    std::vector<uint8_t> blob(){auto n=pod<uint32_t>();require(n<10000000,"case extent");std::vector<uint8_t>b(n);f.read(reinterpret_cast<char*>(b.data()),n);require(bool(f),"case bytes");return b;}
    std::string text(){auto b=blob();return std::string(b.begin(),b.end());}
};
int dispatch(int op,int token,const void* p){auto n=sf_z_drfull_v1(op,token,p);if(n<=0){std::cerr<<"dispatch "<<op<<" hr="<<std::hex<<sf_z_drfull_v1(Error,0,nullptr)<<std::dec<<"\n";throw std::runtime_error("native refused fixture");}if(op==Create){char env[2]{};Configuration c{GetEnvironmentVariableA("SF_DRFULL_TEST_DEFERRED",env,2)?1u:0u,GetEnvironmentVariableA("SF_DRFULL_TEST_LEGACY_CB",env,2)?0u:1u};require(sf_z_drfull_v1(Configure,int(n),&c)!=0,"fixture configure");}return int(n);}
struct Vertex{float p[3],color[4],uv[2],uv1[2];};
ComPtr<ID3D11Buffer> buffer(ID3D11Device* d,const void* p,UINT n,UINT flags){D3D11_BUFFER_DESC b{};b.ByteWidth=n;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=flags;D3D11_SUBRESOURCE_DATA data{};data.pSysMem=p;ComPtr<ID3D11Buffer> out;check(d->CreateBuffer(&b,p?&data:nullptr,&out));return out;}
ComPtr<ID3DBlob> compile(const std::filesystem::path& path,const char* entry,const char* profile,int floor,int snap,int point){
    std::string floorValue=std::to_string(floor);D3D_SHADER_MACRO macros[]={{"FLOOR",floorValue.c_str()},{"SNAP",snap?"1":"0"},{"POINT",point?"1":"0"},{nullptr,nullptr}};
    ComPtr<ID3DBlob> code,error;HRESULT hr=D3DCompileFromFile(path.c_str(),macros,D3D_COMPILE_STANDARD_FILE_INCLUDE,entry,profile,D3DCOMPILE_OPTIMIZATION_LEVEL3|D3DCOMPILE_IEEE_STRICTNESS,0,&code,&error);
    if(FAILED(hr)){if(error)std::cerr<<static_cast<const char*>(error->GetBufferPointer());check(hr);}return code;
}
std::vector<uint8_t> readback(ID3D11Device* d,ID3D11DeviceContext* c,ID3D11Texture2D* texture){D3D11_TEXTURE2D_DESC td{};texture->GetDesc(&td);td.BindFlags=0;td.Usage=D3D11_USAGE_STAGING;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;ComPtr<ID3D11Texture2D> staging;check(d->CreateTexture2D(&td,nullptr,&staging));c->CopyResource(staging.Get(),texture);D3D11_MAPPED_SUBRESOURCE map{};check(c->Map(staging.Get(),0,D3D11_MAP_READ,0,&map));std::vector<uint8_t> bytes(size_t(td.Width)*td.Height*4);for(UINT y=0;y<td.Height;y++)std::memcpy(bytes.data()+size_t(y)*td.Width*4,static_cast<const uint8_t*>(map.pData)+size_t(y)*map.RowPitch,td.Width*4);c->Unmap(staging.Get(),0);return bytes;}
void ppm(const std::filesystem::path& path,const std::vector<uint8_t>& bytes){std::ofstream f(path,std::ios::binary);f<<"P6\n192 128\n255\n";for(size_t i=0;i<bytes.size();i+=4)f.write(reinterpret_cast<const char*>(bytes.data()+i),3);}
std::vector<uint64_t> pipelineState(ID3D11DeviceContext* c){
    std::vector<uint64_t> result;
    auto scalar=[&](uint64_t n){result.push_back(n);};
    auto object=[&](IUnknown* p){scalar(reinterpret_cast<uintptr_t>(p));if(p)p->Release();};
    auto bytes=[&](const void* p,size_t n){auto b=static_cast<const uint8_t*>(p);for(size_t i=0;i<n;i++)scalar(b[i]);};
    ID3D11InputLayout* layout=nullptr;c->IAGetInputLayout(&layout);object(layout);
    ID3D11Buffer* buffers[8]{};UINT strides[8]{},offsets[8]{};c->IAGetVertexBuffers(0,8,buffers,strides,offsets);for(auto b:buffers)object(b);bytes(strides,sizeof(strides));bytes(offsets,sizeof(offsets));
    ID3D11Buffer* index=nullptr;DXGI_FORMAT format{};UINT offset=0;c->IAGetIndexBuffer(&index,&format,&offset);object(index);scalar(format);scalar(offset);
    D3D11_PRIMITIVE_TOPOLOGY topology{};c->IAGetPrimitiveTopology(&topology);scalar(topology);
    ID3D11VertexShader* vs=nullptr;ID3D11PixelShader* ps=nullptr;ID3D11GeometryShader* gs=nullptr;ID3D11HullShader* hs=nullptr;ID3D11DomainShader* ds=nullptr;ID3D11ComputeShader* cs=nullptr;
    c->VSGetShader(&vs,nullptr,nullptr);c->PSGetShader(&ps,nullptr,nullptr);c->GSGetShader(&gs,nullptr,nullptr);c->HSGetShader(&hs,nullptr,nullptr);c->DSGetShader(&ds,nullptr,nullptr);c->CSGetShader(&cs,nullptr,nullptr);
    object(vs);object(ps);object(gs);object(hs);object(ds);object(cs);
    ComPtr<ID3D11DeviceContext1> c1;check(c->QueryInterface(IID_PPV_ARGS(&c1)));
    for(int stage=0;stage<3;stage++){
        ID3D11ShaderResourceView* views[128]{};ID3D11SamplerState* samplers[16]{};ID3D11Buffer* cb[14]{};UINT first[14]{},count[14]{};
        if(stage==0){c->VSGetShaderResources(0,128,views);c->VSGetSamplers(0,16,samplers);c1->VSGetConstantBuffers1(0,14,cb,first,count);}
        if(stage==1){c->PSGetShaderResources(0,128,views);c->PSGetSamplers(0,16,samplers);c1->PSGetConstantBuffers1(0,14,cb,first,count);}
        if(stage==2){c->CSGetShaderResources(0,128,views);c->CSGetSamplers(0,16,samplers);c1->CSGetConstantBuffers1(0,14,cb,first,count);}
        for(auto p:views)object(p);for(auto p:samplers)object(p);for(auto p:cb)object(p);bytes(first,sizeof(first));bytes(count,sizeof(count));
    }
    ID3D11UnorderedAccessView* uav[8]{};c->CSGetUnorderedAccessViews(0,8,uav);for(auto p:uav)object(p);
    ID3D11RenderTargetView* rt[8]{};ID3D11DepthStencilView* depth=nullptr;c->OMGetRenderTargets(8,rt,&depth);for(auto p:rt)object(p);object(depth);
    ID3D11BlendState* blend=nullptr;FLOAT factor[4]{};UINT mask=0;c->OMGetBlendState(&blend,factor,&mask);object(blend);bytes(factor,sizeof(factor));scalar(mask);
    ID3D11DepthStencilState* stencil=nullptr;UINT ref=0;c->OMGetDepthStencilState(&stencil,&ref);object(stencil);scalar(ref);
    ID3D11RasterizerState* raster=nullptr;c->RSGetState(&raster);object(raster);
    D3D11_VIEWPORT vp[16]{};UINT n=16;c->RSGetViewports(&n,vp);scalar(n);bytes(vp,n*sizeof(*vp));
    D3D11_RECT rects[16]{};n=16;c->RSGetScissorRects(&n,rects);scalar(n);bytes(rects,n*sizeof(*rects));
    ID3D11Predicate* predicate=nullptr;BOOL value=FALSE;c->GetPredication(&predicate,&value);object(predicate);scalar(value);
    return result;
}
int main(int argc,char** argv){try{
    require(argc==4,"usage: drfull-warp cases.bin reference.hlsl output-dir");
    const std::filesystem::path out=argv[3];std::filesystem::create_directories(out);
    ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context;D3D_FEATURE_LEVEL level{};
    check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&device,&level,&context));require(level>=D3D_FEATURE_LEVEL_11_0,"WARP FL11 required");
    std::cout<<"adapter=WARP featureLevel="<<std::hex<<level<<std::dec<<" productionABI=1\n";
    std::array<ComPtr<ID3D11Texture2D>,5> textures;
    std::array<ComPtr<ID3D11ShaderResourceView>,5> views;
    for(unsigned k=0;k<5;k++){
        std::array<uint8_t,256> pixels{};for(unsigned y=0;y<8;y++)for(unsigned x=0;x<8;x++){unsigned i=(y*8+x)*4;pixels[i]=uint8_t(31+(x*29+k*17)%210);pixels[i+1]=uint8_t(23+(y*31+k*37)%215);pixels[i+2]=uint8_t(39+((x+y)*17+k*23)%200);pixels[i+3]=uint8_t(35+(x*19+y*13+k*43)%215);}
        D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=8;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
        D3D11_SUBRESOURCE_DATA src{};src.pSysMem=pixels.data();src.SysMemPitch=32;check(device->CreateTexture2D(&td,&src,&textures[k]));check(device->CreateShaderResourceView(textures[k].Get(),nullptr,&views[k]));
    }
    D3D11_TEXTURE2D_DESC td{};td.Width=192;td.Height=128;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
    ComPtr<ID3D11Texture2D> target;ComPtr<ID3D11RenderTargetView> rtv;check(device->CreateTexture2D(&td,nullptr,&target));check(device->CreateRenderTargetView(target.Get(),nullptr,&rtv));
    td.Format=DXGI_FORMAT_D24_UNORM_S8_UINT;td.BindFlags=D3D11_BIND_DEPTH_STENCIL;ComPtr<ID3D11Texture2D> depth;ComPtr<ID3D11DepthStencilView>dsv;check(device->CreateTexture2D(&td,nullptr,&depth));check(device->CreateDepthStencilView(depth.Get(),nullptr,&dsv));
    Reader r(argv[1]);require(r.pod<uint32_t>()==0x34575244,"case magic");unsigned count=r.pod<uint32_t>(),passed=0,negative=0;uint64_t allChanged=0;unsigned globalMax=0;
    std::ofstream csv(out/"warp-results.tsv");csv<<"case\tnative_draws\tnon_background_pixels\tchanged\tmax_channel\tl1\n";
    for(unsigned caseIndex=0;caseIndex<count;caseIndex++){
        std::string label=r.text();int floor=r.pod<int>(),snap=r.pod<int>(),point=r.pod<int>(),mode=r.pod<int>(),filter=r.pod<int>();
        bool namedGrab=label.rfind("named",0)==0;
        auto originalVs=r.blob(),originalPs=r.blob();ProgramIn program{};program.vs=originalVs.data();program.ps=originalPs.data();program.vsBytes=UINT(originalVs.size());program.psBytes=UINT(originalPs.size());for(auto& x:program.state)x=r.pod<int>();
        ProgramIn baselineProgram=program;
        std::vector<Buffer> bindings(r.pod<uint32_t>());for(auto& x:bindings)x=r.pod<Buffer>();std::vector<Patch> patches(r.pod<uint32_t>());for(auto& x:patches)x=r.pod<Patch>();auto constants=r.blob();
        struct TextureInput{int stage,slot,sampler,pattern;};std::vector<TextureInput> texInputs(r.pod<uint32_t>());for(auto& x:texInputs)x=r.pod<TextureInput>();auto reference=r.blob();Pose pose{};for(auto& x:pose.world)x=r.pod<float>();for(int i=0;i<16;i++)pose.inverse[i]=i%5==0?1.f:0.f;
        Frame frame{};for(auto& x:frame.vp)x=r.pod<float>();for(auto& x:frame.screen)x=r.pod<float>();
        bool sprite=mode%2==0;bool multi=mode==3;
        std::array<Vertex,5> vertices{};float positions[4][2]={{-.75f,-.65f},{.72f,-.65f},{.72f,.69f},{-.75f,.69f}};
        const float uniform[4]={.61f,.84f,.47f,.71f};
        for(unsigned i=0;i<4;i++){auto& v=vertices[i+1];v.p[0]=positions[i][0];v.p[1]=positions[i][1];v.p[2]=0;for(int j=0;j<4;j++)v.color[j]=sprite?(floor==4?1.f:uniform[j]):.35f+float((i+unsigned(j)*3)%5)*.12f;v.uv[0]=(i==1||i==2)?1.07f:-.09f;v.uv[1]=(i>=2)?(mode==1?1.85f:1.07f):(mode==1?1.05f:-.07f);v.uv1[0]=positions[i][0]*.81f;v.uv1[1]=positions[i][1]*.77f;}
        if(sprite && floor<2)std::memcpy(constants.data(),uniform,16);
        std::array<uint32_t,9> indices={0,0,0,0,1,2,0,2,3};
        Attribute attributes[]={{0,0,6,0,0},{3,0,2,multi?1u:0u,multi?0u:12u},{4,0,16,0,28},{4,1,16,0,36}};
        GeometryIn geometry{};geometry.attributes=attributes;geometry.attributeCount=4;geometry.strides[0]=sizeof(Vertex);
        std::array<std::array<float,4>,5> colors{};for(size_t i=0;i<5;i++)std::memcpy(colors[i].data(),vertices[i].color,16);
        auto vb=buffer(device.Get(),vertices.data(),UINT(sizeof(vertices)),D3D11_BIND_VERTEX_BUFFER);auto ib=buffer(device.Get(),indices.data(),UINT(sizeof(indices)),D3D11_BIND_INDEX_BUFFER);auto colorBuffer=buffer(device.Get(),colors.data(),UINT(sizeof(colors)),D3D11_BIND_VERTEX_BUFFER);
        if(sprite){geometry.vertices=vertices.data()+1;geometry.vertexBytes=4*sizeof(Vertex);geometry.cpuIndices=indices.data()+3;geometry.cpuIndexCount=6;}
        else {geometry.buffers[0]=vb.Get();if(multi){geometry.buffers[1]=colorBuffer.Get();geometry.strides[1]=16;}geometry.indices=ib.Get();geometry.indexFormat=DXGI_FORMAT_R32_UINT;geometry.indexStart=3;geometry.indexCount=6;geometry.baseVertex=1;}
        CreateIn create{target.Get(),16,1};int token=dispatch(Create,0,&create);int programId=dispatch(Program,token,&program),geometryId=dispatch(Geometry,token,&geometry);
        std::vector<Texture> tex;for(const auto& t:texInputs)tex.push_back(Texture{t.pattern==5?nullptr:textures[size_t(t.pattern)].Get(),t.stage,t.slot,t.sampler,t.pattern==5?1:filter,t.pattern==5?1:mode%2,t.pattern==5?1:mode%2,1,t.pattern==5?(namedGrab?-2:-1):0});
        DrawIn draw{};draw.program=UINT(programId);draw.geometry=UINT(geometryId);draw.buffers=bindings.data();draw.bufferCount=UINT(bindings.size());draw.patches=patches.data();draw.patchCount=UINT(patches.size());draw.textures=tex.data();draw.textureCount=UINT(tex.size());draw.constants=constants.data();draw.constantBytes=UINT(constants.size());draw.sprite=sprite?1u:0u;draw.grab=(floor==2||floor==3)?(namedGrab?2u:1u):0u;
        draw.extent[0]=draw.extent[1]=1;draw.extent[2]=.1f;
        int drawId=dispatch(Draw,token,&draw);uint32_t poseIds[]={0},drawIds[]={UINT(drawId)},offsets[]={0},list[]={UINT(drawId),UINT(drawId)},segs[]={0,mode==2?2u:1u};
        frame.poses=&pose;frame.poseIds=poseIds;frame.poseCount=1;frame.drawIds=drawIds;frame.constantOffsets=offsets;frame.constants=constants.data();frame.list=list;frame.segments=segs;frame.listCount=segs[1];frame.segmentCount=1;frame.listChanged=1;
        dispatch(Commit,token,&frame);
        const float background[]={.13f,.07f,.19f,.21f};ID3D11RenderTargetView* rt=rtv.Get();D3D11_VIEWPORT vp{0,0,192,128,0,1};context->OMSetRenderTargets(1,&rt,dsv.Get());context->RSSetViewports(1,&vp);context->ClearRenderTargetView(rt,background);context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,0,0);
        auto event=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(Event,0,nullptr));event(token<<16);
        Statistics stats{};dispatch(Stats,token,&stats);require(stats.frames==1&&stats.draws==segs[1]&&stats.errors==0,"original shader did not draw");
        auto original=readback(device.Get(),context.Get(),target.Get());
        // Independently compiled reference binds by names/own layout, never original
        // cbuffer slots or variable offsets. External VB route tests submesh/baseVertex.
        auto refVs=compile(argv[2],"vertex","vs_4_0",floor,snap,point),refPs=compile(argv[2],"pixel","ps_4_0",floor,snap,point);
        ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;check(device->CreateVertexShader(refVs->GetBufferPointer(),refVs->GetBufferSize(),nullptr,&vs));check(device->CreatePixelShader(refPs->GetBufferPointer(),refPs->GetBufferSize(),nullptr,&ps));
        D3D11_INPUT_ELEMENT_DESC input[]={{"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},{"COLOR",0,DXGI_FORMAT_R32G32B32A32_FLOAT,multi?1u:0u,multi?0u:12u,D3D11_INPUT_PER_VERTEX_DATA,0},{"TEXCOORD",0,DXGI_FORMAT_R32G32_FLOAT,0,28,D3D11_INPUT_PER_VERTEX_DATA,0},{"TEXCOORD",1,DXGI_FORMAT_R32G32_FLOAT,0,36,D3D11_INPUT_PER_VERTEX_DATA,0}};
        ComPtr<ID3D11InputLayout> layout;check(device->CreateInputLayout(input,4,refVs->GetBufferPointer(),refVs->GetBufferSize(),&layout));
        auto cb=buffer(device.Get(),reference.data(),UINT(reference.size()),D3D11_BIND_CONSTANT_BUFFER);ID3D11Buffer* cbuffer=cb.Get();context->VSSetConstantBuffers(0,1,&cbuffer);context->PSSetConstantBuffers(0,1,&cbuffer);context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);context->IASetInputLayout(layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        ID3D11Buffer* vbs[]={vb.Get(),colorBuffer.Get()};UINT strides[]={sizeof(Vertex),16},offset[]={0,0};context->IASetVertexBuffers(0,2,vbs,strides,offset);context->IASetIndexBuffer(ib.Get(),DXGI_FORMAT_R32_UINT,0);
        std::array<ComPtr<ID3D11SamplerState>,5> samplers;for(size_t j=0;j<5;j++){D3D11_SAMPLER_DESC sd{};sd.Filter=filter?D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT:D3D11_FILTER_MIN_MAG_MIP_POINT;sd.AddressU=sd.AddressV=sd.AddressW=mode%2?D3D11_TEXTURE_ADDRESS_CLAMP:D3D11_TEXTURE_ADDRESS_WRAP;sd.MaxLOD=D3D11_FLOAT32_MAX;check(device->CreateSamplerState(&sd,&samplers[j]));ID3D11ShaderResourceView* srv=views[j].Get();context->PSSetShaderResources(UINT(j),1,&srv);ID3D11SamplerState* sm=samplers[j].Get();context->PSSetSamplers(UINT(j),1,&sm);}
        D3D11_BLEND_DESC bd{};auto& blend=bd.RenderTarget[0];blend.BlendEnable=TRUE;blend.SrcBlend=blend.SrcBlendAlpha=D3D11_BLEND_ONE;blend.DestBlend=blend.DestBlendAlpha=D3D11_BLEND_INV_SRC_ALPHA;blend.BlendOp=blend.BlendOpAlpha=D3D11_BLEND_OP_ADD;blend.RenderTargetWriteMask=15;ComPtr<ID3D11BlendState> blendState;check(device->CreateBlendState(&bd,&blendState));context->OMSetBlendState(blendState.Get(),nullptr,~0u);
        D3D11_RASTERIZER_DESC rd{};rd.FillMode=D3D11_FILL_SOLID;rd.CullMode=D3D11_CULL_NONE;rd.DepthClipEnable=TRUE;ComPtr<ID3D11RasterizerState> raster;check(device->CreateRasterizerState(&rd,&raster));context->RSSetState(raster.Get());D3D11_DEPTH_STENCIL_DESC dd{};dd.DepthEnable=FALSE;ComPtr<ID3D11DepthStencilState> depthState;check(device->CreateDepthStencilState(&dd,&depthState));context->OMSetDepthStencilState(depthState.Get(),0);
        context->ClearRenderTargetView(rt,background);
        ComPtr<ID3D11Texture2D> grab;ComPtr<ID3D11ShaderResourceView> grabSrv;
        if(floor==2||floor==3){D3D11_TEXTURE2D_DESC desc{};target->GetDesc(&desc);desc.BindFlags=D3D11_BIND_SHADER_RESOURCE;check(device->CreateTexture2D(&desc,nullptr,&grab));check(device->CreateShaderResourceView(grab.Get(),nullptr,&grabSrv));}
        for(unsigned repeat=0;repeat<segs[1];repeat++){
            if((floor==2||floor==3)&&(!namedGrab||repeat==0)){ID3D11ShaderResourceView* nil=nullptr;context->PSSetShaderResources(1,1,&nil);context->CopyResource(grab.Get(),target.Get());auto* srv=grabSrv.Get();context->PSSetShaderResources(1,1,&srv);}
            context->DrawIndexed(6,3,1);
        }auto ref=readback(device.Get(),context.Get(),target.Get());
        uint64_t changed=0,l1=0,nonBackground=0;unsigned maxChannel=0;for(size_t i=0;i<original.size();i+=4){bool diff=false,bg=false;for(size_t j=0;j<4;j++){unsigned delta=unsigned(std::abs(int(original[i+j])-int(ref[i+j])));maxChannel=std::max(maxChannel,delta);l1+=delta;diff|=delta!=0;bg|=original[i+j]!=uint8_t(std::round(background[j]*255));}changed+=diff;nonBackground+=bg;}
        csv<<label<<'\t'<<stats.draws<<'\t'<<nonBackground<<'\t'<<changed<<'\t'<<maxChannel<<'\t'<<l1<<'\n';csv.flush();
        if(caseIndex==0||maxChannel>2){ppm(out/(label+"-original.ppm"),original);ppm(out/(label+"-reference.ppm"),ref);}
        require(nonBackground>1000,"blank original falsely looks equal");
        std::cout<<label<<" nonblank="<<nonBackground<<" changed="<<changed<<" max="<<maxChannel<<" L1="<<l1<<"\n";
        require(maxChannel<=2,"original/reference pixel mismatch");globalMax=std::max(globalMax,maxChannel);allChanged+=changed;
        if(floor==2||floor==3){
            GpuPerformance gp{};dispatch(GpuPerf,token,&gp);uint64_t copies=namedGrab?1u:segs[1];
            require(gp.grabCopies==copies&&gp.grabBytes==copies*192*128*4,"named/unnamed Grab copy count or bytes");
            if(mode==2){
                uint32_t split[]={0,1,1,1};frame.segments=split;frame.segmentCount=2;
                context->ClearRenderTargetView(rt,background);dispatch(Commit,token,&frame);event(token<<16);event((token<<16)|1);
                require(readback(device.Get(),context.Get(),target.Get())==original,"Grab cache did not reset per frame/persist across segments");
                GpuPerformance after{};dispatch(GpuPerf,token,&after);require(after.grabCopies-gp.grabCopies==copies,"Grab frame/segment copy scope");
                if(namedGrab){
                    // A second name must take its own snapshot after the first draw.
                    for(auto& t:tex)if(t.srgb==-2)t.srgb=-3;draw.grab=3;
                    int other=dispatch(Draw,token,&draw);list[1]=UINT(other);
                    context->ClearRenderTargetView(rt,background);dispatch(Commit,token,&frame);event(token<<16);event((token<<16)|1);
                    GpuPerformance isolated{};dispatch(GpuPerf,token,&isolated);require(isolated.grabCopies-after.grabCopies==2,"two named Grabs incorrectly alias one surface");
                    list[1]=UINT(drawId);draw.grab=2;for(auto& t:tex)if(t.srgb==-3)t.srgb=-2;
                }
                frame.segments=segs;frame.segmentCount=1;
            }
            std::cout<<"PASS M4g Grab "<<label<<" copies="<<copies<<" draws="<<segs[1]<<" bytes="<<copies*192*128*4<<" frame_reset=True segment_scope=True name_isolation=True pixels=identical\n";
            if(caseIndex==0){
                MemoryPerformance allocated{};dispatch(MemoryPerf,token,&allocated);
                require(allocated.grabBytes==192*128*4,"Grab logical allocation is not one deduplicated surface");
                float saved=pose.world[12];pose.world[12]=100;frame.poseCount=1;
                auto before=gp.grabCopies;dispatch(GpuPerf,token,&gp);before=gp.grabCopies;
                for(int idle=0;idle<514;idle++){dispatch(Commit,token,&frame);event(token<<16);}
                MemoryPerformance retired{};dispatch(MemoryPerf,token,&retired);dispatch(GpuPerf,token,&gp);
                require(retired.grabBytes==0&&gp.grabCopies==before,"offscreen Grab copied or idle snapshot retained");
                pose.world[12]=saved;context->ClearRenderTargetView(rt,background);dispatch(Commit,token,&frame);event(token<<16);
                require(readback(device.Get(),context.Get(),target.Get())==original,"retired Grab did not recreate exact snapshot");
                dispatch(MemoryPerf,token,&retired);require(retired.grabBytes==192*128*4,"Grab reallocation count");
                std::cout<<"PASS M4h Grab memory allocated="<<allocated.grabBytes<<" idle_bytes=0 restored="<<retired.grabBytes<<" offscreen_copies=0 pixels=identical named="<<namedGrab<<"\n";
                // Fail after entering the isolated state, not just before a bad
                // segment is validated. Grab intentionally rejects MSAA sources.
                D3D11_TEXTURE2D_DESC msaaDesc{};target->GetDesc(&msaaDesc);msaaDesc.SampleDesc.Count=2;
                ComPtr<ID3D11Texture2D> msaaTarget;ComPtr<ID3D11RenderTargetView> msaaView;
                check(device->CreateTexture2D(&msaaDesc,nullptr,&msaaTarget));check(device->CreateRenderTargetView(msaaTarget.Get(),nullptr,&msaaView));
                auto* msaaRt=msaaView.Get();context->OMSetRenderTargets(1,&msaaRt,nullptr);auto stateBeforeError=pipelineState(context.Get());
                dispatch(Commit,token,&frame);event(token<<16);Statistics failed{};dispatch(Stats,token,&failed);
                require(failed.errors==1&&stateBeforeError==pipelineState(context.Get()),"active Grab error did not restore full pipeline state");
                context->OMSetRenderTargets(1,&rt,dsv.Get());
                std::cout<<"PASS M4i active-scope-error MSAA_Grab=rejected state=restored errors=1\n";
            }
        }
        if(caseIndex==0 && floor<2){
            // Non-symmetric matrix transposition is a required negative control.
            auto bad=reference;float* matrix=reinterpret_cast<float*>(bad.data());for(int c=0;c<4;c++)for(int row=0;row<c;row++)std::swap(matrix[c*4+row],matrix[row*4+c]);context->UpdateSubresource(cb.Get(),0,nullptr,bad.data(),0,0);context->ClearRenderTargetView(rt,background);context->DrawIndexed(6,3,1);auto badImage=readback(device.Get(),context.Get(),target.Get());unsigned errors=0;for(size_t i=0;i<badImage.size();i+=4)if(std::memcmp(badImage.data()+i,original.data()+i,4)!=0)errors++;require(errors>1000,"matrix control failed to expose wrong layout");negative++;
            // ExecuteCommandList(TRUE) must restore Unity's immediate state.
            // Non-null sentinels in states that native compute/ClearState touch.
            D3D11_BUFFER_DESC sentinelDesc{};sentinelDesc.ByteWidth=64;sentinelDesc.Usage=D3D11_USAGE_DEFAULT;sentinelDesc.BindFlags=D3D11_BIND_UNORDERED_ACCESS;sentinelDesc.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
            ComPtr<ID3D11Buffer> sentinelBuffer;check(device->CreateBuffer(&sentinelDesc,nullptr,&sentinelBuffer));D3D11_UNORDERED_ACCESS_VIEW_DESC sentinelView{};sentinelView.Format=DXGI_FORMAT_R32_TYPELESS;sentinelView.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;sentinelView.Buffer.NumElements=16;sentinelView.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;
            ComPtr<ID3D11UnorderedAccessView> sentinelUav;check(device->CreateUnorderedAccessView(sentinelBuffer.Get(),&sentinelView,&sentinelUav));auto* sentinel=sentinelUav.Get();context->CSSetUnorderedAccessViews(0,1,&sentinel,nullptr);
            const char csSource[]="[numthreads(1,1,1)] void main() {}";ComPtr<ID3DBlob> sentinelCode,sentinelError;check(D3DCompile(csSource,sizeof(csSource)-1,nullptr,nullptr,nullptr,"main","cs_5_0",0,0,&sentinelCode,&sentinelError));ComPtr<ID3D11ComputeShader> sentinelShader;check(device->CreateComputeShader(sentinelCode->GetBufferPointer(),sentinelCode->GetBufferSize(),nullptr,&sentinelShader));context->CSSetShader(sentinelShader.Get(),nullptr,0);
            const char gsSource[]="struct V {float4 p:SV_Position;};[maxvertexcount(3)] void main(triangle V v[3],inout TriangleStream<V> o){o.Append(v[0]);o.Append(v[1]);o.Append(v[2]);}";check(D3DCompile(gsSource,sizeof(gsSource)-1,nullptr,nullptr,nullptr,"main","gs_5_0",0,0,&sentinelCode,&sentinelError));ComPtr<ID3D11GeometryShader> sentinelGs;check(device->CreateGeometryShader(sentinelCode->GetBufferPointer(),sentinelCode->GetBufferSize(),nullptr,&sentinelGs));context->GSSetShader(sentinelGs.Get(),nullptr,0);
            ComPtr<ID3D11DeviceContext1> context1;check(context->QueryInterface(IID_PPV_ARGS(&context1)));std::array<UINT,256> sentinelData{};auto sentinelCb=buffer(device.Get(),sentinelData.data(),1024,D3D11_BIND_CONSTANT_BUFFER);ID3D11Buffer* ranged=sentinelCb.Get();UINT cbFirst=16,cbCount=16;context1->VSSetConstantBuffers1(13,1,&ranged,&cbFirst,&cbCount);context1->PSSetConstantBuffers1(13,1,&ranged,&cbFirst,&cbCount);context1->CSSetConstantBuffers1(13,1,&ranged,&cbFirst,&cbCount);
            D3D11_QUERY_DESC predicateDesc{D3D11_QUERY_OCCLUSION_PREDICATE,0};ComPtr<ID3D11Predicate> predicate;check(device->CreatePredicate(&predicateDesc,&predicate));context->Begin(predicate.Get());context->End(predicate.Get());context->SetPredication(predicate.Get(),TRUE);
            context->UpdateSubresource(cb.Get(),0,nullptr,reference.data(),0,0);auto beforeState=pipelineState(context.Get());dispatch(Commit,token,&frame);event(token<<16);require(beforeState==pipelineState(context.Get()),"native corrupted full pipeline state/ranges");negative++;
            context->SetPredication(nullptr,FALSE);sentinel=nullptr;context->CSSetUnorderedAccessViews(0,1,&sentinel,nullptr);context->CSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);ranged=nullptr;context1->VSSetConstantBuffers1(13,1,&ranged,nullptr,nullptr);context1->PSSetConstantBuffers1(13,1,&ranged,nullptr,nullptr);context1->CSSetConstantBuffers1(13,1,&ranged,nullptr,nullptr);
            SubmissionPerformance submissionState{};dispatch(SubmissionPerf,token,&submissionState);
            if(submissionState.immediateSegments)require(submissionState.immediateAvailable&&submissionState.stateRestores==submissionState.immediateSegments,"immediate state restoration count");
            else require(submissionState.deferredSegments>0,"no submission path exercised");
            Configuration late{1,0};require(sf_z_drfull_v1(Configure,token,&late)==0,"mode changed with in-flight renderer");
            std::cout<<"PASS M4i state IA=8 shaders=6 SRV=384 samplers=48 CB_ranges=42 UAV=8 RT=8 depth_blend_raster_viewport_scissor_predication=restored immediate="<<submissionState.immediateSegments<<" deferred="<<submissionState.deferredSegments<<" ring_bindings="<<submissionState.ringBindings<<" late_config=rejected\n";
            // Incremental constant packet must affect an existing draw, then restore.
            uint32_t dirtyOffsets[]={0,UINT(constants.size())};frame.dirtyCount=1;frame.constantBytes=UINT(constants.size());frame.constantOffsets=dirtyOffsets;
            std::array<uint8_t,16> savedColor{};std::memcpy(savedColor.data(),constants.data(),16);std::memset(constants.data(),0,16);
            context->ClearRenderTargetView(rt,background);dispatch(Commit,token,&frame);event(token<<16);auto zero=readback(device.Get(),context.Get(),target.Get());unsigned visible=0;for(size_t i=0;i<zero.size();i+=4)for(size_t j=0;j<4;j++)visible+=zero[i+j]!=uint8_t(std::round(background[j]*255));require(visible==0,"dirty constant update not applied");
            std::memcpy(constants.data(),savedColor.data(),16);context->ClearRenderTargetView(rt,background);dispatch(Commit,token,&frame);event(token<<16);require(readback(device.Get(),context.Get(),target.Get())==original,"dirty constants did not restore exactly");negative++;
            // Conservative native clip-volume culling uses the same world/VP packet.
            frame.dirtyCount=frame.constantBytes=0;frame.constantOffsets=offsets;const float savedX=pose.world[12];pose.world[12]+=100;context->ClearRenderTargetView(rt,background);Statistics prior{};dispatch(Stats,token,&prior);dispatch(Commit,token,&frame);event(token<<16);Statistics culled{};dispatch(Stats,token,&culled);require(culled.draws==prior.draws&&culled.errors==0,"offscreen bounds not culled");negative++;
            // Unified ShaderLab stencil must apply even with default front/back
            // metadata. Equal/ref=1 rejects cleared 0 and accepts cleared 1.
            pose.world[12]=savedX;program.state[16]=1;program.state[20]=3;draw.program=UINT(dispatch(Program,token,&program));int maskedDraw=dispatch(Draw,token,&draw);list[0]=UINT(maskedDraw);
            context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_STENCIL,0,0);context->ClearRenderTargetView(rt,background);dispatch(Commit,token,&frame);event(token<<16);auto reject=readback(device.Get(),context.Get(),target.Get());visible=0;for(size_t i=0;i<reject.size();i+=4)for(size_t j=0;j<4;j++)visible+=reject[i+j]!=uint8_t(std::round(background[j]*255));require(visible==0,"unified stencil comparison ignored");
            context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_STENCIL,0,1);context->ClearRenderTargetView(rt,background);dispatch(Commit,token,&frame);event(token<<16);require(readback(device.Get(),context.Get(),target.Get())==original,"matching unified stencil rejected original draw");negative++;
            Performance perfBefore{},perfAfter{};dispatch(Perf,token,&perfBefore);
            context->ClearRenderTargetView(rt,background);dispatch(Commit,token,&frame);event(token<<16);dispatch(Perf,token,&perfAfter);
            require(perfBefore.uploads==perfAfter.uploads,"stable constants/color uploaded again");
            require(readback(device.Get(),context.Get(),target.Get())==original,"stable skipped uploads changed pixels");
            dispatch(Draw,token,&draw);Performance cache{};dispatch(Perf,token,&cache);
            require(cache.layoutCreates==perfAfter.layoutCreates&&cache.layoutHits>perfAfter.layoutHits,"identical layout recreated");
            std::cout<<"PASS stable-frame uploads=0 pixels=identical layout-cache-hit=1\n";
            // Packed poses carry a dense value array and sparse native IDs.
            // ID 5 intentionally cannot index the one-element CPU pose array.
            draw.pose=5;int packedDraw=dispatch(Draw,token,&draw);list[0]=UINT(packedDraw);poseIds[0]=5;
            context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,token,&frame);event(token<<16);
            require(readback(device.Get(),context.Get(),target.Get())==original,"dense packed pose / sparse native ID mismatch");
            std::array<float,16> sparseWorld{};std::memcpy(sparseWorld.data(),pose.world,64);frame.poses=reinterpret_cast<const Pose*>(sparseWorld.data());FrameActive sparseActive{frame,nullptr,nullptr,0};
            context->ClearRenderTargetView(rt,background);dispatch(CommitActiveWorld,token,&sparseActive);event(token<<16);require(readback(device.Get(),context.Get(),target.Get())==original,"world-only packed pose / sparse native ID mismatch");frame.poses=&pose;
            std::cout<<"PASS world-only poses dense-count=1 sparse-native-id=5 pixels=identical\n";
            draw.pose=0;list[0]=UINT(maskedDraw);poseIds[0]=0;
            context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,token,&frame);event(token<<16);
            std::cout<<"PASS packed-poses dense-count=1 sparse-native-id=5 pixels=identical legacy-commit=preserved\n";
            // Exercise the production SPSC ring while a separate thread owns the
            // D3D context. Every reused slot must retain its own color snapshot.
            constexpr unsigned packets=128;std::atomic<unsigned> submitted{0},consumed{0};std::atomic<bool> valid{true};
            frame.listChanged=0;frame.dirtyCount=1;frame.constantBytes=UINT(constants.size());frame.constantOffsets=dirtyOffsets;drawIds[0]=UINT(maskedDraw);
            std::thread consumer([&](){
                try {
                    for(unsigned sequence=0;sequence<packets;sequence++){
                        while(submitted.load(std::memory_order_acquire)<=sequence)std::this_thread::yield();
                        context->ClearRenderTargetView(rt,background);event(token<<16);
                        auto pixels=readback(device.Get(),context.Get(),target.Get());
                        if(sequence%2){if(pixels!=original)valid=false;}
                        else for(size_t i=0;i<pixels.size();i++)if(pixels[i]!=uint8_t(std::round(background[i%4]*255)))valid=false;
                        consumed.store(sequence+1,std::memory_order_release);
                    }
                }catch(...){valid=false;consumed.store(packets,std::memory_order_release);}
            });
            for(unsigned sequence=0;sequence<packets;sequence++){
                while(sequence-consumed.load(std::memory_order_acquire)>=4&&valid.load())std::this_thread::yield();
                if(sequence%2)std::memcpy(constants.data(),savedColor.data(),16);else std::memset(constants.data(),0,16);
                if(sf_z_drfull_v1(CommitPacked,token,&frame)!=1)valid=false;
                submitted.store(sequence+1,std::memory_order_release);
            }
            consumer.join();Statistics concurrent{};dispatch(Stats,token,&concurrent);
            require(valid&&concurrent.errors==0&&concurrent.dropped==0,"SPSC packet overwrite/color snapshot/concurrent render failed");
            std::cout<<"PASS SPSC packets="<<packets<<" alternating-color-pixels=identical errors=0 dropped=0\n";
        }
        if(caseIndex==0&&floor<2){
            auto marker=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(TimelineEvent,token,nullptr));
            require(marker!=nullptr,"timeline callback missing");
            auto mark=[&](int id){marker((token<<16)|id);};
            uint32_t savedSecond=list[1];list[1]=list[0]; // same binding in both segments
            uint32_t split[]={0,1,1,1};frame.segments=split;frame.segmentCount=2;frame.listCount=2;frame.list=list;frame.listChanged=1;frame.dirtyCount=frame.constantBytes=0;frame.poseCount=0;
            for(int j=0;j<192;j++){
                mark(2);context->ClearRenderTargetView(rt,background);mark(3);
                dispatch(Commit,token,&frame);mark(0);mark(16);
                event(token<<16);mark(5);context->ClearRenderTargetView(rt,background);mark(7);mark(17);mark(18);
                event((token<<16)|1);mark(6);context->ClearRenderTargetView(rt,background);mark(7);mark(19);mark(20);
                context->ClearRenderTargetView(rt,background);mark(21);mark(1);
                (void)readback(device.Get(),context.Get(),target.Get());frame.listChanged=0;
            }
            TimelinePerformance t{};dispatch(TimelinePerf,token,&t);
            double categories=t.nativeUs+t.explicitUs+t.compositeUs+t.otherUs;
            double phases=t.opaqueUs+t.gapUs+t.alphaUs+t.postAlphaUs+t.effectsUs+t.tailUs;
            require(t.samples==3&&t.auxiliarySamples==3&&t.invalid==0&&t.disjoint==0&&t.overflow==0&&t.cameraUs>0&&t.nativeUs>0&&t.explicitUs>0&&t.compositeUs>0&&t.auxiliaryUs>0,"timeline scope/sample partition");
            require(std::abs(categories-t.cameraUs)<1e-6&&std::abs(phases-t.cameraUs)<1e-6,"GPU categories/phases do not conserve camera total");
            require(t.constantReuses>0&&t.constantEvaluations>0,"same-frame constant reuse across native segments was not exercised");
            require(t.lastSerial==129&&t.maxSerial>0&&(t.maxSerial-1)%64==0,"memory/GPU observations lost their timestamp-slot serial");
            std::cout<<"PASS M4h constants evaluations="<<t.constantEvaluations<<" reuses="<<t.constantReuses<<" across_segments=True\n";
            mark(0);for(unsigned j=0;j<600;j++)mark(5+j%3);mark(1);
            (void)readback(device.Get(),context.Get(),target.Get());mark(0);mark(1);dispatch(TimelinePerf,token,&t);
            require(t.samples==3&&t.overflow>0&&t.invalid==1,"overflow sample was not rejected");
            MemoryPerformance m{};dispatch(MemoryPerf,token,&m);
            require(m.ownedBytes==m.geometryBytes+m.poseBytes+m.drawBytes+m.indexBytes+m.pretransformBytes+m.constantBytes+m.colorBytes+m.grabBytes+m.canvasColorBytes,"memory categories do not conserve logical bytes");
            require(m.peakOwnedBytes>=m.ownedBytes&&m.queries+m.failures>0,"memory query accounting");
            std::cout<<"PASS M4h memory owned="<<m.ownedBytes<<" conservation=exact deduplicated=True budget_supported="<<m.supported<<" queries="<<m.queries<<" failures="<<m.failures<<" adapter=WARP\n";
            std::cout<<"PASS M4h timeline samples="<<t.samples<<" auxiliary_samples="<<t.auxiliarySamples<<" conservation=exact categories=4 phases=6 invalid_overflow=rejected camera_us="<<t.cameraUs<<" native_frame_us="<<t.nativeUs<<" explicit_us="<<t.explicitUs<<" composite_us="<<t.compositeUs<<" other_us="<<t.otherUs<<" adapter=WARP\n";
            list[1]=savedSecond;frame.segments=segs;frame.segmentCount=1;frame.listCount=segs[1];
        }
        dispatch(Destroy,token,nullptr);event(token<<16); // stale command buffer lease is inert.
        if(floor<2||floor==4){
            // Same original DXBC and source order: per-object matrices versus
            // world-space vertices + identity object matrices, with different poses.
            int batchToken=dispatch(Create,0,&create);
            draw.program=UINT(dispatch(Program,batchToken,&baselineProgram));draw.geometry=UINT(dispatch(Geometry,batchToken,&geometry));
            std::array<uint32_t,6> localIndices={0,1,2,0,2,3};
            if(!sprite){CpuGeometry cpu{draw.geometry,vertices.data()+1,4,localIndices.data(),6};dispatch(GeometryCpu,batchToken,&cpu);}
            std::array<Pose,3> batchPoses={pose,pose,pose};
            batchPoses[1].world[12]+=.19f;batchPoses[1].world[13]-=.11f;
            batchPoses[2].world[0]*=-.7f;batchPoses[2].world[1]*=-.7f;batchPoses[2].world[12]-=.23f;
            uint32_t batchPoseIds[]={0,1,2},singleIds[3]{},batchedIds[3]{},batchSegments[]={0,3},zeroOffsets[]={0};
            std::array<std::vector<uint8_t>,3> colorsForBatch={constants,constants,constants};
            for(unsigned j=0;j<3;j++){
                if(sprite){float tint[4]={.37f+float(j)*.13f,.82f-float(j)*.11f,.61f,.53f+float(j)*.09f};
                    if(floor==4){for(const auto& patch:patches){if(patch.kind==16)std::memcpy(colorsForBatch[j].data()+patch.offset,tint,16);if(patch.kind==17){float flips[2]={j==1?-1.f:1.f,j==2?-1.f:1.f};std::memcpy(colorsForBatch[j].data()+patch.offset,flips,8);}}}
                    else std::memcpy(colorsForBatch[j].data(),tint,16);
                }
                draw.pose=j;draw.constants=colorsForBatch[j].data();draw.sprite=sprite?1u:0u;singleIds[j]=UINT(dispatch(Draw,batchToken,&draw));
                draw.sprite=(sprite?1u:0u)|2u;batchedIds[j]=UINT(dispatch(Draw,batchToken,&draw));
            }
            Frame bf=frame;bf.poses=batchPoses.data();bf.poseIds=batchPoseIds;bf.poseCount=3;bf.dirtyCount=bf.constantBytes=0;bf.constantOffsets=zeroOffsets;bf.list=singleIds;bf.listCount=3;bf.segments=batchSegments;bf.segmentCount=1;bf.listChanged=1;
            context->OMSetRenderTargets(1,&rt,dsv.Get());context->RSSetViewports(1,&vp);context->ClearRenderTargetView(rt,background);context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,0,0);
            dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto singles=readback(device.Get(),context.Get(),target.Get());
            bf.list=batchedIds;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto batchImage=readback(device.Get(),context.Get(),target.Get());
            unsigned batchMax=0;uint64_t batchChanged=0;for(size_t k=0;k<singles.size();k+=4){bool different=false;for(size_t c=0;c<4;c++){unsigned delta=unsigned(std::abs(int(singles[k+c])-int(batchImage[k+c])));batchMax=std::max(batchMax,delta);different|=delta!=0;}batchChanged+=different;}
            BatchPerformance bp{};dispatch(BatchPerf,batchToken,&bp);Statistics bs{};dispatch(Stats,batchToken,&bs);
            require(batchMax<=2&&bp.batches==1&&bp.sources==3&&bs.draws==4&&bs.errors==0,"world-space original-DXBC batch parity/draw reduction");
            std::cout<<"PASS M4f batch "<<label<<" sources=3 draws=1 changed="<<batchChanged<<" max="<<batchMax<<" cb_creates="<<bp.constantCreates<<"\n";
            // A native live edit does not send/rebuild the ordered draw list.
            bf.listChanged=0;bf.poseCount=0;uint32_t liveId=1;uint8_t liveValue=0;FrameActive active{bf,&liveId,&liveValue,1};
            context->ClearRenderTargetView(rt,background);dispatch(CommitActive,batchToken,&active);event(batchToken<<16);auto liveBatch=readback(device.Get(),context.Get(),target.Get());
            uint32_t liveSingles[]={singleIds[0],singleIds[2]};bf.list=liveSingles;bf.listCount=2;bf.listChanged=1;batchSegments[1]=2;
            context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto liveReference=readback(device.Get(),context.Get(),target.Get());
            unsigned liveMax=0;for(size_t k=0;k<liveBatch.size();k++)liveMax=std::max(liveMax,unsigned(std::abs(int(liveBatch[k])-int(liveReference[k]))));require(liveMax<=2,"live-bit edit without list rebuild changed order/pixels");
            liveValue=1;bf.list=batchedIds;bf.listCount=3;batchSegments[1]=3;active.frame=bf;
            context->ClearRenderTargetView(rt,background);dispatch(CommitActive,batchToken,&active);event(batchToken<<16);require(readback(device.Get(),context.Get(),target.Get())==batchImage,"reactivation did not restore exactly");
            // Invalidate cached world vertices on actual motion, then compare
            // against original matrices again. Camera-only VP edits stay dynamic.
            batchPoses[0].world[12]+=.137f;bf.poseCount=3;bf.list=batchedIds;bf.listCount=3;bf.listChanged=1;bf.vp[12]+=.071f;
            std::array<std::array<float,16>,3> denseWorlds{};for(unsigned j=0;j<3;j++)std::memcpy(denseWorlds[j].data(),batchPoses[j].world,64);
            bf.poses=reinterpret_cast<const Pose*>(denseWorlds.data());active.frame=bf;
            context->ClearRenderTargetView(rt,background);dispatch(CommitActiveWorld,batchToken,&active);event(batchToken<<16);auto movedBatch=readback(device.Get(),context.Get(),target.Get());
            bf.poses=batchPoses.data();bf.list=singleIds;bf.poseCount=0;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto movedOracle=readback(device.Get(),context.Get(),target.Get());
            unsigned movedMax=0;for(size_t k=0;k<movedBatch.size();k++)movedMax=std::max(movedMax,unsigned(std::abs(int(movedBatch[k])-int(movedOracle[k]))));require(movedMax<=2,"motion/VP did not invalidate/patch batch cache");
            bf.list=batchedIds;bf.listChanged=1;
            // A changed MPB is a hard batch boundary, including on an existing draw.
            auto boundary=colorsForBatch[1];for(const auto& binding:bindings){bool done=false;for(unsigned k=0;k<binding.bytes;k+=4){unsigned cbOffset=binding.offset+k;bool dynamic=false;for(const auto& patch:patches)if(cbOffset>=patch.offset&&cbOffset<patch.offset+(patch.kind<=7?64:16))dynamic=true;if(!dynamic){float value=.271f;std::memcpy(boundary.data()+cbOffset,&value,4);done=true;break;}}if(done)break;}
            uint32_t dirtyBatch=batchedIds[1],dirtyEnds[]={0,UINT(boundary.size())};bf.listChanged=1;bf.drawIds=&dirtyBatch;bf.dirtyCount=1;bf.constants=boundary.data();bf.constantBytes=UINT(boundary.size());bf.constantOffsets=dirtyEnds;
            context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);BatchPerformance boundaryPerf{};dispatch(BatchPerf,batchToken,&boundaryPerf);require(boundaryPerf.batches==bp.batches+3,"different MPB failed to split consecutive runs");
            auto boundaryImage=readback(device.Get(),context.Get(),target.Get());
            dirtyBatch=singleIds[1];bf.list=singleIds;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto boundaryOracle=readback(device.Get(),context.Get(),target.Get());
            unsigned boundaryMax=0;for(size_t k=0;k<boundaryImage.size();k++)boundaryMax=std::max(boundaryMax,unsigned(std::abs(int(boundaryImage[k])-int(boundaryOracle[k]))));require(boundaryMax<=2,"MPB boundary pixels differ from original matrices");
            // Restore the boundary constants, then permute a translucent draw list.
            // Same geometry/poses, different painter order: only indices may upload.
            dirtyBatch=batchedIds[1];bf.constants=colorsForBatch[1].data();bf.list=batchedIds;
            dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);
            dirtyBatch=singleIds[1];bf.list=singleIds;dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);
            bf.dirtyCount=bf.constantBytes=0;bf.constantOffsets=zeroOffsets;bf.poseCount=0;
            uint32_t reverseBatch[]={batchedIds[2],batchedIds[1],batchedIds[0]},reverseSingle[]={singleIds[2],singleIds[1],singleIds[0]};
            UploadPerformance sortBefore{},sortAfter{};BatchPerformance sortBatchBefore{},sortBatchAfter{};
            dispatch(UploadPerf,batchToken,&sortBefore);dispatch(BatchPerf,batchToken,&sortBatchBefore);
            bf.list=reverseBatch;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto reversed=readback(device.Get(),context.Get(),target.Get());
            dispatch(UploadPerf,batchToken,&sortAfter);dispatch(BatchPerf,batchToken,&sortBatchAfter);
            require(sortAfter.geometryBytes==sortBefore.geometryBytes&&sortBatchAfter.gpuGeometryUploads==sortBatchBefore.gpuGeometryUploads&&sortBatchAfter.gpuDispatches==sortBatchBefore.gpuDispatches,"reordering uploaded/recomputed geometry");
            require(sortAfter.indexBytes==sortBefore.indexBytes,"reorder must not upload index bytes");
            bf.list=reverseSingle;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto reversedOracle=readback(device.Get(),context.Get(),target.Get());
            require(reversed==reversedOracle,"stable-geometry reorder pixels differ from singles");
            // Change the LAST sparse pose (not slot zero), to catch boxed-source offsets.
            bf.list=reverseBatch;bf.listChanged=1;dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);
            batchPoses[2].world[13]+=.031f;uint32_t lastPose=2;bf.poseIds=&lastPose;bf.poses=&batchPoses[2];bf.poseCount=1;bf.listChanged=0;
            UploadPerformance sparseBefore{},sparseAfter{};dispatch(UploadPerf,batchToken,&sparseBefore);
            context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto sparseImage=readback(device.Get(),context.Get(),target.Get());dispatch(UploadPerf,batchToken,&sparseAfter);
            require(sparseAfter.poseBytes-sparseBefore.poseBytes==80&&sparseAfter.geometryBytes==sparseBefore.geometryBytes&&sparseAfter.indexBytes==sparseBefore.indexBytes,"one changed pose must upload exactly 80 bytes");
            bf.poseCount=0;bf.list=reverseSingle;bf.listChanged=1;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);
            require(sparseImage==readback(device.Get(),context.Get(),target.Get()),"sparse nonzero-offset upload corrupted pixels");
            UploadPerformance steadyBefore{},steadyAfter{};dispatch(UploadPerf,batchToken,&steadyBefore);bf.listChanged=0;dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);dispatch(UploadPerf,batchToken,&steadyAfter);
            require(steadyAfter.constantBytes==steadyBefore.constantBytes&&steadyAfter.colorBytes==steadyBefore.colorBytes,"stable alternating single draws reuploaded CB/color");
            BatchPerformance reasonStats{};dispatch(BatchPerf,batchToken,&reasonStats);
            require(steadyAfter.singleUnverified+steadyAfter.singleGeometry+steadyAfter.singleProgram+steadyAfter.singleConstants+steadyAfter.singleTextures+steadyAfter.singleSegment+steadyAfter.singleLimit==reasonStats.singleDraws,"single reason partition mismatch");
            std::cout<<"PASS M4g delta "<<label<<" reorder_geometry=0 reorder_dispatches=0 reorder_indices=0 sparse_pose_bytes=80 stable_cb_color=0 pixels=identical\n";
            if(sprite){
                auto tint=colorsForBatch[0];float changedTint[4]{.23f,.45f,.67f,.89f};size_t tintOffset=0;
                for(const auto& patch:patches)if(patch.kind==16)tintOffset=patch.offset;std::memcpy(tint.data()+tintOffset,changedTint,16);
                dirtyBatch=batchedIds[0];bf.list=reverseBatch;bf.listChanged=1;bf.drawIds=&dirtyBatch;bf.dirtyCount=1;bf.constants=tint.data();bf.constantBytes=UINT(tint.size());bf.constantOffsets=dirtyEnds;
                UploadPerformance tintBefore{},tintAfter{};dispatch(UploadPerf,batchToken,&tintBefore);
                context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto tinted=readback(device.Get(),context.Get(),target.Get());dispatch(UploadPerf,batchToken,&tintAfter);
                require(tintAfter.drawBytes-tintBefore.drawBytes==32&&tintAfter.geometryBytes==tintBefore.geometryBytes,"one tint must upload exactly one 32-byte draw slot");
                dirtyBatch=singleIds[0];bf.list=reverseSingle;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);require(tinted==readback(device.Get(),context.Get(),target.Get()),"sparse draw/color cache mismatch");
                colorsForBatch[0]=tint;bf.dirtyCount=bf.constantBytes=0;bf.constantOffsets=zeroOffsets;
                std::cout<<"PASS M4g tint "<<label<<" draw_bytes=32 geometry_bytes=0 pixels=identical\n";
            }
            // Subpixel thin geometry with nonsymmetric/sheared matrices and camera
            // changes. Compare exactly against the SAME original per-object DXBC.
            auto thinVertices=vertices;for(unsigned j=1;j<=4;j++)thinVertices[j].p[1]*=snap?.013f:.0007f;
            GeometryIn thin=geometry;thin.vertices=thinVertices.data()+1;thin.vertexBytes=4*sizeof(Vertex);thin.cpuIndices=localIndices.data();thin.cpuIndexCount=6;thin.buffers[1]=nullptr;thin.strides[1]=0;
            // Force the canonical interleaved layout even for the external multistream case.
            Attribute thinAttrs[]={{0,0,6,0,0},{3,0,2,0,12},{4,0,16,0,28},{4,1,16,0,36}};thin.attributes=thinAttrs;
            draw.geometry=UINT(dispatch(Geometry,batchToken,&thin));uint32_t thinSingle[3],thinBatch[3];
            for(unsigned j=0;j<3;j++){draw.pose=j;draw.constants=colorsForBatch[j].data();draw.sprite=sprite?1u:0u;thinSingle[j]=UINT(dispatch(Draw,batchToken,&draw));draw.sprite|=2;thinBatch[j]=UINT(dispatch(Draw,batchToken,&draw));}
            bf.poseIds=batchPoseIds;bf.poses=batchPoses.data();bf.poseCount=3;bf.listChanged=1;
            uint64_t thinDiff=0,thinCoverage=0;
            for(unsigned sample=0;sample<24;sample++){
                for(unsigned j=0;j<3;j++){batchPoses[j].world[0]=.73f+.0031f*float(sample+j);batchPoses[j].world[1]=.000017f*float(sample+1);batchPoses[j].world[4]=-.13f;batchPoses[j].world[5]=1.07f;batchPoses[j].world[12]=-.12f+.00137f*float(sample+j);batchPoses[j].world[13]=.004901f+.000113f*float(sample+j);}
                bf.vp[0]=.9137f;bf.vp[5]=-.8703f;bf.vp[12]=-.03719f+.00011f*float(sample);bf.vp[13]=.04331f;
                bf.list=thinSingle;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto thinOracle=readback(device.Get(),context.Get(),target.Get());
                bf.list=thinBatch;context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);auto thinImage=readback(device.Get(),context.Get(),target.Get());
                for(size_t k=0;k<thinImage.size();k++)thinDiff+=thinImage[k]!=thinOracle[k];
                for(size_t k=0;k<thinOracle.size();k+=4){bool visible=false;for(size_t c=0;c<4;c++)visible|=thinOracle[k+c]!=uint8_t(std::round(background[c]*255));thinCoverage+=visible;}
            }
            require(thinDiff==0&&thinCoverage>50,"thin subpixel geometry precision/coverage differs from original DXBC");
            std::cout<<"PASS M4g thin "<<label<<" transforms=24 differing_channels="<<thinDiff<<" covered_pixels="<<thinCoverage<<" original_DXBC=True\n";
            // An entirely offscreen batch must not submit a draw, while a partially
            // visible union must keep all its coverage.
            Statistics cullBefore{},cullAfter{};dispatch(Stats,batchToken,&cullBefore);for(auto& pos:batchPoses)pos.world[12]+=100.f;
            context->ClearRenderTargetView(rt,background);dispatch(CommitPacked,batchToken,&bf);event(batchToken<<16);dispatch(Stats,batchToken,&cullAfter);
            require(cullAfter.draws==cullBefore.draws&&cullAfter.errors==0,"offscreen batch bypassed culling");
            std::cout<<"PASS M4g batch-cull "<<label<<" offscreen_draws=0\n";
            dispatch(Destroy,batchToken,nullptr);
            if(caseIndex==0){
                // A submission microbenchmark, never a claim about GPU/game FPS.
                ULONGLONG setupAt=GetTickCount64();CreateIn largeCreate{target.Get(),8192,1};int large=dispatch(Create,0,&largeCreate);
                draw.program=UINT(dispatch(Program,large,&baselineProgram));draw.geometry=UINT(dispatch(Geometry,large,&geometry));draw.constants=constants.data();draw.sprite=3;
                constexpr unsigned population=7000;std::vector<Pose> manyPoses(population,pose);std::vector<uint32_t> manyIds(population),manyList(population);
                for(unsigned j=0;j<population;j++){draw.pose=j;manyIds[j]=j;manyList[j]=UINT(dispatch(Draw,large,&draw));manyPoses[j].world[0]*=.001f;manyPoses[j].world[1]*=.001f;manyPoses[j].world[4]*=.001f;manyPoses[j].world[5]*=.001f;}
                Frame many=frame;uint32_t manySegments[]={0,population};many.poses=manyPoses.data();many.poseIds=manyIds.data();many.poseCount=population;many.list=manyList.data();many.listCount=population;many.segments=manySegments;many.segmentCount=1;many.listChanged=1;many.dirtyCount=many.constantBytes=0;many.constantOffsets=zeroOffsets;
                ULONGLONG setupMs=GetTickCount64()-setupAt;
                dispatch(CommitPacked,large,&many);event(large<<16);many.poseCount=0;many.listChanged=0;
                Statistics warmStats{};Performance warmPhases{};BatchPerformance warmBatch{};dispatch(Stats,large,&warmStats);dispatch(Perf,large,&warmPhases);dispatch(BatchPerf,large,&warmBatch);
                uint64_t beforeHeap=nativeHeapCalls.load();
                for(unsigned j=0;j<256;j++){dispatch(CommitPacked,large,&many);event(large<<16);}
                uint64_t heapCalls=nativeHeapCalls.load()-beforeHeap;Statistics largeStats{};BatchPerformance largePerf{};dispatch(Stats,large,&largeStats);dispatch(BatchPerf,large,&largePerf);Performance phases{};dispatch(Perf,large,&phases);
                require(heapCalls==0&&largeStats.errors==0&&largeStats.dropped==0&&largeStats.draws==257&&largePerf.gpuDispatches==1&&largePerf.gpuGeometryUploads==1&&largePerf.sources==uint64_t(population)*257,"7000-source batch steady allocations/draw count/errors");
                std::cout<<"PASS M4f submission sources="<<population<<" frames=256 draws_per_frame=1 cpp_heap_calls="<<heapCalls<<" render_us="<<(largeStats.renderUs*double(largeStats.frames)-warmStats.renderUs)/256.0<<" batch_build_us="<<(largePerf.buildUs-warmBatch.buildUs)/double(phases.sampledFrames-warmPhases.sampledFrames)<<" execute_us="<<(phases.executeUs*double(phases.sampledFrames)-warmPhases.executeUs*double(warmPhases.sampledFrames))/double(phases.sampledFrames-warmPhases.sampledFrames)<<" setup_native_ms="<<setupMs<<" cb_creates="<<largePerf.constantCreates<<" gpu_pretransform=True tiny_geometry=True WARP_not_game=True\n";
                std::vector<std::array<float,16>> movingWorlds(population);for(unsigned j=0;j<population;j++)std::memcpy(movingWorlds[j].data(),manyPoses[j].world,64);many.poses=reinterpret_cast<const Pose*>(movingWorlds.data());many.poseCount=population;FrameActive movingFrame{many,nullptr,nullptr,0};uint64_t beforeMoving=nativeHeapCalls.load();
                for(unsigned j=0;j<256;j++){for(auto& movingPose:movingWorlds)movingPose[12]+=.000001f;dispatch(CommitActiveWorld,large,&movingFrame);event(large<<16);}
                uint64_t movingHeap=nativeHeapCalls.load()-beforeMoving;Statistics movingStats{};Performance movingPhases{};BatchPerformance movingBatch{};dispatch(Stats,large,&movingStats);dispatch(Perf,large,&movingPhases);dispatch(BatchPerf,large,&movingBatch);
                require(movingHeap==0&&movingStats.errors==0&&movingStats.dropped==0&&movingStats.draws==513&&movingBatch.gpuDispatches==257&&movingBatch.gpuGeometryUploads==1,"moving 7000-source frame allocation/drop/draw mismatch");
                std::cout<<"PASS M4f moving sources=7000 frames=256 draws_per_frame=1 cpp_heap_calls="<<movingHeap<<" render_us="<<(movingStats.renderUs*double(movingStats.frames)-largeStats.renderUs*double(largeStats.frames))/256.0<<" commit_us="<<(movingStats.commitUs*double(movingStats.frames)-largeStats.commitUs*double(largeStats.frames))/256.0<<" batch_build_us="<<(movingBatch.buildUs-largePerf.buildUs)/double(movingPhases.sampledFrames-phases.sampledFrames)<<" world_only=True gpu_pretransform=True tiny_geometry=True WARP_not_game=True\n";
                // Exit-like sparse motion: 131 of 7000 poses, two disjoint ranges.
                std::array<uint32_t,131> sparseIds{};std::array<std::array<float,16>,131> sparseWorlds{};
                for(unsigned j=0;j<131;j++){sparseIds[j]=j<65?100+j:6000+j;std::memcpy(sparseWorlds[j].data(),movingWorlds[sparseIds[j]].data(),64);sparseWorlds[j][12]+=.00001f;}
                many.poses=reinterpret_cast<const Pose*>(sparseWorlds.data());many.poseIds=sparseIds.data();many.poseCount=131;
                FrameActive sparseFrame{many,nullptr,nullptr,0};UploadPerformance sparseStart{},sparseEnd{};dispatch(UploadPerf,large,&sparseStart);
                uint64_t sparseHeapStart=nativeHeapCalls.load();
                for(unsigned j=0;j<256;j++){for(auto& world:sparseWorlds)world[12]+=.000001f;dispatch(CommitActiveWorld,large,&sparseFrame);event(large<<16);}
                uint64_t sparseHeap=nativeHeapCalls.load()-sparseHeapStart;dispatch(UploadPerf,large,&sparseEnd);Statistics sparseStats{};dispatch(Stats,large,&sparseStats);
                require(sparseHeap==0&&sparseStats.errors==0&&sparseEnd.poseBytes-sparseStart.poseBytes==uint64_t(131)*80*256&&sparseEnd.geometryBytes==sparseStart.geometryBytes&&sparseEnd.drawBytes==sparseStart.drawBytes&&sparseEnd.constantBytes==sparseStart.constantBytes,"131 dirty poses uploaded unchanged data/allocated/errored");
                require(sparseEnd.poseRanges-sparseStart.poseRanges==512,"dirty poses not coalesced into two ranges");
                require(sparseEnd.bakedVertices-sparseStart.bakedVertices==uint64_t(14)*64*256,"sparse motion computed unchanged vertex blocks");
                std::cout<<"PASS M4g sparse sources=7000 changed=131 frames=256 pose_bytes_per_frame=10480 pose_ranges_per_frame=2 geometry_bytes=0 draw_bytes=0 constant_bytes=0 cpp_heap_calls="<<sparseHeap<<" render_us="<<(sparseStats.renderUs*double(sparseStats.frames)-movingStats.renderUs*double(movingStats.frames))/256.0<<" work_bytes_per_frame="<<double(sparseEnd.workBytes-sparseStart.workBytes)/256.0<<" baked_vertices_per_frame="<<(sparseEnd.bakedVertices-sparseStart.bakedVertices)/256<<"\n";
                // Reorder all 7000 every frame, with no new geometry or poses.
                many.poseCount=0;many.listChanged=1;UploadPerformance reStart{},reEnd{};BatchPerformance rbStart{},rbEnd{};dispatch(UploadPerf,large,&reStart);dispatch(BatchPerf,large,&rbStart);
                uint64_t reorderHeapStart=nativeHeapCalls.load();
                for(unsigned j=0;j<64;j++){std::reverse(manyList.begin(),manyList.end());dispatch(CommitPacked,large,&many);event(large<<16);}
                uint64_t reorderHeap=nativeHeapCalls.load()-reorderHeapStart;dispatch(UploadPerf,large,&reEnd);dispatch(BatchPerf,large,&rbEnd);Statistics reStats{};dispatch(Stats,large,&reStats);
                require(reorderHeap==0&&reStats.errors==0&&reEnd.geometryBytes==reStart.geometryBytes&&rbEnd.gpuGeometryUploads==rbStart.gpuGeometryUploads&&rbEnd.gpuDispatches==rbStart.gpuDispatches&&reEnd.poseBytes==reStart.poseBytes&&reEnd.indexBytes==reStart.indexBytes,"reorder uploaded geometry/pose/index data");
                SubmissionPerformance submission{};dispatch(SubmissionPerf,large,&submission);
                require(submission.orderBytes>=uint64_t(population)*12*64&&submission.orderDispatches>=64,"GPU reorder descriptor path not exercised");
                std::cout<<"PASS M4g reorder sources=7000 frames=64 geometry_bytes=0 pose_bytes=0 compute_dispatches=0 index_bytes_per_frame=0 order_bytes_per_frame=84000 cpp_heap_calls="<<reorderHeap<<" render_us="<<(reStats.renderUs*double(reStats.frames)-sparseStats.renderUs*double(sparseStats.frames))/64.0<<"\n";
                Performance accounting{};dispatch(Perf,large,&accounting);
                require(accounting.uploadBytes==reEnd.geometryBytes+reEnd.poseBytes+reEnd.drawBytes+reEnd.indexBytes+reEnd.constantBytes+reEnd.colorBytes+reEnd.cpuBatchBytes+reEnd.workBytes,"upload category bytes do not sum to total");
                GpuPerformance timing{};dispatch(GpuPerf,large,&timing);require(timing.samples>0&&timing.eventUs>=0&&timing.maxEventUs>=timing.eventUs,"GPU timestamp query sampling failed");
                std::cout<<"PASS M4g accounting bytes="<<accounting.uploadBytes<<" categories=8 GPU_samples="<<timing.samples<<" gpu_event_us="<<timing.eventUs<<" gpu_max_us="<<timing.maxEventUs<<" adapter=WARP not_hardware=True\n";
                MemoryPerformance footprint{};dispatch(MemoryPerf,large,&footprint);
                require(footprint.poseBytes<=uint64_t(7936)*80*2,"pose streams retain old power-of-two reservation");
                require(footprint.drawBytes<=uint64_t(7936)*32*2,"draw streams retain old power-of-two reservation");
                require(footprint.indexBytes<=uint64_t(47296)*4+uint64_t(7936)*12,"index/descriptor demand reservation too large");
                TimelinePerformance constantsPerf{};dispatch(TimelinePerf,large,&constantsPerf);
                std::cout<<"PASS M4h footprint sources=7000 owned="<<footprint.ownedBytes<<" pose="<<footprint.poseBytes<<" draw="<<footprint.drawBytes<<" index="<<footprint.indexBytes<<" pretransform="<<footprint.pretransformBytes<<" constant_evaluations="<<constantsPerf.constantEvaluations<<" constant_reuses="<<constantsPerf.constantReuses<<" constant_bind_skips="<<constantsPerf.constantBindSkips<<" logical_bytes=True driver_residency=UNMEASURED\n";
                SubmissionPerformance identicalBefore{},identicalAfter{};dispatch(SubmissionPerf,large,&identicalBefore);
                for(unsigned j=0;j<16;j++){many.listChanged=1;dispatch(CommitPacked,large,&many);event(large<<16);}
                dispatch(SubmissionPerf,large,&identicalAfter);
                require(identicalAfter.orderBytes==identicalBefore.orderBytes&&identicalAfter.orderDispatches==identicalBefore.orderDispatches,"same list re-notification rebuilt indices");
                std::cout<<"PASS M4i identical-list packets=16 order_upload_bytes=0 order_dispatches=0\n";
                dispatch(Destroy,large,nullptr);
                // Hundreds of original-DXBC singles with per-object matrix CBs.
                // Small geometry intentionally measures submission, not game FPS.
                constexpr UINT singleCount=345;CreateIn singleCreate{target.Get(),512,1};int singlesToken=dispatch(Create,0,&singleCreate);
                draw.program=UINT(dispatch(Program,singlesToken,&baselineProgram));draw.geometry=UINT(dispatch(Geometry,singlesToken,&geometry));draw.constants=constants.data();draw.sprite=sprite?1u:0u;
                std::vector<Pose> singlePoses(singleCount,pose);std::vector<UINT> singlePoseIds(singleCount),singleList(singleCount);
                for(UINT j=0;j<singleCount;j++){draw.pose=j;singlePoseIds[j]=j;singleList[j]=UINT(dispatch(Draw,singlesToken,&draw));singlePoses[j].world[0]*=.001f;singlePoses[j].world[5]*=.001f;}
                Frame sf=frame;UINT ss[]={0,singleCount};sf.poses=singlePoses.data();sf.poseIds=singlePoseIds.data();sf.poseCount=singleCount;sf.list=singleList.data();sf.listCount=singleCount;sf.segments=ss;sf.segmentCount=1;sf.listChanged=1;sf.dirtyCount=sf.constantBytes=0;sf.constantOffsets=zeroOffsets;
                dispatch(CommitPacked,singlesToken,&sf);event(singlesToken<<16);sf.listChanged=0;
                for(UINT j=0;j<16;j++){dispatch(CommitPacked,singlesToken,&sf);event(singlesToken<<16);}
                Statistics singleStart{},singleEnd{};SubmissionPerformance cbStart{},cbEnd{};dispatch(Stats,singlesToken,&singleStart);dispatch(SubmissionPerf,singlesToken,&cbStart);
                uint64_t beforeSingles=nativeHeapCalls.load();
                for(UINT j=0;j<128;j++){for(auto& pos:singlePoses)pos.world[12]+=.000001f;dispatch(CommitPacked,singlesToken,&sf);event(singlesToken<<16);}
                uint64_t singleHeap=nativeHeapCalls.load()-beforeSingles;dispatch(Stats,singlesToken,&singleEnd);dispatch(SubmissionPerf,singlesToken,&cbEnd);
                require(singleEnd.errors==0&&singleEnd.draws-singleStart.draws==uint64_t(singleCount)*128,"single draw fixture lost submissions");
                require(singleHeap==0,"steady single submission allocated CPU heap");
                require(cbEnd.ringMaps-cbStart.ringMaps<=128,"more than one constant map per single-draw segment");
                std::cout<<"PASS M4i singles sources=345 frames=128 draws_per_frame=345 cpp_heap_calls="<<singleHeap<<" ring_maps="<<cbEnd.ringMaps-cbStart.ringMaps<<" ring_bindings="<<cbEnd.ringBindings-cbStart.ringBindings<<" render_us="<<(singleEnd.renderUs*double(singleEnd.frames)-singleStart.renderUs*double(singleStart.frames))/128<<" immediate="<<cbEnd.immediateSegments<<" deferred="<<cbEnd.deferredSegments<<" WARP_not_Exit=True\n";
                auto intact=pipelineState(context.Get());event((singlesToken<<16)|65535);require(pipelineState(context.Get())==intact,"failed render did not restore pipeline state");dispatch(Stats,singlesToken,&singleEnd);require(singleEnd.errors==1,"invalid segment error not counted");
                std::cout<<"PASS M4i failed-segment state=restored errors=1\n";
                dispatch(Destroy,singlesToken,nullptr);
            }

        }

        passed++;
    }
    require(passed==count&&count>=8&&(negative==5 || (negative==0 && count==8)),"fixture coverage");std::cout<<"PASS cases="<<passed<<" maxChannel="<<globalMax<<" differingPixels="<<allChanged<<" controls="<<negative<<" UnityCapture=UNMEASURED gameFPS=UNMEASURED\n";
    std::ofstream summary(out/"warp-summary.json");summary<<"{\"passed\":"<<passed<<",\"max_channel\":"<<globalMax<<",\"differing_pixels\":"<<allChanged<<",\"controls\":"<<negative<<",\"adapter\":\"WARP\",\"unity_capture\":\"UNMEASURED\",\"game_fps\":\"UNMEASURED\"}\n";return 0;
}catch(HRESULT hr){std::cerr<<"FAIL HRESULT 0x"<<std::hex<<hr<<"\n";return 1;}catch(const std::exception& e){std::cerr<<"FAIL "<<e.what()<<"\n";return 1;}}
