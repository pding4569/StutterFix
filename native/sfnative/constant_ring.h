// StutterFix original code, MIT. Owned D3D11 buffers only, never Unity buffers.
#pragma once
#include <d3d11.h>
#include <wrl/client.h>
#include <array>
#include <cstring>
#include <atomic>
namespace sf_buffers {
struct Counters {
    std::atomic<unsigned long long> writes{0},fallbacks{0};
    std::atomic<unsigned> supported{0};
    void reset() {writes=0;fallbacks=0;supported=0;}
};
inline Counters gameCounters;
class ConstantRing {
    struct Slot {
        Microsoft::WRL::ComPtr<ID3D11Buffer> buffer;
        Microsoft::WRL::ComPtr<ID3D11Query> retired;
        bool inFlight=false,reserved=false;
    };
    std::array<Slot,3> slots{};
    unsigned bytes=0,next=0;
    std::atomic<bool> available{false};
    Counters* shared=nullptr;
    void miss() {++fallbacks;if(shared)shared->fallbacks.fetch_add(1,std::memory_order_relaxed);}
public:
    std::atomic<unsigned> writes{0},fallbacks{0};
    bool supported() const { return available.load(); }
    void reportTo(Counters* counters) {shared=counters;}
    ~ConstantRing() {reset();}
    void reset() {
        for(auto& s:slots) { s.buffer.Reset();s.retired.Reset();s.inFlight=s.reserved=false; }
        bytes=next=writes=fallbacks=0;available=false;
        if(shared)shared->supported.store(0,std::memory_order_relaxed);
    }
    bool initialize(ID3D11Device* device,unsigned size) {
        reset();
        if(!device || !size || size%16 || size>65536) return false;
        D3D11_FEATURE_DATA_D3D11_OPTIONS options{};
        if(FAILED(device->CheckFeatureSupport(D3D11_FEATURE_D3D11_OPTIONS,&options,sizeof(options))) || !options.MapNoOverwriteOnDynamicConstantBuffer) return false;
        D3D11_BUFFER_DESC desc{};desc.ByteWidth=size;desc.Usage=D3D11_USAGE_DYNAMIC;
        desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;desc.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;
        D3D11_QUERY_DESC query{};query.Query=D3D11_QUERY_EVENT;
        for(auto& s:slots) {
            if(FAILED(device->CreateBuffer(&desc,nullptr,&s.buffer)) || FAILED(device->CreateQuery(&query,&s.retired))) {reset();return false;}
        }
        bytes=size;available=true;if(shared)shared->supported.store(1,std::memory_order_relaxed);return true;
    }
    ID3D11Buffer* write(ID3D11DeviceContext* context,const void* data,unsigned size) {
        if(!available || !context || !data || size!=bytes || context->GetType()!=D3D11_DEVICE_CONTEXT_IMMEDIATE) {miss();return nullptr;}
        for(unsigned n=0;n<slots.size();++n) {
            unsigned index=(next+n)%unsigned(slots.size());auto& s=slots[index];
            if(s.reserved) continue;
            if(s.inFlight) {
                BOOL done=FALSE;
                // No flush, wait or frame-count assumption. S_FALSE/error means
                // this slot remains unavailable; the caller keeps its old path.
                if(context->GetData(s.retired.Get(),&done,sizeof(done),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK || !done) continue;
                s.inFlight=false;
            }
            D3D11_MAPPED_SUBRESOURCE mapped{};
            if(FAILED(context->Map(s.buffer.Get(),0,D3D11_MAP_WRITE_NO_OVERWRITE,0,&mapped))) continue;
            std::memcpy(mapped.pData,data,bytes);context->Unmap(s.buffer.Get(),0);
            s.reserved=true;next=(index+1)%unsigned(slots.size());++writes;if(shared)shared->writes.fetch_add(1,std::memory_order_relaxed);return s.buffer.Get();
        }
        miss();return nullptr;
    }
    // Call after executing every command list that refers to reserved buffers.
    // A slot stays reserved across multiple binds in one unexecuted list.
    void submitted(ID3D11DeviceContext* context) {
        for(auto& s:slots) if(s.reserved) {context->End(s.retired.Get());s.reserved=false;s.inFlight=true;}
    }
    // Only for a list discarded BEFORE execution. Never abandon submitted work.
    void abandoned() { for(auto& s:slots) s.reserved=false; }
};
}
