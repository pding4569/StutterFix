// Experimental, default OFF. Temporary discovery, one original game swapchain for every output.
#include "framegen.h"
#include <memory>
using namespace outside;
#define API extern "C" __declspec(dllexport)
using PresentFn=HRESULT(__stdcall*)(IDXGISwapChain*,UINT,UINT);
using ResizeFn=HRESULT(__stdcall*)(IDXGISwapChain*,UINT,UINT,UINT,DXGI_FORMAT,UINT);
static ComPtr<IDXGISwapChain> game;
static std::unique_ptr<Output> engine;
static PresentFn originalPresent=nullptr;
static ResizeFn originalResize=nullptr;
static void** presentCell=nullptr;
static void** resizeCell=nullptr;
static HWND dummy=nullptr;
static std::wstring directory;
static Packet packet{};
static bool havePacket=false;
static std::atomic<int> status{0},failure{0},stopped{0},installed{0};
// Explicit state queries can read counters safely while the render thread tears down engine.
static std::atomic<unsigned long long> sourcePresents{0},generatedPresents{0};
// Unity/render thread only; worker Presents return before these counters.
static bool diagnostics=false;
static unsigned long long syncRequested=0, syncPreserved=0, syncForcedZero=0;
static void patch(void** cell,void* value) { DWORD old=0; if(!VirtualProtect(cell,sizeof(void*),PAGE_EXECUTE_READWRITE,&old)) throw std::runtime_error("vtable protect"); InterlockedExchangePointer(cell,value); DWORD unused; VirtualProtect(cell,sizeof(void*),old,&unused); }

