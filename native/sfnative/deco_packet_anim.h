// Original StutterFix. Animated packet metadata transaction, /fp:strict only.
// Included after deco_packet.h; no renderer/device/resource ownership.
struct SfPacketAnimation { float duration,over,period;uint32_t ease; };
static_assert(sizeof(SfPacketAnimation)==16,"animation packet ABI");
static bool sf_packet_same_bits(float a,float b){return memcmp(&a,&b,4)==0;}
extern "C" __declspec(dllexport) int __cdecl sf_deco_packet_commit_animated_v1(
    SfPacketChannel* master,SfPacketAnchor* anchors,int* bindings,int decorations,
    const SfPacketChannel* expected,SfPacketChannel* slice,int slice_count,
    const uint32_t* targets,const uint32_t* slice_targets,int count,
    const SfPacketArgs* packet,const SfPacketAnimation* animation,
    const SfPacketSource* clocks,int clock_count,const float* origins,int origin_count,
    float* target_scratch,int scratch_count,uint8_t* dirty,int dirty_count)
{
    if(decorations<1||decorations>32768||slice_count<1||slice_count>32768||count<1||count>32768
        ||clock_count<1||clock_count>8192||!packet||!animation||!master||!anchors||!bindings||!expected
        ||!slice||!targets||!slice_targets||!clocks||!target_scratch||scratch_count<count*16
        ||!dirty||dirty_count<(slice_count+15)/16)return -1;
    if(packet->mask==0||packet->mask>65535||packet->relative_position>1||packet->source<0||packet->source>=clock_count
        ||!sf_packet_finite(packet->end_ease)||!sf_packet_finite(animation->duration)||animation->duration<=0
        ||!sf_packet_finite(animation->over)||!sf_packet_finite(animation->period)
        ||!(animation->ease<=31||animation->ease==36))return -1;
    if((packet->mask&3u)&&!packet->relative_position&&(!origins||origin_count<decorations*2))return -1;
    const auto& next=clocks[packet->source];
    if(next.active!=1||next.position!=0.0f||!sf_packet_same_bits(next.duration,animation->duration))return -4;
    const void* writes[]={master,anchors,bindings,slice,dirty,target_scratch};
    size_t sizes[]={(size_t)decorations*16*sizeof(*master),(size_t)decorations*16*sizeof(*anchors),
        (size_t)decorations*16*sizeof(*bindings),(size_t)slice_count*16*sizeof(*slice),(size_t)dirty_count,(size_t)count*16*4};
    const void* reads[]={expected,targets,slice_targets,packet,animation,clocks,origins};
    size_t rs[]={(size_t)decorations*16*sizeof(*expected),(size_t)count*4,(size_t)count*4,sizeof(*packet),sizeof(*animation),
        (size_t)clock_count*sizeof(*clocks),origins&&origin_count>0?(size_t)origin_count*4:0};
    for(int i=0;i<6;i++)
    {
        for(int j=0;j<i;j++)if(sf_packet_overlap(writes[i],sizes[i],writes[j],sizes[j]))return -2;
        for(int j=0;j<7;j++)if(sf_packet_overlap(writes[i],sizes[i],reads[j],rs[j]))return -2;
    }
    uint64_t seen[512]={},ss[512]={};
    for(int i=0;i<count;i++)
    {
        uint32_t m=targets[i],s=slice_targets[i];if(m>=(uint32_t)decorations||s>=(uint32_t)slice_count)return -3;
        uint64_t mb=1ull<<(m&63),sb=1ull<<(s&63);if((seen[m>>6]&mb)||(ss[s>>6]&sb))return -3;
        seen[m>>6]|=mb;ss[s>>6]|=sb;
        for(int c=0;c<16;c++)if(packet->mask&(1u<<c))
        {
            size_t at=(size_t)m*16+c;const auto& old=master[at];const auto& n=expected[at];
            if(bindings[at]!=-1||old.source< -1||old.source>=clock_count||n.source!=packet->source
                ||!sf_packet_finite(old.start)||!sf_packet_finite(old.change)||!sf_packet_finite(old.end)
                ||!sf_packet_finite(n.start)||!sf_packet_finite(n.change)||!sf_packet_finite(n.end))return -4;
            if(old.source>=0)
            {
                const auto& previous=clocks[old.source];
                if(!sf_packet_finite(previous.position)||!sf_packet_finite(previous.duration)||previous.duration<=0
                    ||!sf_packet_finite(previous.easing)||previous.active<0||previous.active>1)return -4;
            }
            float target=packet->operand[c];
            if(c<2)
            {
                float origin=packet->relative_position?sf_packet_current(old,clocks):origins[(size_t)m*2+c];
                if(!sf_packet_finite(origin))return -4;target=origin+target;
            }
            if(!sf_packet_finite(target))return -4;
            float change=target-old.end;
            bool scale=c==5||c==6||c==12||c==13;
            float end=scale?(float)((double)old.end+((double)target-(double)old.end)*(double)packet->end_ease):target;
            if(!sf_packet_same_bits(n.start,old.end)||!sf_packet_same_bits(n.change,change)||!sf_packet_same_bits(n.end,end))return -4;
            target_scratch[(size_t)i*16+c]=target;
        }
    }
    for(int i=0;i<count;i++)
    {
        uint32_t m=targets[i],s=slice_targets[i];
        for(int c=0;c<16;c++)if(packet->mask&(1u<<c))
        {
            size_t at=(size_t)m*16+c;const auto n=expected[at];master[at]=n;bindings[at]=packet->source;slice[(size_t)s*16+c]=n;
            SfPacketAnchor a={};a.valid=1;a.clock=(uint32_t)packet->source;
            a.segment.duration=animation->duration;a.segment.start=n.start;a.segment.end=n.end;a.segment.target=target_scratch[(size_t)i*16+c];
            a.segment.over=animation->over;a.segment.period=animation->period;a.segment.ease=animation->ease;
            a.segment.scale=(c==5||c==6||c==12||c==13)?1u:0u;anchors[at]=a;
        }
        dirty[s>>4]=1;
    }
    return 1;
}
