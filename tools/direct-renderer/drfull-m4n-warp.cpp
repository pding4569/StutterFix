// Windowless production-renderer locality + GPU timestamp comparison.
#define main drfull_m4n_reference_entry
#include "drfull-warp.cpp"
#undef main
#include <chrono>
ComPtr<ID3DBlob> compileLocal(const char* text,const char* entry,const char* target){ComPtr<ID3DBlob>b,e;check(D3DCompile(text,std::strlen(text),nullptr,nullptr,nullptr,entry,target,D3DCOMPILE_IEEE_STRICTNESS,0,&b,&e));return b;}
int main(){try{
    std::cout.setf(std::ios::unitbuf);
    const bool hardware=GetEnvironmentVariableA("SF_M4N_HARDWARE",nullptr,0)!=0;
    ComPtr<ID3D11Device>d;ComPtr<ID3D11DeviceContext>c;D3D_FEATURE_LEVEL fl{};
    check(D3D11CreateDevice(nullptr,hardware?D3D_DRIVER_TYPE_HARDWARE:D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c));
    ComPtr<IDXGIDevice>dxgi;ComPtr<IDXGIAdapter>adapter;check(d.As(&dxgi));check(dxgi->GetAdapter(&adapter));DXGI_ADAPTER_DESC desc{};check(adapter->GetDesc(&desc));
    char adapterName[256]{};WideCharToMultiByte(CP_UTF8,0,desc.Description,-1,adapterName,sizeof(adapterName),nullptr,nullptr);
    std::cout<<"ADAPTER M4n "<<adapterName<<" vendor="<<desc.VendorId<<" device="<<desc.DeviceId<<" dedicated_bytes="<<desc.DedicatedVideoMemory<<"\n";
    D3D11_TEXTURE2D_DESC td{};td.Width=128;td.Height=96;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
    ComPtr<ID3D11Texture2D>target;ComPtr<ID3D11RenderTargetView>rt;check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
    auto bind=[&](){auto* r=rt.Get();c->OMSetRenderTargets(1,&r,nullptr);D3D11_VIEWPORT v{0,0,128,96,0,1};c->RSSetViewports(1,&v);};
    const char* code="cbuffer Globals:register(b0){float4 tint;float4 clock;column_major float4x4 vp;};cbuffer Object:register(b1){column_major float4x4 world;};float4 vs(float3 p:POSITION):SV_Position{return mul(vp,mul(world,float4(p,1)));}float4 ps():SV_Target{return tint*(.7+.2*clock.x);}";
    auto vs=compileLocal(code,"vs","vs_4_0"),ps=compileLocal(code,"ps","ps_4_0");
    ProgramIn prog{};prog.vs=vs->GetBufferPointer();prog.vsBytes=UINT(vs->GetBufferSize());prog.ps=ps->GetBufferPointer();prog.psBytes=UINT(ps->GetBufferSize());prog.state[0]=prog.state[2]=prog.state[7]=1;prog.state[6]=15;prog.state[14]=prog.state[15]=255;prog.state[20]=prog.state[24]=prog.state[28]=8;
    Vertex vertices[3]{};float positions[]={-.06f,.06f,.5f,.06f,.06f,.5f,-.06f,-.06f,.5f};for(UINT i=0;i<3;i++){std::memcpy(vertices[i].p,positions+i*3,12);for(auto& color:vertices[i].color)color=1;}
    UINT indices[]={0,1,2};Attribute attrs[]={{0,0,6,0,0},{3,0,2,0,12},{4,0,16,0,28},{4,1,16,0,36}};
    GeometryIn g{};g.vertices=vertices;g.vertexBytes=sizeof(vertices);g.strides[0]=44;g.cpuIndices=indices;g.cpuIndexCount=3;g.attributes=attrs;g.attributeCount=4;
    Buffer buffers[]={{0,0,96,16},{1,0,96,16},{0,1,64,112}};Patch patches[]={{32,12},{48,3},{112,1}};
    Pose identity{};for(UINT i=0;i<16;i++)identity.world[i]=identity.inverse[i]=i%5==0?1.f:0.f;
    auto event=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(Event,0,nullptr));
    std::vector<std::vector<uint8_t>> reference;
    // Alternating heads exercise a sparse edit inside one LARGE segment. The
    // second segment and the other 218 chunks in the first must remain intact.
    const std::vector<UINT> modes=hardware?std::vector<UINT>{0,1,2,2,1,0}:std::vector<UINT>{0,1,2};
    for(UINT mode:modes){
        bind();CreateIn create{target.Get(),7000,1};int token=dispatch(Create,0,&create);CacheConfiguration config{mode?1u:0u,128};dispatch(CacheConfigure,token,&config);CacheTune tune{mode==2?1u:0u};dispatch(CacheTuning,token,&tune);
        UINT programs[]={UINT(dispatch(Program,token,&prog)),UINT(dispatch(Program,token,&prog))};
        float material[44]{};material[4]=.8f;material[5]=.3f;material[6]=.6f;material[7]=1;
        DrawIn draw{};draw.geometry=UINT(dispatch(Geometry,token,&g));draw.buffers=buffers;draw.bufferCount=3;draw.patches=patches;draw.patchCount=3;draw.constants=material;draw.constantBytes=sizeof(material);draw.sprite=2;draw.extent[0]=draw.extent[1]=.061f;
        std::vector<UINT> list(7000),ids(7000);std::vector<Pose> poses(7000,identity);
        for(UINT i=0;i<7000;i++){draw.program=programs[i%2];draw.pose=i;list[i]=UINT(dispatch(Draw,token,&draw));ids[i]=i;poses[i].world[12]=float(int(i%16)-8)*.15f;poses[i].world[13]=float(int(i/16%12)-6)*.15f;}
        UINT segments[]={0,3500,3500,3500},zero=0;Frame f{};for(UINT i=0;i<16;i++)f.vp[i]=f.view[i]=f.projection[i]=i%5==0?1.f:0.f;
        f.poses=poses.data();f.poseIds=ids.data();f.poseCount=7000;f.list=list.data();f.listCount=7000;f.segments=segments;f.segmentCount=2;f.listChanged=1;f.constantOffsets=&zero;
        auto marker=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(TimelineEvent,token,nullptr));require(marker!=nullptr,"timeline event unavailable");
        auto submit=[&](){float bg[]={.03f,.07f,.11f,1};c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitPacked,token,&f);marker(token<<16);event(token<<16);event((token<<16)|1);marker((token<<16)|1);Statistics st{};dispatch(Stats,token,&st);require(st.errors==0,"M4n render error");};
        for(UINT i=0;i<4;i++){submit();f.poseCount=0;}
        CachePerformance warm{},after{};LocalPlanPerformance initial{},local{};dispatch(CachePerf,token,&warm);dispatch(LocalPlanPerf,token,&initial);
        for(UINT step=0;step<12;step++){
            std::swap(list[130],list[131]);f.listChanged=1;f.time[0]=float(step)*.02f;submit();
            auto pixels=readback(d.Get(),c.Get(),target.Get());if(!mode&&reference.size()<12)reference.push_back(pixels);else require(pixels==reference[step],"M4n local reorder pixels changed");
        }
        dispatch(CachePerf,token,&after);dispatch(LocalPlanPerf,token,&local);
        if(mode){
            require(local.fullPlans==initial.fullPlans,"sparse reorder rebuilt full plan");
            require(local.visitedSources-initial.visitedSources<=12*128,"sparse reorder visited untouched sources");
            require(after.records-warm.records==12,"sparse reorder recorded untouched chunks");
            require(local.indexSources-initial.indexSources==12*2,"index update accounting changed");
        }
        std::cout<<"PASS M4n local mode="<<mode<<" sources=7000 segments=2 frames=12 pixels=exact full_delta="<<local.fullPlans-initial.fullPlans<<" visited_sources="<<local.visitedSources-initial.visitedSources<<" index_sources="<<local.indexSources-initial.indexSources<<" record_delta="<<after.records-warm.records<<" segment_us="<<(local.segmentUs-initial.segmentUs)/12<<" local_segment_max_us="<<local.maxSegmentUs<<" local_plan_max_us="<<local.maxPlanUs<<" local_record_max_us="<<local.maxRecordUs<<"\n";
        TimelinePerformance timeline{};NativeStagePerformance stages{};dispatch(TimelinePerf,token,&timeline);dispatch(NativeStagePerf,token,&stages);double stageSum=0;for(auto us:stages.us)stageSum+=us;
        require(stages.samples>0&&stages.samples==timeline.samples&&std::abs(stageSum-timeline.nativeUs)<1e-5,"GPU stages differ from native timeline sample/sum");
        std::cout<<"PASS M4n GPU_stage_sum mode="<<mode<<" samples="<<stages.samples<<" native_us="<<timeline.nativeUs<<" stage_sum_us="<<stageSum<<" overflow="<<stages.overflow<<" elapsed_not_busy=True\n";
        // Sprite/geometry rebinding appends a native ID without changing list
        // length or the underlying GPU capacity. It must stay local too.
        UploadPerformance uploadBefore{},uploadAfter{};dispatch(UploadPerf,token,&uploadBefore);
        draw.program=programs[0];draw.pose=130;DrawReplacement replacement{list[130],draw};
        list[130]=UINT(dispatch(ReplaceDraw,token,&replacement));f.listChanged=1;submit();
        auto replaced=readback(d.Get(),c.Get(),target.Get());require(replaced==reference[11],"equal-geometry replacement changed pixels");
        LocalPlanPerformance replacedPlan{};CachePerformance replacedCache{};dispatch(LocalPlanPerf,token,&replacedPlan);dispatch(CachePerf,token,&replacedCache);
        dispatch(UploadPerf,token,&uploadAfter);require(uploadAfter.bakedVertices-uploadBefore.bakedVertices<=128,"append rebaked all existing world vertices");
        if(mode)require(replacedPlan.fullPlans==local.fullPlans&&replacedPlan.visitedSources-local.visitedSources==128&&replacedCache.records-after.records==1,"equal-geometry replacement rebuilt untouched plan/chunks");
        std::cout<<"PASS M4n replacement mode="<<mode<<" stable_capacity=True same_indices=True full_delta="<<replacedPlan.fullPlans-local.fullPlans<<" record_delta="<<replacedCache.records-after.records<<" baked_vertices="<<uploadAfter.bakedVertices-uploadBefore.bakedVertices<<" pixels=exact\n";
        if(!hardware){dispatch(Destroy,token,nullptr);continue;}
        // Synchronize the exact timestamp window. Readback lies outside it.
        // These costs are synthetic native work, never Exit FPS or game GPU ms.
        D3D11_QUERY_DESC qd{D3D11_QUERY_TIMESTAMP_DISJOINT,0};ComPtr<ID3D11Query>disjoint,start,end;check(d->CreateQuery(&qd,&disjoint));qd.Query=D3D11_QUERY_TIMESTAMP;check(d->CreateQuery(&qd,&start));check(d->CreateQuery(&qd,&end));
        std::vector<double> gpu,cpu;f.listChanged=0;
        for(UINT sample=0;sample<12;sample++){
            c->Begin(disjoint.Get());c->End(start.Get());auto at=std::chrono::steady_clock::now();
            for(UINT j=0;j<4;j++){f.time[0]+=.0001f;submit();}
            cpu.push_back(std::chrono::duration<double,std::micro>(std::chrono::steady_clock::now()-at).count()/4);
            c->End(end.Get());c->End(disjoint.Get());c->Flush();D3D11_QUERY_DATA_TIMESTAMP_DISJOINT freq{};UINT64 a=0,b=0;
            auto deadline=std::chrono::steady_clock::now()+std::chrono::seconds(15);
            auto wait=[&](ID3D11Query* q,void* data,UINT bytes){HRESULT hr;while((hr=c->GetData(q,data,bytes,0))==S_FALSE){require(std::chrono::steady_clock::now()<deadline,"GPU timestamp timeout");std::this_thread::yield();}check(hr);};
            wait(disjoint.Get(),&freq,sizeof(freq));wait(start.Get(),&a,sizeof(a));wait(end.Get(),&b,sizeof(b));require(!freq.Disjoint&&freq.Frequency,"GPU timestamp disjoint");gpu.push_back(double(b-a)*1e6/double(freq.Frequency)/4);
        }
        std::sort(gpu.begin(),gpu.end());std::sort(cpu.begin(),cpu.end());
        std::cout<<"MEASURE M4n adapter="<<(hardware?"hardware":"WARP")<<" mode="<<mode<<" gpu_median_us="<<gpu[gpu.size()/2]<<" gpu_min_us="<<gpu.front()<<" submit_cpu_median_us="<<cpu[cpu.size()/2]<<" samples="<<gpu.size()<<" frame_batch=4\n";
        dispatch(Destroy,token,nullptr);
    }
    std::cout<<"PASS M4n locality GPU comparison game=not_launched\n";return 0;
}catch(const std::exception& e){std::cerr<<e.what()<<"\n";return 2;}catch(...){std::cerr<<"M4n HRESULT failure\n";return 3;}}
