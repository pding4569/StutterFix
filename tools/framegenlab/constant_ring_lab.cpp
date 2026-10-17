// StutterFix original code, MIT. Independent GPU fixture, no game process.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <d3d11sdklayers.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <cstdio>
#include <stdexcept>
#include <cstring>
#include <cstdlib>
#include "../../native/sfnative/constant_ring.h"
using Microsoft::WRL::ComPtr;
static void check(HRESULT hr) {if(FAILED(hr))throw std::runtime_error("D3D fixture call failed");}
static void require(bool value,const char* why) {if(!value)throw std::runtime_error(why);}
int main() {
    try {
        ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context;D3D_FEATURE_LEVEL level;
        HRESULT hr=D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_DEBUG,nullptr,0,D3D11_SDK_VERSION,&device,&level,&context);
        bool debug=SUCCEEDED(hr);
        if(!debug) check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&device,&level,&context));
        sf_buffers::ConstantRing ring;
        require(!ring.initialize(device.Get(),17),"unaligned size accepted");
        if(!ring.initialize(device.Get(),16)) {printf("constant_ring_supported=0 pixel_cases=0 debug=%d\n",debug?1:0);return 2;}
        const char* source="cbuffer C:register(b0){float4 color;} struct V{float4 p:SV_POSITION;};V VS(uint i:SV_VertexID){V v;v.p=float4(i==1?3:-1,i==2?3:-1,0,1);return v;}float4 PS(V v):SV_TARGET{return color;}";
        ComPtr<ID3DBlob> vsCode,psCode,error;check(D3DCompile(source,strlen(source),nullptr,nullptr,nullptr,"VS","vs_5_0",0,0,&vsCode,&error));check(D3DCompile(source,strlen(source),nullptr,nullptr,nullptr,"PS","ps_5_0",0,0,&psCode,&error));
        ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
        check(device->CreateVertexShader(vsCode->GetBufferPointer(),vsCode->GetBufferSize(),nullptr,&vs));check(device->CreatePixelShader(psCode->GetBufferPointer(),psCode->GetBufferSize(),nullptr,&ps));
        D3D11_BUFFER_DESC bd{};bd.ByteWidth=16;bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;ComPtr<ID3D11Buffer> original;check(device->CreateBuffer(&bd,nullptr,&original));
        D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=32;td.MipLevels=td.ArraySize=1;td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R8G8B8A8_UNORM;td.BindFlags=D3D11_BIND_RENDER_TARGET;
        ComPtr<ID3D11Texture2D> output,read;ComPtr<ID3D11RenderTargetView> target;check(device->CreateTexture2D(&td,nullptr,&output));check(device->CreateRenderTargetView(output.Get(),nullptr,&target));
        td.Usage=D3D11_USAGE_STAGING;td.BindFlags=0;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;check(device->CreateTexture2D(&td,nullptr,&read));
        context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        D3D11_RASTERIZER_DESC rasterDesc{};rasterDesc.FillMode=D3D11_FILL_SOLID;rasterDesc.CullMode=D3D11_CULL_NONE;rasterDesc.DepthClipEnable=TRUE;
        ComPtr<ID3D11RasterizerState> raster;check(device->CreateRasterizerState(&rasterDesc,&raster));context->RSSetState(raster.Get());
        D3D11_VIEWPORT viewport{0,0,32,32,0,1};context->RSSetViewports(1,&viewport);auto rt=target.Get();context->OMSetRenderTargets(1,&rt,nullptr);
        unsigned char baseline[32*32*4];unsigned cases=0,different=0;
        auto picture=[&](unsigned char* dst) {context->CopyResource(read.Get(),output.Get());D3D11_MAPPED_SUBRESOURCE map{};check(context->Map(read.Get(),0,D3D11_MAP_READ,0,&map));for(unsigned y=0;y<32;y++)memcpy(dst+y*128,static_cast<const unsigned char*>(map.pData)+y*map.RowPitch,128);context->Unmap(read.Get(),0);};
        for(unsigned i=0;i<60;i++) {
            float data[4]={float((i*17)%255)/255,float((i*31)%255)/255,float((i*47)%255)/255,1};
            context->UpdateSubresource(original.Get(),0,nullptr,data,0,0);auto old=original.Get();context->PSSetConstantBuffers(0,1,&old);context->Draw(3,0);picture(baseline);
            for(unsigned p=0;p<32*32;p++) {
                for(unsigned channel=0;channel<3;channel++)require(std::abs(int(baseline[p*4+channel])-int(data[channel]*255+.5f))<=1,"baseline draw positive control failed");
                require(baseline[p*4+3]==255,"baseline alpha positive control failed");
            }
            auto cb=ring.write(context.Get(),data,sizeof(data));require(cb!=nullptr,"retired fixture slot unavailable after readback");
            context->PSSetConstantBuffers(0,1,&cb);context->Draw(3,0);ring.submitted(context.Get());unsigned char actual[sizeof(baseline)];picture(actual);
            for(unsigned p=0;p<32*32;p++)different+=memcmp(actual+p*4,baseline+p*4,4)!=0;
            ++cases;
        }
        // Three unsubmitted reservations cannot be overwritten, even if the GPU
        // is idle. This is deliberately not presented as a GPU-busy stress test.
        float data[4]{};auto a=ring.write(context.Get(),data,16),b=ring.write(context.Get(),data,16),c=ring.write(context.Get(),data,16);
        require(a&&b&&c&&a!=b&&a!=c&&b!=c,"distinct three reservations");require(ring.write(context.Get(),data,16)==nullptr,"reserved slot overwritten");ring.abandoned();
        ComPtr<ID3D11DeviceContext> deferred;check(device->CreateDeferredContext(0,&deferred));require(ring.write(deferred.Get(),data,16)==nullptr,"deferred first-map restriction not guarded");
        int invalid=-1;ComPtr<ID3D11InfoQueue> messages;
        if(debug && SUCCEEDED(device.As(&messages))) {invalid=0;for(UINT64 i=0;i<messages->GetNumStoredMessages();i++){SIZE_T size=0;messages->GetMessage(i,nullptr,&size);auto bytes=new unsigned char[size];auto m=reinterpret_cast<D3D11_MESSAGE*>(bytes);if(SUCCEEDED(messages->GetMessage(i,m,&size))&&(m->Severity==D3D11_MESSAGE_SEVERITY_ERROR||m->Severity==D3D11_MESSAGE_SEVERITY_CORRUPTION))++invalid;delete[] bytes;}}
        printf("constant_ring_supported=1 pixel_cases=%u compared_pixels=%u mismatched_pixels=%u writes=%u fallbacks=%u reserved_overwrite=0 deferred_map=0 debug=%d debug_errors=%d\n",cases,cases*1024,different,ring.writes.load(),ring.fallbacks.load(),debug?1:0,invalid);
        require(different==0&&invalid<=0,"pixel/API fixture mismatch");return 0;
    } catch(const std::exception& e) {fprintf(stderr,"%s\n",e.what());return 1;}
}
