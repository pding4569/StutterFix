// Original diagnostic fixture. MIT, StutterFix contributors.
#include "../../out/gpuprobe-safe/query.cpp"
#include <cstdio>
#include <thread>
#include <chrono>
#define CHECK(x) do{if(!(x)){std::printf("FAIL line=%d: %s\n",__LINE__,#x);return 1;}}while(0)
static void drain(ID3D11DeviceContext* c,unsigned long long expected){
 c->Flush(); // FIXTURE ONLY; the plugin uses DONOTFLUSH and never waits.
 for(int i=0;i<1000&&gp::complete<expected;i++){
  ++gp::tick;gp::poll();std::this_thread::sleep_for(std::chrono::milliseconds(1));
 }
}
int main(){
 using namespace gp;
 ComPtr<ID3D11Device> d;ComPtr<ID3D11DeviceContext> c;D3D_FEATURE_LEVEL fl;
 CHECK(SUCCEEDED(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c)));
 D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=64;td.MipLevels=td.ArraySize=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_RENDER_TARGET;
 ComPtr<ID3D11Texture2D> t;ComPtr<ID3D11RenderTargetView> r;
 CHECK(SUCCEEDED(d->CreateTexture2D(&td,nullptr,&t))&&SUCCEEDED(d->CreateRenderTargetView(t.Get(),nullptr,&r)));
 event(4,t.Get());CHECK(ready==1&&psFrames==0&&complete==0);
 float color[4]{.1f,.2f,.3f,1};
 for(int i=0;i<16;i++){
  event(1,nullptr);event(1003,nullptr);c->ClearRenderTargetView(r.Get(),color);event(2003,nullptr);event(2,nullptr);drain(c.Get(),i+1);
 }
 CHECK(complete==16&&psFrames==16&&stats[0]==16&&errors==0&&bad==0);
 // Hold a reader across multiple polls: no pipeline counter may be committed
 // while the aggregate snapshot could not be committed.
 event(1,nullptr);event(1003,nullptr);c->ClearRenderTargetView(r.Get(),color);event(2003,nullptr);event(2,nullptr);
 c->Flush();std::this_thread::sleep_for(std::chrono::milliseconds(10));
 AcquireSRWLockShared(&lock);
 for(int i=0;i<12;i++){++tick;poll();}
 auto before=psFrames.load();auto completed=complete.load();ReleaseSRWLockShared(&lock);
 CHECK(before==16&&completed==16);drain(c.Get(),17);CHECK(psFrames==17&&complete==17);
 for(int i=0;i<12;i++){++tick;poll();}CHECK(psFrames==17&&complete==17);
 // An unclosed scope is excluded as a whole frame, including pipeline stats.
 event(1,nullptr);event(1003,nullptr);event(2,nullptr);c->Flush();
 for(int i=0;i<1000&&bad==0;i++){++tick;poll();std::this_thread::sleep_for(std::chrono::milliseconds(1));}
 CHECK(bad==1&&psFrames==17&&complete==17);
 event(5,nullptr);CHECK(psFrames==0&&complete==0&&stats[0]==0);
 event(1,nullptr);event(1003,nullptr);event(3,nullptr);CHECK(ready==0&&active==-1&&!ctx&&!device);
 event(4,t.Get());CHECK(ready==1&&psFrames==0&&complete==0);event(3,nullptr);
 std::puts("GPUPROBE_SAFE_CHECK completed=17 duplicate_commits=0 blocked_publish=0 invalid_excluded=1 reset_and_active_release=ok scope=diagnostic_fixture_not_performance");return 0;
}
