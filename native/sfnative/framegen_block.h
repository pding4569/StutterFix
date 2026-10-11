// Default-OFF experiment. GPU block matching between TWO KNOWN images; no camera prediction.
// All GPU work is ordered through the existing protected D3D11 immediate context.
class BlockFlow {
    struct Image { Texture texture; ComPtr<ID3D11UnorderedAccessView> target; };
    std::array<Image,2> gray,flow;
    ComPtr<ID3D11DeviceContext> commands;
    ComPtr<ID3D11ComputeShader> reduce,match;
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    ComPtr<ID3D11Buffer> constants;
#ifdef SF_FRAMEGEN_RESEARCH
    ComPtr<ID3D11ComputeShader> metric;
    ComPtr<ID3D11Buffer> counters,counterRead;
    ComPtr<ID3D11UnorderedAccessView> counterTarget;
#endif
    ComPtr<ID3D11SamplerState> linear;
    unsigned width=0,height=0,smallW=0,smallH=0,blocksW=0,blocksH=0,scale=8,blockSize=8,variant=0;
    unsigned skippedPairs=0;
    DenseFlow dense; bool denseOn=false; // capture bit 18: dense pyramid flow instead of the 8x8 block matcher
    unsigned long long sequence=0;
    // planets[k] = (old world x, y, new world x, y) of planet k (0 red, 1 blue). disc = (red radius, blue radius, aspect,
    // valid) with radii in world units. cams = camera (x, y, size, angle) of the old picture, the new picture and the
    // interpolated output. World space keeps the planet move correct while the camera pans, zooms or rotates.
    // tnow = interpolated planet world positions (red x, y, blue x, y) at this output's phase; orbits are followed
    // as arcs about the pivot (a straight line between two true positions cuts inside the orbit).
    // hintInfo.x = hint count; hints = decoration motion hints of the newer picture (box min/max uv, 2x2, translation).
    struct Data { float info[4],grid[4],boxes[4][4],planets[2][4],disc[4],cams[3][4],tnow[4],hintInfo[4],hints[32][12]; };
    static const char* code() { return R"(
cbuffer Data:register(b0) { float4 info; float4 grid; float4 boxes[4]; float4 planets[2]; float4 disc; float4 cams[3]; float4 tnow; float4 hintInfo; float4 hintData[96]; };
// info: width,height,phase,worldFlip. grid: smallW,smallH,blocksW,blocksH.
Texture2D<float4> oldWorld:register(t0),newWorld:register(t1),oldScreen:register(t2),newScreen:register(t3);
Texture2D<float4> forwardFlow:register(t4),backwardFlow:register(t5);
Texture2D<float> grayA:register(t6),grayB:register(t7);
SamplerState linearClamp:register(s0);
RWTexture2D<float> reduced:register(u0); RWTexture2D<float4> vectors:register(u1);
RWStructuredBuffer<uint> totals:register(u2);
// info.w = worldFlip + 2*blendMode. Mode 0: nearest true frame around planets and where flow is unreliable; 1: dissolve the two true pictures around planets; 2: also dissolve where flow is unreliable.
bool flipped() { return fmod(info.w,2.)>.5; }
int blendMode() { return int(floor(info.w*.5+.001)); }
float2 worldUV(float2 uv) { if(flipped()) uv.y=1-uv.y; return uv; }
float3 worldColor(Texture2D<float4> image,float2 uv) { return image.SampleLevel(linearClamp,worldUV(uv),0).rgb; }
[numthreads(8,8,1)] void Reduce(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(grid.xy))) return;
    float2 uv=(id.xy+.5)/grid.xy,offset=2./info.xy;
    float3 c=worldColor(oldWorld,uv+offset)+worldColor(oldWorld,uv-offset)+worldColor(oldWorld,uv+offset*float2(1,-1))+worldColor(oldWorld,uv+offset*float2(-1,1));
    reduced[id.xy]=dot(c*.25,float3(.299,.587,.114));
}
float errorAt(float values[16],int2 origin,int2 delta) {
    if(any(origin+delta<0) || any(origin+delta+6>=int2(grid.xy))) return 1000;
    float sum=0; [unroll] for(int k=0;k<16;k++) {
        float b=grayB.Load(int3(origin+delta+int2(k%4,k/4)*2,0)); float d=values[k]-b; sum+=d*d;
    } return sum/16;
}
[numthreads(8,8,1)] void Match(uint3 id:SV_DispatchThreadID) {
    if(any(id.xy>=uint2(grid.zw))) return;
    int blockSize=max(1,int(ceil(grid.x/grid.z))); int2 origin=int2(id.xy)*blockSize; float values[16],mean=0,squares=0;
    [unroll] for(int k=0;k<16;k++) { float a=grayA.Load(int3(min(origin+int2(k%4,k/4)*2,int2(grid.xy)-1),0)); values[k]=a; mean+=a; squares+=a*a; }
    float variance=squares/16-(mean/16)*(mean/16);
    if(variance<.0004) { vectors[id.xy]=0; return; }
    int2 best=0; float cost=errorAt(values,origin,best),zero=cost;
    // Exhaust the coarse grid before refining: a local hill climb misses even
    // exact translations of detailed textures. Exact zero also stays EXACT zero.
    if(cost>1e-8) {
        // Keep 49 coarse candidates. Scale their positions with the actual output
        // grid, then refine in reduced-image pixels as before (3440/2560=1.34375).
        float2 searchScale=max(float2(1,1),info.xy/float2(2560,1440));
        // +-20 reduced pixels (about +-215 px at 3440 wide): a 60 FPS true frame moves a fast decoration (a big moving
        // title text crosses ~90 px between two true frames) well beyond the old +-6. Coarse grid (121 candidates),
        // then +-3 around the best (49), then +-1 (9): 40% of a flat +-20 search.
        [loop] for(int y=-20;y<=20;y+=4) [loop] for(int x=-20;x<=20;x+=4) {
            int2 d=int2(round(float2(x,y)*searchScale)); float c=errorAt(values,origin,d);
            if(c<cost-1e-7) { cost=c; best=d; }
        }
        int2 center=best;
        [loop] for(int y=-3;y<=3;y++) [loop] for(int x=-3;x<=3;x++) {
            int2 d=center+int2(x,y); float c=errorAt(values,origin,d);
            if(c<cost-1e-7) { cost=c; best=d; }
        }
        center=best;
        [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++) {
            int2 d=center+int2(x,y); float c=errorAt(values,origin,d);
            if(c<cost-1e-7) { cost=c; best=d; }
        }
    }
    float xm=errorAt(values,origin,best-int2(1,0)),xp=errorAt(values,origin,best+int2(1,0));
    float ym=errorAt(values,origin,best-int2(0,1)),yp=errorAt(values,origin,best+int2(0,1));
    float2 curvature=float2(xm+xp-2*cost,ym+yp-2*cost);
    float2 fractional=cost>1e-8 ? clamp(float2(xm-xp,ym-yp)/(2*max(curvature,1e-6)),-.5,.5) : 0;
    float confidence=cost<.008 && (all(best==0) || cost<zero*.85) ? 1 : 0;
    vectors[id.xy]=float4(best+fractional,confidence,cost);
}
bool inside(float2 uv,float4 b) { return b.z>b.x && b.w>b.y && all(uv>=b.xy) && all(uv<=b.zw); }
float4 flowAt(Texture2D<float4> f,float2 uv) {
    if(grid.x==grid.z && grid.x>1) return f.SampleLevel(linearClamp,uv,0); // dense flow: one vector per 4 px, interpolated
    int blockSize=max(1,int(ceil(grid.x/grid.z))); return f.Load(int3(clamp(int2(uv*grid.xy)/blockSize,0,int2(grid.zw)-1),0));
}
float3 closest(float2 uv) { return info.z<.5 ? oldScreen.SampleLevel(linearClamp,uv,0).rgb : newScreen.SampleLevel(linearClamp,uv,0).rgb; }
float difference(float3 a,float3 b) { return max(max(abs(a.r-b.r),abs(a.g-b.g)),abs(a.b-b.b)); }
float2 rot(float2 p,float a) { float s,c; sincos(a,s,c); return float2(c*p.x-s*p.y,s*p.x+c*p.y); }
// Screen UV (top origin) <-> world for camera c = (x, y, size, angle); matches the main output shader and screenPoint().
float2 toWorld(float2 uv,float4 c) { return rot(float2((uv.x-.5)*disc.z,.5-uv.y)*2*c.z,c.w)+c.xy; }
float2 toUV(float2 w,float4 c) { float2 q=rot(w-c.xy,-c.w)/(2*c.z); return float2(.5+q.x/disc.z,.5-q.y); }
// Global change test: a flash, a scene cut or a teleport changes most of the picture. Dissolving those would
// invent grey in-between pictures, so a whole-picture change keeps the nearest true frame.
bool globalChange() {
    float sum=0;
    [unroll] for(int j=0;j<3;j++) [unroll] for(int i=0;i<3;i++) {
        float2 p=float2(.2+.3*i,.2+.3*j);
        float d=difference(worldColor(oldWorld,p),worldColor(newWorld,p));
        // A fast camera pan moves the whole picture on screen without changing it: also compare the two pictures at
        // the same world point (camera valid) and take the smaller difference.
        if(disc.w>.5) {
            float2 w=toWorld(p,cams[2]);
            d=min(d,difference(worldColor(oldWorld,toUV(w,cams[0])),worldColor(newWorld,toUV(w,cams[1]))));
        }
        sum+=d;
    }
    return sum/9>.18;
}
// Difference of two candidate pictures judged over a small neighbourhood, so the pick between "blend" and "nearest
// picture" is the same for a whole moving edge instead of flickering pixel by pixel (salt-and-pepper along fast letters).
float alignedDifference(float2 uvOld,float2 uvNew) {
    // Eight taps on a ring about 8 px out plus the centre: wide enough that the decision is shared by a moving
    // edge and its neighbourhood (a 3 px ring still flickered at the letters of a fast-moving decoration).
    float2 t=8./info.xy;
    float d=difference(worldColor(oldWorld,uvOld),worldColor(newWorld,uvNew));
    [unroll] for(int k=0;k<8;k++) {
        float2 o=float2(k==0||k==4?1:k==1||k==5?.7:k==2||k==6?0:-.7, k==0||k==4?0:k==1||k==5?.7:k==2||k==6?1:.7);
        if(k>=4) o=-o;
        d=max(d,difference(worldColor(oldWorld,uvOld+o*t),worldColor(newWorld,uvNew+o*t)));
    }
    return d;
}
// Block-flow path (content that moves on its own: decorations, particles, anything the cameras do not explain).
)" R"(float3 flowColor(float2 uv,float3 fallback,float3 a,float3 b,out bool moving) {
    moving=false;
    float4 f=flowAt(forwardFlow,uv),r=flowAt(backwardFlow,uv);
    float4 checkR=flowAt(backwardFlow,uv+f.xy/grid.xy);
    bool own=!(f.z<.5 || r.z<.5 || checkR.z<.5 || length(f.xy+checkR.xy)>1.25);
    float bestErr=9; float3 best=fallback;
    if(own) {
        float2 oldUV=uv-info.z*f.xy/grid.xy,newUV=uv-(1-info.z)*r.xy/grid.xy;
        if(all(oldUV>=0) && all(oldUV<=1) && all(newUV>=0) && all(newUV<=1)) {
            bestErr=alignedDifference(oldUV,newUV);
            best=lerp(worldColor(oldWorld,oldUV),worldColor(newWorld,newUV),info.z);
        }
    }
    // Decoration motion hints: the exact screen affine map of a decoration the mod animated (scale and rotation
    // included, which no translation vector can express). For this output point at phase z, find the old point pA
    // with (1-z)*pA + z*T(pA) = uv, then pB = T(pA). Used only where it explains the two pictures better.
    int hc=int(hintInfo.x+.5);
    [loop] for(int h=0;h<hc;h++) {
        float4 box=hintData[h*3];
        if(any(uv<box.xy) || any(uv>box.zw)) continue;
        float4 L=hintData[h*3+1]; float2 t=hintData[h*3+2].xy; float z=info.z;
        float m00=1-z+z*L.x,m01=z*L.y,m10=z*L.z,m11=1-z+z*L.w,det=m00*m11-m01*m10;
        if(abs(det)<1e-4) continue;
        float2 rhs=uv-z*t;
        float2 pA=float2(m11*rhs.x-m01*rhs.y,-m10*rhs.x+m00*rhs.y)/det;
        float2 pB=float2(L.x*pA.x+L.y*pA.y,L.z*pA.x+L.w*pA.y)+t;
        if(any(pA<0) || any(pA>1) || any(pB<0) || any(pB>1)) continue;
        float e=alignedDifference(pA,pB);
        if(e<bestErr-.01) { bestErr=e; best=lerp(worldColor(oldWorld,pA),worldColor(newWorld,pB),z); own=true; }
    }
    if(!own || bestErr>=9) {
        if(blendMode()<2 && disc.w<=.5) return fallback;
        moving=true; return disc.w>.5 ? fallback : lerp(a,b,info.z);
    }
    float occlusion=smoothstep(.25,.65,bestErr); // Occlusion/effects: no double image.
    moving=true;
    return lerp(best,fallback,occlusion);
}
)" R"(float3 colorAt(float2 uv,out bool moving) {
    moving=false; float3 fallback=closest(uv);
    if(info.z<=0 || info.z>=1) return fallback;
    int blend=blendMode();
    bool envelope=false;
    [unroll] for(int k=0;k<4;k++) if(inside(uv,boxes[k])) envelope=true;
    if(envelope && blend<1) return fallback; // Mode 0: planets only change on true frames.
    float3 a=worldColor(oldWorld,uv),b=worldColor(newWorld,uv);
    float3 sa=oldScreen.SampleLevel(linearClamp,uv,0).rgb,sb=newScreen.SampleLevel(linearClamp,uv,0).rgb;
    if(difference(a,sa)>2.5/255. || difference(b,sb)>2.5/255.) return fallback; // True-frame UI.
    if(max(max(a.r,a.g),a.b)<1./255. && max(max(b.r,b.g),b.b)<1./255.) return fallback; // Fixed black border.
    if(disc.w<=.5) {
        // No usable cameras: flow only, as before.
        if(envelope) { if(globalChange()) return fallback; moving=true; return lerp(a,b,info.z); }
        return flowColor(uv,fallback,a,b,moving);
    }
    if(globalChange()) return fallback;
    float4 camOld=cams[0],camNew=cams[1],camNow=cams[2];
    float2 w=toWorld(uv,camNow),ua=toUV(w,camOld),ub=toUV(w,camNew);
    if(!(all(ua>=0) && all(ua<=1) && all(ub>=0) && all(ub<=1))) return flowColor(uv,fallback,a,b,moving);
    float3 A=worldColor(oldWorld,ua),B=worldColor(newWorld,ub);
    // Planets (ball, its glow): both true pictures carry a planet at its own position. Background = the picture that
    // has no planet at this world point; the planet itself is drawn at its interpolated position as "what it adds"
    // (picture minus the other picture at the same world point), so its glow fades out with no rim.
    float freeA=1,freeB=1; bool zone=false;
    [unroll] for(int k=0;k<2;k++) {
        float radius=k==0?disc.x:disc.y;
        if(radius>0) {
            float reach=radius*2.6;
            float2 wa=planets[k].xy,wb=planets[k].zw,wt=k==0?tnow.xy:tnow.zw;
            freeA=min(freeA,smoothstep(reach*.8,reach*1.1,length(w-wa)));
            freeB=min(freeB,smoothstep(reach*.8,reach*1.1,length(w-wb)));
            if(length(w-wt)<reach*1.2) zone=true;
        }
    }
    if(zone || freeA<1 || freeB<1) {
        float wA=freeA*(1-info.z),wB=freeB*info.z;
        float3 bg=(wA+wB>1e-3)?(A*wA+B*wB)/(wA+wB):lerp(A,B,info.z);
        float3 delta=0,core=0; float coreMask=0;
        [unroll] for(int k=0;k<2;k++) {
            float radius=k==0?disc.x:disc.y;
            if(radius>0) {
                float reach=radius*2.6;
                float2 wa=planets[k].xy,wb=planets[k].zw,wt=k==0?tnow.xy:tnow.zw;
                float dist=length(w-wt);
                if(dist<reach*1.15) {
                    float2 sa2=w-wt+wa,sb2=w-wt+wb;
                    float3 pA=worldColor(oldWorld,toUV(sa2,camOld)),pB=worldColor(newWorld,toUV(sb2,camNew));
                    // Glow: additive, so "what it adds" = picture minus the other picture at the same world point.
                    float sep=smoothstep(radius*1.8,radius*3.,length(wb-wa)); // sprites no longer overlap: the other picture shows no planet here
                    float m=smoothstep(reach*1.15,reach*.7,dist)*sep;
                    float3 dA=pA-worldColor(newWorld,toUV(sa2,camNew));
                    float3 dB=pB-worldColor(oldWorld,toUV(sb2,camOld));
                    float3 dd=lerp(dA,dB,info.z);
                    delta+=m*sign(dd)*max(abs(dd)-.02,0.); // film grain/dither differs between pictures: not part of the planet
                    // Sprite: opaque, so its core is pasted (no background shows through it, whatever is under it now).
                    float mc=smoothstep(radius*.75,radius*.55,dist);
                    // The pasted sprite carries the glow it had at its source. Where the background here is much
                    // brighter or darker than at the source (a planet moving into or out of a bloom), pasting leaves a
                    // visible grey disc on the glow; the additive layer alone looks right there.
                    float lumHere=dot(bg,float3(.299,.587,.114));
                    float lumSource=lerp(dot(pA-dA,float3(.299,.587,.114)),dot(pB-dB,float3(.299,.587,.114)),info.z);
                    mc*=1-smoothstep(.08,.28,abs(lumHere-lumSource));
                    if(mc>coreMask) { coreMask=mc; core=lerp(pA,pB,info.z); }
                }
            }
        }
        float3 shaded=saturate(bg+delta);
        float3 result=lerp(shaded,core,coreMask);
        if(disc.w>1.5) {
            // Diagnostic markers: white = interpolated planet position, red = old picture's, blue = new picture's.
            [unroll] for(int k=0;k<2;k++) {
                float radius=k==0?disc.x:disc.y;
                if(radius>0) {
                    float2 wa=planets[k].xy,wb=planets[k].zw,wt=k==0?tnow.xy:tnow.zw;
                    float thick=radius*.12;
                    if(abs(length(w-wt)-radius*.9)<thick) result=float3(1,1,1);
                    if(abs(length(w-wa)-radius*.6)<thick) result=float3(1,0,0);
                    if(abs(length(w-wb)-radius*.6)<thick) result=float3(0,0,1);
                }
            }
        }
        moving=true; return result;
    }
    // Content fixed in the world (tiles, anything that follows the cameras): aligned by the known cameras, exactly;
    // content fixed on the screen (filters, HUD-like decorations): aligned on screen. Only what neither explains
    // (decorations with their own motion) goes to block matching.
    float dc=alignedDifference(ua,ub),ds=alignedDifference(uv,uv);
    float d=min(dc,ds);
    float3 fixedColor=ds<=dc?lerp(a,b,info.z):lerp(A,B,info.z);
    moving=true;
    if(d<.12) return fixedColor;
    bool flowMoving; float3 viaFlow=flowColor(uv,fallback,a,b,flowMoving);
    return lerp(fixedColor,viaFlow,smoothstep(.12,.3,d));
}
struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
V VS(uint id:SV_VertexID) { V v;v.uv=float2((id<<1)&2,id&2);v.position=float4(v.uv*float2(2,-2)+float2(-1,1),0,1);return v; }
float4 PS(V v):SV_TARGET { bool moving;return float4(colorAt(v.uv,moving),1); }
groupshared uint counts[4];
[numthreads(16,16,1)] void Metric(uint3 id:SV_GroupThreadID,uint index:SV_GroupIndex) {
    if(index==0) { counts[0]=counts[1]=counts[2]=counts[3]=0; } GroupMemoryBarrierWithGroupSync();
    float2 uv=(id.xy+.5)/16; bool moving;float3 c=colorAt(uv,moving);
    bool a=difference(c,oldScreen.SampleLevel(linearClamp,uv,0).rgb)>3./255.;
    bool b=difference(c,newScreen.SampleLevel(linearClamp,uv,0).rgb)>3./255.;
    if(a) InterlockedAdd(counts[0],1);if(b) InterlockedAdd(counts[1],1);
    if(a && b) InterlockedAdd(counts[2],1);if(moving) InterlockedAdd(counts[3],1);
    GroupMemoryBarrierWithGroupSync();
    if(index==0) { InterlockedAdd(totals[0],1); if(counts[0]>=4 && counts[1]>=4 && counts[3]>=4) InterlockedAdd(totals[1],1);
        InterlockedAdd(totals[2],counts[2]); InterlockedAdd(totals[3],counts[3]); }
}
)"; }
    void bind(const Data& data,const Slot& a,const Slot& b) {
        commands->ClearState(); commands->UpdateSubresource(constants.Get(),0,nullptr,&data,0,0);
        auto cb=constants.Get(); commands->CSSetConstantBuffers(0,1,&cb);commands->PSSetConstantBuffers(0,1,&cb);
        auto sampler=linear.Get();commands->CSSetSamplers(0,1,&sampler);commands->PSSetSamplers(0,1,&sampler);
        ID3D11ShaderResourceView* views[]={a.images[0].view.Get(),b.images[0].view.Get(),a.images[1].view.Get(),b.images[1].view.Get(),denseOn?dense.forward():flow[0].texture.view.Get(),denseOn?dense.backward():flow[1].texture.view.Get()};
        commands->PSSetShaderResources(0,6,views);commands->CSSetShaderResources(0,6,views);
    }
    void execute(ID3D11DeviceContext* immediate) { ComPtr<ID3D11CommandList> list;check(commands->FinishCommandList(FALSE,&list));immediate->ExecuteCommandList(list.Get(),TRUE); }
