// Research GPU registration fixture. No swapchain, Unity or game installation.
#include "native/outside.h"
using namespace outside;
int wmain(int argc,wchar_t** argv) {
    try {
        ComPtr<ID3D11Device> device; ComPtr<ID3D11DeviceContext> context;
        check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&device,nullptr,&context));
        ImageMotionGate gate; gate.initialize(device.Get());
        constexpr unsigned w=256,h=144;
        auto pattern=[](unsigned x,unsigned y) { unsigned v=x*37217+y*159733+19; v^=v>>13; v*=1274126177; v^=v>>16; return 40+(v%140); };
        auto upload=[&](const std::vector<UINT>& pixels) {
            Texture t; D3D11_TEXTURE2D_DESC td{}; td.Width=w; td.Height=h; td.MipLevels=td.ArraySize=1; td.Format=DXGI_FORMAT_R8G8B8A8_UNORM; td.SampleDesc.Count=1; td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
            D3D11_SUBRESOURCE_DATA initial{pixels.data(),w*4,0};
            check(device->CreateTexture2D(&td,&initial,&t.texture)); check(device->CreateShaderResourceView(t.texture.Get(),nullptr,&t.view)); return t;
        };
        auto make=[&](int shift,bool brightness,bool flat) {
            std::vector<UINT> pixels(w*h);
            for(unsigned y=0;y<h;y++) for(unsigned x=0;x<w;x++) {
                UINT g=flat?128:pattern(x+shift,y); if(brightness) g=UINT(g*.6+40);
                pixels[y*w+x]=0xff000000u|(g<<16)|(g<<8)|g;
            }
            return upload(pixels);
        };
        unsigned long long seq=0;
        auto test=[&](const char* name,int shift,bool brightness,bool flat,int expected,bool flip) {
            Slot a,b; a.packet.pose.camera[2]=b.packet.pose.camera[2]=10;
            b.packet.pose.camera[0]=8.f*20/h; b.packet.flip=flip?1:0;
            a.images[0]=make(0,false,flat); b.images[0]=make(shift,brightness,flat);
            if(!gate.submit(device.Get(),context.Get(),++seq,a,b,w,h)) throw std::runtime_error("fixture submit unexpectedly busy");
            ComPtr<ID3D11ComputeShader> restored; context->CSGetShader(&restored,nullptr,nullptr);
            if(restored) throw std::runtime_error("compute state was not restored");
            context->Flush(); double end=now()+2; MotionDecision d;
            do { d=gate.decision(context.Get(),seq); if(!d.reason) Sleep(1); } while(!d.reason && now()<end);
            printf("%s flip=%d reason=%d textured=%d agree=%d veto=%d same=%.6f warp=%.6f\n",name,flip?1:0,d.reason,d.textured,d.agree,d.veto,d.same,d.warp);
            if(d.reason!=expected) throw std::runtime_error("image gate classification mismatch");
        };
        for(bool flip:{false,true}) {
            test("screen_fixed",0,false,false,2,flip);
            test("world_translation",8,false,false,3,flip);
            test("world_translation_brightness",8,true,false,3,flip);
            test("screen_fixed_brightness",0,true,false,2,flip);
            test("flat_uncertain",0,false,true,1,flip);
        }
        Pose a{},b{}; a.camera[2]=b.camera[2]=10; b.camera[0]=8.f*20/h;
        if(fabs(motionBound(a,b,w,h)-8)>1e-5) throw std::runtime_error("translation bound mismatch");
        b.camera[0]=40.f*20/h; if(motionBound(a,b,w,h)<=16) throw std::runtime_error("jump not bounded");
        b.camera[2]=0; if(std::isfinite(motionBound(a,b,w,h))) throw std::runtime_error("invalid zoom not bounded");
        if(gate.decision(context.Get(),seq+100).reason!=0) throw std::runtime_error("stale sequence was accepted");
        MotionClip clip; clip.initialize(device.Get(),w,h); auto image=make(0,false,false);
        D3D11_VIEWPORT original{17,29,257,193,0,1}; context->RSSetViewports(1,&original);
        if(!clip.submit(device.Get(),context.Get(),image.texture.Get(),nullptr,nullptr,0,0,true,1,20)) throw std::runtime_error("clip fixture rejected first frame");
        D3D11_VIEWPORT restored{}; UINT count=1; context->RSGetViewports(&count,&restored);
        if(count!=1 || memcmp(&original,&restored,sizeof(original))) throw std::runtime_error("clip state changed");
        clip.save(context.Get(),argc>1?argv[1]:L".");
        for(bool flip:{false,true}) {
            std::vector<UINT> world(w*h),screen(w*h),mask(w*h),actual(w*h);
            for(unsigned y=0;y<h;y++) for(unsigned x=0;x<w;x++) {
                UINT g=pattern(x,y),color=0xff000000u|(g<<16)|(g<<8)|g;
                bool inside=x>=24 && x<w-24 && y>=12 && y<h-12;
                bool ui=x>=8 && x<20 && y>=6 && y<18;
                unsigned at=y*w+x,textureAt=(flip?h-1-y:y)*w+x;
                world[textureAt]=color; screen[at]=ui?0xff0000ffu:color;
                mask[textureAt]=inside?0xffffffffu:0xff000000u;
                actual[at]=ui?0xff0000ffu:inside?color:0xff000000u;
            }
            Slot source; source.packet.capture=16; source.packet.flip=flip?1:0;
            source.images[0]=upload(world);source.images[1]=upload(screen);source.images[2]=upload(mask);
            auto actualTexture=upload(actual); MotionClip masked;masked.initialize(device.Get(),w,h);
            if(!masked.submit(device.Get(),context.Get(),actualTexture.texture.Get(),&source,&source,0,flip?4:2,false,2,20)) throw std::runtime_error("masked clip fixture submit failed");
            masked.save(context.Get(),argc>1?argv[1]:L".");
        }
        printf("cases=10 state_errors=0 bound_checks=3 stale_sequence_rejected=1 device_removed=%ld\n",long(device->GetDeviceRemovedReason()));
        return 0;
    } catch(const std::exception& ex) { fprintf(stderr,"%s\n",ex.what()); return 1; }
}
