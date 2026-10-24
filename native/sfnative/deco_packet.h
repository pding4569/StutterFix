// Original StutterFix packet arithmetic prototype. No Unity/D3D calls or allocation.
// Included only by the /fp:strict translation unit. Explicit research commands
// only: channel shadow validation and zero-duration live packet commits.
#include <stdint.h>
#include <stddef.h>
#include <string.h>

struct SfPacketChannel { float start, change, end; int source; };
struct SfPacketSource { float position, duration, easing; int active; };
struct SfPacketArgs { float operand[16]; float end_ease; int source; uint32_t mask, relative_position; };
static_assert(sizeof(SfPacketChannel)==16 && sizeof(SfPacketSource)==16 && sizeof(SfPacketArgs)==80,"packet ABI");

static bool sf_packet_finite(float value)
{
    uint32_t bits;memcpy(&bits,&value,sizeof(bits));return (bits&0x7f800000u)!=0x7f800000u;
}

static bool sf_packet_overlap(const void* a,size_t an,const void* b,size_t bn)
{
    uintptr_t x=(uintptr_t)a,y=(uintptr_t)b;
    return an && bn && (x<=y ? y-x<an : x-y<bn);
}
static float sf_packet_current(const SfPacketChannel& ch,const SfPacketSource* sources)
{
    if(ch.source<0)return ch.end;
    const auto& s=sources[ch.source];
    if(!s.active || s.position>=s.duration)return ch.end;
    if(s.position<=0.0f)return ch.start;
    // Exactly EvalMasterSlotCurrent: double intermediate, one final float cast.
    double product=(double)ch.change*(double)s.easing;
    return (float)((double)ch.start+product);
}

// Caller must supply owned, valid, disjoint buffers with the declared capacities.
// Invalid addresses are NOT made safe by managed try/catch. Rejection preserves
// authoritative channels/dirty words; scratch is explicitly disposable.
// Target order is preserved. Duplicates return -3 for original-path fallback.
extern "C" __declspec(dllexport) int __cdecl sf_deco_packet_apply_v1(
    SfPacketChannel* channels,int decorations,const SfPacketSource* sources,int source_count,
    const SfPacketArgs* packet,const uint32_t* targets,int target_count,
    const float* origins,int origin_count,SfPacketChannel* scratch,int scratch_channels,
    uint64_t* dirty,int dirty_words)
{
    if(decorations<1 || decorations>32768 || target_count<0 || target_count>32768
        || source_count<0 || source_count>8192 || !packet)return -1;
    if(packet->mask>65535 || packet->relative_position>1 || packet->source< -1
        || packet->source>=source_count || !sf_packet_finite(packet->end_ease))return -1;
    if(target_count==0 || packet->mask==0)return 1;
    int needed_words=((decorations+15)/16+63)/64;
    if(!channels || !targets || !scratch || !dirty || dirty_words<needed_words
        || scratch_channels<target_count*16 || (source_count && !sources))return -1;
    if(packet->source>=0)
    {
        const auto& next=sources[packet->source];
        if(next.active!=1 || next.position!=0.0f || !sf_packet_finite(next.duration)
            ||next.duration<=0 ||!sf_packet_finite(next.easing))return -4;
    }
    if((packet->mask&3u) && !packet->relative_position && (!origins || origin_count<decorations*2))return -1;
    size_t cb=(size_t)decorations*16*sizeof(SfPacketChannel),sb=(size_t)target_count*16*sizeof(SfPacketChannel);
    if(sf_packet_overlap(channels,cb,scratch,sb)
        || sf_packet_overlap(channels,cb,dirty,(size_t)dirty_words*8)
        || sf_packet_overlap(scratch,sb,dirty,(size_t)dirty_words*8))return -2;
    // Inputs cannot alias either writable channel storage or disposable scratch.
    const void* input[]={sources,packet,targets,origins};
    size_t size[]={(size_t)source_count*sizeof(SfPacketSource),sizeof(*packet),(size_t)target_count*4,
        origins && origin_count>0?(size_t)origin_count*4:0};
    for(int k=0;k<4;k++)if(size[k] && (sf_packet_overlap(input[k],size[k],channels,cb)
        ||sf_packet_overlap(input[k],size[k],scratch,sb)
        ||sf_packet_overlap(input[k],size[k],dirty,(size_t)dirty_words*8)))return -2;
    uint64_t seen[512]={};
    for(int i=0;i<target_count;i++)
    {
        uint32_t target=targets[i];if(target>=(uint32_t)decorations)return -3;
        uint64_t bit=1ull<<(target&63);if(seen[target>>6]&bit)return -3;seen[target>>6]|=bit;
    }
    for(int c=0;c<16;c++)if((packet->mask&(1u<<c)) && !sf_packet_finite(packet->operand[c]))return -4;
    // Preflight the entire packet before changing any authoritative byte.
    for(int i=0;i<target_count;i++)for(int c=0;c<16;c++)if(packet->mask&(1u<<c))
    {
        uint32_t target=targets[i];const auto& old=channels[(size_t)target*16+c];
        if(old.source< -1 || old.source>=source_count || !sf_packet_finite(old.start)
            ||!sf_packet_finite(old.change)||!sf_packet_finite(old.end))return -4;
        if(old.source>=0)
        {
            const auto& s=sources[old.source];
            if(!sf_packet_finite(s.position)||!sf_packet_finite(s.duration)||!sf_packet_finite(s.easing)
                ||s.duration<=0 ||s.active<0||s.active>1)return -4;
        }
        float target_value=packet->operand[c];
        if(c<2)
        {
            float origin=packet->relative_position?sf_packet_current(old,sources):origins[(size_t)target*2+c];
            if(!sf_packet_finite(origin))return -4;
            target_value=origin+target_value; // C# float addition before Kill(true).
        }
        bool scale=c==5||c==6||c==12||c==13;
        float change=packet->source>=0 || scale?target_value-old.end:0.0f;
        float end=target_value;
        // Mono keeps this float expression on its evaluation stack at double
        // precision until the final assignment (verified in the actual player).
        // change still has its separate float rounding for the LiveChannel ABI.
        if(scale)end=(float)((double)old.end+((double)target_value-(double)old.end)*(double)packet->end_ease);
        SfPacketChannel next=packet->source<0?SfPacketChannel{end,0.0f,end,-1}
            :SfPacketChannel{old.end,change,end,packet->source};
        if(!sf_packet_finite(next.start)||!sf_packet_finite(next.change)||!sf_packet_finite(next.end))return -4;
        scratch[(size_t)i*16+c]=next;
    }
    for(int i=0;i<target_count;i++)
    {
        uint32_t target=targets[i];
        for(int c=0;c<16;c++)if(packet->mask&(1u<<c))channels[(size_t)target*16+c]=scratch[(size_t)i*16+c];
        uint32_t block=target>>4;dirty[block>>6]|=1ull<<(block&63);
    }
    return 1;
}

