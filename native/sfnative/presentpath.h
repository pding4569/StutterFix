// Dormant diagnostic exports. Only the AUTOTEST managed command calls these.
// All COM operations and vptr changes run on Unity's render thread.
namespace sf_presentpath {
using Fn = HRESULT (STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT);
static ComPtr<IDXGISwapChain> chain;
static ComPtr<IDXGISwapChain2> chain2;
static ComPtr<IDXGIDevice1> device1;
static void** before = nullptr;
static std::array<void*,41> table{};
static Fn next = nullptr;
static UINT savedLatency = 0;
static bool changedLatency = false;
static std::atomic<int> mode{0}, status{0};
// status: 0 off, 1 installed, -1 rejected, -2 restore failure, -3 overlay owns vptr.
static std::array<std::atomic<long long>,20> stats{};
static void describe() {
    if(!chain) return;
    DXGI_SWAP_CHAIN_DESC d{};
    HRESULT descHR=chain->GetDesc(&d);
    if(FAILED(descHR)) {stats[17]=descHR;stats[19]=0;++stats[18];return;}
    stats[0]=d.BufferDesc.Width;stats[1]=d.BufferDesc.Height;
    stats[2]=d.BufferCount;stats[3]=d.SwapEffect;stats[4]=d.Flags;stats[5]=d.Windowed;
    ComPtr<IDXGISwapChain1> one;
    DXGI_SWAP_CHAIN_DESC1 d1{};
    HRESULT hr=chain.As(&one);
    if(SUCCEEDED(hr)) hr=one->GetDesc1(&d1);
    stats[17]=hr;
    if(SUCCEEDED(hr)) {stats[0]=d1.Width;stats[1]=d1.Height;stats[2]=d1.BufferCount;stats[3]=d1.SwapEffect;stats[4]=d1.Flags;}
    UINT v=0; hr=chain2 ? chain2->GetMaximumFrameLatency(&v) : device1 ? device1->GetMaximumFrameLatency(&v) : E_NOINTERFACE;
    stats[6]=SUCCEEDED(hr)?v:-1;stats[16]=chain2?2:device1?1:0;
    stats[19]=*reinterpret_cast<void***>(chain.Get())==table.data()?1:0;
    ++stats[18]; // release/seq_cst ACK for managed assertion refresh
}
static HRESULT STDMETHODCALLTYPE present(IDXGISwapChain* self,UINT sync,UINT flags) {
    UINT output=flags;
    if(mode==2 && sync==0 && !(flags & (DXGI_PRESENT_TEST|DXGI_PRESENT_DO_NOT_WAIT))) {
        DXGI_SWAP_CHAIN_DESC d{}; BOOL full=TRUE;
        if(SUCCEEDED(self->GetDesc(&d)) && SUCCEEDED(self->GetFullscreenState(&full,nullptr)) && !full &&
           (d.Flags & DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING) &&
           (d.SwapEffect==DXGI_SWAP_EFFECT_FLIP_DISCARD || d.SwapEffect==DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL))
            output |= DXGI_PRESENT_ALLOW_TEARING;
    }
    LARGE_INTEGER a{},b{},f{};QueryPerformanceCounter(&a);
    HRESULT hr=next(self,sync,output);
    QueryPerformanceCounter(&b);QueryPerformanceFrequency(&f);
    ++stats[7];stats[8]=sync;stats[9]=flags;stats[10]=output;stats[11]=hr;
    stats[12]=(b.QuadPart-a.QuadPart)*1000000/f.QuadPart;stats[13]+=stats[12].load();
    if(output!=flags) ++stats[14];
    if(FAILED(hr)) ++stats[15];
    return hr;
}
static void stop() {
    mode=0;
    if(!chain) {status=0;return;}
    bool ok=true;
    if(changedLatency) {
        HRESULT hr=chain2?chain2->SetMaximumFrameLatency(savedLatency):device1->SetMaximumFrameLatency(savedLatency);
        UINT checkValue=0;
        if(SUCCEEDED(hr)) hr=chain2?chain2->GetMaximumFrameLatency(&checkValue):device1->GetMaximumFrameLatency(&checkValue);
        ok=SUCCEEDED(hr) && checkValue==savedLatency;
        if(ok) changedLatency=false;
    }
    auto cell=reinterpret_cast<void**>(chain.Get());
    if(*cell!=table.data()) {status=-3;describe();return;} // overlay chain retained; pass-through forever
    InterlockedCompareExchangePointer(cell,before,table.data());
    if(*cell!=before || !ok) {status=-2;return;}
    describe();chain.Reset();chain2.Reset();device1.Reset();before=nullptr;status=0;
}
static void __stdcall event(int id,void* ptr) {
    ComPtr<IDXGISwapChain> pinned;
    if((id>=1 && id<=3) || id==5) pinned.Attach(static_cast<IDXGISwapChain*>(ptr));
    if(id==0) {stop();return;}
    if(id==4) {describe();return;}
    if(id==5) {
        if(chain) {describe();return;}
        if(!pinned) return;
        chain=pinned;
        DXGI_SWAP_CHAIN_DESC d{};
        if(SUCCEEDED(chain->GetDesc(&d))) {
            if(d.Flags & DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT) chain.As(&chain2);
            if(!chain2) chain->GetDevice(IID_PPV_ARGS(&device1));
        }
        describe();chain.Reset();chain2.Reset();device1.Reset();return;
    }
    if(chain || id<1 || id>3 || !ptr) {status=-1;return;}
    auto* sc=static_cast<IDXGISwapChain*>(ptr);
    DXGI_SWAP_CHAIN_DESC desc{};
    if(FAILED(sc->GetDesc(&desc))) {status=-1;return;}
    for(auto& s:stats) s=0;
    chain=sc;
    ComPtr<IDXGISwapChain4> four;ComPtr<IDXGISwapChain3> three;ComPtr<IDXGISwapChain1> one;
    unsigned size=18;
    if(SUCCEEDED(sc->QueryInterface(IID_PPV_ARGS(&four))) && four.Get()==sc) size=41;
    else if(SUCCEEDED(sc->QueryInterface(IID_PPV_ARGS(&three))) && three.Get()==sc) size=40;
    else if(SUCCEEDED(sc->QueryInterface(IID_PPV_ARGS(&chain2))) && chain2.Get()==sc) size=36;
    else if(SUCCEEDED(sc->QueryInterface(IID_PPV_ARGS(&one))) && one.Get()==sc) size=29;
    chain2.Reset();
    if(desc.Flags & DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT) sc->QueryInterface(IID_PPV_ARGS(&chain2));
    if(!chain2) sc->GetDevice(IID_PPV_ARGS(&device1));
    describe();
    if(id==2 && (!(desc.Flags & DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING) || !desc.Windowed)) {
        chain.Reset();chain2.Reset();device1.Reset();status=-1;return; // cannot retrofit creation flags
    }
    if(id==3) {
        UINT v=0;
        HRESULT hr=chain2?chain2->GetMaximumFrameLatency(&v):device1?device1->GetMaximumFrameLatency(&v):E_NOINTERFACE;
        if(FAILED(hr) || v==4) {chain.Reset();chain2.Reset();device1.Reset();status=-1;return;}
        savedLatency=v;
        hr=chain2?chain2->SetMaximumFrameLatency(4):device1->SetMaximumFrameLatency(4);
        if(FAILED(hr)) {chain.Reset();chain2.Reset();device1.Reset();status=-1;return;}
        changedLatency=true;
    }
    before=*reinterpret_cast<void***>(sc);
    std::copy_n(before,size,table.begin());next=reinterpret_cast<Fn>(before[8]);
    table[8]=reinterpret_cast<void*>(present);
    mode=id; // 1 observe, 2 tearing, 3 latency4
    if(InterlockedCompareExchangePointer(reinterpret_cast<void**>(sc),table.data(),before)!=before) {
        // No hook was installed. Temporarily point stop at the table only if ours; retain state on conflict.
        status=-3;mode=0;return;
    }
    describe();status=1;
}
}
API void* sf_presentpath_event_ptr() {return reinterpret_cast<void*>(sf_presentpath::event);}
API void sf_presentpath_pin(void* ptr) {if(ptr) static_cast<IDXGISwapChain*>(ptr)->AddRef();}
API void sf_presentpath_disable() {sf_presentpath::mode=0;}
API void sf_presentpath_pending() {sf_presentpath::status=2;}
API long long sf_presentpath_stat(int i) {
    if(i==-1) return sf_presentpath::status;
    if(i==-2) return sf_presentpath::mode;
    return i>=0 && i<20?sf_presentpath::stats[i].load():-1;
}
