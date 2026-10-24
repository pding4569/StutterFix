// SPDX-License-Identifier: MIT. Standalone hardware D3D11 masked copy oracle.
#define main unused_copy_fixture_main
#include "native-copy-check.cpp"
#undef main
int main(int argc,char** argv) {
    try {
        require(argc==2 || (argc==3 && strcmp(argv[2],"--hardware")==0),"native DLL path [--hardware] required");NativeApi api(argv[1],true);
        bool hardware=argc==3;
        auto create=api.load<decltype(api.create)>(hardware?"sf_deco_copy_create_masked":"sf_deco_copy_create8masked");
        auto submit=api.load<int (__cdecl*)(int,uint64_t,uint64_t)>("sf_deco_copy_submit_mask");
        auto callback=reinterpret_cast<void (CALLBACK*)(int)>(api.event());
        ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> context;D3D_FEATURE_LEVEL level=D3D_FEATURE_LEVEL_11_0,actual{};
        check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,&level,1,D3D11_SDK_VERSION,&device,&actual,&context));
        for(UINT count:{1u,513u,1715u}) {
            Fixture f(device.Get(),context.Get(),count);std::vector<void*> pointers;std::vector<unsigned> offsets;
            // Reverse target/offset PAIRS: visible chunk order differs from draw
            // order but each original destination still gets its own vertices.
            for(UINT i=0;i<count;i++){UINT item=count-1-i;pointers.push_back(f.destinations[item].Get());offsets.push_back(item*Fixture::bytes);}
            int id=create(f.source.Get(),pointers.data(),offsets.data(),static_cast<int>(count),Fixture::bytes);require(id>0,"masked create failed");
            require(api.submit(id)==0,"unmasked submit accepted by masked context");
            require(submit(id,~0ULL,~0ULL)==1,"initial submit failed");f.verify(nullptr,42,callback,id,true);
            auto retained=f.expected;
            for(unsigned iteration=0;iteration<140;iteration++) {
                uint64_t mask=iteration%3==0?0:iteration%3==1?2:~0ULL;uint32_t salt=1000+iteration;
                auto wanted=retained;
                for(UINT i=0;i<count;i++)if((mask>>((count-1-i)/512))&1)
                    for(UINT w=0;w<Fixture::words;w++){UINT at=i*Fixture::words+w;wanted[at]=(0x3f000000u^(at*2654435761u))+salt;}
                // Two pending events must retain different visibility masks.
                require(submit(id,mask,0)==1 && submit(id,0,0)==1,"queued mask submit failed");
                f.verify(nullptr,salt,callback,id,true,&wanted);
                f.verify(nullptr,salt+5000,callback,id,false,&wanted);
                retained=wanted;
            }
            NativeApi::Stats s{};require(api.stats(id,&s)==1 && s.errors==0 && s.submitted==281 && s.executed==281,"mask queue stats failed");
            api.destroy(id);callback(id);require(submit(id,1,0)==0,"removed mask token accepted");
            id=create(f.source.Get(),pointers.data(),offsets.data(),static_cast<int>(count),Fixture::bytes);require(id>0,"overflow create failed");
            for(int i=0;i<128;i++)require(submit(id,0,0)==1,"queue rejected within capacity");
            require(submit(id,0,0)==0 && api.stats(id,&s)==1 && s.errors==1,"queue overflow did not fail closed");api.destroy(id);
            id=create(f.source.Get(),pointers.data(),offsets.data(),static_cast<int>(count),Fixture::bytes);require(id>0,"producer create failed");
            require(submit(id,0,0)==1,"producer initial submit failed");int wrong=1;
            std::thread other([&](){wrong=submit(id,~0ULL,~0ULL);});other.join();
            require(wrong==0 && api.stats(id,&s)==1 && s.errors==1,"wrong producer accepted");api.destroy(id);
            context->ClearState();context->Flush();
            printf("masked_copy backend=%s count=%u patterns=281 bytes=0 state=0 queued_visibility=PASS ring_wrap=PASS overflow=rejected producer=rejected removed=skip\n",hardware?"hardware":"eight-uav",count);fflush(stdout);
        }
        printf("GPUDECO_MASKED_COPY=PASS game_ordering=UNMEASURED game_fps=UNMEASURED\n");return 0;
    }catch(const std::exception& e){fprintf(stderr,"GPUDECO_MASKED_COPY=ERROR reason=%s\n",e.what());return 1;}
}
