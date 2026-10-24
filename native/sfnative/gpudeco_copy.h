// SPDX-License-Identifier: MIT. StutterFix contributors.
// Default-off research buffer copies. No swapchain, Present, or draw hooks.
#pragma once
#include <algorithm>
#include <array>
#include <atomic>
#include <cstdint>
#include <limits>
#include <memory>
#include <mutex>
#include <stdexcept>
#include <string>
#include <unordered_map>
#include <vector>
namespace sf_deco_copy {
struct Stats {uint64_t submitted,executed,ticks,maxTicks,errors;};
struct Copy {
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> immediate;
    ComPtr<ID3D11CommandList> commands;
    ComPtr<ID3D11Buffer> source;
    std::vector<ComPtr<ID3D11Buffer>> targets;
    std::vector<ComPtr<ID3D11CommandList>> chunks;
    // SPSC: one Unity submitting thread and one verified render-event thread.
    // Each event consumes its own mask, never a newer frame's visibility.
    struct Mask {uint64_t low,high;};
    std::array<Mask,128> masks{};
    std::atomic<uint64_t> maskWrite{0},maskRead{0};
    std::atomic<DWORD> producer{0};
    std::mutex eventMutex;
    std::atomic<uint64_t> submitted{0},errors{0};
    uint64_t executed=0,ticks=0,maxTicks=0;
    DWORD thread=0;
};
struct Registry {std::mutex mutex;std::unordered_map<int,std::shared_ptr<Copy>> entries;int next=1;};
static Registry& registry() {static Registry value;return value;}
static std::shared_ptr<Copy> find(int id) {
    auto& r=registry();std::lock_guard<std::mutex> lock(r.mutex);auto it=r.entries.find(id);return it==r.entries.end()?nullptr:it->second;
}
static void checked(HRESULT hr) {if(FAILED(hr))throw std::runtime_error("D3D11 copy initialization failed");}
static void recordEight(Copy& copy,ID3D11DeviceContext* deferred,const unsigned* offsets,size_t begin,size_t end,bool scatter=false) {
    std::string code=scatter?"cbuffer Info:register(b0){uint4 a;uint4 b;uint valid;uint3 pad;}ByteAddressBuffer src:register(t0);"
        :"cbuffer Info:register(b0){uint first;uint valid;uint2 pad;}ByteAddressBuffer src:register(t0);";
    for(int i=0;i<8;i++)code+="RWByteAddressBuffer d"+std::to_string(i)+":register(u"+std::to_string(i)+");";
    code+="[numthreads(64,1,1)]void CS(uint3 id:SV_DispatchThreadID){if(id.x<36){";
    for(int i=0;i<8;i++) {
        std::string offset=scatter?std::string(i<4?"a.":"b.")+"xyzw"[i&3]:"first+"+std::to_string(i*36);
        code+="if(valid>"+std::to_string(i)+")d"+std::to_string(i)+".Store(id.x*4,src.Load(("+offset+"+id.x)*4));";
    }
    code+="}}";
    ComPtr<ID3DBlob> binary,error;checked(D3DCompile(code.data(),code.size(),nullptr,nullptr,nullptr,"CS","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&binary,&error));
    ComPtr<ID3D11ComputeShader> copyShader;checked(copy.device->CreateComputeShader(binary->GetBufferPointer(),binary->GetBufferSize(),nullptr,&copyShader));
    D3D11_BUFFER_DESC source{};copy.source->GetDesc(&source);
    D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=DXGI_FORMAT_R32_TYPELESS;sv.ViewDimension=D3D11_SRV_DIMENSION_BUFFEREX;
    sv.BufferEx.NumElements=source.ByteWidth/4;sv.BufferEx.Flags=D3D11_BUFFEREX_SRV_FLAG_RAW;
    ComPtr<ID3D11ShaderResourceView> view;checked(copy.device->CreateShaderResourceView(copy.source.Get(),&sv,&view));
    deferred->CSSetShader(copyShader.Get(),nullptr,0);auto* pointer=view.Get();deferred->CSSetShaderResources(0,1,&pointer);
    for(size_t first=begin;first<end;first+=8) {
        UINT valid=static_cast<UINT>((std::min)(size_t{8},end-first));
        ComPtr<ID3D11UnorderedAccessView> owned[8];ID3D11UnorderedAccessView* targets[8]{};
        for(UINT i=0;i<valid;i++) {
            if(!scatter && offsets[first+i]!=offsets[first]+i*144)throw std::runtime_error("non-contiguous quad offsets");
            D3D11_UNORDERED_ACCESS_VIEW_DESC uv{};uv.Format=DXGI_FORMAT_R32_TYPELESS;uv.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;
            uv.Buffer.NumElements=36;uv.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;
            checked(copy.device->CreateUnorderedAccessView(copy.targets[first+i].Get(),&uv,&owned[i]));targets[i]=owned[i].Get();
        }
        uint32_t values[12]{};
        if(scatter){for(UINT i=0;i<valid;i++)values[i]=offsets[first+i]/4;values[8]=valid;}
        else {values[0]=offsets[first]/4;values[1]=valid;}
        D3D11_BUFFER_DESC cb{};cb.ByteWidth=scatter?48:16;cb.Usage=D3D11_USAGE_IMMUTABLE;cb.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        D3D11_SUBRESOURCE_DATA data{};data.pSysMem=values;ComPtr<ID3D11Buffer> constant;checked(copy.device->CreateBuffer(&cb,&data,&constant));auto* cbPointer=constant.Get();
        deferred->CSSetConstantBuffers(0,1,&cbPointer);deferred->CSSetUnorderedAccessViews(0,8,targets,nullptr);deferred->Dispatch(1,1,1);
    }
}
static void CALLBACK replay(int id) noexcept {
    std::shared_ptr<Copy> copy;
    LARGE_INTEGER begin,end;QueryPerformanceCounter(&begin);
    try {
        copy=find(id);if(!copy)return;
        std::lock_guard<std::mutex> lock(copy->eventMutex);
        DWORD current=GetCurrentThreadId();
        if(copy->thread!=0 && copy->thread!=current)copy->errors.fetch_add(1);
        else {
            copy->thread=current;
            if(copy->chunks.empty())copy->immediate->ExecuteCommandList(copy->commands.Get(),TRUE);
            else {
                uint64_t read=copy->maskRead.load(std::memory_order_relaxed);
                if(read==copy->maskWrite.load(std::memory_order_acquire)){copy->errors.fetch_add(1);return;}
                auto mask=copy->masks[read%copy->masks.size()];
                copy->maskRead.store(read+1,std::memory_order_release);
                for(size_t i=0;i<copy->chunks.size();i++)
                    if(((i<64?mask.low:mask.high)>>(i&63))&1)
                        copy->immediate->ExecuteCommandList(copy->chunks[i].Get(),TRUE);
            }
            if(FAILED(copy->device->GetDeviceRemovedReason()))copy->errors.fetch_add(1);
        }
        QueryPerformanceCounter(&end);uint64_t elapsed=static_cast<uint64_t>(end.QuadPart-begin.QuadPart);
        copy->executed++;copy->ticks+=elapsed;if(elapsed>copy->maxTicks)copy->maxTicks=elapsed;
    } catch(...) {if(copy)copy->errors.fetch_add(1);}
}
}
API int sf_deco_copy_version() noexcept {return 1;}
static int sf_deco_copy_create_impl(void* source,void* const* targets,const unsigned* offsets,int count,unsigned bytes,bool eight,bool masked=false) noexcept {
    try {
        if(!source || !targets || !offsets || count<=0 || count>65536 || bytes==0 || (bytes&3)!=0 || (eight && bytes!=144))return 0;
        auto copy=std::make_shared<sf_deco_copy::Copy>();copy->source=static_cast<ID3D11Buffer*>(source);
        copy->source->GetDevice(&copy->device);if(!copy->device || copy->device->GetFeatureLevel()<D3D_FEATURE_LEVEL_11_0
            || (copy->device->GetCreationFlags()&D3D11_CREATE_DEVICE_SINGLETHREADED)!=0)return 0;
        D3D11_BUFFER_DESC sourceDesc{};copy->source->GetDesc(&sourceDesc);if(sourceDesc.ByteWidth<bytes)return 0;
        ComPtr<ID3D11DeviceContext> deferred;sf_deco_copy::checked(copy->device->CreateDeferredContext(0,&deferred));
        for(int i=0;i<count;i++) {
            if(!targets[i] || offsets[i]>sourceDesc.ByteWidth-bytes || (offsets[i]&3)!=0)return 0;
            ComPtr<ID3D11Buffer> target=static_cast<ID3D11Buffer*>(targets[i]);ComPtr<ID3D11Device> device;target->GetDevice(&device);
            D3D11_BUFFER_DESC desc{};target->GetDesc(&desc);
            if(device.Get()!=copy->device.Get() || desc.ByteWidth<bytes || desc.Usage!=D3D11_USAGE_DEFAULT)return 0;
            if(!eight && !masked){D3D11_BOX box{offsets[i],0,0,offsets[i]+bytes,1,1};deferred->CopySubresourceRegion(target.Get(),0,0,0,0,copy->source.Get(),0,&box);}
            copy->targets.push_back(target);
        }
        if(masked) {
            for(size_t first=0;first<copy->targets.size();first+=512) {
                size_t end=(std::min)(first+512,copy->targets.size());
                if(eight)sf_deco_copy::recordEight(*copy,deferred.Get(),offsets,first,end,true);
                else for(size_t i=first;i<end;i++) {
                    D3D11_BOX box{offsets[i],0,0,offsets[i]+bytes,1,1};
                    deferred->CopySubresourceRegion(copy->targets[i].Get(),0,0,0,0,copy->source.Get(),0,&box);
                }
                ComPtr<ID3D11CommandList> chunk;sf_deco_copy::checked(deferred->FinishCommandList(FALSE,&chunk));copy->chunks.push_back(chunk);
            }
        } else {
            if(eight)sf_deco_copy::recordEight(*copy,deferred.Get(),offsets,0,copy->targets.size());
            sf_deco_copy::checked(deferred->FinishCommandList(FALSE,&copy->commands));
        }
        copy->device->GetImmediateContext(&copy->immediate);
        auto& registry=sf_deco_copy::registry();std::lock_guard<std::mutex> lock(registry.mutex);
        if(registry.entries.size()>=64 || registry.next==(std::numeric_limits<int>::max)())return 0;
        int id=registry.next++;registry.entries.emplace(id,copy);return id;
    } catch(...) {return 0;}
}
API int sf_deco_copy_create(void* source,void* const* targets,const unsigned* offsets,int count,unsigned bytes) noexcept {
    return sf_deco_copy_create_impl(source,targets,offsets,count,bytes,false);
}
API int sf_deco_copy_create8(void* source,void* const* targets,const unsigned* offsets,int count,unsigned bytes) noexcept {
    return sf_deco_copy_create_impl(source,targets,offsets,count,bytes,true);
}
API int sf_deco_copy_create8masked(void* source,void* const* targets,const unsigned* offsets,int count,unsigned bytes) noexcept {
    return sf_deco_copy_create_impl(source,targets,offsets,count,bytes,true,true);
}
API int sf_deco_copy_create_masked(void* source,void* const* targets,const unsigned* offsets,int count,unsigned bytes) noexcept {
    return sf_deco_copy_create_impl(source,targets,offsets,count,bytes,false,true);
}
API void* sf_deco_copy_event() noexcept {return reinterpret_cast<void*>(&sf_deco_copy::replay);}
API int sf_deco_copy_submit(int id) noexcept {
    try {auto copy=sf_deco_copy::find(id);if(!copy || !copy->chunks.empty() || copy->errors.load()!=0)return 0;copy->submitted.fetch_add(1);return 1;}catch(...) {return 0;}
}
API int sf_deco_copy_submit_mask(int id,uint64_t low,uint64_t high) noexcept {
    try {
        auto copy=sf_deco_copy::find(id);if(!copy || copy->chunks.empty() || copy->errors.load()!=0)return 0;
        DWORD current=GetCurrentThreadId(),expected=0;
        copy->producer.compare_exchange_strong(expected,current);
        if(copy->producer.load()!=current){copy->errors.fetch_add(1);return 0;}
        uint64_t write=copy->maskWrite.load(std::memory_order_relaxed),read=copy->maskRead.load(std::memory_order_acquire);
        if(write-read>=copy->masks.size()){copy->errors.fetch_add(1);return 0;}
        copy->masks[write%copy->masks.size()]={low,high};
        copy->maskWrite.store(write+1,std::memory_order_release);copy->submitted.fetch_add(1);return 1;
    }catch(...) {return 0;}
}
API int sf_deco_copy_stats(int id,sf_deco_copy::Stats* output) noexcept {
    try {
        if(!output)return 0;auto copy=sf_deco_copy::find(id);if(!copy)return 0;
        std::lock_guard<std::mutex> lock(copy->eventMutex);
        *output={copy->submitted.load(),copy->executed,copy->ticks,copy->maxTicks,copy->errors.load()};return 1;
    }catch(...) {return 0;}
}
API void sf_deco_copy_destroy(int id) noexcept {
    try {
        std::shared_ptr<sf_deco_copy::Copy> released;
        auto& registry=sf_deco_copy::registry();
        {std::lock_guard<std::mutex> lock(registry.mutex);auto it=registry.entries.find(id);if(it!=registry.entries.end()){released=it->second;registry.entries.erase(it);}}
        // Queued callbacks resolve the non-reused integer token. They either
        // keep their own shared ownership or skip a removed token safely.
    }catch(...) {}
}
// Query outside measurement windows only. Never changes thread scheduling.
API unsigned sf_deco_copy_thread(int id) noexcept {
    try {auto copy=sf_deco_copy::find(id);if(!copy)return 0;std::lock_guard<std::mutex> lock(copy->eventMutex);return copy->thread;}catch(...) {return 0;}
}
