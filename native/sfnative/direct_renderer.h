#pragma once
#include "direct_tracks.h"
// C ABI: Camera is copied on submit; texture pointers must be D3D11 resources.
#ifdef _WIN32
#define DR_API extern "C" __declspec(dllexport)
#else
#define DR_API extern "C"
#endif
DR_API int sf_dr_create(unsigned count,unsigned layers,unsigned animated,unsigned threads,void* textureArray) noexcept;
DR_API int sf_dr_submit(int token,double time,const dr::Camera* camera,void* target) noexcept;
DR_API void sf_dr_destroy(int token) noexcept;
DR_API void* sf_dr_event() noexcept;
// 0 frames,1 drawn,2 draws,3 active,4 skipped,5 errors; metrics 6..17 mean/p95 pairs:
// submit,worker sum,worker wall,render incl wait,render excl wait,wait. All microseconds.
// 18 outside clip volume,19 alpha<=0,20 invalid alpha (last completed frame).
DR_API double sf_dr_stat(int token,int index) noexcept;
