#include "direct_tracks.h"
#include <algorithm>
#include <cmath>
#include <chrono>
#include <random>
#include <stdexcept>
#include <cstring>
#include <emmintrin.h>
namespace dr {
double clockUs(){return std::chrono::duration<double,std::micro>(std::chrono::steady_clock::now().time_since_epoch()).count();}
float ease(int id,float t){
    constexpr float pi=3.14159265358979323846f;
    switch(id){
    case InQuad:return t*t;case OutQuad:return -t*(t-2);case InOutQuad:t*=2;return t<1?0.5f*t*t:-0.5f*((--t)*(t-2)-1);
    case InSine:return -std::cos(t*(pi/2))+1;case OutSine:return std::sin(t*(pi/2));case InOutSine:return -0.5f*(std::cos(pi*t)-1);
    case OutBack:{float s=1.70158f;--t;return t*t*((s+1)*t+s)+1;}
    case OutExpo:return t==1?1:-std::pow(2.0f,-10*t)+1;default:return t;
    }
}
static __forceinline void matrix(Object& o){float a=o.channel[2]*0.017453292519943295f,c=std::cos(a),s=std::sin(a);
    o.cosine=c;o.sine=s;
    float* m=o.gpu.matrix;m[0]=c*o.channel[3];m[1]=-s*o.channel[4];m[2]=o.channel[0];m[3]=s*o.channel[3];m[4]=c*o.channel[4];m[5]=o.channel[1];
    std::copy_n(o.channel+5,4,o.gpu.color);
}
static __forceinline void write(Object& o,unsigned ch,float v){
    o.channel[ch]=v;
    if(ch==0)o.gpu.matrix[2]=v;else if(ch==1)o.gpu.matrix[5]=v;else if(ch==2)matrix(o);
    else if(ch==3){o.gpu.matrix[0]=o.cosine*v;o.gpu.matrix[3]=o.sine*v;}
    else if(ch==4){o.gpu.matrix[1]=-o.sine*v;o.gpu.matrix[4]=o.cosine*v;}
    else o.gpu.color[ch-5]=v;
}
static void cache(HotGroup& h,unsigned ti,Track& tr,std::vector<Object>& objects){
    const auto& s=tr.segments[tr.cursor];h.t0.push_back(s.t0);h.invDuration.push_back(1.0/double(s.duration));
    h.next.push_back(tr.cursor+1<tr.segments.size()?tr.segments[tr.cursor+1].t0:1e100);
    h.from.push_back(s.from);h.delta.push_back(s.to-s.from);h.track.push_back(ti);
    h.object.push_back(&objects[tr.instance]);
}
template<class T> static void eraseSlot(std::vector<T>& v,size_t i){v[i]=v.back();v.pop_back();}
static void eraseSlot(HotGroup& h,size_t i){eraseSlot(h.t0,i);eraseSlot(h.invDuration,i);eraseSlot(h.next,i);eraseSlot(h.from,i);eraseSlot(h.delta,i);eraseSlot(h.track,i);eraseSlot(h.object,i);}
template<unsigned C> static __forceinline void writeChannel(Object& o,float v){
    o.channel[C]=v;
    if constexpr(C==0)o.gpu.matrix[2]=v;
    else if constexpr(C==1)o.gpu.matrix[5]=v;
    else if constexpr(C==2)matrix(o);
    else if constexpr(C==3){o.gpu.matrix[0]=o.cosine*v;o.gpu.matrix[3]=o.sine*v;}
    else if constexpr(C==4){o.gpu.matrix[1]=-o.sine*v;o.gpu.matrix[4]=o.cosine*v;}
    else o.gpu.color[C-5]=v;
}
template<int E,unsigned C> static void evaluateHot(HotGroup& h,double t){
    // Contiguous arithmetic inputs. No Segment vector, cursor or easing dispatch here.
    size_t i=0;
    for(;i+4<=h.track.size();i+=4){
        __m128d now=_mm_set1_pd(t);
        __m128 lo=_mm_cvtpd_ps(_mm_mul_pd(_mm_sub_pd(now,_mm_loadu_pd(h.t0.data()+i)),_mm_loadu_pd(h.invDuration.data()+i)));
        __m128 hi=_mm_cvtpd_ps(_mm_mul_pd(_mm_sub_pd(now,_mm_loadu_pd(h.t0.data()+i+2)),_mm_loadu_pd(h.invDuration.data()+i+2)));
        __m128 u=_mm_max_ps(_mm_setzero_ps(),_mm_min_ps(_mm_set1_ps(1),_mm_movelh_ps(lo,hi))),v=u;
        if constexpr(E==InQuad)v=_mm_mul_ps(u,u);
        else if constexpr(E==OutQuad)v=_mm_mul_ps(_mm_sub_ps(_mm_setzero_ps(),u),_mm_sub_ps(u,_mm_set1_ps(2)));
        else if constexpr(E==InOutQuad){
            __m128 x=_mm_mul_ps(u,_mm_set1_ps(2)),y=_mm_sub_ps(x,_mm_set1_ps(1));
            __m128 low=_mm_mul_ps(_mm_mul_ps(_mm_set1_ps(0.5f),x),x);
            __m128 high=_mm_mul_ps(_mm_set1_ps(-0.5f),_mm_sub_ps(_mm_mul_ps(y,_mm_sub_ps(y,_mm_set1_ps(2))),_mm_set1_ps(1)));
            __m128 select=_mm_cmplt_ps(x,_mm_set1_ps(1));v=_mm_or_ps(_mm_and_ps(select,low),_mm_andnot_ps(select,high));
        }
        else if constexpr(E==OutBack){
            __m128 x=_mm_sub_ps(u,_mm_set1_ps(1));
            v=_mm_add_ps(_mm_mul_ps(_mm_mul_ps(x,x),_mm_add_ps(_mm_mul_ps(_mm_set1_ps(1.70158f+1),x),_mm_set1_ps(1.70158f))),_mm_set1_ps(1));
        }
        else if constexpr(E!=Linear){
            float lanes[4];_mm_storeu_ps(lanes,u);
            for(unsigned k=0;k<4;++k)lanes[k]=ease(E,lanes[k]);
            v=_mm_loadu_ps(lanes);
        }
        v=_mm_add_ps(_mm_loadu_ps(h.from.data()+i),_mm_mul_ps(_mm_loadu_ps(h.delta.data()+i),v));
        float lanes[4];_mm_storeu_ps(lanes,v);
        for(unsigned k=0;k<4;++k)writeChannel<C>(*h.object[i+k],lanes[k]);
    }
    for(;i<h.track.size();++i){float u=(std::max)(0.0f,(std::min)(1.0f,float((t-h.t0[i])*h.invDuration[i])));writeChannel<C>(*h.object[i],h.from[i]+h.delta[i]*ease(E,u));}
}
template<unsigned C> static void evaluateChannel(Worker& w,double t){
    evaluateHot<Linear,C>(w.hot[Linear*9+C],t);evaluateHot<InQuad,C>(w.hot[InQuad*9+C],t);evaluateHot<OutQuad,C>(w.hot[OutQuad*9+C],t);evaluateHot<InOutQuad,C>(w.hot[InOutQuad*9+C],t);
    evaluateHot<InSine,C>(w.hot[InSine*9+C],t);evaluateHot<OutSine,C>(w.hot[OutSine*9+C],t);evaluateHot<InOutSine,C>(w.hot[InOutSine*9+C],t);evaluateHot<OutBack,C>(w.hot[OutBack*9+C],t);evaluateHot<OutExpo,C>(w.hot[OutExpo*9+C],t);
}
static __forceinline bool visible(const Instance& i,const Camera& c){
    if(i.color[3]<=0)return false;
    if(i.flags&2){
        const __m128 mask=_mm_castsi128_ps(_mm_set1_epi32(0x7fffffff));
        __m128 a=_mm_and_ps(_mm_setr_ps(i.matrix[0],i.matrix[3],i.matrix[0],i.matrix[3]),mask);
        __m128 b=_mm_and_ps(_mm_setr_ps(i.matrix[1],i.matrix[4],i.matrix[1],i.matrix[4]),mask);
        __m128 radius=_mm_mul_ps(_mm_add_ps(a,b),_mm_set1_ps(0.5f));
        __m128 center=_mm_setr_ps(i.matrix[2],i.matrix[5],-i.matrix[2],-i.matrix[5]);
        return _mm_movemask_ps(_mm_cmplt_ps(_mm_add_ps(center,radius),_mm_setr_ps(-12,-7,-12,-7)))==0;
    }
    // Conservative homogeneous four-corner plane test; never assumes an orthographic camera.
    bool left=true,right=true,top=true,bottom=true,nearPlane=true,farPlane=true;
    for(unsigned k=0;k<4;++k){float x=(k&1)?0.5f:-0.5f,y=(k&2)?0.5f:-0.5f;
        float wx=i.matrix[0]*x+i.matrix[1]*y+i.matrix[2]+c.position[0]*(1-i.parallax[0]);
        float wy=i.matrix[3]*x+i.matrix[4]*y+i.matrix[5]+c.position[1]*(1-i.parallax[1]);
        if(i.flags&1){wx+=c.position[0]*i.parallax[0];wy+=c.position[1]*i.parallax[1];}
        const float* v=c.vp;float px=v[0]*wx+v[4]*wy+v[12],py=v[1]*wx+v[5]*wy+v[13],pz=v[2]*wx+v[6]*wy+v[14],pw=v[3]*wx+v[7]*wy+v[15];
        left&=px < -pw;right&=px > pw;bottom&=py < -pw;top&=py > pw;nearPlane&=pz<0;farPlane&=pz>pw;
    }return !(left||right||top||bottom||nearPlane||farPlane);
}
Tracks::Tracks(unsigned count,unsigned layers,unsigned animated,unsigned threads):objects(count),workers((std::max)(1u,(std::min)(threads,(std::max)(1u,std::thread::hardware_concurrency()>2?std::thread::hardware_concurrency()-2:1)))){
    std::mt19937 rng(0x535446);auto random=[&](float a,float b){return a+(b-a)*float(rng()>>8)*(1.0f/16777216.0f);};
    for(unsigned j=0;j<workers.size();++j){auto& w=workers[j];w.begin=count*j/unsigned(workers.size());w.end=count*(j+1)/unsigned(workers.size());w.visible.resize(w.end-w.begin);
        for(unsigned n=w.begin;n<w.end;++n){auto& o=objects[n];o.channel[0]=random(-12,12);o.channel[1]=random(-7,7);o.channel[2]=random(-180,180);o.channel[3]=random(0.025f,0.3f);o.channel[4]=random(0.025f,0.3f);
            for(int k=5;k<9;++k)o.channel[k]=random(0.3f,1);if(n%17==0)o.channel[8]=0;
            o.gpu.uv[2]=o.gpu.uv[3]=1;o.gpu.layer=n%layers;o.gpu.flags=2;o.gpu.parallax[0]=o.gpu.parallax[1]=1;
            // Last four objects are stable, opaque screen-centre witnesses, drawn last.
            if(n+4>=count){o.channel[0]=(float(n%4)-1.5f)*2.5f;o.channel[1]=0;o.channel[2]=0;o.channel[3]=o.channel[4]=2;for(int k=5;k<9;++k)o.channel[k]=1;}
            matrix(o);
            if(n%100>=animated||n+4>=count)continue;
            Track tr{};tr.instance=n;tr.channel=n%9;float value=o.channel[tr.channel];
            for(float t=0;t<60;){float duration=(std::min)(random(0.2f,2),60-t);float target=tr.channel<2?random(-10,10):tr.channel==2?random(-180,180):tr.channel<5?random(0.025f,0.3f):random(0,1);
                tr.segments.push_back({t,duration,value,target,int(rng()%EaseCount)});value=target;t+=duration;}
            unsigned ti=unsigned(w.tracks.size());w.events.push_back({0,ti,true});w.events.push_back({60,ti,false});w.tracks.push_back(std::move(tr));
        }
        w.active.reserve(w.tracks.size());std::sort(w.events.begin(),w.events.end(),[](const Event& a,const Event& b){return a.time<b.time||(a.time==b.time&&!a.start&&b.start);});
        std::array<size_t,9> channelCounts{};for(const auto& tr:w.tracks)++channelCounts[tr.channel];
        for(size_t k=0;k<w.hot.size();++k){auto& h=w.hot[k];size_t cap=channelCounts[k%9];h.t0.reserve(cap);h.invDuration.reserve(cap);h.next.reserve(cap);h.from.reserve(cap);h.delta.reserve(cap);h.track.reserve(cap);h.object.reserve(cap);}
    }
    try{for(unsigned j=0;j<workers.size();++j)workers[j].thread=std::thread([this,j]{run(j);});}catch(...){{std::lock_guard<std::mutex> lock(mutex);stopping=true;}wake.notify_all();for(auto& w:workers)if(w.thread.joinable())w.thread.join();throw;}
}
Tracks::~Tracks(){{std::lock_guard<std::mutex> lock(mutex);stopping=true;}wake.notify_all();for(auto& w:workers)if(w.thread.joinable())w.thread.join();}
void Tracks::run(unsigned id){uint64_t seen=0;for(;;){std::unique_lock<std::mutex> lock(mutex);wake.wait(lock,[&]{return stopping||generation!=seen;});if(stopping)return;seen=generation;double t=time;Camera c=camera;lock.unlock();double begin=clockUs();evaluate(workers[id],t,c);workers[id].us=clockUs()-begin;lock.lock();if(--remaining==0){completedUs=clockUs();done.notify_all();}}}
void Tracks::start(double t,const Camera& c){std::lock_guard<std::mutex> lock(mutex);if(remaining)throw std::runtime_error("direct tracks busy");time=t;camera=c;remaining=unsigned(workers.size());++generation;wallUs=clockUs();wake.notify_all();}
void Tracks::wait(){std::unique_lock<std::mutex> lock(mutex);done.wait(lock,[&]{return remaining==0;});wallUs=completedUs-wallUs;sumUs=0;activeCount=0;for(auto& w:workers){sumUs+=w.us;activeCount+=w.active.size();}}
void Tracks::evaluate(Worker& w,double t,const Camera& c){
    double evalBegin=clockUs();
    auto remove=[&](Track& tr){if(tr.slot<0)return;unsigned slot=unsigned(tr.slot),back=w.active.back();w.active[slot]=back;w.tracks[back].slot=int(slot);w.active.pop_back();tr.slot=-1;};
    auto value=[&](Track& tr){auto& ss=tr.segments;while(tr.cursor+1<ss.size()&&t>=ss[tr.cursor+1].t0)++tr.cursor;auto& s=ss[tr.cursor];float u=(std::max)(0.0f,(std::min)(1.0f,float((t-s.t0)/s.duration)));write(objects[tr.instance],tr.channel,s.from+(s.to-s.from)*ease(s.easing,u));};
    if(t<w.last || w.last<0){w.hotValid=false;w.active.clear();for(auto& tr:w.tracks){tr.slot=-1;auto it=std::upper_bound(tr.segments.begin(),tr.segments.end(),t,[](double at,const Segment& s){return at<s.t0;});tr.cursor=it==tr.segments.begin()?0:size_t(it-tr.segments.begin()-1);value(tr);}
        w.next=0; // Rebuild the event-driven active set, including exact boundary events.
    }
    while(w.next<w.events.size()&&w.events[w.next].time<=t){w.hotValid=false;auto& e=w.events[w.next++];auto& tr=w.tracks[e.track];if(e.start){if(tr.slot<0){tr.slot=int(w.active.size());w.active.push_back(e.track);}}else{value(tr);remove(tr);}}
    if(!w.hotValid){for(auto& h:w.hot)h.clear();for(unsigned ti:w.active){auto& tr=w.tracks[ti];while(tr.cursor+1<tr.segments.size()&&t>=tr.segments[tr.cursor+1].t0)++tr.cursor;cache(w.hot[tr.segments[tr.cursor].easing*9+tr.channel],ti,tr,objects);}w.hotValid=true;}
    else for(auto& h:w.hot)for(size_t i=0;i<h.track.size();){if(t<h.next[i]){++i;continue;}unsigned ti=h.track[i];auto& tr=w.tracks[ti];while(tr.cursor+1<tr.segments.size()&&t>=tr.segments[tr.cursor+1].t0)++tr.cursor;eraseSlot(h,i);cache(w.hot[tr.segments[tr.cursor].easing*9+tr.channel],ti,tr,objects);}

    evaluateChannel<0>(w,t);evaluateChannel<1>(w,t);evaluateChannel<2>(w,t);evaluateChannel<3>(w,t);evaluateChannel<4>(w,t);evaluateChannel<5>(w,t);evaluateChannel<6>(w,t);evaluateChannel<7>(w,t);evaluateChannel<8>(w,t);
    double cullBegin=clockUs();w.evalUs=cullBegin-evalBegin;

    w.visibleCount=0;w.alpha=w.outside=w.other=0;
    for(unsigned n=w.begin;n<w.end;++n){const auto& i=objects[n].gpu;uint32_t alphaBits;std::memcpy(&alphaBits,&i.color[3],sizeof(alphaBits));if((alphaBits&0x7f800000u)==0x7f800000u)++w.other;else if(i.color[3]<=0)++w.alpha;else if(visible(i,c))w.visible[w.visibleCount++]=i;else ++w.outside;}w.last=t;w.cullUs=clockUs()-cullBegin;
}
}
