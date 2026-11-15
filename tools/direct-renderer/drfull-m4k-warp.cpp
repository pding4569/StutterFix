// Production renderer, WARP only. Covers windows across many empty segments,
// no per-frame GPU readback, high SRV slots, invisible constants and controls.
#define main drfull_legacy_fixture_entry
#include "drfull-warp.cpp"
#undef main
ComPtr<ID3DBlob> compileText(const char* src,const char* entry,const char* profile){ComPtr<ID3DBlob>b,e;check(D3DCompile(src,std::strlen(src),nullptr,nullptr,nullptr,entry,profile,D3DCOMPILE_IEEE_STRICTNESS,0,&b,&e));return b;}
int main(){try{
    ComPtr<ID3D11Device>d;ComPtr<ID3D11DeviceContext>c;D3D_FEATURE_LEVEL fl{};
    check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c));
    D3D11_TEXTURE2D_DESC td{};td.Width=192;td.Height=128;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
    ComPtr<ID3D11Texture2D>target;ComPtr<ID3D11RenderTargetView>rt;check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
    auto* r=rt.Get();c->OMSetRenderTargets(1,&r,nullptr);D3D11_VIEWPORT vp{0,0,192,128,0,1};c->RSSetViewports(1,&vp);
    const char* source="cbuffer Data:register(b0){float4 color;float4x4 world;};float4 vs(float3 p:POSITION):SV_Position{return float4(p,1);}float4 ps():SV_Target{return color;}";
    auto vs=compileText(source,"vs","vs_4_0"),ps=compileText(source,"ps","ps_4_0");
    ProgramIn prog{};prog.vs=vs->GetBufferPointer();prog.vsBytes=UINT(vs->GetBufferSize());prog.ps=ps->GetBufferPointer();prog.psBytes=UINT(ps->GetBufferSize());prog.state[0]=prog.state[2]=prog.state[7]=1;prog.state[6]=15;prog.state[20]=prog.state[24]=prog.state[28]=8;prog.state[14]=prog.state[15]=255;
    float triangle[]={-1,1,.5f,3,1,.5f,-1,-3,.5f};UINT indices[]={0,1,2};auto vb=buffer(d.Get(),triangle,sizeof(triangle),D3D11_BIND_VERTEX_BUFFER),ib=buffer(d.Get(),indices,sizeof(indices),D3D11_BIND_INDEX_BUFFER);
    Attribute a{0,0,6,0,0};GeometryIn geo{};geo.buffers[0]=vb.Get();geo.strides[0]=12;geo.indices=ib.Get();geo.indexFormat=42;geo.indexCount=3;geo.attributes=&a;geo.attributeCount=1;
    Pose pose{};for(int i=0;i<16;i++)pose.world[i]=pose.inverse[i]=i%5==0?1.f:0.f;
    auto event=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(Event,0,nullptr));
    for(UINT first:{0u,256u,512u}){
        CreateIn create{target.Get(),512,1};int token=dispatch(Create,0,&create);DrawProbeConfiguration config{1,first,256,1};dispatch(DrawProbeConfigure,token,&config);
        Buffer binding{1,0,80,0};Patch patch{16,1};float data[20]{.61f,.23f,.79f,.47f};DrawIn draw{};
        draw.program=UINT(dispatch(Program,token,&prog));draw.geometry=UINT(dispatch(Geometry,token,&geo));draw.buffers=&binding;draw.bufferCount=1;draw.patches=&patch;draw.patchCount=1;draw.constants=data;draw.constantBytes=sizeof(data);
        std::vector<UINT>list(315),ids(315);std::vector<Pose>poses(315,pose);
        for(UINT i=0;i<315;i++){draw.pose=i;ids[i]=i;list[i]=UINT(dispatch(Draw,token,&draw));}
        UINT segments[]={0,0,0,40,40,0,40,40,80,40,120,40,160,40,200,40,240,40,280,35};UINT zero=0;
        Frame f{};for(int i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses.data();f.poseIds=ids.data();f.poseCount=315;f.list=list.data();f.listCount=315;f.segments=segments;f.segmentCount=10;f.listChanged=1;f.constantOffsets=&zero;
        for(UINT frame=0;frame<20;frame++){dispatch(CommitPacked,token,&f);for(UINT seg=0;seg<10;seg++)event((token<<16)|seg);f.listChanged=0;}
        auto pixels=readback(d.Get(),c.Get(),target.Get());
        // Poll after GPU completion through an EMPTY leading segment, no Flush
        // or blocking GetData in production. Fixture readback is the only wait.
        dispatch(CommitPacked,token,&f);event(token<<16);
        DrawProbeReadback q{};q.index=UINT32_MAX;dispatch(DrawProbeRead,token,&q);DrawProbeState state{};dispatch(DrawProbeStatus,token,&state);
        require(state.lastVisible==315&&state.maxVisible==315,"actual visible count wrong");
        if(first<315){require(q.total>0&&q.invalid==0,"valid window failed to sample");
            for(UINT i=0;i<q.total;i++){q.index=i;dispatch(DrawProbeRead,token,&q);DrawProbeDetail detail{};detail.index=i;dispatch(DrawProbeDetailRead,token,&detail);
                require(q.row.ordinal>=first&&q.row.ordinal<std::min(315u,first+256)&&q.row.segment>0,"window identity wrong");
                require(detail.prepareGpuUs>=0&&detail.prepareConstantsUs>=0&&detail.grabUs>=0&&detail.grabUs<=q.row.us&&detail.prepareGpuUs+detail.prepareConstantsUs+q.row.us<=q.row.segmentUs+.01,"resource interval ordering wrong");}
        }else require(q.total==0&&state.sampledSegments==0&&q.skipped==0,"out-of-range window consumed query slots");
        Statistics stats{};dispatch(Stats,token,&stats);require(stats.errors==0,"window native errors");
        std::cout<<"PASS M4k window first="<<first<<" actual_visible="<<state.lastVisible<<" rows="<<q.total<<" invalid="<<q.invalid<<" sampled_segments="<<state.sampledSegments<<" skipped="<<q.skipped<<" empty_leading_poll=True no_interframe_readback=True\n";
        require(std::abs(int(pixels[400])-156)<=1,"window pixels changed");dispatch(Destroy,token,nullptr);
    }
    // Invisible moving objects must not allocate/upload their object constants.
    CreateIn create{target.Get(),512,1};int token=dispatch(Create,0,&create);Buffer binding{1,0,80,0};Patch patch{16,1};float data[20]{.61f,.23f,.79f,.47f};DrawIn draw{};
    draw.program=UINT(dispatch(Program,token,&prog));draw.geometry=UINT(dispatch(Geometry,token,&geo));draw.buffers=&binding;draw.bufferCount=1;draw.patches=&patch;draw.patchCount=1;draw.constants=data;draw.constantBytes=sizeof(data);draw.extent[0]=draw.extent[1]=.1f;
    std::vector<UINT>list(200),ids(200);std::vector<Pose>poses(200,pose);for(UINT i=0;i<200;i++){draw.pose=i;ids[i]=i;list[i]=UINT(dispatch(Draw,token,&draw));if(i>=100)poses[i].world[12]=100;}
    UINT seg[]={0,200},zero=0;Frame f{};for(int i=0;i<16;i++)f.vp[i]=i%5==0?1.f:0.f;f.poses=poses.data();f.poseIds=ids.data();f.poseCount=200;f.list=list.data();f.listCount=200;f.segments=seg;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
    dispatch(CommitPacked,token,&f);event(token<<16);f.listChanged=0;SubmissionPerformance start{},end{};dispatch(SubmissionPerf,token,&start);
    for(UINT frame=0;frame<32;frame++){for(UINT i=100;i<200;i++)poses[i].world[12]+=.01f;dispatch(CommitPacked,token,&f);event(token<<16);}dispatch(SubmissionPerf,token,&end);
    ResourcePerformance resource{};dispatch(ResourcePerf,token,&resource);require(end.ringBytes==start.ringBytes&&resource.constantVisible*2==resource.constantCandidates,"invisible constants uploaded/evaluated");
    auto original=readback(d.Get(),c.Get(),target.Get());poses[199].world[12]=0;dispatch(CommitPacked,token,&f);event(token<<16);dispatch(SubmissionPerf,token,&end);require(end.ringBytes>start.ringBytes,"newly visible constants missing");require(readback(d.Get(),c.Get(),target.Get())==original,"visibility restoration changed pixels");
    std::cout<<"PASS M4k constants candidates="<<resource.constantCandidates<<" visible="<<resource.constantVisible<<" invisible_motion_upload=0 reentry=uploaded parity=exact\n";dispatch(Destroy,token,nullptr);
    // Explicit resource configuration rejects changes after drawing resources exist.
    token=dispatch(Create,0,&create);ResourceConfiguration controls{0,0,0,0};dispatch(ResourceConfigure,token,&controls);
    draw.program=UINT(dispatch(Program,token,&prog));draw.geometry=UINT(dispatch(Geometry,token,&geo));dispatch(Draw,token,&draw);
    require(sf_z_drfull_v1(ResourceConfigure,token,&controls)==0,"late resource control accepted");dispatch(Destroy,token,nullptr);
    // High-slot bindings survive a low-slot-only clear. Verify the production
    // shader transition trim and Grab copy/ablation independently.
    const char* textureSource="Texture2D<float4> tex:register(t127);cbuffer Data:register(b0){float4 color;float4x4 world;};float4 vs(float3 p:POSITION):SV_Position{return float4(p,1);}float4 ps():SV_Target{return tex.Load(int3(0,0,0))*color;}";
    auto texturePs=compileText(textureSource,"ps","ps_5_0");ProgramIn textured=prog;textured.ps=texturePs->GetBufferPointer();textured.psBytes=UINT(texturePs->GetBufferSize());
    D3D11_TEXTURE2D_DESC imageDesc=td;imageDesc.Width=imageDesc.Height=4;imageDesc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    std::array<UINT,16> white;white.fill(0xffffffff);D3D11_SUBRESOURCE_DATA imageData{white.data(),16,0};ComPtr<ID3D11Texture2D>image;check(d->CreateTexture2D(&imageDesc,&imageData,&image));
    for(UINT skip:{0u,1u}){
        token=dispatch(Create,0,&create);controls={skip,1,1,1};dispatch(ResourceConfigure,token,&controls);
        draw.geometry=UINT(dispatch(Geometry,token,&geo));draw.pose=0;draw.program=UINT(dispatch(Program,token,&textured));draw.extent[0]=draw.extent[1]=0;draw.grab=0;
        Texture tex{};tex.resource=image.Get();tex.stage=1;tex.slot=127;tex.sampler=-1;tex.filter=1;tex.wrapU=tex.wrapV=1;tex.aniso=1;draw.textures=&tex;draw.textureCount=1;
        UINT sequence[3];sequence[0]=UINT(dispatch(Draw,token,&draw));draw.program=UINT(dispatch(Program,token,&prog));draw.textureCount=0;sequence[1]=UINT(dispatch(Draw,token,&draw));
        draw.program=UINT(dispatch(Program,token,&textured));tex.resource=nullptr;tex.srgb=-1;draw.textureCount=1;draw.grab=1;sequence[2]=UINT(dispatch(Draw,token,&draw));
        UINT range[]={0,3};f.list=sequence;f.listCount=3;f.segments=range;f.poseCount=1;f.poses=&pose;f.poseIds=&zero;f.listChanged=1;
        dispatch(CommitPacked,token,&f);event(token<<16);GpuPerformance gpu{};dispatch(GpuPerf,token,&gpu);dispatch(ResourcePerf,token,&resource);
        require(resource.srvUnbinds>=1,"unused high-slot SRV stayed bound");require(gpu.grabCopies==(skip?0u:1u)&&resource.grabSkips==skip,"Grab switch/counter wrong");
        if(!skip){auto pixels=readback(d.Get(),c.Get(),target.Get());for(int channel=0;channel<4;channel++)require(std::abs(int(pixels[400+channel])-int(std::round(std::round(data[channel]*255)/255*data[channel]*255)))<=1,"high-slot Grab changed pixels");}
        std::cout<<"PASS M4k bindings slot=127 stale_unbinds="<<resource.srvUnbinds<<" skip_grab="<<skip<<" copies="<<gpu.grabCopies<<" skipped="<<resource.grabSkips<<"\n";
        // Rebinding must keep the old texture alive for already recorded lists,
        // and release it only once the replacement packet omits the old ID.
        draw.grab=0;tex.resource=image.Get();tex.srgb=0;DrawReplacement replacement{};replacement.previous=sequence[0];replacement.draw=draw;
        UINT successor=UINT(dispatch(ReplaceDraw,token,&replacement));TextureProbeInfo oldInfo{};oldInfo.draw=sequence[0];dispatch(TextureProbe,token,&oldInfo);require(oldInfo.total==1,"old draw retired before replacement consumed");
        f.listChanged=0;dispatch(CommitPacked,token,&f);event(token<<16);dispatch(ResourcePerf,token,&resource);require(resource.retiredDraws==0,"unchanged list retired a live draw");
        sequence[0]=successor;f.listChanged=1;dispatch(CommitPacked,token,&f);event(token<<16);oldInfo.draw=replacement.previous;dispatch(TextureProbe,token,&oldInfo);dispatch(ResourcePerf,token,&resource);require(oldInfo.total==0&&resource.retiredDraws==1,"replacement kept old SRV resources");
        replacement.previous=successor;replacement.draw={};dispatch(ReplaceDraw,token,&replacement);f.list=sequence+1;f.listCount=2;range[1]=2;dispatch(CommitPacked,token,&f);event(token<<16);dispatch(ResourcePerf,token,&resource);require(resource.retiredDraws==2,"explicit pass retirement failed");
        std::cout<<"PASS M4k retirement old_list=preserved replacement_consumed=released removed_pass=released retired="<<resource.retiredDraws<<"\n";
        dispatch(Destroy,token,nullptr);
    }
    // Matched fixed-vs-renamed upload and pretransform controls, with moving
    // verified batch inputs. Changed bytes and visible image must agree.
    std::vector<uint8_t> reference;
    for(UINT mode=0;mode<3;mode++){
        token=dispatch(Create,0,&create);controls={0,mode==2?0u:1u,mode==1?0u:1u,1};dispatch(ResourceConfigure,token,&controls);
        Vertex vertices[3]{};for(int i=0;i<3;i++){std::memcpy(vertices[i].p,triangle+i*3,12);for(float& color:vertices[i].color)color=1;}
        GeometryIn canonical{};canonical.vertices=vertices;canonical.vertexBytes=sizeof(vertices);canonical.strides[0]=sizeof(Vertex);canonical.cpuIndices=indices;canonical.cpuIndexCount=3;canonical.attributes=&a;canonical.attributeCount=1;
        draw.geometry=UINT(dispatch(Geometry,token,&canonical));draw.program=UINT(dispatch(Program,token,&prog));draw.sprite=2;draw.grab=0;draw.textureCount=0;draw.extent[0]=draw.extent[1]=0;
        UINT batchList[3],batchIds[]={0,1,2},range[]={0,3};Pose moving[3]{pose,pose,pose};
        for(UINT i=0;i<3;i++){draw.pose=i;batchList[i]=UINT(dispatch(Draw,token,&draw));}
        f.poses=moving;f.poseIds=batchIds;f.poseCount=3;f.list=batchList;f.listCount=3;f.segments=range;f.listChanged=1;
        for(UINT frame=0;frame<8;frame++){for(auto& p:moving)p.world[12]+=.001f;dispatch(CommitPacked,token,&f);event(token<<16);f.listChanged=0;}
        auto pixels=readback(d.Get(),c.Get(),target.Get());if(mode==0)reference=pixels;else require(reference==pixels,"resource controls changed pixels");
        dispatch(ResourcePerf,token,&resource);BatchPerformance batch{};dispatch(BatchPerf,token,&batch);
        if(mode==0)require(resource.defaultUpdates>0&&resource.defaultBytes>0,"fixed upload path untested");
        if(mode==1)require(resource.defaultUpdates==0&&resource.discardMaps>8,"renamed upload control untested");
        if(mode==2)require(batch.batches==0&&batch.gpuDispatches==0,"pretransform-off still dispatched");
        std::cout<<"PASS M4k uploads mode="<<mode<<" default_updates="<<resource.defaultUpdates<<" default_bytes="<<resource.defaultBytes<<" discard_maps="<<resource.discardMaps<<" gpu_dispatches="<<batch.gpuDispatches<<" parity=exact\n";
        dispatch(Destroy,token,nullptr);
    }
    std::cout<<"PASS M4k resource_controls late=rejected ABI=append_only game=not_launched\n";return 0;
}catch(const std::exception&e){std::cerr<<e.what()<<"\n";return 1;}catch(HRESULT hr){std::cerr<<"HRESULT "<<std::hex<<hr<<"\n";return 2;}}
