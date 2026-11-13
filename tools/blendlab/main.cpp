// Independent D3D11 correctness fixture; no game or installed-file access.
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <dxgi.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <cstdio>
#include <vector>
#include <stdexcept>
#include <cstdint>
#include <string>
using Microsoft::WRL::ComPtr;
static void ck(HRESULT h){if(FAILED(h))throw std::runtime_error("D3D failure "+std::to_string((unsigned)h));}
struct P {UINT mode,special,width,repeatTest;float alpha;UINT candidate,p0,p1;};
int wmain(int argc,wchar_t** argv){try{
 if(argc!=2)throw std::runtime_error("usage: blendlab absolute-lab.hlsl");
 ComPtr<ID3D11Device> dev;ComPtr<ID3D11DeviceContext> ctx;D3D_FEATURE_LEVEL fl;
 ck(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&dev,&fl,&ctx));
 ComPtr<IDXGIDevice> dx;ck(dev.As(&dx));ComPtr<IDXGIAdapter> adapter;ck(dx->GetAdapter(&adapter));DXGI_ADAPTER_DESC ad{};ck(adapter->GetDesc(&ad));
 char name[256]{};WideCharToMultiByte(CP_UTF8,0,ad.Description,-1,name,256,nullptr,nullptr);
 printf("adapter=%s vendor=%u device=%u feature=%x RT=RGBA8_UNORM linear MSAA=1\n",name,ad.VendorId,ad.DeviceId,fl);fflush(stdout);
 auto compile=[&](const char* entry,const char* profile){ComPtr<ID3DBlob>b,e;HRESULT h=D3DCompileFromFile(argv[1],nullptr,nullptr,entry,profile,D3DCOMPILE_ENABLE_STRICTNESS,0,&b,&e);if(e)fprintf(stderr,"%s",(char*)e->GetBufferPointer());ck(h);return b;};
 auto vb=compile("VS","vs_5_0"),ib=compile("Init","ps_5_0"),pb=compile("PS","ps_5_0"),cb=compile("CS","cs_5_0");
 ComPtr<ID3D11VertexShader>vs;ComPtr<ID3D11PixelShader>init,ps;ComPtr<ID3D11ComputeShader>cs;
 ck(dev->CreateVertexShader(vb->GetBufferPointer(),vb->GetBufferSize(),nullptr,&vs));ck(dev->CreatePixelShader(ib->GetBufferPointer(),ib->GetBufferSize(),nullptr,&init));ck(dev->CreatePixelShader(pb->GetBufferPointer(),pb->GetBufferSize(),nullptr,&ps));ck(dev->CreateComputeShader(cb->GetBufferPointer(),cb->GetBufferSize(),nullptr,&cs));
 D3D11_BUFFER_DESC bd{};bd.ByteWidth=sizeof(P);bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;ComPtr<ID3D11Buffer>params;ck(dev->CreateBuffer(&bd,nullptr,&params));
 bd.ByteWidth=24;bd.BindFlags=D3D11_BIND_UNORDERED_ACCESS;bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;bd.StructureByteStride=4;ComPtr<ID3D11Buffer>stats,readStats;ck(dev->CreateBuffer(&bd,nullptr,&stats));
 D3D11_UNORDERED_ACCESS_VIEW_DESC ud{};ud.Format=DXGI_FORMAT_UNKNOWN;ud.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;ud.Buffer.NumElements=6;ComPtr<ID3D11UnorderedAccessView>uav;ck(dev->CreateUnorderedAccessView(stats.Get(),&ud,&uav));
 bd.Usage=D3D11_USAGE_STAGING;bd.BindFlags=0;bd.CPUAccessFlags=D3D11_CPU_ACCESS_READ;bd.MiscFlags=0;ck(dev->CreateBuffer(&bd,nullptr,&readStats));
 auto pc=params.Get();ctx->VSSetShader(vs.Get(),nullptr,0);ctx->PSSetConstantBuffers(0,1,&pc);ctx->CSSetConstantBuffers(0,1,&pc);ctx->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
 D3D11_RASTERIZER_DESC rd{};rd.FillMode=D3D11_FILL_SOLID;rd.CullMode=D3D11_CULL_NONE;rd.DepthClipEnable=TRUE;ComPtr<ID3D11RasterizerState>rs;ck(dev->CreateRasterizerState(&rd,&rs));ctx->RSSetState(rs.Get());
 for(UINT mode=0;mode<5;mode++)for(UINT special=0;special<4;special++){
  P p{mode,special,special?256u:4096u,0,special==1?5.1f/255:special==2?0.1234567f:0.5001f,0,0,0};
  D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=p.width;td.MipLevels=td.ArraySize=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
  ComPtr<ID3D11Texture2D>tex,stage;ck(dev->CreateTexture2D(&td,nullptr,&tex));ComPtr<ID3D11RenderTargetView>rt;ComPtr<ID3D11ShaderResourceView>srv;ck(dev->CreateRenderTargetView(tex.Get(),nullptr,&rt));ck(dev->CreateShaderResourceView(tex.Get(),nullptr,&srv));
  D3D11_BLEND_DESC blend{};auto& b=blend.RenderTarget[0];b.BlendEnable=TRUE;b.SrcBlend=(mode==1||mode==2)?D3D11_BLEND_SRC_ALPHA:D3D11_BLEND_ONE;b.DestBlend=mode==2||mode==4?D3D11_BLEND_ONE:D3D11_BLEND_INV_SRC_ALPHA;b.BlendOp=b.BlendOpAlpha=D3D11_BLEND_OP_ADD;b.SrcBlendAlpha=b.SrcBlend;b.DestBlendAlpha=b.DestBlend;b.RenderTargetWriteMask=mode==2?7:15;ComPtr<ID3D11BlendState>bs;ck(dev->CreateBlendState(&blend,&bs));
  auto render=[&](bool reset){ID3D11ShaderResourceView*nil=nullptr;ctx->CSSetShaderResources(0,1,&nil);auto r=rt.Get();ctx->OMSetRenderTargets(1,&r,nullptr);ctx->UpdateSubresource(params.Get(),0,nullptr,&p,0,0);D3D11_VIEWPORT vp{0,0,(float)p.width,(float)p.width,0,1};ctx->RSSetViewports(1,&vp);if(reset){ctx->OMSetBlendState(nullptr,nullptr,~0u);ctx->PSSetShader(init.Get(),nullptr,0);ctx->Draw(3,0);}ctx->OMSetBlendState(bs.Get(),nullptr,~0u);ctx->PSSetShader(ps.Get(),nullptr,0);ctx->Draw(3,0);ctx->OMSetRenderTargets(0,nullptr,nullptr);};
  render(true);
  for(UINT candidate=0;candidate<10;candidate++){
   p.candidate=candidate;ctx->UpdateSubresource(params.Get(),0,nullptr,&p,0,0);UINT zero[]={0,~0u,0,0,~0u,0};ctx->UpdateSubresource(stats.Get(),0,nullptr,zero,0,0);auto s=srv.Get();ctx->CSSetShaderResources(0,1,&s);auto u=uav.Get();ctx->CSSetUnorderedAccessViews(0,1,&u,nullptr);ctx->CSSetShader(cs.Get(),nullptr,0);ctx->Dispatch(p.width/16,p.width/16,1);ID3D11UnorderedAccessView*nu=nullptr;ctx->CSSetUnorderedAccessViews(0,1,&nu,nullptr);ctx->CopyResource(readStats.Get(),stats.Get());D3D11_MAPPED_SUBRESOURCE map{};ck(ctx->Map(readStats.Get(),0,D3D11_MAP_READ,0,&map));auto v=(UINT*)map.pData;printf("formula mode=%u special=%u alpha=%.9g cases=%u candidate=%u rgb_bad=%u rgb_first=%u rgb_max=%u alpha_bad=%u alpha_first=%u alpha_max=%u\n",mode,special,p.alpha,p.width*p.width,candidate,v[0],v[1],v[2],v[3],v[4],v[5]);ctx->Unmap(readStats.Get(),0);fflush(stdout);
  }
  if(special){
   td.Usage=D3D11_USAGE_STAGING;td.BindFlags=0;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;ck(dev->CreateTexture2D(&td,nullptr,&stage));
   auto read=[&](){std::vector<uint8_t>v(65536*4);ctx->CopyResource(stage.Get(),tex.Get());D3D11_MAPPED_SUBRESOURCE m{};ck(ctx->Map(stage.Get(),0,D3D11_MAP_READ,0,&m));for(UINT y=0;y<256;y++)memcpy(v.data()+y*1024,(char*)m.pData+y*m.RowPitch,1024);ctx->Unmap(stage.Get(),0);return v;};
   auto lut=read();std::vector<uint8_t>acc(65536*2);for(UINT n=0;n<65536;n++){acc[n*2]=lut[n*4];acc[n*2+1]=lut[n*4+3];}
   uint64_t bad=0;for(UINT k=2;k<=255;k++){render(false);auto got=read();for(UINT n=0;n<65536;n++)for(UINT ch=0;ch<2;ch++){auto& a=acc[n*2+ch];a=lut[((n&0xff00)+a)*4+(ch?3:0)];bad+=a!=got[n*4+(ch?3:0)];}}
   printf("repeat mode=%u special=%u k=1..255 cases=%u rgb_alpha_mismatches=%llu source=measured_ROP_transition_LUT\n",mode,special,65536u*255u,(unsigned long long)bad);
   if(bad)throw std::runtime_error("empirical transition LUT failed repeated ROP validation");
   for(UINT c:{0u,128u,255u}){printf("fixed mode=%u special=%u color=%u dst=",mode,special,c);for(UINT d=0;d<256;d++)if(lut[(c*256+d)*4]==d)printf("%u,",d);printf("\n");}fflush(stdout);
  }
 }
 return 0;
 }catch(const std::exception&e){fprintf(stderr,"%s\n",e.what());return 1;}}

