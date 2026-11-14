// Explicit M0 research lease. No discovery, Unity API, swapchain or Present hook.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <wrl/client.h>
#include <algorithm>
#include <memory>
#include <stdexcept>
#include <cstring>
#include <cmath>
#include <atomic>
#include "direct_renderer.h"
#include "direct_shader_bytes.h"
namespace {
using Microsoft::WRL::ComPtr;
void check(HRESULT hr){if(FAILED(hr))throw std::runtime_error("direct D3D11 failure");}
DXGI_FORMAT viewFormat(DXGI_FORMAT f,bool srgb){switch(f){
    case DXGI_FORMAT_R8G8B8A8_TYPELESS:return srgb?DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:DXGI_FORMAT_R8G8B8A8_UNORM;
    case DXGI_FORMAT_B8G8R8A8_TYPELESS:return srgb?DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:DXGI_FORMAT_B8G8R8A8_UNORM;
    case DXGI_FORMAT_B8G8R8X8_TYPELESS:return srgb?DXGI_FORMAT_B8G8R8X8_UNORM_SRGB:DXGI_FORMAT_B8G8R8X8_UNORM;
    case DXGI_FORMAT_R16G16B16A16_TYPELESS:return DXGI_FORMAT_R16G16B16A16_FLOAT;
    case DXGI_FORMAT_R32G32B32A32_TYPELESS:return DXGI_FORMAT_R32G32B32A32_FLOAT;
    case DXGI_FORMAT_R16G16_TYPELESS:return DXGI_FORMAT_R16G16_FLOAT;
    case DXGI_FORMAT_R32_TYPELESS:return DXGI_FORMAT_R32_FLOAT;
    default:return f;
}}
struct Metric {
    std::array<double,4096> samples{};uint64_t count=0;double sum=0;
    void add(double v){samples[count++%samples.size()]=v;sum+=v;}
    double get(bool percentile)const {if(!count)return 0;if(!percentile)return sum/double(count);auto copy=samples;size_t n=(std::min)(size_t(count),samples.size()),p=(n*95+99)/100-1;std::nth_element(copy.begin(),copy.begin()+p,copy.begin()+n);return copy[p];}
};
struct Renderer {
    dr::Tracks tracks; std::vector<dr::Instance> staging;dr::Camera camera{};ComPtr<ID3D11Texture2D> pendingTarget,target;
    ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context,deferred;
    ComPtr<ID3D11ShaderResourceView> textures,instancesView;ComPtr<ID3D11RenderTargetView> rtv;
    ComPtr<ID3D11Buffer> instances,constant;ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11BlendState> blend;ComPtr<ID3D11DepthStencilState> depth;ComPtr<ID3D11RasterizerState> raster;ComPtr<ID3D11SamplerState> sampler;
    D3D11_TEXTURE2D_DESC targetDesc{};
    struct Job {double time=0;dr::Camera camera{};ComPtr<ID3D11Texture2D> target;};
    std::mutex queueMutex;std::array<Job,8> queue{};uint64_t read=0,write=0;
    Metric metrics[6];uint64_t frames=0,drawn=0,draws=0,errors=0;std::atomic<uint64_t> skipped{0};bool pending=false;int token;
    Renderer(int id,unsigned n,unsigned m,unsigned anim,unsigned threads,ID3D11Texture2D* array):tracks(n,m,anim,threads),staging(n),token(id){
        array->GetDevice(&device);device->GetImmediateContext(&context);check(device->CreateDeferredContext(0,&deferred));
        D3D11_TEXTURE2D_DESC ad{};array->GetDesc(&ad);if(ad.ArraySize!=m||ad.SampleDesc.Count!=1)throw std::runtime_error("direct texture array mismatch");
        D3D11_SHADER_RESOURCE_VIEW_DESC arrayView{};arrayView.Format=viewFormat(ad.Format,true);arrayView.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;arrayView.Texture2DArray.MipLevels=ad.MipLevels;arrayView.Texture2DArray.ArraySize=m;
        check(device->CreateShaderResourceView(array,&arrayView,&textures));
        check(device->CreateVertexShader(dr_vs,sizeof(dr_vs),nullptr,&vs));check(device->CreatePixelShader(dr_ps,sizeof(dr_ps),nullptr,&ps));
        D3D11_BUFFER_DESC b{};b.ByteWidth=n*sizeof(dr::Instance);b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_SHADER_RESOURCE;b.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;b.StructureByteStride=sizeof(dr::Instance);
        check(device->CreateBuffer(&b,nullptr,&instances));D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=DXGI_FORMAT_UNKNOWN;sv.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;sv.Buffer.NumElements=n;check(device->CreateShaderResourceView(instances.Get(),&sv,&instancesView));
        b={};b.ByteWidth=sizeof(dr::Camera);b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&b,nullptr,&constant));
        D3D11_BLEND_DESC bd{};auto& rt=bd.RenderTarget[0];rt.BlendEnable=TRUE;rt.SrcBlend=rt.SrcBlendAlpha=D3D11_BLEND_ONE;rt.DestBlend=rt.DestBlendAlpha=D3D11_BLEND_INV_SRC_ALPHA;rt.BlendOp=rt.BlendOpAlpha=D3D11_BLEND_OP_ADD;rt.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;check(device->CreateBlendState(&bd,&blend));
        D3D11_DEPTH_STENCIL_DESC dd{};check(device->CreateDepthStencilState(&dd,&depth));D3D11_RASTERIZER_DESC rd{};rd.FillMode=D3D11_FILL_SOLID;rd.CullMode=D3D11_CULL_NONE;rd.DepthClipEnable=TRUE;check(device->CreateRasterizerState(&rd,&raster));
        D3D11_SAMPLER_DESC sd{};sd.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR;sd.AddressU=sd.AddressV=sd.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;sd.MaxLOD=D3D11_FLOAT32_MAX;check(device->CreateSamplerState(&sd,&sampler));
    }
    ~Renderer(){if(pending)tracks.wait();}
    bool submit(double t,const dr::Camera& c,ID3D11Texture2D* output){
        double begin=dr::clockUs();std::lock_guard<std::mutex> lock(queueMutex);
        if(write-read==queue.size()){++skipped;return false;}
        auto& job=queue[write++%queue.size()];job.time=t;job.camera=c;job.target=output;
        if(!pending){tracks.start(t,c);pending=true;}metrics[0].add(dr::clockUs()-begin);return true;
    }
    void render(){
        double begin=dr::clockUs();
        {std::lock_guard<std::mutex> lock(queueMutex);if(!pending)return;auto& job=queue[read%queue.size()];camera=job.camera;pendingTarget=job.target;}
        double waitBegin=dr::clockUs();tracks.wait();double waited=dr::clockUs()-waitBegin;
        try{
            D3D11_TEXTURE2D_DESC td{};pendingTarget->GetDesc(&td);
            if(!rtv||target!=pendingTarget||td.Width!=targetDesc.Width||td.Height!=targetDesc.Height||td.Format!=targetDesc.Format){
                rtv.Reset();target=pendingTarget;targetDesc=td;ComPtr<ID3D11Device> owner;target->GetDevice(&owner);if(owner!=device)throw std::runtime_error("direct target device mismatch");
                D3D11_RENDER_TARGET_VIEW_DESC rv{};rv.Format=viewFormat(td.Format,camera.position[3]!=0);rv.ViewDimension=td.SampleDesc.Count>1?D3D11_RTV_DIMENSION_TEXTURE2DMS:D3D11_RTV_DIMENSION_TEXTURE2D;
                check(device->CreateRenderTargetView(target.Get(),&rv,&rtv));
            }
            unsigned count=0;
            for(auto& w:tracks.workers){if(!w.visible.empty())std::memcpy(staging.data()+count,w.visible.data(),w.visible.size()*sizeof(dr::Instance));count+=unsigned(w.visible.size());}
            if(count){D3D11_BOX box{0,0,0,count*unsigned(sizeof(dr::Instance)),1,1};deferred->UpdateSubresource(instances.Get(),0,&box,staging.data(),0,0);}
            deferred->UpdateSubresource(constant.Get(),0,nullptr,&camera,0,0);
            auto* output=rtv.Get();deferred->OMSetRenderTargets(1,&output,nullptr);deferred->OMSetBlendState(blend.Get(),nullptr,~0u);deferred->OMSetDepthStencilState(depth.Get(),0);
            deferred->RSSetState(raster.Get());D3D11_VIEWPORT vp{0,0,float(td.Width),float(td.Height),0,1};deferred->RSSetViewports(1,&vp);
            deferred->IASetInputLayout(nullptr);deferred->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            deferred->VSSetShader(vs.Get(),nullptr,0);deferred->PSSetShader(ps.Get(),nullptr,0);deferred->GSSetShader(nullptr,nullptr,0);deferred->HSSetShader(nullptr,nullptr,0);deferred->DSSetShader(nullptr,nullptr,0);
            auto* cb=constant.Get();auto* iv=instancesView.Get();auto* tex=textures.Get();auto* sm=sampler.Get();deferred->VSSetConstantBuffers(0,1,&cb);deferred->VSSetShaderResources(0,1,&iv);deferred->PSSetShaderResources(0,1,&tex);deferred->PSSetSamplers(0,1,&sm);
            if(count)deferred->DrawInstanced(6,count,0,0);
            ComPtr<ID3D11CommandList> commands;check(deferred->FinishCommandList(FALSE,&commands));context->ExecuteCommandList(commands.Get(),TRUE); // Restores the complete Unity immediate-context state.
            ++frames;drawn=count;draws=count?1:0;
        }catch(...){++errors;deferred->ClearState();ComPtr<ID3D11CommandList> discard;deferred->FinishCommandList(FALSE,&discard);}
        metrics[1].add(tracks.sumUs);metrics[2].add(tracks.wallUs);pendingTarget.Reset();
        {std::lock_guard<std::mutex> lock(queueMutex);queue[read++%queue.size()].target.Reset();pending=read!=write;if(pending){auto& job=queue[read%queue.size()];tracks.start(job.time,job.camera);}}
        double elapsed=dr::clockUs()-begin;metrics[3].add(elapsed);metrics[4].add(elapsed-waited);metrics[5].add(waited);
    }
};
std::mutex gate;std::shared_ptr<Renderer> renderer;int nextToken=0;
void __stdcall event(int token) noexcept {try{std::lock_guard<std::mutex> lock(gate);auto r=std::atomic_load(&renderer);if(r&&r->token==token)r->render();}catch(...){}}
}
DR_API int sf_dr_create(unsigned n,unsigned m,unsigned anim,unsigned threads,void* texture) noexcept {try{
    if(!texture||n==0||n>100000||m==0||m>2048||anim>100||threads==0||threads>64)return 0;
    std::lock_guard<std::mutex> lock(gate);std::atomic_store(&renderer,std::shared_ptr<Renderer>{});std::atomic_store(&renderer,std::make_shared<Renderer>(++nextToken,n,m,anim,threads,static_cast<ID3D11Texture2D*>(texture)));return nextToken;
}catch(...){return 0;}}
DR_API int sf_dr_submit(int token,double t,const dr::Camera* camera,void* target) noexcept {try{auto r=std::atomic_load(&renderer);if(!r||r->token!=token||!camera||!target||!std::isfinite(t))return 0;return r->submit(t,*camera,static_cast<ID3D11Texture2D*>(target))?1:0;}catch(...){return 0;}}
DR_API void sf_dr_destroy(int token) noexcept {try{std::lock_guard<std::mutex> lock(gate);auto r=std::atomic_load(&renderer);if(r&&r->token==token)std::atomic_store(&renderer,std::shared_ptr<Renderer>{});}catch(...){}}
DR_API void* sf_dr_event() noexcept {return reinterpret_cast<void*>(&event);}
DR_API double sf_dr_stat(int token,int i) noexcept {try{std::lock_guard<std::mutex> lock(gate);auto owned=std::atomic_load(&renderer);if(!owned||owned->token!=token)return -1;auto& r=*owned;std::lock_guard<std::mutex> queueLock(r.queueMutex);switch(i){case 0:return double(r.frames);case 1:return double(r.drawn);case 2:return double(r.draws);case 3:return double(r.tracks.activeCount);case 4:return double(r.skipped.load());case 5:return double(r.errors);default:return i>=6&&i<18?r.metrics[(i-6)/2].get((i&1)!=0):-1;}}catch(...){return -1;}}
