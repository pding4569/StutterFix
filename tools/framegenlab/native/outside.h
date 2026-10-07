// Research only. One existing swapchain, with a plugin gate around Unity rendering.
#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11_4.h>
#include <dxgi1_5.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <array>
#include <vector>
#include <thread>
#include <mutex>
#include <atomic>
#include <cmath>
#include <cstdio>
#include <string>
#include <limits>
#include <stdexcept>
#include <algorithm>
namespace outside {
using Microsoft::WRL::ComPtr;
inline void check(HRESULT hr) { if(FAILED(hr)) throw std::runtime_error("outside D3D11 failure"); }
inline double now() { LARGE_INTEGER q,f; QueryPerformanceCounter(&q); QueryPerformanceFrequency(&f); return double(q.QuadPart)/double(f.QuadPart); }
struct Pose { float camera[4]; float planet[2][4]; }; // xy, size/radius, z angle radians
struct Packet {
    void* textures[5]; // Full scene without UI; four reserved pointers for the existing packet layout.
    Pose pose;
    double song;
    int frame,mode,measure,flip,linear,capture;
};
static_assert(sizeof(Packet)==120,"packet ABI");
struct Record { double time,song,gpu; int real,frame; HRESULT hr; double age; };
struct SourceRecord { double time,song; int frame; Pose pose; double cameraError,redError,blueError,horizon; };
inline float angleDifference(float a,float b) { return std::remainder(a-b,6.28318530718f); }
inline Pose predict(const Pose& previous,const Pose& last,double dt,double ahead) {
    Pose result=last;
    if(dt<=.00001 || dt>.1 || ahead<0) return result;
    // No future state. A bounded 50 ms horizon avoids unbounded zoom/teleport extrapolation.
    float ratio=float(std::min(ahead,.05)/dt);
    auto extrapolate=[ratio](const float* a,const float* b,float* c) { for(int i=0;i<3;i++) c[i]=b[i]+(b[i]-a[i])*ratio; c[3]=b[3]+angleDifference(b[3],a[3])*ratio; };
    extrapolate(previous.camera,last.camera,result.camera);
    result.camera[2]=std::clamp(result.camera[2],last.camera[2]*.5f,last.camera[2]*2.f);
    for(int i=0;i<2;i++) { extrapolate(previous.planet[i],last.planet[i],result.planet[i]); result.planet[i][2]=std::max(.001f,result.planet[i][2]); }
    return result;
}
inline std::array<double,2> screenPoint(double x,double y,const Pose& p,unsigned w,unsigned h) {
    double a=p.camera[3],c=cos(a),s=sin(a),dx=x-p.camera[0],dy=y-p.camera[1];
    return {double(w)*.5+(c*dx+s*dy)*h/(2*p.camera[2]),double(h)*.5-(-s*dx+c*dy)*h/(2*p.camera[2])};
}
inline double pointError(double x,double y,double px,double py,const Pose& actual,const Pose& predicted,unsigned w,unsigned h) {
    auto a=screenPoint(x,y,actual,w,h),b=screenPoint(px,py,predicted,w,h); return hypot(a[0]-b[0],a[1]-b[1]);
}
inline DXGI_FORMAT raw(DXGI_FORMAT f) {
    if(f==DXGI_FORMAT_R8G8B8A8_TYPELESS || f==DXGI_FORMAT_R8G8B8A8_UNORM_SRGB) return DXGI_FORMAT_R8G8B8A8_UNORM;
    if(f==DXGI_FORMAT_B8G8R8A8_TYPELESS || f==DXGI_FORMAT_B8G8R8A8_UNORM_SRGB) return DXGI_FORMAT_B8G8R8A8_UNORM;
    if(f==DXGI_FORMAT_R16G16B16A16_TYPELESS) return DXGI_FORMAT_R16G16B16A16_FLOAT;
    return f;
}
struct ContextLock { ID3D11Multithread* m; explicit ContextLock(ID3D11Multithread* p):m(p){m->Enter();} ~ContextLock(){m->Leave();} };
struct Texture { ComPtr<ID3D11Texture2D> texture; ComPtr<ID3D11ShaderResourceView> view; };
struct Slot { std::array<Texture,2> images; Packet packet{}; Pose previous{}; double qpc=0,previousSong=0,period=.005; unsigned long long sequence=0; };
struct Query { ComPtr<ID3D11Query> begin,end,disjoint; size_t row=SIZE_MAX; };
class Output {
public:
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> immediate;
    ComPtr<ID3D11Multithread> protection;
    std::atomic<int> error{0};
    std::atomic<unsigned long long> outputs{0};
    std::recursive_mutex frameMutex;
    std::atomic<DWORD> workerId{0};
    std::atomic<bool> frameHeld{false};
    std::atomic<unsigned long long> frameBegins{0}, frameEnds{0};
    std::vector<Record> records;
    std::vector<SourceRecord> sources;
    std::wstring path;
    unsigned width=0,height=0;
    bool oldProtection=false;
    void trace(const char* message) { FILE* f=nullptr; _wfopen_s(&f,(path+L"/native-init.txt").c_str(),L"ab"); if(f) { fprintf(f,"%s thread=%lu\n",message,GetCurrentThreadId()); fclose(f); } }
    void start(ID3D11Device* d,IDXGISwapChain* chain,const std::wstring& p,unsigned w,unsigned h) {
        if(worker.joinable()) throw std::runtime_error("already running");
        if(d->GetCreationFlags()&D3D11_CREATE_DEVICE_SINGLETHREADED) throw std::runtime_error("single-threaded device is unsupported");
        path=p; trace("start");
        device=d; device->GetImmediateContext(&immediate); trace("immediate"); check(immediate.As(&protection)); trace("protection interface");
        oldProtection=protection->SetMultithreadProtected(TRUE)!=FALSE;
        trace("protection enabled"); check(chain->QueryInterface(IID_PPV_ARGS(&swap))); width=w; height=h; stopRequested=false;
        records.reserve(1000000); sources.reserve(500000); // Bounded, allocated before playing; three whole runs fit without growth.
    }
    void beginFrame() {
        frameMutex.lock();
        if(frameHeld.exchange(true)) { error=4; frameMutex.unlock(); return; }
        ++frameBegins;
    }
    void endFrame() { if(frameHeld.exchange(false)) { ++frameEnds; frameMutex.unlock(); } }
    void stop() { stopRequested=true; endFrame(); if(worker.joinable()) worker.join(); }
    bool isWorker() const { return workerId.load()==GetCurrentThreadId(); }
    void drawReal() { std::lock_guard<std::mutex> gate(mutex); auto& s=slots[published%slots.size()]; if(s.sequence) render(s,true,s.qpc); }
    HRESULT present() {
        ContextLock context(protection.Get());
        ComPtr<ID3D11RenderTargetView> old[8]; ID3D11RenderTargetView* restore[8]{}; ComPtr<ID3D11DepthStencilView> z;
        for(int i=0;i<8;i++) restore[i]=nullptr;
        immediate->OMGetRenderTargets(8,restore,&z); for(int i=0;i<8;i++) old[i].Attach(restore[i]);
        HRESULT hr=swap->Present(0,0);
        immediate->OMSetRenderTargets(8,restore,z.Get()); // Flip Present can unbind Unity's cached backbuffer target.
        return hr;
    }
    void release() { stop(); if(protection) protection->SetMultithreadProtected(oldProtection); for(auto& s:slots) s=Slot{}; protection.Reset(); immediate.Reset(); device.Reset(); }
    ~Output(){release();}
    // Producer/render thread only. Ordered GPU copies and publication are under the same gate as playback.
    void publish(const Packet& p,ID3D11Texture2D* screen) {
        if(error.load()) throw std::runtime_error("worker failed");
        double t=now();
        if(p.song<lastPacket.song-1) captured=0;
        SourceRecord row{t,p.song,p.frame,p.pose,0,0,0,0};
        if(p.measure && lastPacket.measure && previousPacket.measure && p.pose.camera[2]>.001f && lastPacket.pose.camera[2]>.001f && previousPacket.pose.camera[2]>.001f && havePrevious && p.song>lastPacket.song && lastPacket.song>previousPacket.song) {
            double horizon=p.song-lastPacket.song; Pose predicted=predict(previousPacket.pose,lastPacket.pose,lastPacket.song-previousPacket.song,horizon);
            double aspect=double(width)/height,maximum=0;
            for(int corner=0;corner<4;corner++) {
                double dx=(corner&1?1:-1)*p.pose.camera[2]*aspect,dy=(corner&2?1:-1)*p.pose.camera[2];
                double c=cos(p.pose.camera[3]),s=sin(p.pose.camera[3]); double x=p.pose.camera[0]+c*dx-s*dy,y=p.pose.camera[1]+s*dx+c*dy;
                maximum=std::max(maximum,pointError(x,y,x,y,p.pose,predicted,width,height));
            }
            row.cameraError=maximum;
            row.redError=pointError(p.pose.planet[0][0],p.pose.planet[0][1],predicted.planet[0][0],predicted.planet[0][1],p.pose,predicted,width,height);
            row.blueError=pointError(p.pose.planet[1][0],p.pose.planet[1][1],predicted.planet[1][0],predicted.planet[1][1],p.pose,predicted,width,height);
            row.horizon=horizon;
        }
        if(p.measure && sources.size()<500000) sources.push_back(row);
        if(p.mode==0) { mode=0; previousPacket=lastPacket; lastPacket=p; lastQpc=t; havePrevious=true; return; }
        if(!initialized) {
            initialize(); initialized=true;
        }
        if(!worker.joinable()) {
            worker=std::thread([this]{run();});
        }
        std::lock_guard<std::mutex> gate(mutex);
        ContextLock context(protection.Get());
        auto& s=slots[(published+1)%slots.size()];
        for(int i=0;i<2;i++) copy(i==1?screen:static_cast<ID3D11Texture2D*>(p.textures[0]),s.images[i]);
        s.packet=p; s.previous=lastPacket.pose; s.previousSong=lastPacket.song;
        s.period=havePrevious?std::clamp(t-lastQpc,.001,.05):.005; s.qpc=t; s.sequence=++published;
        immediate->Flush(); // Hand buffered Unity work to the driver before waking independent output.
        previousPacket=lastPacket; lastPacket=p; lastQpc=t; havePrevious=true; mode=p.mode;
    }
    void save() {
        FILE* f=nullptr; _wfopen_s(&f,(path+L"/presents.csv").c_str(),L"wb");
        if(f) { fprintf(f,"present_s,real,unity_frame,gpu_ms,hr,song_s,source_age_ms\n"); for(auto& r:records) fprintf(f,"%.9f,%d,%d,%.6f,%ld,%.9f,%.6f\n",r.time,r.real,r.frame,r.gpu,long(r.hr),r.song,r.age*1000); fclose(f); }
        _wfopen_s(&f,(path+L"/sources.csv").c_str(),L"wb");
        if(f) { fprintf(f,"source_s,song_s,unity_frame,camera_px,red_px,blue_px,horizon_ms,camera_x,camera_y,camera_size,camera_angle,red_x,red_y,blue_x,blue_y\n"); for(auto& r:sources) fprintf(f,"%.9f,%.9f,%d,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f\n",r.time,r.song,r.frame,r.cameraError,r.redError,r.blueError,r.horizon*1000,r.pose.camera[0],r.pose.camera[1],r.pose.camera[2],r.pose.camera[3],r.pose.planet[0][0],r.pose.planet[0][1],r.pose.planet[1][0],r.pose.planet[1][1]); fclose(f); }
    }
    void recordOff(const Packet& p,HRESULT hr) { ++outputs; std::lock_guard<std::mutex> gate(mutex); if(p.measure && records.size()<1000000) records.push_back({now(),p.song,NAN,1,p.frame,hr,0}); }
private:
    std::thread worker;
    std::mutex mutex;
    std::atomic<bool> stopRequested{false};
    std::atomic<int> mode{0};
    bool initialized=false;
    std::array<Slot,3> slots;
    unsigned long long published=0;
    Packet previousPacket{},lastPacket{};
    double lastQpc=0;
    bool havePrevious=false;
    ComPtr<ID3D11DeviceContext> deferred;
    ComPtr<IDXGISwapChain1> swap;
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11Buffer> constants;
    ComPtr<ID3D11SamplerState> sampler;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11DepthStencilState> depth;
    std::array<Query,256> queries;
    size_t nextQuery=0,collectQuery=0;
    Record pending{};
    Query* pendingQuery=nullptr;
    int captured=0;
    void picture(ID3D11Texture2D* texture,const wchar_t* name) {
        ContextLock lock(protection.Get()); D3D11_TEXTURE2D_DESC d{}; texture->GetDesc(&d); DXGI_FORMAT format=raw(d.Format); bool half=format==DXGI_FORMAT_R16G16B16A16_FLOAT;
        d.Usage=D3D11_USAGE_STAGING; d.BindFlags=d.MiscFlags=0; d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
        ComPtr<ID3D11Texture2D> staging; check(device->CreateTexture2D(&d,nullptr,&staging)); immediate->CopyResource(staging.Get(),texture);
        D3D11_MAPPED_SUBRESOURCE mapped{}; check(immediate->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped));
        FILE* f=nullptr; _wfopen_s(&f,(path+L"/"+name).c_str(),L"wb");
        if(f) { fprintf(f,"P6\n%u %u\n255\n",d.Width,d.Height); std::vector<unsigned char> row(d.Width*3);
            for(UINT y=0;y<d.Height;y++) { auto p=static_cast<unsigned char*>(mapped.pData)+size_t(y)*mapped.RowPitch;
                for(UINT x=0;x<d.Width;x++) for(int c=0;c<3;c++) {
                    if(half) { unsigned value=reinterpret_cast<unsigned short*>(p+x*8)[c],e=(value>>10)&31,m=value&1023; double channel=e?ldexp(1.+double(m)/1024,int(e)-15):ldexp(double(m),-24); if(value&32768) channel=-channel; row[x*3+c]=static_cast<unsigned char>(std::clamp(channel*255,0.,255.)); }
                    else row[x*3+c]=p[x*4+(format==DXGI_FORMAT_B8G8R8A8_UNORM?2-c:c)];
                } fwrite(row.data(),1,row.size(),f);
            } fclose(f);
        } immediate->Unmap(staging.Get(),0);
    }
    void copy(ID3D11Texture2D* input,Texture& out) {
        if(!input) throw std::runtime_error("missing texture");
        D3D11_TEXTURE2D_DESC d{}; input->GetDesc(&d);
        if(out.texture) { D3D11_TEXTURE2D_DESC old{}; out.texture->GetDesc(&old); if(old.Width!=d.Width || old.Height!=d.Height || raw(old.Format)!=raw(d.Format)) out=Texture{}; }
        if(!out.texture) {
            d.Usage=D3D11_USAGE_DEFAULT; d.BindFlags=D3D11_BIND_SHADER_RESOURCE; d.CPUAccessFlags=d.MiscFlags=0;
            check(device->CreateTexture2D(&d,nullptr,&out.texture));
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{}; sd.Format=raw(d.Format); sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D; sd.Texture2D.MipLevels=1;
            check(device->CreateShaderResourceView(out.texture.Get(),&sd,&out.view));
        }
        immediate->CopyResource(out.texture.Get(),input);
    }
    void initialize();
    void render(const Slot& s,bool real,double tick);
    void collect(bool all=false) {
        std::lock_guard<std::recursive_mutex> frame(frameMutex);
        ContextLock lock(protection.Get());
        for(size_t i=0;i<(all?queries.size():8);i++) {
            auto& q=queries[collectQuery++%queries.size()]; if(q.row==SIZE_MAX) continue;
            D3D11_QUERY_DATA_TIMESTAMP_DISJOINT d{}; UINT64 a=0,b=0;
            if(immediate->GetData(q.disjoint.Get(),&d,sizeof(d),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK || immediate->GetData(q.begin.Get(),&a,sizeof(a),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK || immediate->GetData(q.end.Get(),&b,sizeof(b),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK) continue;
            if(!d.Disjoint && d.Frequency && b>=a) records[q.row].gpu=double(b-a)*1000/d.Frequency;
            q.row=SIZE_MAX;
        }
    }
    void run() {
        workerId=GetCurrentThreadId();
        HANDLE timer=CreateWaitableTimerExW(nullptr,nullptr,0x2,TIMER_ALL_ACCESS);
        try {
            if(!timer) throw std::runtime_error("high resolution timer unavailable");
            unsigned long long seen=0; double deadline=0;
            while(!stopRequested) {
                if(!mode.load()) { Sleep(1); continue; }
                double t=now();
                {
                    std::lock_guard<std::recursive_mutex> frame(frameMutex);
                    if(stopRequested || error) break;
                    std::lock_guard<std::mutex> gate(mutex);
                    auto& s=slots[published%slots.size()];
                    int currentMode=mode.load();
                    if(s.sequence && currentMode) {
                        double interval=s.period/currentMode;
                        if(seen!=s.sequence) { seen=s.sequence; deadline=s.qpc+interval; }
                        if(t>=deadline) {
                            render(s,false,t);
                            HRESULT hr=present();
                            if(FAILED(hr)) { error=int(hr); throw std::runtime_error("Present failed"); }
                            ++outputs;
                            if(pending.frame>=0 && records.size()<1000000) {
                                pending.time=now(); pending.hr=hr; records.push_back(pending);
                                if(pendingQuery) pendingQuery->row=records.size()-1;
                            }
                            deadline+=interval;
                            if(deadline<t-interval) deadline=t+interval;
                        }
                    }
                }
                collect();
                double remaining=deadline-now();
                if(remaining>.00015) { LARGE_INTEGER due; due.QuadPart=-LONGLONG((remaining-.0001)*10000000); SetWaitableTimer(timer,&due,0,nullptr,nullptr,FALSE); WaitForSingleObject(timer,5); }
                else SwitchToThread();
            }
            { std::lock_guard<std::recursive_mutex> frame(frameMutex); ContextLock context(protection.Get()); immediate->Flush(); }
            double end=now()+2; while(now()<end) { collect(true); bool outstanding=false; for(auto& q:queries) outstanding|=q.row!=SIZE_MAX; if(!outstanding) break; Sleep(1); }
        } catch(const std::exception& ex) { trace(ex.what()); if(!error) error=1; }
        if(timer) CloseHandle(timer);
        workerId=0;
    }
};
inline const char* shader=R"(
cbuffer Data:register(b0) { float4 oldCamera; float4 camera; float4 info; };
Texture2D worldTex:register(t0); Texture2D screenTex:register(t1);
SamplerState linearSampler:register(s0);
struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
V VS(uint id:SV_VertexID) { V v; v.uv=float2((id<<1)&2,id&2); v.pos=float4(v.uv*float2(2,-2)+float2(-1,1),0,1); return v; }
float2 rotate(float2 p,float a) { float s,c; sincos(a,s,c); return float2(c*p.x-s*p.y,s*p.x+c*p.y); }
float2 world(float2 uv,float4 c) { return rotate(float2((uv.x-.5)*info.x,.5-uv.y)*2*c.z,c.w)+c.xy; }
float2 sourceUV(float2 p) { float2 q=rotate(p-oldCamera.xy,-oldCamera.w)/(2*oldCamera.z); float2 uv=float2(.5+q.x/info.x,.5-q.y); if(info.y>.5) uv.y=1-uv.y; return uv; }
float4 PS(V v):SV_TARGET {
    float3 color=worldTex.SampleLevel(linearSampler,sourceUV(world(v.uv,camera)),0).rgb;
    float3 oldWorld=worldTex.SampleLevel(linearSampler,sourceUV(world(v.uv,oldCamera)),0).rgb;
    float3 ui=screenTex.SampleLevel(linearSampler,v.uv,0).rgb;
    if(max(max(abs(ui.r-oldWorld.r),abs(ui.g-oldWorld.g)),abs(ui.b-oldWorld.b))>2.5/255) color=ui;
    return float4(color,1);
})";
inline void Output::initialize() {
    ContextLock initializationGate(protection.Get());
    check(device->CreateDeferredContext(0,&deferred));
    trace("existing swapchain; deferred created");
    for(auto& q:queries) { D3D11_QUERY_DESC desc{D3D11_QUERY_TIMESTAMP,0}; check(device->CreateQuery(&desc,&q.begin)); check(device->CreateQuery(&desc,&q.end)); desc.Query=D3D11_QUERY_TIMESTAMP_DISJOINT; check(device->CreateQuery(&desc,&q.disjoint)); }
    trace("queries created");
    ComPtr<ID3DBlob> v,p,errors;
    check(D3DCompile(shader,strlen(shader),nullptr,nullptr,nullptr,"VS","vs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&v,&errors));
    check(D3DCompile(shader,strlen(shader),nullptr,nullptr,nullptr,"PS","ps_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&p,&errors));
    trace("shaders compiled");
    check(device->CreateVertexShader(v->GetBufferPointer(),v->GetBufferSize(),nullptr,&vs)); check(device->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&ps));
    D3D11_BUFFER_DESC bd{}; bd.ByteWidth=48; bd.Usage=D3D11_USAGE_DEFAULT; bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER; check(device->CreateBuffer(&bd,nullptr,&constants));
    D3D11_SAMPLER_DESC ss{}; ss.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR; ss.AddressU=ss.AddressV=ss.AddressW=D3D11_TEXTURE_ADDRESS_BORDER; ss.MaxLOD=D3D11_FLOAT32_MAX; check(device->CreateSamplerState(&ss,&sampler));
    D3D11_RASTERIZER_DESC rd{}; rd.FillMode=D3D11_FILL_SOLID; rd.CullMode=D3D11_CULL_NONE; rd.DepthClipEnable=TRUE; check(device->CreateRasterizerState(&rd,&raster));
    D3D11_DEPTH_STENCIL_DESC dd{}; check(device->CreateDepthStencilState(&dd,&depth));
    trace("worker ready");
}
inline void Output::render(const Slot& s,bool real,double tick) {
    double age=std::max(0.,tick-s.qpc),song=s.packet.song+age;
    Pose predicted=real?s.packet.pose:predict(s.previous,s.packet.pose,s.packet.song-s.previousSong,age);
    ComPtr<ID3D11Texture2D> back; ComPtr<ID3D11RenderTargetView> rtv;
    { ContextLock lock(protection.Get()); check(swap->GetBuffer(0,IID_PPV_ARGS(&back))); check(device->CreateRenderTargetView(back.Get(),nullptr,&rtv)); }
    auto& q=queries[nextQuery++%queries.size()]; bool timing=!real && s.packet.measure && q.row==SIZE_MAX && records.size()<1000000;
    deferred->ClearState(); if(timing) { deferred->Begin(q.disjoint.Get()); deferred->End(q.begin.Get()); }
    float data[12]; memcpy(data,s.packet.pose.camera,16); memcpy(data+4,predicted.camera,16); data[8]=float(width)/height; data[9]=float(s.packet.flip); data[10]=data[11]=0;
    deferred->UpdateSubresource(constants.Get(),0,nullptr,data,0,0); ID3D11Buffer* b=constants.Get(); deferred->PSSetConstantBuffers(0,1,&b);
    ID3D11ShaderResourceView* views[2]={s.images[0].view.Get(),s.images[1].view.Get()}; deferred->PSSetShaderResources(0,2,views);
    auto sm=sampler.Get(); deferred->PSSetSamplers(0,1,&sm); auto rt=rtv.Get(); deferred->OMSetRenderTargets(1,&rt,nullptr);
    D3D11_VIEWPORT vp{0,0,float(width),float(height),0,1}; deferred->RSSetViewports(1,&vp); deferred->RSSetState(raster.Get()); deferred->OMSetDepthStencilState(depth.Get(),0);
    deferred->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST); deferred->VSSetShader(vs.Get(),nullptr,0); deferred->PSSetShader(ps.Get(),nullptr,0); deferred->Draw(3,0);
    if(timing) { deferred->End(q.end.Get()); deferred->End(q.disjoint.Get()); }
    deferred->OMSetRenderTargets(0,nullptr,nullptr); ComPtr<ID3D11CommandList> list; check(deferred->FinishCommandList(FALSE,&list));
    { ContextLock lock(protection.Get()); immediate->ExecuteCommandList(list.Get(),TRUE); }
    const double captureTimes[]={50,120,175,300}; // All readbacks occur AFTER the 5..45s performance window.
    if(s.packet.capture && !real && captured<4 && s.packet.song>=captureTimes[captured]) {
        wchar_t name[100]; swprintf_s(name,L"%dx-capture-%02d-song%.1f.ppm",s.packet.mode,captured,song); picture(back.Get(),name);
        FILE* f=nullptr; _wfopen_s(&f,(path+L"/visual-pose.txt").c_str(),L"ab"); if(f) { fprintf(f,"frame=%d song=%.9f age=%.6f source=%.6f,%.6f,%.6f,%.6f predicted=%.6f,%.6f,%.6f,%.6f\n",s.packet.frame,song,age,s.packet.pose.camera[0],s.packet.pose.camera[1],s.packet.pose.camera[2],s.packet.pose.camera[3],predicted.camera[0],predicted.camera[1],predicted.camera[2],predicted.camera[3]); fclose(f); } ++captured;
    }
    pending={0,song,NAN,real?1:0,s.packet.measure?s.packet.frame:-1,S_OK,age}; pendingQuery=timing?&q:nullptr;
}
} // namespace outside
