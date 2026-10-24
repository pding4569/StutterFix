// Shadow-only arithmetic. Compile separately with /fp:strict; no FMA or fast-math.
#include "deco_packet.h"
#include "deco_packet_anim.h"
static float ease_value(int e, float t, float d)
{
    switch(e) {
    case 0: return t/d;
    case 1: t/=d; return t*t;
    case 2: t/=d; return -t*(t-2);
    case 3: t/=d; return t*t*t;
    case 4: t=t/d-1; return t*t*t+1;
    case 5: t/=d*.5f; if(t<1) return .5f*t*t; --t; return -.5f*(t*(t-2)-1);
    case 6: t/=d*.5f; if(t<1) return .5f*t*t*t; t-=2; return .5f*(t*t*t+2);
    default: return 0;
    }
}
extern "C" __declspec(dllexport) int __cdecl sf_decobatch_step(int n, const int* ease, const int* key,
    const float* dur, float* pos, const float* start, const float* change,
    const float* over, const float* period, float* output, int* done, float td)
{
    (void)over; (void)period; // Polynomial eases do not consume these values.
    if(n<0 || n>65536) return 0;
    if(n && (!ease||!key||!dur||!pos||!start||!change||!output||!done)) return 0;
    for(int i=0;i<n;i++) {
        if(ease[i]<0 || ease[i]>6 || !(key[i]==1||key[i]==2||key[i]==5||key[i]==7||key[i]==8||key[i]==9||key[i]==10)) { done[i]=-1; continue; }
        float p=pos[i]+td; bool end=p>=dur[i]; if(end) p=dur[i];
        pos[i]=p; done[i]=end?1:0; float e=ease_value(ease[i],p,dur[i]); int b=i*4;
        int lanes=key[i]==9?4:(key[i]==7||key[i]==8?2:1);
        for(int j=0;j<lanes;j++) {
            if((key[i]==7&&j==1)||(key[i]==8&&j==0)) output[b+j]=start[b+j];
            else { float product=change[b+j]*e; output[b+j]=start[b+j]+product; }
        }
    }
    return 1;
}
