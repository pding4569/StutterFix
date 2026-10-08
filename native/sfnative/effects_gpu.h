// Explicit developer-only caller. No setup, thread, hook or query until requested.
namespace effects_gpu {
struct Slot { outside::ComPtr<ID3D11Query> disjoint,start,end;bool pending=false;unsigned epoch=0; };
inline outside::ComPtr<ID3D11Device> device;
inline outside::ComPtr<ID3D11DeviceContext> context;
inline std::array<Slot,8> slots;
inline int active=-1;
inline unsigned epoch=0;
inline std::atomic<unsigned long long> samples{0},totalNs{0},maxNs{0},skipped{0},failures{0};
inline void reset(){++epoch;samples=totalNs=maxNs=skipped=failures=0;}
inline void __stdcall event(int id,void* texture) noexcept {
 try {
  if(id==3){for(auto& s:slots)s=Slot{};context.Reset();device.Reset();active=-1;return;}
  if(id==4){reset();return;}
  if(id==2){if(active>=0){auto& s=slots[static_cast<size_t>(active)];context->End(s.end.Get());context->End(s.disjoint.Get());s.pending=true;active=-1;}return;}
  if(id!=1 || texture==nullptr)return;
  if(!device){static_cast<ID3D11Texture2D*>(texture)->GetDevice(&device);device->GetImmediateContext(&context);
   for(auto& s:slots){D3D11_QUERY_DESC d{D3D11_QUERY_TIMESTAMP_DISJOINT,0};outside::check(device->CreateQuery(&d,&s.disjoint));d.Query=D3D11_QUERY_TIMESTAMP;outside::check(device->CreateQuery(&d,&s.start));outside::check(device->CreateQuery(&d,&s.end));}}
  for(auto& s:slots)if(s.pending){D3D11_QUERY_DATA_TIMESTAMP_DISJOINT d{};UINT64 a=0,b=0;
   if(context->GetData(s.disjoint.Get(),&d,sizeof(d),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK &&
      context->GetData(s.start.Get(),&a,sizeof(a),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK &&
      context->GetData(s.end.Get(),&b,sizeof(b),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK){
    s.pending=false;if(s.epoch==epoch && !d.Disjoint && d.Frequency && b>=a){auto ns=static_cast<unsigned long long>(double(b-a)*1e9/double(d.Frequency));++samples;totalNs+=ns;if(ns>maxNs.load())maxNs=ns;}
   }}
  for(size_t i=0;i<slots.size();i++)if(!slots[i].pending){active=static_cast<int>(i);auto& s=slots[i];s.epoch=epoch;context->Begin(s.disjoint.Get());context->End(s.start.Get());return;}
  ++skipped;
 }catch(...){++failures;active=-1;}
}
}
API void* sf_effects_gpu_event(){return reinterpret_cast<void*>(&effects_gpu::event);}
API unsigned long long sf_effects_gpu_stat(int index){switch(index){case 0:return effects_gpu::samples;case 1:return effects_gpu::totalNs;case 2:return effects_gpu::maxNs;case 3:return effects_gpu::skipped;default:return effects_gpu::failures;}}
