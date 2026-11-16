// M4m. Included inside Renderer. Lists contain bindings/draws only: every upload,
// compute dispatch and query used for profiling stays outside reusable lists.
CacheConfiguration cacheConfig{1,128};CachePerformance cachePerf{};CacheDetails cacheDetail{};CacheTune cacheTune{};
ComPtr<ID3D11DeviceContext1> cacheDeferred1;
struct CachePage {ComPtr<ID3D11Buffer> buffer;std::vector<uint8_t> bytes;UINT used=0,category=0;bool dirty=false;};
struct CacheUse {UINT draw,binding;bool batched;};
struct CacheValue {ConstantStorage value;std::vector<uint8_t> bytes,material;std::vector<uint32_t> poolKey;std::vector<CacheUse> uses;Buffer binding{};UINT draw=0,page=0,offset=0,refs=0;bool batched=false,queued=false,privateMaterial=false;};
struct CacheBinding {UINT value=0;};
std::vector<CacheValue> cacheValues;std::map<std::vector<uint32_t>,UINT> cachePool;
std::map<std::pair<UINT,UINT>,UINT> cacheDetached;
std::vector<UINT> cacheValueDirty;
struct CacheDraw {std::array<std::vector<CacheBinding>,2> bindings;std::array<bool,2> pose{};UINT heads=0;int headDelta=0;bool deltaQueued=false,queued=false,active=false,batched=false;};
struct CacheOp {UINT draw=0,first=0,end=0,indices=0,start=0;INT base=0;UINT batched=0,composite=0,live=1,direct=0;uintptr_t black=0,white=0;};
struct CacheCull {float center[4]{},extent[4]{};UINT pose=0,grab=0,end=0,indices=0,start=0;INT base=0;UINT composite=0,pad=0;};
static_assert(sizeof(CacheCull)==64,"M4l cull ABI");
struct CacheChunk {std::vector<CacheOp> ops;ComPtr<ID3D11CommandList> commands;ID3D11RenderTargetView* target=nullptr;ID3D11DepthStencilView* depth=nullptr;D3D11_VIEWPORT viewport{};uint64_t generation=0;};
struct CacheSegment {std::vector<CacheChunk> chunks;std::vector<CacheOp> scratch;std::vector<UINT> indirect;UINT lo=UINT32_MAX,hi=0;bool dirty=true;};
LocalPlanPerformance localPlanPerf{};
static constexpr bool cacheGrabMerge=false; // Claude 2026-11-16: M4n 의 Grab 묶음 복사가 DD VRAM 초과 붕괴를 되살리는지 시험
std::vector<UINT> changedSegments;
std::vector<UINT> positionSegment;
std::vector<CacheChunk> cacheReplacement;
std::vector<std::pair<UINT,UINT>> cacheCullRanges;
std::vector<UINT> cacheHeadChanges;
std::vector<CachePage> cachePages;std::vector<CacheDraw> cacheDraws;
std::vector<UINT> cacheDirty,cacheCamera,cacheTime;std::vector<CacheSegment> cacheSegments;
std::vector<UINT> cacheDirtyPages;ID3D11RenderTargetView* cacheGrabTarget=nullptr;size_t cacheGrabCount=0;
std::unordered_map<UINT,CacheChunk> cacheReuse;bool cachePrepared=false;ID3D11RenderTargetView* cacheLastTarget=nullptr;ID3D11DepthStencilView* cacheLastDepth=nullptr;D3D11_VIEWPORT cacheLastViewport{};std::vector<CacheCull> cacheCullData;std::vector<UINT> cacheCullSlots;bool cachePlanDirty=true,cacheCullDirty=true;
uint64_t cacheGeneration=1,cacheFrame=UINT64_MAX,cacheBounds=UINT64_MAX,cacheCameraEpoch=UINT64_MAX,cacheTimeEpoch=UINT64_MAX;
ComPtr<ID3D11Buffer> cacheCullBuffer,cacheArgs,cacheControl;UINT cacheCullCapacity=0,cacheArgsCapacity=0;
ComPtr<ID3D11Buffer> cacheWorkBuffer;ComPtr<ID3D11ShaderResourceView> cacheWorkView;UINT cacheWorkCapacity=0;
ComPtr<ID3D11ShaderResourceView> cacheCullView;ComPtr<ID3D11UnorderedAccessView> cacheArgsUav;
ComPtr<ID3D11ComputeShader> cacheCullShader;
bool cacheEnabled(){
    if(!cachePerf.available){D3D11_FEATURE_DATA_D3D11_OPTIONS o{};
        if(context1&&SUCCEEDED(deferred.As(&cacheDeferred1))&&SUCCEEDED(device->CheckFeatureSupport(D3D11_FEATURE_D3D11_OPTIONS,&o,sizeof(o)))&&o.ConstantBufferOffsetting)cachePerf.available=1;}
    return cacheConfig.enabled&&cachePerf.available&&submission.immediateAvailable&&gpuBatch&&configuration.submission==0&&configuration.constantRing&&resourceConfiguration.pretransform&&resourceConfiguration.stableUploads&&!drawProbe.config.enabled;
}
void cacheQueueValue(UINT id){auto& v=cacheValues[id];if(!v.queued){v.queued=true;cacheValueDirty.push_back(id);}}
void cacheQueue(UINT id){if(id<cacheDraws.size()&&cacheDraws[id].active&&!cacheDraws[id].queued){cacheDraws[id].queued=true;cacheDirty.push_back(id);}}
void cacheInvalidateDraw(UINT id){
    if(id>=drawPositions.size())return;
    for(auto at:drawPositions[id])if(at<positionSegment.size()){
        auto seg=positionSegment[at];if(seg>=cacheSegments.size())continue;
        for(auto& chunk:cacheSegments[seg].chunks)for(auto& op:chunk.ops)if(op.draw==id){chunk.commands.Reset();break;}
    }
}
UINT cacheNewValue(UINT id,size_t k,bool batched,bool privateMaterial){
    auto& d=*draws[id];const auto& binding=d.buffers[k].binding;CacheValue v;v.draw=id;v.binding=binding;v.batched=batched;v.privateMaterial=privateMaterial;
    v.material=d.buffers[k].material;v.bytes.assign(d.bytes.begin()+binding.offset,d.bytes.begin()+binding.offset+binding.bytes);v.value.uploaded.resize(binding.bytes);
    constantsChanged(v.value,d,binding,batched,id);
    UINT category=(v.value.dependencies&(1|8))?2u:(v.value.dependencies&6)?1u:2u;
    UINT bytes=(binding.bytes+255)&~255u,page=UINT(cachePages.size());
    for(UINT i=0;i<cachePages.size();i++)if(cachePages[i].category==category&&cachePages[i].used+bytes<=cachePages[i].bytes.size()){page=i;break;}
    if(page==cachePages.size()){CachePage fresh;fresh.category=category;fresh.bytes.resize(std::max(bytes,category?4096u:65536u));D3D11_BUFFER_DESC desc{};desc.ByteWidth=UINT(fresh.bytes.size());desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&desc,nullptr,&fresh.buffer));cachePages.push_back(std::move(fresh));memoryDirty=true;}
    v.page=page;v.offset=cachePages[page].used;cachePages[page].used+=bytes;
    UINT result=UINT(cacheValues.size());cacheValues.push_back(std::move(v));
    if(cacheValues[result].value.dependencies&2)cacheCamera.push_back(result);
    if(cacheValues[result].value.dependencies&4)cacheTime.push_back(result);
    cacheQueueValue(result);return result;
}
void cacheSyncBindings(UINT id){
    auto& state=cacheDraws[id];if(!state.active||draws[id]->retired)return;auto& d=*draws[id];auto& bindings=state.bindings[state.batched?1:0];
    for(size_t k=0;k<bindings.size();k++){
        UINT index=bindings[k].value;auto& v=cacheValues[index];
        if(v.material!=d.buffers[k].material){
            cacheDetail.variableEvaluations[0]++;cacheDetail.variableChanges[0]++;
            bool together=true;
            for(const auto& use:v.uses){auto& user=cacheDraws[use.draw];if(!user.active||draws[use.draw]->retired||user.batched!=use.batched||user.bindings[use.batched?1:0][use.binding].value!=index)continue;
                if(draws[use.draw]->buffers[use.binding].material!=d.buffers[k].material){together=false;break;}}
            if(!together){v.refs--;auto key=std::make_pair(index,id);auto found=cacheDetached.find(key);
                if(found==cacheDetached.end()||cacheValues[found->second].material!=d.buffers[k].material){index=cacheNewValue(id,k,state.batched,true);cacheDetached[key]=index;cacheDetail.materialDetaches++;}
                else index=found->second;
                bindings[k].value=index;cacheValues[index].refs++;cacheValues[index].uses.push_back({id,UINT(k),state.batched});cacheInvalidateDraw(id);}
            else {if(!v.poolKey.empty()){cachePool.erase(v.poolKey);v.poolKey.clear();}v.material=d.buffers[k].material;std::memcpy(v.bytes.data(),d.bytes.data()+v.binding.offset,v.binding.bytes);cacheQueueValue(index);}
        }
        auto& current=cacheValues[index];if(!current.value.ready||constantsChanged(current.value,d,current.binding,state.batched,id))cacheQueueValue(index);
    }
}
void cachePacket(const Packet& p){
    if(!cacheConfig.enabled)return;
    if(cacheCameraEpoch!=cameraEpoch){for(auto id:cacheCamera)if(cacheValues[id].refs)cacheQueueValue(id);cacheCameraEpoch=cameraEpoch;cacheCullDirty=true;}
    if(cacheTimeEpoch!=timeEpoch){for(auto id:cacheTime)if(cacheValues[id].refs)cacheQueueValue(id);cacheTimeEpoch=timeEpoch;}
    for(auto id:p.poseIds)for(auto draw:poseDraws[id])if(draw<cacheDraws.size()&&cacheDraws[draw].active){auto& state=cacheDraws[draw];for(auto& b:state.bindings[state.batched?1:0])if(cacheValues[b.value].value.dependencies&1)cacheQueueValue(b.value);}
    for(auto id:p.ids){cacheSyncBindings(id-1);cacheQueue(id-1);}
    cacheCullDirty|=!p.liveIds.empty()||cacheBounds!=boundsEpoch;cacheBounds=boundsEpoch;
}
void cacheInitialize(){
    if(cacheCullShader)return;
    const char code[]=R"(
        struct P {float4x4 world;uint live;uint3 pad;};
        struct S {float4 center,extent;uint pose,grab,end,indices,start;int base;uint composite,pad;};
        StructuredBuffer<S> items:register(t0);StructuredBuffer<P> poses:register(t1);StructuredBuffer<uint> work:register(t2);
        RWByteAddressBuffer args:register(u0);cbuffer C:register(b0){float4 planes[6];uint count;uint3 pad;};
        [numthreads(64,1,1)] void cull(uint3 tid:SV_DispatchThreadID){uint i=tid.x+tid.y*65535*64;if(i>=count)return;uint slot=work[i];S s=items[slot];
            bool any=s.composite!=0,bounded=true;float3 lo=3.402823e38,hi=-3.402823e38;
            for(uint j=slot;j<s.end;j++){S v=items[j];P p=poses[v.pose];if(p.live==0)continue;any=true;
                if(all(v.extent.xyz==0)){bounded=false;continue;}
                precise float3 center=p.world._m00_m10_m20*v.center.x;
                center=center+p.world._m01_m11_m21*v.center.y;center=center+p.world._m02_m12_m22*v.center.z;center=center+p.world._m03_m13_m23;
                precise float3 extent=abs(p.world._m00_m10_m20)*v.extent.x;
                extent=extent+abs(p.world._m01_m11_m21)*v.extent.y;extent=extent+abs(p.world._m02_m12_m22)*v.extent.z;
                lo=min(lo,center-extent);hi=max(hi,center+extent);}
            bool visible=any;if(any&&bounded&&s.composite==0){precise float3 center=(lo+hi)*.5;precise float3 extent=(hi-lo)*.5+max(1,max(abs(lo),abs(hi)))*1e-5;
                for(uint k=0;k<6;k++){precise float distance=planes[k].w;precise float radius=0;
                    for(uint a=0;a<3;a++){distance=distance+planes[k][a]*center[a];radius=radius+abs(planes[k][a])*extent[a];}
                    if(distance+radius<0)visible=false;}}
            args.Store4(s.pad*40,uint4(s.indices,visible?1:0,s.start,asuint(s.base)));args.Store(s.pad*40+16,0);
        }
    )";
    auto compileCache=[&](const char* entry,const char* target){ComPtr<ID3DBlob>b,e;HRESULT hr=D3DCompile(code,sizeof(code)-1,nullptr,nullptr,nullptr,entry,target,D3DCOMPILE_OPTIMIZATION_LEVEL3|D3DCOMPILE_IEEE_STRICTNESS,0,&b,&e);check(hr);return b;};
    auto b=compileCache("cull","cs_5_0");check(device->CreateComputeShader(b->GetBufferPointer(),b->GetBufferSize(),nullptr,&cacheCullShader));
    D3D11_BUFFER_DESC cb{};cb.ByteWidth=112;cb.Usage=D3D11_USAGE_DEFAULT;cb.BindFlags=D3D11_BIND_CONSTANT_BUFFER;check(device->CreateBuffer(&cb,nullptr,&cacheControl));
}
void cacheAllocateBindings(UINT id,bool batched){
    auto& state=cacheDraws[id];auto& d=*draws[id];auto& bindings=state.bindings[batched?1:0];
    if(bindings.empty()&&!d.buffers.empty()){
        bindings.resize(d.buffers.size());
        for(size_t k=0;k<bindings.size();k++){
            const auto& binding=d.buffers[k].binding;ConstantStorage dependencies;constantsChanged(dependencies,d,binding,batched,id);
            if(!(dependencies.dependencies&6)||(dependencies.dependencies&(1|8))){bindings[k].value=cacheNewValue(id,k,batched,true);cacheDetail.privateBindings++;state.pose[batched?1:0]|=(dependencies.dependencies&1)!=0;continue;}
            // Exact material + relative patch layout. Neither shader identity nor
            // stage/slot changes the contents. Mixed $Globals are shared too.
            std::vector<uint32_t> key(binding.bytes/4+4);key[0]=0;key[1]=(dependencies.dependencies&1)?d.pose+1:0;key[2]=(dependencies.dependencies&8)?id+1:0;key[3]=binding.bytes;
            std::memcpy(key.data()+4,d.buffers[k].material.data(),binding.bytes);
            for(const auto& patch:d.patches)if(patch.offset>=binding.offset&&patch.offset<binding.offset+binding.bytes){key.push_back(patch.offset-binding.offset);key.push_back(patch.kind);}
            auto found=cachePool.find(key);UINT value;
            if(found==cachePool.end()){value=cacheNewValue(id,k,batched,false);cacheValues[value].poolKey=key;cachePool.emplace(std::move(key),value);cacheDetail.privateBindings++;}
            else {value=found->second;cacheDetail.sharedBindings++;}
            bindings[k].value=value;state.pose[batched?1:0]|=(dependencies.dependencies&1)!=0;
        }
        for(size_t k=0;k<bindings.size();k++)cacheValues[bindings[k].value].uses.push_back({id,UINT(k),batched});
        cacheQueue(id);
    }
    for(auto& binding:bindings)cacheValues[binding.value].refs++;
    cacheSyncBindings(id);
}
void cacheReleaseHead(const CacheOp& op){
    auto& state=cacheDraws[op.draw];if(!state.heads||--state.heads)return;
    state.active=false;for(auto& b:state.bindings[state.batched?1:0])cacheValues[b.value].refs--;
}
void cacheHeadDelta(UINT id,int delta){auto& s=cacheDraws[id];if(!s.deltaQueued){s.deltaQueued=true;cacheHeadChanges.push_back(id);}s.headDelta+=delta;}
void cacheBuildPlan(){
    const double at=now();bool full=cachePlanDirty||cacheSegments.size()!=segs.size()/2||cacheCullData.size()!=list.size();
    cacheDraws.resize(draws.size());cacheDirty.reserve(draws.size());cacheCamera.reserve(draws.size());cacheTime.reserve(draws.size());
    if(full){
        for(auto& seg:cacheSegments)for(auto& chunk:seg.chunks)for(auto& op:chunk.ops)cacheReleaseHead(op);
        // Keep recorded lists by absolute first position. A boundary-only change
        // (explicit/canvas split moved) leaves most chunks byte-identical.
        cacheReuse.clear();
        for(auto& seg:cacheSegments){for(auto& c:seg.chunks){if(!c.ops.empty()&&c.commands)cacheReuse.emplace(c.ops.front().first,std::move(c));}seg.chunks.clear();}
        cacheSegments.resize(segs.size()/2);cacheCullData.resize(list.size());
        for(auto& seg:cacheSegments){seg.dirty=true;seg.chunks.clear();seg.indirect.clear();seg.lo=UINT32_MAX;seg.hi=0;}
        localPlanPerf.fullPlans++;
    }
    cacheCullSlots.clear();cacheCullRanges.clear();
    for(UINT seg=0;seg<cacheSegments.size();seg++){
        auto& segment=cacheSegments[seg];
        if(!segment.dirty){localPlanPerf.unchangedSegments++;continue;}
        const double begin=now();auto& chunks=segment.chunks;
        UINT first=segs[seg*2],end=first+segs[seg*2+1],chunk=0;
        size_t from=0,to=chunks.size();
        if(!full&&!chunks.empty()){
            auto lo=std::lower_bound(chunks.begin(),chunks.end(),segment.lo,[](const CacheChunk& c,UINT at){return c.ops.back().end<=at;});
            auto hi=std::lower_bound(lo,chunks.end(),segment.hi,[](const CacheChunk& c,UINT at){return c.ops.front().first<at;});
            from=size_t(lo-chunks.begin());to=size_t(hi-chunks.begin());
            if(from==to){segment.dirty=false;continue;}
            first=chunks[from].ops.front().first;end=chunks[to-1].ops.back().end;
            for(size_t k=from;k<to;k++)for(auto& op:chunks[k].ops)cacheHeadDelta(op.draw,-1);
        }
        segment.indirect.erase(std::remove_if(segment.indirect.begin(),segment.indirect.end(),[&](UINT i){return i>=first&&i<end;}),segment.indirect.end());
        cacheReplacement.clear();
        auto& ops=segment.scratch;ops.clear();ops.reserve(cacheConfig.chunkSize);
        auto flush=[&](){if(ops.empty())return;
            CacheChunk c;
            if(from+chunk<to)c=std::move(chunks[from+chunk]);
            else if(full){auto it=cacheReuse.find(ops.front().first);if(it!=cacheReuse.end()){c=std::move(it->second);cacheReuse.erase(it);}}
            if(c.ops.size()!=ops.size()||std::memcmp(c.ops.data(),ops.data(),ops.size()*sizeof(CacheOp))){c.commands.Reset();c.ops=ops;}
            cacheReplacement.push_back(std::move(c));chunk++;ops.clear();};
        for(UINT i=first;i<end;){UINT id=list[i]-1;auto& d=*draws[id];bool batched=gpuBatch&&d.batchable;UINT stop=batched?std::min(end,batchEnds[i]):i+1;
            CacheOp op{};op.draw=id;op.first=i;op.end=stop;op.batched=batched?1u:0u;op.composite=d.composite?1u:0u;
            op.black=reinterpret_cast<uintptr_t>(d.black.Get());op.white=reinterpret_cast<uintptr_t>(d.white.Get());
            if(cacheTune.directSingles&&!batched&&!d.composite){op.direct=1;op.live=live[d.pose]?1u:0u;}
            op.indices=d.composite?3u:batched?orderOffsets[stop]-orderOffsets[i]:d.geometry->indexCount;op.start=batched?orderOffsets[i]:d.composite?0u:d.geometry->indexStart;op.base=batched||d.composite?0:d.geometry->baseVertex;
            if(d.grab||d.composite)flush();ops.push_back(op);if(ops.size()==cacheConfig.chunkSize||d.grab||d.composite)flush();
            if(full){auto& state=cacheDraws[id];bool modeChanged=state.batched!=batched;
                if(state.heads++==0){state.active=true;state.batched=batched;if(!d.composite){cacheAllocateBindings(id,batched);if(modeChanged)cacheQueue(id);}}}
            else cacheHeadDelta(id,1);
            if(!batched||cacheTune.indirectBatches)for(UINT j=i;j<stop;j++){auto& source=*draws[list[j]-1];auto& c=cacheCullData[j];c={};std::memcpy(c.center,source.center,12);std::memcpy(c.extent,source.extent,12);c.pose=source.pose;c.pad=j;}
            auto& c=cacheCullData[i];c.grab=d.grabId;c.end=stop;c.indices=op.indices;c.start=op.start;c.base=op.base;c.composite=op.composite;
            if((!batched||cacheTune.indirectBatches)&&!d.composite&&!op.direct)segment.indirect.push_back(i);
            localPlanPerf.visitedSources+=stop-i;localPlanPerf.visitedOps++;i=stop;
        }flush();
        chunks.erase(chunks.begin()+from,chunks.begin()+to);
        chunks.insert(chunks.begin()+from,std::make_move_iterator(cacheReplacement.begin()),std::make_move_iterator(cacheReplacement.end()));
        cacheReplacement.clear();
        for(auto id:cacheHeadChanges){auto& state=cacheDraws[id];int delta=state.headDelta;state.headDelta=0;state.deltaQueued=false;
            if(delta<0){state.heads-=UINT(-delta);if(!state.heads){state.active=false;for(auto& b:state.bindings[state.batched?1:0])cacheValues[b.value].refs--;}}
            else if(delta>0){bool activate=state.heads==0;state.heads+=UINT(delta);if(activate){state.active=true;state.batched=gpuBatch&&draws[id]->batchable;if(!draws[id]->composite)cacheAllocateBindings(id,state.batched);}}
        }cacheHeadChanges.clear();
        // Direct normalized draws do not use this stream. Preserve the buffer
        // only for unknown singles/indirect controls, with boxed segment updates.
        if(end>first)cacheCullRanges.emplace_back(first,end);
        segment.dirty=false;segment.lo=UINT32_MAX;segment.hi=0;double us=now()-begin;localPlanPerf.segmentUs+=us;if(!full)localPlanPerf.maxSegmentUs=std::max(localPlanPerf.maxSegmentUs,us);localPlanPerf.segmentPlans++;
    }
    // The sparse work directory contains only indirect heads. No binding or
    // constant reconstruction here; untouched segment command lists survive.
    for(auto& seg:cacheSegments)cacheCullSlots.insert(cacheCullSlots.end(),seg.indirect.begin(),seg.indirect.end());
    if(!cacheCullSlots.empty()){
        bool fresh=!cacheCullBuffer||cacheCullCapacity<cacheCullData.size();
        structured(cacheCullBuffer,cacheCullView,cacheCullCapacity,UINT(cacheCullData.size()),sizeof(CacheCull),false);
        if(fresh){D3D11_BOX box{0,0,0,UINT(cacheCullData.size()*sizeof(CacheCull)),1,1};context->UpdateSubresource(cacheCullBuffer.Get(),0,&box,cacheCullData.data(),0,0);}
        else for(auto range:cacheCullRanges){D3D11_BOX box{range.first*UINT(sizeof(CacheCull)),0,0,range.second*UINT(sizeof(CacheCull)),1,1};context->UpdateSubresource(cacheCullBuffer.Get(),0,&box,cacheCullData.data()+range.first,0,0);}
        structured(cacheWorkBuffer,cacheWorkView,cacheWorkCapacity,UINT(cacheCullSlots.size()),sizeof(UINT),false);
        D3D11_BOX box{0,0,0,UINT(cacheCullSlots.size()*sizeof(UINT)),1,1};context->UpdateSubresource(cacheWorkBuffer.Get(),0,&box,cacheCullSlots.data(),0,0);
    }
    if(!cacheCullSlots.empty()&&(!cacheArgs||cacheArgsCapacity<list.size())){
        cacheArgsCapacity=grow(UINT(list.size()));cacheArgs.Reset();cacheArgsUav.Reset();D3D11_BUFFER_DESC b{};b.ByteWidth=cacheArgsCapacity*40;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_UNORDERED_ACCESS;b.MiscFlags=D3D11_RESOURCE_MISC_DRAWINDIRECT_ARGS|D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
        check(device->CreateBuffer(&b,nullptr,&cacheArgs));D3D11_UNORDERED_ACCESS_VIEW_DESC u{};u.Format=DXGI_FORMAT_R32_TYPELESS;u.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;u.Buffer.NumElements=b.ByteWidth/4;u.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;check(device->CreateUnorderedAccessView(cacheArgs.Get(),&u,&cacheArgsUav));cacheGeneration++;memoryDirty=true;
    }
    cacheReuse.clear();changedSegments.clear();cacheCullDirty=true;cachePlanDirty=false;cachePerf.planBuilds++;double elapsed=now()-at;cachePerf.planUs+=elapsed;if(!full)localPlanPerf.maxPlanUs=std::max(localPlanPerf.maxPlanUs,elapsed);
}
void cacheConstants(){
    timeline.nativeStage(5);
    const double at=now();
    for(auto index:cacheValueDirty){auto& v=cacheValues[index];v.queued=false;if(!v.refs)continue;auto& d=*draws[v.draw];
        patchInto(d,v.binding,v.batched,v.bytes.data());cachePerf.constantEvaluations++;
        bool frameOnly=(v.value.dependencies&6)&&!(v.value.dependencies&(1|8));
        if(frameOnly)cacheDetail.frameEvaluations++;else cacheDetail.drawEvaluations++;
        for(const auto& p:d.patches)if(p.offset>=v.binding.offset&&p.offset<v.binding.offset+v.binding.bytes){UINT offset=p.offset-v.binding.offset,n=p.kind<=7?64u:16u;cacheDetail.variableEvaluations[p.kind]++;if(!v.value.ready||std::memcmp(v.value.uploaded.data()+offset,v.bytes.data()+offset,n))cacheDetail.variableChanges[p.kind]++;}
        if(!v.value.ready||std::memcmp(v.value.uploaded.data(),v.bytes.data(),v.binding.bytes)){
            std::memcpy(v.value.uploaded.data(),v.bytes.data(),v.binding.bytes);auto& page=cachePages[v.page];std::memcpy(page.bytes.data()+v.offset,v.bytes.data(),v.binding.bytes);
            if(!page.dirty){page.dirty=true;cacheDirtyPages.push_back(v.page);}v.value.ready=true;
        }rememberConstants(v.value,d,v.draw);
    }cacheValueDirty.clear();
    for(auto id:cacheDirty){auto& state=cacheDraws[id];state.queued=false;if(!state.active||draws[id]->retired)continue;auto& d=*draws[id];
        if(d.color&&!state.batched&&(!d.colorUploaded||std::memcmp(d.uploadedColor.data(),d.bytes.data(),16))){context->UpdateSubresource(d.color.Get(),0,nullptr,d.bytes.data(),0,0);std::memcpy(d.uploadedColor.data(),d.bytes.data(),16);d.colorUploaded=true;uploadPerf.colorBytes+=16;}
    }cacheDirty.clear();
    // Frame and draw pages are separate. Whole DEFAULT updates are ordered
    // against preceding GPU frames; never overwrite an in-flight mapped slice.
    for(auto id:cacheDirtyPages){auto& p=cachePages[id];context->UpdateSubresource(p.buffer.Get(),0,nullptr,p.bytes.data(),0,0);p.dirty=false;cachePerf.constantPages++;cachePerf.constantBytes+=p.bytes.size();uploadPerf.constantBytes+=p.bytes.size();uploadPerf.constantUploads++;if(p.category==1)cacheDetail.frameBytes+=p.bytes.size();else cacheDetail.drawBytes+=p.bytes.size();}cacheDirtyPages.clear();
    cachePerf.constantsUs+=now()-at;
}
void cacheCull(){
    if(!cacheCullDirty||cacheCullSlots.empty()||gpuPoses.empty())return;const double at=now();timeline.nativeStage(4);cacheInitialize();
    struct Control {float planes[6][4];UINT count,pad[3];} control{};std::memcpy(control.planes,frustum,sizeof(frustum));control.count=UINT(cacheCullSlots.size());context->UpdateSubresource(cacheControl.Get(),0,nullptr,&control,0,0);
    ID3D11ShaderResourceView* views[]={cacheCullView.Get(),worldView.Get(),cacheWorkView.Get()};ID3D11UnorderedAccessView* uav=cacheArgsUav.Get();ID3D11Buffer* cb=cacheControl.Get();
    context->CSSetShaderResources(0,3,views);context->CSSetConstantBuffers(0,1,&cb);context->CSSetUnorderedAccessViews(0,1,&uav,nullptr);context->CSSetShader(cacheCullShader.Get(),nullptr,0);
    cacheDetail.cullItems+=cacheCullSlots.size();UINT groups=(UINT(cacheCullSlots.size())+63)/64;context->Dispatch(std::min(groups,65535u),(groups+65534)/65535,1);cachePerf.cullDispatches++;
    uav=nullptr;views[0]=views[1]=views[2]=nullptr;context->CSSetUnorderedAccessViews(0,1,&uav,nullptr);context->CSSetShaderResources(0,3,views);context->CSSetShader(nullptr,nullptr,0);cacheCullDirty=false;cachePerf.cullUs+=now()-at;
}
void cacheGrabs(ID3D11RenderTargetView* rt){
    if(grabSurfaces.empty()||(cacheGrabTarget==rt&&cacheGrabCount==grabSurfaces.size()))return;ComPtr<ID3D11Resource> resource;rt->GetResource(&resource);ComPtr<ID3D11Texture2D> source;check(resource.As(&source));D3D11_TEXTURE2D_DESC td{};source->GetDesc(&td);D3D11_RENDER_TARGET_VIEW_DESC vd{};rt->GetDesc(&vd);require(td.SampleDesc.Count==1&&td.ArraySize==1&&vd.ViewDimension==D3D11_RTV_DIMENSION_TEXTURE2D&&vd.Texture2D.MipSlice==0);
    for(auto& pair:grabSurfaces){auto& g=pair.second;if(g.texture&&g.desc.Width==td.Width&&g.desc.Height==td.Height&&g.desc.Format==td.Format&&g.format==vd.Format)continue;
        g.desc=td;g.format=vd.Format;g.texture.Reset();g.view.Reset();auto desc=td;desc.BindFlags=D3D11_BIND_SHADER_RESOURCE;desc.Usage=D3D11_USAGE_DEFAULT;desc.CPUAccessFlags=desc.MiscFlags=0;check(device->CreateTexture2D(&desc,nullptr,&g.texture));grabPriority(g.texture.Get());D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=vd.Format;sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=desc.MipLevels;check(device->CreateShaderResourceView(g.texture.Get(),&sv,&g.view));cacheGeneration++;memoryDirty=true;}
    cacheGrabTarget=rt;cacheGrabCount=grabSurfaces.size();
}
bool grabVisible(DrawCall& d){
    if(!live[d.pose])return false;
    if(d.extent[0]==0&&d.extent[1]==0&&d.extent[2]==0)return true;
    worldBounds(d);float extent[3];for(UINT a=0;a<3;a++)extent[a]=d.worldExtent[a]+std::max(1.f,std::abs(d.worldCenter[a])+d.worldExtent[a])*1e-5f;
    return !outsideWorld(d.worldCenter,extent);
}
void cacheCopyGrabRegion(DrawCall& d,ID3D11RenderTargetView* target,const D3D11_BOX* box){
    auto& g=grabSurfaces.at(d.grabId);
    if(d.grabId>1&&g.frame==stats.frames){gpuPerf.namedGrabHits++;return;}
    if(resourceConfiguration.skipGrab==1){resourcePerf.grabSkips++;g.frame=stats.frames;return;}
    ComPtr<ID3D11Resource> source;target->GetResource(&source);
    timeline.nativeStage(6);
    if(box)context->CopySubresourceRegion(g.texture.Get(),0,box->left,box->top,0,source.Get(),0,box);
    else context->CopyResource(g.texture.Get(),source.Get());
    UINT pixelBytes=(g.desc.Format==DXGI_FORMAT_R32G32B32A32_FLOAT||g.desc.Format==DXGI_FORMAT_R32G32B32A32_TYPELESS)?16u:(g.desc.Format==DXGI_FORMAT_R16G16B16A16_FLOAT||g.desc.Format==DXGI_FORMAT_R16G16B16A16_TYPELESS||g.desc.Format==DXGI_FORMAT_R16G16B16A16_UNORM)?8u:4u;
    gpuPerf.grabCopies++;gpuPerf.grabBytes+=(box?uint64_t(box->right-box->left)*(box->bottom-box->top):uint64_t(g.desc.Width)*g.desc.Height)*pixelBytes;g.frame=stats.frames;
}
size_t cacheGrabGroup(std::vector<CacheChunk>& chunks,size_t first,ID3D11RenderTargetView* target,const D3D11_VIEWPORT& viewport){
    auto& d=*draws[chunks[first].ops.front().draw];auto& g=grabSurfaces.at(d.grabId);D3D11_BOX box{};size_t end=first+1;
    bool fullViewport=viewport.TopLeftX==0&&viewport.TopLeftY==0&&viewport.Width==g.desc.Width&&viewport.Height==g.desc.Height;
    // A partial mip-0 snapshot cannot preserve sampling from other mip levels.
    bool rectTarget=fullViewport&&g.desc.MipLevels==1;
    if(resourceConfiguration.skipGrab!=1&&d.grabId==1&&!rectTarget)cachePerf.rectFallbacks++;
    bool partial=resourceConfiguration.skipGrab!=1&&d.grabId==1&&rectTarget&&grabRect(d,g.desc.Width,g.desc.Height,box);
    // A later screen-space Grab may share this snapshot ONLY when all prior
    // writes are disjoint from its read rectangle. Each accepted rectangle also
    // contains its raster footprint. Never cross a normal draw, name, segment,
    // unknown program, overlap, or unverified bounds. Limit union byte growth.
    if(partial){uint64_t area=uint64_t(box.right-box.left)*(box.bottom-box.top);
        for(;end<chunks.size()&&end<first+(cacheGrabMerge?16:1);end++){
            auto& next=*draws[chunks[end].ops.front().draw];D3D11_BOX other{};
            if(chunks[end].ops.size()!=1||next.grabId!=1||!next.grab||!next.screenGrab||!grabVisible(next)||!grabRect(next,g.desc.Width,g.desc.Height,other))break;
            if(box.left<other.right&&other.left<box.right&&box.top<other.bottom&&other.top<box.bottom)break;
            D3D11_BOX merged{std::min(box.left,other.left),std::min(box.top,other.top),0,std::max(box.right,other.right),std::max(box.bottom,other.bottom),1};
            uint64_t sum=area+uint64_t(other.right-other.left)*(other.bottom-other.top),combined=uint64_t(merged.right-merged.left)*(merged.bottom-merged.top);
            if(combined*4>sum*5)break;box=merged;area=sum;
        }
    }
    cacheCopyGrabRegion(d,target,partial?&box:nullptr);
    if(end>first+1){cachePerf.grabGroups++;cachePerf.grabMerged+=end-first-1;}
    return end;
}
void cacheRecord(CacheChunk& chunk,ID3D11RenderTargetView* rt,ID3D11DepthStencilView* depth,const D3D11_VIEWPORT& viewport){
    const double at=now();auto* dc=deferred.Get();dc->ClearState();dc->SetPredication(nullptr,FALSE);dc->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    std::array<std::array<bool,128>,2> bound{};std::array<std::array<UINT,14>,2> constantBound;for(auto& stage:constantBound)stage.fill(UINT32_MAX);Shader* lastShader=nullptr;
    auto* target=rt;dc->OMSetRenderTargets(1,&target,depth);dc->RSSetViewports(1,&viewport);
    for(const auto& op:chunk.ops){if(op.direct&&!op.live)continue;auto& d=*draws[op.draw];auto& s=*d.shader;
        if(lastShader!=&s){lastShader=&s;dc->VSSetShader(s.vs.Get(),nullptr,0);dc->PSSetShader(s.ps.Get(),nullptr,0);dc->OMSetBlendState(s.blendState.Get(),nullptr,~0u);dc->OMSetDepthStencilState(s.depth.Get(),s.stencilRef);dc->RSSetState(s.raster.Get());}
        // Lists start at ClearState. Unbind only slots used by earlier draws
        // in THIS list; no 128-slot sweeps or state queries in steady replay.
        for(UINT stage=0;stage<2;stage++)for(UINT slot=0;slot<128;slot++)if(bound[stage][slot]&&!s.textureSlots[stage][slot]){
            ID3D11ShaderResourceView* nil=nullptr;if(stage==0)dc->VSSetShaderResources(slot,1,&nil);else dc->PSSetShaderResources(slot,1,&nil);bound[stage][slot]=false;}

        if(d.composite){dc->IASetInputLayout(nullptr);ID3D11ShaderResourceView* views[]={d.black.Get(),d.white.Get()};dc->PSSetShaderResources(0,2,views);dc->Draw(3,0);continue;}
        auto& g=*d.geometry;dc->IASetInputLayout(op.batched?d.batchLayout.Get():d.layout.Get());
        if(op.batched){auto* buffer=gpuVertices.Get();UINT stride=sizeof(BatchVertex),offset=0;dc->IASetVertexBuffers(0,1,&buffer,&stride,&offset);dc->IASetIndexBuffer(orderBuffer.Get(),DXGI_FORMAT_R32_UINT,0);}
        else {ID3D11Buffer* buffers[8]{};for(UINT j=0;j<8;j++)buffers[j]=g.buffers[j].Get();dc->IASetVertexBuffers(0,8,buffers,g.strides.data(),g.offsets.data());dc->IASetIndexBuffer(g.indices.Get(),g.indexFormat,0);if(d.color){auto* color=d.color.Get();UINT zero=0;dc->IASetVertexBuffers(7,1,&color,&zero,&zero);}}
        auto& bindings=cacheDraws[op.draw].bindings[op.batched?1:0];
        for(size_t k=0;k<bindings.size();k++){auto& b=cacheValues[bindings[k].value];auto& binding=d.buffers[k].binding;if(constantBound[binding.stage][binding.slot]==bindings[k].value)continue;constantBound[binding.stage][binding.slot]=bindings[k].value;auto* buffer=cachePages[b.page].buffer.Get();UINT offset=b.offset/16,length=((binding.bytes+255)&~255u)/16;ID3D11Buffer* nullBuffer=nullptr;
            // Required workaround for software command-list offset emulation.
            if(binding.stage==0){dc->VSSetConstantBuffers(binding.slot,1,&nullBuffer);cacheDeferred1->VSSetConstantBuffers1(binding.slot,1,&buffer,&offset,&length);}
            else {dc->PSSetConstantBuffers(binding.slot,1,&nullBuffer);cacheDeferred1->PSSetConstantBuffers1(binding.slot,1,&buffer,&offset,&length);}}
        for(const auto& t:d.textures){bound[t.stage][t.slot]=true;auto* view=t.grab?grabSurfaces.at(t.grabId).view.Get():t.view.Get();auto* sampler=t.sampler.Get();if(t.stage==0){dc->VSSetShaderResources(UINT(t.slot),1,&view);if(t.samplerSlot>=0)dc->VSSetSamplers(UINT(t.samplerSlot),1,&sampler);}else{dc->PSSetShaderResources(UINT(t.slot),1,&view);if(t.samplerSlot>=0)dc->PSSetSamplers(UINT(t.samplerSlot),1,&sampler);}}
        // GPU world positions keep the original shader/clipper visibility. Only
        // live changes degenerate vertices; camera motion never rebakes geometry.
        if((op.batched&&!cacheTune.indirectBatches)||op.direct)dc->DrawIndexed(op.indices,op.start,op.base);else dc->DrawIndexedInstancedIndirect(cacheArgs.Get(),op.first*40);
    }
    check(dc->FinishCommandList(FALSE,&chunk.commands));bool replayedBefore=chunk.generation!=0;chunk.target=rt;chunk.depth=depth;chunk.viewport=viewport;chunk.generation=cacheGeneration;cachePerf.records++;double elapsed=now()-at;cachePerf.recordUs+=elapsed;if(replayedBefore)localPlanPerf.maxRecordUs=std::max(localPlanPerf.maxRecordUs,elapsed);
}
void cacheApplyPacket(){const double at=now();profile=stats.frames%64==0;if(profile)perf.sampledFrames++;const uint64_t r=read.load(std::memory_order_relaxed);require(r!=write.load(std::memory_order_acquire));auto& packet=ring[r%ring.size()];apply(packet);cachePacket(packet);read.store(r+1,std::memory_order_release);cachePerf.applyUs+=now()-at;}
void cachePrepareFrame(){const double at=now();auto* oldVertices=gpuVertices.Get();auto* oldOrder=orderBuffer.Get();prepareGpu();
    // Singles still need GPU live/pose data for indirect visibility.
    // A Canvas-only list has no pose stream or indirect geometry draws.
    // Never issue an empty UpdateSubresource box for that stream.
    if(!gpuPoses.empty())uploadRanges(gpuWorld,worldView,worldCapacity,poseUpload,poseUploadView,poseUploadCapacity,poseDefaultCurrent,poseDirect,gpuPoses,dirtyPoses,poseMarked,uploadPerf.poseBytes,uploadPerf.poseRanges);
    if(oldVertices!=gpuVertices.Get()||oldOrder!=orderBuffer.Get())cacheGeneration++;
    cachePerf.poseUs+=now()-at;timeline.nativeStage(0);if(cachePlanDirty||!changedSegments.empty())cacheBuildPlan();cacheConstants();cacheCull();timeline.nativeStage(0);cacheFrame=stats.frames;}
