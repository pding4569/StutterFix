// Offline WARP fixture: production drfull.cpp + original stripped DXBC versus
// independently compiled named arithmetic. No Unity process, swapchain or install.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
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
int dispatch(int op,int token,const void* p){auto n=sf_z_drfull_v1(op,token,p);if(n<=0){std::cerr<<"dispatch "<<op<<" hr="<<std::hex<<sf_z_drfull_v1(Error,0,nullptr)<<std::dec<<"\n";throw std::runtime_error("native refused fixture");}return int(n);}
struct Vertex{float p[3],color[4],uv[2],uv1[2];};
ComPtr<ID3D11Buffer> buffer(ID3D11Device* d,const void* p,UINT n,UINT flags){D3D11_BUFFER_DESC b{};b.ByteWidth=n;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=flags;D3D11_SUBRESOURCE_DATA data{};data.pSysMem=p;ComPtr<ID3D11Buffer> out;check(d->CreateBuffer(&b,p?&data:nullptr,&out));return out;}
ComPtr<ID3DBlob> compile(const std::filesystem::path& path,const char* entry,const char* profile,int floor,int snap,int point){
    D3D_SHADER_MACRO macros[]={{"FLOOR",floor?"1":"0"},{"SNAP",snap?"1":"0"},{"POINT",point?"1":"0"},{nullptr,nullptr}};
    ComPtr<ID3DBlob> code,error;HRESULT hr=D3DCompileFromFile(path.c_str(),macros,D3D_COMPILE_STANDARD_FILE_INCLUDE,entry,profile,D3DCOMPILE_OPTIMIZATION_LEVEL3|D3DCOMPILE_IEEE_STRICTNESS,0,&code,&error);
    if(FAILED(hr)){if(error)std::cerr<<static_cast<const char*>(error->GetBufferPointer());check(hr);}return code;
}
std::vector<uint8_t> readback(ID3D11Device* d,ID3D11DeviceContext* c,ID3D11Texture2D* texture){D3D11_TEXTURE2D_DESC td{};texture->GetDesc(&td);td.BindFlags=0;td.Usage=D3D11_USAGE_STAGING;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;ComPtr<ID3D11Texture2D> staging;check(d->CreateTexture2D(&td,nullptr,&staging));c->CopyResource(staging.Get(),texture);D3D11_MAPPED_SUBRESOURCE map{};check(c->Map(staging.Get(),0,D3D11_MAP_READ,0,&map));std::vector<uint8_t> bytes(size_t(td.Width)*td.Height*4);for(UINT y=0;y<td.Height;y++)std::memcpy(bytes.data()+size_t(y)*td.Width*4,static_cast<const uint8_t*>(map.pData)+size_t(y)*map.RowPitch,td.Width*4);c->Unmap(staging.Get(),0);return bytes;}
void ppm(const std::filesystem::path& path,const std::vector<uint8_t>& bytes){std::ofstream f(path,std::ios::binary);f<<"P6\n192 128\n255\n";for(size_t i=0;i<bytes.size();i+=4)f.write(reinterpret_cast<const char*>(bytes.data()+i),3);}
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
        auto originalVs=r.blob(),originalPs=r.blob();ProgramIn program{};program.vs=originalVs.data();program.ps=originalPs.data();program.vsBytes=UINT(originalVs.size());program.psBytes=UINT(originalPs.size());for(auto& x:program.state)x=r.pod<int>();
        std::vector<Buffer> bindings(r.pod<uint32_t>());for(auto& x:bindings)x=r.pod<Buffer>();std::vector<Patch> patches(r.pod<uint32_t>());for(auto& x:patches)x=r.pod<Patch>();auto constants=r.blob();
        struct TextureInput{int stage,slot,sampler,pattern;};std::vector<TextureInput> texInputs(r.pod<uint32_t>());for(auto& x:texInputs)x=r.pod<TextureInput>();auto reference=r.blob();Pose pose{};for(auto& x:pose.world)x=r.pod<float>();for(int i=0;i<16;i++)pose.inverse[i]=i%5==0?1.f:0.f;
        Frame frame{};for(auto& x:frame.vp)x=r.pod<float>();for(auto& x:frame.screen)x=r.pod<float>();
        bool sprite=mode%2==0;bool multi=mode==3;
        std::array<Vertex,5> vertices{};float positions[4][2]={{-.75f,-.65f},{.72f,-.65f},{.72f,.69f},{-.75f,.69f}};
        const float uniform[4]={.61f,.84f,.47f,.71f};
        for(unsigned i=0;i<4;i++){auto& v=vertices[i+1];v.p[0]=positions[i][0];v.p[1]=positions[i][1];v.p[2]=0;for(int j=0;j<4;j++)v.color[j]=sprite?uniform[j]:.35f+float((i+unsigned(j)*3)%5)*.12f;v.uv[0]=(i==1||i==2)?1.07f:-.09f;v.uv[1]=(i>=2)?(mode==1?1.85f:1.07f):(mode==1?1.05f:-.07f);v.uv1[0]=positions[i][0]*.81f;v.uv1[1]=positions[i][1]*.77f;}
        if(sprite)std::memcpy(constants.data(),uniform,16);
        std::array<uint32_t,9> indices={0,0,0,0,1,2,0,2,3};
        Attribute attributes[]={{0,0,6,0,0},{3,0,2,multi?1u:0u,multi?0u:12u},{4,0,16,0,28},{4,1,16,0,36}};
        GeometryIn geometry{};geometry.attributes=attributes;geometry.attributeCount=4;geometry.strides[0]=sizeof(Vertex);
        std::array<std::array<float,4>,5> colors{};for(size_t i=0;i<5;i++)std::memcpy(colors[i].data(),vertices[i].color,16);
        auto vb=buffer(device.Get(),vertices.data(),UINT(sizeof(vertices)),D3D11_BIND_VERTEX_BUFFER);auto ib=buffer(device.Get(),indices.data(),UINT(sizeof(indices)),D3D11_BIND_INDEX_BUFFER);auto colorBuffer=buffer(device.Get(),colors.data(),UINT(sizeof(colors)),D3D11_BIND_VERTEX_BUFFER);
        if(sprite){geometry.vertices=vertices.data()+1;geometry.vertexBytes=4*sizeof(Vertex);geometry.cpuIndices=indices.data()+3;geometry.cpuIndexCount=6;}
        else {geometry.buffers[0]=vb.Get();if(multi){geometry.buffers[1]=colorBuffer.Get();geometry.strides[1]=16;}geometry.indices=ib.Get();geometry.indexFormat=DXGI_FORMAT_R32_UINT;geometry.indexStart=3;geometry.indexCount=6;geometry.baseVertex=1;}
        CreateIn create{target.Get(),16,1};int token=dispatch(Create,0,&create);int programId=dispatch(Program,token,&program),geometryId=dispatch(Geometry,token,&geometry);
        std::vector<Texture> tex;for(const auto& t:texInputs)tex.push_back(Texture{textures[size_t(t.pattern)].Get(),t.stage,t.slot,t.sampler,filter,mode%2,mode%2,1,0});
        DrawIn draw{};draw.program=UINT(programId);draw.geometry=UINT(geometryId);draw.buffers=bindings.data();draw.bufferCount=UINT(bindings.size());draw.patches=patches.data();draw.patchCount=UINT(patches.size());draw.textures=tex.data();draw.textureCount=UINT(tex.size());draw.constants=constants.data();draw.constantBytes=UINT(constants.size());draw.sprite=sprite?1u:0u;
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
        context->ClearRenderTargetView(rt,background);for(unsigned repeat=0;repeat<segs[1];repeat++)context->DrawIndexed(6,3,1);auto ref=readback(device.Get(),context.Get(),target.Get());
        uint64_t changed=0,l1=0,nonBackground=0;unsigned maxChannel=0;for(size_t i=0;i<original.size();i+=4){bool diff=false,bg=false;for(size_t j=0;j<4;j++){unsigned delta=unsigned(std::abs(int(original[i+j])-int(ref[i+j])));maxChannel=std::max(maxChannel,delta);l1+=delta;diff|=delta!=0;bg|=original[i+j]!=uint8_t(std::round(background[j]*255));}changed+=diff;nonBackground+=bg;}
        csv<<label<<'\t'<<stats.draws<<'\t'<<nonBackground<<'\t'<<changed<<'\t'<<maxChannel<<'\t'<<l1<<'\n';csv.flush();
        if(caseIndex==0||maxChannel>2){ppm(out/(label+"-original.ppm"),original);ppm(out/(label+"-reference.ppm"),ref);}
        require(nonBackground>1000,"blank original falsely looks equal");
        std::cout<<label<<" nonblank="<<nonBackground<<" changed="<<changed<<" max="<<maxChannel<<" L1="<<l1<<"\n";
        require(maxChannel<=2,"original/reference pixel mismatch");globalMax=std::max(globalMax,maxChannel);allChanged+=changed;
        if(caseIndex==0){
            // Non-symmetric matrix transposition is a required negative control.
            auto bad=reference;float* matrix=reinterpret_cast<float*>(bad.data());for(int c=0;c<4;c++)for(int row=0;row<c;row++)std::swap(matrix[c*4+row],matrix[row*4+c]);context->UpdateSubresource(cb.Get(),0,nullptr,bad.data(),0,0);context->ClearRenderTargetView(rt,background);context->DrawIndexed(6,3,1);auto badImage=readback(device.Get(),context.Get(),target.Get());unsigned errors=0;for(size_t i=0;i<badImage.size();i+=4)if(std::memcmp(badImage.data()+i,original.data()+i,4)!=0)errors++;require(errors>1000,"matrix control failed to expose wrong layout");negative++;
            // ExecuteCommandList(TRUE) must restore Unity's immediate state.
            context->UpdateSubresource(cb.Get(),0,nullptr,reference.data(),0,0);dispatch(Commit,token,&frame);event(token<<16);ComPtr<ID3D11VertexShader> restored;context->VSGetShader(&restored,nullptr,nullptr);require(restored.Get()==vs.Get(),"native corrupted immediate shader state");negative++;
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
        }
        dispatch(Destroy,token,nullptr);event(token<<16); // stale command buffer lease is inert.
        passed++;
    }
    require(passed==count&&count>=48&&negative==5,"fixture coverage");std::cout<<"PASS cases="<<passed<<" maxChannel="<<globalMax<<" differingPixels="<<allChanged<<" controls="<<negative<<" UnityCapture=UNMEASURED gameFPS=UNMEASURED\n";
    std::ofstream summary(out/"warp-summary.json");summary<<"{\"passed\":"<<passed<<",\"max_channel\":"<<globalMax<<",\"differing_pixels\":"<<allChanged<<",\"controls\":"<<negative<<",\"adapter\":\"WARP\",\"unity_capture\":\"UNMEASURED\",\"game_fps\":\"UNMEASURED\"}\n";return 0;
}catch(HRESULT hr){std::cerr<<"FAIL HRESULT 0x"<<std::hex<<hr<<"\n";return 1;}catch(const std::exception& e){std::cerr<<"FAIL "<<e.what()<<"\n";return 1;}}
