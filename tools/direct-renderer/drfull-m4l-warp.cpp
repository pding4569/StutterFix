// Production M4l versus production legacy, WARP, no window or game process.
#define main drfull_legacy_fixture_entry
#include "drfull-warp.cpp"
#undef main
ComPtr<ID3DBlob> compileText(const char* text,const char* entry,const char* target){ComPtr<ID3DBlob>b,e;check(D3DCompile(text,std::strlen(text),nullptr,nullptr,nullptr,entry,target,D3DCOMPILE_IEEE_STRICTNESS,0,&b,&e));return b;}
int main(){try{
    ComPtr<ID3D11Device>d;ComPtr<ID3D11DeviceContext>c;D3D_FEATURE_LEVEL fl{};check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c));
    D3D11_TEXTURE2D_DESC td{};td.Width=96;td.Height=64;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
    ComPtr<ID3D11Texture2D>target;ComPtr<ID3D11RenderTargetView>rt;check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
    auto bind=[&](){auto* r=rt.Get();c->OMSetRenderTargets(1,&r,nullptr);D3D11_VIEWPORT v{0,0,96,64,0,1};c->RSSetViewports(1,&v);};bind();
    auto event=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(Event,0,nullptr));
    const char* source="cbuffer Data:register(b0){float4 color;column_major float4x4 world;};float4 vs(float3 p:POSITION):SV_Position{return mul(world,float4(p,1));}float4 ps():SV_Target{return color;}";
    auto vs=compileText(source,"vs","vs_4_0"),ps=compileText(source,"ps","ps_4_0");
    ProgramIn prog{};prog.vs=vs->GetBufferPointer();prog.vsBytes=UINT(vs->GetBufferSize());prog.ps=ps->GetBufferPointer();prog.psBytes=UINT(ps->GetBufferSize());prog.state[0]=prog.state[2]=prog.state[7]=1;prog.state[6]=15;prog.state[14]=prog.state[15]=255;prog.state[20]=prog.state[24]=prog.state[28]=8;
    float triangle[]={-.65f,.65f,.5f,.65f,.65f,.5f,-.65f,-.65f,.5f};UINT indices[]={0,1,2};auto vb=buffer(d.Get(),triangle,sizeof(triangle),D3D11_BIND_VERTEX_BUFFER),ib=buffer(d.Get(),indices,sizeof(indices),D3D11_BIND_INDEX_BUFFER);Attribute a{0,0,6,0,0};
    GeometryIn geo{};geo.buffers[0]=vb.Get();geo.strides[0]=12;geo.indices=ib.Get();geo.indexFormat=42;geo.indexCount=3;geo.attributes=&a;geo.attributeCount=1;
    Pose identity{};for(int i=0;i<16;i++)identity.world[i]=identity.inverse[i]=i%5==0?1.f:0.f;
    std::vector<std::vector<uint8_t>> reference;CachePerformance cached{};
    for(UINT mode=0;mode<2;mode++){
        bind();CreateIn create{target.Get(),256,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode,16};dispatch(CacheConfigure,token,&config);
        Buffer bindings[]={{0,0,80,0},{1,0,80,0}};Patch patch{16,1};float data[20]{.61f,.23f,.79f,1};DrawIn draw{};draw.program=UINT(dispatch(Program,token,&prog));draw.geometry=UINT(dispatch(Geometry,token,&geo));draw.buffers=bindings;draw.bufferCount=2;draw.patches=&patch;draw.patchCount=1;draw.constants=data;draw.constantBytes=sizeof(data);draw.extent[0]=draw.extent[1]=.66f;
        std::vector<UINT> list(96),ids(96);std::vector<Pose> poses(96,identity);std::vector<float> materials(96*20);
        for(UINT i=0;i<96;i++){data[0]=float(i%7+1)/8;data[1]=float(i%5+1)/6;std::memcpy(materials.data()+i*20,data,sizeof(data));draw.pose=i;ids[i]=i;list[i]=UINT(dispatch(Draw,token,&draw));}
        UINT segments[]={0,0,0,48,48,48},zero=0;Frame f{};for(int i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses.data();f.poseIds=ids.data();f.poseCount=96;f.list=list.data();f.listCount=96;f.segments=segments;f.segmentCount=3;f.listChanged=1;f.constantOffsets=&zero;
        auto submit=[&](){float bg[]={.1f,.2f,.3f,1};c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitPacked,token,&f);for(UINT seg=0;seg<3;seg++)event((token<<16)|seg);f.listChanged=0;Statistics stats{};dispatch(Stats,token,&stats);require(stats.errors==0,"cache native error");};
        auto pixel=[&](UINT step){auto pixels=readback(d.Get(),c.Get(),target.Get());if(mode==0)reference.push_back(pixels);else require(pixels==reference[step],"cached/legacy pixel mismatch");};
        submit();pixel(0);CachePerformance warm{};dispatch(CachePerf,token,&warm);
        for(UINT i=0;i<16;i++)submit();dispatch(CachePerf,token,&cached);if(mode){require(cached.available==1&&cached.records==6&&cached.records==warm.records&&cached.planBuilds==warm.planBuilds,"static list re-recorded");require(cached.constantBytes==warm.constantBytes,"static constants reuploaded");}
        for(auto& p:poses)p.world[12]=.13f;submit();pixel(1);dispatch(CachePerf,token,&cached);if(mode)require(cached.records==warm.records,"pose caused recording");
        poses[95].world[12]=100;submit();pixel(2);poses[95].world[12]=0;
        FrameActive active{};UINT liveId=95;uint8_t liveValue=0;active.ids=&liveId;active.values=&liveValue;active.count=1;active.frame=f;
        float bg[]={.1f,.2f,.3f,1};c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitActive,token,&active);for(UINT seg=0;seg<3;seg++)event((token<<16)|seg);pixel(3);liveValue=1;active.frame=f;dispatch(CommitActive,token,&active);for(UINT seg=0;seg<3;seg++)event((token<<16)|seg);
        UINT dirty=list[95],offsets[]={0,80};materials[95*20]=.12f;f.drawIds=&dirty;f.constantOffsets=offsets;f.dirtyCount=1;f.constants=reinterpret_cast<const uint8_t*>(materials.data()+95*20);f.constantBytes=80;submit();pixel(4);f.dirtyCount=f.constantBytes=0;f.constantOffsets=&zero;
        dispatch(CachePerf,token,&cached);if(mode)require(cached.records==warm.records,"material/live/cull caused recording");
        std::swap(list[80],list[81]);f.listChanged=1;submit();pixel(5);dispatch(CachePerf,token,&cached);if(mode)require(cached.records==warm.records+1,"local reorder did not rebuild exactly one chunk");
        // Steady producer + renderer path must perform no C++ heap allocation.
        for(UINT i=0;i<8;i++)submit();auto before=nativeHeapCalls.load();for(UINT i=0;i<32;i++)submit();auto heaps=nativeHeapCalls.load()-before;if(mode)require(heaps==0,"steady cached path allocated");
        std::cout<<"PASS M4l singles mode="<<mode<<" pixel_cases=6 records="<<cached.records<<" replays="<<cached.replays<<" cpp_heap_calls="<<heaps<<" local_reorder_records="<<(mode?1:0)<<"\n";
        dispatch(Destroy,token,nullptr);
    }
    // Verified GPU world vertices: live toggles and pose updates replay bindings.
    td.MipLevels=1;target.Reset();rt.Reset();check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
    reference.clear();
    for(UINT mode=0;mode<2;mode++){
        bind();CreateIn create{target.Get(),16,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode,16};dispatch(CacheConfigure,token,&config);
        Vertex vertices[3]{};for(UINT i=0;i<3;i++){std::memcpy(vertices[i].p,triangle+i*3,12);for(auto& color:vertices[i].color)color=1;}
        Attribute attrs[]={{0,0,6,0,0},{3,0,2,0,12},{4,0,16,0,28},{4,1,16,0,36}};GeometryIn g{};g.vertices=vertices;g.vertexBytes=sizeof(vertices);g.strides[0]=44;g.cpuIndices=indices;g.cpuIndexCount=3;g.attributes=attrs;g.attributeCount=4;
        float data[20]{.61f,.23f,.79f,1};Buffer binding{0,0,80,0};Buffer bindings[]={binding,{1,0,80,0}};Patch patch{16,1};DrawIn draw{};draw.program=UINT(dispatch(Program,token,&prog));draw.geometry=UINT(dispatch(Geometry,token,&g));draw.constants=data;draw.constantBytes=80;draw.buffers=bindings;draw.bufferCount=2;draw.patches=&patch;draw.patchCount=1;draw.sprite=2;
        UINT list[3],ids[]={0,1,2};Pose poses[]={identity,identity,identity};for(UINT i=0;i<3;i++){draw.pose=i;list[i]=UINT(dispatch(Draw,token,&draw));}
        UINT seg[]={0,3},zero=0;Frame f{};for(int i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses;f.poseIds=ids;f.poseCount=3;f.list=list;f.listCount=3;f.segments=seg;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
        CachePerformance warm{};
        for(UINT step=0;step<8;step++){for(auto& pose:poses)pose.world[12]=float(step)*.03125f;float bg[]={0,0,0,1};c->ClearRenderTargetView(rt.Get(),bg);FrameActive active{};UINT liveId=1;uint8_t liveValue=step%2?0:1;active.frame=f;active.ids=&liveId;active.values=&liveValue;active.count=1;dispatch(CommitActive,token,&active);event(token<<16);f.listChanged=0;
            Statistics stats{};dispatch(Stats,token,&stats);require(stats.errors==0,"GPU pose cache error");auto pixels=readback(d.Get(),c.Get(),target.Get());if(mode==0)reference.push_back(pixels);else require(pixels==reference[step],"GPU pose parity");if(step==0)dispatch(CachePerf,token,&warm);}
        dispatch(CachePerf,token,&cached);if(mode)require(cached.records==1&&cached.records==warm.records&&cached.constantBytes==warm.constantBytes,"GPU pose/live invalidated commands or normalized CB");
        std::cout<<"PASS M4l GPU_pose mode="<<mode<<" cases=8 parity=exact records="<<cached.records<<" constants_pose_upload=0\n";dispatch(Destroy,token,nullptr);
    }
    // Named snapshot chooses first VISIBLE occurrence, including across segments.
    auto grabPs=compileText("Texture2D<float4> grab:register(t127);float4 ps(float4 p:SV_Position):SV_Target{return grab.Load(int3(int2(p.xy),0))*.7;}","ps","ps_5_0");
    reference.clear();
    for(UINT named:{1u,2u})for(UINT mode=0;mode<2;mode++){
        bind();CreateIn create{target.Get(),16,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode,16};dispatch(CacheConfigure,token,&config);
        float data[20]{.61f,.23f,.79f,1};Buffer binding{0,0,80,0};Buffer bindings[]={binding,{1,0,80,0}};Patch patch{16,1};DrawIn draw{};draw.geometry=UINT(dispatch(Geometry,token,&geo));draw.constants=data;draw.constantBytes=80;draw.buffers=bindings;draw.bufferCount=2;draw.patches=&patch;draw.patchCount=1;draw.extent[0]=draw.extent[1]=.66f;UINT ordinary=UINT(dispatch(Program,token,&prog));auto grabProg=prog;grabProg.ps=grabPs->GetBufferPointer();grabProg.psBytes=UINT(grabPs->GetBufferSize());UINT grab=UINT(dispatch(Program,token,&grabProg));
        Texture texture{};texture.srgb=-int(named);texture.stage=1;texture.slot=127;texture.sampler=-1;texture.aniso=1;
        UINT list[3],ids[]={0,1,2};Pose poses[]={identity,identity,identity};for(UINT i=0;i<3;i++){draw.pose=i;draw.program=i==1?ordinary:grab;draw.grab=i==1?0:named;draw.textures=i==1?nullptr:&texture;draw.textureCount=i==1?0:1;list[i]=UINT(dispatch(Draw,token,&draw));}
        UINT seg[]={0,1,1,2},zero=0;Frame f{};for(int i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses;f.poseIds=ids;f.poseCount=3;f.list=list;f.listCount=3;f.segments=seg;f.segmentCount=2;f.listChanged=1;f.constantOffsets=&zero;
        for(UINT step=0;step<4;step++){poses[0].world[12]=step%2?0.f:100.f;float bg[]={.8f-float(step)*.1f,.6f,.4f+float(step)*.1f,1};c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitPacked,token,&f);event(token<<16);event((token<<16)|1);f.listChanged=0;Statistics stats{};dispatch(Stats,token,&stats);require(stats.errors==0,"Grab cache error");auto pixels=readback(d.Get(),c.Get(),target.Get());if(mode==0)reference.push_back(pixels);else require(pixels==reference[(named-1)*4+step],"predicated Grab pixel parity");}
        dispatch(CachePerf,token,&cached);GpuPerformance gp{};dispatch(GpuPerf,token,&gp);require(gp.grabCopies==(named==1?6u:4u),"Grab copy missing or named copied twice");if(mode)require(cached.records==3,"Grab pose caused recording");std::cout<<"PASS M4l Grab name="<<named<<" mode="<<mode<<" first_visible=CPU_barrier cross_segment=True parity=exact records="<<cached.records<<"\n";dispatch(Destroy,token,nullptr);
    }
    // Full-copy oracle versus verified rectangles, deliberately wrong bounds,
    // unreadable external geometry, and an unknown screen-sampling contract.
    // Change the background every frame: a stale snapshot must fail parity.
    reference.clear();
    for(UINT mode=0;mode<6;mode++){
        td.MipLevels=mode==5?2u:1u;target.Reset();rt.Reset();check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
        bind();CreateIn create{target.Get(),16,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode?1u:0u,16};dispatch(CacheConfigure,token,&config);
        Vertex vertices[3]{};for(UINT i=0;i<3;i++){std::memcpy(vertices[i].p,triangle+i*3,12);for(auto& color:vertices[i].color)color=1;}
        Attribute attrs[]={{0,0,6,0,0},{3,0,2,0,12},{4,0,16,0,28},{4,1,16,0,36}};
        GeometryIn g{};g.vertices=vertices;g.vertexBytes=sizeof(vertices);g.strides[0]=44;g.cpuIndices=indices;g.cpuIndexCount=3;g.attributes=attrs;g.attributeCount=4;
        auto grabProg=prog;grabProg.ps=grabPs->GetBufferPointer();grabProg.psBytes=UINT(grabPs->GetBufferSize());
        float data[20]{1,1,1,1};Buffer bindings[]={{0,0,80,0},{1,0,80,0}};Patch patch{16,1};Texture texture{};texture.srgb=-1;texture.stage=1;texture.slot=127;texture.sampler=-1;texture.aniso=1;
        DrawIn draw{};draw.program=UINT(dispatch(Program,token,&grabProg));draw.geometry=UINT(dispatch(Geometry,token,mode==3?&geo:&g));draw.constants=data;draw.constantBytes=80;draw.buffers=bindings;draw.bufferCount=2;draw.patches=&patch;draw.patchCount=1;draw.textures=&texture;draw.textureCount=1;draw.grab=1;draw.sprite=mode>0&&mode!=4?4u:0u;draw.extent[0]=draw.extent[1]=mode==2?.1f:.66f;
        UINT list=UINT(dispatch(Draw,token,&draw)),ids=0,seg[]={0,1},zero=0;Pose pose=identity;
        Frame f{};for(int i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=&pose;f.poseIds=&ids;f.poseCount=1;f.list=&list;f.listCount=1;f.segments=seg;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
        for(UINT step=0;step<8;step++){
            pose.world[12]=float(step)*.01f;f.vp[5]=step%2?-1.f:1.f;float bg[]={.8f-float(step)*.07f,.6f,.2f+float(step)*.07f,1};c->ClearRenderTargetView(rt.Get(),bg);
            auto saved=pipelineState(c.Get());dispatch(CommitPacked,token,&f);event(token<<16);f.listChanged=0;
            require(pipelineState(c.Get())==saved,"cached pipeline restoration failed");Statistics stats{};dispatch(Stats,token,&stats);require(stats.errors==0,"rectangle cache error");
            auto pixels=readback(d.Get(),c.Get(),target.Get());if(mode==0)reference.push_back(pixels);else require(pixels==reference[step],"partial/full snapshot mismatch");
        }
        CachePerformance cp{};GpuPerformance gp{};dispatch(CachePerf,token,&cp);dispatch(GpuPerf,token,&gp);require(gp.grabCopies==8,"rectangle copy counter missing");
        if(mode==1)require(cp.rectCopies==8&&cp.rectFallbacks==0&&gp.grabBytes<8ull*96*64*4,"rectangle optimization untested");
        if(mode==2)require(cp.rectMismatches==8&&cp.rectFallbacks==8,"bad bounds not rejected");
        if(mode>=3)require(cp.rectFallbacks==8&&gp.grabBytes==8ull*96*64*4,"unknown geometry/contract not full copy");
        if(mode)require(cp.records==1,"rectangle motion re-recorded commands");
        std::cout<<"PASS M4l rectangle mode="<<mode<<" copies="<<gp.grabCopies<<" bytes="<<gp.grabBytes<<" accepted="<<cp.rectCopies<<" mismatches="<<cp.rectMismatches<<" fallback="<<cp.rectFallbacks<<" frames=8 parity=exact state=restored\n";
        dispatch(Destroy,token,nullptr);
    }
    td.MipLevels=1;target.Reset();rt.Reset();check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
    reference.clear();
    for(UINT mode=0;mode<2;mode++){
        bind();CreateIn create{target.Get(),16,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode,16};dispatch(CacheConfigure,token,&config);
        Vertex vertices[3]{};for(UINT i=0;i<3;i++){std::memcpy(vertices[i].p,triangle+i*3,12);for(auto& color:vertices[i].color)color=1;}
        Attribute attrs[]={{0,0,6,0,0},{3,0,2,0,12},{4,0,16,0,28},{4,1,16,0,36}};GeometryIn g{};g.vertices=vertices;g.vertexBytes=sizeof(vertices);g.strides[0]=44;g.cpuIndices=indices;g.cpuIndexCount=3;g.attributes=attrs;g.attributeCount=4;
        auto grabProg=prog;grabProg.ps=grabPs->GetBufferPointer();grabProg.psBytes=UINT(grabPs->GetBufferSize());float data[20]{1,1,1,1};Buffer bindings[]={{0,0,80,0},{1,0,80,0}};Patch patch{16,1};Texture texture{};texture.srgb=-1;texture.stage=1;texture.slot=127;texture.sampler=-1;texture.aniso=1;
        DrawIn draw{};draw.program=UINT(dispatch(Program,token,&grabProg));draw.geometry=UINT(dispatch(Geometry,token,&g));draw.constants=data;draw.constantBytes=80;draw.buffers=bindings;draw.bufferCount=2;draw.patches=&patch;draw.patchCount=1;draw.textures=&texture;draw.textureCount=1;draw.grab=1;draw.sprite=mode?4u:0u;draw.extent[0]=draw.extent[1]=.66f;
        UINT list[2],ids[]={0,1},seg[]={0,2},zero=0;Pose poses[]={identity,identity};for(UINT i=0;i<2;i++){draw.pose=i;list[i]=UINT(dispatch(Draw,token,&draw));poses[i].world[0]=poses[i].world[5]=.1f;}
        Frame f{};for(int i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses;f.poseIds=ids;f.poseCount=2;f.list=list;f.listCount=2;f.segments=seg;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
        for(UINT step=0;step<4;step++){
            poses[0].world[12]=step%2?-.03f:-.18f;poses[1].world[12]=-poses[0].world[12];float bg[]={.8f-float(step)*.1f,.6f,.4f,1};c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitPacked,token,&f);event(token<<16);f.listChanged=0;
            auto pixels=readback(d.Get(),c.Get(),target.Get());if(mode==0)reference.push_back(pixels);else require(pixels==reference[step],"merged Grab changed overlap semantics");
        }
        CachePerformance cp{};GpuPerformance gp{};dispatch(CachePerf,token,&cp);dispatch(GpuPerf,token,&gp);require(gp.grabCopies==(mode?6u:8u),"disjoint Grab group copy count");if(mode)require(cp.grabGroups==2&&cp.grabMerged==2&&cp.records==2,"Grab grouping untested or re-recorded");
        std::cout<<"PASS M4l disjoint_grab mode="<<mode<<" copies="<<gp.grabCopies<<" groups="<<cp.grabGroups<<" saved="<<cp.grabMerged<<" overlap=separate parity=exact\n";dispatch(Destroy,token,nullptr);
    }
    std::cout<<"PASS M4l cached renderer fixtures\n";return 0;
}catch(HRESULT hr){std::cerr<<"HRESULT "<<std::hex<<hr<<"\n";return 2;}catch(const std::exception& e){std::cerr<<e.what()<<"\n";return 1;}}