// Early event (before the main camera): do all CPU-side preparation and
// re-record stale chunks while the GPU still works on earlier passes, so the
// camera interval only executes lists (no GPU bubble from render-thread CPU).
void prepareCached(){
    if(read.load(std::memory_order_relaxed)==write.load(std::memory_order_acquire))return;
    StateScope state(*this);
    try{
        recording=context.Get();state.begin(true);cacheApplyPacket();cachePrepared=true;
        if(cacheFrame!=stats.frames)cachePrepareFrame();
        if(cacheLastTarget)for(auto& seg:cacheSegments)for(auto& chunk:seg.chunks)
            if(!chunk.commands||chunk.generation!=cacheGeneration||chunk.target!=cacheLastTarget||chunk.depth!=cacheLastDepth)cacheRecord(chunk,cacheLastTarget,cacheLastDepth,cacheLastViewport);
        cachePerf.grabMerged++; // grab merge is disabled (cacheGrabMerge=false): grab_merged now counts early prepares
    }catch(HRESULT hr){stats.errors++;stats.lastHR=hr;cachePlanDirty=true;for(auto& seg:cacheSegments)for(auto& chunk:seg.chunks)chunk.commands.Reset();deferred->ClearState();ComPtr<ID3D11CommandList> discard;deferred->FinishCommandList(FALSE,&discard);}
    catch(...){stats.errors++;stats.lastHR=E_FAIL;cachePlanDirty=true;}
    state.restore();
}
void renderCached(UINT segment){
    const double start=now();StateScope state(*this);Timing* timing=nullptr;bool timelineOpen=false;
    try {
        recording=context.Get();
        if(segment==0){if(cachePrepared)cachePrepared=false;else cacheApplyPacket();}
        require(size_t(segment)*2+1<segs.size());UINT count=segs[segment*2+1];
        if(count==0&&!frame.clearStencil&&segment!=0){timeline.perf.emptySegments++;finishRender(start);return;}
        ComPtr<ID3D11RenderTargetView> target;ComPtr<ID3D11DepthStencilView> depth;context->OMGetRenderTargets(1,&target,&depth);UINT viewports=1;D3D11_VIEWPORT viewport{};context->RSGetViewports(&viewports,&viewport);require(target&&viewports==1);
        if(segment==0&&frame.clearStencil){require(depth!=nullptr);context->ClearDepthStencilView(depth.Get(),D3D11_CLEAR_STENCIL,0,0);}
        state.begin(true);timing=profile?beginGpu():nullptr;if(timing){context->Begin(timing->disjoint.Get());context->End(timing->start.Get());}timeline.mark(4);timelineOpen=true;
        if(cacheFrame!=stats.frames)cachePrepareFrame();
        cacheGrabs(target.Get());cacheLastTarget=target.Get();cacheLastDepth=depth.Get();cacheLastViewport=viewport;
        if(count){require(segment<cacheSegments.size());auto& chunks=cacheSegments[segment].chunks;size_t groupedUntil=0;for(size_t ci=0;ci<chunks.size();ci++){auto& chunk=chunks[ci];
            if(!chunk.commands||chunk.target!=target.Get()||chunk.depth!=depth.Get()||chunk.generation!=cacheGeneration||std::memcmp(&chunk.viewport,&viewport,sizeof(viewport)))cacheRecord(chunk,target.Get(),depth.Get(),viewport);
            const double at=now();// A Grab is a CPU-visible barrier, never a predicated CopyResource.
            // The previous list ended with FALSE, leaving default bindings; no
            // SRVs or render target need unbinding before the copy.
            auto& head=*draws[chunk.ops.front().draw];
            if(head.grab){if(!grabVisible(head)){timeline.perf.culledGrabs++;continue;}
                timeline.perf.visibleGrabs++;if(ci>=groupedUntil)groupedUntil=cacheGrabGroup(chunks,ci,target.Get(),viewport);}
            timeline.nativeStage(7);context->ExecuteCommandList(chunk.commands.Get(),FALSE);cachePerf.executeUs+=now()-at;cachePerf.replays++;for(const auto& op:chunk.ops)if((op.batched&&!cacheTune.indirectBatches)||op.composite||op.direct)cacheDetail.directDraws++;else {cachePerf.indirectDraws++;cacheDetail.indirectDraws++;}stats.draws+=chunk.ops.size();}}
        cachePerf.segments++;if(timing)context->End(timing->end.Get());timeline.mark(7);timelineOpen=false;endGpu(timing);timing=nullptr;
    }catch(HRESULT hr){stats.errors++;stats.lastHR=hr;cachePlanDirty=true;for(auto& seg:cacheSegments)for(auto& chunk:seg.chunks)chunk.commands.Reset();deferred->ClearState();ComPtr<ID3D11CommandList> discard;deferred->FinishCommandList(FALSE,&discard);}
    catch(...){stats.errors++;stats.lastHR=E_FAIL;cachePlanDirty=true;}
    if(timelineOpen)timeline.mark(7);if(timing)endGpu(timing);state.restore();finishRender(start);
}
