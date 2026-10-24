// StutterFix contributors, MIT. Independent D3D11 buffer-copy fixture.
// No game process, swapchain, Present, hooks, or Unity pointers are used.
#include <windows.h>
#include <d3d11.h>
#include <d3d11sdklayers.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <memory>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>
using Microsoft::WRL::ComPtr;
static void check(HRESULT hr) {if(FAILED(hr))throw std::runtime_error("HRESULT="+std::to_string(static_cast<unsigned long>(hr)));}
static void require(bool yes,const char* why) {if(!yes)throw std::runtime_error(why);}
static double now() {LARGE_INTEGER n,f;QueryPerformanceCounter(&n);QueryPerformanceFrequency(&f);return static_cast<double>(n.QuadPart)*1000.0/static_cast<double>(f.QuadPart);}
static void wait(ID3D11DeviceContext* context,ID3D11Asynchronous* query,void* data,UINT size) {
    double limit=now()+10000;
    for(;;) {HRESULT hr=context->GetData(query,data,size,0);if(hr==S_OK)return;check(hr);require(now()<limit,"GPU query timeout");Sleep(0);}
}
struct Fixture {
    static constexpr UINT words=36,bytes=144;
    UINT count;
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    ComPtr<ID3D11Buffer> source,staging;
    ComPtr<ID3D11ShaderResourceView> sourceView;
    ComPtr<ID3D11ComputeShader> shader;
    ComPtr<ID3D11CommandList> shaderList,copyList,groupList;
    std::vector<ComPtr<ID3D11Buffer>> destinations,constants;
    std::vector<ComPtr<ID3D11UnorderedAccessView>> views;
    std::vector<uint32_t> expected;
    Fixture(ID3D11Device* d,ID3D11DeviceContext* c,UINT n):count(n),device(d),context(c) {
        expected.resize(count*words);
        for(UINT i=0;i<expected.size();i++)expected[i]=0x3f000000u^(i*2654435761u);
        D3D11_BUFFER_DESC bd{};bd.ByteWidth=count*bytes;bd.Usage=D3D11_USAGE_DEFAULT;
        bd.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_UNORDERED_ACCESS;bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
        D3D11_SUBRESOURCE_DATA init{};init.pSysMem=expected.data();check(device->CreateBuffer(&bd,&init,&source));
        D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=DXGI_FORMAT_R32_TYPELESS;sv.ViewDimension=D3D11_SRV_DIMENSION_BUFFEREX;
        sv.BufferEx.NumElements=count*words;sv.BufferEx.Flags=D3D11_BUFFEREX_SRV_FLAG_RAW;check(device->CreateShaderResourceView(source.Get(),&sv,&sourceView));
        bd.Usage=D3D11_USAGE_STAGING;bd.CPUAccessFlags=D3D11_CPU_ACCESS_READ;bd.BindFlags=0;bd.MiscFlags=0;check(device->CreateBuffer(&bd,nullptr,&staging));
        for(UINT i=0;i<count;i++) {
            bd={};bd.ByteWidth=bytes;bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_VERTEX_BUFFER|D3D11_BIND_UNORDERED_ACCESS;
            bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;ComPtr<ID3D11Buffer> target;check(device->CreateBuffer(&bd,nullptr,&target));destinations.push_back(target);
            D3D11_UNORDERED_ACCESS_VIEW_DESC uv{};uv.Format=DXGI_FORMAT_R32_TYPELESS;uv.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;
            uv.Buffer.NumElements=words;uv.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;ComPtr<ID3D11UnorderedAccessView> view;
            check(device->CreateUnorderedAccessView(target.Get(),&uv,&view));views.push_back(view);
            uint32_t info[4]={i*words,0,0,0};bd={};bd.ByteWidth=16;bd.Usage=D3D11_USAGE_IMMUTABLE;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
            init.pSysMem=info;ComPtr<ID3D11Buffer> cb;check(device->CreateBuffer(&bd,&init,&cb));constants.push_back(cb);
        }
        const char* code="cbuffer Info:register(b0){uint first;uint3 pad;} ByteAddressBuffer src:register(t0);RWByteAddressBuffer dst:register(u0);[numthreads(64,1,1)]void CS(uint3 id:SV_DispatchThreadID){if(id.x<36)dst.Store(id.x*4,src.Load((first+id.x)*4));}";
        ComPtr<ID3DBlob> binary,error;check(D3DCompile(code,strlen(code),nullptr,nullptr,nullptr,"CS","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&binary,&error));
        check(device->CreateComputeShader(binary->GetBufferPointer(),binary->GetBufferSize(),nullptr,&shader));
        shaderList=record(true);copyList=record(false);
    }
    ComPtr<ID3D11CommandList> record(bool compute) {
        ComPtr<ID3D11DeviceContext> deferred;check(device->CreateDeferredContext(0,&deferred));
        if(compute) {deferred->CSSetShader(shader.Get(),nullptr,0);auto* srv=sourceView.Get();deferred->CSSetShaderResources(0,1,&srv);}
        for(UINT i=0;i<count;i++) {
            if(compute) {
                auto* uav=views[i].Get();auto* cb=constants[i].Get();deferred->CSSetUnorderedAccessViews(0,1,&uav,nullptr);
                deferred->CSSetConstantBuffers(0,1,&cb);deferred->Dispatch(1,1,1);
            } else {
                D3D11_BOX box{i*bytes,0,0,(i+1)*bytes,1,1};
                deferred->CopySubresourceRegion(destinations[i].Get(),0,0,0,0,source.Get(),0,&box);
            }
        }
        ComPtr<ID3D11CommandList> result;check(deferred->FinishCommandList(FALSE,&result));return result;
    }
    void recordGrouped() {
        // Eight independent raw UAVs in one dispatch; bit copies only.
        // This is a standalone cost hypothesis, not an adopted game backend.
        std::string code="cbuffer Info:register(b0){uint first;uint valid;uint2 pad;} ByteAddressBuffer src:register(t0);";
        for(int i=0;i<8;i++)code+="RWByteAddressBuffer d"+std::to_string(i)+":register(u"+std::to_string(i)+");";
        code+="[numthreads(64,1,1)]void CS(uint3 id:SV_DispatchThreadID){if(id.x<36){";
        for(int i=0;i<8;i++)code+="if(valid>"+std::to_string(i)+")d"+std::to_string(i)+".Store(id.x*4,src.Load((first+"+std::to_string(i*words)+"+id.x)*4));";
        code+="}}";
        ComPtr<ID3DBlob> binary,error;check(D3DCompile(code.data(),code.size(),nullptr,nullptr,nullptr,"CS","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&binary,&error));
        ComPtr<ID3D11ComputeShader> grouped;check(device->CreateComputeShader(binary->GetBufferPointer(),binary->GetBufferSize(),nullptr,&grouped));
        ComPtr<ID3D11DeviceContext> deferred;check(device->CreateDeferredContext(0,&deferred));
        deferred->CSSetShader(grouped.Get(),nullptr,0);auto* srv=sourceView.Get();deferred->CSSetShaderResources(0,1,&srv);
        for(UINT first=0;first<count;first+=8) {
            UINT valid=(std::min)(8u,count-first);ID3D11UnorderedAccessView* targets[8]{};
            for(UINT i=0;i<valid;i++)targets[i]=views[first+i].Get();
            uint32_t data[4]={first*words,valid,0,0};D3D11_BUFFER_DESC bd{};bd.ByteWidth=16;bd.Usage=D3D11_USAGE_IMMUTABLE;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
            D3D11_SUBRESOURCE_DATA init{};init.pSysMem=data;ComPtr<ID3D11Buffer> cb;check(device->CreateBuffer(&bd,&init,&cb));auto* pointer=cb.Get();
            deferred->CSSetConstantBuffers(0,1,&pointer);deferred->CSSetUnorderedAccessViews(0,8,targets,nullptr);deferred->Dispatch(1,1,1);
        }
        check(deferred->FinishCommandList(FALSE,&groupList));
    }
    void verify(ID3D11CommandList* list,uint32_t salt,void (CALLBACK* callback)(int)=nullptr,int token=0,bool sourceBoundAsOutput=false,const std::vector<uint32_t>* outputExpected=nullptr) {
        for(UINT i=0;i<expected.size();i++)expected[i]=(0x3f000000u^(i*2654435761u))+salt;
        context->UpdateSubresource(source.Get(),0,nullptr,expected.data(),0,0);
        context->CSSetShader(shader.Get(),nullptr,0);auto* srv=sourceView.Get();context->CSSetShaderResources(0,1,&srv);
        ComPtr<ID3D11UnorderedAccessView> sourceOutput;
        if(sourceBoundAsOutput) {
            ID3D11ShaderResourceView* empty=nullptr;context->CSSetShaderResources(0,1,&empty);
            D3D11_UNORDERED_ACCESS_VIEW_DESC uv{};uv.Format=DXGI_FORMAT_R32_TYPELESS;uv.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;
            uv.Buffer.NumElements=count*words;uv.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;
            check(device->CreateUnorderedAccessView(source.Get(),&uv,&sourceOutput));auto* uav=sourceOutput.Get();context->CSSetUnorderedAccessViews(7,1,&uav,nullptr);
        }
        auto* vb=destinations[0].Get();UINT stride=36,offset=0;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
        if(callback)callback(token);else context->ExecuteCommandList(list,TRUE);
        ComPtr<ID3D11ComputeShader> after;context->CSGetShader(&after,nullptr,nullptr);require(after.Get()==shader.Get(),"compute state not restored");
        ComPtr<ID3D11ShaderResourceView> afterView;context->CSGetShaderResources(0,1,&afterView);require(afterView.Get()==(sourceBoundAsOutput?nullptr:sourceView.Get()),"SRV state not restored");
        if(sourceBoundAsOutput){ComPtr<ID3D11UnorderedAccessView> afterOutput;context->CSGetUnorderedAccessViews(7,1,&afterOutput);require(afterOutput.Get()==sourceOutput.Get(),"source UAV state not restored");}
        ComPtr<ID3D11Buffer> afterVertex;UINT afterStride=0,afterOffset=0;context->IAGetVertexBuffers(0,1,&afterVertex,&afterStride,&afterOffset);
        require(afterVertex.Get()==vb && afterStride==stride && afterOffset==offset,"vertex input state not restored");
        context->ClearState();
        for(UINT i=0;i<count;i++) {D3D11_BOX box{0,0,0,bytes,1,1};context->CopySubresourceRegion(staging.Get(),0,i*bytes,0,0,destinations[i].Get(),0,&box);}
        D3D11_MAPPED_SUBRESOURCE read{};check(context->Map(staging.Get(),0,D3D11_MAP_READ,0,&read));
        const auto& wanted=outputExpected?*outputExpected:expected;
        bool equal=memcmp(read.pData,wanted.data(),expected.size()*sizeof(uint32_t))==0;context->Unmap(staging.Get(),0);require(equal,"copy bytes mismatch");
    }
    void measure(ID3D11CommandList* list,const char* mode,int sample) {
        constexpr int frames=64;
        for(int i=0;i<8;i++)context->ExecuteCommandList(list,TRUE);
        ComPtr<ID3D11Query> disjoint,begin,end;D3D11_QUERY_DESC q{};q.Query=D3D11_QUERY_TIMESTAMP_DISJOINT;check(device->CreateQuery(&q,&disjoint));
        q.Query=D3D11_QUERY_TIMESTAMP;check(device->CreateQuery(&q,&begin));check(device->CreateQuery(&q,&end));
        context->Begin(disjoint.Get());context->End(begin.Get());double start=now();
        for(int i=0;i<frames;i++)context->ExecuteCommandList(list,TRUE);
        double cpu=(now()-start)/frames;context->End(end.Get());context->End(disjoint.Get());
        D3D11_QUERY_DATA_TIMESTAMP_DISJOINT clock{};UINT64 a=0,b=0;
        wait(context.Get(),disjoint.Get(),&clock,sizeof(clock));wait(context.Get(),begin.Get(),&a,sizeof(a));wait(context.Get(),end.Get(),&b,sizeof(b));
        require(!clock.Disjoint && clock.Frequency>0,"timestamp disjoint");
        double gpu=static_cast<double>(b-a)*1000.0/static_cast<double>(clock.Frequency)/frames;
        printf("copy_fixture count=%u bytes_each=%u mode=%s sample=%d frames=%d cpu_execute_ms=%.6f gpu_ms=%.6f restore_context=True\n",count,bytes,mode,sample,frames,cpu,gpu);fflush(stdout);
    }
};
struct NativeApi {
    struct Stats {uint64_t submitted,executed,ticks,maxTicks,errors;};
    HMODULE module;
    int (__cdecl* version)();
    int (__cdecl* create)(void*,void* const*,const unsigned*,int,unsigned);
    void* (__cdecl* event)();
    int (__cdecl* submit)(int);
    int (__cdecl* stats)(int,Stats*);
    void (__cdecl* destroy)(int);
    unsigned (__cdecl* threadId)(int);
    template<class T> T load(const char* name) {auto p=GetProcAddress(module,name);require(p!=nullptr,"native export missing");T result;static_assert(sizeof(result)==sizeof(p));memcpy(&result,&p,sizeof(p));return result;}
    explicit NativeApi(const char* path,bool eight=false):module(LoadLibraryA(path)) {
        require(module!=nullptr,"native LoadLibrary failed");
        version=load<decltype(version)>("sf_deco_copy_version");create=load<decltype(create)>(eight?"sf_deco_copy_create8":"sf_deco_copy_create");
        event=load<decltype(event)>("sf_deco_copy_event");submit=load<decltype(submit)>("sf_deco_copy_submit");
        stats=load<decltype(stats)>("sf_deco_copy_stats");destroy=load<decltype(destroy)>("sf_deco_copy_destroy");threadId=load<decltype(threadId)>("sf_deco_copy_thread");require(version()==1,"native version changed");
    }
    ~NativeApi(){if(module)FreeLibrary(module);}
    void verify(Fixture& f) {
        std::vector<void*> pointers;std::vector<unsigned> offsets;
        for(UINT i=0;i<f.count;i++){pointers.push_back(f.destinations[i].Get());offsets.push_back(i*Fixture::bytes);}
        require(create(nullptr,pointers.data(),offsets.data(),static_cast<int>(f.count),Fixture::bytes)==0,"invalid source accepted");
        require(create(f.source.Get(),pointers.data(),offsets.data(),static_cast<int>(f.count),145)==0,"unaligned size accepted");
        auto callback=reinterpret_cast<void (CALLBACK*)(int)>(event());require(callback!=nullptr,"render event missing");
        int id=create(f.source.Get(),pointers.data(),offsets.data(),static_cast<int>(f.count),Fixture::bytes);require(id>0,"native create failed");
        for(uint32_t salt:{42u,918u}){require(submit(id)==1,"submit failed");f.verify(nullptr,salt,callback,id,salt==918);}
        Stats s{};require(stats(id,&s)==1 && s.submitted==2 && s.executed==2 && s.errors==0,"native callback stats mismatch");
        require(threadId(id)==GetCurrentThreadId(),"native render thread identity mismatch");
        destroy(id);callback(id);require(submit(id)==0 && stats(id,&s)==0 && threadId(id)==0,"removed token still active");destroy(id);
        int next=create(f.source.Get(),pointers.data(),offsets.data(),static_cast<int>(f.count),Fixture::bytes);require(next>id,"token reused");
        require(submit(next)==1,"second token submit failed");callback(next);
        std::thread wrong([&](){callback(next);});wrong.join();
        require(stats(next,&s)==1 && s.errors==1 && submit(next)==0,"wrong-thread fallback failed");destroy(next);
        printf("native_api count=%u patterns=2 byte_errors=0 state_errors=0 source_prebound_uav=True removed_callback=skip token_reuse=0 wrong_thread=rejected\n",f.count);
    }
};
int main(int argc,char** argv) {
    try {
        bool debugRequested=argc>1 && std::string(argv[1])=="--debug";
        bool nativeRequested=argc==3 && (std::string(argv[1])=="--native" || std::string(argv[1])=="--native8");
        bool groupedRequested=argc>1 && std::string(argv[1])=="--grouped";
        std::unique_ptr<NativeApi> native;if(nativeRequested)native=std::make_unique<NativeApi>(argv[2],std::string(argv[1])=="--native8");
        ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context;D3D_FEATURE_LEVEL level=D3D_FEATURE_LEVEL_11_0,actual{};
        HRESULT hr=D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,debugRequested?D3D11_CREATE_DEVICE_DEBUG:0,&level,1,D3D11_SDK_VERSION,&device,&actual,&context);
        check(hr);printf("device=D3D11 hardware=True debug=%s game=False swapchain=False Present=False\n",debugRequested?"True":"False");
        for(UINT count:{1u,128u,1715u}) {
            Fixture f(device.Get(),context.Get(),count);
            if(groupedRequested) {
                f.recordGrouped();f.verify(f.groupList.Get(),192);f.verify(f.groupList.Get(),512);
                printf("grouped_bytes=PASS count=%u patterns=2 state_errors=0\n",count);
                for(int sample=0;sample<3;sample++){f.measure(f.copyList.Get(),"copy_region",sample);f.measure(f.groupList.Get(),"eight_uav",sample);}
            }
            else if(native)native->verify(f);
            else {f.verify(f.shaderList.Get(),123);f.verify(f.copyList.Get(),987);printf("copy_bytes=PASS count=%u state_errors=0\n",count);}
            if(!debugRequested && !nativeRequested && !groupedRequested)for(int sample=0;sample<3;sample++) {f.measure(f.shaderList.Get(),"shader_dispatch",sample);f.measure(f.copyList.Get(),"copy_region",sample);}
            context->ClearState();context->Flush();
        }
        if(debugRequested) {
            ComPtr<ID3D11InfoQueue> info;check(device.As(&info));UINT64 errors=0;
            for(UINT64 i=0;i<info->GetNumStoredMessagesAllowedByRetrievalFilter();i++) {
                SIZE_T size=0;check(info->GetMessage(i,nullptr,&size));std::vector<unsigned char> data(size);auto* message=reinterpret_cast<D3D11_MESSAGE*>(data.data());check(info->GetMessage(i,message,&size));
                if(message->Severity<=D3D11_MESSAGE_SEVERITY_ERROR) {errors++;fprintf(stderr,"D3D11: %s\n",message->pDescription);}
            }
            printf("debug_errors=%llu\n",static_cast<unsigned long long>(errors));require(errors==0,"D3D11 debug errors");
        } else printf("debug_errors=UNMEASURED\n");
        printf("GPUDECO_COPY_LAB=PASS Unity_ordering=UNMEASURED game_fps=UNMEASURED plugin_lifetime=UNMEASURED\n");return 0;
    } catch(const std::exception& e) {fprintf(stderr,"GPUDECO_COPY_LAB=ERROR reason=%s\n",e.what());return 1;}
}
