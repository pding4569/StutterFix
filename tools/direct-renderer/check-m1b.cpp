// Offline WARP regression for the actual DR2 renderer, no Unity/game process.
#include "../../native/sfnative/dr2.cpp"
#include <d3dcompiler.h>
#include <cstdio>
#include <stdexcept>
#include <string>
static void require(bool ok, const char* message) { if (!ok) throw std::runtime_error(message); }
static void hr(HRESULT value) { require(SUCCEEDED(value), "D3D11 operation failed"); }
struct Fixture {
    ComPtr<ID3D11Device> dev; ComPtr<ID3D11DeviceContext> ctx;
    ComPtr<ID3D11Texture2D> output, readback, texture; ComPtr<ID3D11RenderTargetView> rtv;
    int id = 0; static constexpr UINT size = 32;
    float channels[32]{}, statics[64]{}; uint32_t slots[2]{0,1}, list[2]{0,1}, segs[2]{0,1}; Run run{};
    float camera[4]{0,0,0,1}, ortho[4]{1,1,1,0};
    Fixture() {
        hr(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &dev, nullptr, &ctx));
        D3D11_TEXTURE2D_DESC td{}; td.Width=td.Height=size; td.MipLevels=td.ArraySize=1; td.Format=DXGI_FORMAT_R8G8B8A8_UNORM; td.SampleDesc.Count=1; td.BindFlags=D3D11_BIND_RENDER_TARGET;
        hr(dev->CreateTexture2D(&td,nullptr,&output)); hr(dev->CreateRenderTargetView(output.Get(),nullptr,&rtv));
        td.BindFlags=0; td.Usage=D3D11_USAGE_STAGING; td.CPUAccessFlags=D3D11_CPU_ACCESS_READ; hr(dev->CreateTexture2D(&td,nullptr,&readback));
        id=sf_dr2_create(output.Get(),2); require(id!=0,"DR2 create");
        for (unsigned i=0;i<2;i++) {
            float* s=statics+i*32; s[0]=s[5]=2; s[2]=s[6]=-1; s[8]=s[9]=s[14]=s[15]=s[16]=s[17]=s[22]=s[23]=s[24]=s[25]=s[26]=s[27]=s[28]=1;
            float* c=channels+i*16; c[5]=c[6]=c[7]=c[8]=c[9]=c[10]=c[11]=1;
        }
        run.count=1; run.texture=0;
    }
    ~Fixture() { sf_dr2_destroy(id); }
    void image(const std::vector<uint32_t>& pixels, UINT width, UINT height, bool srgb=false) {
        D3D11_TEXTURE2D_DESC td{}; td.Width=width;td.Height=height;td.MipLevels=td.ArraySize=1;td.SampleDesc.Count=1;td.Format=srgb?DXGI_FORMAT_R8G8B8A8_TYPELESS:DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
        D3D11_SUBRESOURCE_DATA data{};data.pSysMem=pixels.data();data.SysMemPitch=width*4;texture.Reset();hr(dev->CreateTexture2D(&td,&data,&texture));
        require(sf_dr2_texture(id,0,texture.Get(),srgb?256:0,1,1,1)==1,"DR2 texture");
        for(unsigned i=0;i<2;i++){statics[i*32+30]=float(width);statics[i*32+31]=float(height);}
    }
    std::vector<uint32_t> draw(unsigned mode=0,unsigned count=1,uint32_t flags=0) {
        float clear[4]={64.f/255,128.f/255,192.f/255,1};ctx->ClearRenderTargetView(rtv.Get(),clear);
        ID3D11RenderTargetView* target=rtv.Get();ctx->OMSetRenderTargets(1,&target,nullptr);D3D11_VIEWPORT viewport{0,0,float(size),float(size),0,1};ctx->RSSetViewports(1,&viewport);
        ctx->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_LINELIST);
        for(unsigned i=0;i<2;i++)statics[i*32+19]=float(mode);
        require(sf_dr2_statics(id,0,2,statics)==1,"DR2 statics");
        FrameIn f{}; f.vp[0]=f.vp[5]=f.vp[10]=f.vp[15]=1;std::memcpy(f.cam,camera,sizeof(camera));std::memcpy(f.ortho,ortho,sizeof(ortho));
        f.dirtyCount=2;f.dirtySlots=slots;f.channels=channels;f.listChanged=1;f.list=list;f.runs=&run;f.segs=segs;f.listCount=count;f.runCount=f.segCount=1;
        run.count=count;run.flags=flags|(mode<<8);require(sf_dr2_commit(id,&f)==1,"DR2 commit");onEvent(id<<12);
        require(sf_dr2_stat(id,4)==0,"DR2 render errors");
        D3D11_PRIMITIVE_TOPOLOGY topology{};ctx->IAGetPrimitiveTopology(&topology);require(topology==D3D11_PRIMITIVE_TOPOLOGY_LINELIST,"immediate topology restored");
        ComPtr<ID3D11RenderTargetView> restored;ctx->OMGetRenderTargets(1,&restored,nullptr);require(restored==rtv,"immediate RT restored");
        ctx->CopyResource(readback.Get(),output.Get());D3D11_MAPPED_SUBRESOURCE map{};hr(ctx->Map(readback.Get(),0,D3D11_MAP_READ,0,&map));
        std::vector<uint32_t> pixels(size*size);for(UINT y=0;y<size;y++)std::memcpy(pixels.data()+y*size,static_cast<const char*>(map.pData)+y*map.RowPitch,size*4);ctx->Unmap(readback.Get(),0);return pixels;
    }
};
static int channelByte(uint32_t v,unsigned k){return int((v>>(k*8))&255u);}
static float clamp01(float v){return std::max(0.f,std::min(1.f,v));}
static float blendValue(unsigned mode,float b,float s){switch(mode){
    case 1:return std::min(b,s);case 2:return b*s;case 4:return b+s-1;case 6:return std::max(b,s);case 7:return 1-(1-b)*(1-s);case 9:return b+s;
    case 11:return b>.5f?1-2*(1-b)*(1-s):2*b*s;case 12:return b*(1-b)*s+(1-(1-b)*(1-s))*b;
    case 18:return std::abs(b-s);case 19:return b+s-2*b*s;case 20:return b-s;case 21:return clamp01(b/s);default:return s;}}
