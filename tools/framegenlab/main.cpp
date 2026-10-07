#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_3.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <fstream>
#include <iomanip>
#include <stdexcept>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;
static void check(HRESULT hr, const char* name) {
    if (FAILED(hr)) { char s[160]; snprintf(s,sizeof(s),"%s failed: 0x%08lX",name,static_cast<unsigned long>(hr)); throw std::runtime_error(s); }
}
struct Handle {
    HANDLE h = nullptr;
    ~Handle() { if (h) CloseHandle(h); }
};
struct State { float source[4], current[4], clock[4], flags[4]; };
struct Record { double time, interval, lateness, draw, present, sourceAge, qpcStart; int real, slot; UINT presentId; };
struct DisplayRecord { UINT present, refresh, sync; LONGLONG qpc; double observed; };
static double frequency;
static double now() { LARGE_INTEGER t; QueryPerformanceCounter(&t); return static_cast<double>(t.QuadPart)/frequency; }
static bool pump() {
    MSG m;
    while (PeekMessageW(&m,nullptr,0,0,PM_REMOVE)) {
        if (m.message == WM_QUIT) return false;
        TranslateMessage(&m); DispatchMessageW(&m);
    }
    return true;
}
static LRESULT CALLBACK wndproc(HWND h, UINT m, WPARAM w, LPARAM l) {
    if (m == WM_CLOSE || (m == WM_KEYDOWN && w == VK_ESCAPE)) { DestroyWindow(h); return 0; }
    if (m == WM_DESTROY) { PostQuitMessage(0); return 0; }
    return DefWindowProcW(h,m,w,l);
}
struct Window { HWND h = nullptr; ~Window() { if (h && IsWindow(h)) DestroyWindow(h); } };
static void camera(double t, float* c) {
    c[0] = static_cast<float>(t*2.5-0.4); c[1] = 0.8f*std::sin(static_cast<float>(t)*0.9f);
    c[2] = 1.0f+0.07f*std::sin(static_cast<float>(t)*3.0f);
    c[3] = 0.07f*std::sin(static_cast<float>(t)*1.1f);
}
static ComPtr<ID3DBlob> shader(const wchar_t* path, const char* entry, const char* profile) {
    ComPtr<ID3DBlob> code, errors;
    HRESULT hr = D3DCompileFromFile(path,nullptr,nullptr,entry,profile,D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&errors);
    if (FAILED(hr) && errors) throw std::runtime_error(static_cast<const char*>(errors->GetBufferPointer()));
    check(hr,"D3DCompileFromFile"); return code;
}
static void capture(ID3D11Device* dev, ID3D11DeviceContext* ctx, ID3D11Texture2D* texture, const std::wstring& path) {
    D3D11_TEXTURE2D_DESC d; texture->GetDesc(&d);
    d.Usage = D3D11_USAGE_STAGING; d.BindFlags = 0; d.CPUAccessFlags = D3D11_CPU_ACCESS_READ; d.MiscFlags = 0;
    ComPtr<ID3D11Texture2D> staging; check(dev->CreateTexture2D(&d,nullptr,&staging),"staging");
    ctx->CopyResource(staging.Get(),texture);
    D3D11_MAPPED_SUBRESOURCE mapped; check(ctx->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped),"capture map");
    FILE* f = nullptr; _wfopen_s(&f,path.c_str(),L"wb");
    if (!f) { ctx->Unmap(staging.Get(),0); throw std::runtime_error("Cannot write capture"); }
    // PPM avoids an image library; test-only readback occurs after the timed run.
    fprintf(f,"P6\n%u %u\n255\n",d.Width,d.Height);
    for (UINT y=0;y<d.Height;++y) {
        auto row = static_cast<const unsigned char*>(mapped.pData)+y*mapped.RowPitch;
        for (UINT x=0;x<d.Width;++x) fwrite(row+x*4,1,3,f);
    }
    fclose(f); ctx->Unmap(staging.Get(),0);
}
int wmain(int argc, wchar_t** argv) {
    try {
        double hz = 0, base = 60, seconds = 20; int multiple = 2, mode = 2, width = 1280, height = 720;
        bool borderless = false; std::wstring csv = L"frames.csv", shot;
        for (int i=1;i<argc;++i) {
            std::wstring a = argv[i];
            if (a == L"--borderless") { borderless = true; continue; }
            if (a == L"--help") { puts("FrameGenLab --multiplier 2|3|4 --base 60 (0 = hz / multiplier) --hz 0 --seconds 20 --mode none|reproj|hybrid --csv frames.csv --capture image.ppm --borderless --width 1280 --height 720"); return 0; }
            if (++i >= argc) throw std::runtime_error("Missing option value");
            if (a == L"--multiplier") multiple = std::stoi(argv[i]);
            else if (a == L"--base") base = std::stod(argv[i]);
            else if (a == L"--hz") hz = std::stod(argv[i]);
            else if (a == L"--seconds") seconds = std::stod(argv[i]);
            else if (a == L"--mode") { std::wstring v = argv[i]; if (v!=L"none" && v!=L"reproj" && v!=L"hybrid") throw std::runtime_error("Invalid mode"); mode = v==L"none" ? 0 : v==L"reproj" ? 1 : 2; }
            else if (a == L"--csv") csv = argv[i];
            else if (a == L"--capture") shot = argv[i];
            else if (a == L"--width") width = std::stoi(argv[i]);
            else if (a == L"--height") height = std::stoi(argv[i]);
            else throw std::runtime_error("Unknown option");
        }
        if (multiple<2 || multiple>4 || !std::isfinite(base) || base<0 || !std::isfinite(hz) || hz<0 || !std::isfinite(seconds) || seconds<=0 || seconds>600 || width<64 || height<64 || width>8192 || height>8192)
            throw std::runtime_error("Invalid numeric options");
        SetProcessDPIAware();
        POINT pt{0,0}; HMONITOR monitor = MonitorFromPoint(pt,MONITOR_DEFAULTTOPRIMARY);
        MONITORINFOEXW mi{}; mi.cbSize = sizeof(mi); GetMonitorInfoW(monitor,&mi);
        DEVMODEW dm{}; dm.dmSize = sizeof(dm); if (!EnumDisplaySettingsW(mi.szDevice,ENUM_CURRENT_SETTINGS,&dm)) throw std::runtime_error("Display mode unavailable");
        double monitorHz = dm.dmDisplayFrequency;
        if (monitorHz<10) throw std::runtime_error("Monitor refresh unavailable");
        if (hz==0) hz = monitorHz;
        hz = std::min(hz,monitorHz);
        if (base==0) base = hz/multiple;
        if (base<1 || base>1000) throw std::runtime_error("base must be 1..1000 FPS");
        double target = std::min(mode==0 ? base : base*multiple,hz);
        if (borderless) { width = mi.rcMonitor.right-mi.rcMonitor.left; height = mi.rcMonitor.bottom-mi.rcMonitor.top; }
        LARGE_INTEGER f; QueryPerformanceFrequency(&f); frequency = static_cast<double>(f.QuadPart);
        WNDCLASSW wc{}; wc.lpfnWndProc = wndproc; wc.hInstance = GetModuleHandleW(nullptr); wc.lpszClassName = L"FrameGenLab"; wc.hCursor = LoadCursorW(nullptr,IDC_ARROW);
        if (!RegisterClassW(&wc)) throw std::runtime_error("RegisterClass failed");
        DWORD style = borderless ? WS_POPUP : WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU|WS_MINIMIZEBOX;
        RECT r{0,0,width,height}; AdjustWindowRect(&r,style,FALSE);
        Window window;
        window.h = CreateWindowW(wc.lpszClassName,L"FrameGenLab - synthetic camera + planets - ESC exits",style,mi.rcMonitor.left,mi.rcMonitor.top,r.right-r.left,r.bottom-r.top,nullptr,nullptr,wc.hInstance,nullptr);
        if (!window.h) throw std::runtime_error("CreateWindow failed");
        ComPtr<ID3D11Device> dev; ComPtr<ID3D11DeviceContext> ctx;
        check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&dev,nullptr,&ctx),"D3D11CreateDevice");
        ComPtr<IDXGIDevice> dxdev; check(dev.As(&dxdev),"DXGI device");
        ComPtr<IDXGIAdapter> adapter; check(dxdev->GetAdapter(&adapter),"adapter");
        DXGI_ADAPTER_DESC ad{}; check(adapter->GetDesc(&ad),"adapter description");
        printf("process_id=%lu adapter_vendor=0x%04X device=0x%04X dedicated_vram_mb=%zu\n",GetCurrentProcessId(),ad.VendorId,ad.DeviceId,ad.DedicatedVideoMemory/1048576);
        ComPtr<IDXGIFactory2> factory; check(adapter->GetParent(IID_PPV_ARGS(&factory)),"factory");
        DXGI_SWAP_CHAIN_DESC1 sd{}; sd.Width = static_cast<UINT>(width); sd.Height = static_cast<UINT>(height); sd.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        sd.SampleDesc.Count = 1; sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT; sd.BufferCount = 2;
        sd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD; sd.Flags = DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT;
        ComPtr<IDXGISwapChain1> sc; check(factory->CreateSwapChainForHwnd(dev.Get(),window.h,&sd,nullptr,nullptr,&sc),"swapchain");
        check(factory->MakeWindowAssociation(window.h,DXGI_MWA_NO_ALT_ENTER),"window association");
        ComPtr<IDXGISwapChain2> sc2; check(sc.As(&sc2),"swapchain2"); check(sc2->SetMaximumFrameLatency(1),"frame latency");
        Handle latency; latency.h = sc2->GetFrameLatencyWaitableObject(); if (!latency.h) throw std::runtime_error("No latency waitable object");
        ComPtr<ID3D11Texture2D> back; check(sc->GetBuffer(0,IID_PPV_ARGS(&back)),"backbuffer");
        ComPtr<ID3D11RenderTargetView> backView; check(dev->CreateRenderTargetView(back.Get(),nullptr,&backView),"back view");
        const float overscan = 1.15f;
        D3D11_TEXTURE2D_DESC td{}; td.Width = static_cast<UINT>(width*overscan); td.Height = static_cast<UINT>(height*overscan);
        td.MipLevels = td.ArraySize = 1; td.Format = sd.Format; td.SampleDesc.Count = 1; td.Usage = D3D11_USAGE_DEFAULT;
        td.BindFlags = D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
        ComPtr<ID3D11Texture2D> source; check(dev->CreateTexture2D(&td,nullptr,&source),"source");
        ComPtr<ID3D11RenderTargetView> sourceView; check(dev->CreateRenderTargetView(source.Get(),nullptr,&sourceView),"source RTV");
        ComPtr<ID3D11ShaderResourceView> sourceSrv; check(dev->CreateShaderResourceView(source.Get(),nullptr,&sourceSrv),"source SRV");
        wchar_t exe[MAX_PATH]; GetModuleFileNameW(nullptr,exe,MAX_PATH);
        std::wstring shaderPath = exe; shaderPath = shaderPath.substr(0,shaderPath.find_last_of(L"\\/")+1)+L"scene.hlsl";
        auto vsCode = shader(shaderPath.c_str(),"VS","vs_5_0"), psCode = shader(shaderPath.c_str(),"PS","ps_5_0");
        ComPtr<ID3D11VertexShader> vs; ComPtr<ID3D11PixelShader> ps;
        check(dev->CreateVertexShader(vsCode->GetBufferPointer(),vsCode->GetBufferSize(),nullptr,&vs),"vertex shader");
        check(dev->CreatePixelShader(psCode->GetBufferPointer(),psCode->GetBufferSize(),nullptr,&ps),"pixel shader");
        D3D11_BUFFER_DESC bd{}; bd.ByteWidth = sizeof(State); bd.Usage = D3D11_USAGE_DEFAULT; bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        ComPtr<ID3D11Buffer> cb; check(dev->CreateBuffer(&bd,nullptr,&cb),"constants");
        D3D11_SAMPLER_DESC ss{}; ss.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR; ss.AddressU = ss.AddressV = ss.AddressW = D3D11_TEXTURE_ADDRESS_BORDER; ss.MaxLOD = D3D11_FLOAT32_MAX;
        ComPtr<ID3D11SamplerState> sampler; check(dev->CreateSamplerState(&ss,&sampler),"sampler");
        ctx->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST); ctx->VSSetShader(vs.Get(),nullptr,0); ctx->PSSetShader(ps.Get(),nullptr,0);
        ID3D11Buffer* cbPtr = cb.Get(); ctx->PSSetConstantBuffers(0,1,&cbPtr);
        ID3D11SamplerState* samplerPtr = sampler.Get(); ctx->PSSetSamplers(0,1,&samplerPtr);
        auto draw = [&](ID3D11RenderTargetView* view, UINT w, UINT h, State& state) {
            ctx->OMSetRenderTargets(1,&view,nullptr); // Flip Present unbinds this; rebind on every output.
            D3D11_VIEWPORT vp{0,0,static_cast<float>(w),static_cast<float>(h),0,1}; ctx->RSSetViewports(1,&vp);
            ctx->UpdateSubresource(cb.Get(),0,nullptr,&state,0,0); ctx->Draw(3,0);
        };
        auto verifyCode = shader(shaderPath.c_str(),"VerifyPS","ps_5_0");
        ComPtr<ID3D11PixelShader> verifyPS;
        check(dev->CreatePixelShader(verifyCode->GetBufferPointer(),verifyCode->GetBufferSize(),nullptr,&verifyPS),"verification shader");
        D3D11_TEXTURE2D_DESC vd = td; vd.Width = 64; vd.Height = 64;
        ComPtr<ID3D11Texture2D> verification; check(dev->CreateTexture2D(&vd,nullptr,&verification),"verification target");
        ComPtr<ID3D11RenderTargetView> verificationView; check(dev->CreateRenderTargetView(verification.Get(),nullptr,&verificationView),"verification view");
        vd.Usage = D3D11_USAGE_STAGING; vd.BindFlags = 0; vd.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        ComPtr<ID3D11Texture2D> readback; check(dev->CreateTexture2D(&vd,nullptr,&readback),"verification readback");
        ctx->PSSetShader(verifyPS.Get(),nullptr,0);
        int checkedPixels = 0;
        for (int test=0;test<32;++test) {
            State s{}; camera(test*0.17,s.source); camera(test*0.17+0.03*(test%5),s.current);
            s.current[2] *= 0.8f+0.02f*test; s.current[3] += 0.035f*test;
            s.clock[2] = static_cast<float>(width)/height; s.clock[3] = overscan;
            draw(verificationView.Get(),64,64,s); ctx->CopyResource(readback.Get(),verification.Get());
            D3D11_MAPPED_SUBRESOURCE map; check(ctx->Map(readback.Get(),0,D3D11_MAP_READ,0,&map),"verification map");
            bool failed = false;
            for (int y : {8,24,40,56}) for (int x : {8,24,40,56}) {
                double px = ((x+0.5)/64-0.5)*s.clock[2]*10/s.current[2];
                double py = -((y+0.5)/64-0.5)*10/s.current[2];
                double ca = std::cos(s.current[3]), sa = std::sin(s.current[3]);
                double wx = s.current[0]+ca*px-sa*py, wy = s.current[1]+sa*px+ca*py;
                double dx = wx-s.source[0], dy = wy-s.source[1];
                ca = std::cos(s.source[3]); sa = std::sin(s.source[3]);
                double uv[2] = {0.5+(ca*dx+sa*dy)*s.source[2]/(s.clock[2]*10*overscan),0.5-(-sa*dx+ca*dy)*s.source[2]/(10*overscan)};
                auto pixel = static_cast<const unsigned char*>(map.pData)+y*map.RowPitch+x*4;
                for (int channel=0;channel<2;++channel) {
                    int expected = static_cast<int>(std::lround(std::clamp(uv[channel],0.0,1.0)*255));
                    if (std::abs(expected-pixel[channel])>1) failed = true;
                }
                ++checkedPixels;
            }
            ctx->Unmap(readback.Get(),0);
            if (failed) throw std::runtime_error("GPU reprojection disagrees with CPU affine reference");
        }
        printf("GPU affine verification: %d pixels (32 cameras), mismatch=0, tolerance=1/255\n",checkedPixels);
        ctx->PSSetShader(ps.Get(),nullptr,0);
        ShowWindow(window.h,SW_SHOW); SetForegroundWindow(window.h);
        printf("monitor=%.3fHz target=%.3fHz base=%.3fFPS multiple=%d mode=%d size=%dx%d FlipDiscard waitable latency=1\n",monitorHz,target,base,multiple,mode,width,height); fflush(stdout);
        std::vector<Record> records; records.reserve(static_cast<size_t>(seconds*target+100));
        std::vector<DisplayRecord> displayRecords; displayRecords.reserve(static_cast<size_t>(seconds*target+100));
        UINT lastDisplay = 0; HRESULT statsResult = S_OK;
        double start = now(), previous = 0, sourceTime = 0; int oldReal = -1, missed = 0; bool occluded = false;
        Handle timer; timer.h = CreateWaitableTimerExW(nullptr,nullptr,0x2,TIMER_ALL_ACCESS); // CREATE_WAITABLE_TIMER_HIGH_RESOLUTION (Win10 1803+)
        if (!timer.h) timer.h = CreateWaitableTimerW(nullptr,FALSE,nullptr);
        if (!timer.h) throw std::runtime_error("Timer unavailable");
        for (int slot=0;pump();++slot) {
            double deadline = start+slot/target;
            if (deadline-start >= seconds) break;
            double remain = deadline-now();
            while (remain > 0.001) {
                LARGE_INTEGER due; due.QuadPart = -static_cast<LONGLONG>((remain-0.0005)*1e7);
                if (!SetWaitableTimer(timer.h,&due,0,nullptr,nullptr,FALSE)) throw std::runtime_error("SetWaitableTimer failed");
                DWORD w = MsgWaitForMultipleObjectsEx(1,&timer.h,2000,QS_ALLINPUT,MWMO_INPUTAVAILABLE);
                if (w == WAIT_FAILED || w == WAIT_TIMEOUT) throw std::runtime_error("Timer wait failed");
                if (!pump()) break;
                remain = deadline-now();
            }
            if (!IsWindow(window.h)) break;
            while (now()<deadline) { YieldProcessor(); }
            DWORD wait = MsgWaitForMultipleObjectsEx(1,&latency.h,2000,QS_ALLINPUT,MWMO_INPUTAVAILABLE);
            while (wait == WAIT_OBJECT_0+1) { if (!pump()) break; wait = MsgWaitForMultipleObjectsEx(1,&latency.h,2000,QS_ALLINPUT,MWMO_INPUTAVAILABLE); }
            if (!IsWindow(window.h)) break;
            if (wait != WAIT_OBJECT_0) throw std::runtime_error("Latency wait failed");
            double t = now(); if (t-start >= seconds) break;
            // Drop missed slots, never burst stale frames after a stall.
            int actualSlot = static_cast<int>(std::floor((t-start)*target));
            if (actualSlot>slot) { missed += actualSlot-slot; slot = actualSlot; deadline = start+slot/target; }
            int realIndex = static_cast<int>(std::floor((t-start)*base));
            bool real = realIndex!=oldReal;
            State s{}; s.clock[2] = static_cast<float>(width)/height; s.clock[3] = overscan; s.flags[0] = static_cast<float>(mode);
            if (real) { sourceTime = realIndex/base; oldReal = realIndex; }
            camera(sourceTime,s.source); camera(t-start,s.current); s.clock[0] = static_cast<float>(sourceTime); s.clock[1] = static_cast<float>(t-start);
            ID3D11ShaderResourceView* nullSrv = nullptr;
            if (real) { ctx->PSSetShaderResources(0,1,&nullSrv); s.flags[1] = 1; draw(sourceView.Get(),td.Width,td.Height,s); }
            s.flags[1] = 0;
            ID3D11ShaderResourceView* srv = sourceSrv.Get(); ctx->PSSetShaderResources(0,1,&srv); draw(backView.Get(),static_cast<UINT>(width),static_cast<UINT>(height),s);
            double beforePresent = now(); HRESULT hr = sc->Present(1,0); double afterPresent = now();
            check(hr,"Present"); if (hr == DXGI_STATUS_OCCLUDED) { puts("Occluded; measurement aborted"); occluded = true; break; }
            UINT presentId = 0; check(sc->GetLastPresentCount(&presentId),"last Present count");
            DXGI_FRAME_STATISTICS stats{}; statsResult = sc->GetFrameStatistics(&stats);
            if (SUCCEEDED(statsResult) && stats.PresentCount>lastDisplay && stats.SyncQPCTime.QuadPart>0) {
                displayRecords.push_back({stats.PresentCount,stats.PresentRefreshCount,stats.SyncRefreshCount,stats.SyncQPCTime.QuadPart,afterPresent-start});
                lastDisplay = stats.PresentCount;
            }
            records.push_back({t-start,previous ? (t-previous)*1000 : 0,(t-deadline)*1000,(beforePresent-t)*1000,(afterPresent-beforePresent)*1000,(t-start-sourceTime)*1000,t,real ? 1:0,slot,presentId});
            previous = t;
        }
        // Draw one last frame after the measured interval for an optional readback.
        if (!shot.empty() && !records.empty()) {
            const auto& last = records.back(); State s{}; camera(sourceTime,s.source); camera(last.time,s.current);
            s.clock[0] = static_cast<float>(sourceTime); s.clock[1] = static_cast<float>(last.time); s.clock[2] = static_cast<float>(width)/height; s.clock[3] = overscan; s.flags[0] = static_cast<float>(mode);
            draw(backView.Get(),static_cast<UINT>(width),static_cast<UINT>(height),s); capture(dev.Get(),ctx.Get(),back.Get(),shot);
        }
        FILE* file = nullptr; _wfopen_s(&file,csv.c_str(),L"wb"); if (!file) throw std::runtime_error("Cannot write CSV");
        fprintf(file,"time_s,interval_ms,lateness_ms,cpu_submit_ms,present_call_ms,source_age_ms,qpc_start_s,real,slot,present_id\n");
        size_t realCount = 0;
        for (auto& row : records) { realCount += row.real; fprintf(file,"%.9f,%.6f,%.6f,%.6f,%.6f,%.6f,%.9f,%d,%d,%u\n",row.time,row.interval,row.lateness,row.draw,row.present,row.sourceAge,row.qpcStart,row.real,row.slot,row.presentId); }
        fclose(file);
        std::wstring displayPath = csv+L".display.csv";
        _wfopen_s(&file,displayPath.c_str(),L"wb"); if (!file) throw std::runtime_error("Cannot write DXGI statistics");
        fprintf(file,"present_count,present_refresh_count,sync_refresh_count,sync_qpc_s,observed_s\n");
        for (auto& row : displayRecords) fprintf(file,"%u,%u,%u,%.9f,%.9f\n",row.present,row.refresh,row.sync,static_cast<double>(row.qpc)/frequency,row.observed);
        fclose(file);
        printf("DXGI displayed statistics samples=%zu last_hresult=0x%08lX\n",displayRecords.size(),static_cast<unsigned long>(statsResult));
        printf("outputs=%zu real=%zu generated=%zu missed_slots=%d elapsed=%.3f csv written (CPU submit is NOT GPU time; output count is NOT displayed count)\n",records.size(),realCount,records.size()-realCount,missed,now()-start);
        return records.size()>10 && !occluded ? 0 : 2;
    } catch (const std::exception& e) { fprintf(stderr,"FrameGenLab: %s\n",e.what()); return 1; }
}