static HRESULT __stdcall present(IDXGISwapChain* self,UINT sync,UINT flags) {
    if(!game && dummy) {
        DXGI_SWAP_CHAIN_DESC desc{}; DWORD pid=0;
        if(SUCCEEDED(self->GetDesc(&desc)) && desc.OutputWindow!=dummy && GetWindowThreadProcessId(desc.OutputWindow,&pid) && pid==GetCurrentProcessId() && IsWindowVisible(desc.OutputWindow)) {
            game=self;
            try {
                ComPtr<ID3D11Device> d; check(self->GetDevice(IID_PPV_ARGS(&d)));
                engine=std::make_unique<Output>(); engine->start(d.Get(),self,directory,desc.BufferDesc.Width,desc.BufferDesc.Height,diagnostics);
                status=1;
            } catch(...) { failure=1; status=-1; }
        }
    }
    // Calls the ordinary captured Present chain, including Steam. No byte/RVA checks or raw DXGI dispatch.
    if(self!=game.Get() || !engine) return originalPresent(self,sync,flags);
    if(engine->isWorker()) { HRESULT hr=originalPresent(self,0,flags); if(hr==S_OK) ++generatedPresents; return hr; }
    if(status<0) { engine->endFrame(); havePacket=false; return originalPresent(self,sync,flags); }
    try {
        if(status<0 || engine->error) throw std::runtime_error("admission failed");
        if(havePacket) {
            if(packet.mode && !engine->frameHeld) { failure=6; throw std::runtime_error("source without begin event"); }
            ComPtr<ID3D11Texture2D> back;
            { ContextLock lock(engine->protection.Get()); check(game->GetBuffer(0,IID_PPV_ARGS(&back))); }
            engine->publish(packet,back.Get());

        }
        bool active=havePacket && packet.mode>=2 && packet.mode<=8;
        if(active && cameraBlendEnabled(packet)) engine->drawReal(); // Research: real and generated output share the delayed timeline.
        UINT outputSync=active?0:sync;
        if(sync) { ++syncRequested; if(active) ++syncForcedZero; else if(outputSync==sync) ++syncPreserved; }
        HRESULT hr=originalPresent(self,outputSync,flags);
        if(hr==S_OK) ++sourcePresents;
        if(havePacket) engine->recordOff(packet,hr);
        havePacket=false;
        engine->endFrame(); // EOF snapshot does not unlock: Unity's real Present must finish first.
        return hr;
    } catch(const std::exception& ex) {
        engine->trace(ex.what());
        failure=engine->error?engine->error.load():failure?failure.load():1; status=-1;
        engine->endFrame(); havePacket=false;
        engine->stop(); // A failed experiment must not leave worker Present running.
        return originalPresent(self,sync,flags);
    }
}
static void finish() {
    havePacket=false;
    if(engine) {
        engine->stop(); engine->save();
        FILE* f=nullptr; if(diagnostics) _wfopen_s(&f,(directory+L"/safety.txt").c_str(),L"wb");
        if(f) { fprintf(f,"outputs=%llu frame_begins=%llu frame_ends=%llu worker_error=%d sync_requested=%llu sync_preserved_off=%llu sync_forced_zero_active=%llu\n",engine->outputs.load(),engine->frameBegins.load(),engine->frameEnds.load(),engine->error.load(),syncRequested,syncPreserved,syncForcedZero); fclose(f); }
    }
}
static HRESULT __stdcall resize(IDXGISwapChain* self,UINT count,UINT w,UINT h,DXGI_FORMAT format,UINT flags) {
    if(self==game.Get() && engine) {
        char detail[180]; sprintf_s(detail,"ResizeBuffers requested count=%u width=%u height=%u format=%u flags=%u qpc=%.9f",count,w,h,unsigned(format),flags,now()); engine->trace(detail);
        finish(); engine.reset(); status=-1; failure=2;
    }
    return originalResize(self,count,w,h,format,flags); // No resize/recovery workaround in this experiment.
}
static void discover() {
    WNDCLASSW wc{}; wc.lpfnWndProc=DefWindowProcW; wc.hInstance=GetModuleHandleW(nullptr); wc.lpszClassName=L"StutterFix.OutsideDiscovery";
    if(!RegisterClassW(&wc) && GetLastError()!=ERROR_CLASS_ALREADY_EXISTS) throw std::runtime_error("discovery class");
    dummy=CreateWindowW(wc.lpszClassName,L"discovery",WS_OVERLAPPEDWINDOW,0,0,1,1,nullptr,nullptr,wc.hInstance,nullptr); if(!dummy) throw std::runtime_error("discovery window");
    DXGI_SWAP_CHAIN_DESC desc{}; desc.BufferDesc.Width=desc.BufferDesc.Height=1; desc.BufferDesc.Format=DXGI_FORMAT_R8G8B8A8_UNORM; desc.SampleDesc.Count=1; desc.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT; desc.BufferCount=2; desc.OutputWindow=dummy; desc.Windowed=TRUE; desc.SwapEffect=DXGI_SWAP_EFFECT_FLIP_DISCARD;
    ComPtr<IDXGISwapChain> temporary; ComPtr<ID3D11Device> d; ComPtr<ID3D11DeviceContext> c;
    check(D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&desc,&temporary,&d,nullptr,&c));
    presentCell=*reinterpret_cast<void***>(temporary.Get())+8; resizeCell=*reinterpret_cast<void***>(temporary.Get())+13;
    originalPresent=reinterpret_cast<PresentFn>(*presentCell); originalResize=reinterpret_cast<ResizeFn>(*resizeCell);
    patch(resizeCell,reinterpret_cast<void*>(resize)); patch(presentCell,reinterpret_cast<void*>(present));
}
static void __stdcall event(int id,void* data) {
    try {
        if(id==0) { discover(); return; }
        if(id==5) { if(engine && status==1 && !engine->error) engine->beginFrame(); return; }
        if(id==1 && data && status==1) { packet=*static_cast<Packet*>(data); havePacket=true; return; }
        if(id==4) { finish(); status=-2; return; }
        if(id==3) {
            finish();
            if(presentCell && *presentCell==reinterpret_cast<void*>(present)) patch(presentCell,reinterpret_cast<void*>(originalPresent));
            if(resizeCell && *resizeCell==reinterpret_cast<void*>(resize)) patch(resizeCell,reinterpret_cast<void*>(originalResize));
            engine.reset(); game.Reset(); presentCell=resizeCell=nullptr;
            if(dummy) { DestroyWindow(dummy); dummy=nullptr; } UnregisterClassW(L"StutterFix.OutsideDiscovery",GetModuleHandleW(nullptr));
            status=0; stopped=1; installed=0;
        }
    } catch(const std::exception& ex) { failure=1; status=-1; if(engine) { engine->trace(ex.what()); engine->endFrame(); } }
}
API int sf_framegen_setup(const wchar_t* path,int collect) { if(installed.exchange(1)) return 0; directory=path; diagnostics=collect!=0; sourcePresents=generatedPresents=0; syncRequested=syncPreserved=syncForcedZero=0; failure=stopped=status=0; return 1; }
API void* sf_framegen_event_ptr() { return reinterpret_cast<void*>(event); }
API int sf_framegen_status() { return status; }
API int sf_framegen_error() { return failure; }
API int sf_framegen_stopped() { return stopped; }
API int sf_framegen_installed() { return installed; }
API unsigned long long sf_framegen_sources() { return sourcePresents; }
API unsigned long long sf_framegen_generated() { return generatedPresents; }
