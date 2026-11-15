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
#include <map>
#include <tuple>
#include <atomic>
#include "drfull.h"
namespace {
using Microsoft::WRL::ComPtr;
using namespace drfull;
void check(HRESULT hr) { if(FAILED(hr)) throw hr; }
void require(bool b) { if(!b) throw E_INVALIDARG; }
double now() { static const double scale=[](){LARGE_INTEGER f;QueryPerformanceFrequency(&f);return 1e6/double(f.QuadPart);}();LARGE_INTEGER c;QueryPerformanceCounter(&c);return double(c.QuadPart)*scale; }
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
struct BatchVertex { float position[3],color[4],uv[2],uv1[2]; };
struct LocalVertex { BatchVertex vertex;uint32_t draw; };
struct GpuPose { float world[16];uint32_t live=1,pad[3]{}; };
struct GpuDraw { float color[4]{};uint32_t pose=0,sprite=0;float flip[2]{1,1}; };
static_assert(sizeof(BatchVertex)==44&&sizeof(LocalVertex)==48&&sizeof(GpuPose)==80&&sizeof(GpuDraw)==32,"GPU stream ABI");
struct Geometry {
    std::vector<BatchVertex> cpu;std::vector<uint32_t> cpuIndices;std::map<const Shader*,ComPtr<ID3D11InputLayout>> batchLayouts;
    std::array<ComPtr<ID3D11Buffer>,8> buffers;std::array<UINT,8> strides{},offsets{};
    ComPtr<ID3D11Buffer> indices;DXGI_FORMAT indexFormat;UINT indexStart,indexCount;INT baseVertex;
    std::vector<D3D11_INPUT_ELEMENT_DESC> attributes;
    std::map<std::pair<const Shader*,bool>,ComPtr<ID3D11InputLayout>> layouts;
};
struct Sample { ComPtr<ID3D11ShaderResourceView> view;ComPtr<ID3D11SamplerState> sampler;int stage,slot,samplerSlot;bool grab=false; };
struct ConstantStorage { ComPtr<ID3D11Buffer> buffer;std::vector<uint8_t> uploaded;bool ready=false; };
struct Constants { Buffer binding;std::shared_ptr<ConstantStorage> storage; };
struct DrawCall {
    std::shared_ptr<Shader> shader;std::shared_ptr<Geometry> geometry;ComPtr<ID3D11InputLayout> layout,batchLayout;
    std::vector<Constants> buffers;std::vector<Patch> patches;std::vector<Sample> textures;std::vector<uint8_t> bytes;uint32_t pose;bool grab=false,batchable=false,sprite=false;ComPtr<ID3D11Buffer> color;
    float center[3]{},extent[3]{};float rendererTint[4]{1,1,1,1},flip[4]{1,1,1,1};bool spritePatches=false;
    std::vector<uint8_t> batchKey,nextBatchKey;uint64_t batchHash=0;std::vector<BatchVertex> baked;
    bool composite=false;ComPtr<ID3D11ShaderResourceView> black,white;
};
struct Packet {
    Frame f{};bool worldOnly=false;std::vector<uint32_t> liveIds;std::vector<uint8_t> liveValues;std::vector<Pose> poses;std::vector<uint32_t> poseIds,ids,offsets,list,segs;std::vector<uint8_t> bytes;
};
void multiply(float* out,const float* a,const float* b) {
    for(int c=0;c<4;c++)for(int r=0;r<4;r++){float v=0;for(int k=0;k<4;k++)v+=a[k*4+r]*b[c*4+k];out[c*4+r]=v;}
}
struct Renderer {
    int token=0;uint32_t capacity;
    ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context,deferred;
    std::mutex mutex;std::vector<std::shared_ptr<Shader>> shaders;std::vector<std::shared_ptr<Geometry>> geometries;std::vector<std::shared_ptr<DrawCall>> draws;
    std::array<Packet,8> ring;std::atomic<uint64_t> read{0},write{0};std::vector<Pose> poses;
    std::vector<uint32_t> list,segs;Frame frame{};Statistics stats{};double renderSum=0,commitSum=0;uint64_t commits=0;
    ComPtr<ID3D11Texture2D> grabTexture;ComPtr<ID3D11ShaderResourceView> grabView;D3D11_TEXTURE2D_DESC grabDesc{};DXGI_FORMAT grabFormat=DXGI_FORMAT_UNKNOWN;
    std::shared_ptr<Shader> compositor;
    Performance perf{};BatchPerformance batchPerf{};bool profile=false;
    std::vector<uint8_t> live;std::vector<uint64_t> poseVersions;std::vector<uint32_t> batchEnds;bool batchesDirty=true;
    static constexpr UINT batchLimit=65532;
    ComPtr<ID3D11Buffer> batchBuffer,sharedColor;std::array<uint8_t,16> uploadedColor{};bool colorUploaded=false;
    std::vector<BatchVertex> batchVertices;size_t batchVertexCount=0;
    struct BatchItem { const BatchVertex* cpu=nullptr;const uint32_t* indices=nullptr;BatchVertex* baked=nullptr;const float* tint=nullptr;const float* rendererTint=nullptr;const float* flip=nullptr;uint32_t pose=0,id=0;size_t count=0;uint64_t poseVersion=UINT64_MAX,colorVersion=UINT64_MAX;bool sprite=false; };
    bool gpuBatch=false,geometryDirty=true,gpuPoseDirty=true,gpuDrawDirty=true,gpuVerticesDirty=true,hasBatches=false;
    ComPtr<ID3D11ComputeShader> bakeShader;
    ComPtr<ID3D11Buffer> gpuLocal,gpuWorld,gpuDraw,gpuVertices;
    ComPtr<ID3D11ShaderResourceView> localView,worldView,drawView;ComPtr<ID3D11UnorderedAccessView> verticesUav;
    UINT localCapacity=0,worldCapacity=0,drawCapacity=0;
    std::vector<LocalVertex> localVertices;std::vector<uint32_t> vertexOffsets;
    std::vector<GpuPose> gpuPoses;std::vector<GpuDraw> gpuDraws;
    std::vector<BatchItem> batchItems;std::vector<uint64_t> colorVersions;
    std::map<std::tuple<const Shader*,uint32_t,uint32_t,uint32_t>,std::shared_ptr<ConstantStorage>> constantPool;
    std::atomic<uint64_t> publishedFrames{0},publishedDraws{0},publishedErrors{0},publishedBytes{0},publishedEvents{0},dropped{0};
    std::atomic<int32_t> publishedHR{0};std::atomic<double> publishedRenderUs{0};
    Statistics snapshot() {
        Statistics s{};s.frames=publishedFrames.load(std::memory_order_acquire);s.draws=publishedDraws.load(std::memory_order_relaxed);
        s.errors=publishedErrors.load(std::memory_order_relaxed);s.bytes=publishedBytes.load(std::memory_order_relaxed);s.events=publishedEvents.load(std::memory_order_relaxed);
        s.lastHR=publishedHR.load(std::memory_order_relaxed);s.dropped=dropped.load(std::memory_order_relaxed);s.renderUs=publishedRenderUs.load(std::memory_order_relaxed);
        s.commitUs=commits?commitSum/double(commits):0;return s;
    }
    std::map<std::pair<void*,int>,ComPtr<ID3D11ShaderResourceView>> textureViews;
    std::map<std::tuple<int,int,int,int>,ComPtr<ID3D11SamplerState>> samplers;
    Renderer(const CreateIn& in):capacity(in.capacity) {
        require(in.resource&&in.abi==1&&capacity>0&&capacity<=4000000);
        static_cast<ID3D11DeviceChild*>(in.resource)->GetDevice(&device);require(device!=nullptr);
        require((device->GetCreationFlags()&D3D11_CREATE_DEVICE_SINGLETHREADED)==0);
        device->GetImmediateContext(&context);check(device->CreateDeferredContext(0,&deferred));
        live.resize(capacity,1);poseVersions.resize(capacity);batchEnds.reserve(size_t(capacity)*4);batchVertices.resize(batchLimit);batchItems.reserve(size_t(capacity)*4);colorVersions.reserve(size_t(capacity)*8);poses.reserve(capacity);gpuPoses.reserve(capacity);gpuDraws.reserve(size_t(capacity)*8);vertexOffsets.reserve(size_t(capacity)*4+1);localVertices.reserve(size_t(capacity)*6);
        D3D11_BUFFER_DESC vb{};vb.ByteWidth=batchLimit*sizeof(BatchVertex);vb.Usage=D3D11_USAGE_DYNAMIC;vb.BindFlags=D3D11_BIND_VERTEX_BUFFER;vb.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;check(device->CreateBuffer(&vb,nullptr,&batchBuffer));
        vb.ByteWidth=16;vb.Usage=D3D11_USAGE_DEFAULT;vb.CPUAccessFlags=0;check(device->CreateBuffer(&vb,nullptr,&sharedColor));
        if(device->GetFeatureLevel()>=D3D_FEATURE_LEVEL_11_0){
            static const ComPtr<ID3DBlob> code=[](){
                static const char source[]=R"(
                    struct V { float3 position;float4 color;float2 uv;float2 uv1;uint draw; };
                    struct P { float4x4 world;uint live;uint3 pad; };
                    struct D { float4 color;uint pose;uint sprite;float2 flip; };
                    StructuredBuffer<V> source:register(t0);StructuredBuffer<P> poses:register(t1);StructuredBuffer<D> draws:register(t2);
                    RWByteAddressBuffer output:register(u0);
                    [numthreads(64,1,1)] void main(uint3 id:SV_DispatchThreadID){
                        uint count,stride;source.GetDimensions(count,stride);if(id.x>=count)return;
                        V v=source[id.x];D d=draws[v.draw];P p=poses[d.pose];
                        float3 position=p.live!=0?mul(p.world,float4(v.position.xy*d.flip,v.position.z,1)).xyz:float3(0,0,0);
                        uint at=id.x*44;output.Store3(at,asuint(position));output.Store4(at+12,asuint(d.sprite!=0?d.color:v.color));
                        output.Store2(at+28,asuint(v.uv));output.Store2(at+36,asuint(v.uv1));
                    }
                )";
                ComPtr<ID3DBlob> blob,error;check(D3DCompile(source,sizeof(source)-1,nullptr,nullptr,nullptr,"main","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3|D3DCOMPILE_IEEE_STRICTNESS,0,&blob,&error));return blob;
            }();
            check(device->CreateComputeShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&bakeShader));gpuBatch=true;batchPerf.gpuAvailable=1;
        }
        list.reserve(capacity*4);segs.reserve(131072);draws.reserve(capacity*4);
        for(auto& p:ring){p.liveIds.reserve(capacity);p.liveValues.reserve(capacity);p.poses.reserve(capacity);gpuPoses.reserve(capacity);gpuDraws.reserve(size_t(capacity)*8);vertexOffsets.reserve(size_t(capacity)*4+1);localVertices.reserve(size_t(capacity)*6);p.poseIds.reserve(capacity);p.ids.reserve(capacity*4);p.offsets.reserve(capacity*4+1);p.bytes.reserve(size_t(std::min(capacity,8192u))*1024);p.list.reserve(capacity*4);p.segs.reserve(131072);}
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
        view(in.black,d->black);view(in.white,d->white);draws.push_back(d);colorVersions.push_back(0);gpuDraws.emplace_back();gpuDrawDirty=true;return int(draws.size());
    }
    int geometry(const GeometryIn& in) {
        auto g=std::make_shared<Geometry>();require(in.attributeCount&&in.attributeCount<=32&&in.attributes);
        for(unsigned i=0;i<8;i++){g->buffers[i]=static_cast<ID3D11Buffer*>(in.buffers[i]);g->strides[i]=in.strides[i];g->offsets[i]=in.offsets[i];}
        g->indices=static_cast<ID3D11Buffer*>(in.indices);g->indexFormat=DXGI_FORMAT(in.indexFormat);g->indexStart=in.indexStart;g->indexCount=in.indexCount;g->baseVertex=in.baseVertex;
        if(in.vertices){require(in.vertexBytes&&in.cpuIndices&&in.cpuIndexCount);D3D11_BUFFER_DESC b{};b.ByteWidth=in.vertexBytes;b.Usage=D3D11_USAGE_IMMUTABLE;b.BindFlags=D3D11_BIND_VERTEX_BUFFER;D3D11_SUBRESOURCE_DATA data{};data.pSysMem=in.vertices;check(device->CreateBuffer(&b,&data,&g->buffers[0]));b.ByteWidth=in.cpuIndexCount*4;b.BindFlags=D3D11_BIND_INDEX_BUFFER;data.pSysMem=in.cpuIndices;check(device->CreateBuffer(&b,&data,&g->indices));g->indexFormat=DXGI_FORMAT_R32_UINT;g->indexStart=0;g->indexCount=in.cpuIndexCount;g->baseVertex=0;}
        require(g->indices&&g->indexCount&&(g->indexFormat==DXGI_FORMAT_R16_UINT||g->indexFormat==DXGI_FORMAT_R32_UINT));
        for(unsigned i=0;i<in.attributeCount;i++){const auto& a=in.attributes[i];require(a.semantic<5&&a.stream<8&&g->buffers[a.stream]);D3D11_INPUT_ELEMENT_DESC d{};d.SemanticName=semantics[a.semantic];d.SemanticIndex=a.index;d.Format=DXGI_FORMAT(a.format);d.InputSlot=a.stream;d.AlignedByteOffset=a.offset;g->attributes.push_back(d);}
        if(in.vertices && in.strides[0] && in.vertexBytes%in.strides[0]==0) {
            bool canonical=true;for(const auto& a:g->attributes)canonical &= a.InputSlot==0 && ((std::strcmp(a.SemanticName,"POSITION")==0&&a.Format==DXGI_FORMAT_R32G32B32_FLOAT&&a.AlignedByteOffset==0)||(std::strcmp(a.SemanticName,"COLOR")==0&&a.Format==DXGI_FORMAT_R32G32B32A32_FLOAT&&a.AlignedByteOffset==12)||(std::strcmp(a.SemanticName,"TEXCOORD")==0&&a.Format==DXGI_FORMAT_R32G32_FLOAT&&a.AlignedByteOffset==(a.SemanticIndex==0?28u:36u)&&a.SemanticIndex<=1));
            if(canonical&&(in.strides[0]==36||in.strides[0]==44)) {
                const auto* bytes=static_cast<const uint8_t*>(in.vertices);g->cpu.resize(in.vertexBytes/in.strides[0]);
                for(size_t i=0;i<g->cpu.size();i++)std::memcpy(&g->cpu[i],bytes+i*in.strides[0],in.strides[0]);
                g->cpuIndices.assign(in.cpuIndices,in.cpuIndices+in.cpuIndexCount);for(auto index:g->cpuIndices)require(index<g->cpu.size());
            }
        }
        geometries.push_back(g);return int(geometries.size());
    }
    int cpuGeometry(const CpuGeometry& in) {
        require(in.geometry&&in.geometry<=geometries.size()&&in.vertices&&in.indices&&in.count&&in.indexCount&&in.indexCount%3==0);
        auto& g=*geometries[in.geometry-1];require(g.layouts.empty()&&in.indexCount==g.indexCount);const auto* vertices=static_cast<const BatchVertex*>(in.vertices);
        g.cpu.assign(vertices,vertices+in.count);g.cpuIndices.assign(in.indices,in.indices+in.indexCount);for(auto i:g.cpuIndices)require(i<in.count);return 1;
    }
    int draw(const DrawIn& in) {
        require(in.program&&in.program<=shaders.size()&&in.geometry&&in.geometry<=geometries.size()&&in.pose<capacity);
        require(in.constantBytes<=16384&&in.constants&&in.bufferCount<=28&&in.textureCount<=32&&draws.size()<capacity*8);
        auto d=std::make_shared<DrawCall>();d->shader=shaders[in.program-1];d->geometry=geometries[in.geometry-1];d->pose=in.pose;d->grab=in.grab!=0;d->sprite=(in.sprite&1)!=0;d->batchable=(in.sprite&2)!=0&&!d->grab&&!d->geometry->cpu.empty();
        std::memcpy(d->center,in.center,12);std::memcpy(d->extent,in.extent,12);for(int i=0;i<3;i++)require(std::isfinite(d->center[i])&&std::isfinite(d->extent[i])&&d->extent[i]>=0);
        auto& g=*d->geometry;auto attributes=g.attributes;
        if(d->sprite){for(auto& a:attributes)if(std::strcmp(a.SemanticName,"COLOR")==0){a.InputSlot=7;a.AlignedByteOffset=0;a.Format=DXGI_FORMAT_R32G32B32A32_FLOAT;}d->color=sharedColor;}
        auto layoutKey=std::make_pair(d->shader.get(),d->sprite);auto cached=g.layouts.find(layoutKey);
        if(cached!=g.layouts.end()){d->layout=cached->second;perf.layoutHits++;}
        else {check(device->CreateInputLayout(attributes.data(),UINT(attributes.size()),d->shader->signature.data(),d->shader->signature.size(),&d->layout));g.layouts.emplace(layoutKey,d->layout);perf.layoutCreates++;}
        if(poses.size()<=in.pose){poses.resize(size_t(in.pose)+1);gpuPoses.resize(poses.size());gpuPoseDirty=true;}
        const auto* bytes=static_cast<const uint8_t*>(in.constants);d->bytes.assign(bytes,bytes+in.constantBytes);
        if(in.patchCount)d->patches.assign(in.patches,in.patches+in.patchCount);
        captureSpriteProperties(*d);
        for(const auto& p:d->patches)require(p.kind>=1&&p.kind<=17&&p.offset+(p.kind<=7?64:16)<=in.constantBytes);
        for(unsigned i=0;i<in.bufferCount;i++){Constants c{};c.binding=in.buffers[i];require(c.binding.slot<14&&c.binding.stage<2&&c.binding.bytes>0&&c.binding.bytes%16==0&&c.binding.offset+c.binding.bytes<=in.constantBytes);auto key=std::make_tuple(d->shader.get(),c.binding.stage,c.binding.slot,c.binding.bytes);auto& storage=constantPool[key];if(!storage){storage=std::make_shared<ConstantStorage>();D3D11_BUFFER_DESC b{};b.ByteWidth=c.binding.bytes;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&b,nullptr,&storage->buffer));storage->uploaded.resize(c.binding.bytes);batchPerf.constantCreates++;}c.storage=storage;d->buffers.push_back(std::move(c));}
        for(unsigned i=0;i<in.textureCount;i++){const auto& t=in.textures[i];require((t.resource||t.srgb==-1)&&t.stage<2&&t.slot>=0&&t.slot<128&&t.sampler<16);Sample s{};s.stage=t.stage;s.slot=t.slot;s.samplerSlot=t.sampler;s.grab=t.srgb==-1;
            if(!s.grab){auto* tex=static_cast<ID3D11Texture2D*>(t.resource);D3D11_TEXTURE2D_DESC td{};tex->GetDesc(&td);require(td.ArraySize==1&&td.SampleDesc.Count==1);
            auto key=std::make_pair(t.resource,t.srgb);auto found=textureViews.find(key);
            if(found!=textureViews.end())s.view=found->second;
            else {D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=format(td.Format,t.srgb!=0);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=td.MipLevels;check(device->CreateShaderResourceView(tex,&sd,&s.view));textureViews.emplace(key,s.view);}
            }
            D3D11_SAMPLER_DESC sm{};sm.Filter=t.filter==0?D3D11_FILTER_MIN_MAG_MIP_POINT:t.aniso>1?D3D11_FILTER_ANISOTROPIC:t.filter==2?D3D11_FILTER_MIN_MAG_MIP_LINEAR:D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT;
            auto addr=[](int x){return x==0?D3D11_TEXTURE_ADDRESS_WRAP:x==1?D3D11_TEXTURE_ADDRESS_CLAMP:x==2?D3D11_TEXTURE_ADDRESS_MIRROR:D3D11_TEXTURE_ADDRESS_MIRROR_ONCE;};sm.AddressU=addr(t.wrapU);sm.AddressV=addr(t.wrapV);sm.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;sm.MaxAnisotropy=UINT(std::clamp(t.aniso,1,16));sm.MaxLOD=D3D11_FLOAT32_MAX;auto key=std::make_tuple(t.filter,t.wrapU,t.wrapV,t.aniso);auto found=samplers.find(key);
            if(found!=samplers.end())s.sampler=found->second;else {check(device->CreateSamplerState(&sm,&s.sampler));samplers.emplace(key,s.sampler);}d->textures.push_back(s);
        }
        if(d->batchable) {
            auto& layout=d->geometry->batchLayouts[d->shader.get()];
            if(!layout){D3D11_INPUT_ELEMENT_DESC a[]={{"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},{"COLOR",0,DXGI_FORMAT_R32G32B32A32_FLOAT,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},{"TEXCOORD",0,DXGI_FORMAT_R32G32_FLOAT,0,28,D3D11_INPUT_PER_VERTEX_DATA,0},{"TEXCOORD",1,DXGI_FORMAT_R32G32_FLOAT,0,36,D3D11_INPUT_PER_VERTEX_DATA,0}};check(device->CreateInputLayout(a,4,d->shader->signature.data(),d->shader->signature.size(),&layout));}
            d->batchLayout=layout;d->batchKey.resize(d->bytes.size());d->nextBatchKey.resize(d->bytes.size());d->baked.resize(d->geometry->cpuIndices.size());updateBatchKey(*d);
        }
        GpuDraw info{};info.pose=d->pose;info.sprite=d->sprite?1u:0u;for(int c=0;c<4;c++)info.color[c]=reinterpret_cast<const float*>(d->bytes.data())[c]*d->rendererTint[c];info.flip[0]=d->flip[0];info.flip[1]=d->flip[1];
        draws.push_back(d);colorVersions.push_back(0);gpuDraws.push_back(info);gpuDrawDirty=true;return int(draws.size());
    }
    void captureSpriteProperties(DrawCall& d) {
        for(const auto& p:d.patches){
            if(p.kind==16){require(p.offset+16<=d.bytes.size());std::memcpy(d.rendererTint,d.bytes.data()+p.offset,16);d.spritePatches=true;}
            if(p.kind==17){require(p.offset+8<=d.bytes.size());std::memcpy(d.flip,d.bytes.data()+p.offset,8);d.spritePatches=true;}
        }
    }
    void updateBatchKey(DrawCall& d) {
        if(!d.batchable)return;
        std::memcpy(d.nextBatchKey.data(),d.bytes.data(),d.bytes.size());
        if(d.sprite)std::memset(d.nextBatchKey.data(),0,16);
        for(const auto& p:d.patches)std::memset(d.nextBatchKey.data()+p.offset,0,p.kind<=7?64:16);
        if(d.batchKey!=d.nextBatchKey)batchesDirty=true;d.batchKey.swap(d.nextBatchKey);
        d.batchHash=1469598103934665603ull;for(auto x:d.batchKey){d.batchHash^=x;d.batchHash*=1099511628211ull;}
    }
    int commit(const Frame& f,bool packed=false,const FrameActive* active=nullptr,bool worldOnly=false) {
        const double start=now();require(f.poseCount<=capacity&&f.dirtyCount<=draws.size());
        const uint64_t w=write.load(std::memory_order_relaxed);
        if(w-read.load(std::memory_order_acquire)==ring.size()){dropped.fetch_add(1,std::memory_order_relaxed);return 0;}
        auto& p=ring[w%ring.size()];p.f=f;p.worldOnly=worldOnly;p.liveIds.clear();p.liveValues.clear();if(active){require(active->count<=capacity);for(uint32_t i=0;i<active->count;i++){require(active->ids[i]<capacity&&active->values[i]<=1);p.liveIds.push_back(active->ids[i]);p.liveValues.push_back(active->values[i]);}}p.poseIds.assign(f.poseIds,f.poseIds+f.poseCount);p.poses.resize(f.poseCount);
        for(unsigned i=0;i<f.poseCount;i++){require(f.poseIds[i]<capacity);if(worldOnly)std::memcpy(p.poses[i].world,reinterpret_cast<const float*>(f.poses)+size_t(i)*16,64);else p.poses[i]=f.poses[packed?i:f.poseIds[i]];}
        p.ids.assign(f.drawIds,f.drawIds+f.dirtyCount);p.offsets.assign(f.constantOffsets,f.constantOffsets+f.dirtyCount+1);
        require(f.constantBytes<=size_t(capacity)*16384&&p.offsets.back()==f.constantBytes);
        p.bytes.assign(f.constants,f.constants+f.constantBytes);
        if(f.listChanged){require(f.listCount<=capacity*4&&f.segmentCount<=65535);p.list.assign(f.list,f.list+f.listCount);p.segs.assign(f.segments,f.segments+size_t(f.segmentCount)*2);}
        write.store(w+1,std::memory_order_release);commitSum+=now()-start;commits++;return 1;
    }
    void apply(Packet& p) {
        frame=p.f;
        for(size_t i=0;i<p.poseIds.size();i++){if(poses.size()<=p.poseIds[i]){poses.resize(size_t(p.poseIds[i])+1);gpuPoses.resize(poses.size());}if(p.worldOnly)std::memcpy(poses[p.poseIds[i]].world,p.poses[i].world,64);else poses[p.poseIds[i]]=p.poses[i];std::memcpy(gpuPoses[p.poseIds[i]].world,p.poses[i].world,64);poseVersions[p.poseIds[i]]++;gpuPoseDirty=true;}
        for(size_t i=0;i<p.ids.size();i++){require(p.ids[i]&&p.ids[i]<=draws.size());auto& d=*draws[p.ids[i]-1];require(p.offsets[i+1]-p.offsets[i]==d.bytes.size());if(d.sprite&&(d.spritePatches||std::memcmp(d.bytes.data(),p.bytes.data()+p.offsets[i],16)))colorVersions[p.ids[i]-1]++;std::memcpy(d.bytes.data(),p.bytes.data()+p.offsets[i],d.bytes.size());stats.bytes+=d.bytes.size();captureSpriteProperties(d);auto& info=gpuDraws[p.ids[i]-1];for(int c=0;c<4;c++)info.color[c]=reinterpret_cast<const float*>(d.bytes.data())[c]*d.rendererTint[c];info.flip[0]=d.flip[0];info.flip[1]=d.flip[1];gpuDrawDirty=true;updateBatchKey(d);}
        for(size_t i=0;i<p.liveIds.size();i++){live[p.liveIds[i]]=p.liveValues[i];if(gpuPoses.size()<=p.liveIds[i])gpuPoses.resize(size_t(p.liveIds[i])+1);gpuPoses[p.liveIds[i]].live=p.liveValues[i];gpuPoseDirty=true;}batchPerf.liveEdits+=p.liveIds.size();
        if(p.f.listChanged){list=p.list;segs=p.segs;batchesDirty=true;geometryDirty=true;}
        if(batchesDirty){buildBatchRuns();batchesDirty=false;}
        stats.frames++;
    }
    void patch(DrawCall& d,bool batched=false) {
        Pose identity{};for(int i=0;i<16;i++)identity.world[i]=identity.inverse[i]=i%5==0?1.f:0.f;
        const auto& pose=batched?identity:poses[d.pose];const float ones[4]={1,1,1,1};
        for(const auto& p:d.patches){void* out=d.bytes.data()+p.offset;const float* src=nullptr;float temp[16]{};
            switch(p.kind){case 1:src=pose.world;break;case 2:src=pose.inverse;break;case 3:src=frame.vp;break;case 4:src=frame.view;break;case 5:src=frame.projection;break;
            case 6:multiply(temp,frame.vp,pose.world);src=temp;break;
            case 7:{float mv[16];multiply(mv,pose.inverse,frame.view);for(int c=0;c<4;c++)for(int r=0;r<4;r++)temp[c*4+r]=mv[r*4+c];src=temp;break;}
            case 8:src=frame.camera;break;case 9:src=frame.screen;break;case 10:src=frame.projectionParams;break;case 11:src=frame.ortho;break;case 12:src=frame.time;break;case 13:src=frame.sinTime;break;case 14:src=frame.cosTime;break;case 15:src=frame.delta;break;case 16:src=batched?ones:d.rendererTint;break;case 17:src=batched?ones:d.flip;break;default:throw E_INVALIDARG;}
            std::memcpy(out,src,p.kind<=7?64:16);
        }
    }
    bool compatible(const DrawCall& a,const DrawCall& b) {
        if(!b.batchable||a.shader!=b.shader||a.batchHash!=b.batchHash||a.batchKey!=b.batchKey||a.textures.size()!=b.textures.size()||a.buffers.size()!=b.buffers.size())return false;
        for(size_t i=0;i<a.buffers.size();i++)if(std::memcmp(&a.buffers[i].binding,&b.buffers[i].binding,sizeof(Buffer)))return false;
        for(size_t i=0;i<a.textures.size();i++){const auto& x=a.textures[i];const auto& y=b.textures[i];if(x.view!=y.view||x.sampler!=y.sampler||x.stage!=y.stage||x.slot!=y.slot||x.samplerSlot!=y.samplerSlot||x.grab||y.grab)return false;}
        return true;
    }
    void buildBatchRuns() {
        hasBatches=false;
        batchEnds.resize(list.size());batchItems.resize(list.size());
        for(size_t i=0;i<list.size();i++){
            require(list[i]&&list[i]<=draws.size());auto& d=*draws[list[i]-1];auto& item=batchItems[i];item={};item.pose=d.pose;item.id=list[i]-1;
            if(d.batchable){auto& g=*d.geometry;item.cpu=g.cpu.data();item.indices=g.cpuIndices.data();item.baked=d.baked.data();item.tint=reinterpret_cast<const float*>(d.bytes.data());item.rendererTint=d.rendererTint;item.flip=d.flip;item.count=d.baked.size();item.sprite=d.sprite;}
        }
        for(size_t i=0;i<list.size();){
            require(list[i]&&list[i]<=draws.size());auto& head=*draws[list[i]-1];size_t end=i+1;
            if(head.batchable)for(;end<list.size();end++){require(list[end]&&list[end]<=draws.size());if(!compatible(head,*draws[list[end]-1]))break;}
            if(end>i+1)hasBatches=true;for(size_t j=i;j<end;j++)batchEnds[j]=uint32_t(end);i=end;
        }
    }
    void appendBatch(size_t index) {
        auto& item=batchItems[index];
        if(item.poseVersion!=poseVersions[item.pose]||item.colorVersion!=colorVersions[item.id]) {
            const auto* m=poses[item.pose].world;
            for(size_t i=0;i<item.count;i++){BatchVertex v=item.cpu[item.indices[i]];
                float x=v.position[0]*item.flip[0],y=v.position[1]*item.flip[1],z=v.position[2];for(int row=0;row<3;row++)v.position[row]=((m[row]*x+m[4+row]*y)+m[8+row]*z)+m[12+row];
                if(item.sprite)for(int c=0;c<4;c++)v.color[c]=item.tint[c]*item.rendererTint[c];item.baked[i]=v;
            }
            item.poseVersion=poseVersions[item.pose];item.colorVersion=colorVersions[item.id];
        }
        std::memcpy(batchVertices.data()+batchVertexCount,item.baked,item.count*sizeof(BatchVertex));batchVertexCount+=item.count;
    }
    static UINT grow(UINT count) { UINT n=1;while(n<count){require(n<=UINT32_MAX/2);n*=2;}return n; }
    void structured(ComPtr<ID3D11Buffer>& buffer,ComPtr<ID3D11ShaderResourceView>& view,UINT& capacityIn,UINT count,UINT stride,bool dynamic) {
        if(buffer&&capacityIn>=count)return;
        capacityIn=grow(count);buffer.Reset();view.Reset();D3D11_BUFFER_DESC b{};b.ByteWidth=capacityIn*stride;b.Usage=dynamic?D3D11_USAGE_DYNAMIC:D3D11_USAGE_DEFAULT;
        b.BindFlags=D3D11_BIND_SHADER_RESOURCE;b.CPUAccessFlags=dynamic?D3D11_CPU_ACCESS_WRITE:0;b.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;b.StructureByteStride=stride;check(device->CreateBuffer(&b,nullptr,&buffer));
        D3D11_SHADER_RESOURCE_VIEW_DESC v{};v.Format=DXGI_FORMAT_UNKNOWN;v.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;v.Buffer.NumElements=capacityIn;check(device->CreateShaderResourceView(buffer.Get(),&v,&view));
    }
    void uploadStructured(ID3D11Buffer* buffer,const void* bytes,size_t count) {
        D3D11_MAPPED_SUBRESOURCE map{};check(deferred->Map(buffer,0,D3D11_MAP_WRITE_DISCARD,0,&map));std::memcpy(map.pData,bytes,count);deferred->Unmap(buffer,0);perf.uploads++;perf.uploadBytes+=count;
    }
    void prepareGpu() {
        if(!gpuBatch||!hasBatches)return;
        const double bt=profile?now():0;
        if(geometryDirty){
            localVertices.clear();vertexOffsets.resize(list.size()+1);
            for(size_t i=0;i<list.size();i++){
                vertexOffsets[i]=UINT(localVertices.size());const auto& item=batchItems[i];
                for(size_t k=0;k<item.count;k++)localVertices.push_back(LocalVertex{item.cpu[item.indices[k]],item.id});
            }
            vertexOffsets.back()=UINT(localVertices.size());require(!localVertices.empty()&&localVertices.size()<=16777216);
            UINT oldCapacity=localCapacity;structured(gpuLocal,localView,localCapacity,UINT(localVertices.size()),sizeof(LocalVertex),false);
            // SRV exposes only populated elements, so the final compute group is bounded.
            D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;sv.Format=DXGI_FORMAT_UNKNOWN;sv.Buffer.NumElements=UINT(localVertices.size());localView.Reset();check(device->CreateShaderResourceView(gpuLocal.Get(),&sv,&localView));
            D3D11_BOX box{0,0,0,UINT(localVertices.size()*sizeof(LocalVertex)),1,1};deferred->UpdateSubresource(gpuLocal.Get(),0,&box,localVertices.data(),0,0);perf.uploads++;perf.uploadBytes+=localVertices.size()*sizeof(LocalVertex);
            if(!gpuVertices||oldCapacity!=localCapacity){
                gpuVertices.Reset();verticesUav.Reset();D3D11_BUFFER_DESC b{};b.ByteWidth=localCapacity*sizeof(BatchVertex);b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_VERTEX_BUFFER|D3D11_BIND_UNORDERED_ACCESS;b.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;check(device->CreateBuffer(&b,nullptr,&gpuVertices));
                D3D11_UNORDERED_ACCESS_VIEW_DESC v{};v.Format=DXGI_FORMAT_R32_TYPELESS;v.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;v.Buffer.NumElements=b.ByteWidth/4;v.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;check(device->CreateUnorderedAccessView(gpuVertices.Get(),&v,&verticesUav));
            }
            geometryDirty=false;gpuVerticesDirty=true;batchPerf.gpuGeometryUploads++;
        }
        if(gpuPoseDirty){structured(gpuWorld,worldView,worldCapacity,UINT(gpuPoses.size()),sizeof(GpuPose),true);uploadStructured(gpuWorld.Get(),gpuPoses.data(),gpuPoses.size()*sizeof(GpuPose));gpuPoseDirty=false;gpuVerticesDirty=true;batchPerf.gpuPoseUploads++;}
        if(gpuDrawDirty){structured(gpuDraw,drawView,drawCapacity,UINT(gpuDraws.size()),sizeof(GpuDraw),true);uploadStructured(gpuDraw.Get(),gpuDraws.data(),gpuDraws.size()*sizeof(GpuDraw));gpuDrawDirty=false;gpuVerticesDirty=true;}
        if(gpuVerticesDirty){
            auto* dc=deferred.Get();ID3D11Buffer* nilBuffer=nullptr;UINT zero=0;dc->IASetVertexBuffers(0,1,&nilBuffer,&zero,&zero);
            dc->CSSetShader(bakeShader.Get(),nullptr,0);ID3D11ShaderResourceView* input[]={localView.Get(),worldView.Get(),drawView.Get()};dc->CSSetShaderResources(0,3,input);ID3D11UnorderedAccessView* uav=verticesUav.Get();dc->CSSetUnorderedAccessViews(0,1,&uav,nullptr);
            dc->Dispatch((UINT(localVertices.size())+63)/64,1,1);batchPerf.gpuDispatches++;ID3D11UnorderedAccessView* nilUav=nullptr;dc->CSSetUnorderedAccessViews(0,1,&nilUav,nullptr);ID3D11ShaderResourceView* nilViews[3]{};dc->CSSetShaderResources(0,3,nilViews);dc->CSSetShader(nullptr,nullptr,0);gpuVerticesDirty=false;
        }
        if(profile)batchPerf.buildUs+=now()-bt;
    }
    void render(unsigned segment) {
        const double start=now();
        try {
            if(segment==0){profile=stats.frames%64==0;if(profile)perf.sampledFrames++;const double at=profile?now():0;const uint64_t r=read.load(std::memory_order_relaxed);require(r!=write.load(std::memory_order_acquire));auto& p=ring[r%ring.size()];apply(p);read.store(r+1,std::memory_order_release);if(profile)perf.applyUs+=now()-at;}
            require(size_t(segment)*2+1<segs.size());
            UINT first=segs[segment*2],count=segs[segment*2+1];require(first+count<=list.size());
            ComPtr<ID3D11RenderTargetView> rtv;ComPtr<ID3D11DepthStencilView> dsv;context->OMGetRenderTargets(1,&rtv,&dsv);require(rtv!=nullptr);
            if(segment==0&&frame.clearStencil){require(dsv!=nullptr);D3D11_DEPTH_STENCIL_VIEW_DESC desc{};dsv->GetDesc(&desc);require(desc.Format==DXGI_FORMAT_D24_UNORM_S8_UINT||desc.Format==DXGI_FORMAT_D32_FLOAT_S8X24_UINT);context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_STENCIL,0,0);}
            UINT viewportCount=1;D3D11_VIEWPORT viewport{};context->RSGetViewports(&viewportCount,&viewport);require(viewportCount==1);
            auto* dc=deferred.Get();ID3D11RenderTargetView* rt=rtv.Get();dc->OMSetRenderTargets(1,&rt,dsv.Get());dc->RSSetViewports(1,&viewport);dc->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            dc->GSSetShader(nullptr,nullptr,0);dc->HSSetShader(nullptr,nullptr,0);dc->DSSetShader(nullptr,nullptr,0);
            prepareGpu();
            for(UINT i=first;i<first+count;i++){
                require(list[i]&&list[i]<=draws.size());auto& d=*draws[list[i]-1];if(!d.composite&&!live[d.pose])continue;auto& s=*d.shader;
                if(d.composite){
                    dc->VSSetShader(s.vs.Get(),nullptr,0);dc->PSSetShader(s.ps.Get(),nullptr,0);dc->IASetInputLayout(nullptr);
                    dc->OMSetBlendState(s.blendState.Get(),nullptr,~0u);dc->OMSetDepthStencilState(s.depth.Get(),0);dc->RSSetState(s.raster.Get());
                    ID3D11ShaderResourceView* views[]={d.black.Get(),d.white.Get()};dc->PSSetShaderResources(0,2,views);dc->Draw(3,0);
                    ID3D11ShaderResourceView* nil[2]{};dc->PSSetShaderResources(0,2,nil);stats.draws++;continue;
                }
                auto& g=*d.geometry;
                bool batched=false;
                UINT batchStart=0;
                if(gpuBatch&&d.batchable&&batchEnds[i]>i+1){
                    UINT end=std::min(first+count,batchEnds[i]);if(end>i+1){batched=true;batchStart=vertexOffsets[i];batchVertexCount=vertexOffsets[end]-batchStart;batchPerf.batches++;batchPerf.sources+=end-i;batchPerf.vertices+=batchVertexCount;i=end-1;}
                }
                else if(!gpuBatch&&d.batchable&&g.cpuIndices.size()<=batchLimit){
                    const double bt=profile?now():0;batchVertexCount=0;appendBatch(i);UINT end=i+1,used=1;
                    for(;end<std::min(first+count,batchEnds[i]);end++){
                        const auto& item=batchItems[end];if(!live[item.pose])continue;
                        if(batchVertexCount+item.count>batchLimit)break;
                        appendBatch(end);used++;
                    }
                    if(used>1){batched=true;i=end-1;batchPerf.batches++;batchPerf.sources+=used;batchPerf.vertices+=batchVertexCount;if(profile)batchPerf.buildUs+=now()-bt;}
                }
                const double cullStart=profile?now():0;
                if(!batched&&(d.extent[0]>0||d.extent[1]>0||d.extent[2]>0)){
                    float m[16];multiply(m,frame.vp,poses[d.pose].world);unsigned outside=63;
                    for(unsigned corner=0;corner<8&&outside;corner++){float p[3];for(unsigned axis=0;axis<3;axis++)p[axis]=d.center[axis]+((corner&(1u<<axis))?d.extent[axis]:-d.extent[axis]);float clip[4];for(unsigned row=0;row<4;row++)clip[row]=m[row]*p[0]+m[4+row]*p[1]+m[8+row]*p[2]+m[12+row];unsigned flags=(clip[0]<-clip[3]?1u:0u)|(clip[0]>clip[3]?2u:0u)|(clip[1]<-clip[3]?4u:0u)|(clip[1]>clip[3]?8u:0u)|(clip[2]<0?16u:0u)|(clip[2]>clip[3]?32u:0u);outside&=flags;}
                    if(outside){if(profile)perf.cullUs+=now()-cullStart;continue;}
                }
                if(profile)perf.cullUs+=now()-cullStart;const double constantsStart=profile?now():0;
                if(d.grab){
                    ComPtr<ID3D11Resource> resource;rtv->GetResource(&resource);ComPtr<ID3D11Texture2D> source;check(resource.As(&source));D3D11_TEXTURE2D_DESC td{};source->GetDesc(&td);require(td.SampleDesc.Count==1&&td.ArraySize==1);
                    D3D11_RENDER_TARGET_VIEW_DESC vd{};rtv->GetDesc(&vd);require(vd.ViewDimension==D3D11_RTV_DIMENSION_TEXTURE2D&&vd.Texture2D.MipSlice==0);
                    if(!grabTexture||td.Width!=grabDesc.Width||td.Height!=grabDesc.Height||td.Format!=grabDesc.Format||vd.Format!=grabFormat){grabDesc=td;grabFormat=vd.Format;grabTexture.Reset();grabView.Reset();td.BindFlags=D3D11_BIND_SHADER_RESOURCE;td.Usage=D3D11_USAGE_DEFAULT;td.CPUAccessFlags=0;td.MiscFlags=0;check(device->CreateTexture2D(&td,nullptr,&grabTexture));D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=vd.Format;sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=td.MipLevels;check(device->CreateShaderResourceView(grabTexture.Get(),&sv,&grabView));}
                    ID3D11ShaderResourceView* nil[16]{};dc->PSSetShaderResources(0,16,nil);dc->OMSetRenderTargets(0,nullptr,nullptr);dc->CopyResource(grabTexture.Get(),source.Get());dc->OMSetRenderTargets(1,&rt,dsv.Get());
                }
                patch(d,batched);dc->VSSetShader(s.vs.Get(),nullptr,0);dc->PSSetShader(s.ps.Get(),nullptr,0);dc->IASetInputLayout(batched?d.batchLayout.Get():d.layout.Get());
                ID3D11Buffer* vb[8]{};for(unsigned j=0;j<8;j++)vb[j]=g.buffers[j].Get();auto strides=g.strides;auto offsets=g.offsets;if(d.color&&!batched){if(!colorUploaded||std::memcmp(uploadedColor.data(),d.bytes.data(),16)){dc->UpdateSubresource(d.color.Get(),0,nullptr,d.bytes.data(),0,0);std::memcpy(uploadedColor.data(),d.bytes.data(),16);colorUploaded=true;perf.uploads++;perf.uploadBytes+=16;}vb[7]=d.color.Get();strides[7]=offsets[7]=0;}dc->IASetVertexBuffers(0,8,vb,strides.data(),offsets.data());dc->IASetIndexBuffer(g.indices.Get(),g.indexFormat,0);
                if(batched&&!gpuBatch){D3D11_MAPPED_SUBRESOURCE map{};check(dc->Map(batchBuffer.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&map));std::memcpy(map.pData,batchVertices.data(),batchVertexCount*sizeof(BatchVertex));dc->Unmap(batchBuffer.Get(),0);ID3D11Buffer* batch=batchBuffer.Get();UINT stride=sizeof(BatchVertex),offset=0;dc->IASetVertexBuffers(0,1,&batch,&stride,&offset);perf.uploads++;perf.uploadBytes+=batchVertexCount*sizeof(BatchVertex);}
                if(batched&&gpuBatch){ID3D11Buffer* batch=gpuVertices.Get();UINT stride=sizeof(BatchVertex),offset=0;dc->IASetVertexBuffers(0,1,&batch,&stride,&offset);}
                dc->OMSetBlendState(s.blendState.Get(),nullptr,~0u);dc->OMSetDepthStencilState(s.depth.Get(),s.stencilRef);dc->RSSetState(s.raster.Get());
                for(auto& binding:d.buffers){auto& c=*binding.storage;const auto& cbBinding=binding.binding;const auto* data=d.bytes.data()+cbBinding.offset;if(!c.ready||std::memcmp(c.uploaded.data(),data,cbBinding.bytes)){dc->UpdateSubresource(c.buffer.Get(),0,nullptr,data,0,0);std::memcpy(c.uploaded.data(),data,cbBinding.bytes);c.ready=true;perf.uploads++;perf.uploadBytes+=cbBinding.bytes;}ID3D11Buffer* cb=c.buffer.Get();if(cbBinding.stage==0)dc->VSSetConstantBuffers(cbBinding.slot,1,&cb);else dc->PSSetConstantBuffers(cbBinding.slot,1,&cb);}
                if(profile)perf.constantsUs+=now()-constantsStart;const double drawStart=profile?now():0;
                for(const auto& t:d.textures){ID3D11ShaderResourceView* v=t.grab?grabView.Get():t.view.Get();ID3D11SamplerState* sm=t.sampler.Get();if(t.stage==0){dc->VSSetShaderResources(UINT(t.slot),1,&v);if(t.samplerSlot>=0)dc->VSSetSamplers(UINT(t.samplerSlot),1,&sm);}else{dc->PSSetShaderResources(UINT(t.slot),1,&v);if(t.samplerSlot>=0)dc->PSSetSamplers(UINT(t.samplerSlot),1,&sm);}}
                if(batched)dc->Draw(UINT(batchVertexCount),batchStart);else{dc->DrawIndexed(g.indexCount,g.indexStart,g.baseVertex);batchPerf.singleDraws++;}stats.draws++;if(profile)perf.drawUs+=now()-drawStart;
            }
            const double executeStart=profile?now():0;ComPtr<ID3D11CommandList> command;check(dc->FinishCommandList(FALSE,&command));context->ExecuteCommandList(command.Get(),TRUE);if(profile)perf.executeUs+=now()-executeStart;
        }catch(HRESULT hr){stats.errors++;stats.lastHR=hr;deferred->ClearState();ComPtr<ID3D11CommandList> discard;deferred->FinishCommandList(FALSE,&discard);}
        catch(...){stats.errors++;stats.lastHR=E_FAIL;}
        stats.events++;renderSum+=now()-start;
        publishedDraws.store(stats.draws,std::memory_order_relaxed);publishedErrors.store(stats.errors,std::memory_order_relaxed);
        publishedBytes.store(stats.bytes,std::memory_order_relaxed);publishedEvents.store(stats.events,std::memory_order_relaxed);publishedHR.store(stats.lastHR,std::memory_order_relaxed);
        publishedRenderUs.store(stats.frames?renderSum/double(stats.frames):0,std::memory_order_relaxed);publishedFrames.store(stats.frames,std::memory_order_release);
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
        auto r=get(token);require(r!=nullptr);
        // Commit is a single-producer ring copy. It does not touch render-owned
        // resources or wait for deferred recording. Stats reads published atomics.
        if(op==drfull::CommitActiveWorld){const auto& a=*static_cast<const drfull::FrameActive*>(args);return r->commit(a.frame,true,&a,true);}
        if(op==drfull::CommitActive){const auto& a=*static_cast<const drfull::FrameActive*>(args);return r->commit(a.frame,true,&a);}
        if(op==drfull::Commit||op==drfull::CommitPacked)return r->commit(*static_cast<const drfull::Frame*>(args),op==drfull::CommitPacked);
        if(op==drfull::Stats){*const_cast<drfull::Statistics*>(static_cast<const drfull::Statistics*>(args))=r->snapshot();return 1;}
        std::lock_guard<std::mutex> lock(r->mutex);
        switch(op){case drfull::GeometryCpu:return r->cpuGeometry(*static_cast<const drfull::CpuGeometry*>(args));case drfull::BatchPerf:*const_cast<drfull::BatchPerformance*>(static_cast<const drfull::BatchPerformance*>(args))=r->batchPerf;return 1;case drfull::Perf:{auto p=r->perf;const double n=double(std::max(uint64_t(1),r->perf.sampledFrames));p.applyUs/=n;p.cullUs/=n;p.constantsUs/=n;p.drawUs/=n;p.executeUs/=n;*const_cast<drfull::Performance*>(static_cast<const drfull::Performance*>(args))=p;return 1;}case drfull::Composite:return r->composite(*static_cast<const drfull::CompositeIn*>(args));case drfull::Program:return r->program(*static_cast<const drfull::ProgramIn*>(args));case drfull::Geometry:return r->geometry(*static_cast<const drfull::GeometryIn*>(args));case drfull::Draw:return r->draw(*static_cast<const drfull::DrawIn*>(args));default:throw E_INVALIDARG;}
    }catch(HRESULT hr){lastHR=hr;return 0;}catch(...){lastHR=E_FAIL;return 0;}
}
