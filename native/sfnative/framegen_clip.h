// Research visual diagnostics only. Decimate on GPU, then poll a tiny staging image.
// File I/O happens after the worker stops. Never used by a performance run.
class MotionClip {
    struct Read {
        std::array<Texture,3> reduced;
        std::array<ComPtr<ID3D11RenderTargetView>,3> targets;
        std::array<ComPtr<ID3D11Texture2D>,3> cpu;
        ComPtr<ID3D11Query> ready;
        int index=-1,mode=0,real=0,sourceFrame=0; double sourceSong=0;
    };
    struct Frame { int index,mode,real,sourceFrame; double sourceSong; std::array<std::vector<unsigned char>,3> rgb; };
    std::array<Read,3> reads;
    std::vector<Frame> frames;
    Texture shown;
    ComPtr<ID3D11DeviceContext> deferred;
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11Buffer> constants;
    unsigned width=0,height=0,fullHeight=0;
public:
    unsigned skipped=0;
    void initialize(ID3D11Device* device,unsigned w,unsigned h) {
        if(vs) return;
        width=w/6; height=h/6; fullHeight=h;
        const char* code=R"(
cbuffer Info:register(b0) { uint4 info; };
Texture2D shown:register(t0); Texture2D screen:register(t1); Texture2D world:register(t2); Texture2D mask:register(t3); Texture2D nextScreen:register(t4); Texture2D nextWorld:register(t5); Texture2D nextMask:register(t6);
float4 VS(uint id:SV_VertexID):SV_POSITION { float2 uv=float2((id<<1)&2,id&2); return float4(uv*float2(2,-2)+float2(-1,1),0,1); }
float3 referenceColor(Texture2D s,Texture2D w,Texture2D m,int2 p,uint masked) {
    float3 ui=s.Load(int3(p,0)).rgb;
    if(masked!=0) { int2 q=p; if(info.z!=0) q.y=info.x-1-p.y;
        float3 oldWorld=w.Load(int3(q,0)).rgb;
        if(max(max(abs(ui.r-oldWorld.r),abs(ui.g-oldWorld.g)),abs(ui.b-oldWorld.b))<=2.5/255.) return oldWorld*m.Load(int3(q,0)).rgb;
    }
    return ui;
}
struct Pair { float4 actual:SV_TARGET0; float4 reference:SV_TARGET1; float4 next:SV_TARGET2; };
Pair PS(float4 position:SV_POSITION) {
    int2 p=int2(position.xy)*6; Pair r; r.actual=float4(shown.Load(int3(p,0)).rgb,1);
    r.reference=float4(referenceColor(screen,world,mask,p,info.y),1);
    r.next=float4(referenceColor(nextScreen,nextWorld,nextMask,p,info.w),1);
    return r;
})";
        ComPtr<ID3DBlob> v,p,e;
        check(D3DCompile(code,strlen(code),nullptr,nullptr,nullptr,"VS","vs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&v,&e));
        HRESULT hr=D3DCompile(code,strlen(code),nullptr,nullptr,nullptr,"PS","ps_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&p,&e);
        if(FAILED(hr)) throw std::runtime_error(e?static_cast<const char*>(e->GetBufferPointer()):"clip shader failure");
        check(device->CreateVertexShader(v->GetBufferPointer(),v->GetBufferSize(),nullptr,&vs)); check(device->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&ps));
        check(device->CreateDeferredContext(0,&deferred));
        D3D11_BUFFER_DESC bd{}; bd.ByteWidth=16; bd.Usage=D3D11_USAGE_DEFAULT; bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER; check(device->CreateBuffer(&bd,nullptr,&constants));
        for(auto& r:reads) {
            for(int i=0;i<3;i++) {
                D3D11_TEXTURE2D_DESC td{}; td.Width=width; td.Height=height; td.MipLevels=td.ArraySize=1; td.Format=DXGI_FORMAT_R8G8B8A8_UNORM; td.SampleDesc.Count=1; td.BindFlags=D3D11_BIND_RENDER_TARGET;
                check(device->CreateTexture2D(&td,nullptr,&r.reduced[i].texture)); check(device->CreateRenderTargetView(r.reduced[i].texture.Get(),nullptr,&r.targets[i]));
                td.Usage=D3D11_USAGE_STAGING; td.BindFlags=0; td.CPUAccessFlags=D3D11_CPU_ACCESS_READ; check(device->CreateTexture2D(&td,nullptr,&r.cpu[i]));
            }
            D3D11_QUERY_DESC q{D3D11_QUERY_EVENT,0}; check(device->CreateQuery(&q,&r.ready));
        }
        frames.reserve(256);
    }
    void collect(ID3D11DeviceContext* immediate) {
        for(auto& r:reads) {
            if(r.index<0) continue;
            HRESULT hr=immediate->GetData(r.ready.Get(),nullptr,0,D3D11_ASYNC_GETDATA_DONOTFLUSH);
            if(hr==S_FALSE) continue; check(hr);
            D3D11_MAPPED_SUBRESOURCE maps[3]{}; int mapped=0;
            for(;mapped<3;mapped++) {
                hr=immediate->Map(r.cpu[mapped].Get(),0,D3D11_MAP_READ,D3D11_MAP_FLAG_DO_NOT_WAIT,&maps[mapped]);
                if(hr!=S_OK) break;
            }
            if(mapped!=3) { for(int i=0;i<mapped;i++) immediate->Unmap(r.cpu[i].Get(),0); if(hr==DXGI_ERROR_WAS_STILL_DRAWING) continue; check(hr); }
            Frame f{r.index,r.mode,r.real,r.sourceFrame,r.sourceSong,{}};
            for(int image=0;image<3;image++) {
                auto& pixels=f.rgb[image]; pixels.resize(width*height*3);
                for(unsigned y=0;y<height;y++) {
                    auto line=static_cast<const unsigned char*>(maps[image].pData)+y*maps[image].RowPitch;
                    for(unsigned x=0;x<width;x++) memcpy(pixels.data()+(y*width+x)*3,line+x*4,3);
                }
                immediate->Unmap(r.cpu[image].Get(),0);
            }
            frames.push_back(std::move(f)); r.index=-1;
        }
    }
    bool submit(ID3D11Device* device,ID3D11DeviceContext* immediate,ID3D11Texture2D* texture,const Slot* source,const Slot* next,int index,int mode,bool real,int frame,double song) {
        collect(immediate); Read* r=nullptr;
        for(auto& candidate:reads) if(candidate.index<0) {r=&candidate;break;}
        if(!r) {++skipped;return false;}
        if(!shown.texture) {
            D3D11_TEXTURE2D_DESC td{}; texture->GetDesc(&td); td.BindFlags=D3D11_BIND_SHADER_RESOURCE; td.MiscFlags=0; td.Format=raw(td.Format);
            check(device->CreateTexture2D(&td,nullptr,&shown.texture)); check(device->CreateShaderResourceView(shown.texture.Get(),nullptr,&shown.view));
        }
        immediate->CopyResource(shown.texture.Get(),texture);
        deferred->ClearState(); UINT data[4]={fullHeight,source && screenBorderEnabled(source->packet)?1u:0u,source?UINT(source->packet.flip):0u,next && screenBorderEnabled(next->packet)?1u:0u};
        deferred->UpdateSubresource(constants.Get(),0,nullptr,data,0,0); auto cb=constants.Get(); deferred->PSSetConstantBuffers(0,1,&cb);
        ID3D11ShaderResourceView* views[]={shown.view.Get(),source?source->images[1].view.Get():shown.view.Get(),source?source->images[0].view.Get():nullptr,source && screenBorderEnabled(source->packet)?source->images[2].view.Get():nullptr,next?next->images[1].view.Get():shown.view.Get(),next?next->images[0].view.Get():nullptr,next && screenBorderEnabled(next->packet)?next->images[2].view.Get():nullptr};
        deferred->PSSetShaderResources(0,7,views); ID3D11RenderTargetView* targets[]={r->targets[0].Get(),r->targets[1].Get(),r->targets[2].Get()}; deferred->OMSetRenderTargets(3,targets,nullptr);
        D3D11_VIEWPORT vp{0,0,float(width),float(height),0,1}; deferred->RSSetViewports(1,&vp);
        // Explicit rasterizer/depth/blend defaults are part of ClearState on our deferred context.
        deferred->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST); deferred->VSSetShader(vs.Get(),nullptr,0); deferred->PSSetShader(ps.Get(),nullptr,0); deferred->Draw(3,0);
        deferred->OMSetRenderTargets(0,nullptr,nullptr);
        for(int i=0;i<3;i++) deferred->CopyResource(r->cpu[i].Get(),r->reduced[i].texture.Get());
        deferred->End(r->ready.Get()); ComPtr<ID3D11CommandList> list; check(deferred->FinishCommandList(FALSE,&list)); immediate->ExecuteCommandList(list.Get(),TRUE);
        r->index=index; r->mode=mode; r->real=real?1:0; r->sourceFrame=source?source->packet.frame:frame; r->sourceSong=source?source->packet.song:song; return true;
    }
    void save(ID3D11DeviceContext* immediate,const std::wstring& path) {
        if(!vs) return;
        immediate->Flush(); double end=now()+2;
        do { collect(immediate); bool pending=false; for(auto& r:reads) pending|=r.index>=0; if(!pending) break; Sleep(1); } while(now()<end);
        std::sort(frames.begin(),frames.end(),[](const Frame& a,const Frame& b){return a.index<b.index;});
        FILE* csv=nullptr; _wfopen_s(&csv,(path+L"/clip-reference.csv").c_str(),L"wb"); if(csv) fprintf(csv,"index,source_frame,source_song_s\n");
        for(auto& f:frames) {
            if(csv) fprintf(csv,"%d,%d,%.9f\n",f.index,f.sourceFrame,f.sourceSong);
            for(int i=0;i<3;i++) {
                wchar_t name[100]; if(i==0) swprintf_s(name,L"/clip-%dx-%03d-%s.ppm",f.mode,f.index,f.real?L"real":L"generated"); else if(i==1) swprintf_s(name,L"/reference-%dx-%03d.ppm",f.mode,f.index); else swprintf_s(name,L"/next-reference-%dx-%03d.ppm",f.mode,f.index);
                FILE* out=nullptr; _wfopen_s(&out,(path+name).c_str(),L"wb"); if(!out) throw std::runtime_error("clip write failed");
                fprintf(out,"P6\n%u %u\n255\n",width,height); fwrite(f.rgb[i].data(),1,f.rgb[i].size(),out); fclose(out);
            }
        }
        if(csv) fclose(csv);
        FILE* total=nullptr; _wfopen_s(&total,(path+L"/clip-total.txt").c_str(),L"wb"); if(total) {fprintf(total,"submitted=%zu completed=%zu skipped_busy=%u step=6 async=1\n",frames.size(),frames.size(),skipped);fclose(total);}
        for(auto& r:reads) if(r.index>=0) throw std::runtime_error("clip query did not complete after shutdown");
    }
};