// Matches GpuDecoGpu.Anchor exactly. Segment hi/lo stay zero for live anchors.
struct SfPacketSegment { float hi,lo,duration,start,end,target,over,period;uint32_t ease,scale; };
struct SfPacketAnchor { SfPacketSegment segment;uint32_t clock,valid; };
static_assert(sizeof(SfPacketAnchor)==48,"live anchor ABI");

// Commit a preflighted zero-duration packet to the authoritative channels and
// the one native-live slice. Managed code detaches the old source memberships
// first. No draw order, renderer, collider, clock or creation-order changes.
extern "C" __declspec(dllexport) int __cdecl sf_deco_packet_commit_static_v1(
    SfPacketChannel* master,SfPacketAnchor* anchors,int* bindings,int decorations,
    const SfPacketChannel* expected,SfPacketChannel* slice,int slice_count,
    const uint32_t* targets,const uint32_t* slice_targets,int count,uint32_t mask,
    uint8_t* dirty,int dirty_count)
{
    if(decorations<1||decorations>32768||slice_count<1||slice_count>32768||count<1||count>32768
        ||mask==0||mask>65535||dirty_count<(slice_count+15)/16
        ||!master||!anchors||!bindings||!expected||!slice||!targets||!slice_targets||!dirty)return -1;
    const void* writes[]={master,anchors,bindings,slice,dirty};
    size_t sizes[]={(size_t)decorations*16*sizeof(*master),(size_t)decorations*16*sizeof(*anchors),
        (size_t)decorations*16*sizeof(*bindings),(size_t)slice_count*16*sizeof(*slice),(size_t)dirty_count};
    const void* reads[]={expected,targets,slice_targets};
    size_t read_sizes[]={(size_t)decorations*16*sizeof(*expected),(size_t)count*4,(size_t)count*4};
    for(int i=0;i<5;i++)
    {
        for(int j=0;j<i;j++)if(sf_packet_overlap(writes[i],sizes[i],writes[j],sizes[j]))return -2;
        for(int j=0;j<3;j++)if(sf_packet_overlap(writes[i],sizes[i],reads[j],read_sizes[j]))return -2;
    }
    uint64_t seen[512]={},slice_seen[512]={};
    for(int i=0;i<count;i++)
    {
        uint32_t m=targets[i],s=slice_targets[i];if(m>=(uint32_t)decorations||s>=(uint32_t)slice_count)return -3;
        uint64_t mb=1ull<<(m&63),sb=1ull<<(s&63);
        if((seen[m>>6]&mb)||(slice_seen[s>>6]&sb))return -3;
        seen[m>>6]|=mb;slice_seen[s>>6]|=sb;
        for(int c=0;c<16;c++)if(mask&(1u<<c))
        {
            const auto& v=expected[(size_t)m*16+c];
            if(bindings[(size_t)m*16+c]!=-1||v.source!=-1||v.change!=0.0f||!sf_packet_finite(v.end)
                ||memcmp(&v.start,&v.end,sizeof(float))!=0)return -4;
        }
    }
    for(int i=0;i<count;i++)
    {
        uint32_t m=targets[i],s=slice_targets[i];
        for(int c=0;c<16;c++)if(mask&(1u<<c))
        {
            size_t at=(size_t)m*16+c;const auto v=expected[at];
            master[at]=v;bindings[at]=-1;slice[(size_t)s*16+c]=v;
            SfPacketAnchor a={};a.valid=1;a.segment.start=a.segment.end=a.segment.target=v.end;
            a.segment.ease=1;a.segment.scale=(c==5||c==6||c==12||c==13)?1u:0u;anchors[at]=a;
        }
        dirty[s>>4]=1;
    }
    return 1;
}
