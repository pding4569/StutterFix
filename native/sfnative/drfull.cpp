// M4: original Unity DXBC, explicit camera lease; no Present hooks or Unity calls.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <vector>
#include <array>
#include <memory>
#include <mutex>
#include <string>
#include <algorithm>
#include <cstring>
#include <cmath>
#include "drfull.h"
namespace {
using Microsoft::WRL::ComPtr;
using namespace drfull;
void check(HRESULT hr) { if(FAILED(hr)) throw hr; }
void require(bool b) { if(!b) throw E_INVALIDARG; }
double now() { LARGE_INTEGER c,f;QueryPerformanceCounter(&c);QueryPerformanceFrequency(&f);return double(c.QuadPart)*1e6/double(f.QuadPart); }
const char* semantics[]={"POSITION","NORMAL","TANGENT","COLOR","TEXCOORD"};
DXGI_FORMAT format(DXGI_FORMAT f,bool srgb) {
    switch(f) {
    case DXGI_FORMAT_R32G32B32A32_TYPELESS:return DXGI_FORMAT_R32G32B32A32_FLOAT;
    case DXGI_FORMAT_R16G16B16A16_TYPELESS:return DXGI_FORMAT_R16G16B16A16_FLOAT;
    case DXGI_FORMAT_R8G8B8A8_TYPELESS:return srgb?DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:DXGI_FORMAT_R8G8B8A8_UNORM;
    case DXGI_FORMAT_B8G8R8A8_TYPELESS:return srgb?DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:DXGI_FORMAT_B8G8R8A8_UNORM;
    case DXGI_FORMAT_BC1_TYPELESS:return srgb?DXGI_FORMAT_BC1_UNORM_SRGB:DXGI_FORMAT_BC1_UNORM;
    case DXGI_FORMAT_BC2_TYPELESS:return srgb?DXGI_FORMAT_BC2_UNORM_SRGB:DXGI_FORMAT_BC2_UNORM;
    case DXGI_FORMAT_BC3_TYPELESS:return srgb?DXGI_FORMAT_BC3_UNORM_SRGB:DXGI_FORMAT_BC3_UNORM;
    case DXGI_FORMAT_BC7_TYPELESS:return srgb?DXGI_FORMAT_BC7_UNORM_SRGB:DXGI_FORMAT_BC7_UNORM;
    default:return f;
    }
}
D3D11_BLEND blend(int x) {
    static const D3D11_BLEND table[]={D3D11_BLEND_ZERO,D3D11_BLEND_ONE,D3D11_BLEND_DEST_COLOR,D3D11_BLEND_SRC_COLOR,D3D11_BLEND_INV_DEST_COLOR,D3D11_BLEND_SRC_ALPHA,D3D11_BLEND_INV_SRC_COLOR,D3D11_BLEND_DEST_ALPHA,D3D11_BLEND_INV_DEST_ALPHA,D3D11_BLEND_SRC_ALPHA_SAT,D3D11_BLEND_INV_SRC_ALPHA};
    require(x>=0&&x<11);return table[x];
}
D3D11_STENCIL_OP stencil(int x) { static const D3D11_STENCIL_OP t[]={D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_ZERO,D3D11_STENCIL_OP_REPLACE,D3D11_STENCIL_OP_INCR_SAT,D3D11_STENCIL_OP_DECR_SAT,D3D11_STENCIL_OP_INVERT,D3D11_STENCIL_OP_INCR,D3D11_STENCIL_OP_DECR};require(x>=0&&x<8);return t[x]; }
struct Shader {
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;std::vector<uint8_t> signature;
    ComPtr<ID3D11BlendState> blendState;ComPtr<ID3D11DepthStencilState> depth;ComPtr<ID3D11RasterizerState> raster;UINT stencilRef=0;
};
struct Geometry {
    std::array<ComPtr<ID3D11Buffer>,8> buffers;std::array<UINT,8> strides{},offsets{};
    ComPtr<ID3D11Buffer> indices;DXGI_FORMAT indexFormat;UINT indexStart,indexCount;INT baseVertex;
    std::vector<D3D11_INPUT_ELEMENT_DESC> attributes;
};
struct Sample { ComPtr<ID3D11ShaderResourceView> view;ComPtr<ID3D11SamplerState> sampler;int stage,slot,samplerSlot;bool grab=false; };
struct Constants { Buffer binding;ComPtr<ID3D11Buffer> buffer; };
struct DrawCall {
    std::shared_ptr<Shader> shader;std::shared_ptr<Geometry> geometry;ComPtr<ID3D11InputLayout> layout;
    std::vector<Constants> buffers;std::vector<Patch> patches;std::vector<Sample> textures;std::vector<uint8_t> bytes;uint32_t pose;bool grab=false;ComPtr<ID3D11Buffer> color;
    float center[3]{},extent[3]{};
    bool composite=false;ComPtr<ID3D11ShaderResourceView> black,white;
};
struct Packet {
    Frame f{};std::vector<Pose> poses;std::vector<uint32_t> poseIds,ids,offsets,list,segs;std::vector<uint8_t> bytes;
};
void multiply(float* out,const float* a,const float* b) {
    for(int c=0;c<4;c++)for(int r=0;r<4;r++){float v=0;for(int k=0;k<4;k++)v+=a[k*4+r]*b[c*4+k];out[c*4+r]=v;}
}
struct Renderer {
    int token=0;uint32_t capacity;
    ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context,deferred;
    std::mutex mutex;std::vector<std::shared_ptr<Shader>> shaders;std::vector<std::shared_ptr<Geometry>> geometries;std::vector<std::shared_ptr<DrawCall>> draws;
    std::array<Packet,8> ring;uint64_t read=0,write=0;std::vector<Pose> poses;
    std::vector<uint32_t> list,segs;Frame frame{};Statistics stats{};double renderSum=0,commitSum=0;uint64_t commits=0;
    ComPtr<ID3D11Texture2D> grabTexture;ComPtr<ID3D11ShaderResourceView> grabView;D3D11_TEXTURE2D_DESC grabDesc{};DXGI_FORMAT grabFormat=DXGI_FORMAT_UNKNOWN;
    std::shared_ptr<Shader> compositor;
    Renderer(const CreateIn& in):capacity(in.capacity) {
        require(in.resource&&in.abi==1&&capacity>0&&capacity<=4000000);
        static_cast<ID3D11DeviceChild*>(in.resource)->GetDevice(&device);require(device!=nullptr);
        require((device->GetCreationFlags()&D3D11_CREATE_DEVICE_SINGLETHREADED)==0);
        device->GetImmediateContext(&context);check(device->CreateDeferredContext(0,&deferred));
        poses.resize(capacity);list.reserve(capacity*4);segs.reserve(131072);draws.reserve(capacity*4);
        for(auto& p:ring){p.poses.reserve(capacity);p.poseIds.reserve(capacity);p.ids.reserve(capacity*4);p.offsets.reserve(capacity*4+1);p.bytes.reserve(size_t(std::min(capacity,8192u))*1024);p.list.reserve(capacity*4);p.segs.reserve(131072);}
    }
    int program(const ProgramIn& in) {
        auto s=std::make_shared<Shader>();require(in.vs&&in.ps&&in.vsBytes>=32&&in.psBytes>=32);
        const auto* v=static_cast<const uint8_t*>(in.vs);s->signature.assign(v,v+in.vsBytes);
        check(device->CreateVertexShader(in.vs,in.vsBytes,nullptr,&s->vs));check(device->CreatePixelShader(in.ps,in.psBytes,nullptr,&s->ps));
        const int32_t* a=in.state;D3D11_BLEND_DESC b{};b.AlphaToCoverageEnable=a[13]!=0;auto& rt=b.RenderTarget[0];
        rt.BlendEnable=a[0]!=1||a[1]!=0||a[2]!=1||a[3]!=0;rt.SrcBlend=blend(a[0]);rt.DestBlend=blend(a[1]);rt.SrcBlendAlpha=blend(a[2]);rt.DestBlendAlpha=blend(a[3]);
        require(a[4]>=0&&a[4]<=4&&a[5]>=0&&a[5]<=4);rt.BlendOp=D3D11_BLEND_OP(a[4]+1);rt.BlendOpAlpha=D3D11_BLEND_OP(a[5]+1);rt.RenderTargetWriteMask=UINT8(a[6]);check(device->CreateBlendState(&b,&s->blendState));
        D3D11_DEPTH_STENCIL_DESC dd{};dd.DepthEnable=a[8]!=0;dd.DepthWriteMask=a[9]?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;
        // Unity reverses Z on D3D11; convert Less/Greater and their inclusive forms.
        int z=a[8];if(z==2)z=5;else if(z==5)z=2;else if(z==4)z=7;else if(z==7)z=4;
        dd.DepthFunc=z?D3D11_COMPARISON_FUNC(z):D3D11_COMPARISON_ALWAYS;
        dd.StencilReadMask=UINT8(a[14]);dd.StencilWriteMask=UINT8(a[15]);s->stencilRef=UINT(a[16]);
        auto face=[&](int off){D3D11_DEPTH_STENCILOP_DESC d{};d.StencilPassOp=stencil(a[off]);d.StencilFailOp=stencil(a[off+1]);d.StencilDepthFailOp=stencil(a[off+2]);d.StencilFunc=a[off+3]?D3D11_COMPARISON_FUNC(a[off+3]):D3D11_COMPARISON_ALWAYS;return d;};
        // Serialized front/back defaults do not override ShaderLab's unified
        // Stencil block. Non-default per-face descriptions override it.
        auto useCommon=[&](int off){return a[off]==0&&a[off+1]==0&&a[off+2]==0&&a[off+3]==8;};
        dd.FrontFace=face(useCommon(21)?17:21);dd.BackFace=face(useCommon(25)?17:25);
        auto enabled=[](const D3D11_DEPTH_STENCILOP_DESC& f){return f.StencilFunc!=D3D11_COMPARISON_ALWAYS||f.StencilPassOp!=D3D11_STENCIL_OP_KEEP||f.StencilFailOp!=D3D11_STENCIL_OP_KEEP||f.StencilDepthFailOp!=D3D11_STENCIL_OP_KEEP;};
        dd.StencilEnable=enabled(dd.FrontFace)||enabled(dd.BackFace);
        check(device->CreateDepthStencilState(&dd,&s->depth));
        D3D11_RASTERIZER_DESC rd{};rd.FillMode=D3D11_FILL_SOLID;require(a[10]>=0&&a[10]<=2);rd.CullMode=a[10]==0?D3D11_CULL_NONE:a[10]==1?D3D11_CULL_FRONT:D3D11_CULL_BACK;
        rd.FrontCounterClockwise=FALSE;rd.DepthClipEnable=a[7]!=0;rd.SlopeScaledDepthBias=-float(a[11])/1000000.f;rd.DepthBias=-a[12];check(device->CreateRasterizerState(&rd,&s->raster));
        shaders.push_back(s);return int(shaders.size());
    }
    int composite(const CompositeIn& in) {
        require(in.black&&in.white&&draws.size()<capacity*8);
        if(!compositor){
            // Two backgrounds recover out = black + destination * (white-black)
            // on all four channels. This also preserves UI alpha-squared blending.
            static const char code[]=R"(
                Texture2D<float4> b:register(t0);Texture2D<float4> w:register(t1);
                float4 vs(uint i:SV_VertexID):SV_Position { return float4(i==1?3:-1,i==2?-3:1,0,1); }
                struct O { float4 c:SV_Target0;float4 t:SV_Target1; };
                O ps(float4 p:SV_Position) { O o;int2 xy=int2(p.xy);float4 a=b.Load(int3(xy,0));float4 z=w.Load(int3(xy,0));
                    o.c=a;o.t=saturate(z-a);return o; }
            )";
            ComPtr<ID3DBlob> vs,ps,error;
            check(D3DCompile(code,sizeof(code)-1,nullptr,nullptr,nullptr,"vs","vs_4_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&vs,&error));
            check(D3DCompile(code,sizeof(code)-1,nullptr,nullptr,nullptr,"ps","ps_4_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&ps,&error));
            auto s=std::make_shared<Shader>();
            check(device->CreateVertexShader(vs->GetBufferPointer(),vs->GetBufferSize(),nullptr,&s->vs));
            check(device->CreatePixelShader(ps->GetBufferPointer(),ps->GetBufferSize(),nullptr,&s->ps));
            D3D11_BLEND_DESC bd{};auto& rt=bd.RenderTarget[0];rt.BlendEnable=TRUE;
            rt.SrcBlend=rt.SrcBlendAlpha=D3D11_BLEND_ONE;rt.DestBlend=D3D11_BLEND_SRC1_COLOR;rt.DestBlendAlpha=D3D11_BLEND_SRC1_ALPHA;
            rt.BlendOp=rt.BlendOpAlpha=D3D11_BLEND_OP_ADD;rt.RenderTargetWriteMask=15;
            check(device->CreateBlendState(&bd,&s->blendState));
            D3D11_DEPTH_STENCIL_DESC dd{};dd.DepthFunc=D3D11_COMPARISON_ALWAYS;check(device->CreateDepthStencilState(&dd,&s->depth));
            D3D11_RASTERIZER_DESC rd{};rd.FillMode=D3D11_FILL_SOLID;rd.CullMode=D3D11_CULL_NONE;rd.DepthClipEnable=TRUE;check(device->CreateRasterizerState(&rd,&s->raster));compositor=s;
        }
        auto d=std::make_shared<DrawCall>();d->composite=true;d->shader=compositor;
        auto view=[&](void* resource,ComPtr<ID3D11ShaderResourceView>& out){auto* t=static_cast<ID3D11Texture2D*>(resource);D3D11_TEXTURE2D_DESC td{};t->GetDesc(&td);require(td.SampleDesc.Count==1&&td.ArraySize==1);D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=format(td.Format,false);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;check(device->CreateShaderResourceView(t,&sd,&out));};
        view(in.black,d->black);view(in.white,d->white);draws.push_back(d);return int(draws.size());
    }
    int geometry(const GeometryIn& in) {
        auto g=std::make_shared<Geometry>();require(in.attributeCount&&in.attributeCount<=32&&in.attributes);
        for(unsigned i=0;i<8;i++){g->buffers[i]=static_cast<ID3D11Buffer*>(in.buffers[i]);g->strides[i]=in.strides[i];g->offsets[i]=in.offsets[i];}
        g->indices=static_cast<ID3D11Buffer*>(in.indices);g->indexFormat=DXGI_FORMAT(in.indexFormat);g->indexStart=in.indexStart;g->indexCount=in.indexCount;g->baseVertex=in.baseVertex;
        if(in.vertices){require(in.vertexBytes&&in.cpuIndices&&in.cpuIndexCount);D3D11_BUFFER_DESC b{};b.ByteWidth=in.vertexBytes;b.Usage=D3D11_USAGE_IMMUTABLE;b.BindFlags=D3D11_BIND_VERTEX_BUFFER;D3D11_SUBRESOURCE_DATA data{};data.pSysMem=in.vertices;check(device->CreateBuffer(&b,&data,&g->buffers[0]));b.ByteWidth=in.cpuIndexCount*4;b.BindFlags=D3D11_BIND_INDEX_BUFFER;data.pSysMem=in.cpuIndices;check(device->CreateBuffer(&b,&data,&g->indices));g->indexFormat=DXGI_FORMAT_R32_UINT;g->indexStart=0;g->indexCount=in.cpuIndexCount;g->baseVertex=0;}
        require(g->indices&&g->indexCount&&(g->indexFormat==DXGI_FORMAT_R16_UINT||g->indexFormat==DXGI_FORMAT_R32_UINT));
        for(unsigned i=0;i<in.attributeCount;i++){const auto& a=in.attributes[i];require(a.semantic<5&&a.stream<8&&g->buffers[a.stream]);D3D11_INPUT_ELEMENT_DESC d{};d.SemanticName=semantics[a.semantic];d.SemanticIndex=a.index;d.Format=DXGI_FORMAT(a.format);d.InputSlot=a.stream;d.AlignedByteOffset=a.offset;g->attributes.push_back(d);}
        geometries.push_back(g);return int(geometries.size());
    }
    int draw(const DrawIn& in) {
        require(in.program&&in.program<=shaders.size()&&in.geometry&&in.geometry<=geometries.size()&&in.pose<capacity);
        require(in.constantBytes<=16384&&in.constants&&in.bufferCount<=28&&in.textureCount<=32&&draws.size()<capacity*8);
        auto d=std::make_shared<DrawCall>();d->shader=shaders[in.program-1];d->geometry=geometries[in.geometry-1];d->pose=in.pose;d->grab=in.grab!=0;
        std::memcpy(d->center,in.center,12);std::memcpy(d->extent,in.extent,12);for(int i=0;i<3;i++)require(std::isfinite(d->center[i])&&std::isfinite(d->extent[i])&&d->extent[i]>=0);
        auto& g=*d->geometry;auto attributes=g.attributes;
        if(in.sprite){for(auto& a:attributes)if(std::strcmp(a.SemanticName,"COLOR")==0){a.InputSlot=7;a.AlignedByteOffset=0;a.Format=DXGI_FORMAT_R32G32B32A32_FLOAT;}D3D11_BUFFER_DESC color{};color.ByteWidth=16;color.Usage=D3D11_USAGE_DEFAULT;color.BindFlags=D3D11_BIND_VERTEX_BUFFER;check(device->CreateBuffer(&color,nullptr,&d->color));}
        check(device->CreateInputLayout(attributes.data(),UINT(attributes.size()),d->shader->signature.data(),d->shader->signature.size(),&d->layout));
        const auto* bytes=static_cast<const uint8_t*>(in.constants);d->bytes.assign(bytes,bytes+in.constantBytes);
        if(in.patchCount)d->patches.assign(in.patches,in.patches+in.patchCount);
        for(const auto& p:d->patches)require(p.kind>=1&&p.kind<=15&&p.offset+(p.kind<=7?64:16)<=in.constantBytes);
        for(unsigned i=0;i<in.bufferCount;i++){Constants c{};c.binding=in.buffers[i];require(c.binding.slot<14&&c.binding.stage<2&&c.binding.bytes>0&&c.binding.bytes%16==0&&c.binding.offset+c.binding.bytes<=in.constantBytes);D3D11_BUFFER_DESC b{};b.ByteWidth=c.binding.bytes;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&b,nullptr,&c.buffer));d->buffers.push_back(c);}
        for(unsigned i=0;i<in.textureCount;i++){const auto& t=in.textures[i];require((t.resource||t.srgb==-1)&&t.stage<2&&t.slot>=0&&t.slot<128&&t.sampler<16);Sample s{};s.stage=t.stage;s.slot=t.slot;s.samplerSlot=t.sampler;s.grab=t.srgb==-1;
            if(!s.grab){auto* tex=static_cast<ID3D11Texture2D*>(t.resource);D3D11_TEXTURE2D_DESC td{};tex->GetDesc(&td);require(td.ArraySize==1&&td.SampleDesc.Count==1);
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=format(td.Format,t.srgb!=0);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=td.MipLevels;check(device->CreateShaderResourceView(tex,&sd,&s.view));
            }
            D3D11_SAMPLER_DESC sm{};sm.Filter=t.filter==0?D3D11_FILTER_MIN_MAG_MIP_POINT:t.aniso>1?D3D11_FILTER_ANISOTROPIC:t.filter==2?D3D11_FILTER_MIN_MAG_MIP_LINEAR:D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT;
            auto addr=[](int x){return x==0?D3D11_TEXTURE_ADDRESS_WRAP:x==1?D3D11_TEXTURE_ADDRESS_CLAMP:x==2?D3D11_TEXTURE_ADDRESS_MIRROR:D3D11_TEXTURE_ADDRESS_MIRROR_ONCE;};sm.AddressU=addr(t.wrapU);sm.AddressV=addr(t.wrapV);sm.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;sm.MaxAnisotropy=UINT(std::clamp(t.aniso,1,16));sm.MaxLOD=D3D11_FLOAT32_MAX;check(device->CreateSamplerState(&sm,&s.sampler));d->textures.push_back(s);
        }
        draws.push_back(d);return int(draws.size());
    }
    int commit(const Frame& f) {
        const double start=now();require(f.poseCount<=capacity&&f.dirtyCount<=draws.size());
        if(write-read==ring.size()){stats.dropped++;return 0;}
        auto& p=ring[write%ring.size()];p.f=f;p.poseIds.assign(f.poseIds,f.poseIds+f.poseCount);p.poses.resize(f.poseCount);
        for(unsigned i=0;i<f.poseCount;i++){require(f.poseIds[i]<capacity);p.poses[i]=f.poses[f.poseIds[i]];}
        p.ids.assign(f.drawIds,f.drawIds+f.dirtyCount);p.offsets.assign(f.constantOffsets,f.constantOffsets+f.dirtyCount+1);
        require(f.constantBytes<=size_t(capacity)*16384&&p.offsets.back()==f.constantBytes);
        p.bytes.assign(f.constants,f.constants+f.constantBytes);
        if(f.listChanged){require(f.listCount<=capacity*4&&f.segmentCount<=65535);p.list.assign(f.list,f.list+f.listCount);p.segs.assign(f.segments,f.segments+size_t(f.segmentCount)*2);}
        write++;commitSum+=now()-start;commits++;return 1;
    }
    void apply(Packet& p) {
        frame=p.f;
        for(size_t i=0;i<p.poseIds.size();i++)poses[p.poseIds[i]]=p.poses[i];
        for(size_t i=0;i<p.ids.size();i++){require(p.ids[i]&&p.ids[i]<=draws.size());auto& d=*draws[p.ids[i]-1];require(p.offsets[i+1]-p.offsets[i]==d.bytes.size());std::memcpy(d.bytes.data(),p.bytes.data()+p.offsets[i],d.bytes.size());stats.bytes+=d.bytes.size();}
        if(p.f.listChanged){list=p.list;segs=p.segs;}
        stats.frames++;
    }
    void patch(DrawCall& d) {
        const auto& pose=poses[d.pose];
        for(const auto& p:d.patches){void* out=d.bytes.data()+p.offset;const float* src=nullptr;float temp[16]{};
            switch(p.kind){case 1:src=pose.world;break;case 2:src=pose.inverse;break;case 3:src=frame.vp;break;case 4:src=frame.view;break;case 5:src=frame.projection;break;
            case 6:multiply(temp,frame.vp,pose.world);src=temp;break;
            case 7:{float mv[16];multiply(mv,pose.inverse,frame.view);for(int c=0;c<4;c++)for(int r=0;r<4;r++)temp[c*4+r]=mv[r*4+c];src=temp;break;}
            case 8:src=frame.camera;break;case 9:src=frame.screen;break;case 10:src=frame.projectionParams;break;case 11:src=frame.ortho;break;case 12:src=frame.time;break;case 13:src=frame.sinTime;break;case 14:src=frame.cosTime;break;case 15:src=frame.delta;break;default:throw E_INVALIDARG;}
            std::memcpy(out,src,p.kind<=7?64:16);
        }
    }
    void render(unsigned segment) {
        const double start=now();
        try {
            if(segment==0){require(read!=write);auto& p=ring[read%ring.size()];apply(p);read++;}
            require(size_t(segment)*2+1<segs.size());
            UINT first=segs[segment*2],count=segs[segment*2+1];require(first+count<=list.size());
            ComPtr<ID3D11RenderTargetView> rtv;ComPtr<ID3D11DepthStencilView> dsv;context->OMGetRenderTargets(1,&rtv,&dsv);require(rtv!=nullptr);
            if(segment==0&&frame.clearStencil){require(dsv!=nullptr);D3D11_DEPTH_STENCIL_VIEW_DESC desc{};dsv->GetDesc(&desc);require(desc.Format==DXGI_FORMAT_D24_UNORM_S8_UINT||desc.Format==DXGI_FORMAT_D32_FLOAT_S8X24_UINT);context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_STENCIL,0,0);}
            UINT viewportCount=1;D3D11_VIEWPORT viewport{};context->RSGetViewports(&viewportCount,&viewport);require(viewportCount==1);
            auto* dc=deferred.Get();ID3D11RenderTargetView* rt=rtv.Get();dc->OMSetRenderTargets(1,&rt,dsv.Get());dc->RSSetViewports(1,&viewport);dc->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            dc->GSSetShader(nullptr,nullptr,0);dc->HSSetShader(nullptr,nullptr,0);dc->DSSetShader(nullptr,nullptr,0);
            for(UINT i=first;i<first+count;i++){
                require(list[i]&&list[i]<=draws.size());auto& d=*draws[list[i]-1];auto& s=*d.shader;
                if(d.composite){
                    dc->VSSetShader(s.vs.Get(),nullptr,0);dc->PSSetShader(s.ps.Get(),nullptr,0);dc->IASetInputLayout(nullptr);
                    dc->OMSetBlendState(s.blendState.Get(),nullptr,~0u);dc->OMSetDepthStencilState(s.depth.Get(),0);dc->RSSetState(s.raster.Get());
                    ID3D11ShaderResourceView* views[]={d.black.Get(),d.white.Get()};dc->PSSetShaderResources(0,2,views);dc->Draw(3,0);
                    ID3D11ShaderResourceView* nil[2]{};dc->PSSetShaderResources(0,2,nil);stats.draws++;continue;
                }
                auto& g=*d.geometry;
                if(d.extent[0]>0||d.extent[1]>0||d.extent[2]>0){
                    float m[16];multiply(m,frame.vp,poses[d.pose].world);unsigned outside=63;
                    for(unsigned corner=0;corner<8&&outside;corner++){float p[3];for(unsigned axis=0;axis<3;axis++)p[axis]=d.center[axis]+((corner&(1u<<axis))?d.extent[axis]:-d.extent[axis]);float clip[4];for(unsigned row=0;row<4;row++)clip[row]=m[row]*p[0]+m[4+row]*p[1]+m[8+row]*p[2]+m[12+row];unsigned flags=(clip[0]<-clip[3]?1u:0u)|(clip[0]>clip[3]?2u:0u)|(clip[1]<-clip[3]?4u:0u)|(clip[1]>clip[3]?8u:0u)|(clip[2]<0?16u:0u)|(clip[2]>clip[3]?32u:0u);outside&=flags;}
                    if(outside)continue;
                }
                if(d.grab){
                    ComPtr<ID3D11Resource> resource;rtv->GetResource(&resource);ComPtr<ID3D11Texture2D> source;check(resource.As(&source));D3D11_TEXTURE2D_DESC td{};source->GetDesc(&td);require(td.SampleDesc.Count==1&&td.ArraySize==1);
                    D3D11_RENDER_TARGET_VIEW_DESC vd{};rtv->GetDesc(&vd);require(vd.ViewDimension==D3D11_RTV_DIMENSION_TEXTURE2D&&vd.Texture2D.MipSlice==0);
                    if(!grabTexture||td.Width!=grabDesc.Width||td.Height!=grabDesc.Height||td.Format!=grabDesc.Format||vd.Format!=grabFormat){grabDesc=td;grabFormat=vd.Format;grabTexture.Reset();grabView.Reset();td.BindFlags=D3D11_BIND_SHADER_RESOURCE;td.Usage=D3D11_USAGE_DEFAULT;td.CPUAccessFlags=0;td.MiscFlags=0;check(device->CreateTexture2D(&td,nullptr,&grabTexture));D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=vd.Format;sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=td.MipLevels;check(device->CreateShaderResourceView(grabTexture.Get(),&sv,&grabView));}
                    ID3D11ShaderResourceView* nil[16]{};dc->PSSetShaderResources(0,16,nil);dc->OMSetRenderTargets(0,nullptr,nullptr);dc->CopyResource(grabTexture.Get(),source.Get());dc->OMSetRenderTargets(1,&rt,dsv.Get());
                }
                patch(d);dc->VSSetShader(s.vs.Get(),nullptr,0);dc->PSSetShader(s.ps.Get(),nullptr,0);dc->IASetInputLayout(d.layout.Get());
                ID3D11Buffer* vb[8]{};for(unsigned j=0;j<8;j++)vb[j]=g.buffers[j].Get();auto strides=g.strides;auto offsets=g.offsets;if(d.color){dc->UpdateSubresource(d.color.Get(),0,nullptr,d.bytes.data(),0,0);vb[7]=d.color.Get();strides[7]=offsets[7]=0;}dc->IASetVertexBuffers(0,8,vb,strides.data(),offsets.data());dc->IASetIndexBuffer(g.indices.Get(),g.indexFormat,0);
                dc->OMSetBlendState(s.blendState.Get(),nullptr,~0u);dc->OMSetDepthStencilState(s.depth.Get(),s.stencilRef);dc->RSSetState(s.raster.Get());
                for(const auto& c:d.buffers){dc->UpdateSubresource(c.buffer.Get(),0,nullptr,d.bytes.data()+c.binding.offset,0,0);ID3D11Buffer* cb=c.buffer.Get();if(c.binding.stage==0)dc->VSSetConstantBuffers(c.binding.slot,1,&cb);else dc->PSSetConstantBuffers(c.binding.slot,1,&cb);}
                for(const auto& t:d.textures){ID3D11ShaderResourceView* v=t.grab?grabView.Get():t.view.Get();ID3D11SamplerState* sm=t.sampler.Get();if(t.stage==0){dc->VSSetShaderResources(UINT(t.slot),1,&v);if(t.samplerSlot>=0)dc->VSSetSamplers(UINT(t.samplerSlot),1,&sm);}else{dc->PSSetShaderResources(UINT(t.slot),1,&v);if(t.samplerSlot>=0)dc->PSSetSamplers(UINT(t.samplerSlot),1,&sm);}}
                dc->DrawIndexed(g.indexCount,g.indexStart,g.baseVertex);stats.draws++;
            }
            ComPtr<ID3D11CommandList> command;check(dc->FinishCommandList(FALSE,&command));context->ExecuteCommandList(command.Get(),TRUE);
        }catch(HRESULT hr){stats.errors++;stats.lastHR=hr;deferred->ClearState();ComPtr<ID3D11CommandList> discard;deferred->FinishCommandList(FALSE,&discard);}
        catch(...){stats.errors++;stats.lastHR=E_FAIL;}
        stats.events++;renderSum+=now()-start;
    }
};
std::mutex gate;std::shared_ptr<Renderer> current;int nextToken=0;thread_local HRESULT lastHR=S_OK;
std::shared_ptr<Renderer> get(int token){std::lock_guard<std::mutex> lock(gate);return current&&current->token==token?current:nullptr;}
void __stdcall event(int id) noexcept {try{auto r=get(id>>16);if(r){std::lock_guard<std::mutex> lock(r->mutex);r->render(unsigned(id&65535));}}catch(...){}}
}
extern "C" intptr_t __cdecl sf_z_drfull_v1(int op,int token,const void* args) noexcept {
    try {
        if(op==drfull::Create){require(args!=nullptr);auto r=std::make_shared<Renderer>(*static_cast<const drfull::CreateIn*>(args));std::lock_guard<std::mutex> lock(gate);require(!current&&nextToken<32767);r->token=++nextToken;current=r;return r->token;}
        if(op==drfull::Abi){require(args!=nullptr);*const_cast<drfull::AbiInfo*>(static_cast<const drfull::AbiInfo*>(args))={1,sizeof(drfull::CreateIn),sizeof(drfull::ProgramIn),sizeof(drfull::GeometryIn),sizeof(drfull::DrawIn),sizeof(drfull::Frame),sizeof(drfull::Statistics),sizeof(drfull::Pose)};return 1;}
        if(op==drfull::Event)return reinterpret_cast<intptr_t>(&event);
        if(op==drfull::Error)return lastHR;
        if(op==drfull::Destroy){std::lock_guard<std::mutex> lock(gate);if(current&&current->token==token)current.reset();return 1;}
        auto r=get(token);require(r!=nullptr);std::lock_guard<std::mutex> lock(r->mutex);
        switch(op){case drfull::Composite:return r->composite(*static_cast<const drfull::CompositeIn*>(args));case drfull::Program:return r->program(*static_cast<const drfull::ProgramIn*>(args));case drfull::Geometry:return r->geometry(*static_cast<const drfull::GeometryIn*>(args));case drfull::Draw:return r->draw(*static_cast<const drfull::DrawIn*>(args));case drfull::Commit:return r->commit(*static_cast<const drfull::Frame*>(args));case drfull::Stats:{auto* s=const_cast<drfull::Statistics*>(static_cast<const drfull::Statistics*>(args));*s=r->stats;s->renderUs=r->stats.frames?r->renderSum/double(r->stats.frames):0;s->commitUs=r->commits?r->commitSum/double(r->commits):0;return 1;}default:throw E_INVALIDARG;}
    }catch(HRESULT hr){lastHR=hr;return 0;}catch(...){lastHR=E_FAIL;return 0;}
}
