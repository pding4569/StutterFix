#include "../../native/sfnative/direct_tracks.cpp"
#include "../../native/sfnative/direct_renderer.cpp"
using dr::matrix;
#include <cstdio>
#include <fstream>
#include <cstdlib>
static void require(bool pass,const char* why){if(!pass){std::fprintf(stderr,"FAIL %s\n",why);std::exit(1);}}
static dr::Camera identity(){dr::Camera c{};c.vp[0]=c.vp[5]=c.vp[10]=c.vp[15]=1;return c;}
static void tracksCheck(){
    dr::Tracks one(3000,64,70,1),three(3000,64,70,3);auto cam=identity();cam.vp[0]=0.08f;cam.vp[5]=0.14f;
    const double times[]={0,0.2,0.21,1,2,23.4,59.999,60,61,40,1,0,10,60};
    for(double t:times){one.start(t,cam);three.start(t,cam);one.wait();three.wait();require(one.objects.size()==three.objects.size(),"object counts");
        for(size_t n=0;n<one.objects.size();++n)require(std::memcmp(&one.objects[n].gpu,&three.objects[n].gpu,sizeof(dr::Instance))==0,"1/3 worker bit parity");
        require(one.activeCount==three.activeCount,"active parity");if(t>=60)require(one.activeCount==0,"end boundary inactive");else require(one.activeCount==2100,"animated count");
    }
    dr::Tracks gap(1,1,0,1);auto& w=gap.workers[0];dr::Track track{};track.instance=0;track.channel=0;track.segments={{1,1,2,4,dr::Linear},{3,1,4,8,dr::Linear}};w.tracks.push_back(track);w.active.reserve(1);w.events={{1,0,true},{2,0,false},{3,0,true},{4,0,false}};
    const double boundary[]={0,1,1.5,2,2.5,3,3.5,4,5,1.5,0,3.5};const float expected[]={2,2,3,4,4,4,6,8,8,3,2,6};const size_t active[]={0,1,1,0,0,1,1,0,0,1,0,1};
    for(size_t k=0;k<sizeof(boundary)/sizeof(*boundary);++k){gap.start(boundary[k],cam);gap.wait();require(gap.objects[0].channel[0]==expected[k],"seek/gap value");require(gap.activeCount==active[k],"start/end/gap active set");}
    std::puts("tracks: 3000 instances, 14 forward/backward/terminal times, 1/3 workers bit-identical; 12 gap/boundary cases passed");
    std::ofstream csv("native-ease.csv");csv.precision(9);for(int e=0;e<dr::EaseCount;++e)for(int j=0;j<=1000;++j){float t=float(j)/1000;csv<<e<<','<<j<<','<<dr::ease(e,t)<<'\n';}
}
static void stressCheck(){
    auto cam=identity();cam.vp[0]=0.08f;cam.vp[5]=0.14f;
    for(unsigned n:{3000u,30000u,100000u}){
        dr::Tracks tracks(n,64,70,3);Metric start,wall,sum;
        for(int frame=0;frame<150;++frame){double b=dr::clockUs();tracks.start(double(frame)/60,cam);double submit=dr::clockUs()-b;tracks.wait();if(frame>=30){start.add(submit);wall.add(tracks.wallUs);sum.add(tracks.sumUs);}}
        size_t drawn=0;for(auto& w:tracks.workers)drawn+=w.visible.size();
        std::printf("offline CPU stress N=%u workers=%zu anim=70 active=%llu visible=%zu start_us_mean=%.3f p95=%.3f worker_wall_us_mean=%.3f p95=%.3f worker_sum_us_mean=%.3f p95=%.3f (not game timing)\n",n,tracks.workers.size(),static_cast<unsigned long long>(tracks.activeCount),drawn,start.get(false),start.get(true),wall.get(false),wall.get(true),sum.get(false),sum.get(true));
    }
}
static void pixelCheck(){
    ComPtr<ID3D11Device> d;ComPtr<ID3D11DeviceContext> c;check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,nullptr,&c));
    uint32_t colors[4]={0xff0000ff,0xff00ff00,0xffff0000,0x80ffffff};D3D11_SUBRESOURCE_DATA data[4]{};for(unsigned k=0;k<4;++k){data[k].pSysMem=&colors[k];data[k].SysMemPitch=4;}
    D3D11_TEXTURE2D_DESC desc{};desc.Width=desc.Height=1;desc.MipLevels=1;desc.ArraySize=4;desc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;desc.SampleDesc.Count=1;desc.Usage=D3D11_USAGE_IMMUTABLE;desc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture2D> array;check(d->CreateTexture2D(&desc,data,&array));desc.Width=desc.Height=16;desc.ArraySize=1;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_RENDER_TARGET;
    ComPtr<ID3D11Texture2D> output;check(d->CreateTexture2D(&desc,nullptr,&output));ComPtr<ID3D11RenderTargetView> view;check(d->CreateRenderTargetView(output.Get(),nullptr,&view));float clear[4]{};c->ClearRenderTargetView(view.Get(),clear);
    // Seed non-default immediate state; ExecuteCommandList(TRUE) must preserve it.
    c->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_LINELIST);D3D11_VIEWPORT oldViewport{2,3,7,9,0.2f,0.8f};c->RSSetViewports(1,&oldViewport);auto* oldOutput=view.Get();c->OMSetRenderTargets(1,&oldOutput,nullptr);
    int id=sf_dr_create(4,4,0,3,array.Get());require(id!=0,"create renderer");
    for(unsigned k=0;k<4;++k){auto& o=renderer->tracks.objects[k];std::fill_n(o.channel,9,0.0f);o.channel[0]=(k&1)?0.5f:-0.5f;o.channel[1]=(k&2)?-0.5f:0.5f;o.channel[3]=o.channel[4]=1;for(unsigned j=5;j<9;++j)o.channel[j]=1;matrix(o);}
    auto cam=identity();require(sf_dr_submit(id,0,&cam,output.Get())==1,"first submit");event(id);
    require(sf_dr_stat(id,5)==0,"render errors");require(sf_dr_stat(id,1)==4&&sf_dr_stat(id,2)==1,"4 quads one draw");
    D3D11_PRIMITIVE_TOPOLOGY topology{};c->IAGetPrimitiveTopology(&topology);require(topology==D3D11_PRIMITIVE_TOPOLOGY_LINELIST,"topology restored");D3D11_VIEWPORT actual{};UINT count=1;c->RSGetViewports(&count,&actual);require(std::memcmp(&actual,&oldViewport,sizeof(actual))==0,"viewport restored");ComPtr<ID3D11RenderTargetView> restored;c->OMGetRenderTargets(1,&restored,nullptr);require(restored==view,"RT restored");
    desc.Usage=D3D11_USAGE_STAGING;desc.BindFlags=0;desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;ComPtr<ID3D11Texture2D> read;check(d->CreateTexture2D(&desc,nullptr,&read));c->CopyResource(read.Get(),output.Get());D3D11_MAPPED_SUBRESOURCE map{};check(c->Map(read.Get(),0,D3D11_MAP_READ,0,&map));
    unsigned maxDelta=0,changed=0;double total=0;for(unsigned y=0;y<16;++y)for(unsigned x=0;x<16;++x){unsigned k=(x>=8?1u:0u)+(y>=8?2u:0u);const unsigned char* p=static_cast<unsigned char*>(map.pData)+y*map.RowPitch+x*4;unsigned expected[4]{};
        if(k==3)std::fill_n(expected,4,128u);else{expected[k]=255;expected[3]=255;}
        for(unsigned j=0;j<4;++j){unsigned delta=unsigned(std::abs(int(p[j])-int(expected[j])));maxDelta=(std::max)(maxDelta,delta);total+=delta;if(delta)++changed;}}
    c->Unmap(read.Get(),0);std::printf("WARP 4 quads: max_channel_delta=%u mean_channel_delta=%.9f changed_channels=%u/1024\n",maxDelta,total/1024,changed);require(maxDelta<=1,"expected RGBA pixels");
    for(int k=0;k<8;++k)require(sf_dr_submit(id,double(k),&cam,output.Get())==1,"eight in-flight camera snapshots queued");require(sf_dr_submit(id,9,&cam,output.Get())==0,"bounded queue overflow visible");for(int k=0;k<8;++k)event(id);require(sf_dr_stat(id,0)==9,"every queued frame rendered");
    // Resize/rebind, invisibility compaction, stale queued events, and fresh lease.
    auto& invisible=renderer->tracks.objects[0];invisible.channel[8]=0;matrix(invisible);renderer->tracks.objects[1].channel[0]=100;matrix(renderer->tracks.objects[1]);
    desc.Width=desc.Height=8;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_RENDER_TARGET;desc.CPUAccessFlags=0;ComPtr<ID3D11Texture2D> resized;check(d->CreateTexture2D(&desc,nullptr,&resized));require(sf_dr_submit(id,2,&cam,resized.Get())==1,"resize submit");event(id);require(sf_dr_stat(id,1)==2,"alpha and viewport cull");require(renderer->targetDesc.Width==8,"RTV rebind");sf_dr_destroy(id);event(id);int fresh=sf_dr_create(4,4,0,1,array.Get());require(fresh!=0&&fresh!=id,"fresh event token");event(id);require(sf_dr_stat(fresh,0)==0,"stale event ignored");sf_dr_destroy(fresh);
    std::puts("D3D11 state restore, resize RTV, alpha/offscreen cull, eight queued frames/overflow, stale callback and release passed");
    // Unity can expose typeless resources, and an array of length one still
    // needs a Texture2DArray SRV rather than the default Texture2D view.
    uint32_t grey=0xff808080;D3D11_SUBRESOURCE_DATA greyData{&grey,4,0};D3D11_TEXTURE2D_DESC single{};single.Width=single.Height=single.MipLevels=single.ArraySize=single.SampleDesc.Count=1;single.Format=DXGI_FORMAT_R8G8B8A8_TYPELESS;single.Usage=D3D11_USAGE_IMMUTABLE;single.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture2D> greyArray;check(d->CreateTexture2D(&single,&greyData,&greyArray));single.Width=single.Height=8;single.Usage=D3D11_USAGE_DEFAULT;single.BindFlags=D3D11_BIND_RENDER_TARGET;ComPtr<ID3D11Texture2D> srgbTarget;check(d->CreateTexture2D(&single,nullptr,&srgbTarget));
    int gamma=sf_dr_create(1,1,0,1,greyArray.Get());require(gamma!=0,"typeless single-layer creation");auto& full=renderer->tracks.objects[0];full.channel[0]=full.channel[1]=full.channel[2]=0;full.channel[3]=full.channel[4]=2;for(int j=5;j<9;++j)full.channel[j]=1;matrix(full);auto unchanged=full.gpu;
    auto moved=identity();moved.position[0]=7;moved.position[3]=1;moved.vp[12]=-7;require(sf_dr_submit(gamma,0,&moved,srgbTarget.Get())==1,"camera translation submit");event(gamma);require(sf_dr_stat(gamma,5)==0,"typeless render success");require(std::memcmp(&unchanged,&full.gpu,sizeof(unchanged))==0,"camera move requires zero instance rewrites");
    D3D11_SHADER_RESOURCE_VIEW_DESC arrayDesc{};renderer->textures->GetDesc(&arrayDesc);require(arrayDesc.ViewDimension==D3D11_SRV_DIMENSION_TEXTURE2DARRAY,"one layer explicit array SRV");
    single.Usage=D3D11_USAGE_STAGING;single.BindFlags=0;single.CPUAccessFlags=D3D11_CPU_ACCESS_READ;ComPtr<ID3D11Texture2D> gammaRead;check(d->CreateTexture2D(&single,nullptr,&gammaRead));c->CopyResource(gammaRead.Get(),srgbTarget.Get());check(c->Map(gammaRead.Get(),0,D3D11_MAP_READ,0,&map));const auto* greyPixel=static_cast<const unsigned char*>(map.pData)+4*map.RowPitch+4*4;
    for(unsigned j=0;j<4;++j)require(std::abs(int(greyPixel[j])-(j==3?255:128))<=1,"sRGB sample/write roundtrip");c->Unmap(gammaRead.Get(),0);sf_dr_destroy(gamma);
    std::puts("typeless sRGB RT/array, M=1 array SRV, camera translation without instance rewrites passed");
}
int main(){try{tracksCheck();pixelCheck();stressCheck();std::puts("PASS");return 0;}catch(const std::exception& e){std::fprintf(stderr,"FAIL exception %s\n",e.what());return 1;}}
