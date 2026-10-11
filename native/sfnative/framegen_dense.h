// Dense coarse-to-fine optical flow between the two true pictures (FrameGenDense, default on).
// Luminance pyramid 1/4 .. 1/64 of the output; full search at the coarsest level, then at each finer level the upsampled
// vector of this pixel and of its neighbours are candidates, refined by +-1 with a sub-pixel fit and cleaned by a 3x3
// vector median. Patches are compared with their mean removed so a flash or fade does not look like motion.
// Result: forward (old -> new) and backward (new -> old) vectors in level-0 pixels, float4(vx, vy, confidence, cost),
// the same layout the block matcher wrote, so the output shader reads either.
class DenseFlow {
public:
    static constexpr int Levels=5,Final=0; // flow ends at level 0 (1/4 of the output, one vector per 4 px)
    struct Image { Texture texture; ComPtr<ID3D11UnorderedAccessView> target; };
    struct Level { unsigned w=0,h=0; Image gray[2],flow[2],spare; };
    std::array<Level,Levels> level;
    ComPtr<ID3D11ComputeShader> lum0,down,coarse,refine,median;
    ComPtr<ID3D11Buffer> constants;
    unsigned width=0,height=0;
    struct Params { float size[4],param[4]; };
    static const char* code() { return R"(
cbuffer Dense:register(b1) { float4 dsize; float4 dparam; };
// dsize: this level w,h and the coarser level w,h. dparam: full output w,h, worldFlip, coarse search radius.
Texture2D<float4> srcImage:register(t0);
Texture2D<float> gA:register(t1),gB:register(t2);
Texture2D<float4> coarseFlow:register(t3);
SamplerState lin:register(s0);
RWTexture2D<float> outGray:register(u0);
RWTexture2D<float4> outFlow:register(u1);
[numthreads(8,8,1)] void Lum0(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(dsize.xy))) return;
    float2 uv=(id.xy+.5)/dsize.xy,o=1./dparam.xy;
    float3 c=0;
    [unroll] for(int k=0;k<4;k++) {
        float2 p=uv+o*float2(k&1?1:-1,k&2?1:-1);
        if(dparam.z>.5) p.y=1-p.y;
        c+=srcImage.SampleLevel(lin,p,0).rgb;
    }
    outGray[id.xy]=dot(c*.25,float3(.299,.587,.114));
}
[numthreads(8,8,1)] void Down(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(dsize.xy))) return;
    outGray[id.xy]=gA.SampleLevel(lin,(id.xy+.5)/dsize.xy,0); // gA = the finer level, bilinear = 2x2 average
}
float patchCost(int2 p,float2 v,float a[25],float ma) {
    float b[25],mb=0;
    [unroll] for(int i=0;i<25;i++) { float2 q=float2(p+int2(i%5-2,i/5-2))+v; b[i]=gB.SampleLevel(lin,(q+.5)/dsize.xy,0); mb+=b[i]; }
    mb/=25; float s=0;
    [unroll] for(int j=0;j<25;j++) s+=abs((a[j]-ma)-(b[j]-mb));
    return s/25+.15*abs(ma-mb);
}
void loadPatch(int2 p,out float a[25],out float ma) {
    ma=0; int2 hi=int2(dsize.xy)-1;
    [unroll] for(int i=0;i<25;i++) { a[i]=gA.Load(int3(clamp(p+int2(i%5-2,i/5-2),0,hi),0)); ma+=a[i]; }
    ma/=25;
}
float4 finish(int2 p,float2 best,float cost,float a[25],float ma) {
    // Sub-pixel: parabola through the +-1 costs on each axis.
    float xm=patchCost(p,best-float2(1,0),a,ma),xp=patchCost(p,best+float2(1,0),a,ma);
    float ym=patchCost(p,best-float2(0,1),a,ma),yp=patchCost(p,best+float2(0,1),a,ma);
    float2 curv=float2(xm+xp-2*cost,ym+yp-2*cost);
    float2 frac=clamp(float2(xm-xp,ym-yp)/(2*max(curv,1e-5)),-.5,.5);
    return float4(best+frac,cost<.045?1:0,cost);
}
[numthreads(8,8,1)] void Coarse(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(dsize.xy))) return;
    int2 p=int2(id.xy); float a[25],ma; loadPatch(p,a,ma);
    float2 best=0; float cost=patchCost(p,0,a,ma);
    int r=int(dparam.w);
    [loop] for(int y=-r;y<=r;y++) [loop] for(int x=-r;x<=r;x++) {
        float2 v=float2(x,y); float c=patchCost(p,v,a,ma)+.002*length(v);
        if(c<cost) { cost=c; best=v; }
    }
    outFlow[id.xy]=float4(best,1,cost);
}
// Integer-offset patch cost (plain loads, no filtering): the refinement searches whole pixels and fits the fraction.
float patchCostInt(int2 p,int2 v,float a[25],float ma) {
    float b[25],mb=0; int2 hi=int2(dsize.xy)-1;
    [unroll] for(int i=0;i<25;i++) { b[i]=gB.Load(int3(clamp(p+v+int2(i%5-2,i/5-2),0,hi),0)); mb+=b[i]; }
    mb/=25; float s=0;
    [unroll] for(int j=0;j<25;j++) s+=abs((a[j]-ma)-(b[j]-mb));
    return s/25+.15*abs(ma-mb);
}
[numthreads(8,8,1)] void Refine(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(dsize.xy))) return;
    int2 p=int2(id.xy); float a[25],ma; loadPatch(p,a,ma);
    float2 uv=(id.xy+.5)/dsize.xy;
    float2 pred=2*coarseFlow.SampleLevel(lin,uv,0).xy;
    float2 cp=(id.xy+.5)*.5-.5; int2 c0=int2(round(cp)); int2 chi=int2(dsize.zw)-1;
    int2 cand[6];
    cand[0]=int2(round(pred)); cand[1]=0;
    cand[2]=int2(round(2*coarseFlow.Load(int3(clamp(c0+int2(1,0),0,chi),0)).xy)); cand[3]=int2(round(2*coarseFlow.Load(int3(clamp(c0-int2(1,0),0,chi),0)).xy));
    cand[4]=int2(round(2*coarseFlow.Load(int3(clamp(c0+int2(0,1),0,chi),0)).xy)); cand[5]=int2(round(2*coarseFlow.Load(int3(clamp(c0-int2(0,1),0,chi),0)).xy));
    int2 best=cand[0]; float cost=1e9;
    [unroll] for(int k=0;k<6;k++) { float c=patchCostInt(p,cand[k],a,ma)+.004*length(float2(cand[k])-pred); if(c<cost) { cost=c; best=cand[k]; } }
    // 8 neighbours of the best whole-pixel vector; the cross among them gives the sub-pixel parabola.
    int2 center=best; float cc=patchCostInt(p,center,a,ma);
    float n[9]; n[4]=cc;
    [unroll] for(int j=0;j<9;j++) { if(j==4) continue; n[j]=patchCostInt(p,center+int2(j%3-1,j/3-1),a,ma); }
    int pick=4; float score=1e9;
    [unroll] for(int q=0;q<9;q++) { float c=n[q]+.004*length(float2(center+int2(q%3-1,q/3-1))-pred); if(c<score) { score=c; pick=q; } }
    float low=n[pick];
    int px=pick%3,py=pick/3; int2 vi=center+int2(px-1,py-1);
    // Cross of the picked vector: inside the 3x3 it is already known, outside it costs one more evaluation.
    float xm=px>0?n[pick-1]:patchCostInt(p,vi-int2(1,0),a,ma),xp=px<2?n[pick+1]:patchCostInt(p,vi+int2(1,0),a,ma);
    float ym=py>0?n[pick-3]:patchCostInt(p,vi-int2(0,1),a,ma),yp=py<2?n[pick+3]:patchCostInt(p,vi+int2(0,1),a,ma);
    float2 curv=float2(xm+xp-2*low,ym+yp-2*low);
    float2 frac=float2(curv.x>1e-5?clamp((xm-xp)/(2*curv.x),-.5,.5):0,curv.y>1e-5?clamp((ym-yp)/(2*curv.y),-.5,.5):0);
    outFlow[id.xy]=float4(float2(vi)+frac,low<.045?1:0,low);
}
// 3x3 vector median: the neighbour vector with the smallest summed distance to the others. Removes lone wrong matches
// without blurring a motion boundary (the median is one of the actual vectors).
[numthreads(8,8,1)] void Median(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(dsize.xy))) return;
    int2 hi=int2(dsize.xy)-1; float4 n[9];
    [unroll] for(int i=0;i<9;i++) n[i]=coarseFlow.Load(int3(clamp(int2(id.xy)+int2(i%3-1,i/3-1),0,hi),0));
    float bestSum=1e9; int pick=4;
    [unroll] for(int j=0;j<9;j++) { float s=0; [unroll] for(int k=0;k<9;k++) s+=abs(n[j].x-n[k].x)+abs(n[j].y-n[k].y); if(s<bestSum) { bestSum=s; pick=j; } }
    float4 own=n[4]; outFlow[id.xy]=float4(n[pick].xy,own.z,own.w);
}
)"; }
    void initialize(ID3D11Device* device,unsigned w,unsigned h) {
        if(lum0) return;
        width=w; height=h;
        auto compile=[&](const char* entry) {ComPtr<ID3DBlob> result,errors;HRESULT hr=D3DCompile(code(),strlen(code()),nullptr,nullptr,nullptr,entry,"cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&result,&errors);if(FAILED(hr)) throw std::runtime_error(errors?static_cast<const char*>(errors->GetBufferPointer()):"dense shader compile failed");ComPtr<ID3D11ComputeShader> s;check(device->CreateComputeShader(result->GetBufferPointer(),result->GetBufferSize(),nullptr,&s));return s;};
        lum0=compile("Lum0");down=compile("Down");coarse=compile("Coarse");refine=compile("Refine");median=compile("Median");
        D3D11_BUFFER_DESC bd{};bd.ByteWidth=sizeof(Params);bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&bd,nullptr,&constants));
        auto image=[&](Image& out,unsigned iw,unsigned ih,DXGI_FORMAT format) {D3D11_TEXTURE2D_DESC td{};td.Width=iw;td.Height=ih;td.MipLevels=td.ArraySize=1;td.Format=format;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_UNORDERED_ACCESS|D3D11_BIND_SHADER_RESOURCE;check(device->CreateTexture2D(&td,nullptr,&out.texture.texture));check(device->CreateShaderResourceView(out.texture.texture.Get(),nullptr,&out.texture.view));check(device->CreateUnorderedAccessView(out.texture.texture.Get(),nullptr,&out.target));};
        unsigned lw=(w+3)/4,lh=(h+3)/4;
        for(int k=0;k<Levels;k++) {
            auto& l=level[k]; l.w=std::max(1u,lw); l.h=std::max(1u,lh);
            for(auto& g:l.gray) image(g,l.w,l.h,DXGI_FORMAT_R16_FLOAT);
            for(auto& f:l.flow) image(f,l.w,l.h,DXGI_FORMAT_R32G32B32A32_FLOAT);
            image(l.spare,l.w,l.h,DXGI_FORMAT_R32G32B32A32_FLOAT);
            lw=(lw+1)/2; lh=(lh+1)/2;
        }
    }
    unsigned baseW() const { return level[Final].w; }
    unsigned baseH() const { return level[Final].h; }
    ID3D11ShaderResourceView* forward() const { return level[Final].flow[0].texture.view.Get(); }
    ID3D11ShaderResourceView* backward() const { return level[Final].flow[1].texture.view.Get(); }
    // Records the whole pyramid into the deferred context (sampler s0 is the caller's linear clamp).
    void run(ID3D11DeviceContext* c,ID3D11ShaderResourceView* oldImage,ID3D11ShaderResourceView* newImage,bool flip,ID3D11SamplerState* sampler) {
        ID3D11ShaderResourceView* noViews[4]{}; ID3D11UnorderedAccessView* noTargets[2]{};
        auto params=[&](const Level& l,const Level* coarser,float radius) {
            Params p{{float(l.w),float(l.h),coarser?float(coarser->w):0.f,coarser?float(coarser->h):0.f},{float(width),float(height),flip?1.f:0.f,radius}};
            c->UpdateSubresource(constants.Get(),0,nullptr,&p,0,0);
        };
        auto cb=constants.Get(); c->CSSetConstantBuffers(1,1,&cb); c->CSSetSamplers(0,1,&sampler);
        auto dispatch=[&](const Level& l) { c->Dispatch((l.w+7)/8,(l.h+7)/8,1); };
        // Luminance pyramids.
        for(int i=0;i<2;i++) {
            params(level[0],nullptr,0);
            c->CSSetShader(lum0.Get(),nullptr,0);
            ID3D11ShaderResourceView* src=i?newImage:oldImage; c->CSSetShaderResources(0,1,&src);
            auto t=level[0].gray[i].target.Get(); c->CSSetUnorderedAccessViews(0,1,&t,nullptr); dispatch(level[0]);
            c->CSSetUnorderedAccessViews(0,2,noTargets,nullptr); c->CSSetShaderResources(0,4,noViews);
            c->CSSetShader(down.Get(),nullptr,0);
            for(int k=1;k<Levels;k++) {
                params(level[k],nullptr,0);
                auto finer=level[k-1].gray[i].texture.view.Get(); c->CSSetShaderResources(1,1,&finer);
                auto target=level[k].gray[i].target.Get(); c->CSSetUnorderedAccessViews(0,1,&target,nullptr); dispatch(level[k]);
                c->CSSetUnorderedAccessViews(0,2,noTargets,nullptr); c->CSSetShaderResources(0,4,noViews);
            }
        }
        // Flow, both directions: d=0 old->new, d=1 new->old.
        for(int d=0;d<2;d++) {
            for(int k=Levels-1;k>=Final;k--) {
                Level& l=level[k];
                ID3D11ShaderResourceView* grays[2]={l.gray[d].texture.view.Get(),l.gray[1-d].texture.view.Get()};
                if(k==Levels-1) {
                    params(l,nullptr,8);
                    c->CSSetShader(coarse.Get(),nullptr,0);
                    c->CSSetShaderResources(1,2,grays);
                } else {
                    params(l,&level[k+1],0);
                    c->CSSetShader(refine.Get(),nullptr,0);
                    ID3D11ShaderResourceView* views[3]={grays[0],grays[1],level[k+1].flow[d].texture.view.Get()};
                    c->CSSetShaderResources(1,3,views);
                }
                auto spare=l.spare.target.Get(); c->CSSetUnorderedAccessViews(1,1,&spare,nullptr); dispatch(l);
                c->CSSetUnorderedAccessViews(0,2,noTargets,nullptr); c->CSSetShaderResources(0,4,noViews);
                // Median: spare -> flow[d]
                params(l,nullptr,0);
                c->CSSetShader(median.Get(),nullptr,0);
                auto sv=l.spare.texture.view.Get(); c->CSSetShaderResources(3,1,&sv);
                auto ft=l.flow[d].target.Get(); c->CSSetUnorderedAccessViews(1,1,&ft,nullptr); dispatch(l);
                c->CSSetUnorderedAccessViews(0,2,noTargets,nullptr); c->CSSetShaderResources(0,4,noViews);
            }
        }
    }
};
