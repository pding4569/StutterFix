// Cached-list resource lifetime, target replacement, failure and fallback.
#define main drfull_base_entry
#include "drfull-warp.cpp"
#undef main
int main(){try{
    ComPtr<ID3D11Device> d;ComPtr<ID3D11DeviceContext> c;D3D_FEATURE_LEVEL fl{};
    check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c));
    D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=32;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
    ComPtr<ID3D11Texture2D> target;ComPtr<ID3D11RenderTargetView> rt;check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
    auto bind=[&](){auto* r=rt.Get();c->OMSetRenderTargets(1,&r,nullptr);D3D11_VIEWPORT v{0,0,float(td.Width),float(td.Height),0,1};c->RSSetViewports(1,&v);c->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_LINESTRIP);};bind();
    const char code[]="float4 vs(float3 p:POSITION):SV_Position{return float4(p,1);}cbuffer C:register(b0){float4 color;}float4 ps():SV_Target{return color;}";
    ComPtr<ID3DBlob> vs,ps,error;check(D3DCompile(code,sizeof(code)-1,nullptr,nullptr,nullptr,"vs","vs_4_0",0,0,&vs,&error));check(D3DCompile(code,sizeof(code)-1,nullptr,nullptr,nullptr,"ps","ps_4_0",0,0,&ps,&error));
    ProgramIn program{};program.vs=vs->GetBufferPointer();program.vsBytes=UINT(vs->GetBufferSize());program.ps=ps->GetBufferPointer();program.psBytes=UINT(ps->GetBufferSize());program.state[0]=program.state[2]=program.state[7]=1;program.state[6]=15;program.state[14]=program.state[15]=255;program.state[20]=program.state[24]=program.state[28]=8;
    float triangle[]={-.7f,.7f,.5f,.7f,.7f,.5f,-.7f,-.7f,.5f};UINT indices[]={0,1,2};auto vb=buffer(d.Get(),triangle,sizeof(triangle),D3D11_BIND_VERTEX_BUFFER),ib=buffer(d.Get(),indices,sizeof(indices),D3D11_BIND_INDEX_BUFFER);Attribute attr{0,0,6,0,0};
    GeometryIn geometry{};geometry.buffers[0]=vb.Get();geometry.strides[0]=12;geometry.indices=ib.Get();geometry.indexFormat=42;geometry.indexCount=3;geometry.attributes=&attr;geometry.attributeCount=1;
    CreateIn create{target.Get(),128,1};int token=dispatch(Create,0,&create);CacheConfiguration cache{1,16};dispatch(CacheConfigure,token,&cache);
    float red[]={1,0,0,1},green[]={0,1,0,1};Buffer cb{1,0,16,0};DrawIn draw{};draw.program=UINT(dispatch(Program,token,&program));draw.geometry=UINT(dispatch(Geometry,token,&geometry));draw.constants=red;draw.constantBytes=16;draw.buffers=&cb;draw.bufferCount=1;
    UINT list=UINT(dispatch(Draw,token,&draw)),poseId=0,segments[]={0,1},zero=0;Pose pose{};for(UINT i=0;i<16;i++)pose.world[i]=pose.inverse[i]=i%5==0?1.f:0.f;
    Frame f{};for(UINT i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=&pose;f.poseIds=&poseId;f.poseCount=1;f.list=&list;f.listCount=1;f.segments=segments;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
    auto event=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(Event,0,nullptr));
    auto submit=[&](){float bg[]={0,0,0,1};c->ClearRenderTargetView(rt.Get(),bg);auto before=pipelineState(c.Get());dispatch(CommitPacked,token,&f);event(token<<16);f.listChanged=0;require(pipelineState(c.Get())==before,"cached lifecycle state restoration");return readback(d.Get(),c.Get(),target.Get());};
    auto original=submit();submit();draw.constants=green;DrawReplacement replacement{list,draw};UINT next=UINT(dispatch(ReplaceDraw,token,&replacement));
    require(submit()==original,"replacement retired a still-published list");ResourcePerformance resource{};dispatch(ResourcePerf,token,&resource);require(resource.retiredDraws==0,"premature cached retirement");
    list=next;f.listChanged=1;auto replaced=submit();require(replaced!=original,"replacement not replayed");dispatch(ResourcePerf,token,&resource);require(resource.retiredDraws==1,"cached retirement missing");
    CachePerformance warm{};dispatch(CachePerf,token,&warm);
    td.Width=48;target.Reset();rt.Reset();check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));bind();submit();
    CachePerformance resized{};dispatch(CachePerf,token,&resized);require(resized.records==warm.records+1,"new target did not invalidate cached list");
    c->OMSetRenderTargets(0,nullptr,nullptr);auto before=pipelineState(c.Get());dispatch(CommitPacked,token,&f);event(token<<16);require(pipelineState(c.Get())==before,"failed cached event leaked state");
    Statistics stats{};dispatch(Stats,token,&stats);require(stats.errors==1,"invalid target not rejected");bind();submit();dispatch(Stats,token,&stats);require(stats.errors==1,"cached event did not recover");dispatch(Destroy,token,nullptr);event(token<<16);
    // An unsupported cache configuration follows the original renderer.
    create.resource=target.Get();token=dispatch(Create,0,&create);Configuration legacy{0,0};dispatch(Configure,token,&legacy);dispatch(CacheConfigure,token,&cache);draw.program=UINT(dispatch(Program,token,&program));draw.geometry=UINT(dispatch(Geometry,token,&geometry));draw.constants=red;list=UINT(dispatch(Draw,token,&draw));f.listChanged=1;
    submit();dispatch(CachePerf,token,&warm);dispatch(Stats,token,&stats);require(warm.segments==0&&warm.fallbackSegments==1&&stats.errors==0,"cache fallback failed");dispatch(Destroy,token,nullptr);
    // Canvas-only: no pose upload, no cull dispatch, no empty buffer update.
    ComPtr<ID3D11Texture2D> black,white;ComPtr<ID3D11RenderTargetView> br,wr;td.BindFlags|=D3D11_BIND_SHADER_RESOURCE;
    check(d->CreateTexture2D(&td,nullptr,&black));check(d->CreateTexture2D(&td,nullptr,&white));check(d->CreateRenderTargetView(black.Get(),nullptr,&br));check(d->CreateRenderTargetView(white.Get(),nullptr,&wr));c->ClearRenderTargetView(br.Get(),green);c->ClearRenderTargetView(wr.Get(),green);
    token=dispatch(Create,0,&create);dispatch(CacheConfigure,token,&cache);CompositeIn composite{black.Get(),white.Get()};list=UINT(dispatch(Composite,token,&composite));f.poses=nullptr;f.poseIds=nullptr;f.poseCount=0;f.listChanged=1;
    auto canvas=submit();submit();for(size_t i=0;i<canvas.size();i++)require(canvas[i]==((i%4==1||i%4==3)?255:0),"pose-free Canvas pixel mismatch");
    dispatch(CachePerf,token,&warm);dispatch(ResourcePerf,token,&resource);dispatch(Stats,token,&stats);require(stats.errors==0&&warm.records==1&&warm.replays==2&&warm.cullDispatches==0&&resource.defaultUpdates==0,"pose-free Canvas uploaded or culled");dispatch(Destroy,token,nullptr);
    std::cout<<"PASS M4l Canvas_only poses=0 cull_dispatches=0 default_updates=0 records=1 replays=2 pixels=exact state=restored\n";
    std::cout<<"PASS M4l lifecycle published_old_list=retained retired=1 target_change=re_recorded failure_state=restored recovery=PASS stale_event=inert legacy_fallback=PASS\n";return 0;
}catch(const std::exception& e){std::cerr<<e.what()<<"\n";return 1;}catch(HRESULT hr){std::cerr<<"HRESULT "<<std::hex<<hr<<"\n";return 2;}}
