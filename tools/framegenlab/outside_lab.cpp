// Tests one original swapchain shared by a gated producer and an independent output thread.
#include "native/outside.h"
#include <d3d11sdklayers.h>
using namespace outside;
static Texture texture(ID3D11Device* d,unsigned w,unsigned h,DXGI_FORMAT format) {
    Texture t; D3D11_TEXTURE2D_DESC td{}; td.Width=w; td.Height=h; td.MipLevels=td.ArraySize=1; td.Format=format; td.SampleDesc.Count=1; td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    check(d->CreateTexture2D(&td,nullptr,&t.texture)); check(d->CreateShaderResourceView(t.texture.Get(),nullptr,&t.view)); return t;
}
int wmain(int argc,wchar_t** argv) {
    try {
        { Packet a{},b{}; a.pose.camera[2]=b.pose.camera[2]=10;
          a.pose.camera[0]=.5f; b.pose.camera[0]=-.5f; b.textures[1]=reinterpret_cast<void*>(1);
          float base[4]={0,0,1,0}; memcpy(a.textures+3,base,16); memcpy(b.textures+3,base,16);
          auto p=scenePrediction(a,b,.005,.0025);
          if(fabs(p.camera[0]-b.pose.camera[0])>.00001) throw std::runtime_error("random camera shake was extrapolated");
          b.pulse[0]=12; b.pulse[1]=10; b.pulse[2]=.095f; b.pulse[3]=.1f; b.pose.camera[2]=10.1f;
          p=scenePrediction(a,b,.005,.01);
          if(fabs(p.camera[2]-10)>.00001) throw std::runtime_error("pulse exceeded its endpoint"); }
        int mode=argc>1?_wtoi(argv[1]):4; double fps=argc>2?_wtof(argv[2]):200,seconds=argc>3?_wtof(argv[3]):15;
        if(mode<1 || mode>8 || !std::isfinite(fps) || fps<=0 || !std::isfinite(seconds) || seconds<=0) return 2;
        std::wstring path=argc>4?argv[4]:L"."; SetProcessDPIAware();
        bool narrow=argc>5 && _wtoi(argv[5])==1;
        bool blend=argc>6 && _wtoi(argv[6])==1;
        bool imageGate=argc>7 && _wtoi(argv[7])==1;
        int costStage=argc>8?_wtoi(argv[8]):0;
        if(costStage && (!narrow || !blend || imageGate || costStage>3 || costStage<0)) return 2;
        if(imageGate && !blend) return 2;
        { Pose a{},b{}; a.camera[0]=-5; a.camera[2]=2; b.camera[0]=7; b.camera[2]=20;
          for(int i=-10;i<=110;i++) { auto p=interpolateCamera(a,b,i*.01); if(p.camera[0]<-5 || p.camera[0]>7 || p.camera[2]<2 || p.camera[2]>20) throw std::runtime_error("camera blend exceeded known poses"); } }
        HWND window=CreateWindowExW(0,L"STATIC",L"FrameGen shared-device producer",WS_POPUP|WS_VISIBLE,0,0,3440,1440,nullptr,nullptr,GetModuleHandleW(nullptr),nullptr); if(!window) return 3;
        ComPtr<ID3D11Device> d; ComPtr<ID3D11DeviceContext> context; UINT flags=D3D11_CREATE_DEVICE_DEBUG;
        DXGI_SWAP_CHAIN_DESC sd{}; sd.BufferDesc.Width=3440; sd.BufferDesc.Height=1440; sd.BufferDesc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;
        sd.SampleDesc.Count=1; sd.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT; sd.BufferCount=2; sd.OutputWindow=window; sd.Windowed=TRUE; sd.SwapEffect=DXGI_SWAP_EFFECT_FLIP_DISCARD;
        ComPtr<IDXGISwapChain> swap;
        HRESULT hr=D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,flags,nullptr,0,D3D11_SDK_VERSION,&sd,&swap,&d,nullptr,&context);
        bool debug=SUCCEEDED(hr); if(!debug) check(D3D11CreateDeviceAndSwapChain(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&sd,&swap,&d,nullptr,&context));
        Output engine; engine.start(d.Get(),swap.Get(),path,3440,1440);
        std::array<Texture,6> images; std::array<ComPtr<ID3D11RenderTargetView>,6> rtvs;
        std::array<Texture,3> worldRing;std::array<ComPtr<ID3D11RenderTargetView>,3> worldTargets;
        if(costStage) for(int i=0;i<3;i++) {worldRing[i]=texture(d.Get(),3440,1440,DXGI_FORMAT_R8G8B8A8_UNORM);check(d->CreateRenderTargetView(worldRing[i].texture.Get(),nullptr,&worldTargets[i]));}
        for(int i=0;i<6;i++) { images[i]=texture(d.Get(),i==0||i==5?3440:512,i==0||i==5?1440:512,i==0||i==5?DXGI_FORMAT_R8G8B8A8_UNORM:DXGI_FORMAT_R16G16B16A16_FLOAT); check(d->CreateRenderTargetView(images[i].texture.Get(),nullptr,&rtvs[i])); }
        ComPtr<ID3DBlob> vertexCode,pixelCode,errors;
        const char* marker="cbuffer C:register(b0){float4 color;} float4 PS(float4 position:SV_POSITION):SV_TARGET{return position.y<18?float4(.9,.7,.1,1):color;}";
        check(D3DCompile(shader,strlen(shader),nullptr,nullptr,nullptr,"VS","vs_5_0",0,0,&vertexCode,&errors));
        check(D3DCompile(marker,strlen(marker),nullptr,nullptr,nullptr,"PS","ps_5_0",0,0,&pixelCode,&errors));
        ComPtr<ID3D11VertexShader> producerVS; ComPtr<ID3D11PixelShader> producerPS; ComPtr<ID3D11Buffer> producerCB;
        check(d->CreateVertexShader(vertexCode->GetBufferPointer(),vertexCode->GetBufferSize(),nullptr,&producerVS)); check(d->CreatePixelShader(pixelCode->GetBufferPointer(),pixelCode->GetBufferSize(),nullptr,&producerPS));
        D3D11_BUFFER_DESC cbDesc{}; cbDesc.ByteWidth=16; cbDesc.Usage=D3D11_USAGE_DEFAULT; cbDesc.BindFlags=D3D11_BIND_CONSTANT_BUFFER; check(d->CreateBuffer(&cbDesc,nullptr,&producerCB));
        double start=now(),deadline=start; int frames=0,stateErrors=0,pixelErrors=0; bool paused=false; unsigned long long beforePause=0,afterPause=0,gapOutputs=0;
        while(now()-start<seconds) {
            MSG msg; while(PeekMessageW(&msg,nullptr,0,0,PM_REMOVE)) {TranslateMessage(&msg); DispatchMessageW(&msg);}
            double t=now()-start; if(now()<deadline) { Sleep(0); continue; } deadline+=1/fps;
            if(!paused && t>seconds*.5) { beforePause=engine.outputs; Sleep(100); afterPause=engine.outputs; paused=true; }
            Packet p{}; p.frame=++frames; p.mode=mode==1?0:mode; p.measure=1; p.song=now()-start;
            if(blend) p.capture=4;
            if(imageGate) p.capture|=32;
            if(costStage) p.capture=64|4|(3<<7)|2048;
            if(costStage>=2) p.capture|=4096;
            if(costStage>=3) p.capture|=8192;
            if(costStage && mode==1) {p.mode=1;p.capture|=1024;}
            if(blend && !cameraBlendEnabled(p)) throw std::runtime_error("camera blend research guard was not compiled");
            if(!narrow) engine.beginFrame();
            p.pose.camera[0]=float(p.song)*2; p.pose.camera[1]=float(sin(p.song)*.3); p.pose.camera[2]=10+float(sin(p.song*2)); p.pose.camera[3]=float(sin(p.song)*.05);
            p.pose.planet[0][0]=p.pose.camera[0]+float(cos(p.song*4)); p.pose.planet[0][1]=float(sin(p.song*4)); p.pose.planet[0][2]=1;
            p.pose.planet[1][0]=p.pose.camera[0]-1; p.pose.planet[1][2]=1;
            // Deliberate non-default pipeline; worker must leave it unchanged.
            { ContextLock lock(engine.protection.Get());
              D3D11_VIEWPORT vp{17,29,257,193,0,1}; context->RSSetViewports(1,&vp); auto target=rtvs[0].Get(); context->OMSetRenderTargets(1,&target,nullptr);
              for(int i=0;i<6;i++) { float color[4]={.05f+float(frames%32)/200,.08f,.12f,1}; if(i==2||i==4) color[0]=color[1]=color[2]=1; else if(i!=0 && i!=5) color[0]=color[1]=color[2]=0; context->ClearRenderTargetView(rtvs[i].Get(),color); }
              context->VSSetShader(producerVS.Get(),nullptr,0); context->PSSetShader(producerPS.Get(),nullptr,0); ID3D11Buffer* cb=producerCB.Get(); context->PSSetConstantBuffers(0,1,&cb); float color[4]={.05f+float(frames%32)/200,.08f,.12f,1}; context->UpdateSubresource(cb,0,nullptr,color,0,0); context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST); context->Draw(3,0);
            }
            if(narrow) engine.beginFrame();
            for(int i=0;i<5;i++) p.textures[i]=images[i].texture.Get();
            if(narrow) p.textures[1]=reinterpret_cast<void*>(1);
            if(narrow) { float base[4]={p.pose.camera[0],p.pose.camera[1],p.pose.camera[2],p.pose.camera[3]}; memcpy(p.textures+3,base,16); }
            if(costStage) {
                unsigned long long beforeGap=engine.outputs;
                if(costStage>=3) {
                    engine.stageScreen(p,images[5].texture.Get());engine.endFrame(false);
                    // Model another worker output overwriting the mutable original
                    // screen. Only the pre-gap UI snapshot may be published later.
                    ContextLock lock(engine.protection.Get());float black[4]={0,0,0,1};context->ClearRenderTargetView(rtvs[5].Get(),black);
                }
                int index=(frames-1)%3;
                {ContextLock lock(engine.protection.Get());float color[4]={.05f+float(frames%32)/200,.08f,.12f,1};context->ClearRenderTargetView(worldTargets[index].Get(),color);}
                p.textures[0]=worldRing[index].texture.Get();
                if(costStage>=3) {Sleep(2);gapOutputs+=engine.outputs-beforeGap;engine.beginFrame(false);}
            }
            engine.publish(p,images[5].texture.Get());
            if(mode>=2) engine.drawReal();
            if(costStage && mode>=2 && frames%50==0) {
                ComPtr<ID3D11Texture2D> back,sample;check(swap->GetBuffer(0,IID_PPV_ARGS(&back)));
                D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.Usage=D3D11_USAGE_STAGING;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;check(d->CreateTexture2D(&td,nullptr,&sample));
                ContextLock lock(engine.protection.Get());D3D11_BOX box{1720,720,0,1721,721,1};context->CopySubresourceRegion(sample.Get(),0,0,0,0,back.Get(),0,&box);
                D3D11_MAPPED_SUBRESOURCE mapped{};check(context->Map(sample.Get(),0,D3D11_MAP_READ,0,&mapped));auto pixel=static_cast<BYTE*>(mapped.pData);int reference=frames>1?frames-1:frames;int expected=int((.05+double(reference%32)/200)*255+.5);
                if(abs(int(pixel[0])-expected)>1 || abs(int(pixel[1])-20)>1 || abs(int(pixel[2])-31)>1) ++pixelErrors;context->Unmap(sample.Get(),0);
            }
            check(engine.present()); engine.recordOff(p,S_OK);
            { ContextLock lock(engine.protection.Get()); D3D11_VIEWPORT vp{}; UINT count=1; context->RSGetViewports(&count,&vp); ComPtr<ID3D11RenderTargetView> target; context->OMGetRenderTargets(1,&target,nullptr);
              ComPtr<ID3D11PixelShader> ps; context->PSGetShader(&ps,nullptr,nullptr); ComPtr<ID3D11Buffer> cb; context->PSGetConstantBuffers(0,1,&cb);
              if(count!=1 || vp.TopLeftX!=17 || vp.TopLeftY!=29 || vp.Width!=257 || vp.Height!=193 || target.Get()!=rtvs[0].Get() || ps.Get()!=producerPS.Get() || cb.Get()!=producerCB.Get()) ++stateErrors;
            }
            engine.endFrame();
            if(engine.error) break;
        }
        engine.stop(); engine.save();
        { ContextLock lock(engine.protection.Get()); D3D11_TEXTURE2D_DESC td{}; images[0].texture->GetDesc(&td); td.Usage=D3D11_USAGE_STAGING; td.BindFlags=0; td.CPUAccessFlags=D3D11_CPU_ACCESS_READ; ComPtr<ID3D11Texture2D> readback; check(d->CreateTexture2D(&td,nullptr,&readback)); context->CopyResource(readback.Get(),images[0].texture.Get()); D3D11_MAPPED_SUBRESOURCE mapped{}; check(context->Map(readback.Get(),0,D3D11_MAP_READ,0,&mapped)); auto pixel=static_cast<unsigned char*>(mapped.pData)+60*mapped.RowPitch+100*4; int expected=int((.05+double(frames%32)/200)*255+.5); if(abs(int(pixel[0])-expected)>1 || abs(int(pixel[1])-20)>1 || abs(int(pixel[2])-31)>1) ++pixelErrors; context->Unmap(readback.Get(),0); }
        ComPtr<ID3D11InfoQueue> info; unsigned long long serious=0;
        if(SUCCEEDED(d.As(&info))) for(UINT64 i=0;i<info->GetNumStoredMessages();i++) { SIZE_T n=0; info->GetMessage(i,nullptr,&n); std::vector<char> bytes(n); auto m=reinterpret_cast<D3D11_MESSAGE*>(bytes.data()); info->GetMessage(i,m,&n); if(m->Severity==D3D11_MESSAGE_SEVERITY_ERROR || m->Severity==D3D11_MESSAGE_SEVERITY_CORRUPTION) ++serious; }
        printf("swapchains=1 debug_layer=%d frames=%d outputs=%llu producer_state_errors=%d pixel_errors=%d debug_errors=%llu pause_outputs=%llu worker_error=%d device_removed=0x%08lX\n",debug?1:0,frames,engine.outputs.load(),stateErrors,pixelErrors,serious,afterPause-beforePause,engine.error.load(),static_cast<unsigned long>(d->GetDeviceRemovedReason()));
        printf("private_world_gap_outputs=%llu\n",gapOutputs);
        FILE* f=nullptr; _wfopen_s(&f,(path+L"/check.txt").c_str(),L"wb"); if(f) { fprintf(f,"swapchains=1 debug_layer=%d frames=%d outputs=%llu state_errors=%d pixel_errors=%d debug_errors=%llu pause_outputs=%llu worker_error=%d device_removed=%ld frame_begins=%llu frame_ends=%llu\n",debug?1:0,frames,engine.outputs.load(),stateErrors,pixelErrors,serious,afterPause-beforePause,engine.error.load(),long(d->GetDeviceRemovedReason()),engine.frameBegins.load(),engine.frameEnds.load()); fclose(f); }
        engine.release(); DestroyWindow(window); return stateErrors||pixelErrors||serious||engine.error||(mode>=2 && afterPause<=beforePause)||(costStage>=3 && mode>=2 && !gapOutputs)?1:0;
    } catch(const std::exception& e) { fprintf(stderr,"%s\n",e.what()); return 1; }
}