public:
    void resetMetrics(ID3D11DeviceContext* immediate) {
#ifdef SF_FRAMEGEN_RESEARCH
        if(counterTarget) {UINT zeros[4]{};immediate->ClearUnorderedAccessViewUint(counterTarget.Get(),zeros);}
#else
        (void)immediate;
#endif
    }
    void skipIfUnused(const Slot& source) {
        if(source.sequence && sequence!=source.sequence) ++skippedPairs;
    }
    void initialize(ID3D11Device* device,unsigned w,unsigned h,unsigned selectedVariant) {
        if(vertex) return; width=w;height=h;variant=selectedVariant;scale=variant==1?16:8;blockSize=variant==2?16:8;smallW=(w+scale-1)/scale;smallH=(h+scale-1)/scale;blocksW=(smallW+blockSize-1)/blockSize;blocksH=(smallH+blockSize-1)/blockSize;
        auto compile=[&](const char* entry,const char* profile) {ComPtr<ID3DBlob> result,errors;HRESULT hr=D3DCompile(code(),strlen(code()),nullptr,nullptr,nullptr,entry,profile,D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&result,&errors);if(FAILED(hr)) throw std::runtime_error(errors?static_cast<const char*>(errors->GetBufferPointer()):"block shader compile failed");return result;};
        auto vs=compile("VS","vs_5_0"),ps=compile("PS","ps_5_0"),cs=compile("Reduce","cs_5_0"),ms=compile("Match","cs_5_0");
        check(device->CreateVertexShader(vs->GetBufferPointer(),vs->GetBufferSize(),nullptr,&vertex));check(device->CreatePixelShader(ps->GetBufferPointer(),ps->GetBufferSize(),nullptr,&pixel));
        check(device->CreateComputeShader(cs->GetBufferPointer(),cs->GetBufferSize(),nullptr,&reduce));check(device->CreateComputeShader(ms->GetBufferPointer(),ms->GetBufferSize(),nullptr,&match));
#ifdef SF_FRAMEGEN_RESEARCH
        auto qs=compile("Metric","cs_5_0");check(device->CreateComputeShader(qs->GetBufferPointer(),qs->GetBufferSize(),nullptr,&metric));
#endif
        check(device->CreateDeferredContext(0,&commands));
        D3D11_BUFFER_DESC bd{};bd.ByteWidth=sizeof(Data);bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&bd,nullptr,&constants));
        D3D11_SAMPLER_DESC sd{};sd.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR;sd.AddressU=sd.AddressV=sd.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;sd.MaxLOD=D3D11_FLOAT32_MAX;check(device->CreateSamplerState(&sd,&linear));
        auto image=[&](Image& out,unsigned iw,unsigned ih,DXGI_FORMAT format) {D3D11_TEXTURE2D_DESC td{};td.Width=iw;td.Height=ih;td.MipLevels=td.ArraySize=1;td.Format=format;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_UNORDERED_ACCESS|D3D11_BIND_SHADER_RESOURCE;check(device->CreateTexture2D(&td,nullptr,&out.texture.texture));check(device->CreateShaderResourceView(out.texture.texture.Get(),nullptr,&out.texture.view));check(device->CreateUnorderedAccessView(out.texture.texture.Get(),nullptr,&out.target));};
        for(auto& g:gray) image(g,smallW,smallH,DXGI_FORMAT_R32_FLOAT);for(auto& f:flow) image(f,blocksW,blocksH,DXGI_FORMAT_R32G32B32A32_FLOAT);
