#include <d3d11.h>
#include <wrl/client.h>
#include <array>
#include <atomic>
using Microsoft::WRL::ComPtr;
struct Slot {ComPtr<ID3D11Query> d,a,b;bool pending=false;int type=0;unsigned epoch=0;};
static std::array<Slot,512> slots;
static std::array<std::atomic<unsigned long long>,1024> counts{},ns{};
static ComPtr<ID3D11Device> device;static ComPtr<ID3D11DeviceContext> ctx;
static int active=-1;static unsigned epoch=0;
static std::atomic<unsigned long long> skipped{0},bad{0};
static void __stdcall event(int id,void* tex) noexcept {
 if(id==3){for(auto& s:slots)s=Slot{};ctx.Reset();device.Reset();active=-1;return;}
 if(id==4){++epoch;for(auto& v:counts)v=0;for(auto& v:ns)v=0;skipped=bad=0;return;}
 if(id==2){if(active>=0){auto& s=slots[active];ctx->End(s.b.Get());ctx->End(s.d.Get());s.pending=true;active=-1;}return;}
 if(id<1024||id>=2048||!tex)return;
 if(!device){static_cast<ID3D11Texture2D*>(tex)->GetDevice(&device);device->GetImmediateContext(&ctx);
 for(auto& s:slots){D3D11_QUERY_DESC q{D3D11_QUERY_TIMESTAMP_DISJOINT,0};
 if(FAILED(device->CreateQuery(&q,&s.d))){++bad;device.Reset();ctx.Reset();return;}q.Query=D3D11_QUERY_TIMESTAMP;
 if(FAILED(device->CreateQuery(&q,&s.a))||FAILED(device->CreateQuery(&q,&s.b))){++bad;device.Reset();ctx.Reset();return;}}}
 for(auto& s:slots)if(s.pending){D3D11_QUERY_DATA_TIMESTAMP_DISJOINT d{};UINT64 a=0,b=0;
 if(ctx->GetData(s.d.Get(),&d,sizeof d,D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK&&ctx->GetData(s.a.Get(),&a,sizeof a,D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK&&ctx->GetData(s.b.Get(),&b,sizeof b,D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK){
 s.pending=false;if(s.epoch==epoch){if(!d.Disjoint&&d.Frequency&&b>=a){++counts[s.type];ns[s.type]+=static_cast<unsigned long long>(double(b-a)*1e9/double(d.Frequency));}else ++bad;}}}
 for(int i=0;i<512;++i)if(!slots[i].pending){auto& s=slots[i];active=i;s.type=id-1024;s.epoch=epoch;ctx->Begin(s.d.Get());ctx->End(s.a.Get());return;}++skipped;
}
extern "C" __declspec(dllexport) void* fg_event(){return reinterpret_cast<void*>(&event);}
extern "C" __declspec(dllexport) unsigned long long fg_stat(int t,int k){if(t<0)return k==0?skipped.load():bad.load();return k==0?counts[t].load():ns[t].load();}
