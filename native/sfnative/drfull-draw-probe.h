// Opt-in GPU intervals, bounded windows, no Flush or blocking GetData.
// Includes draw setup/Grab; excludes segment prepareGpu/prepareConstants.
struct DrawGpuProbe {
    static constexpr UINT limit=256,history=4096;
    struct Slot {
        ComPtr<ID3D11Query> disjoint,start,end;
        std::array<ComPtr<ID3D11Query>,limit*2> points;
        std::array<DrawProbeRow,limit> rows{};
        UINT count=0;bool pending=false,valid=true;
    };
    std::array<Slot,4> slots;Slot* active=nullptr;UINT next=0,ordinal=0,write=0,total=0;
    std::array<DrawProbeRow,history> rows{};
    std::array<DrawProbeRow,history> snapshot{};UINT snapshotTotal=0;
    DrawProbeConfiguration config{};uint64_t pending=0,skipped=0,invalid=0,overflow=0;
    void init(ID3D11Device* device){
        for(auto& s:slots){D3D11_QUERY_DESC q{D3D11_QUERY_TIMESTAMP_DISJOINT,0};check(device->CreateQuery(&q,&s.disjoint));q.Query=D3D11_QUERY_TIMESTAMP;
            check(device->CreateQuery(&q,&s.start));check(device->CreateQuery(&q,&s.end));for(auto& p:s.points)check(device->CreateQuery(&q,&p));}
    }
    void poll(ID3D11DeviceContext* dc){
        pending=0;for(auto& s:slots)if(s.pending){
            D3D11_QUERY_DATA_TIMESTAMP_DISJOINT d{};UINT64 begin=0,end=0;std::array<UINT64,limit*2> times{};
            bool ready=dc->GetData(s.disjoint.Get(),&d,sizeof(d),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK;
            if(ready&&!s.valid){s.pending=false;invalid++;continue;}
            if(ready)ready=dc->GetData(s.start.Get(),&begin,sizeof(begin),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK&&dc->GetData(s.end.Get(),&end,sizeof(end),D3D11_ASYNC_GETDATA_DONOTFLUSH)==S_OK;
            if(ready)for(UINT i=0;i<s.count*2;i++)if(dc->GetData(s.points[i].Get(),&times[i],sizeof(UINT64),D3D11_ASYNC_GETDATA_DONOTFLUSH)!=S_OK){ready=false;break;}
            if(!ready){pending++;continue;}s.pending=false;
            bool ordered=end>=begin;UINT64 previous=begin;for(UINT i=0;i<s.count*2;i++){ordered&=times[i]>=previous&&times[i]<=end;previous=times[i];}
            if(!s.valid||d.Disjoint||!d.Frequency||!ordered){invalid++;continue;}
            double scale=1e6/double(d.Frequency);for(UINT i=0;i<s.count;i++){
                auto r=s.rows[i];r.us=double(times[i*2+1]-times[i*2])*scale;r.segmentUs=double(end-begin)*scale;
                rows[write]=r;write=(write+1)%history;if(total<history)total++;else overflow++;
            }
        }
    }
    void begin(ID3D11DeviceContext* immediate,ID3D11DeviceContext* dc,uint64_t frame,UINT segment){
        if(!config.enabled)return;poll(immediate);if(segment==0)ordinal=0;
        if(frame%config.interval)return;
        auto& s=slots[next];if(s.pending){skipped++;return;}next=(next+1)%UINT(slots.size());
        s.count=0;s.valid=true;active=&s;immediate->Begin(s.disjoint.Get());dc->End(s.start.Get());
    }
    bool before(ID3D11DeviceContext* dc,DrawProbeRow row){
        row.ordinal=ordinal++;if(!active||row.ordinal<config.first||row.ordinal-config.first>=config.count)return false;
        UINT i=active->count;require(i<limit);active->rows[i]=row;dc->End(active->points[i*2].Get());return true;
    }
    void after(ID3D11DeviceContext* dc,bool measured){if(measured){dc->End(active->points[active->count*2+1].Get());active->count++;}}
    void finish(ID3D11DeviceContext* immediate,ID3D11DeviceContext* dc,bool valid){
        if(!active)return;active->valid=valid;dc->End(active->end.Get());
        // Deferred end timestamp must execute before closing the disjoint scope.
        if(!valid){immediate->End(active->disjoint.Get());active->pending=true;active=nullptr;}
    }
    void submitted(ID3D11DeviceContext* dc){if(active){dc->End(active->disjoint.Get());active->pending=true;active=nullptr;}}
    void read(DrawProbeReadback& q){
        if(q.index==UINT32_MAX){snapshotTotal=total;for(UINT i=0;i<total;i++)snapshot[i]=rows[(write+history-total+i)%history];}
        q.total=snapshotTotal;q.pending=pending;q.skipped=skipped;q.invalid=invalid;q.overflow=overflow;
        if(q.index<snapshotTotal)q.row=snapshot[q.index];
    }
};
