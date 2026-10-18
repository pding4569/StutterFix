// Offline WARP test: no game, Unity or Mods writes. Flush belongs to this test only.
#include "query.cpp"
#include <cstdio>
#include <thread>
#include <chrono>
int main(){
 using namespace gp;
 ComPtr<ID3D11Device> d;ComPtr<ID3D11DeviceContext> c;D3D_FEATURE_LEVEL fl;
 if(FAILED(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c)))return 1;
 D3D11_TEXTURE2D_DESC td{};td.Width=256;td.Height=256;td.MipLevels=td.ArraySize=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_RENDER_TARGET;
 ComPtr<ID3D11Texture2D> t;ComPtr<ID3D11RenderTargetView> r;
 if(FAILED(d->CreateTexture2D(&td,nullptr,&t))||FAILED(d->CreateRenderTargetView(t.Get(),nullptr,&r)))return 2;
 unsigned desc[6]{};if(!gp_texture_info(t.Get(),desc)||desc[0]!=256||desc[1]!=256||desc[2]!=1||desc[3]!=DXGI_FORMAT_R8G8B8A8_UNORM||desc[5]!=1)return 13;
 event(4,t.Get());if(!gp_status(0))return 3;
 float color[4]{.1f,.2f,.3f,1};
 for(int i=0;i<40;i++){
  event(1,nullptr);event(1003,nullptr);event(1035,nullptr);
  c->ClearRenderTargetView(r.Get(),color);
  event(2035,nullptr);event(2003,nullptr);event(2,nullptr);
  c->Flush();std::this_thread::sleep_for(std::chrono::milliseconds(2));
 }
 for(int i=0;i<20;i++){event(2,nullptr);c->Flush();std::this_thread::sleep_for(std::chrono::milliseconds(2));}
 unsigned long long s[Scopes*4]{};if(!gp_snapshot(s,Scopes*4))return 4;
 printf("complete=%llu frame_calls=%llu camera_calls=%llu effect_calls=%llu full=%llu invalid=%llu errors=%llu\n",gp_status(1),s[3],s[3*4+3],s[35*4+3],gp_status(3),gp_status(2),gp_status(6));
 if(s[0]!=40||s[3*4]!=40||s[35*4]!=40||s[1]<s[3*4+1]||s[3*4+1]<s[35*4+1]||s[5]!=s[3*4+1]||gp_status(2)||gp_status(3)||gp_status(6))return 5;
 // Malformed unclosed scope and overflow must discard the entire sample.
 event(1,nullptr);event(1003,nullptr);event(2,nullptr);
 event(1,nullptr);for(int i=0;i<Segments+1;i++){event(1035,nullptr);event(2035,nullptr);}event(2,nullptr);
 for(int i=0;i<20;i++){event(2,nullptr);c->Flush();std::this_thread::sleep_for(std::chrono::milliseconds(2));}
 if(gp_status(2)!=2||gp_status(4)!=1||gp_status(1)!=40)return 6;
 event(3,nullptr);if(gp_status(0)||ctx||device)return 7;
 event(4,t.Get());if(gp_status(1)||gp_status(2)||!gp_status(0))return 8;
 // Repeated camera invocation is accumulated per frame; absent work has a zero in the all-frame mean.
 event(1,nullptr);
 for(int i=0;i<2;i++){event(1003,nullptr);event(1035,nullptr);c->ClearRenderTargetView(r.Get(),color);event(2035,nullptr);event(2003,nullptr);}
 event(2,nullptr);event(1,nullptr);event(2,nullptr);
 for(int i=0;i<20;i++){event(2,nullptr);c->Flush();std::this_thread::sleep_for(std::chrono::milliseconds(2));}
 if(!gp_snapshot(s,Scopes*4)||s[0]!=2||s[3*4]!=1||s[3*4+3]!=2||s[35*4+3]!=2||s[3*4+1]!=s[3*4+2]||gp_status(7))return 10;
 // Reset must not reuse unresolved queries or allow an old epoch into the new window.
 event(1,nullptr);event(1003,nullptr);event(2003,nullptr);event(2,nullptr);event(5,nullptr);
 for(int i=0;i<20;i++){event(2,nullptr);c->Flush();std::this_thread::sleep_for(std::chrono::milliseconds(2));}
 if(!gp_snapshot(s,Scopes*4)||s[0]!=0||gp_status(1))return 12;
 event(3,nullptr);event(4,t.Get());
 // Fill ring without allowing an age of three: no pending query may be reused.
 for(int i=0;i<Ring;i++){event(1,nullptr);event(2,nullptr);ring[i].tick=tick;}
 // Explicitly keep all slots pending, independent of software-driver completion speed.
 for(auto& f:ring){f.pending=true;f.tick=tick;}
 event(1,nullptr);if(gp_status(3)!=1)return 9;
 event(3,nullptr);event(4,nullptr);if(gp_status(0)||gp_status(6)!=1)return 11;
 puts("nested_timestamps/repeated_camera/zero_work_frame/discard/overflow/reset/pending_ring/shutdown/null_init=ok");return 0;
}
