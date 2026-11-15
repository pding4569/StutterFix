// Render-thread-only timestamp timeline. No Flush, spin, wait or GPU busy claim.
// IDs: 0/1 main begin/end, 2/3 aux begin/end, 4..7 category,
// 16..21 phase. Native intervals are inserted at ExecuteCommandList, after recording.
struct GpuTimeline {
    static constexpr unsigned limit=512;
    struct Slot {
        ComPtr<ID3D11Query> disjoint;
        std::array<ComPtr<ID3D11Query>,limit> points;
        std::array<unsigned,limit> categories{},phases{};
        unsigned count=0;bool pending=false,aux=false,valid=true;
        uint64_t usage=0,budget=0,nonLocal=0,serial=0;
    };
    std::array<Slot,4> slots;
    Slot* active=nullptr;size_t next=0;uint64_t mainSerial=0,auxSerial=0;
    ID3D11DeviceContext* context=nullptr;
    drfull::TimelinePerformance perf{};
    std::array<double,4> categorySum{};std::array<double,6> phaseSum{};
    double cameraSum=0,auxSum=0;unsigned category=3,phase=0;
    void init(ID3D11Device* device,ID3D11DeviceContext* ctx) {
        context=ctx;
        for(auto& s:slots){
            D3D11_QUERY_DESC q{D3D11_QUERY_TIMESTAMP_DISJOINT,0};check(device->CreateQuery(&q,&s.disjoint));q.Query=D3D11_QUERY_TIMESTAMP;
            for(auto& p:s.points)check(device->CreateQuery(&q,&p));
        }
    }
    void poll() {
        perf.pending=0;
        for(auto& s:slots)if(s.pending){
            D3D11_QUERY_DATA_TIMESTAMP_DISJOINT d{};std::array<UINT64,limit> stamps{};
            bool ready=context->GetData(s.disjoint.Get(),&d,sizeof(d),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK;
            if(ready)for(unsigned i=0;i<s.count;i++)if(context->GetData(s.points[i].Get(),&stamps[i],sizeof(UINT64),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK){ready=false;break;}
            if(!ready){perf.pending++;continue;}s.pending=false;
            if(!s.valid){perf.invalid++;continue;}
            bool ordered=true;for(unsigned i=1;i<s.count;i++)ordered&=stamps[i]>=stamps[i-1];
            if(d.Disjoint||!d.Frequency||!ordered){perf.disjoint++;continue;}
            const double scale=1e6/double(d.Frequency),total=double(stamps[s.count-1]-stamps[0])*scale;
            if(s.aux){auxSum+=total;perf.auxiliarySamples++;perf.auxiliaryUs=auxSum/double(perf.auxiliarySamples);continue;}
            cameraSum+=total;perf.samples++;perf.intervals+=s.count-1;
            perf.lastCameraUs=total;perf.lastUsage=s.usage;perf.lastBudget=s.budget;perf.lastNonLocal=s.nonLocal;perf.lastSerial=s.serial;
            if(total>=perf.maxCameraUs){perf.maxCameraUs=total;perf.maxUsage=s.usage;perf.maxBudget=s.budget;perf.maxNonLocal=s.nonLocal;perf.maxSerial=s.serial;}
            for(unsigned i=1;i<s.count;i++){double us=double(stamps[i]-stamps[i-1])*scale;categorySum[s.categories[i-1]]+=us;phaseSum[s.phases[i-1]]+=us;}
            const double n=double(perf.samples);perf.cameraUs=cameraSum/n;
            perf.nativeUs=categorySum[0]/n;perf.explicitUs=categorySum[1]/n;perf.compositeUs=categorySum[2]/n;perf.otherUs=categorySum[3]/n;
            perf.opaqueUs=phaseSum[0]/n;perf.gapUs=phaseSum[1]/n;perf.alphaUs=phaseSum[2]/n;perf.postAlphaUs=phaseSum[3]/n;perf.effectsUs=phaseSum[4]/n;perf.tailUs=phaseSum[5]/n;
        }
    }
    void stamp() {
        if(!active)return;
        if(active->count==limit){active->valid=false;perf.overflow++;return;}
        unsigned i=active->count++;active->categories[i]=category;active->phases[i]=phase;context->End(active->points[i].Get());
    }
    void close(bool expectedAux) {
        if(!active)return;
        if(active->aux!=expectedAux)active->valid=false;
        stamp();context->End(active->disjoint.Get());active->pending=true;active=nullptr;
    }
    void mark(unsigned id,uint64_t usage=0,uint64_t budget=0,uint64_t nonLocal=0) {
        if(!context)return;
        if(id==0||id==2){
            if(active){active->valid=false;close(active->aux);}
            poll();bool aux=id==2;auto& serial=aux?auxSerial:mainSerial;
            if(serial++%64)return;
            auto& s=slots[next];if(s.pending){perf.skipped++;return;}next=(next+1)%slots.size();
            s.count=0;s.aux=aux;s.valid=true;s.usage=usage;s.budget=budget;s.nonLocal=nonLocal;s.serial=serial;
            active=&s;category=3;phase=0;context->Begin(s.disjoint.Get());stamp();
        }else if(id==1||id==3)close(id==3);
        else if(active){
            if(id>=4&&id<=7){stamp();category=id-4;active->categories[active->count-1]=category;}
            else if(id>=16&&id<=21){stamp();phase=id-16;active->phases[active->count-1]=phase;}
            else {active->valid=false;}
        }
    }
};
