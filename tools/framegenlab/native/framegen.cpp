// Research-only addition to sfnative. Normal native/build-sfnative.bat does not compile this file.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d11_1.h>
#include <dxgi.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <array>
#include <vector>
#include <string>
#include <stdexcept>
#include <cstdio>
#include <cmath>
#include <limits>
using Microsoft::WRL::ComPtr;
#define API extern "C" __declspec(dllexport)
struct Packet { void *scene,*black,*white; float x,y,size,angle; int real,frame,mode,measure; double seconds; int flip,linear,capture; double song; };
static_assert(sizeof(Packet)==88,"Managed/native packet layout must match");
struct Row { double time,gpu,song; int real,frame; HRESULT hr; };
struct Query { ComPtr<ID3D11Query> start,end,disjoint; size_t row=SIZE_MAX; bool begun=false; };
static ComPtr<IDXGISwapChain> sc;
static ComPtr<ID3D11Device> dev;
static ComPtr<ID3D11DeviceContext1> ctx;
static ComPtr<ID3DDeviceContextState> ownState;
static ComPtr<ID3D11Texture2D> source,screen;
static ComPtr<ID3D11ShaderResourceView> sourceView,screenView;
static ComPtr<ID3D11ShaderResourceView> blackView,whiteView;
static void *lastBlack=nullptr,*lastWhite=nullptr;
static ComPtr<ID3D11RenderTargetView> backView;
static ComPtr<ID3D11Texture2D> viewedBack;
static ComPtr<ID3D11VertexShader> vs;
static ComPtr<ID3D11PixelShader> ps;
static ComPtr<ID3D11Buffer> cb;
static ComPtr<ID3D11SamplerState> sampler;
static ComPtr<ID3D11RasterizerState> raster;
static ComPtr<ID3D11DepthStencilState> depth;
static std::array<Query,128> queries;
static size_t nextQuery=0;
static Query* activeQuery=nullptr;
static std::vector<Row> rows;
static Packet latest{},saved{};
static bool havePacket=false;
static volatile LONG ready=0;
static LARGE_INTEGER frequency;
static std::wstring logPath;
static volatile LONG stopped=0,errorCode=0;
static volatile LONG installed=0;
using PresentFn=HRESULT(__stdcall*)(IDXGISwapChain*,UINT,UINT);
using ResizeFn=HRESULT(__stdcall*)(IDXGISwapChain*,UINT,UINT,UINT,DXGI_FORMAT,UINT);
static PresentFn originalPresent=nullptr;
static ResizeFn originalResize=nullptr;
static void** presentCell=nullptr;
static void** resizeCell=nullptr;
static HWND dummyWindow=nullptr;
static void init();
static void check(HRESULT hr) { if(FAILED(hr)) { InterlockedExchange(&errorCode,hr); throw std::runtime_error("D3D11 failure"); } }
static double clockNow() { LARGE_INTEGER q; QueryPerformanceCounter(&q); return double(q.QuadPart)/frequency.QuadPart; }
static void patch(void** cell,void* value) { DWORD old; if(!VirtualProtect(cell,sizeof(void*),PAGE_EXECUTE_READWRITE,&old)) throw std::runtime_error("vtable protect failed"); InterlockedExchangePointer(cell,value); DWORD unused; VirtualProtect(cell,sizeof(void*),old,&unused); }
static HRESULT __stdcall present(IDXGISwapChain* self,UINT sync,UINT flags)
{
    if(!sc && dummyWindow) {
        DXGI_SWAP_CHAIN_DESC d{}; DWORD pid=0;
        if(SUCCEEDED(self->GetDesc(&d)) && d.OutputWindow!=dummyWindow && GetWindowThreadProcessId(d.OutputWindow,&pid) && pid==GetCurrentProcessId() && IsWindowVisible(d.OutputWindow)) {
            sc=self; try { init(); } catch(...) { InterlockedExchange(&errorCode,1); }
        }
    }
    if(self!=sc.Get()) return originalPresent(self,sync,flags);
    HRESULT hr=originalPresent(self,0,flags); // One output per Unity tick; no duplicate extra Present.
    if(havePacket && latest.measure && rows.size()<100000) {
        rows.push_back({clockNow(),std::numeric_limits<double>::quiet_NaN(),latest.song,latest.real,latest.frame,hr});
        if(activeQuery) { activeQuery->row=rows.size()-1; activeQuery=nullptr; }
    } else if(activeQuery) { activeQuery->row=SIZE_MAX; activeQuery=nullptr; }
    return hr;
}
static HRESULT __stdcall resize(IDXGISwapChain* self,UINT count,UINT width,UINT height,DXGI_FORMAT format,UINT flags) {
    if(self==sc.Get() && ctx && ownState) { ComPtr<ID3DDeviceContextState> previous; ctx->SwapDeviceContextState(ownState.Get(),&previous); ctx->OMSetRenderTargets(0,nullptr,nullptr); ctx->SwapDeviceContextState(previous.Get(),nullptr); backView.Reset(); viewedBack.Reset(); }
    return originalResize(self,count,width,height,format,flags);
}
static DXGI_FORMAT rawFormat(DXGI_FORMAT f) {
    if(f==DXGI_FORMAT_R8G8B8A8_TYPELESS || f==DXGI_FORMAT_R8G8B8A8_UNORM_SRGB) return DXGI_FORMAT_R8G8B8A8_UNORM;
    if(f==DXGI_FORMAT_B8G8R8A8_TYPELESS || f==DXGI_FORMAT_B8G8R8A8_UNORM_SRGB) return DXGI_FORMAT_B8G8R8A8_UNORM;
    if(f==DXGI_FORMAT_R16G16B16A16_TYPELESS) return DXGI_FORMAT_R16G16B16A16_FLOAT;
    return f;
}
static void copyTexture(ID3D11Texture2D* input,ComPtr<ID3D11Texture2D>& output,ComPtr<ID3D11ShaderResourceView>& view) {
    D3D11_TEXTURE2D_DESC d{}; input->GetDesc(&d);
    if(output) { D3D11_TEXTURE2D_DESC old{}; output->GetDesc(&old); if(old.Width!=d.Width || old.Height!=d.Height || rawFormat(old.Format)!=rawFormat(d.Format)) { view.Reset(); output.Reset(); } }
    if(!output) {
        d.BindFlags=D3D11_BIND_SHADER_RESOURCE; d.Usage=D3D11_USAGE_DEFAULT; d.CPUAccessFlags=d.MiscFlags=0;
        if(rawFormat(d.Format)==DXGI_FORMAT_R8G8B8A8_UNORM) d.Format=DXGI_FORMAT_R8G8B8A8_TYPELESS;
        if(rawFormat(d.Format)==DXGI_FORMAT_B8G8R8A8_UNORM) d.Format=DXGI_FORMAT_B8G8R8A8_TYPELESS;
        check(dev->CreateTexture2D(&d,nullptr,&output)); D3D11_SHADER_RESOURCE_VIEW_DESC s{};
        s.Format=rawFormat(d.Format); s.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D; s.Texture2D.MipLevels=1;
        check(dev->CreateShaderResourceView(output.Get(),&s,&view));
    }
    ctx->CopyResource(output.Get(),input);
}
static const char* shader=R"(
cbuffer Data : register(b0) { float4 oldCamera; float4 camera; float4 info; };
Texture2D worldTex:register(t0); Texture2D uiTex:register(t1); Texture2D blackTex:register(t2); Texture2D whiteTex:register(t3);
SamplerState sampleLinear:register(s0);
struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
V VS(uint id:SV_VertexID) { V o; o.uv=float2((id<<1)&2,id&2); o.pos=float4(o.uv*float2(2,-2)+float2(-1,1),0,1); return o; }
float2 rotate(float2 p,float a) { float s,c; sincos(a,s,c); return float2(c*p.x-s*p.y,s*p.x+c*p.y); }
float3 decode(float3 c) { return lerp(c/12.92,pow((max(c,0)+.055)/1.055,2.4),step(.04045,c)); }
float3 encode(float3 c) { return lerp(c*12.92,1.055*pow(max(c,0),1/2.4)-.055,step(.0031308,c)); }
float4 PS(V i):SV_TARGET {
    float2 uv=i.uv;
    float2 p=float2((uv.x-.5)*info.x,(.5-uv.y))*2*camera.z;
    p=rotate(p,camera.w)+camera.xy;
    p=rotate(p-oldCamera.xy,-oldCamera.w)/(2*oldCamera.z);
    float2 sourceUV=float2(.5+p.x/info.x,.5-p.y);
    float2 sameUV=uv;
    if(info.y>.5) { sourceUV.y=1-sourceUV.y; sameUV.y=1-sameUV.y; }
    float3 world=worldTex.SampleLevel(sampleLinear,sourceUV,0).rgb;
    float3 oldWorld=worldTex.SampleLevel(sampleLinear,sameUV,0).rgb;
    float3 oldScreen=uiTex.SampleLevel(sampleLinear,uv,0).rgb;
    float2 planetUV=uv; if(info.z>.5) planetUV.y=1-planetUV.y;
    float3 b=blackTex.SampleLevel(sampleLinear,planetUV,0).rgb;
    float3 w=whiteTex.SampleLevel(sampleLinear,planetUV,0).rgb;
    float3 transmission=saturate(w-b); // Two backgrounds preserve alpha/additive sprites without guessing alpha.
    float3 result=info.w>.5 ? encode(b+decode(world)*transmission) : b+world*transmission;
    float difference=max(max(abs(oldScreen.r-oldWorld.r),abs(oldScreen.g-oldWorld.g)),abs(oldScreen.b-oldWorld.b));
    if(difference>2.5/255) result=oldScreen; // Authorized fallback: frozen real-frame UI pixels, including translucent background.
    return float4(result,1);
})";
static ComPtr<ID3DBlob> compile(const char* entry,const char* target) { ComPtr<ID3DBlob> code,errors; check(D3DCompile(shader,strlen(shader),nullptr,nullptr,nullptr,entry,target,D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&errors)); return code; }
static void init() {
    check(sc->GetDevice(IID_PPV_ARGS(&dev))); ComPtr<ID3D11DeviceContext> base; dev->GetImmediateContext(&base); check(base.As(&ctx));
    ComPtr<ID3D11Device1> d1; check(dev.As(&d1)); D3D_FEATURE_LEVEL level=dev->GetFeatureLevel(),used;
    check(d1->CreateDeviceContextState(0,&level,1,D3D11_SDK_VERSION,__uuidof(ID3D11Device),&used,&ownState));
    auto v=compile("VS","vs_5_0"),p=compile("PS","ps_5_0"); check(dev->CreateVertexShader(v->GetBufferPointer(),v->GetBufferSize(),nullptr,&vs)); check(dev->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&ps));
    D3D11_BUFFER_DESC bd{}; bd.ByteWidth=48; bd.Usage=D3D11_USAGE_DEFAULT; bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER; check(dev->CreateBuffer(&bd,nullptr,&cb));
    D3D11_SAMPLER_DESC sd{}; sd.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR; sd.AddressU=sd.AddressV=sd.AddressW=D3D11_TEXTURE_ADDRESS_BORDER; sd.MaxLOD=D3D11_FLOAT32_MAX; check(dev->CreateSamplerState(&sd,&sampler));
    D3D11_RASTERIZER_DESC rd{}; rd.FillMode=D3D11_FILL_SOLID; rd.CullMode=D3D11_CULL_NONE; rd.DepthClipEnable=TRUE; check(dev->CreateRasterizerState(&rd,&raster));
    D3D11_DEPTH_STENCIL_DESC dd{}; check(dev->CreateDepthStencilState(&dd,&depth));
    for(auto& q:queries) { D3D11_QUERY_DESC a{D3D11_QUERY_TIMESTAMP,0}; check(dev->CreateQuery(&a,&q.start)); check(dev->CreateQuery(&a,&q.end)); a.Query=D3D11_QUERY_TIMESTAMP_DISJOINT; check(dev->CreateQuery(&a,&q.disjoint)); }
    rows.reserve(100000); QueryPerformanceFrequency(&frequency);
    ready=true;
}
static void discover() {
    WNDCLASSW wc{}; wc.lpfnWndProc=DefWindowProcW; wc.hInstance=GetModuleHandleW(nullptr); wc.lpszClassName=L"StutterFix.FrameGenDiscovery";
    if(!RegisterClassW(&wc) && GetLastError()!=ERROR_CLASS_ALREADY_EXISTS) throw std::runtime_error("window class failed");
    dummyWindow=CreateWindowW(wc.lpszClassName,L"FrameGen discovery",WS_OVERLAPPEDWINDOW,0,0,1,1,nullptr,nullptr,wc.hInstance,nullptr);
    if(!dummyWindow) throw std::runtime_error("discovery window failed");
    DXGI_SWAP_CHAIN_DESC d{}; d.BufferDesc.Width=d.BufferDesc.Height=1; d.BufferDesc.Format=DXGI_FORMAT_R8G8B8A8_UNORM; d.SampleDesc.Count=1;
    d.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT; d.BufferCount=2; d.OutputWindow=dummyWindow; d.Windowed=TRUE; d.SwapEffect=DXGI_SWAP_EFFECT_FLIP_DISCARD;
    ComPtr<IDXGISwapChain> temporary; ComPtr<ID3D11Device> device; ComPtr<ID3D11DeviceContext> context;
    check(D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&temporary,&device,nullptr,&context));
    presentCell=(*reinterpret_cast<void***>(temporary.Get()))+8; originalPresent=reinterpret_cast<PresentFn>(*presentCell);
    resizeCell=(*reinterpret_cast<void***>(temporary.Get()))+13; originalResize=reinterpret_cast<ResizeFn>(*resizeCell);
    patch(resizeCell,reinterpret_cast<void*>(resize));
    patch(presentCell,reinterpret_cast<void*>(present));
}
static void collect(Query& q) {
    if(q.row==SIZE_MAX) return; D3D11_QUERY_DATA_TIMESTAMP_DISJOINT d{}; UINT64 a,b;
    if(ctx->GetData(q.disjoint.Get(),&d,sizeof(d),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK || ctx->GetData(q.start.Get(),&a,sizeof(a),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK || ctx->GetData(q.end.Get(),&b,sizeof(b),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK) return;
    if(!d.Disjoint && d.Frequency && b>=a) rows[q.row].gpu=double(b-a)*1000/d.Frequency; q.row=SIZE_MAX;
}
// Opt-in visual smoke test only. Never enabled in performance runs.
static void picture(ID3D11Texture2D* texture,const wchar_t* name) {
    D3D11_TEXTURE2D_DESC d{}; texture->GetDesc(&d); DXGI_FORMAT f=rawFormat(d.Format);
    bool half=f==DXGI_FORMAT_R16G16B16A16_FLOAT;
    if(!half && f!=DXGI_FORMAT_R8G8B8A8_UNORM && f!=DXGI_FORMAT_B8G8R8A8_UNORM) return;
    d.Usage=D3D11_USAGE_STAGING; d.BindFlags=d.MiscFlags=0; d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Texture2D> staging; check(dev->CreateTexture2D(&d,nullptr,&staging)); ctx->CopyResource(staging.Get(),texture);
    D3D11_MAPPED_SUBRESOURCE mapped{}; check(ctx->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped));
    FILE* out=nullptr; auto path=logPath.substr(0,logPath.find_last_of(L"/\\")+1)+name; _wfopen_s(&out,path.c_str(),L"wb");
    double maximum=0;
    if(out) { fprintf(out,"P6\n%u %u\n255\n",d.Width,d.Height); std::vector<unsigned char> line(d.Width*3);
        for(UINT y=0;y<d.Height;y++) { auto p=static_cast<unsigned char*>(mapped.pData)+size_t(y)*mapped.RowPitch;
            for(UINT x=0;x<d.Width;x++) { if(half) { auto values=reinterpret_cast<const unsigned short*>(p+x*8); for(int c=0;c<3;c++) { unsigned h=values[c],e=(h>>10)&31,m=h&1023; double value=e?ldexp(1.0+double(m)/1024,int(e)-15):ldexp(double(m),-24); if(h&32768) value=-value; maximum=fmax(maximum,value); line[x*3+c]=static_cast<unsigned char>(fmin(255.,fmax(0.,value*255.))); } }
                else { line[x*3]=p[x*4+(f==DXGI_FORMAT_B8G8R8A8_UNORM?2:0)]; line[x*3+1]=p[x*4+1]; line[x*3+2]=p[x*4+(f==DXGI_FORMAT_B8G8R8A8_UNORM?0:2)]; } } fwrite(line.data(),1,line.size(),out); } fclose(out); }
    ctx->Unmap(staging.Get(),0);
    if(half) { FILE* meta=nullptr; _wfopen_s(&meta,(path+L".range.txt").c_str(),L"wb"); if(meta) { fprintf(meta,"HDR channel maximum=%.9f\n",maximum); fclose(meta); } }
}
static void draw(Packet& packet) {
    ComPtr<ID3D11Texture2D> back; check(sc->GetBuffer(0,IID_PPV_ARGS(&back)));
    if(packet.real) { copyTexture(static_cast<ID3D11Texture2D*>(packet.scene),source,sourceView); copyTexture(back.Get(),screen,screenView); saved=packet; }
    if(!source || !screen) return;
    ComPtr<ID3DDeviceContextState> previous; ctx->SwapDeviceContextState(ownState.Get(),&previous);
    try {
        if(viewedBack.Get()!=back.Get()) { backView.Reset(); viewedBack=back; D3D11_TEXTURE2D_DESC bd{}; back->GetDesc(&bd); D3D11_RENDER_TARGET_VIEW_DESC vd{}; vd.Format=rawFormat(bd.Format); vd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D; check(dev->CreateRenderTargetView(back.Get(),&vd,&backView)); }
        D3D11_SHADER_RESOURCE_VIEW_DESC sv{}; sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D; sv.Texture2D.MipLevels=1;
        if(lastBlack!=packet.black || lastWhite!=packet.white) { blackView.Reset(); whiteView.Reset(); D3D11_TEXTURE2D_DESC t{}; static_cast<ID3D11Texture2D*>(packet.black)->GetDesc(&t); sv.Format=rawFormat(t.Format); check(dev->CreateShaderResourceView(static_cast<ID3D11Texture2D*>(packet.black),&sv,&blackView)); static_cast<ID3D11Texture2D*>(packet.white)->GetDesc(&t); sv.Format=rawFormat(t.Format); check(dev->CreateShaderResourceView(static_cast<ID3D11Texture2D*>(packet.white),&sv,&whiteView)); lastBlack=packet.black; lastWhite=packet.white; }
        D3D11_TEXTURE2D_DESC bd{}; back->GetDesc(&bd); float data[12]={saved.x,saved.y,saved.size,saved.angle,packet.x,packet.y,packet.size,packet.angle,float(bd.Width)/bd.Height,float(packet.flip),float(packet.flip),float(packet.linear)};
        ctx->UpdateSubresource(cb.Get(),0,nullptr,data,0,0); ID3D11Buffer* buffer=cb.Get(); ctx->PSSetConstantBuffers(0,1,&buffer);
        ID3D11ShaderResourceView* views[]={sourceView.Get(),screenView.Get(),blackView.Get(),whiteView.Get()}; ctx->PSSetShaderResources(0,4,views);
        auto sam=sampler.Get(); ctx->PSSetSamplers(0,1,&sam); auto rt=backView.Get(); ctx->OMSetRenderTargets(1,&rt,nullptr);
        D3D11_VIEWPORT vp{0,0,float(bd.Width),float(bd.Height),0,1}; ctx->RSSetViewports(1,&vp); ctx->RSSetState(raster.Get());
        ctx->OMSetBlendState(nullptr,nullptr,0xffffffff); ctx->OMSetDepthStencilState(depth.Get(),0); ctx->IASetInputLayout(nullptr); ctx->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        ctx->VSSetShader(vs.Get(),nullptr,0); ctx->PSSetShader(ps.Get(),nullptr,0); ctx->GSSetShader(nullptr,nullptr,0); ctx->HSSetShader(nullptr,nullptr,0); ctx->DSSetShader(nullptr,nullptr,0); ctx->Draw(3,0);
    } catch(...) { ctx->SwapDeviceContextState(previous.Get(),nullptr); throw; }
    ctx->SwapDeviceContextState(previous.Get(),nullptr);
    if(packet.capture==1) { picture(source.Get(),L"world.ppm"); picture(screen.Get(),L"ui-screen.ppm"); picture(static_cast<ID3D11Texture2D*>(packet.black),L"planets.ppm"); picture(static_cast<ID3D11Texture2D*>(packet.white),L"planets-white.ppm"); picture(back.Get(),L"real-output.ppm"); }
    if(packet.capture==2) { picture(back.Get(),L"generated-output.ppm"); FILE* out=nullptr; auto path=logPath+L".pose.txt"; _wfopen_s(&out,path.c_str(),L"wb"); if(out) { fprintf(out,"source=%.6f %.6f %.6f %.6f current=%.6f %.6f %.6f %.6f\n",saved.x,saved.y,saved.size,saved.angle,packet.x,packet.y,packet.size,packet.angle); fclose(out); } }
}
static void dump() {
    ctx->Flush(); double end=clockNow()+2; while(clockNow()<end) { bool pending=false; for(auto& q:queries) { collect(q); pending|=q.row!=SIZE_MAX; } if(!pending) break; Sleep(1); }
    FILE* f=nullptr; _wfopen_s(&f,logPath.c_str(),L"wb"); if(f) { fprintf(f,"present_s,real,unity_frame,gpu_ms,hr,song_s\n"); for(auto& r:rows) fprintf(f,"%.9f,%d,%d,%.6f,%ld,%.9f\n",r.time,r.real,r.frame,r.gpu,long(r.hr),r.song); fclose(f); }
}
static void shutdown() {
    if(presentCell && *presentCell==reinterpret_cast<void*>(present)) patch(presentCell,reinterpret_cast<void*>(originalPresent));
    if(resizeCell && *resizeCell==reinterpret_cast<void*>(resize)) patch(resizeCell,reinterpret_cast<void*>(originalResize));
    havePacket=false; ready=false; source.Reset(); screen.Reset(); sourceView.Reset(); screenView.Reset(); blackView.Reset(); whiteView.Reset(); lastBlack=lastWhite=nullptr; backView.Reset(); ownState.Reset(); vs.Reset(); ps.Reset(); cb.Reset(); sampler.Reset(); raster.Reset(); depth.Reset();
    for(auto& q:queries) q=Query{}; viewedBack.Reset(); ctx.Reset(); dev.Reset(); sc.Reset(); presentCell=nullptr;
    resizeCell=nullptr; if(dummyWindow) { DestroyWindow(dummyWindow); dummyWindow=nullptr; } UnregisterClassW(L"StutterFix.FrameGenDiscovery",GetModuleHandleW(nullptr)); InterlockedExchange(&stopped,1); InterlockedExchange(&installed,0);
}
static void __stdcall event(int id,void* ptr) {
    try {
        if(id==0) { discover(); return; }
        if(id==4) { havePacket=false; if(ready) dump(); return; }
        if(id==3) { shutdown(); return; }
        if(!ready || !ptr) return;
        Packet packet=*static_cast<Packet*>(ptr);
        if(id==2) {
            for(auto& q:queries) collect(q);
            if(!packet.real && packet.measure) { auto& q=queries[nextQuery++%queries.size()]; if(q.row==SIZE_MAX) { ctx->Begin(q.disjoint.Get()); ctx->End(q.start.Get()); q.begun=true; activeQuery=&q; } }
            return;
        }
        if(id==1) {
            if(packet.mode && packet.scene && packet.black && packet.white) draw(packet);
            if(activeQuery && activeQuery->begun) { ctx->End(activeQuery->end.Get()); ctx->End(activeQuery->disjoint.Get()); activeQuery->begun=false; }
            latest=packet; havePacket=true;
        }
    } catch(...) { if(!errorCode) InterlockedExchange(&errorCode,1); }
}
API int sf_fg_setup(void* chain,const wchar_t* path) { if(InterlockedCompareExchange(&installed,1,0)) return 0; sc=static_cast<IDXGISwapChain*>(chain); logPath=path; rows.clear(); nextQuery=0; activeQuery=nullptr; havePacket=false; ready=0; stopped=errorCode=0; return 1; }
API void* sf_fg_event_ptr() { return reinterpret_cast<void*>(event); }
API int sf_fg_status() { return errorCode ? -1 : (ready ? 1 : 0); }
API int sf_fg_error() { return errorCode; }
API int sf_fg_stopped() { return stopped; }