#ifdef SF_FRAMEGEN_RESEARCH
        bd.ByteWidth=16;bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_UNORDERED_ACCESS;bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;bd.StructureByteStride=4;UINT zeros[4]{};D3D11_SUBRESOURCE_DATA initial{zeros,0,0};check(device->CreateBuffer(&bd,&initial,&counters));
        D3D11_UNORDERED_ACCESS_VIEW_DESC ud{};ud.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;ud.Buffer.NumElements=4;check(device->CreateUnorderedAccessView(counters.Get(),&ud,&counterTarget));
        bd.Usage=D3D11_USAGE_STAGING;bd.CPUAccessFlags=D3D11_CPU_ACCESS_READ;bd.BindFlags=bd.MiscFlags=bd.StructureByteStride=0;check(device->CreateBuffer(&bd,nullptr,&counterRead));
#endif
    }
    static double wrapAngle(double x) { const double pi=3.14159265358979323846; while(x>pi) x-=2*pi; while(x<-pi) x+=2*pi; return x; }
    static void rotateAbout(const double c[2],const double p[2],double angle,double out[2]) {
        double dx=p[0]-c[0],dy=p[1]-c[1],s=sin(angle),k=cos(angle); out[0]=c[0]+k*dx-s*dy; out[1]=c[1]+s*dx+k*dy;
    }
    // Planet world positions at phase t between two true frames. One planet is the pivot; the other turns about it,
    // and where the pivot changes inside the interval (a tile was reached) the first arc ends at the new pivot and
    // the second starts there. Angular speed is constant, so the angles split the interval. Anything that does not
    // fit (teleports, SetSpeed jumps, more than half a turn) falls back to a straight line.
    static void orbit(const double a[2][2],const double b[2][2],double t,float out[4]) {
        auto lineTo=[&]{ for(int k=0;k<2;k++) for(int c=0;c<2;c++) out[k*2+c]=float(a[k][c]+(b[k][c]-a[k][c])*t); };
        double sepA=hypot(a[1][0]-a[0][0],a[1][1]-a[0][1]),sepB=hypot(b[1][0]-b[0][0],b[1][1]-b[0][1]);
        double R=std::max(sepA,sepB); if(R<1e-4) { lineTo(); return; }
        double m0=hypot(b[0][0]-a[0][0],b[0][1]-a[0][1]),m1=hypot(b[1][0]-a[1][0],b[1][1]-a[1][1]),e=.06*R;
        double p[2][2];
        auto arcAbout=[&](int pivot,int mover,double u) { // mover turns about pivot (pivot lerped), radius lerped
            double c[2]={a[pivot][0]+(b[pivot][0]-a[pivot][0])*u,a[pivot][1]+(b[pivot][1]-a[pivot][1])*u};
            double a0=atan2(a[mover][1]-a[pivot][1],a[mover][0]-a[pivot][0]),a1=atan2(b[mover][1]-b[pivot][1],b[mover][0]-b[pivot][0]);
            double turn=wrapAngle(a1-a0),ra=hypot(a[mover][0]-a[pivot][0],a[mover][1]-a[pivot][1]),rb=hypot(b[mover][0]-b[pivot][0],b[mover][1]-b[pivot][1]);
            double ang=a0+turn*u,r=ra+(rb-ra)*u;
            p[mover][0]=c[0]+r*cos(ang);p[mover][1]=c[1]+r*sin(ang);p[pivot][0]=c[0];p[pivot][1]=c[1];
        };
        if(m0<e && m1<e) { lineTo(); return; }
        if(m0<e) { arcAbout(0,1,t); }
        else if(m1<e) { arcAbout(1,0,t); }
        else {
            // Pivot change inside the interval: the mover turns about the old pivot to its new tile, then the old pivot turns about it.
            double bestErr=1e30; int pv=0;
            for(int h=0;h<2;h++) {
                int pivot=h,mover=1-h;
                double err=fabs(hypot(b[mover][0]-a[pivot][0],b[mover][1]-a[pivot][1])-sepA)+fabs(hypot(b[pivot][0]-b[mover][0],b[pivot][1]-b[mover][1])-sepB);
                if(err<bestErr) { bestErr=err; pv=h; }
            }
            if(bestErr>.35*R) { lineTo(); return; }
            int mover=1-pv;
            double a1=wrapAngle(atan2(b[mover][1]-a[pv][1],b[mover][0]-a[pv][0])-atan2(a[mover][1]-a[pv][1],a[mover][0]-a[pv][0]));
            double a2=wrapAngle(atan2(b[pv][1]-b[mover][1],b[pv][0]-b[mover][0])-atan2(a[pv][1]-b[mover][1],a[pv][0]-b[mover][0]));
            double f=fabs(a1)/std::max(1e-9,fabs(a1)+fabs(a2)); f=std::clamp(f,.05,.95);
            if(t<f) {
                double u=t/f,q0[2]={a[pv][0],a[pv][1]},q1[2]={a[mover][0],a[mover][1]},r1[2];
                rotateAbout(q0,q1,a1*u,r1);
                p[pv][0]=q0[0];p[pv][1]=q0[1];p[mover][0]=r1[0];p[mover][1]=r1[1];
            } else {
                double u=(t-f)/(1-f),c[2]={b[mover][0],b[mover][1]},q[2]={a[pv][0],a[pv][1]},r1[2];
                rotateAbout(c,q,a2*u,r1);
                p[mover][0]=c[0];p[mover][1]=c[1];p[pv][0]=r1[0];p[pv][1]=r1[1];
            }
        }
        out[0]=float(p[0][0]);out[1]=float(p[0][1]);out[2]=float(p[1][0]);out[3]=float(p[1][1]);
    }
    Data data(const Slot& a,const Slot& b,double phase) const {
        Data d{{float(width),float(height),float(std::clamp(phase,0.,1.)),float((b.packet.flip?1:0)+2*((b.packet.capture>>14)&3))},{float(smallW),float(smallH),float(blocksW),float(blocksH)},{}};
        if(denseOn) { d.grid[0]=d.grid[2]=float(dense.baseW()); d.grid[1]=d.grid[3]=float(dense.baseH()); }
        if(b.hintCount>0 && a.sequence+1==b.sequence) { d.hintInfo[0]=float(b.hintCount); memcpy(d.hints,b.hints,sizeof(float)*12*b.hintCount); }
        memcpy(d.boxes[0],a.packet.textures+3,16);memcpy(d.boxes[1],a.packet.pulse,16);memcpy(d.boxes[2],b.packet.textures+3,16);memcpy(d.boxes[3],b.packet.pulse,16);
        // Planet world positions and radius (about the sprite plus a little glow, world units), and the three cameras.
        // A teleport (large planet or camera jump), a missing camera or a hidden planet disables the move.
        d.disc[0]=d.disc[1]=0;d.disc[2]=height?float(width)/float(height):1.f;d.disc[3]=0;
        const bool movePlanets=((b.packet.capture>>16)&1)!=0;   // experimental; off keeps the ball exactly as in the true frames
        const Pose& pa=a.packet.pose; const Pose& pb=b.packet.pose;
        if(width && height && pa.camera[2]>.0001f && pb.camera[2]>.0001f) {
            float t=float(std::clamp(phase,0.,1.));
            double turn=angleDifference(pb.camera[3],pa.camera[3]);
            bool cameraOk=hypot(double(pb.camera[0])-pa.camera[0],double(pb.camera[1])-pa.camera[1])<std::max(pa.camera[2],pb.camera[2])*2. && fabs(turn)<.2618 && fabs(pb.camera[2]/pa.camera[2]-1.f)<.35f;
            if(cameraOk) {
                for(int i=0;i<4;i++) {d.cams[0][i]=pa.camera[i];d.cams[1][i]=pb.camera[i];}
                for(int i=0;i<3;i++) d.cams[2][i]=pa.camera[i]+(pb.camera[i]-pa.camera[i])*t;
                d.cams[2][3]=pa.camera[3]+float(turn)*t;
                d.disc[3]=((b.packet.capture>>17)&1)?2.f:1.f; // 2 = draw the planet position markers (diagnostic)
                double oldPos[2][2],newPos[2][2];
                for(int k=0;k<2;k++) {
                    d.planets[k][0]=pa.planet[k][0];d.planets[k][1]=pa.planet[k][1];d.planets[k][2]=pb.planet[k][0];d.planets[k][3]=pb.planet[k][1];
                    oldPos[k][0]=pa.planet[k][0];oldPos[k][1]=pa.planet[k][1];newPos[k][0]=pb.planet[k][0];newPos[k][1]=pb.planet[k][1];
                    float planetScale=.5f*(pa.planet[k][2]+pb.planet[k][2]);
                    float jump=hypotf(pb.planet[k][0]-pa.planet[k][0],pb.planet[k][1]-pa.planet[k][1]);
                    // Sprite radius ~0.22 tile; glow reaches about 2.6x that. A teleport disables the move.
                    // A planet hidden in either picture (no renderer coverage) has no sprite to move.
                    bool seenOld=d.boxes[k][2]>d.boxes[k][0] && d.boxes[k][3]>d.boxes[k][1],seenNew=d.boxes[2+k][2]>d.boxes[2+k][0] && d.boxes[2+k][3]>d.boxes[2+k][1];
                    // Orbit radius ~ planet separation; a planet never moves further than the orbit's diameter between two pictures.
                    double sepOld=hypot(double(pa.planet[1][0])-pa.planet[0][0],double(pa.planet[1][1])-pa.planet[0][1]),sepNew=hypot(double(pb.planet[1][0])-pb.planet[0][0],double(pb.planet[1][1])-pb.planet[0][1]);
                    float reachLimit=float(2.2*std::max(std::max(sepOld,sepNew),.25));
                    if(movePlanets && seenOld && seenNew && planetScale>.05f && planetScale<20.f && jump<reachLimit) d.disc[k]=std::clamp(.3f*planetScale,.08f,1.5f);
                }
                orbit(oldPos,newPos,t,d.tnow);
            }
        }
        return d;
    }
    void prepare(ID3D11Device* device,ID3D11DeviceContext* immediate,const Slot& a,const Slot& b,unsigned w,unsigned h,bool flush=true) {
        initialize(device,w,h,blockFlowVariant(b.packet));
        if(sequence==b.sequence) return;
        denseOn=((b.packet.capture>>18)&1)!=0;
        if(denseOn) dense.initialize(device,w,h);
        auto d=data(a,b,0);bind(d,a,b);
        ID3D11ShaderResourceView* none[8]{};ID3D11UnorderedAccessView* noTarget=nullptr;
        commands->PSSetShaderResources(0,8,none);commands->CSSetShaderResources(4,2,none);
        if(denseOn) {
            dense.run(commands.Get(),a.images[0].view.Get(),b.images[0].view.Get(),b.packet.flip!=0,linear.Get());
            commands->CSSetShaderResources(0,8,none);
            if(flush) execute(immediate);
            sequence=b.sequence;
            return;
        }
        commands->CSSetShader(reduce.Get(),nullptr,0);
        for(int i=0;i<2;i++) {auto view=(i?b:a).images[0].view.Get();commands->CSSetShaderResources(0,1,&view);auto target=gray[i].target.Get();commands->CSSetUnorderedAccessViews(0,1,&target,nullptr);commands->Dispatch((smallW+7)/8,(smallH+7)/8,1);commands->CSSetUnorderedAccessViews(0,1,&noTarget,nullptr);}
        commands->CSSetShader(match.Get(),nullptr,0);
        for(int i=0;i<2;i++) {ID3D11ShaderResourceView* views[]={gray[i].texture.view.Get(),gray[1-i].texture.view.Get()};commands->CSSetShaderResources(6,2,views);auto target=flow[i].target.Get();commands->CSSetUnorderedAccessViews(1,1,&target,nullptr);commands->Dispatch((blocksW+7)/8,(blocksH+7)/8,1);commands->CSSetUnorderedAccessViews(1,1,&noTarget,nullptr);}
        commands->CSSetShaderResources(0,8,none);
        // Research variant4 records matching and the immediately following draw in one list.
        // draw() is mandatory before this protected context scope is left when flush is false.
        if(flush) execute(immediate);
        sequence=b.sequence;
    }
    void draw(ID3D11DeviceContext* immediate,ID3D11RenderTargetView* target,const Slot& a,const Slot& b,double phase,Query* timing,bool measure) {
        if(sequence!=b.sequence) phase=phase<.5?0:1;
        auto d=data(a,b,phase);bind(d,a,b);
        if(timing) {commands->Begin(timing->disjoint.Get());commands->End(timing->begin.Get());}
        D3D11_VIEWPORT vp{0,0,float(width),float(height),0,1};commands->RSSetViewports(1,&vp);commands->OMSetRenderTargets(1,&target,nullptr);commands->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);commands->VSSetShader(vertex.Get(),nullptr,0);commands->PSSetShader(pixel.Get(),nullptr,0);commands->Draw(3,0);commands->OMSetRenderTargets(0,nullptr,nullptr);
        if(timing) {commands->End(timing->end.Get());commands->End(timing->disjoint.Get());}
