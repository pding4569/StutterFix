// Moving camera/time/material/live controls. Production renderer, no game/window.
#define main drfull_m4m_legacy_entry
#include "drfull-warp.cpp"
#undef main
ComPtr<ID3DBlob> compileMoving(const char* text,const char* entry,const char* target){ComPtr<ID3DBlob>b,e;check(D3DCompile(text,std::strlen(text),nullptr,nullptr,nullptr,entry,target,D3DCOMPILE_IEEE_STRICTNESS,0,&b,&e));return b;}
int main(){try{
    std::cout.setf(std::ios::unitbuf);
    ComPtr<ID3D11Device>d;ComPtr<ID3D11DeviceContext>c;D3D_FEATURE_LEVEL fl{};check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c));
    D3D11_TEXTURE2D_DESC td{};td.Width=48;td.Height=32;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
    ComPtr<ID3D11Texture2D>target;ComPtr<ID3D11RenderTargetView>rt;check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
    auto bind=[&](){auto* r=rt.Get();c->OMSetRenderTargets(1,&r,nullptr);D3D11_VIEWPORT v{0,0,48,32,0,1};c->RSSetViewports(1,&v);};bind();
    const char* code="cbuffer Globals:register(b0){float4 tint;float4 clock;column_major float4x4 vp;};cbuffer Object:register(b1){column_major float4x4 world;};float4 vs(float3 p:POSITION):SV_Position{return mul(vp,mul(world,float4(p,1)));}float4 ps():SV_Target{return tint*(.7+.2*clock.x);}";
    auto vs=compileMoving(code,"vs","vs_4_0"),ps=compileMoving(code,"ps","ps_4_0");
    ProgramIn prog{};prog.vs=vs->GetBufferPointer();prog.vsBytes=UINT(vs->GetBufferSize());prog.ps=ps->GetBufferPointer();prog.psBytes=UINT(ps->GetBufferSize());prog.state[0]=prog.state[2]=prog.state[7]=1;prog.state[6]=15;prog.state[14]=prog.state[15]=255;prog.state[20]=prog.state[24]=prog.state[28]=8;
    Vertex vertices[3]{};float positions[]={-.2f,.2f,.5f,.2f,.2f,.5f,-.2f,-.2f,.5f};for(UINT i=0;i<3;i++){std::memcpy(vertices[i].p,positions+i*3,12);for(auto& color:vertices[i].color)color=1;}
    UINT indices[]={0,1,2};Attribute attrs[]={{0,0,6,0,0},{3,0,2,0,12},{4,0,16,0,28},{4,1,16,0,36}};
    GeometryIn g{};g.vertices=vertices;g.vertexBytes=sizeof(vertices);g.strides[0]=44;g.cpuIndices=indices;g.cpuIndexCount=3;g.attributes=attrs;g.attributeCount=4;
    Buffer buffers[]={{0,0,96,16},{1,0,96,16},{0,1,64,112}};Patch patches[]={{32,12},{48,3},{112,1}};
    Pose identity{};for(UINT i=0;i<16;i++)identity.world[i]=identity.inverse[i]=i%5==0?1.f:0.f;
    auto event=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(Event,0,nullptr));
    std::vector<std::vector<uint8_t>> reference;
    // Alternating program IDs prevent batching while the identical mixed Globals
    // must still share ONE slice across stages, programs and 128 draw heads.
    for(UINT mode=0;mode<3;mode++){
        bind();CreateIn create{target.Get(),256,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode?1u:0u,16};dispatch(CacheConfigure,token,&config);CacheTune tune{mode==2?1u:0u};dispatch(CacheTuning,token,&tune);
        UINT programs[]={UINT(dispatch(Program,token,&prog)),UINT(dispatch(Program,token,&prog))};
        DrawIn draw{};draw.geometry=UINT(dispatch(Geometry,token,&g));draw.buffers=buffers;draw.bufferCount=3;draw.patches=patches;draw.patchCount=3;draw.sprite=2;draw.extent[0]=draw.extent[1]=.21f;
        if(mode==1)draw.center[0]=100; // Deliberately false bounds: direct clipping must use the actual vertices.
        std::vector<UINT> list(128),ids(128),offsets(129);std::vector<Pose> poses(128,identity);std::vector<float> material(128*44);
        for(UINT i=0;i<128;i++){auto* bytes=material.data()+i*44;bytes[4]=.8f;bytes[5]=.3f;bytes[6]=.6f;bytes[7]=1;draw.program=programs[i%2];draw.pose=i;draw.constants=bytes;draw.constantBytes=176;list[i]=UINT(dispatch(Draw,token,&draw));ids[i]=i;offsets[i]=i*176;}offsets[128]=128*176;
        UINT seg[]={0,64,64,64},zero=0;Frame f{};for(UINT i=0;i<16;i++)f.vp[i]=f.view[i]=f.projection[i]=i%5==0?1.f:0.f;f.poses=poses.data();f.poseIds=ids.data();f.poseCount=128;f.list=list.data();f.listCount=128;f.segments=seg;f.segmentCount=2;f.listChanged=1;f.constantOffsets=&zero;
        auto submit=[&](){float bg[]={.03f,.07f,.11f,1};c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitPacked,token,&f);event(token<<16);event((token<<16)|1);Statistics st{};dispatch(Stats,token,&st);require(st.errors==0,"moving cache error");};
        CachePerformance warm{},after{};CacheDetails detail{};BatchPerformance poseStep{};
        for(UINT step=0;step<20;step++){
            f.time[0]=float(step)*.03125f;f.vp[12]=step==4?4.f:float(step%3)*.01f;
            for(UINT i=0;i<128;i++){poses[i].world[12]=float(int(i%8)-4)*.2f;poses[i].world[13]=float(int(i/8)%4-2)*.2f;}
            poses[127].world[12]=step%3?0.f:100.f;
            // A single material diverges from its shared mixed CB, then animates
            // through the same fixed slice. Other users retain the original tint.
            if(step>=8){material[127*44+4]=.1f+float(step-8)*.02f;f.drawIds=&list[127];f.dirtyCount=1;f.constantOffsets=offsets.data();offsets[0]=0;offsets[1]=176;f.constants=reinterpret_cast<const uint8_t*>(material.data()+127*44);f.constantBytes=176;}
            submit();auto pixels=readback(d.Get(),c.Get(),target.Get());if(!mode)reference.push_back(pixels);else require(pixels==reference[step],"moving camera/time/material pixel mismatch");
            if(mode&&step==1)dispatch(BatchPerf,token,&poseStep);
            if(mode&&step==2){BatchPerformance cameraOnly{};dispatch(BatchPerf,token,&cameraOnly);require(cameraOnly.gpuDispatches==poseStep.gpuDispatches,"camera-only motion rebaked world vertices");}
            if(step==0)dispatch(CachePerf,token,&warm);
            if(step==7&&mode){dispatch(CachePerf,token,&after);require(after.planBuilds==warm.planBuilds&&after.records==warm.records,"camera/time/pose/identical list invalidated commands");require(after.constantEvaluations-warm.constantEvaluations==7,"frame constants evaluated per draw instead of once");require(after.constantBytes-warm.constantBytes==7*4096,"frame constants dirtied draw pages");}
        }
        dispatch(CachePerf,token,&after);dispatch(CacheDetail,token,&detail);
        if(mode){require(after.planBuilds==1&&after.records==warm.records+1,"material divergence invalidated more than one chunk");require(detail.sharedBindings>=255&&detail.materialDetaches==1&&(mode==1?detail.indirectDraws==0:detail.indirectDraws==128*20),"sharing/direct control was not exercised");require(detail.sameLists==19&&detail.sameRuns>=11,"identical list/run was not suppressed");}
        std::cout<<"PASS M4m moving mode="<<mode<<" pixels=20 exact=True plans="<<after.planBuilds<<" shared="<<detail.sharedBindings<<" detaches="<<detail.materialDetaches<<" frame_eval="<<detail.frameEvaluations<<" draw_eval="<<detail.drawEvaluations<<" direct="<<detail.directDraws<<" indirect="<<detail.indirectDraws<<" camera_rebake=0 untrusted_bounds="<<(mode==1?"clipper_verified":"control")<<"\n";
        dispatch(Destroy,token,nullptr);
    }
    reference.clear();
    for(UINT mode=0;mode<3;mode++){
        bind();CreateIn create{target.Get(),16,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode?1u:0u,16};dispatch(CacheConfigure,token,&config);CacheTune tune{mode==2?1u:0u};dispatch(CacheTuning,token,&tune);
        DrawIn draw{};draw.program=UINT(dispatch(Program,token,&prog));draw.geometry=UINT(dispatch(Geometry,token,&g));draw.buffers=buffers;draw.bufferCount=3;draw.patches=patches;draw.patchCount=3;draw.sprite=2;draw.extent[0]=draw.extent[1]=.21f;
        float data[44]{};data[4]=.8f;data[5]=.3f;data[6]=.6f;data[7]=1;draw.constants=data;draw.constantBytes=176;
        UINT list[3],ids[]={0,1,2},segments[]={0,3},zero=0,offsets[]={0,176};Pose poses[]={identity,identity,identity};
        for(UINT i=0;i<3;i++){draw.pose=i;list[i]=UINT(dispatch(Draw,token,&draw));poses[i].world[12]=float(int(i)-1)*.5f;}
        Frame f{};for(UINT i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses;f.poseIds=ids;f.poseCount=3;f.list=list;f.listCount=3;f.segments=segments;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
        CachePerformance warm{},last{};
        for(UINT step=0;step<5;step++){
            float bg[]={.02f,.04f,.08f,1};c->ClearRenderTargetView(rt.Get(),bg);
            if(step){data[4]=step<3?.2f:.8f;f.drawIds=&list[1];f.dirtyCount=1;f.constantOffsets=offsets;f.constants=reinterpret_cast<const uint8_t*>(data);f.constantBytes=176;}
            dispatch(CommitPacked,token,&f);event(token<<16);f.listChanged=0;auto pixels=readback(d.Get(),c.Get(),target.Get());if(!mode)reference.push_back(pixels);else require(pixels==reference[step],"run split/merge changed pixels");
            Statistics st{};dispatch(Stats,token,&st);require(st.errors==0,"run transition error");dispatch(CachePerf,token,&last);if(!step)warm=last;
        }
        if(mode)require(last.planBuilds==warm.planBuilds+2&&last.records==warm.records+2,"real run split/merge did not replan exactly twice");
        std::cout<<"PASS M4m run_boundaries mode="<<mode<<" pixels=5 split=plan merge=plan same_material=skip plans_delta="<<last.planBuilds-warm.planBuilds<<"\n";
        dispatch(Destroy,token,nullptr);
    }
    // Equal contents in VS/PS may later diverge independently. Reusing one
    // detached slice by draw ID alone would alias these two different values.
    std::string stageCode=code;auto expression=stageCode.find("return mul(vp,mul(world,float4(p,1)));" );stageCode.replace(expression,std::strlen("return mul(vp,mul(world,float4(p,1)));"),"float4 q=mul(vp,mul(world,float4(p,1)));q.x+=tint.x*.25;return q;");
    auto stageVs=compileMoving(stageCode.c_str(),"vs","vs_4_0");auto stageProg=prog;stageProg.vs=stageVs->GetBufferPointer();stageProg.vsBytes=UINT(stageVs->GetBufferSize());reference.clear();
    for(UINT mode=0;mode<2;mode++){
        bind();CreateIn create{target.Get(),16,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode,16};dispatch(CacheConfigure,token,&config);
        Buffer stageBuffers[]={{0,0,96,16},{1,0,96,176},{0,1,64,112}};Patch stagePatches[]={{32,12},{48,3},{112,1},{192,12},{208,3}};float data[68]{};
        for(UINT at:{4u,44u}){data[at]=.6f;data[at+1]=.2f;data[at+2]=.7f;data[at+3]=1;}
        DrawIn draw{};draw.program=UINT(dispatch(Program,token,&stageProg));draw.geometry=UINT(dispatch(Geometry,token,&g));draw.buffers=stageBuffers;draw.bufferCount=3;draw.patches=stagePatches;draw.patchCount=5;draw.constants=data;draw.constantBytes=sizeof(data);
        UINT list[2],ids[]={0,1},segments[]={0,2},zero=0,offsets[]={0,sizeof(data)};Pose poses[]={identity,identity};poses[0].world[12]=-.4f;poses[1].world[12]=.4f;
        for(UINT i=0;i<2;i++){draw.pose=i;list[i]=UINT(dispatch(Draw,token,&draw));}
        Frame f{};for(UINT i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses;f.poseIds=ids;f.poseCount=2;f.list=list;f.listCount=2;f.segments=segments;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
        for(UINT step=0;step<5;step++){
            float bg[]={.02f,.04f,.08f,1};c->ClearRenderTargetView(rt.Get(),bg);f.time[0]=float(step)*.03f;
            if(step){data[44]=step==1?.2f:step==2?.2f:step==3?.4f:.6f;data[4]=step==1?.6f:step==2?.9f:step==3?.8f:.6f;f.drawIds=&list[1];f.dirtyCount=1;f.constantOffsets=offsets;f.constants=reinterpret_cast<const uint8_t*>(data);f.constantBytes=sizeof(data);}
            dispatch(CommitPacked,token,&f);event(token<<16);f.listChanged=0;auto pixels=readback(d.Get(),c.Get(),target.Get());if(!mode)reference.push_back(pixels);else require(pixels==reference[step],"independently animated VS/PS buffers alias");
            Statistics st{};dispatch(Stats,token,&st);require(st.errors==0,"stage divergence error");
        }
        CachePerformance cp{};CacheDetails detail{};dispatch(CachePerf,token,&cp);dispatch(CacheDetail,token,&detail);if(mode)require(cp.planBuilds==1&&cp.records==3&&detail.materialDetaches==2,"stage divergence did not retain independent slices");
        std::cout<<"PASS M4m stage_divergence mode="<<mode<<" pixels=5 independent_VS_PS=True detaches="<<detail.materialDetaches<<"\n";dispatch(Destroy,token,nullptr);
    }
    // 7,000 heads, 21,000 CB bindings. Time/camera/pose drift and redundant list
    // notifications must cost one frame evaluation, 4KiB and no plan per frame.
    bind();CreateIn create{target.Get(),7000,1};int token=dispatch(Create,0,&create);CacheConfiguration config{1,128};dispatch(CacheConfigure,token,&config);
    UINT programs[]={UINT(dispatch(Program,token,&prog)),UINT(dispatch(Program,token,&prog))};DrawIn draw{};draw.geometry=UINT(dispatch(Geometry,token,&g));draw.buffers=buffers;draw.bufferCount=3;draw.patches=patches;draw.patchCount=3;draw.sprite=2;draw.extent[0]=draw.extent[1]=.21f;
    std::vector<UINT> list(7000),ids(7000),dirty(7000),offsets(7001);std::vector<Pose> poses(7000,identity);std::vector<float> material(7000*44);
    for(UINT i=0;i<7000;i++){auto* data=material.data()+i*44;data[4]=.8f;data[5]=.3f;data[6]=.6f;data[7]=1;draw.program=programs[i%2];draw.pose=i;draw.constants=data;draw.constantBytes=176;list[i]=UINT(dispatch(Draw,token,&draw));ids[i]=i;dirty[i]=list[i];offsets[i]=i*176;poses[i].world[12]=100.f;}offsets[7000]=7000*176;
    UINT seg[]={0,7000},zero=0;Frame f{};for(UINT i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses.data();f.poseIds=ids.data();f.poseCount=7000;f.list=list.data();f.listCount=7000;f.segments=seg;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
    auto submit=[&](){dispatch(CommitPacked,token,&f);event(token<<16);Statistics st{};dispatch(Stats,token,&st);require(st.errors==0,"7000 moving cache error");};
    for(UINT i=0;i<12;i++){f.time[0]+=0.01f;f.vp[12]+=0.0001f;for(auto& pose:poses)pose.world[13]+=0.0001f;submit();}
    CachePerformance warm{},after{};CacheDetails before{},detail{};dispatch(CachePerf,token,&warm);dispatch(CacheDetail,token,&before);
    auto allocations=nativeHeapCalls.load();for(UINT i=0;i<16;i++){f.time[0]+=0.01f;f.vp[12]+=0.0001f;for(auto& pose:poses)pose.world[13]+=0.0001f;submit();}auto heaps=nativeHeapCalls.load()-allocations;
    dispatch(CachePerf,token,&after);dispatch(CacheDetail,token,&detail);
    require(heaps==0&&after.planBuilds==warm.planBuilds&&after.records==warm.records,"7000 moving path rebuilt or allocated");
    require(after.constantEvaluations-warm.constantEvaluations==16&&after.constantBytes-warm.constantBytes==16*4096&&detail.drawEvaluations==before.drawEvaluations,"7000 moving frame reevaluated draw constants");
    require(detail.variableChanges[12]-before.variableChanges[12]==16&&detail.variableChanges[3]-before.variableChanges[3]==16&&detail.variableEvaluations[1]==before.variableEvaluations[1],"per-variable frame/draw accounting");
    require(detail.indirectDraws==0&&detail.cullItems==0&&detail.directDraws-before.directDraws==7000*16,"7000 offscreen indirect work remains");
    std::cout<<"PASS M4m scale heads=7000 bindings=21000 frames=16 plans_delta=0 records_delta=0 evaluations_per_frame=1 bytes_per_frame=4096 draw_evaluations_delta=0 indirect=0 cull_items=0 cpp_heap_calls="<<heaps<<"\n";
    // Every material changes together. Keep one shared slice, no detach or plan.
    for(UINT i=0;i<7000;i++)material[i*44+4]=.4f;
    f.drawIds=dirty.data();f.dirtyCount=7000;f.constantOffsets=offsets.data();f.constants=reinterpret_cast<const uint8_t*>(material.data());f.constantBytes=UINT(material.size()*4);submit();
    dispatch(CachePerf,token,&after);dispatch(CacheDetail,token,&detail);require(after.planBuilds==warm.planBuilds&&after.records==warm.records&&detail.materialDetaches==0,"co-changing material broke shared frame binding");
    std::cout<<"PASS M4m synchronized_material heads=7000 shared_slice=retained detach=0 plan_delta=0 record_delta=0\n";
    CacheTune late{1};require(sf_z_drfull_v1(CacheTuning,token,&late)==0,"late cache tuning accepted");
    dispatch(Destroy,token,nullptr);std::cout<<"PASS M4m moving playback fixtures game=not_launched\n";return 0;
}catch(const std::exception& e){std::cerr<<e.what()<<"\n";return 2;}catch(...){std::cerr<<"M4m HRESULT failure\n";return 3;}}