static int quant(float f){return int(std::floor(clamp01(f)*255+.5f));}
static void blendCheck(){
    Fixture f;const uint32_t texel=0x803366cc;f.image({texel},1,1);unsigned cases=0,maxDelta=0;
    for(unsigned mode:{0u,1u,2u,4u,6u,7u,9u,11u,12u,18u,19u,20u,21u})for(unsigned count:{1u,2u}){
        auto pixels=f.draw(mode,count);int expected[4]={64,128,192,255};float alpha=128.f/255;
        for(unsigned n=0;n<count;n++)for(unsigned k=0;k<3;k++){
            float b=expected[k]/255.f,s=channelByte(texel,k)/255.f;
            expected[k]=quant(clamp01(blendValue(mode,b,s)*alpha)+b*(1-alpha));
        }
        for(auto p:pixels)for(unsigned k=0;k<4;k++){
            unsigned delta=unsigned(std::abs(channelByte(p,k)-expected[k]));maxDelta=std::max(maxDelta,delta);
            if(delta>2){std::printf("blend failure mode=%u count=%u channel=%u expected=%d actual=%d\n",mode,count,k,expected[k],channelByte(p,k));throw std::runtime_error("blend analytic parity");}
        }
        cases++;
    }
    require(sf_dr2_stat(f.id,9)==36,"each Grab instance takes its own snapshot");
    std::printf("WARP analytic RGBA: %u cases (normal + 12 blend modes, 1/2 sequential instances), max_channel_delta=%u; immediate state restored\n",cases,maxDelta);
}
static void uvCheck(){
    Fixture f;const std::vector<uint32_t> colors={0xff0000ff,0xff00ff00,0xffff0000,0xffffffff};f.image(colors,2,2);
    for(unsigned flip=0;flip<4;flip++){
        f.statics[20]=(flip&1)?1.f:0.f;f.statics[21]=(flip&2)?1.f:0.f;f.statics[22]=(flip&1)?-1.f:1.f;f.statics[23]=(flip&2)?-1.f:1.f;
        auto pixels=f.draw(0,1,1);for(unsigned y=0;y<32;y++)for(unsigned x=0;x<32;x++){
            unsigned u=(x>=16?1u:0u)^((flip&1)?1u:0u),v=(y<16?1u:0u)^((flip&2)?1u:0u);
            require(pixels[y*32+x]==colors[v*2+u],"UV orientation / point sample parity");
        }
    }
    // Swapped UV axes (off diagonal terms ux.w / uy.w).
    f.statics[20]=f.statics[21]=f.statics[22]=f.statics[23]=0;f.statics[3]=f.statics[7]=1;
    auto pixels=f.draw();for(unsigned y=0;y<32;y++)for(unsigned x=0;x<32;x++)require(pixels[y*32+x]==colors[(x>=16?2u:0u)+(y<16?1u:0u)],"affine UV transpose");
    std::puts("WARP asymmetric 2x2 texture: 4 UV orientations + transpose, 5120 pixels exact");
}
static void regionAndSrgbCheck(){
    Fixture f;f.image({0xff808080},1,1,true);auto pixels=f.draw();require(std::abs(channelByte(pixels[0],0)-55)<=1,"sRGB texture decoding");
    f.image({0x803366cc},1,1);f.statics[0]=f.statics[5]=.5f;f.statics[2]=f.statics[6]=-.25f;
    pixels=f.draw(2);unsigned changed=0;for(auto p:pixels)if(p!=0xffc08040)changed++;
    require(changed==64,"small sprite changes only its 8x8 rectangle");require(sf_dr2_stat(f.id,10)==144,"Grab copies 12x12 including guard, not full RT");
    std::puts("WARP sRGB decode passed; 8x8 sprite copies 12x12 guarded rectangle (144/1024 pixels)");
}
static std::array<float,2> referencePoint(const Fixture& f,unsigned corner){
    const float* c=f.channels;const float* s=f.statics;unsigned flags=unsigned(s[18]);float wx,wy;
    if(flags&128){float x=c[0]+c[2],y=c[1]+c[3];if(flags&256)x*=f.ortho[2];x=(2*(x/20+.5f)-1)*f.ortho[0]*f.ortho[1];y=(2*(y/20+.5f)-1)*f.ortho[0];float a=f.camera[2]*0.01745329238474369f;wx=f.camera[0]+std::cos(a)*x-std::sin(a)*y;wy=f.camera[1]+std::sin(a)*x+std::cos(a)*y;}
    else{wx=(f.camera[0]-c[0])*c[12]+c[0];wy=(f.camera[1]-c[1])*c[13]+c[1];}
    float mx=((flags&1)?f.camera[3]:1)*s[8],my=((flags&1)?f.camera[3]:1)*s[9];wx+=c[14]*mx/s[14];wy+=c[15]*my/s[15];
    float half=(s[12]+c[4]+((flags&2)?f.camera[2]:0))*0.01745329238474369f*.5f,qz=std::sin(half),qw=std::cos(half),norm=std::sqrt(qz*qz+qw*qw);qz/=norm;qw/=norm;
    float sn=qw*(qz*2),cs=1-qz*(qz*2),sx=c[5]*mx,sy=c[6]*my;
    // Independent TRS point path: child affine -> pivot translation -> scale/rotation -> world.
    float x=(s[0]*float(corner&1)+s[1]*float(corner>>1)+s[2])+(c[2]+s[10])/s[8];
    float y=(s[4]*float(corner&1)+s[5]*float(corner>>1)+s[6])+(c[3]+s[11])/s[9];
    return {wx+cs*sx*x-sn*sy*y,wy+sn*sx*x+cs*sy*y};
}
static void vertexCheck(){
    Fixture f;f.image({0xffffffff},1,1);auto r=get(f.id);
    const char* gs="struct V{float4 p:SV_Position;float4 c:COLOR0;float2 u:TEXCOORD0;};[maxvertexcount(1)]void Probe(point V a[1],inout PointStream<V> o){o.Append(a[0]);}";
    ComPtr<ID3DBlob> code,errors;hr(D3DCompile(gs,std::strlen(gs),nullptr,nullptr,nullptr,"Probe","gs_5_0",D3DCOMPILE_ENABLE_STRICTNESS,0,&code,&errors));
    D3D11_SO_DECLARATION_ENTRY decl[]={{0,"SV_Position",0,0,4,0},{0,"COLOR",0,0,4,0},{0,"TEXCOORD",0,0,2,0}};UINT stride=40;
    ComPtr<ID3D11GeometryShader> probe;hr(f.dev->CreateGeometryShaderWithStreamOutput(code->GetBufferPointer(),code->GetBufferSize(),decl,3,&stride,1,D3D11_SO_NO_RASTERIZED_STREAM,nullptr,&probe));
    D3D11_BUFFER_DESC bd{};bd.ByteWidth=4*stride;bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_STREAM_OUTPUT;ComPtr<ID3D11Buffer> buffer,read;
    hr(f.dev->CreateBuffer(&bd,nullptr,&buffer));bd.Usage=D3D11_USAGE_STAGING;bd.BindFlags=0;bd.CPUAccessFlags=D3D11_CPU_ACCESS_READ;hr(f.dev->CreateBuffer(&bd,nullptr,&read));
    double maxError=0;unsigned cases=0;
    for(unsigned flags:{0u,1u,2u,3u,128u,131u,384u,387u})for(float angle:{0.f,37.f,180.f,360.f,-725.f}){
        f.statics[18]=float(flags);f.statics[8]=.37f;f.statics[9]=1.6f;f.statics[10]=2.1f;f.statics[11]=-.8f;
        f.statics[0]=-23.7f;f.statics[1]=3.1f;f.statics[2]=11.85f;f.statics[4]=7.3f;f.statics[5]=43.1f;f.statics[6]=-21.55f;
        f.channels[0]=3.7f;f.channels[1]=-2.1f;f.channels[2]=1.3f;f.channels[3]=-.6f;f.channels[4]=angle;f.channels[5]=-1.2f;f.channels[6]=.7f;f.channels[12]=.3f;f.channels[13]=.8f;f.channels[14]=1.1f;f.channels[15]=-.3f;
        f.camera[0]=9.1f;f.camera[1]=2.7f;f.camera[2]=17.f;f.camera[3]=1.7f;f.ortho[0]=5.5f;f.ortho[1]=1.8f;f.ortho[2]=.6f;
        f.draw();auto* ctx=f.ctx.Get();ctx->IASetInputLayout(r->layout.Get());ctx->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_POINTLIST);
        ID3D11Buffer* vb[]={r->vertexBuffer.Get(),r->listBuffer.Get()};UINT strides[]={8,4},offsets[]={0,0};ctx->IASetVertexBuffers(0,2,vb,strides,offsets);
        ctx->VSSetShader(r->vs.Get(),nullptr,0);ID3D11Buffer* cb=r->frameBuffer.Get();ctx->VSSetConstantBuffers(0,1,&cb);ID3D11ShaderResourceView* views[]={r->channelView.Get(),r->staticView.Get()};ctx->VSSetShaderResources(0,2,views);
        ctx->GSSetShader(probe.Get(),nullptr,0);ID3D11Buffer* so=buffer.Get();UINT zero=0;ctx->SOSetTargets(1,&so,&zero);ctx->DrawInstanced(4,1,0,0);ctx->SOSetTargets(0,nullptr,nullptr);ctx->GSSetShader(nullptr,nullptr,0);
        ctx->CopyResource(read.Get(),buffer.Get());D3D11_MAPPED_SUBRESOURCE map{};hr(ctx->Map(read.Get(),0,D3D11_MAP_READ,0,&map));const float* values=static_cast<const float*>(map.pData);
        for(unsigned k=0;k<4;k++){auto ref=referencePoint(f,k);for(unsigned j=0;j<2;j++){double delta=std::abs(double(values[k*10+j])-ref[j]);maxError=std::max(maxError,delta);require(delta<1e-4,"VS world coordinates versus independent TRS");}require(values[k*10+7]==1,"SO vertex alpha");}
        ctx->Unmap(read.Get(),0);cases++;
    }
    std::printf("WARP actual VS stream-output: %u TRS/Camera/CameraAspect/lock/crop/negative-scale cases, max_world_error=%.9g\n",cases,maxError);
}
static void maskCheck(){
    Fixture f;f.image({0xffffffff},1,1);f.statics[0]=f.statics[5]=.5f;f.statics[2]=f.statics[6]=-.25f;f.statics[29]=.5f;f.draw();
    D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=32;td.MipLevels=td.ArraySize=1;td.Format=DXGI_FORMAT_D24_UNORM_S8_UINT;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_DEPTH_STENCIL;
    ComPtr<ID3D11Texture2D> depth;ComPtr<ID3D11DepthStencilView> dsv;hr(f.dev->CreateTexture2D(&td,nullptr,&depth));hr(f.dev->CreateDepthStencilView(depth.Get(),nullptr,&dsv));
    ID3D11RenderTargetView* target=f.rtv.Get();f.ctx->OMSetRenderTargets(1,&target,dsv.Get());
    float clear[4]={64.f/255,128.f/255,192.f/255,1};f.ctx->ClearRenderTargetView(target,clear);f.ctx->ClearDepthStencilView(dsv.Get(),D3D11_CLEAR_STENCIL,1,1);
    uint32_t list[]={0,1,0,1},segs[]={0,0,0,2,2,2};Run runs[]={{0,1,0,0,4},{1,1,0,0,16},{2,1,0,0,8},{3,1,0,0,32}};
    FrameIn frame{};frame.vp[0]=frame.vp[5]=frame.vp[10]=frame.vp[15]=1;frame.cam[3]=frame.ortho[0]=frame.ortho[1]=frame.ortho[2]=1;
    frame.listChanged=1;frame.list=list;frame.runs=runs;frame.segs=segs;frame.listCount=frame.runCount=4;frame.segCount=3;frame.clearStencil=1;
    require(sf_dr2_commit(f.id,&frame)==1,"mask commit");onEvent(f.id<<12);onEvent((f.id<<12)|1);
    auto changedPixels=[&](){f.ctx->CopyResource(f.readback.Get(),f.output.Get());D3D11_MAPPED_SUBRESOURCE map{};hr(f.ctx->Map(f.readback.Get(),0,D3D11_MAP_READ,0,&map));unsigned changed=0;for(unsigned y=0;y<32;y++)for(unsigned x=0;x<32;x++){auto p=reinterpret_cast<const uint32_t*>(static_cast<const char*>(map.pData)+y*map.RowPitch)[x];if(p!=0xffc08040){require(p==0xffffffff,"mask pixel color");changed++;}}f.ctx->Unmap(f.readback.Get(),0);return changed;};
    require(changedPixels()==64,"empty segment zero clears stencil; inside mask is 8x8");
    onEvent((f.id<<12)|2);require(changedPixels()==1024,"leave decrements stencil; outside mask covers remainder");require(sf_dr2_stat(f.id,4)==0,"mask native errors");
    std::puts("WARP stencil: empty background clears stale stencil; enter/inside/leave/outside preserve 64/1024 pixels");
}
int main(){try{blendCheck();uvCheck();regionAndSrgbCheck();vertexCheck();maskCheck();std::puts("M1b offline PASS (not in-game framecmp/performance)");return 0;}catch(const std::exception& e){std::printf("FAIL: %s\n",e.what());return 1;}catch(...){std::puts("FAIL: native exception");return 1;}}