#ifdef SF_FRAMEGEN_RESEARCH
        if(measure) {auto uav=counterTarget.Get();commands->CSSetUnorderedAccessViews(2,1,&uav,nullptr);commands->CSSetShader(metric.Get(),nullptr,0);commands->Dispatch(1,1,1);ID3D11UnorderedAccessView* empty=nullptr;commands->CSSetUnorderedAccessViews(2,1,&empty,nullptr);}
#else
        (void)measure;
#endif
        execute(immediate);
    }
#ifdef SF_FRAMEGEN_RESEARCH
    void save(ID3D11DeviceContext* immediate,const std::wstring& path) {
        if(!vertex) return; // CPU readback AFTER worker stop only, never in a performance interval.
        immediate->CopyResource(counterRead.Get(),counters.Get());D3D11_MAPPED_SUBRESOURCE mapped{};check(immediate->Map(counterRead.Get(),0,D3D11_MAP_READ,0,&mapped));UINT v[4];memcpy(v,mapped.pData,sizeof(v));immediate->Unmap(counterRead.Get(),0);
        FILE* f=nullptr;_wfopen_s(&f,(path+L"/block-flow.txt").c_str(),L"wb");if(f) {fprintf(f,"variant=%u scale=%u block_size=%u skipped_pairs=%u generated=%u new_picture=%u both_changed_samples=%u moving_samples=%u samples_per_frame=256 threshold=3/255 minimum_samples=4 window=5..45\n",variant,scale,blockSize,skippedPairs,v[0],v[1],v[2],v[3]);fclose(f);}
    }
#endif
};
