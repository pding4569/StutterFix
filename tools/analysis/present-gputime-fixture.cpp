// Actual Tracker code with a deterministic non-GPU backend. Never runs a game.
#include <windows.h>
#include <dxgi.h>
#include <wrl/client.h>
#include <array>
#include <atomic>
#include <algorithm>
#include <cassert>
#include <cstdio>
#include "../../native/sfnative/present_gputime.h"
struct Fake {
    struct Query {D3D11_QUERY type=D3D11_QUERY_EVENT;UINT64 value=0;};
    int creates=0,failCreate=0,begins=0,ends=0,gets=0;
    bool blocked=false,disjoint=false,eventPending=false,failGet=false,resetDone=false;
    UINT64 tick=0,frequency=1000000;
    bool initialize(IDXGISwapChain*) {return true;}
    bool create(Query& q,D3D11_QUERY type) {q.type=type;return ++creates!=failCreate;}
    void begin(Query&) {++begins;}
    void end(Query& q) {++ends;if(q.type==D3D11_QUERY_TIMESTAMP)q.value=(tick+=1000);}
    HRESULT get(Query& q,void* data,UINT) {
        ++gets;
        if(failGet)return E_FAIL;
        if(q.type==D3D11_QUERY_EVENT) {*static_cast<BOOL*>(data)=!eventPending;return eventPending?S_FALSE:S_OK;}
        if(blocked)return S_FALSE;
        if(q.type==D3D11_QUERY_TIMESTAMP_DISJOINT)
            *static_cast<D3D11_QUERY_DATA_TIMESTAMP_DISJOINT*>(data)={frequency,disjoint};
        else *static_cast<UINT64*>(data)=q.value;
        return S_OK;
    }
    void reset() {resetDone=true;}
};
using Tracker=sf_ppgpu::Tracker<Fake>;
static void frame(Tracker& t) {t.begin();auto sampled=t.beforePresent();t.afterPresent(sampled,2800);}
int main() {
    static Tracker t;
    assert(t.initialize(nullptr) && t.api.creates==144);
    t.begin();t.begin();assert(t.api.begins==1);
    t.afterPresent(t.beforePresent(),2800);
    frame(t);
    assert(t.render.count==2 && t.render.mean()==1000 && t.after.mean()==1000);
    assert(t.interval.count==1 && t.interval.mean()==3000 && t.checked==2 && t.unobserved==0);
    t.api.eventPending=true;frame(t);frame(t);assert(t.unobserved==2 && t.checked==4);
    t.api.disjoint=true;frame(t);frame(t);assert(t.disjoints==1 && t.render.count==4);
    t.api.disjoint=false;frame(t);frame(t);assert(t.interval.count==3); // cannot bridge invalid epoch
    frame(t);
    t.afterPresent(t.beforePresent(),2800);assert(t.missing==1);
    frame(t);assert(t.interval.count==3); // cannot bridge a missing camera event
    t.api.blocked=true;
    for(int i=0;i<32;++i)frame(t);
    assert(t.queued==16);
    const auto ends=t.api.ends;
    for(int i=0;i<8;++i)frame(t);
    assert(t.drops==8 && t.queued==16 && t.api.ends==ends); // no reuse or wait
    t.api.blocked=false;t.collect();assert(t.queued==0);
    t.api.failGet=true;frame(t);frame(t);assert(t.failures>0);
    t.api.failGet=false;t.begin();t.stop();
    assert(!t.ready && t.active==-1 && t.queued==0 && t.api.resetDone && t.discarded==1);
    const auto oldBegins=t.api.begins;t.begin();assert(t.api.begins==oldBegins);
    static std::array<std::atomic<long long>,61> stats{};t.publish(stats);
    assert(stats[53]>0 && stats[57]==1 && stats[60]==0);
    static Tracker failed;failed.api.failCreate=7;
    assert(!failed.initialize(nullptr) && failed.failures==1 && failed.api.resetDone && !failed.ready);
    sf_ppgpu::Distribution d;d.add(1);d.add(9);d.add(70000);
    assert(d.percentile(50)==16 && d.percentile(95)==70000);
    std::puts("PASS: 16 pair slots / 32 frames saturation, same-disjoint b->b, no overwrite/wait, ordered retirement, dedup, missing/disjoint exclusion, EVENT pending, query failure, partial-create cleanup, stop-active cleanup, histogram overflow, stats");
}
