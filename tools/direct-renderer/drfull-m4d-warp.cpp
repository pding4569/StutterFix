// Standalone software D3D11 fixtures. Includes the existing fixture's helpers,
// but the entry point below only tests M4d production composite/stencil paths.
#define main m4bc_fixture_entry
#include "drfull-warp.cpp"
#undef main

ComPtr<ID3DBlob> code(const char* source,const char* entry,const char* profile){
    ComPtr<ID3DBlob> b,e;HRESULT hr=D3DCompile(source,std::strlen(source),nullptr,nullptr,nullptr,entry,profile,D3DCOMPILE_OPTIMIZATION_LEVEL3|D3DCOMPILE_IEEE_STRICTNESS,0,&b,&e);
    if(FAILED(hr)&&e)std::cerr<<static_cast<const char*>(e->GetBufferPointer());check(hr);return b;
}
struct Target {
    ComPtr<ID3D11Texture2D> texture;ComPtr<ID3D11RenderTargetView> rtv;
    Target(ID3D11Device* d,DXGI_FORMAT format){D3D11_TEXTURE2D_DESC td{};td.Width=64;td.Height=48;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=format;td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;check(d->CreateTexture2D(&td,nullptr,&texture));D3D11_RENDER_TARGET_VIEW_DESC vd{};vd.Format=format==DXGI_FORMAT_R32G32B32A32_TYPELESS?DXGI_FORMAT_R32G32B32A32_FLOAT:format;vd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;check(d->CreateRenderTargetView(texture.Get(),&vd,&rtv));}
};
std::vector<float> floats(ID3D11Device* d,ID3D11DeviceContext* c,ID3D11Texture2D* texture){
    D3D11_TEXTURE2D_DESC td{};texture->GetDesc(&td);td.BindFlags=0;td.Usage=D3D11_USAGE_STAGING;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;ComPtr<ID3D11Texture2D> s;check(d->CreateTexture2D(&td,nullptr,&s));c->CopyResource(s.Get(),texture);D3D11_MAPPED_SUBRESOURCE m{};check(c->Map(s.Get(),0,D3D11_MAP_READ,0,&m));
    std::vector<float> v(size_t(td.Width)*td.Height*4);for(UINT y=0;y<td.Height;y++)std::memcpy(v.data()+size_t(y)*td.Width*4,static_cast<const uint8_t*>(m.pData)+size_t(y)*m.RowPitch,size_t(td.Width)*16);c->Unmap(s.Get(),0);return v;
}
void setTarget(ID3D11DeviceContext* c,Target& t,ID3D11DepthStencilView* depth=nullptr){auto* rt=t.rtv.Get();c->OMSetRenderTargets(1,&rt,depth);D3D11_VIEWPORT v{0,0,64,48,0,1};c->RSSetViewports(1,&v);}
void identity(float* m){for(int i=0;i<16;i++)m[i]=i%5==0?1.f:0.f;}
Frame fixtureFrame(Pose& pose,uint32_t* list,uint32_t* seg,uint32_t* zero,uint8_t* bytes){
    Frame f{};identity(f.vp);identity(f.view);identity(f.projection);f.poses=&pose;f.poseIds=zero;f.poseCount=1;f.drawIds=zero;f.constantOffsets=zero;f.constants=bytes;f.list=list;f.segments=seg;f.listCount=1;f.segmentCount=1;f.listChanged=1;return f;
}
int main(int argc,char** argv){try{
    require(argc==2,"usage: drfull-m4d-warp output-directory");std::filesystem::path out=argv[1];std::filesystem::create_directories(out);
    ComPtr<ID3D11Device> d;ComPtr<ID3D11DeviceContext> c;D3D_FEATURE_LEVEL level{};check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&level,&c));require(level>=D3D_FEATURE_LEVEL_11_0,"FL11");
    const char* shader=R"(
        cbuffer Data:register(b0){float4 color;float4 bounds;float4 options;};
        float4 vs(float3 p:POSITION):SV_Position{return float4(p,1);}
        float4 ps(float4 p:SV_Position):SV_Target{clip(p.x-bounds.x);clip(bounds.z-p.x);clip(p.y-bounds.y);clip(bounds.w-p.y);
            float4 o=color;o.a*=options.y!=0?(0.25+0.75*((uint)p.x%7)/6):1;if(options.x!=0)o.rgb*=o.a;return o;}
        float4 mask(float4 p:SV_Position):SV_Target{clip(39.5-p.x);clip(31.5-p.y);return 0;}
    )";
    auto v=code(shader,"vs","vs_4_0"),p=code(shader,"ps","ps_4_0"),m=code(shader,"mask","ps_4_0");
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;check(d->CreateVertexShader(v->GetBufferPointer(),v->GetBufferSize(),nullptr,&vs));check(d->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&ps));
    const float triangle[]={-1,1,.5f,3,1,.5f,-1,-3,.5f};uint32_t indices[]={0,1,2};
    auto vb=buffer(d.Get(),triangle,sizeof(triangle),D3D11_BIND_VERTEX_BUFFER),ib=buffer(d.Get(),indices,sizeof(indices),D3D11_BIND_INDEX_BUFFER);
    D3D11_INPUT_ELEMENT_DESC input{"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0};ComPtr<ID3D11InputLayout> layout;check(d->CreateInputLayout(&input,1,v->GetBufferPointer(),v->GetBufferSize(),&layout));
    float constants[12]{};auto cb=buffer(d.Get(),constants,sizeof(constants),D3D11_BIND_CONSTANT_BUFFER);
    D3D11_RASTERIZER_DESC rd{};rd.FillMode=D3D11_FILL_SOLID;rd.CullMode=D3D11_CULL_NONE;rd.DepthClipEnable=TRUE;ComPtr<ID3D11RasterizerState> raster;check(d->CreateRasterizerState(&rd,&raster));
    D3D11_DEPTH_STENCIL_DESC dd{};dd.DepthFunc=D3D11_COMPARISON_ALWAYS;ComPtr<ID3D11DepthStencilState> noDepth;check(d->CreateDepthStencilState(&dd,&noDepth));
    auto geometry=[&](){ID3D11Buffer* b=vb.Get();UINT stride=12,offset=0;c->IASetVertexBuffers(0,1,&b,&stride,&offset);c->IASetInputLayout(layout.Get());c->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);c->VSSetShader(vs.Get(),nullptr,0);c->PSSetShader(ps.Get(),nullptr,0);c->RSSetState(raster.Get());ID3D11Buffer* cc=cb.Get();c->PSSetConstantBuffers(0,1,&cc);};
    std::array<ComPtr<ID3D11BlendState>,2> blends;
    for(int i=0;i<2;i++){D3D11_BLEND_DESC bd{};auto& b=bd.RenderTarget[0];b.BlendEnable=TRUE;b.SrcBlend=b.SrcBlendAlpha=i?D3D11_BLEND_ONE:D3D11_BLEND_SRC_ALPHA;b.DestBlend=b.DestBlendAlpha=D3D11_BLEND_INV_SRC_ALPHA;b.BlendOp=b.BlendOpAlpha=D3D11_BLEND_OP_ADD;b.RenderTargetWriteMask=15;check(d->CreateBlendState(&bd,&blends[i]));}
    Target black(d.Get(),DXGI_FORMAT_R32G32B32A32_TYPELESS),white(d.Get(),DXGI_FORMAT_R32G32B32A32_TYPELESS),direct(d.Get(),DXGI_FORMAT_R32G32B32A32_FLOAT),composed(d.Get(),DXGI_FORMAT_R32G32B32A32_FLOAT);
    CreateIn create{direct.texture.Get(),16,1};int token=dispatch(Create,0,&create);CompositeIn composite{black.texture.Get(),white.texture.Get()};int draw=dispatch(Composite,token,&composite);
    Pose pose{};identity(pose.world);identity(pose.inverse);uint32_t zero[]={0},list[]={UINT(draw)},seg[]={0,1};uint8_t bytes[16]{};Frame f=fixtureFrame(pose,list,seg,zero,bytes);auto event=reinterpret_cast<void(__stdcall*)(int)>(sf_z_drfull_v1(Event,0,nullptr));
    unsigned cases=0,rgba8Max=0;uint64_t rgba8Changed=0;float floatMax=0;unsigned naiveFailed=0;
    std::ofstream rows(out/"composition.tsv");rows<<"case\tfloat_max\tRGBA8_changed\tRGBA8_max\n";
    Target d8(d.Get(),DXGI_FORMAT_R8G8B8A8_UNORM),c8(d.Get(),DXGI_FORMAT_R8G8B8A8_UNORM);
    auto paint=[&](Target& t,const float* background,int mode,int scene){
        setTarget(c.Get(),t);c->ClearRenderTargetView(t.rtv.Get(),background);geometry();c->OMSetDepthStencilState(noDepth.Get(),0);c->OMSetBlendState(blends[mode].Get(),nullptr,~0u);
        for(int k=0;k<4;k++){
            constants[0]=.12f+.17f*float(k);constants[1]=.73f-.13f*float(k);constants[2]=.21f+.09f*float(k);constants[3]=scene==0?0:scene==1?1:.17f+.16f*float(k);
            constants[4]=float(k*7);constants[5]=float(k*4);constants[6]=float(64-k*3);constants[7]=float(48-k*2);constants[8]=float(mode);constants[9]=scene==3?1.f:0;
            c->UpdateSubresource(cb.Get(),0,nullptr,constants,0,0);c->Draw(3,0);
        }
    };
    const float clear[]={0,0,0,0},opaque[]={1,1,1,1};
    for(int mode=0;mode<2;mode++)for(int scene=0;scene<4;scene++)for(int bg=0;bg<3;bg++){
        float background[]={.11f+.23f*float(bg),.27f,.69f-.19f*float(bg),.19f+.31f*float(bg)};
        paint(black,clear,mode,scene);paint(white,opaque,mode,scene);paint(direct,background,mode,scene);
        setTarget(c.Get(),composed);c->ClearRenderTargetView(composed.rtv.Get(),background);dispatch(Commit,token,&f);event(token<<16);
        auto expected=floats(d.Get(),c.Get(),direct.texture.Get()),actual=floats(d.Get(),c.Get(),composed.texture.Get());float maximum=0;
        for(size_t i=0;i<actual.size();i++)maximum=std::max(maximum,std::abs(expected[i]-actual[i]));require(maximum<.000002f,"affine four-channel composite mismatch");floatMax=std::max(floatMax,maximum);
        paint(d8,background,mode,scene);setTarget(c.Get(),c8);c->ClearRenderTargetView(c8.rtv.Get(),background);dispatch(Commit,token,&f);event(token<<16);
        auto a=readback(d.Get(),c.Get(),d8.texture.Get()),b=readback(d.Get(),c.Get(),c8.texture.Get());unsigned max8=0;uint64_t changed8=0;
        for(size_t i=0;i<a.size();i+=4){bool changed=false;for(int k=0;k<4;k++){unsigned diff=unsigned(std::abs(int(a[i+size_t(k)])-int(b[i+size_t(k)])));max8=std::max(max8,diff);changed|=diff!=0;}changed8+=changed;}
        require(max8<=2,"RGBA8 bounded quantization mismatch");rgba8Max=std::max(rgba8Max,max8);rgba8Changed+=changed8;
        if(mode==0&&scene==2){auto blk=floats(d.Get(),c.Get(),black.texture.Get());bool fails=false;for(size_t i=0;i<blk.size();i+=4)fails|=std::abs((blk[i]+background[0]*(1-blk[i+3]))-expected[i])>.01f;require(fails,"negative control: naive UI alpha unexpectedly matched");naiveFailed++;}
        rows<<cases++<<'\t'<<maximum<<'\t'<<changed8<<'\t'<<max8<<'\n';
    }
    Statistics stats{};dispatch(Stats,token,&stats);require(stats.errors==0&&stats.draws==48,"production compositor draw coverage");dispatch(Destroy,token,nullptr);
    // Native increment/decrement draws and independent Unity-style target draws
    // use the SAME DSV. This validates the reconstructed Ref=0/NotEqual|Equal
    // protocol; it does not execute Unity's internal SpriteRenderer state injection.
    D3D11_TEXTURE2D_DESC td{};td.Width=64;td.Height=48;td.MipLevels=td.ArraySize=td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_D24_UNORM_S8_UINT;td.BindFlags=D3D11_BIND_DEPTH_STENCIL;
    ComPtr<ID3D11Texture2D> depth;ComPtr<ID3D11DepthStencilView> depthView;check(d->CreateTexture2D(&td,nullptr,&depth));check(d->CreateDepthStencilView(depth.Get(),nullptr,&depthView));
    token=dispatch(Create,0,&create);Attribute attribute{0,0,6,0,0};GeometryIn g{};g.buffers[0]=vb.Get();g.strides[0]=12;g.indices=ib.Get();g.indexFormat=DXGI_FORMAT_R32_UINT;g.indexCount=3;g.attributes=&attribute;g.attributeCount=1;int gid=dispatch(Geometry,token,&g);
    ProgramIn program{};program.vs=v->GetBufferPointer();program.vsBytes=UINT(v->GetBufferSize());program.ps=m->GetBufferPointer();program.psBytes=UINT(m->GetBufferSize());program.state[0]=program.state[2]=1;program.state[7]=1;program.state[14]=program.state[15]=255;
    program.state[20]=program.state[24]=program.state[28]=8;
    auto maskDraw=[&](int operation){program.state[17]=operation;DrawIn in{};in.program=UINT(dispatch(Program,token,&program));in.geometry=UINT(gid);in.constants=bytes;in.constantBytes=16;return dispatch(Draw,token,&in);};
    int push=maskDraw(3),pop=maskDraw(4);unsigned stencilCases=0;
    auto nativeMask=[&](int id){list[0]=UINT(id);setTarget(c.Get(),direct,depthView.Get());dispatch(Commit,token,&f);event(token<<16);};
    c->ClearDepthStencilView(depthView.Get(),D3D11_CLEAR_DEPTH|D3D11_CLEAR_STENCIL,0,0);
    auto targetTest=[&](bool inside,bool present){
        setTarget(c.Get(),direct,depthView.Get());c->ClearRenderTargetView(direct.rtv.Get(),clear);geometry();
        dd.StencilEnable=TRUE;dd.StencilReadMask=255;dd.StencilWriteMask=0;dd.FrontFace.StencilFunc=inside?D3D11_COMPARISON_NOT_EQUAL:D3D11_COMPARISON_EQUAL;dd.FrontFace.StencilPassOp=dd.FrontFace.StencilFailOp=dd.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;dd.BackFace=dd.FrontFace;
        ComPtr<ID3D11DepthStencilState> targetState;check(d->CreateDepthStencilState(&dd,&targetState));c->OMSetDepthStencilState(targetState.Get(),0);c->OMSetBlendState(nullptr,nullptr,~0u);
        for(int i=0;i<4;i++)constants[i]=1;constants[4]=constants[5]=0;constants[6]=64;constants[7]=48;constants[8]=constants[9]=0;c->UpdateSubresource(cb.Get(),0,nullptr,constants,0,0);c->Draw(3,0);
        auto image=floats(d.Get(),c.Get(),direct.texture.Get());for(unsigned y=0;y<48;y++)for(unsigned x=0;x<64;x++){bool in=present&&x<40&&y<32;float expect=(inside?in:!in)?1.f:0.f;require(image[(size_t(y)*64+x)*4]==expect,"shared stencil target mismatch");}stencilCases++;
    };
    targetTest(true,false);targetTest(false,false);nativeMask(push);targetTest(true,true);targetTest(false,true);nativeMask(push);targetTest(true,true);nativeMask(pop);targetTest(true,true);targetTest(false,true);nativeMask(pop);targetTest(true,false);targetTest(false,false);
    dispatch(Stats,token,&stats);require(stats.errors==0&&stats.draws==4,"native stencil draw coverage");dispatch(Destroy,token,nullptr);event(token<<16);
    require(cases==24&&naiveFailed==3&&stencilCases==9,"fixture coverage");
    std::ofstream summary(out/"summary.json");summary<<"{\"adapter\":\"WARP\",\"composition_cases\":"<<cases<<",\"float_max_error\":"<<floatMax<<",\"rgba8_max_channel\":"<<rgba8Max<<",\"rgba8_changed_pixels\":"<<rgba8Changed<<",\"naive_alpha_rejections\":"<<naiveFailed<<",\"shared_stencil_cases\":"<<stencilCases<<",\"Unity_internal_state\":\"UNMEASURED\"}\n";
    std::cout<<"PASS WARP composite="<<cases<<" floatMax="<<floatMax<<" RGBA8max="<<rgba8Max<<" RGBA8changed="<<rgba8Changed<<" naive-alpha-rejected="<<naiveFailed<<" shared-stencil="<<stencilCases<<" UnityInternalState=UNMEASURED\n";return 0;
}catch(HRESULT hr){std::cerr<<"FAIL HRESULT 0x"<<std::hex<<hr<<'\n';return 1;}catch(const std::exception& e){std::cerr<<"FAIL "<<e.what()<<'\n';return 1;}}
