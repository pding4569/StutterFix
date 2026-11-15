#define main drfull_legacy_fixture_entry
#include "drfull-warp.cpp"
#undef main
#include "../../native/sfnative/drfull-state.h"
ComPtr<ID3DBlob> compileText(const char* src,const char* entry,const char* profile){ComPtr<ID3DBlob>b,e;HRESULT h=D3DCompile(src,std::strlen(src),nullptr,nullptr,nullptr,entry,profile,D3DCOMPILE_IEEE_STRICTNESS,0,&b,&e);if(FAILED(h)&&e)std::cerr<<static_cast<const char*>(e->GetBufferPointer());check(h);return b;}
int main(){try{
    const int blendExpected[]={1,2,9,3,10,5,4,7,8,11,6},alphaExpected[]={1,2,7,5,8,5,6,7,8,2,6};
    for(int i=0;i<11;i++){require(unityState::blend(i)==blendExpected[i],"blend translation");require(unityState::blendAlpha(i)==alphaExpected[i],"alpha blend translation");}
    const int stencilExpected[]={1,2,3,4,5,6,7,8},reversed[]={8,1,5,3,7,2,6,4,8},addressExpected[]={1,3,2,5};
    for(int i=0;i<8;i++)require(unityState::stencil(i)==stencilExpected[i],"stencil translation");
    for(int i=0;i<9;i++){require(unityState::compare(i)==(i?i:8),"compare translation");require(unityState::compare(i,true)==reversed[i],"reversed depth translation");}
    for(int i=0;i<5;i++)require(unityState::blendOp(i)==i+1,"blend op translation");
    for(int i=0;i<4;i++)require(unityState::address(i)==addressExpected[i],"address translation");
    for(int i=0;i<3;i++)require(unityState::cull(i)==i+1,"cull translation");
    require(unityState::filter(0,1)==0&&unityState::filter(0,16)==0x55&&unityState::filter(1,1)==0x14&&unityState::filter(2,1)==0x15&&unityState::filter(1,4)==0x55,"filter translation");
    unsigned rejected=0;for(int kind=0;kind<8;kind++)try{switch(kind){case 0:unityState::blend(11);break;case 1:unityState::blendOp(5);break;case 2:unityState::colorMask(16);break;case 3:unityState::compare(9);break;case 4:unityState::stencil(8);break;case 5:unityState::cull(3);break;case 6:unityState::address(4);break;case 7:unityState::filter(3,1);break;}}catch(HRESULT){rejected++;}
    require(rejected==8,"invalid serialized state accepted");
    std::cout<<"PASS M4j enum blend=11 alpha=11 ops=5 masks=16 compare=9 reversed=9 stencil=8 cull=3 address=4 filter=4 invalid=8\n";
    ComPtr<ID3D11Device>d;ComPtr<ID3D11DeviceContext>c;D3D_FEATURE_LEVEL fl{};check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&fl,&c));
    D3D11_TEXTURE2D_DESC td{};td.Width=192;td.Height=128;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
    ComPtr<ID3D11Texture2D>target;ComPtr<ID3D11RenderTargetView>rt;check(d->CreateTexture2D(&td,nullptr,&target));check(d->CreateRenderTargetView(target.Get(),nullptr,&rt));
    auto bindTarget=[&](){auto* r=rt.Get();c->OMSetRenderTargets(1,&r,nullptr);D3D11_VIEWPORT vp{0,0,192,128,0,1};c->RSSetViewports(1,&vp);};bindTarget();
    const char* hlsl="cbuffer Data:register(b0){float4 color;float4x4 world;};float4 vs(float3 p:POSITION):SV_Position{return float4(p,1);}float4 ps():SV_Target{return color;}";
    auto vs=compileText(hlsl,"vs","vs_4_0"),ps=compileText(hlsl,"ps","ps_4_0");
    ProgramIn prog{};prog.vs=vs->GetBufferPointer();prog.vsBytes=UINT(vs->GetBufferSize());prog.ps=ps->GetBufferPointer();prog.psBytes=UINT(ps->GetBufferSize());prog.state[0]=prog.state[2]=prog.state[7]=1;prog.state[6]=15;prog.state[20]=prog.state[24]=prog.state[28]=8;prog.state[14]=prog.state[15]=255;
    float triangle[]={-1,1,.5f,3,1,.5f,-1,-3,.5f};UINT indices[]={0,1,2};auto vb=buffer(d.Get(),triangle,sizeof(triangle),D3D11_BIND_VERTEX_BUFFER),ib=buffer(d.Get(),indices,sizeof(indices),D3D11_BIND_INDEX_BUFFER);Attribute a{0,0,6,0,0};GeometryIn geo{};geo.buffers[0]=vb.Get();geo.strides[0]=12;geo.indices=ib.Get();geo.indexFormat=42;geo.indexCount=3;geo.attributes=&a;geo.attributeCount=1;
    CreateIn create{target.Get(),512,1};int token=dispatch(Create,0,&create);DrawProbeConfiguration config{1,0,256,1};dispatch(DrawProbeConfigure,token,&config);
    Buffer binding{1,0,16384,0};Patch patch{16,1};std::vector<uint8_t>data(16384);float color[]={.61f,.23f,.79f,.47f};std::memcpy(data.data(),color,16);
    DrawIn draw{};draw.program=UINT(dispatch(Program,token,&prog));draw.geometry=UINT(dispatch(Geometry,token,&geo));draw.buffers=&binding;draw.bufferCount=1;draw.patches=&patch;draw.patchCount=1;draw.constants=data.data();draw.constantBytes=UINT(data.size());
    Pose pose{};for(int i=0;i<16;i++)pose.world[i]=pose.inverse[i]=i%5==0?1.f:0.f;
    UINT poseId=0,list=0,seg[]={0,1},zero=0;Frame f{};f.poses=&pose;f.poseIds=&poseId;f.poseCount=1;f.list=&list;f.listCount=1;f.segments=seg;f.segmentCount=1;f.listChanged=1;f.constantOffsets=&zero;
    auto event=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(Event,0,nullptr));
    float bg[]={.12f,.34f,.56f,.78f};
    for(int mask=0;mask<16;mask++){
        prog.state[6]=mask;draw.program=UINT(dispatch(Program,token,&prog));list=UINT(dispatch(Draw,token,&draw));c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitPacked,token,&f);event(token<<16);auto pixels=readback(d.Get(),c.Get(),target.Get());
        for(int channel=0;channel<4;channel++){int expected=int(std::round(((mask&(8>>channel))?color[channel]:bg[channel])*255));require(std::abs(int(pixels[100*4+channel])-expected)<=1,"ColorMask channel preservation");}
    }
    prog.state[6]=15;prog.state[1]=6;draw.program=UINT(dispatch(Program,token,&prog));list=UINT(dispatch(Draw,token,&draw));c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitPacked,token,&f);event(token<<16);auto blendPixels=readback(d.Get(),c.Get(),target.Get());
    for(int k=0;k<3;k++)require(std::abs(int(blendPixels[400+k])-int(std::round((color[k]+bg[k]*(1-color[k]))*255)))<=1,"OneMinusSrcColor used source alpha");
    for(int op=3;op<=4;op++){prog.state[1]=0;prog.state[4]=prog.state[5]=op;draw.program=UINT(dispatch(Program,token,&prog));list=UINT(dispatch(Draw,token,&draw));c->ClearRenderTargetView(rt.Get(),bg);dispatch(CommitPacked,token,&f);event(token<<16);auto pixels=readback(d.Get(),c.Get(),target.Get());for(int k=0;k<4;k++)require(std::abs(int(pixels[400+k])-int(std::round((op==3?std::min(color[k],bg[k]):std::max(color[k],bg[k]))*255)))<=1,"Min/Max blend disabled for One Zero");}
    std::cout<<"PASS M4j production pixels masks=16 RGB_A_zero=True OneMinusSrcColor=True MinMax=True\n";
    dispatch(Destroy,token,nullptr);token=dispatch(Create,0,&create);config={1,0,8,1};dispatch(DrawProbeConfigure,token,&config);prog.state[4]=prog.state[5]=0;draw.program=UINT(dispatch(Program,token,&prog));draw.geometry=UINT(dispatch(Geometry,token,&geo));
    constexpr UINT population=384;std::vector<UINT>ids(population),draws(population);std::vector<Pose>poses(population,pose);
    for(UINT i=0;i<population;i++){draw.pose=i;ids[i]=i;draws[i]=UINT(dispatch(Draw,token,&draw));}
    seg[1]=population;f.poses=poses.data();f.poseIds=ids.data();f.poseCount=population;f.list=draws.data();f.listCount=population;dispatch(CommitPacked,token,&f);event(token<<16);f.listChanged=0;
    SubmissionPerformance start{},end{};dispatch(SubmissionPerf,token,&start);require(start.ringAvailable==1,"NO_OVERWRITE feature must be tested");
    auto beforePixels=readback(d.Get(),c.Get(),target.Get());
    for(int i=0;i<16;i++){dispatch(CommitPacked,token,&f);event(token<<16);}dispatch(SubmissionPerf,token,&end);require(end.ringBytes==start.ringBytes,"static 6MB constants reuploaded");
    uint64_t sparseBytes=end.ringBytes;f.poses=&poses.back();f.poseIds=&ids.back();f.poseCount=1;
    for(int i=0;i<192;i++){poses.back().world[12]+=.001f;dispatch(CommitPacked,token,&f);event(token<<16);}dispatch(SubmissionPerf,token,&end);
    // One arena rollover recopies at most one live 6MB set. Rest are 16KB updates.
    uint64_t uploaded=end.ringBytes-sparseBytes;require(uploaded<=uint64_t(192)*16384+uint64_t(population)*16384,"sparse changes reupload full ring");
    require(readback(d.Get(),c.Get(),target.Get())==beforePixels,"NO_OVERWRITE/rollover corrupted unchanged colors");
    DrawProbeReadback q{};q.index=UINT32_MAX;dispatch(DrawProbeRead,token,&q);require(q.total>0&&q.invalid==0,"draw GPU intervals missing/disjoint");
    for(UINT i=0;i<q.total;i++){q.index=i;dispatch(DrawProbeRead,token,&q);require(q.row.draw>0&&q.row.count==1&&q.row.indices==3&&q.row.us>=0&&q.row.us<=q.row.segmentUs,"GPU row identity/time invalid");}
    // Material detach changes visible output, then restores it; all other CBs
    // retain their persistent offsets. Readback also drains pending probe rows.
    UINT dirty=draws.back(),ends[]={0,16384};f.drawIds=&dirty;f.dirtyCount=1;f.constants=data.data();f.constantBytes=16384;f.constantOffsets=ends;
    reinterpret_cast<float*>(data.data())[0]=.31f;dispatch(CommitPacked,token,&f);event(token<<16);
    auto edited=readback(d.Get(),c.Get(),target.Get());require(std::abs(int(edited[400])-79)<=1,"sparse material edit missing");
    reinterpret_cast<float*>(data.data())[0]=color[0];dispatch(CommitPacked,token,&f);event(token<<16);
    require(readback(d.Get(),c.Get(),target.Get())==beforePixels,"sparse material restore mismatch");
    f.dirtyCount=f.constantBytes=0;f.constantOffsets=&zero;UINT twoSegments[]={0,population/2,population/2,population/2};f.segments=twoSegments;f.segmentCount=2;f.listChanged=1;
    dispatch(CommitPacked,token,&f);event(token<<16);event((token<<16)|1);f.listChanged=0;
    SubmissionPerformance splitStart{},splitEnd{};dispatch(SubmissionPerf,token,&splitStart);
    for(int i=0;i<16;i++){dispatch(CommitPacked,token,&f);event(token<<16);event((token<<16)|1);}dispatch(SubmissionPerf,token,&splitEnd);
    require(splitEnd.ringBytes==splitStart.ringBytes,"segment switching reuploaded resident constants");
    require(readback(d.Get(),c.Get(),target.Get())==beforePixels,"multiple segments corrupted constants");
    std::cout<<"PASS M4j sparse material_edit=visible material_restore=exact multi_segment_static_upload=0\n";
    Statistics stats{};dispatch(Stats,token,&stats);require(stats.errors==0,"native errors");
    std::cout<<"PASS M4j ring live_bytes=6291456 static_frames=16 static_upload=0 sparse_frames=192 dirty_bytes=16384 uploaded="<<uploaded<<" full_reupload_bound=1 parity=exact probe_rows="<<q.total<<" probe_invalid="<<q.invalid<<"\n";
    dispatch(Destroy,token,nullptr);return 0;
}catch(const std::exception&e){std::cerr<<e.what()<<"\n";return 1;}catch(HRESULT hr){std::cerr<<"HRESULT "<<std::hex<<hr<<"\n";return 2;}}
