#include "query.cpp"
#include <cstdio>
#include <thread>
#include <chrono>
int main(){
 ComPtr<ID3D11Device> d;ComPtr<ID3D11DeviceContext> c;
 D3D_FEATURE_LEVEL fl;
 if(FAILED(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c)))return 1;
 D3D11_TEXTURE2D_DESC td{};td.Width=3440;td.Height=1440;td.MipLevels=td.ArraySize=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_RENDER_TARGET;
 ComPtr<ID3D11Texture2D> t;ComPtr<ID3D11RenderTargetView> r;
 if(FAILED(d->CreateTexture2D(&td,nullptr,&t))||FAILED(d->CreateRenderTargetView(t.Get(),nullptr,&r)))return 2;
 event(4,nullptr);
 float color[4]{0.1f,0.2f,0.3f,1};
 for(int i=0;i<600;i++){event(1024,t.Get());c->ClearRenderTargetView(r.Get(),color);event(2,nullptr);c->Flush();std::this_thread::sleep_for(std::chrono::milliseconds(1));}
 auto n=fg_stat(0,0);auto sum=fg_stat(0,1);
 printf("samples=%llu sum_ns=%llu skipped=%llu invalid=%llu\n",n,sum,fg_stat(-1,0),fg_stat(-1,1));
 if(n<500||sum==0||fg_stat(-1,0)||fg_stat(-1,1))return 3;
 event(4,nullptr);if(fg_stat(0,0)||fg_stat(0,1))return 4;
 event(3,nullptr);puts("reset_and_shutdown=ok");return 0;
}
