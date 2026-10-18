// Diagnostic-only plugin. Adapted from tools/filtergpu/query.cpp.
#define NOMINMAX
#include <d3d11.h>
#include <wrl/client.h>
#include <array>
#include <atomic>
#include <algorithm>
using Microsoft::WRL::ComPtr;
namespace gp {
constexpr int Scopes=512, Ring=8, Segments=96, CameraEnd=35;
struct Segment { ComPtr<ID3D11Query> a,b; int id=-1; bool closed=false; };
struct Frame { ComPtr<ID3D11Query> disjoint,a,b; std::array<Segment,Segments> seg; int used=0; bool pending=false,invalid=false; unsigned tick=0,epoch=0; };
static std::array<Frame,Ring> ring;
static ComPtr<ID3D11Device> device;
static ComPtr<ID3D11DeviceContext> ctx;
static int active=-1;
static unsigned tick=0;
static unsigned epoch=0;
static SRWLOCK lock=SRWLOCK_INIT;
// scope: frame count with work, sum ns, max per-frame ns, invocation count.
static unsigned long long stats[Scopes*4]{};
static std::atomic<unsigned long long> complete{0},bad{0},full{0},overflow{0},notReady{0},errors{0},ready{0},outsideCamera{0};
static void release(){ ready=0; if(ctx&&active>=0){ctx->End(ring[active].b.Get());ctx->End(ring[active].disjoint.Get());} active=-1; for(auto& f:ring) f=Frame{}; ctx.Reset();device.Reset(); }
static void init(void* texture){
 release(); tick=0; ++epoch; complete=bad=full=overflow=notReady=errors=outsideCamera=0;
 AcquireSRWLockExclusive(&lock); std::fill(std::begin(stats),std::end(stats),0); ReleaseSRWLockExclusive(&lock);
 if(!texture){++errors;return;}
 static_cast<ID3D11Texture2D*>(texture)->GetDevice(&device);device->GetImmediateContext(&ctx);
 auto create=[](D3D11_QUERY q,ComPtr<ID3D11Query>& p){D3D11_QUERY_DESC d{q,0};return SUCCEEDED(device->CreateQuery(&d,&p));};
 for(auto& f:ring){
  if(!create(D3D11_QUERY_TIMESTAMP_DISJOINT,f.disjoint)||!create(D3D11_QUERY_TIMESTAMP,f.a)||!create(D3D11_QUERY_TIMESTAMP,f.b)){++errors;release();return;}
  for(auto& s:f.seg)if(!create(D3D11_QUERY_TIMESTAMP,s.a)||!create(D3D11_QUERY_TIMESTAMP,s.b)){++errors;release();return;}
 }
 ready=1;
}
static void poll(){
 int oldest=-1;
 for(int i=0;i<Ring;i++)if(ring[i].pending&&tick-ring[i].tick>=3&&(oldest<0||ring[i].tick<ring[oldest].tick))oldest=i;
 if(oldest<0)return;
 auto& f=ring[oldest]; D3D11_QUERY_DATA_TIMESTAMP_DISJOINT d{};UINT64 a=0,b=0;
 auto get=[](ID3D11Query* q,void* p,UINT n){return ctx->GetData(q,p,n,D3D11_ASYNC_GETDATA_DONOTFLUSH);};
 HRESULT hr=get(f.disjoint.Get(),&d,sizeof d);
 if(hr==S_FALSE){++notReady;return;} if(FAILED(hr)){++errors;f.pending=false;return;}
 if(f.epoch!=epoch){f.pending=false;return;}
 if(d.Disjoint||!d.Frequency||f.invalid){++bad;f.pending=false;return;}
 unsigned long long values[Scopes]{},calls[Scopes]{};UINT64 starts[Segments]{},ends[Segments]{};
 hr=get(f.a.Get(),&a,sizeof a);if(hr==S_OK)hr=get(f.b.Get(),&b,sizeof b);
 if(hr==S_FALSE){++notReady;return;}if(FAILED(hr)||b<a){++errors;f.pending=false;return;}
 values[0]=static_cast<unsigned long long>(double(b-a)*1e9/d.Frequency);calls[0]=1;
 for(int j=0;j<f.used;j++){
  auto& s=f.seg[j];if(!s.closed){++bad;f.pending=false;return;}
  hr=get(s.a.Get(),&a,sizeof a);if(hr==S_OK)hr=get(s.b.Get(),&b,sizeof b);
  if(hr==S_FALSE){++notReady;return;}if(FAILED(hr)||b<a){++errors;f.pending=false;return;}
  starts[j]=a;ends[j]=b;
  values[s.id]+=static_cast<unsigned long long>(double(b-a)*1e9/d.Frequency);++calls[s.id];
 }
 unsigned long long outside=0;
 for(int j=0;j<f.used;j++)if(f.seg[j].id>=CameraEnd){
  bool inside=false;
  for(int k=0;k<f.used;k++)if(f.seg[k].id<CameraEnd&&starts[j]>=starts[k]&&ends[j]<=ends[k]){inside=true;break;}
  if(!inside)++outside;
 }
 for(int i=3;i<CameraEnd;i++){values[1]+=values[i];calls[1]+=calls[i];}
 // Signed residual is calculated by the consumer; never clamp negative nested-camera overlap.
 // A concurrent state snapshot must never make the render-thread poll wait.
 if(!TryAcquireSRWLockExclusive(&lock))return;
 outsideCamera+=outside;
 for(int i=0;i<Scopes;i++){if(calls[i])++stats[i*4];stats[i*4+1]+=values[i];stats[i*4+2]=std::max(stats[i*4+2],values[i]);stats[i*4+3]+=calls[i];}
 ++complete;ReleaseSRWLockExclusive(&lock);f.pending=false;
}
static void __stdcall event(int id,void* texture) noexcept {
 if(id==4){init(texture);return;}if(id==3){release();return;}if(!ctx)return;
 if(id==5){
  ++epoch;complete=bad=full=overflow=notReady=errors=outsideCamera=0;
  AcquireSRWLockExclusive(&lock);std::fill(std::begin(stats),std::end(stats),0);ReleaseSRWLockExclusive(&lock);return;
 }
 if(id==1){
  if(active>=0){++bad;return;}
  for(int i=0;i<Ring;i++)if(!ring[i].pending){auto& f=ring[i];active=i;f.used=0;f.invalid=false;f.tick=tick;f.epoch=epoch;ctx->Begin(f.disjoint.Get());ctx->End(f.a.Get());return;}
  ++full;return;
 }
 if(id==2){
  if(active>=0){auto& f=ring[active];ctx->End(f.b.Get());ctx->End(f.disjoint.Get());f.pending=true;active=-1;}
  ++tick;poll();return;
 }
 if(active<0)return;auto& f=ring[active];
 if(id>=1003&&id<1000+Scopes){
  if(f.used==Segments){if(!f.invalid)++overflow;f.invalid=true;return;}
  auto& s=f.seg[f.used++];s.id=id-1000;s.closed=false;ctx->End(s.a.Get());return;
 }
 if(id>=2003&&id<2000+Scopes){
  for(int j=f.used-1;j>=0;j--)if(f.seg[j].id==id-2000&&!f.seg[j].closed){ctx->End(f.seg[j].b.Get());f.seg[j].closed=true;return;}
  f.invalid=true;
 }
}
}
extern "C" __declspec(dllexport) void* gp_event(){return reinterpret_cast<void*>(&gp::event);}
extern "C" __declspec(dllexport) unsigned long long gp_status(int k){
 using namespace gp;switch(k){case 0:return ready;case 1:return complete;case 2:return bad;case 3:return full;case 4:return overflow;case 5:return notReady;case 7:return outsideCamera;default:return errors;}
}
extern "C" __declspec(dllexport) int gp_snapshot(unsigned long long* output,int length){
 if(length!=gp::Scopes*4||!output||!TryAcquireSRWLockShared(&gp::lock))return 0;
 std::copy(std::begin(gp::stats),std::end(gp::stats),output);ReleaseSRWLockShared(&gp::lock);return 1;
}
// Census only, on demand. Texture3D / other resource kinds fail QueryInterface safely.
extern "C" __declspec(dllexport) int gp_texture_info(void* resource,unsigned* output){
 if(!resource||!output)return 0;
 ComPtr<ID3D11Texture2D> texture;
 if(FAILED(static_cast<IUnknown*>(resource)->QueryInterface(IID_PPV_ARGS(&texture))))return 0;
 D3D11_TEXTURE2D_DESC d{};texture->GetDesc(&d);
 output[0]=d.Width;output[1]=d.Height;output[2]=d.MipLevels;output[3]=d.Format;output[4]=d.ArraySize;output[5]=d.SampleDesc.Count;return 1;
}
