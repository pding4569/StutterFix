// Logical allocation bytes, deduplicated across draws/views. Driver residency,
// tiling/alignment, query/state objects and external Unity assets are separate.
struct GpuMemory {
    ComPtr<IDXGIAdapter3> adapter;drfull::MemoryPerformance perf{};
    struct Resource {ID3D11Resource* pointer;uint64_t bytes;unsigned category;};
    std::vector<Resource> resources;
    bool prepared=false;
    void init(ID3D11Device* device) {
        resources.reserve(8192);
        ComPtr<IDXGIDevice> dxgi;ComPtr<IDXGIAdapter> base;
        if(SUCCEEDED(device->QueryInterface(IID_PPV_ARGS(&dxgi)))&&SUCCEEDED(dxgi->GetAdapter(&base)))base.As(&adapter);
        sample();perf.beforeUsage=perf.currentUsage;perf.beforeBudget=perf.currentBudget;
    }
    void sample() {
        if(!adapter){perf.failures++;return;}
        DXGI_QUERY_VIDEO_MEMORY_INFO local{},nonLocal{};
        if(FAILED(adapter->QueryVideoMemoryInfo(0,DXGI_MEMORY_SEGMENT_GROUP_LOCAL,&local))){perf.failures++;return;}
        perf.supported=1;perf.queries++;perf.currentUsage=local.CurrentUsage;perf.currentBudget=local.Budget;perf.peakUsage=std::max(perf.peakUsage,local.CurrentUsage);
        if(local.CurrentUsage>local.Budget){perf.overBudgetSamples++;perf.maxOverBudgetBytes=std::max(perf.maxOverBudgetBytes,local.CurrentUsage-local.Budget);}
        perf.minHeadroomBytes=std::min(perf.queries==1?UINT64_MAX:perf.minHeadroomBytes,local.Budget>local.CurrentUsage?local.Budget-local.CurrentUsage:0);
        if(SUCCEEDED(adapter->QueryVideoMemoryInfo(0,DXGI_MEMORY_SEGMENT_GROUP_NON_LOCAL,&nonLocal))){perf.nonLocalUsage=nonLocal.CurrentUsage;perf.nonLocalBudget=nonLocal.Budget;}
    }
    void begin() {resources.clear();}
    void buffer(ID3D11Buffer* b,unsigned category){if(b){D3D11_BUFFER_DESC d{};b->GetDesc(&d);resources.push_back({b,d.ByteWidth,category});}}
    void texture(ID3D11Texture2D* t,unsigned category){
        if(!t)return;D3D11_TEXTURE2D_DESC d{};t->GetDesc(&d);uint64_t pixel=d.Format==DXGI_FORMAT_R32G32B32A32_TYPELESS||d.Format==DXGI_FORMAT_R32G32B32A32_FLOAT?16u:d.Format==DXGI_FORMAT_R16G16B16A16_FLOAT||d.Format==DXGI_FORMAT_R16G16B16A16_TYPELESS||d.Format==DXGI_FORMAT_R16G16B16A16_UNORM?8u:4u;
        uint64_t bytes=0;UINT w=d.Width,h=d.Height;for(UINT i=0;i<d.MipLevels;i++){bytes+=uint64_t(w)*h*pixel;w=std::max(1u,w/2);h=std::max(1u,h/2);}resources.push_back({t,bytes*d.ArraySize*d.SampleDesc.Count,category});
    }
    void view(ID3D11ShaderResourceView* v,unsigned category){if(v){ComPtr<ID3D11Resource> r;ComPtr<ID3D11Texture2D> t;v->GetResource(&r);if(SUCCEEDED(r.As(&t)))texture(t.Get(),category);}}
    void finish() {
        std::sort(resources.begin(),resources.end(),[](const Resource& a,const Resource& b){return std::less<ID3D11Resource*>{}(a.pointer,b.pointer);});
        std::array<uint64_t,9> sums{};ID3D11Resource* previous=nullptr;
        for(const auto& r:resources)if(r.pointer!=previous){sums[r.category]+=r.bytes;previous=r.pointer;}
        perf.geometryBytes=sums[0];perf.poseBytes=sums[1];perf.drawBytes=sums[2];perf.indexBytes=sums[3];perf.pretransformBytes=sums[4];perf.constantBytes=sums[5];perf.colorBytes=sums[6];perf.grabBytes=sums[7];perf.canvasColorBytes=sums[8];
        perf.ownedBytes=0;for(auto n:sums)perf.ownedBytes+=n;perf.peakOwnedBytes=std::max(perf.peakOwnedBytes,perf.ownedBytes);
        sample();if(!prepared){prepared=true;perf.preparedUsage=perf.currentUsage;perf.preparedBudget=perf.currentBudget;}
    }
};
