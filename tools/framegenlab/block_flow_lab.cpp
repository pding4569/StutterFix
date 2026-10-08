// Independent GPU fixture: exact known translation, stationary region, true UI/planet box.
#include "native/outside.h"
using namespace outside;
int wmain(int argc,wchar_t** argv) {
    try {
        constexpr unsigned w=640,h=384;
        const int variant=argc>2?_wtoi(argv[2]):0;
        if(variant<0 || variant>4) throw std::runtime_error("unsupported fixture variant");
        ComPtr<ID3D11Device> d;ComPtr<ID3D11DeviceContext> c;check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,nullptr,&c));
        auto pattern=[](int x,int y) {UINT a=UINT(x/8)*1299827u+UINT(y/8)*738563u+19;a^=a>>13;a*=1274126177u;a^=a>>16;return 40+a%160;};
        auto texture=[&](int shift,bool screen,bool flat,bool flip) {Texture t;std::vector<UINT> p(w*h);for(unsigned y=0;y<h;y++)for(unsigned x=0;x<w;x++) {UINT a=flat?96:pattern(int(x)-(x<w/2?shift:0),int(y));UINT color=0xff000000u|(a<<16)|(a<<8)|a;if(screen && x>=32 && x<64 && y>=32 && y<64)color=0xffff0040u;p[(flip?h-1-y:y)*w+x]=color;}D3D11_TEXTURE2D_DESC td{};td.Width=w;td.Height=h;td.MipLevels=td.ArraySize=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;D3D11_SUBRESOURCE_DATA data{p.data(),w*4,0};check(d->CreateTexture2D(&td,&data,&t.texture));check(d->CreateShaderResourceView(t.texture.Get(),nullptr,&t.view));return t;};
        for(int movement:{-16,0,16}) for(bool flip:{false,true}) for(bool flat:{false,true}) {
            Slot a,b;a.sequence=1;b.sequence=2;b.packet.flip=flip?1:0;b.packet.capture=64|4|(variant<<7);
            a.images[0]=texture(0,false,flat,flip);b.images[0]=texture(movement,false,flat,flip);a.images[1]=texture(0,true,flat,false);b.images[1]=texture(movement,true,flat,false);
            float box[4]={.2f,.2f,.3f,.4f};memcpy(a.packet.textures+3,box,16);memcpy(b.packet.textures+3,box,16);
            D3D11_TEXTURE2D_DESC td{};td.Width=w;td.Height=h;td.MipLevels=td.ArraySize=1;td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
            ComPtr<ID3D11Texture2D> output;ComPtr<ID3D11RenderTargetView> target;check(d->CreateTexture2D(&td,nullptr,&output));check(d->CreateRenderTargetView(output.Get(),nullptr,&target));
            BlockFlow flow;
            if(variant>=3) flow.initialize(d.Get(),w,h,variant);
            else flow.prepare(d.Get(),c.Get(),a,b,w,h);
            for(double phase:{0.,.25,.5,.75,1.}) {
            D3D11_VIEWPORT original{11,13,101,103,0,1};c->RSSetViewports(1,&original);
            if(variant>=3 && phase>0 && phase<1) flow.prepare(d.Get(),c.Get(),a,b,w,h,variant!=4);
            flow.draw(c.Get(),target.Get(),a,b,phase,nullptr,phase>0 && phase<1);
            D3D11_VIEWPORT restored{};UINT n=1;c->RSGetViewports(&n,&restored);if(n!=1 || memcmp(&original,&restored,sizeof(original)))throw std::runtime_error("producer state changed");
            td.Usage=D3D11_USAGE_STAGING;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;td.BindFlags=0;ComPtr<ID3D11Texture2D> read;check(d->CreateTexture2D(&td,nullptr,&read));c->CopyResource(read.Get(),output.Get());D3D11_MAPPED_SUBRESOURCE m{};check(c->Map(read.Get(),0,D3D11_MAP_READ,0,&m));
            unsigned checked=0,errors=0;double absolute=0;for(unsigned y=80;y<h-80;y++)for(unsigned x=80;x<w-80;x++) {
                if(x>=w/2-64 && x<w/2+64)continue;
                bool held=(x+.5f)/w>=.2f && (x+.5f)/w<=.3f && (y+.5f)/h>=.2f && (y+.5f)/h<=.4f;
                int shift=held?(phase<.5?0:movement):int(movement*phase);UINT expected=flat?96:pattern(int(x)-(x<w/2?shift:0),int(y));auto pixel=static_cast<const BYTE*>(m.pData)+y*m.RowPitch+x*4;int error=abs(int(pixel[0])-int(expected));absolute+=error;errors+=error>3;++checked;
            }
            unsigned uiErrors=0;for(unsigned y=32;y<64;y++)for(unsigned x=32;x<64;x++) {auto p=static_cast<const BYTE*>(m.pData)+y*m.RowPitch+x*4;uiErrors+=p[0]!=64 || p[1]!=0 || p[2]!=255;}
            c->Unmap(read.Get(),0);printf("movement=%d flip=%d flat=%d phase=%.2f checked=%u errors_above3=%u mean_error=%.6f ui_errors=%u state_errors=0\n",movement,flip?1:0,flat?1:0,phase,checked,errors,absolute/checked,uiErrors);
            if(errors || uiErrors)throw std::runtime_error("known translation, stationary, protected planet or true UI failed");
            }
            std::wstring path=argc>1?argv[1]:L".";path+=L"/move"+std::to_wstring(movement)+L"-flip"+std::to_wstring(flip?1:0)+L"-flat"+std::to_wstring(flat?1:0);CreateDirectoryW(path.c_str(),nullptr);flow.save(c.Get(),path);
            FILE* f=nullptr;_wfopen_s(&f,(path+L"/block-flow.txt").c_str(),L"rb");if(!f)throw std::runtime_error("fixture counters missing");UINT generated=0,fresh=0;int fields=fscanf_s(f,"variant=%*u scale=%*u block_size=%*u skipped_pairs=%*u generated=%u new_picture=%u",&generated,&fresh);fclose(f);
            if(fields!=2 || generated!=3 || fresh!=UINT(!flat && movement?3:0))throw std::runtime_error("new-picture counter disagrees with known pixels");
        }
        return 0;
    }catch(const std::exception& e){fprintf(stderr,"%s\n",e.what());return 1;}
}
