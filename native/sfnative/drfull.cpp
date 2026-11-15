// M4: original Unity DXBC, explicit camera lease; no Present hooks or Unity calls.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <d3d11_1.h>
#include <dxgi1_4.h>
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
#include <cfloat>
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
    bool owned=false;
    std::vector<BatchVertex> cpu;std::vector<uint32_t> cpuIndices;std::map<const Shader*,ComPtr<ID3D11InputLayout>> batchLayouts;
    std::array<ComPtr<ID3D11Buffer>,8> buffers;std::array<UINT,8> strides{},offsets{};
    ComPtr<ID3D11Buffer> indices;DXGI_FORMAT indexFormat;UINT indexStart,indexCount;INT baseVertex;
    std::vector<D3D11_INPUT_ELEMENT_DESC> attributes;
    std::map<std::pair<const Shader*,bool>,ComPtr<ID3D11InputLayout>> layouts;
};
struct Sample { ComPtr<ID3D11ShaderResourceView> view;ComPtr<ID3D11SamplerState> sampler;int stage,slot,samplerSlot;bool grab=false;uint32_t grabId=0; };
struct ConstantStorage {
    ComPtr<ID3D11Buffer> buffer;std::vector<uint8_t> uploaded;bool ready=false;
    bool dependenciesReady=false,inputReady=false,materialDirty=false;uint32_t dependencies=0;
    uint64_t poseEpoch=0,cameraEpoch=0,timeEpoch=0,colorEpoch=0;
    uint64_t evaluatedFrame=UINT64_MAX,ringSerial=0;UINT ringOffset=0;
};
struct Constants { Buffer binding;std::shared_ptr<ConstantStorage> storage,batchStorage;std::vector<uint8_t> material;bool privateMaterial=false; };
struct DrawCall {
    std::shared_ptr<Shader> shader;std::shared_ptr<Geometry> geometry;ComPtr<ID3D11InputLayout> layout,batchLayout;
    std::vector<Constants> buffers;std::vector<Patch> patches;std::vector<Sample> textures;std::vector<uint8_t> bytes;uint32_t pose;bool grab=false,batchable=false,sprite=false;uint32_t grabId=0;ComPtr<ID3D11Buffer> color;
    float center[3]{},extent[3]{},worldCenter[3]{},worldExtent[3]{};uint64_t boundsVersion=UINT64_MAX;float rendererTint[4]{1,1,1,1},flip[4]{1,1,1,1};bool spritePatches=false,batchRequested=false;
    std::array<uint8_t,16> uploadedColor{};bool colorUploaded=false;
    std::vector<uint8_t> batchKey,nextBatchKey;uint64_t batchHash=0;std::vector<BatchVertex> baked;
    bool composite=false;ComPtr<ID3D11ShaderResourceView> black,white;
};
struct Packet {
    Frame f{};bool worldOnly=false;std::vector<uint32_t> liveIds;std::vector<uint8_t> liveValues;std::vector<Pose> poses;std::vector<uint32_t> poseIds,ids,offsets,list,segs;std::vector<uint8_t> bytes;
};
void multiply(float* out,const float* a,const float* b) {
    for(int c=0;c<4;c++)for(int r=0;r<4;r++){float v=0;for(int k=0;k<4;k++)v+=a[k*4+r]*b[c*4+k];out[c*4+r]=v;}
}
#include "drfull-timeline.h"
#include "drfull-memory.h"
struct Renderer {
    int token=0;uint32_t capacity;
    ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context,deferred;
    ComPtr<ID3D11DeviceContext1> context1;ComPtr<ID3DDeviceContextState> isolatedState;
    ID3D11DeviceContext* recording=nullptr;
    Configuration configuration{0,1};SubmissionPerformance submission{};
    // A complete runtime state object includes bindings in stages we do not use,
    // UAVs, stream output, predication and D3D11.1 constant buffer ranges.
    struct StateScope {
        Renderer& owner;ComPtr<ID3DDeviceContextState> saved;
        explicit StateScope(Renderer& r):owner(r){}
        void begin(bool active){if(active){double t=now();owner.context1->SwapDeviceContextState(owner.isolatedState.Get(),&saved);owner.context->ClearState();owner.submission.stateUs+=now()-t;}}
        void restore(){if(saved){double t=now();owner.context1->SwapDeviceContextState(saved.Get(),nullptr);saved.Reset();owner.submission.stateRestores++;owner.submission.stateUs+=now()-t;}}
        ~StateScope(){restore();}
    };
    std::mutex mutex;std::vector<std::shared_ptr<Shader>> shaders;std::vector<std::shared_ptr<Geometry>> geometries;std::vector<std::shared_ptr<DrawCall>> draws;
    std::array<Packet,8> ring;std::atomic<uint64_t> read{0},write{0};std::vector<Pose> poses;
    std::vector<uint32_t> list,segs;Frame frame{};Statistics stats{};double renderSum=0,commitSum=0;uint64_t commits=0;
    float frustum[6][4]{};
    uint64_t cameraEpoch=0,timeEpoch=0;
    struct GrabSurface {ComPtr<ID3D11Texture2D> texture;ComPtr<ID3D11ShaderResourceView> view;D3D11_TEXTURE2D_DESC desc{};DXGI_FORMAT format=DXGI_FORMAT_UNKNOWN;uint64_t frame=0;};
    std::map<uint32_t,GrabSurface> grabSurfaces;
    GpuPerformance gpuPerf{};double gpuSum=0;
    GpuTimeline timeline;
    GpuMemory memory;
    bool memoryDirty=true;
    uint64_t memoryFrame=UINT64_MAX;
    std::vector<uint8_t> compositeUsed;
    void sampleMemory() {
        memory.begin();
        for(const auto& g:geometries)if(g->owned){memory.buffer(g->buffers[0].Get(),0);memory.buffer(g->indices.Get(),0);}
        memory.buffer(gpuLocal.Get(),0);memory.buffer(gpuWorld.Get(),1);memory.buffer(poseUpload.Get(),1);
        memory.buffer(gpuDraw.Get(),2);memory.buffer(drawUpload.Get(),2);memory.buffer(orderBuffer.Get(),3);memory.buffer(orderUpload.Get(),3);
        memory.buffer(gpuVertices.Get(),4);memory.buffer(batchBuffer.Get(),4);memory.buffer(blockUpload.Get(),4);memory.buffer(bakeControl.Get(),4);
        for(const auto& d:draws){memory.buffer(d->color.Get(),6);for(const auto& b:d->buffers){memory.buffer(b.storage->buffer.Get(),5);memory.buffer(b.batchStorage->buffer.Get(),5);}if(d->composite){memory.view(d->black.Get(),8);memory.view(d->white.Get(),8);}}
        for(const auto& entry:constantPool)memory.buffer(entry.second->buffer.Get(),5);
        memory.buffer(constantRing.Get(),5);
        memory.buffer(orderControl.Get(),5);
        for(const auto& pair:grabSurfaces)memory.texture(pair.second.texture.Get(),7);
        memory.finish();
        memoryDirty=false;
    }
    struct Timing {ComPtr<ID3D11Query> disjoint,start,end;bool pending=false;};std::array<Timing,16> timings;size_t timingNext=0;
    void pollGpu() {
        gpuPerf.pending=0;
        for(auto& t:timings)if(t.pending){
            D3D11_QUERY_DATA_TIMESTAMP_DISJOINT d{};UINT64 begin=0,end=0;
            if(context->GetData(t.disjoint.Get(),&d,sizeof(d),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK||context->GetData(t.start.Get(),&begin,sizeof(begin),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK||context->GetData(t.end.Get(),&end,sizeof(end),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK){gpuPerf.pending++;continue;}
            t.pending=false;if(d.Disjoint||d.Frequency==0||end<begin){gpuPerf.disjoint++;continue;}
            double us=double(end-begin)*1e6/double(d.Frequency);gpuSum+=us;gpuPerf.samples++;gpuPerf.eventUs=gpuSum/double(gpuPerf.samples);gpuPerf.maxEventUs=std::max(gpuPerf.maxEventUs,us);
        }
    }
    Timing* beginGpu() {pollGpu();auto& t=timings[timingNext];if(t.pending)return nullptr;timingNext=(timingNext+1)%timings.size();return &t;}
    void endGpu(Timing* t){if(t){context->End(t->disjoint.Get());t->pending=true;}}
    std::shared_ptr<Shader> compositor;
    Performance perf{};BatchPerformance batchPerf{};UploadPerformance uploadPerf{};bool profile=false;
    ComPtr<ID3D11Buffer> constantRing;UINT constantRingCapacity=0;
    std::vector<uint8_t> ringData,ringResident;uint64_t ringSerial=0;
    bool ringActive=false;
    std::vector<uint8_t> live;std::vector<uint64_t> poseVersions;std::vector<uint32_t> batchEnds;bool batchesDirty=true;
    struct RunBounds {float center[3]{},extent[3]{};uint64_t epoch=UINT64_MAX;UINT end=0;bool bounded=false,any=false;};
    std::vector<RunBounds> runBounds;uint64_t boundsEpoch=0;
    static constexpr UINT batchLimit=65532;
    ComPtr<ID3D11Buffer> batchBuffer;UINT cpuBatchCapacity=0;
    std::vector<BatchVertex> batchVertices;size_t batchVertexCount=0;
    struct BatchItem { const BatchVertex* cpu=nullptr;const uint32_t* indices=nullptr;BatchVertex* baked=nullptr;const float* tint=nullptr;const float* rendererTint=nullptr;const float* flip=nullptr;uint32_t pose=0,id=0;size_t count=0;uint64_t poseVersion=UINT64_MAX,colorVersion=UINT64_MAX;bool sprite=false; };
    bool gpuBatch=false,geometryDirty=true,indexDirty=true,gpuVerticesDirty=true,hasBatches=false;
    ComPtr<ID3D11ComputeShader> bakeShader;
    ComPtr<ID3D11Buffer> bakeControl;UINT uploadedBakeCount=UINT32_MAX;
    ComPtr<ID3D11Buffer> gpuLocal,gpuWorld,gpuDraw,gpuVertices;
    ComPtr<ID3D11ShaderResourceView> localView,worldView,drawView;ComPtr<ID3D11UnorderedAccessView> verticesUav;
    UINT localCapacity=0,worldCapacity=0,drawCapacity=0;
    std::vector<LocalVertex> localVertices;std::vector<uint32_t> vertexOffsets,orderOffsets;
    struct OrderRange {UINT source,destination,count;};
    std::vector<OrderRange> orderRanges;
    ComPtr<ID3D11ComputeShader> orderShader;
    ComPtr<ID3D11Buffer> orderControl;UINT orderCount=UINT32_MAX;
    ComPtr<ID3D11Buffer> orderUpload;ComPtr<ID3D11ShaderResourceView> orderView;
    ComPtr<ID3D11UnorderedAccessView> orderUav;UINT orderUploadCapacity=0;
    ComPtr<ID3D11Buffer> orderBuffer;UINT orderCapacity=0;size_t geometryDrawCount=0;
    std::vector<std::vector<uint32_t>> poseDraws;
    std::vector<uint32_t> bakeBlocks;std::vector<uint8_t> blockMarked;
    ComPtr<ID3D11Buffer> blockUpload;ComPtr<ID3D11ShaderResourceView> blockView;UINT blockCapacity=0;
    std::vector<uint32_t> dirtyPoses,dirtyDraws;std::vector<uint8_t> poseMarked,drawMarked;
    std::vector<GpuPose> gpuPoses;std::vector<GpuDraw> gpuDraws;
    ComPtr<ID3D11Buffer> poseUpload,drawUpload;UINT poseUploadCapacity=0,drawUploadCapacity=0;
    ComPtr<ID3D11ShaderResourceView> poseUploadView,drawUploadView;
    bool poseDefaultCurrent=false,drawDefaultCurrent=false,poseDirect=false,drawDirect=false;
    std::vector<BatchItem> batchItems;std::vector<uint64_t> colorVersions;
    // Frame-only constants share storage; object/material constants own persistent
    // storage. A batch has a separate normalized storage, never ping-pongs with singles.
    std::map<std::tuple<const Shader*,uint32_t,uint32_t,std::vector<uint32_t>>,std::shared_ptr<ConstantStorage>> constantPool;
    void markPose(uint32_t id) { if(!poseMarked[id]){poseMarked[id]=1;dirtyPoses.push_back(id);} }
    void markDraw(uint32_t id) { if(!drawMarked[id]){drawMarked[id]=1;dirtyDraws.push_back(id);} }
    std::shared_ptr<ConstantStorage> newConstants(UINT bytes) {
        auto storage=std::make_shared<ConstantStorage>();storage->uploaded.resize(bytes);return storage;
    }
    void legacyConstants(ConstantStorage& c,UINT bytes) {
        if(c.buffer)return;
        D3D11_BUFFER_DESC b{};b.ByteWidth=bytes;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        check(device->CreateBuffer(&b,nullptr,&c.buffer));memoryDirty=true;batchPerf.constantCreates++;
    }
    std::shared_ptr<ConstantStorage> constantStorage(const DrawCall& d,const Buffer& binding,bool batched) {
        std::vector<uint32_t> key(binding.bytes/4+2);key[0]=batched?1u:0u;
        std::memcpy(key.data()+2,d.bytes.data()+binding.offset,binding.bytes);
        bool object=false;
        for(const auto& patch:d.patches)if(patch.offset>=binding.offset&&patch.offset<binding.offset+binding.bytes){
            const uint32_t n=patch.kind<=7?64u:16u;require(patch.offset+n<=binding.offset+binding.bytes);
            std::memset(reinterpret_cast<uint8_t*>(key.data()+2)+patch.offset-binding.offset,0,n);
            key.push_back(patch.offset-binding.offset);key.push_back(patch.kind);
            object|=(!batched&&(patch.kind==1||patch.kind==2||patch.kind==6||patch.kind==7||patch.kind==16||patch.kind==17));
        }
        // Share identical material bytes initially. A material edit detaches the
        // affected binding once (copy-on-write), so animated MPBs cannot ping-pong.
        key[1]=object?d.pose+1:0u;
        auto poolKey=std::make_tuple(d.shader.get(),binding.stage,binding.slot,std::move(key));
        auto& storage=constantPool[poolKey];
        if(!storage)storage=newConstants(binding.bytes);
        return storage;
    }
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
        ComPtr<ID3D11Device1> device1;
        if(SUCCEEDED(device.As(&device1))&&SUCCEEDED(context.As(&context1))){
            D3D_FEATURE_LEVEL level=device->GetFeatureLevel(),selected{};
            if(SUCCEEDED(device1->CreateDeviceContextState(0,&level,1,D3D11_SDK_VERSION,__uuidof(ID3D11Device),&selected,&isolatedState)))submission.immediateAvailable=1;
            D3D11_FEATURE_DATA_D3D11_OPTIONS options{};
            if(SUCCEEDED(device->CheckFeatureSupport(D3D11_FEATURE_D3D11_OPTIONS,&options,sizeof(options)))&&options.ConstantBufferOffsetting)submission.ringAvailable=1;
        }
        recording=deferred.Get();
        memory.init(device.Get());
        for(auto& t:timings){D3D11_QUERY_DESC q{D3D11_QUERY_TIMESTAMP_DISJOINT,0};check(device->CreateQuery(&q,&t.disjoint));q.Query=D3D11_QUERY_TIMESTAMP;check(device->CreateQuery(&q,&t.start));check(device->CreateQuery(&q,&t.end));}
        ringData.reserve(size_t(capacity)*512);ringResident.reserve(size_t(capacity)*512);
        poseDraws.resize(capacity);poseMarked.resize(capacity);drawMarked.resize(size_t(capacity)*8);dirtyPoses.reserve(capacity);dirtyDraws.reserve(size_t(capacity)*8);
        orderRanges.reserve(size_t(capacity)*4);orderOffsets.reserve(size_t(capacity)*4+1);
        live.resize(capacity,1);poseVersions.resize(capacity);runBounds.reserve(size_t(capacity)*4);batchEnds.reserve(size_t(capacity)*4);batchVertices.resize(batchLimit);batchItems.reserve(size_t(capacity)*4);colorVersions.reserve(size_t(capacity)*8);poses.reserve(capacity);gpuPoses.reserve(capacity);gpuDraws.reserve(size_t(capacity)*8);vertexOffsets.reserve(size_t(capacity)*4+1);localVertices.reserve(size_t(capacity)*6);
        // CPU fallback VB is allocated only when used, sized to the actual run.
        if(device->GetFeatureLevel()>=D3D_FEATURE_LEVEL_11_0){
            static const ComPtr<ID3DBlob> code=[](){
                static const char source[]=R"(
                    struct V { float3 position;float4 color;float2 uv;float2 uv1;uint draw; };
                    struct P { float4x4 world;uint live;uint3 pad; };
                    struct D { float4 color;uint pose;uint sprite;float2 flip; };
                    StructuredBuffer<V> source:register(t0);StructuredBuffer<P> poses:register(t1);StructuredBuffer<D> draws:register(t2);StructuredBuffer<uint> blocks:register(t3);
                    RWByteAddressBuffer output:register(u0);cbuffer Control:register(b0){uint workCount;uint3 reserved;};
                    [numthreads(64,1,1)] void main(uint3 group:SV_GroupID,uint3 thread:SV_GroupThreadID){
                        uint work=group.x+group.y*65535;if(work>=workCount)return;uint index=blocks[work]*64+thread.x;
                        uint count,stride;source.GetDimensions(count,stride);if(index>=count)return;
                        V v=source[index];D d=draws[v.draw];P p=poses[d.pose];
                        float3 local=float3(v.position.xy*d.flip,v.position.z);
                        precise float3 position=p.world._m01_m11_m21*local.y;
                        position=mad(p.world._m00_m10_m20,local.x,position);
                        position=mad(p.world._m02_m12_m22,local.z,position);
                        position=position+p.world._m03_m13_m23;
                        if(p.live==0)position=0;
                        uint at=index*44;output.Store3(at,asuint(position));output.Store4(at+12,asuint(d.sprite!=0?d.color:v.color));
                        output.Store2(at+28,asuint(v.uv));output.Store2(at+36,asuint(v.uv1));
                    }
                )";
                ComPtr<ID3DBlob> blob,error;check(D3DCompile(source,sizeof(source)-1,nullptr,nullptr,nullptr,"main","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3|D3DCOMPILE_IEEE_STRICTNESS,0,&blob,&error));return blob;
            }();
            check(device->CreateComputeShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&bakeShader));
            D3D11_BUFFER_DESC control{};control.ByteWidth=16;control.BindFlags=D3D11_BIND_CONSTANT_BUFFER;control.Usage=D3D11_USAGE_DEFAULT;memoryDirty=true;check(device->CreateBuffer(&control,nullptr,&bakeControl));gpuBatch=true;batchPerf.gpuAvailable=1;
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
        memoryDirty=true;
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
        view(in.black,d->black);view(in.white,d->white);
        for(size_t i=0;i<draws.size();i++)if(draws[i]->composite&&!draws[i]->black&&!draws[i]->white){draws[i]=d;return int(i+1);}
        draws.push_back(d);colorVersions.push_back(0);gpuDraws.emplace_back();markDraw(UINT(draws.size()-1));geometryDirty=true;return int(draws.size());
    }
    int geometry(const GeometryIn& in) {
        memoryDirty=true;
        auto g=std::make_shared<Geometry>();g->owned=in.vertices!=nullptr;require(in.attributeCount&&in.attributeCount<=32&&in.attributes);
        for(unsigned i=0;i<8;i++){g->buffers[i]=static_cast<ID3D11Buffer*>(in.buffers[i]);g->strides[i]=in.strides[i];g->offsets[i]=in.offsets[i];}
        g->indices=static_cast<ID3D11Buffer*>(in.indices);g->indexFormat=DXGI_FORMAT(in.indexFormat);g->indexStart=in.indexStart;g->indexCount=in.indexCount;g->baseVertex=in.baseVertex;
        if(in.vertices){require(in.vertexBytes&&in.cpuIndices&&in.cpuIndexCount);D3D11_BUFFER_DESC b{};b.ByteWidth=in.vertexBytes;b.Usage=D3D11_USAGE_IMMUTABLE;b.BindFlags=D3D11_BIND_VERTEX_BUFFER;D3D11_SUBRESOURCE_DATA data{};data.pSysMem=in.vertices;memoryDirty=true;check(device->CreateBuffer(&b,&data,&g->buffers[0]));b.ByteWidth=in.cpuIndexCount*4;b.BindFlags=D3D11_BIND_INDEX_BUFFER;data.pSysMem=in.cpuIndices;memoryDirty=true;check(device->CreateBuffer(&b,&data,&g->indices));g->indexFormat=DXGI_FORMAT_R32_UINT;g->indexStart=0;g->indexCount=in.cpuIndexCount;g->baseVertex=0;}
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
        memoryDirty=true;
        require(in.program&&in.program<=shaders.size()&&in.geometry&&in.geometry<=geometries.size()&&in.pose<capacity);
        require(in.constantBytes<=16384&&in.constants&&in.bufferCount<=28&&in.textureCount<=32&&draws.size()<capacity*8);
        auto d=std::make_shared<DrawCall>();d->shader=shaders[in.program-1];d->geometry=geometries[in.geometry-1];d->pose=in.pose;d->grab=in.grab!=0;d->grabId=in.grab;if(in.grab)grabSurfaces.try_emplace(in.grab);d->sprite=(in.sprite&1)!=0;d->batchable=(in.sprite&2)!=0&&!d->grab&&!d->geometry->cpu.empty();
        std::memcpy(d->center,in.center,12);std::memcpy(d->extent,in.extent,12);for(int i=0;i<3;i++)require(std::isfinite(d->center[i])&&std::isfinite(d->extent[i])&&d->extent[i]>=0);
        auto& g=*d->geometry;auto attributes=g.attributes;
        d->batchRequested=(in.sprite&2)!=0;
        if(d->sprite){for(auto& a:attributes)if(std::strcmp(a.SemanticName,"COLOR")==0){a.InputSlot=7;a.AlignedByteOffset=0;a.Format=DXGI_FORMAT_R32G32B32A32_FLOAT;}D3D11_BUFFER_DESC b{};b.ByteWidth=16;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_VERTEX_BUFFER;memoryDirty=true;check(device->CreateBuffer(&b,nullptr,&d->color));}
        auto layoutKey=std::make_pair(d->shader.get(),d->sprite);auto cached=g.layouts.find(layoutKey);
        if(cached!=g.layouts.end()){d->layout=cached->second;perf.layoutHits++;}
        else {check(device->CreateInputLayout(attributes.data(),UINT(attributes.size()),d->shader->signature.data(),d->shader->signature.size(),&d->layout));g.layouts.emplace(layoutKey,d->layout);perf.layoutCreates++;}
        if(poses.size()<=in.pose){poses.resize(size_t(in.pose)+1);gpuPoses.resize(poses.size());markPose(in.pose);}
        const auto* bytes=static_cast<const uint8_t*>(in.constants);d->bytes.assign(bytes,bytes+in.constantBytes);
        if(in.patchCount)d->patches.assign(in.patches,in.patches+in.patchCount);
        captureSpriteProperties(*d);
        for(const auto& p:d->patches)require(p.kind>=1&&p.kind<=17&&p.offset+(p.kind<=7?64:16)<=in.constantBytes);
        for(unsigned i=0;i<in.bufferCount;i++){
            Constants c{};c.binding=in.buffers[i];require(c.binding.slot<14&&c.binding.stage<2&&c.binding.bytes>0&&c.binding.bytes%16==0&&c.binding.offset+c.binding.bytes<=in.constantBytes);
            c.storage=constantStorage(*d,c.binding,false);c.batchStorage=d->batchable?constantStorage(*d,c.binding,true):c.storage;
            c.material.assign(d->bytes.begin()+c.binding.offset,d->bytes.begin()+c.binding.offset+c.binding.bytes);
            for(const auto& patch:d->patches)if(patch.offset>=c.binding.offset&&patch.offset<c.binding.offset+c.binding.bytes)std::memset(c.material.data()+patch.offset-c.binding.offset,0,patch.kind<=7?64:16);
            d->buffers.push_back(std::move(c));
        }
        for(unsigned i=0;i<in.textureCount;i++){const auto& t=in.textures[i];require((t.resource||t.srgb<0)&&t.stage<2&&t.slot>=0&&t.slot<128&&t.sampler<16);Sample s{};s.stage=t.stage;s.slot=t.slot;s.samplerSlot=t.sampler;s.grab=t.srgb<0;s.grabId=s.grab?uint32_t(-t.srgb):0;if(s.grab)grabSurfaces.try_emplace(s.grabId);
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
            d->batchLayout=layout;d->baked.resize(d->geometry->cpuIndices.size());
        }
        d->batchKey.resize(d->bytes.size());d->nextBatchKey.resize(d->bytes.size());updateBatchKey(*d);
        GpuDraw info{};info.pose=d->pose;info.sprite=d->sprite?1u:0u;for(int c=0;c<4;c++)info.color[c]=reinterpret_cast<const float*>(d->bytes.data())[c]*d->rendererTint[c];info.flip[0]=d->flip[0];info.flip[1]=d->flip[1];
        poseDraws[d->pose].push_back(UINT(draws.size()));draws.push_back(d);colorVersions.push_back(0);gpuDraws.push_back(info);markDraw(UINT(draws.size()-1));geometryDirty=true;return int(draws.size());
    }
    void captureSpriteProperties(DrawCall& d) {
        for(const auto& p:d.patches){
            if(p.kind==16){require(p.offset+16<=d.bytes.size());std::memcpy(d.rendererTint,d.bytes.data()+p.offset,16);d.spritePatches=true;}
            if(p.kind==17){require(p.offset+8<=d.bytes.size());std::memcpy(d.flip,d.bytes.data()+p.offset,8);d.spritePatches=true;}
        }
    }
    bool updateBatchKey(DrawCall& d) {
        std::memcpy(d.nextBatchKey.data(),d.bytes.data(),d.bytes.size());
        if(d.sprite)std::memset(d.nextBatchKey.data(),0,16);
        for(const auto& p:d.patches)std::memset(d.nextBatchKey.data()+p.offset,0,p.kind<=7?64:16);
        bool changed=d.batchKey!=d.nextBatchKey;if(changed&&d.batchable)batchesDirty=true;d.batchKey.swap(d.nextBatchKey);
        d.batchHash=1469598103934665603ull;for(auto x:d.batchKey){d.batchHash^=x;d.batchHash*=1099511628211ull;}
        return changed;
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
        if(std::memcmp(&frame,&p.f,offsetof(Frame,time)))cameraEpoch++;
        if(std::memcmp(frame.time,p.f.time,64))timeEpoch++;
        frame=p.f;
        for(int plane=0;plane<6;plane++)for(int i=0;i<4;i++)frustum[plane][i]=plane==4?frame.vp[i*4+2]:frame.vp[i*4+3]+(plane%2==0?1.f:-1.f)*frame.vp[i*4+plane/2];
        for(size_t i=0;i<p.poseIds.size();i++){
            uint32_t id=p.poseIds[i];if(poses.size()<=id){poses.resize(size_t(id)+1);gpuPoses.resize(poses.size());}
            bool inverseChanged=!p.worldOnly&&std::memcmp(poses[id].inverse,p.poses[i].inverse,64)!=0;
            if(p.worldOnly)std::memcpy(poses[id].world,p.poses[i].world,64);else poses[id]=p.poses[i];
            if(inverseChanged)poseVersions[id]++;
            if(std::memcmp(gpuPoses[id].world,p.poses[i].world,64)){std::memcpy(gpuPoses[id].world,p.poses[i].world,64);markPose(id);poseVersions[id]++;boundsEpoch++;}
        }
        for(size_t i=0;i<p.ids.size();i++){
            require(p.ids[i]&&p.ids[i]<=draws.size());uint32_t id=p.ids[i]-1;auto& d=*draws[id];require(p.offsets[i+1]-p.offsets[i]==d.bytes.size());
            if(d.sprite&&(d.spritePatches||std::memcmp(d.bytes.data(),p.bytes.data()+p.offsets[i],16)))colorVersions[id]++;
            std::memcpy(d.bytes.data(),p.bytes.data()+p.offsets[i],d.bytes.size());stats.bytes+=d.bytes.size();captureSpriteProperties(d);
            auto info=gpuDraws[id];for(int c=0;c<4;c++)info.color[c]=reinterpret_cast<const float*>(d.bytes.data())[c]*d.rendererTint[c];info.flip[0]=d.flip[0];info.flip[1]=d.flip[1];
            if(std::memcmp(&info,&gpuDraws[id],sizeof(info))){gpuDraws[id]=info;markDraw(id);}
            if(updateBatchKey(d))for(auto& binding:d.buffers){
                const auto* material=d.batchKey.data()+binding.binding.offset;
                if(std::memcmp(material,binding.material.data(),binding.binding.bytes)){
                    if(!binding.privateMaterial){binding.storage=newConstants(binding.binding.bytes);binding.batchStorage=d.batchable?newConstants(binding.binding.bytes):binding.storage;binding.privateMaterial=true;}
                    std::memcpy(binding.material.data(),material,binding.binding.bytes);
                    binding.storage->materialDirty=true;binding.batchStorage->materialDirty=true;
                }
            }
        }
        for(size_t i=0;i<p.liveIds.size();i++){
            uint32_t id=p.liveIds[i];live[id]=p.liveValues[i];if(gpuPoses.size()<=id)gpuPoses.resize(size_t(id)+1);
            if(gpuPoses[id].live!=p.liveValues[i]){gpuPoses[id].live=p.liveValues[i];markPose(id);boundsEpoch++;}
        }batchPerf.liveEdits+=p.liveIds.size();
        if(p.f.listChanged){
            // Retirement happens on the render thread after the replacement
            // packet is consumed, never while an earlier segment uses its SRVs.
            compositeUsed.assign(draws.size(),0);for(auto id:p.list){require(id&&id<=draws.size());compositeUsed[id-1]=1;}
            for(auto id:list)if(draws[id-1]->composite&&!compositeUsed[id-1]){draws[id-1]->black.Reset();draws[id-1]->white.Reset();}
            bool reordered=list!=p.list;list=p.list;segs=p.segs;batchesDirty|=reordered;indexDirty|=reordered;
        }
        if(batchesDirty){buildBatchRuns();batchesDirty=false;}
        stats.frames++;
    }
    void patch(DrawCall& d,const Buffer& binding,bool batched=false) {
        Pose identity{};for(int i=0;i<16;i++)identity.world[i]=identity.inverse[i]=i%5==0?1.f:0.f;
        const auto& pose=batched?identity:poses[d.pose];const float ones[4]={1,1,1,1};
        for(const auto& p:d.patches){if(p.offset<binding.offset||p.offset>=binding.offset+binding.bytes)continue;void* out=d.bytes.data()+p.offset;const float* src=nullptr;float temp[16]{};
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
    void countSingle(UINT index,UINT first,UINT end,const DrawCall& d) {
        if(!d.batchRequested){uploadPerf.singleUnverified++;return;}
        if(!d.batchable){uploadPerf.singleGeometry++;return;}
        if(!gpuBatch&&d.baked.size()>batchLimit){uploadPerf.singleLimit++;return;}
        if((index+1==end&&batchEnds[index]>end)||(index==first&&index>0&&batchEnds[index-1]>index)){uploadPerf.singleSegment++;return;}
        const DrawCall* other=index+1<end?draws[list[index+1]-1].get():index>first?draws[list[index-1]-1].get():nullptr;
        if(!other){uploadPerf.singleSegment++;return;}
        if(!other->batchable||d.shader!=other->shader){uploadPerf.singleProgram++;return;}
        if(d.batchHash!=other->batchHash||d.batchKey!=other->batchKey||d.buffers.size()!=other->buffers.size()){uploadPerf.singleConstants++;return;}
        for(size_t i=0;i<d.buffers.size();i++)if(std::memcmp(&d.buffers[i].binding,&other->buffers[i].binding,sizeof(Buffer))){uploadPerf.singleConstants++;return;}
        uploadPerf.singleTextures++;
    }
    bool outsideWorld(const float* center,const float* extent) const {
        for(int plane=0;plane<6;plane++){
            const float* p=frustum[plane];
            float distance=p[3],radius=0;for(int i=0;i<3;i++){distance+=p[i]*center[i];radius+=std::abs(p[i])*extent[i];}
            if(distance+radius<0)return true;
        }return false;
    }
    void worldBounds(DrawCall& d) {
        if(d.boundsVersion==poseVersions[d.pose])return;
        const auto* m=poses[d.pose].world;
        for(int row=0;row<3;row++){
            d.worldCenter[row]=m[row]*d.center[0]+m[4+row]*d.center[1]+m[8+row]*d.center[2]+m[12+row];
            d.worldExtent[row]=std::abs(m[row])*d.extent[0]+std::abs(m[4+row])*d.extent[1]+std::abs(m[8+row])*d.extent[2];
        }d.boundsVersion=poseVersions[d.pose];
    }
    bool outsideBatch(UINT first,UINT end) {
        auto& cached=runBounds[first];
        if(cached.epoch==boundsEpoch&&cached.end==end)return !cached.any||(cached.bounded&&outsideWorld(cached.center,cached.extent));
        cached.epoch=boundsEpoch;cached.end=end;cached.bounded=true;cached.any=false;
        float low[3]{FLT_MAX,FLT_MAX,FLT_MAX},high[3]{-FLT_MAX,-FLT_MAX,-FLT_MAX};bool any=false;
        for(UINT i=first;i<end;i++){
            auto& d=*draws[list[i]-1];if(!live[d.pose])continue;
            if(d.extent[0]==0&&d.extent[1]==0&&d.extent[2]==0){cached.any=true;cached.bounded=false;return false;} // no trustworthy bounds
            worldBounds(d);
            any=true;for(int row=0;row<3;row++){low[row]=std::min(low[row],d.worldCenter[row]-d.worldExtent[row]);high[row]=std::max(high[row],d.worldCenter[row]+d.worldExtent[row]);}
        }
        cached.any=any;if(!any)return true;float* center=cached.center;float* extent=cached.extent;
        // Expand by floating-point error at world scale. A near-edge run remains
        // visible; culling must not move a thin line across a pixel boundary.
        for(int i=0;i<3;i++){center[i]=(low[i]+high[i])*.5f;extent[i]=(high[i]-low[i])*.5f+std::max(1.f,std::max(std::abs(low[i]),std::abs(high[i])))*1e-5f;}
        return outsideWorld(center,extent);
    }
    void buildBatchRuns() {
        hasBatches=false;
        boundsEpoch++;runBounds.resize(list.size());batchEnds.resize(list.size());batchItems.resize(list.size());
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
    // GPU storage tracks demand in 12.5% steps, rounded to 64 elements, instead
    // of keeping up to almost 100% spare VRAM in every duplicate stream.
    static UINT grow(UINT count) { uint64_t n=std::max(uint64_t(64),((uint64_t(count)+count/8+63)/64)*64);require(n<=UINT32_MAX);return UINT(n); }
    void structured(ComPtr<ID3D11Buffer>& buffer,ComPtr<ID3D11ShaderResourceView>& view,UINT& capacityIn,UINT count,UINT stride,bool dynamic) {
        if(buffer&&capacityIn>=count)return;
        capacityIn=grow(count);buffer.Reset();view.Reset();D3D11_BUFFER_DESC b{};b.ByteWidth=capacityIn*stride;b.Usage=dynamic?D3D11_USAGE_DYNAMIC:D3D11_USAGE_DEFAULT;
        memoryDirty=true;
        b.BindFlags=D3D11_BIND_SHADER_RESOURCE;b.CPUAccessFlags=dynamic?D3D11_CPU_ACCESS_WRITE:0;b.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;b.StructureByteStride=stride;memoryDirty=true;check(device->CreateBuffer(&b,nullptr,&buffer));
        D3D11_SHADER_RESOURCE_VIEW_DESC v{};v.Format=DXGI_FORMAT_UNKNOWN;v.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;v.Buffer.NumElements=capacityIn;check(device->CreateShaderResourceView(buffer.Get(),&v,&view));
    }
    template<class T> bool uploadRanges(ComPtr<ID3D11Buffer>& buffer,ComPtr<ID3D11ShaderResourceView>& view,UINT& allocated,
                      ComPtr<ID3D11Buffer>& staging,ComPtr<ID3D11ShaderResourceView>& stagingView,UINT& stagingCapacity,bool& defaultCurrent,bool& direct,
                      const std::vector<T>& values,std::vector<uint32_t>& dirty,std::vector<uint8_t>& marked,uint64_t& bytes,uint64_t& ranges) {
        if(dirty.empty()&&buffer&&allocated>=values.size())return false;
        bool fresh=!buffer||allocated<values.size();structured(buffer,view,allocated,UINT(values.size()),sizeof(T),false);
        UINT count=UINT(fresh?values.size():dirty.size());
        bool dense=fresh||dirty.size()==values.size();
        // Dense frames bind the renamed upload buffer directly. On the first sparse
        // frame, synchronize the persistent cache on the GPU BEFORE renaming that
        // upload buffer; host traffic still contains only changed slots.
        if(!dense&&!defaultCurrent){recording->CopyResource(buffer.Get(),staging.Get());defaultCurrent=true;}
        if(!staging||stagingCapacity<count){stagingCapacity=grow(count);staging.Reset();stagingView.Reset();D3D11_BUFFER_DESC b{};b.ByteWidth=stagingCapacity*sizeof(T);b.Usage=D3D11_USAGE_DYNAMIC;b.BindFlags=D3D11_BIND_SHADER_RESOURCE;b.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;b.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;b.StructureByteStride=sizeof(T);memoryDirty=true;check(device->CreateBuffer(&b,nullptr,&staging));D3D11_SHADER_RESOURCE_VIEW_DESC v{};v.Format=DXGI_FORMAT_UNKNOWN;v.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;v.Buffer.NumElements=stagingCapacity;check(device->CreateShaderResourceView(staging.Get(),&v,&stagingView));}
        std::sort(dirty.begin(),dirty.end());D3D11_MAPPED_SUBRESOURCE mapped{};check(recording->Map(staging.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped));
        if(dense)std::memcpy(mapped.pData,values.data(),size_t(count)*sizeof(T));
        else for(size_t i=0;i<dirty.size();i++)std::memcpy(static_cast<uint8_t*>(mapped.pData)+i*sizeof(T),&values[dirty[i]],sizeof(T));
        recording->Unmap(staging.Get(),0);uint64_t n=uint64_t(count)*sizeof(T);bytes+=n;perf.uploads++;perf.uploadBytes+=n;
        // One renamed upload allocation, then GPU copies only the changed ranges.
        // No boxed UpdateSubresource on deferred contexts (legacy driver offset bug).
        auto copy=[&](UINT first,UINT end,UINT source){D3D11_BOX box{source*UINT(sizeof(T)),0,0,(source+end-first)*UINT(sizeof(T)),1,1};recording->CopySubresourceRegion(buffer.Get(),0,first*UINT(sizeof(T)),0,0,staging.Get(),0,&box);ranges++;};
        if(dense){ranges++;defaultCurrent=false;direct=true;}
        else {direct=false;for(size_t i=0;i<dirty.size();){UINT source=UINT(i),first=dirty[i],end=first+1;i++;while(i<dirty.size()&&dirty[i]==end){end++;i++;}copy(first,end,source);}}
        for(auto id:dirty)marked[id]=0;dirty.clear();return true;
    }
    void markBake(uint32_t id) {
        if(!draws[id]->batchable)return;
        UINT first=vertexOffsets[id],end=vertexOffsets[id+1];
        if(first==end)return;
        for(UINT block=first/64;block<=(end-1)/64;block++)if(!blockMarked[block]){blockMarked[block]=1;bakeBlocks.push_back(block);}
    }
    void prepareGpu() {
        if(!gpuBatch||!hasBatches)return;
        const double bt=profile?now():0;
        if(geometryDirty){
            size_t oldSize=localVertices.size();
            // Stable native draw slots. A new draw appends; reordering never touches
            // these vertices or changes the world-VB address of an existing draw.
            vertexOffsets.resize(draws.size()+1);
            for(size_t i=geometryDrawCount;i<draws.size();i++){
                vertexOffsets[i]=UINT(localVertices.size());const auto& d=*draws[i];
                if(d.batchable)for(auto index:d.geometry->cpuIndices)localVertices.push_back(LocalVertex{d.geometry->cpu[index],UINT(i)});
            }
            geometryDrawCount=draws.size();vertexOffsets.back()=UINT(localVertices.size());require(!localVertices.empty()&&localVertices.size()<=16777216);
            bool fresh=!gpuLocal||localCapacity<localVertices.size();structured(gpuLocal,localView,localCapacity,UINT(localVertices.size()),sizeof(LocalVertex),false);
            D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;sv.Format=DXGI_FORMAT_UNKNOWN;sv.Buffer.NumElements=UINT(localVertices.size());localView.Reset();check(device->CreateShaderResourceView(gpuLocal.Get(),&sv,&localView));
            UINT first=UINT(fresh?0:oldSize);if(first<localVertices.size()){
                D3D11_BOX box{first*UINT(sizeof(LocalVertex)),0,0,UINT(localVertices.size()*sizeof(LocalVertex)),1,1};context->UpdateSubresource(gpuLocal.Get(),0,&box,localVertices.data()+first,0,0);
                uint64_t n=(localVertices.size()-first)*sizeof(LocalVertex);perf.uploads++;perf.uploadBytes+=n;uploadPerf.geometryBytes+=n;batchPerf.gpuGeometryUploads++;
            }
            if(!gpuVertices||fresh){
                gpuVertices.Reset();verticesUav.Reset();D3D11_BUFFER_DESC b{};b.ByteWidth=localCapacity*sizeof(BatchVertex);b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_VERTEX_BUFFER|D3D11_BIND_UNORDERED_ACCESS;b.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;memoryDirty=true;check(device->CreateBuffer(&b,nullptr,&gpuVertices));
                D3D11_UNORDERED_ACCESS_VIEW_DESC v{};v.Format=DXGI_FORMAT_R32_TYPELESS;v.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;v.Buffer.NumElements=b.ByteWidth/4;v.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;check(device->CreateUnorderedAccessView(gpuVertices.Get(),&v,&verticesUav));
            }
            geometryDirty=false;gpuVerticesDirty=true;indexDirty=true;
            blockMarked.resize((localVertices.size()+63)/64);bakeBlocks.reserve(blockMarked.size());
            // Geometry growth may replace the output resource. Rebake once; a pure
            // reorder never enters this branch and never dispatches compute.
            for(UINT block=0;block<blockMarked.size();block++)if(!blockMarked[block]){blockMarked[block]=1;bakeBlocks.push_back(block);}
        }
        if(indexDirty){
            // Upload one 12-byte range per source. The index stream is generated
            // on the GPU, never mapped/copied from the host, including reorders.
            orderRanges.clear();orderOffsets.resize(list.size()+1);UINT total=0;
            for(size_t i=0;i<list.size();i++){
                orderOffsets[i]=total;UINT id=list[i]-1,n=vertexOffsets[id+1]-vertexOffsets[id];
                if(n)orderRanges.push_back({vertexOffsets[id],total,n});total+=n;
            }
            orderOffsets.back()=total;
            if(!orderBuffer||orderCapacity<total){
                orderCapacity=grow(total);orderBuffer.Reset();orderUav.Reset();D3D11_BUFFER_DESC b{};
                b.ByteWidth=orderCapacity*4;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_INDEX_BUFFER|D3D11_BIND_UNORDERED_ACCESS;b.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
                memoryDirty=true;check(device->CreateBuffer(&b,nullptr,&orderBuffer));
                D3D11_UNORDERED_ACCESS_VIEW_DESC v{};v.Format=DXGI_FORMAT_R32_TYPELESS;v.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;v.Buffer.NumElements=orderCapacity;v.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;
                check(device->CreateUnorderedAccessView(orderBuffer.Get(),&v,&orderUav));
            }
            if(!orderRanges.empty()){
                if(!orderShader){
                    const char source[]=R"(struct R {uint source,destination,count;};StructuredBuffer<R> ranges:register(t0);RWByteAddressBuffer output:register(u0);cbuffer Control:register(b0){uint count;uint3 pad;};
                        [numthreads(64,1,1)] void main(uint3 thread:SV_DispatchThreadID){uint id=thread.x+thread.y*65535*64;if(id>=count)return;R r=ranges[id];for(uint k=0;k<r.count;k++)output.Store((r.destination+k)*4,r.source+k);})";
                    ComPtr<ID3DBlob> code,error;check(D3DCompile(source,sizeof(source)-1,nullptr,nullptr,nullptr,"main","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&error));
                    check(device->CreateComputeShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&orderShader));
                    D3D11_BUFFER_DESC b{};b.ByteWidth=16;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&b,nullptr,&orderControl));
                }
                structured(orderUpload,orderView,orderUploadCapacity,UINT(orderRanges.size()),sizeof(OrderRange),true);
                D3D11_MAPPED_SUBRESOURCE mapped{};check(recording->Map(orderUpload.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped));
                UINT bytes=UINT(orderRanges.size()*sizeof(OrderRange));std::memcpy(mapped.pData,orderRanges.data(),bytes);recording->Unmap(orderUpload.Get(),0);
                perf.uploads++;perf.uploadBytes+=bytes;uploadPerf.workBytes+=bytes;submission.orderBytes+=bytes;
                recording->IASetIndexBuffer(nullptr,DXGI_FORMAT_R32_UINT,0);
                recording->CSSetShader(orderShader.Get(),nullptr,0);ID3D11ShaderResourceView* input=orderView.Get();recording->CSSetShaderResources(0,1,&input);
                ID3D11UnorderedAccessView* output=orderUav.Get();recording->CSSetUnorderedAccessViews(0,1,&output,nullptr);
                UINT n=UINT(orderRanges.size());if(orderCount!=n){UINT control[4]{n,0,0,0};recording->UpdateSubresource(orderControl.Get(),0,nullptr,control,0,0);orderCount=n;perf.uploads++;perf.uploadBytes+=16;uploadPerf.workBytes+=16;}
                ID3D11Buffer* control=orderControl.Get();recording->CSSetConstantBuffers(0,1,&control);
                UINT groups=(n+63)/64;recording->Dispatch(std::min(groups,65535u),(groups+65534)/65535,1);submission.orderDispatches++;
                input=nullptr;output=nullptr;recording->CSSetShaderResources(0,1,&input);recording->CSSetUnorderedAccessViews(0,1,&output,nullptr);recording->CSSetShader(nullptr,nullptr,0);
            }
            indexDirty=false;
        }
        // Expand dirty source IDs into stable 64-vertex blocks BEFORE uploadRanges
        // consumes those queues. Adjacent slots share a group; unchanged geometry
        // outside the touched blocks stays in the persistent world vertex buffer.
        for(auto id:dirtyPoses)for(auto draw:poseDraws[id])markBake(draw);
        for(auto id:dirtyDraws)markBake(id);
        if(uploadRanges(gpuWorld,worldView,worldCapacity,poseUpload,poseUploadView,poseUploadCapacity,poseDefaultCurrent,poseDirect,gpuPoses,dirtyPoses,poseMarked,uploadPerf.poseBytes,uploadPerf.poseRanges)){gpuVerticesDirty=true;batchPerf.gpuPoseUploads++;}
        if(uploadRanges(gpuDraw,drawView,drawCapacity,drawUpload,drawUploadView,drawUploadCapacity,drawDefaultCurrent,drawDirect,gpuDraws,dirtyDraws,drawMarked,uploadPerf.drawBytes,uploadPerf.drawRanges))gpuVerticesDirty=true;
        if(gpuVerticesDirty&&!bakeBlocks.empty()){
            auto* dc=recording;ID3D11Buffer* nilBuffer=nullptr;UINT zero=0;dc->IASetVertexBuffers(0,1,&nilBuffer,&zero,&zero);
            structured(blockUpload,blockView,blockCapacity,UINT(bakeBlocks.size()),sizeof(uint32_t),true);
            D3D11_MAPPED_SUBRESOURCE blocksMap{};check(dc->Map(blockUpload.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&blocksMap));std::memcpy(blocksMap.pData,bakeBlocks.data(),bakeBlocks.size()*4);dc->Unmap(blockUpload.Get(),0);
            perf.uploads++;perf.uploadBytes+=bakeBlocks.size()*4;uploadPerf.workBytes+=bakeBlocks.size()*4;uploadPerf.bakedVertices+=bakeBlocks.size()*64;
            dc->CSSetShader(bakeShader.Get(),nullptr,0);ID3D11ShaderResourceView* input[]={localView.Get(),poseDirect?poseUploadView.Get():worldView.Get(),drawDirect?drawUploadView.Get():drawView.Get(),blockView.Get()};dc->CSSetShaderResources(0,4,input);ID3D11UnorderedAccessView* uav=verticesUav.Get();dc->CSSetUnorderedAccessViews(0,1,&uav,nullptr);
            UINT work=UINT(bakeBlocks.size());if(work!=uploadedBakeCount){UINT control[4]{work,0,0,0};dc->UpdateSubresource(bakeControl.Get(),0,nullptr,control,0,0);uploadedBakeCount=work;perf.uploads++;perf.uploadBytes+=16;uploadPerf.workBytes+=16;}
            ID3D11Buffer* control=bakeControl.Get();dc->CSSetConstantBuffers(0,1,&control);
            dc->Dispatch(std::min(work,65535u),(work+65534)/65535,1);batchPerf.gpuDispatches++;ID3D11UnorderedAccessView* nilUav=nullptr;dc->CSSetUnorderedAccessViews(0,1,&nilUav,nullptr);ID3D11ShaderResourceView* nilViews[4]{};dc->CSSetShaderResources(0,4,nilViews);dc->CSSetShader(nullptr,nullptr,0);gpuVerticesDirty=false;for(auto block:bakeBlocks)blockMarked[block]=0;bakeBlocks.clear();
        }
        if(profile)batchPerf.buildUs+=now()-bt;
    }
    bool constantsChanged(ConstantStorage& c,const DrawCall& d,const Buffer& binding,bool batched,uint32_t id) {
        if(!c.dependenciesReady){
            for(const auto& p:d.patches)if(p.offset>=binding.offset&&p.offset<binding.offset+binding.bytes){
                if(!batched&&(p.kind==1||p.kind==2||p.kind==6||p.kind==7))c.dependencies|=1;
                if(p.kind>=3&&p.kind<=11)c.dependencies|=2;
                if(p.kind>=12&&p.kind<=15)c.dependencies|=4;
                if(!batched&&(p.kind==16||p.kind==17))c.dependencies|=8;
            }c.dependenciesReady=true;
        }
        bool changed=!c.inputReady||c.materialDirty||((c.dependencies&1)&&c.poseEpoch!=poseVersions[d.pose])
            ||((c.dependencies&2)&&c.cameraEpoch!=cameraEpoch)||((c.dependencies&4)&&c.timeEpoch!=timeEpoch)
            ||((c.dependencies&8)&&c.colorEpoch!=colorVersions[id]);
        return changed;
    }
    void rememberConstants(ConstantStorage& c,const DrawCall& d,uint32_t id) {
        c.inputReady=true;c.materialDirty=false;c.poseEpoch=poseVersions[d.pose];c.cameraEpoch=cameraEpoch;c.timeEpoch=timeEpoch;c.colorEpoch=colorVersions[id];
    }
    void prepareConstants(UINT first,UINT count) {
        ringData.clear();ringSerial++;
        for(UINT i=first;i<first+count;i++){
            UINT id=list[i]-1;auto& d=*draws[id];if(d.composite||!live[d.pose])continue;
            bool batched=gpuBatch&&d.batchable&&std::min(first+count,batchEnds[i])>i+1;
            if(batched)i=std::min(first+count,batchEnds[i])-1;
            for(auto& binding:d.buffers){
                auto& c=*(batched?binding.batchStorage:binding.storage);const auto& b=binding.binding;
                if(c.ringSerial==ringSerial)continue;
                if(c.evaluatedFrame!=stats.frames){
                    if(constantsChanged(c,d,b,batched,id)){
                        patch(d,b,batched);std::memcpy(c.uploaded.data(),d.bytes.data()+b.offset,b.bytes);rememberConstants(c,d,id);timeline.perf.constantEvaluations++;
                    }else timeline.perf.constantReuses++;
                    c.evaluatedFrame=stats.frames;
                }else timeline.perf.constantReuses++;
                c.ringOffset=UINT(ringData.size());c.ringSerial=ringSerial;
                size_t aligned=(size_t(b.bytes)+255)&~size_t(255);ringData.resize(ringData.size()+aligned);
                std::memcpy(ringData.data()+c.ringOffset,c.uploaded.data(),b.bytes);
            }
        }
        if(ringData.empty()||ringData==ringResident)return;
        require(ringData.size()<=UINT32_MAX-255);
        if(!constantRing||constantRingCapacity<ringData.size()){
            constantRingCapacity=UINT((ringData.size()+255)&~size_t(255));constantRing.Reset();D3D11_BUFFER_DESC b{};
            b.ByteWidth=constantRingCapacity;b.Usage=D3D11_USAGE_DYNAMIC;b.BindFlags=D3D11_BIND_CONSTANT_BUFFER;b.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;
            check(device->CreateBuffer(&b,nullptr,&constantRing));memoryDirty=true;
        }
        D3D11_MAPPED_SUBRESOURCE mapped{};check(recording->Map(constantRing.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped));
        std::memcpy(mapped.pData,ringData.data(),ringData.size());recording->Unmap(constantRing.Get(),0);
        perf.uploads++;perf.uploadBytes+=ringData.size();uploadPerf.constantBytes+=ringData.size();uploadPerf.constantUploads++;
        submission.ringMaps++;submission.ringBytes+=ringData.size();
        ringResident=ringData;
    }
    void render(unsigned segment) {
        const double start=now();Timing* timing=nullptr;
        const bool immediate=configuration.submission==0&&submission.immediateAvailable;
        // Snapshot only after consuming/validating a nonempty segment.
        ComPtr<ID3D11RenderTargetView> rtv;ComPtr<ID3D11DepthStencilView> dsv;
        UINT viewportCount=1;D3D11_VIEWPORT viewport{};
        StateScope state(*this);recording=immediate?context.Get():deferred.Get();
        bool timelineOpen=false;
        try {
            if(segment==0){profile=stats.frames%64==0;if(profile)perf.sampledFrames++;const double at=profile?now():0;const uint64_t r=read.load(std::memory_order_relaxed);require(r!=write.load(std::memory_order_acquire));auto& p=ring[r%ring.size()];apply(p);read.store(r+1,std::memory_order_release);if(profile)perf.applyUs+=now()-at;}
            require(size_t(segment)*2+1<segs.size());
            UINT first=segs[segment*2],count=segs[segment*2+1];require(first+count<=list.size());
            // An empty boundary still consumes segment zero's packet, but does
            // not snapshot/restore the pipeline or execute an empty command list.
            if(count==0&&!frame.clearStencil){timeline.perf.emptySegments++;state.restore();finishRender(start);return;}
            context->OMGetRenderTargets(1,&rtv,&dsv);context->RSGetViewports(&viewportCount,&viewport);
            require(rtv!=nullptr);
            if(segment==0&&frame.clearStencil){require(dsv!=nullptr);D3D11_DEPTH_STENCIL_VIEW_DESC desc{};dsv->GetDesc(&desc);require(desc.Format==DXGI_FORMAT_D24_UNORM_S8_UINT||desc.Format==DXGI_FORMAT_D32_FLOAT_S8X24_UINT);context->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_STENCIL,0,0);}
            require(viewportCount==1);
            state.begin(immediate);
            auto* dc=recording;ID3D11RenderTargetView* rt=rtv.Get();dc->OMSetRenderTargets(1,&rt,dsv.Get());dc->RSSetViewports(1,&viewport);dc->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            dc->GSSetShader(nullptr,nullptr,0);dc->HSSetShader(nullptr,nullptr,0);dc->DSSetShader(nullptr,nullptr,0);
            timing=profile?beginGpu():nullptr;
            const bool compositeOnly=count==1&&draws[list[first]-1]->composite;
            if(immediate){if(timing)context->Begin(timing->disjoint.Get());timeline.mark(compositeOnly?6u:4u);timelineOpen=true;submission.immediateSegments++;}
            else submission.deferredSegments++;
            const double recordStart=now();
            if(timing)dc->End(timing->start.Get());prepareGpu();
            // Use offset bindings only on immediate contexts. Old deferred drivers
            // can drop offset-only changes; the reference path keeps legacy CBs.
            ringActive=immediate&&configuration.constantRing&&submission.ringAvailable;
            if(ringActive){double at=profile?now():0;prepareConstants(first,count);if(profile)perf.constantsUs+=now()-at;}
            std::array<ID3D11Buffer*,14> boundVs{},boundPs{};
            std::array<ConstantStorage*,14> boundVsStorage{},boundPsStorage{};Shader* boundShader=nullptr;
            Geometry* boundGeometry=nullptr;ID3D11InputLayout* boundLayout=nullptr;bool boundBatch=false;
            ID3D11Buffer* boundColor=nullptr;
            std::array<ID3D11ShaderResourceView*,128> boundVsViews{},boundPsViews{};
            std::array<ID3D11SamplerState*,16> boundVsSamplers{},boundPsSamplers{};
            for(UINT i=first;i<first+count;i++){
                require(list[i]&&list[i]<=draws.size());auto& d=*draws[list[i]-1];if(!d.composite&&!live[d.pose])continue;auto& s=*d.shader;
                if(d.composite){
                    dc->VSSetShader(s.vs.Get(),nullptr,0);dc->PSSetShader(s.ps.Get(),nullptr,0);dc->IASetInputLayout(nullptr);
                    dc->OMSetBlendState(s.blendState.Get(),nullptr,~0u);dc->OMSetDepthStencilState(s.depth.Get(),0);dc->RSSetState(s.raster.Get());
                    ID3D11ShaderResourceView* views[]={d.black.Get(),d.white.Get()};dc->PSSetShaderResources(0,2,views);dc->Draw(3,0);
                    ID3D11ShaderResourceView* nil[2]{};dc->PSSetShaderResources(0,2,nil);boundPsViews[0]=boundPsViews[1]=nullptr;boundLayout=nullptr;boundShader=nullptr;stats.draws++;continue;
                }
                auto& g=*d.geometry;
                bool batched=false;UINT batchFirst=i;
                UINT batchStart=0;
                if(gpuBatch&&d.batchable&&batchEnds[i]>i+1){
                    UINT end=std::min(first+count,batchEnds[i]);if(end>i+1){batched=true;batchStart=orderOffsets[i];batchVertexCount=orderOffsets[end]-batchStart;batchPerf.batches++;batchPerf.sources+=end-i;batchPerf.vertices+=batchVertexCount;i=end-1;}
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
                if(batched&&outsideBatch(batchFirst,i+1)){batchPerf.batches--;batchPerf.sources-=i+1-batchFirst;batchPerf.vertices-=batchVertexCount;gpuPerf.culledDraws++;gpuPerf.culledSources+=i+1-batchFirst;if(profile)perf.cullUs+=now()-cullStart;continue;}
                if(!batched&&(d.extent[0]>0||d.extent[1]>0||d.extent[2]>0)){
                    worldBounds(d);float extent[3];
                    for(int axis=0;axis<3;axis++)extent[axis]=d.worldExtent[axis]+std::max(1.f,std::abs(d.worldCenter[axis])+d.worldExtent[axis])*1e-5f;
                    if(outsideWorld(d.worldCenter,extent)){gpuPerf.culledDraws++;gpuPerf.culledSources++;if(d.grab)timeline.perf.culledGrabs++;if(profile)perf.cullUs+=now()-cullStart;continue;}
                }
                if(profile)perf.cullUs+=now()-cullStart;const double constantsStart=profile?now():0;
                if(d.grab){
                    timeline.perf.visibleGrabs++;
                    auto& grab=grabSurfaces.at(d.grabId);
                    if(d.grabId==1||grab.frame!=stats.frames){
                        ComPtr<ID3D11Resource> resource;rtv->GetResource(&resource);ComPtr<ID3D11Texture2D> source;check(resource.As(&source));D3D11_TEXTURE2D_DESC td{};source->GetDesc(&td);require(td.SampleDesc.Count==1&&td.ArraySize==1);
                        D3D11_RENDER_TARGET_VIEW_DESC vd{};rtv->GetDesc(&vd);require(vd.ViewDimension==D3D11_RTV_DIMENSION_TEXTURE2D&&vd.Texture2D.MipSlice==0);
                        if(!grab.texture||td.Width!=grab.desc.Width||td.Height!=grab.desc.Height||td.Format!=grab.desc.Format||vd.Format!=grab.format){grab.desc=td;grab.format=vd.Format;grab.texture.Reset();grab.view.Reset();td.BindFlags=D3D11_BIND_SHADER_RESOURCE;td.Usage=D3D11_USAGE_DEFAULT;td.CPUAccessFlags=0;td.MiscFlags=0;memoryDirty=true;check(device->CreateTexture2D(&td,nullptr,&grab.texture));D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=vd.Format;sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=td.MipLevels;check(device->CreateShaderResourceView(grab.texture.Get(),&sv,&grab.view));}
                        ID3D11ShaderResourceView* nil[16]{};dc->VSSetShaderResources(0,16,nil);dc->PSSetShaderResources(0,16,nil);std::fill_n(boundVsViews.begin(),16,nullptr);std::fill_n(boundPsViews.begin(),16,nullptr);dc->OMSetRenderTargets(0,nullptr,nullptr);dc->CopyResource(grab.texture.Get(),source.Get());dc->OMSetRenderTargets(1,&rt,dsv.Get());
                        grab.frame=stats.frames;gpuPerf.grabCopies++;
                        // camRT formats accepted here are 32/64/128-bit color formats.
                        UINT pixelBytes=(td.Format==DXGI_FORMAT_R32G32B32A32_FLOAT||td.Format==DXGI_FORMAT_R32G32B32A32_TYPELESS)?16u:(td.Format==DXGI_FORMAT_R16G16B16A16_FLOAT||td.Format==DXGI_FORMAT_R16G16B16A16_TYPELESS||td.Format==DXGI_FORMAT_R16G16B16A16_UNORM)?8u:4u;
                        gpuPerf.grabBytes+=uint64_t(td.Width)*td.Height*pixelBytes;
                    }else gpuPerf.namedGrabHits++;
                }
                if(boundShader!=&s){dc->VSSetShader(s.vs.Get(),nullptr,0);dc->PSSetShader(s.ps.Get(),nullptr,0);dc->OMSetBlendState(s.blendState.Get(),nullptr,~0u);dc->OMSetDepthStencilState(s.depth.Get(),s.stencilRef);dc->RSSetState(s.raster.Get());boundShader=&s;}
                auto* layout=batched?d.batchLayout.Get():d.layout.Get();if(boundLayout!=layout){dc->IASetInputLayout(layout);boundLayout=layout;}
                if(!batched){
                    if(boundGeometry!=&g||boundBatch){
                        ID3D11Buffer* vb[8]{};for(unsigned j=0;j<8;j++)vb[j]=g.buffers[j].Get();
                        dc->IASetVertexBuffers(0,8,vb,g.strides.data(),g.offsets.data());dc->IASetIndexBuffer(g.indices.Get(),g.indexFormat,0);boundColor=g.buffers[7].Get();
                    }
                    if(d.color){
                        if(!d.colorUploaded||std::memcmp(d.uploadedColor.data(),d.bytes.data(),16)){dc->UpdateSubresource(d.color.Get(),0,nullptr,d.bytes.data(),0,0);std::memcpy(d.uploadedColor.data(),d.bytes.data(),16);d.colorUploaded=true;perf.uploads++;perf.uploadBytes+=16;uploadPerf.colorBytes+=16;}
                        if(boundColor!=d.color.Get()){ID3D11Buffer* color=d.color.Get();UINT zero=0;dc->IASetVertexBuffers(7,1,&color,&zero,&zero);boundColor=color;}
                    }else if(boundColor!=g.buffers[7].Get()){ID3D11Buffer* color=g.buffers[7].Get();dc->IASetVertexBuffers(7,1,&color,&g.strides[7],&g.offsets[7]);boundColor=color;}
                }
                boundGeometry=&g;bool wasBatch=boundBatch;boundBatch=batched;
                if(batched&&!gpuBatch){if(cpuBatchCapacity<batchVertexCount){cpuBatchCapacity=UINT(batchVertexCount);batchBuffer.Reset();D3D11_BUFFER_DESC b{};b.ByteWidth=cpuBatchCapacity*sizeof(BatchVertex);b.Usage=D3D11_USAGE_DYNAMIC;b.BindFlags=D3D11_BIND_VERTEX_BUFFER;b.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;memoryDirty=true;check(device->CreateBuffer(&b,nullptr,&batchBuffer));}D3D11_MAPPED_SUBRESOURCE map{};check(dc->Map(batchBuffer.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&map));std::memcpy(map.pData,batchVertices.data(),batchVertexCount*sizeof(BatchVertex));dc->Unmap(batchBuffer.Get(),0);ID3D11Buffer* batch=batchBuffer.Get();UINT stride=sizeof(BatchVertex),offset=0;dc->IASetVertexBuffers(0,1,&batch,&stride,&offset);perf.uploads++;perf.uploadBytes+=batchVertexCount*sizeof(BatchVertex);uploadPerf.cpuBatchBytes+=batchVertexCount*sizeof(BatchVertex);}
                if(batched&&gpuBatch&&!wasBatch){ID3D11Buffer* batch=gpuVertices.Get();UINT stride=sizeof(BatchVertex),offset=0;dc->IASetVertexBuffers(0,1,&batch,&stride,&offset);dc->IASetIndexBuffer(orderBuffer.Get(),DXGI_FORMAT_R32_UINT,0);}
                for(auto& binding:d.buffers){
                    auto& c=*(batched?binding.batchStorage:binding.storage);const auto& cbBinding=binding.binding;
                    if(ringActive){
                        require(c.ringSerial==ringSerial);
                        auto& previous=cbBinding.stage==0?boundVsStorage[cbBinding.slot]:boundPsStorage[cbBinding.slot];
                        if(previous==&c){timeline.perf.constantBindSkips++;continue;}previous=&c;
                        ID3D11Buffer* cb=constantRing.Get();UINT offset=c.ringOffset/16,length=((cbBinding.bytes+255)&~255u)/16;
                        if(cbBinding.stage==0)context1->VSSetConstantBuffers1(cbBinding.slot,1,&cb,&offset,&length);
                        else context1->PSSetConstantBuffers1(cbBinding.slot,1,&cb,&offset,&length);
                        submission.ringBindings++;continue;
                    }
                    legacyConstants(c,cbBinding.bytes);
                    if(c.evaluatedFrame!=stats.frames){
                        if(constantsChanged(c,d,cbBinding,batched,list[batchFirst]-1)){
                            patch(d,cbBinding,batched);const auto* data=d.bytes.data()+cbBinding.offset;
                            if(!c.ready||std::memcmp(c.uploaded.data(),data,cbBinding.bytes)){dc->UpdateSubresource(c.buffer.Get(),0,nullptr,data,0,0);std::memcpy(c.uploaded.data(),data,cbBinding.bytes);c.ready=true;perf.uploads++;perf.uploadBytes+=cbBinding.bytes;uploadPerf.constantBytes+=cbBinding.bytes;uploadPerf.constantUploads++;}
                            rememberConstants(c,d,list[batchFirst]-1);timeline.perf.constantEvaluations++;
                        }else timeline.perf.constantReuses++;
                        c.evaluatedFrame=stats.frames;
                    }else timeline.perf.constantReuses++;
                    ID3D11Buffer* cb=c.buffer.Get();auto& previous=cbBinding.stage==0?boundVs[cbBinding.slot]:boundPs[cbBinding.slot];
                    if(previous==cb){timeline.perf.constantBindSkips++;continue;}previous=cb;
                    if(cbBinding.stage==0)dc->VSSetConstantBuffers(cbBinding.slot,1,&cb);else dc->PSSetConstantBuffers(cbBinding.slot,1,&cb);
                }
                if(profile)perf.constantsUs+=now()-constantsStart;const double drawStart=profile?now():0;
                for(const auto& t:d.textures){
                    ID3D11ShaderResourceView* v=t.grab?grabSurfaces.at(t.grabId).view.Get():t.view.Get();ID3D11SamplerState* sm=t.sampler.Get();
                    auto& previous=t.stage==0?boundVsViews[t.slot]:boundPsViews[t.slot];
                    if(previous!=v){previous=v;if(t.stage==0)dc->VSSetShaderResources(UINT(t.slot),1,&v);else dc->PSSetShaderResources(UINT(t.slot),1,&v);}
                    if(t.samplerSlot>=0){auto& prior=t.stage==0?boundVsSamplers[t.samplerSlot]:boundPsSamplers[t.samplerSlot];if(prior!=sm){prior=sm;if(t.stage==0)dc->VSSetSamplers(UINT(t.samplerSlot),1,&sm);else dc->PSSetSamplers(UINT(t.samplerSlot),1,&sm);}}
                }
                if(batched){if(gpuBatch)dc->DrawIndexed(UINT(batchVertexCount),batchStart,0);else dc->Draw(UINT(batchVertexCount),0);}else{dc->DrawIndexed(g.indexCount,g.indexStart,g.baseVertex);batchPerf.singleDraws++;countSingle(i,first,first+count,d);}stats.draws++;if(profile)perf.drawUs+=now()-drawStart;
            }
            if(timing)dc->End(timing->end.Get());
            submission.recordUs+=now()-recordStart;
            const double executeStart=profile?now():0;
            if(!immediate){
                ComPtr<ID3D11CommandList> command;check(dc->FinishCommandList(FALSE,&command));
                if(timing)context->Begin(timing->disjoint.Get());
                timeline.mark(compositeOnly?6u:4u);timelineOpen=true;context->ExecuteCommandList(command.Get(),TRUE);
            }
            timeline.mark(7);timelineOpen=false;
            endGpu(timing);timing=nullptr;if(profile)perf.executeUs+=now()-executeStart;
        }catch(HRESULT hr){stats.errors++;stats.lastHR=hr;if(!immediate){deferred->ClearState();ComPtr<ID3D11CommandList> discard;deferred->FinishCommandList(FALSE,&discard);}}
        catch(...){stats.errors++;stats.lastHR=E_FAIL;}
        if(timelineOpen)timeline.mark(7);
        if(timing)endGpu(timing);
        state.restore();
        finishRender(start);
    }
    void finishRender(double start) {
        if(stats.frames%256==1&&memoryFrame!=stats.frames){
            memoryFrame=stats.frames;
            // Idle snapshots may be recreated at their next real use. Never
            // alias distinct named snapshots still needed in the current frame.
            for(auto& pair:grabSurfaces)if(pair.second.texture&&pair.second.frame+256<stats.frames){pair.second.view.Reset();pair.second.texture.Reset();memoryDirty=true;}
            for(auto it=constantPool.begin();it!=constantPool.end();)if(it->second.use_count()==1){it=constantPool.erase(it);memoryDirty=true;}else ++it;
            if(memoryDirty)sampleMemory();else memory.sample();
        }
        stats.events++;renderSum+=now()-start;
        publishedDraws.store(stats.draws,std::memory_order_relaxed);publishedErrors.store(stats.errors,std::memory_order_relaxed);
        publishedBytes.store(stats.bytes,std::memory_order_relaxed);publishedEvents.store(stats.events,std::memory_order_relaxed);publishedHR.store(stats.lastHR,std::memory_order_relaxed);
        publishedRenderUs.store(stats.frames?renderSum/double(stats.frames):0,std::memory_order_relaxed);publishedFrames.store(stats.frames,std::memory_order_release);
    }
};
std::mutex gate;std::shared_ptr<Renderer> current;int nextToken=0;thread_local HRESULT lastHR=S_OK;
std::shared_ptr<Renderer> get(int token){std::lock_guard<std::mutex> lock(gate);return current&&current->token==token?current:nullptr;}
void __stdcall event(int id) noexcept {try{auto r=get(id>>16);if(r){std::lock_guard<std::mutex> lock(r->mutex);r->render(unsigned(id&65535));}}catch(...){}}
void __stdcall timelineEvent(int id) noexcept {try{auto r=get(id>>16);if(r){
    std::lock_guard<std::mutex> lock(r->mutex);unsigned marker=unsigned(id&65535);
    // Pair a submission-time DXGI observation with this exact timestamp slot.
    // It is correlation evidence, not a GPU residency/page-fault measurement.
    if(marker==0&&r->timeline.mainSerial%64==0){r->memory.sample();const auto& p=r->memory.perf;r->timeline.mark(marker,p.currentUsage,p.currentBudget,p.nonLocalUsage);}
    else r->timeline.mark(marker);
}}catch(...){}}
}
extern "C" intptr_t __cdecl sf_z_drfull_v1(int op,int token,const void* args) noexcept {
    try {
        if(op==drfull::Create){require(args!=nullptr);auto r=std::make_shared<Renderer>(*static_cast<const drfull::CreateIn*>(args));std::lock_guard<std::mutex> lock(gate);require(!current&&nextToken<32767);r->token=++nextToken;current=r;return r->token;}
        if(op==drfull::Abi){require(args!=nullptr);*const_cast<drfull::AbiInfo*>(static_cast<const drfull::AbiInfo*>(args))={1,sizeof(drfull::CreateIn),sizeof(drfull::ProgramIn),sizeof(drfull::GeometryIn),sizeof(drfull::DrawIn),sizeof(drfull::Frame),sizeof(drfull::Statistics),sizeof(drfull::Pose)};return 1;}
        if(op==drfull::Event)return reinterpret_cast<intptr_t>(&event);
        if(op==drfull::TimelineEvent){auto r=get(token);require(r!=nullptr);std::lock_guard<std::mutex> lock(r->mutex);if(!r->timeline.context)r->timeline.init(r->device.Get(),r->context.Get());return reinterpret_cast<intptr_t>(&timelineEvent);}
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
        if(op==drfull::Configure){require(args&&r->stats.events==0);auto c=*static_cast<const drfull::Configuration*>(args);require(c.submission<=1&&c.constantRing<=1);r->configuration=c;return 1;}
        if(op==drfull::SubmissionPerf){*const_cast<drfull::SubmissionPerformance*>(static_cast<const drfull::SubmissionPerformance*>(args))=r->submission;return 1;}
        if(op==drfull::MemoryPerf){r->sampleMemory();*const_cast<drfull::MemoryPerformance*>(static_cast<const drfull::MemoryPerformance*>(args))=r->memory.perf;return 1;}
        if(op==drfull::TimelinePerf){*const_cast<drfull::TimelinePerformance*>(static_cast<const drfull::TimelinePerformance*>(args))=r->timeline.perf;return 1;}
        switch(op){case drfull::GpuPerf:*const_cast<drfull::GpuPerformance*>(static_cast<const drfull::GpuPerformance*>(args))=r->gpuPerf;return 1;case drfull::UploadPerf:*const_cast<drfull::UploadPerformance*>(static_cast<const drfull::UploadPerformance*>(args))=r->uploadPerf;return 1;case drfull::GeometryCpu:return r->cpuGeometry(*static_cast<const drfull::CpuGeometry*>(args));case drfull::BatchPerf:*const_cast<drfull::BatchPerformance*>(static_cast<const drfull::BatchPerformance*>(args))=r->batchPerf;return 1;case drfull::Perf:{auto p=r->perf;const double n=double(std::max(uint64_t(1),r->perf.sampledFrames));p.applyUs/=n;p.cullUs/=n;p.constantsUs/=n;p.drawUs/=n;p.executeUs/=n;*const_cast<drfull::Performance*>(static_cast<const drfull::Performance*>(args))=p;return 1;}case drfull::Composite:return r->composite(*static_cast<const drfull::CompositeIn*>(args));case drfull::Program:return r->program(*static_cast<const drfull::ProgramIn*>(args));case drfull::Geometry:return r->geometry(*static_cast<const drfull::GeometryIn*>(args));case drfull::Draw:return r->draw(*static_cast<const drfull::DrawIn*>(args));default:throw E_INVALIDARG;}
    }catch(HRESULT hr){lastHR=hr;return 0;}catch(...){lastHR=E_FAIL;return 0;}
}
