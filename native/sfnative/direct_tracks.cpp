#include "direct_tracks.h"
#include <algorithm>
#include <cmath>
#include <chrono>
#include <random>
#include <stdexcept>
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
static void matrix(Object& o){float a=o.channel[2]*0.017453292519943295f,c=std::cos(a),s=std::sin(a);
    float* m=o.gpu.matrix;m[0]=c*o.channel[3];m[1]=-s*o.channel[4];m[2]=o.channel[0];m[3]=s*o.channel[3];m[4]=c*o.channel[4];m[5]=o.channel[1];
    std::copy_n(o.channel+5,4,o.gpu.color);
}
static bool visible(const Instance& i,const Camera& c){
    if(i.color[3]<=0)return false;
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
    for(unsigned j=0;j<workers.size();++j){auto& w=workers[j];w.begin=count*j/unsigned(workers.size());w.end=count*(j+1)/unsigned(workers.size());w.visible.reserve(w.end-w.begin);
        for(unsigned n=w.begin;n<w.end;++n){auto& o=objects[n];o.channel[0]=random(-12,12);o.channel[1]=random(-7,7);o.channel[2]=random(-180,180);o.channel[3]=random(0.025f,0.3f);o.channel[4]=random(0.025f,0.3f);
            for(int k=5;k<9;++k)o.channel[k]=random(0.3f,1);if(n%17==0)o.channel[8]=0;
            o.gpu.uv[2]=o.gpu.uv[3]=1;o.gpu.layer=n%layers;o.gpu.flags=1;o.gpu.parallax[0]=o.gpu.parallax[1]=1;matrix(o);
            if(n%100>=animated)continue;
            Track tr{};tr.instance=n;tr.channel=n%9;float value=o.channel[tr.channel];
            for(float t=0;t<60;){float duration=(std::min)(random(0.2f,2),60-t);float target=tr.channel<2?random(-10,10):tr.channel==2?random(-180,180):tr.channel<5?random(0.025f,0.3f):random(0,1);
                tr.segments.push_back({t,duration,value,target,int(rng()%EaseCount)});value=target;t+=duration;}
            unsigned ti=unsigned(w.tracks.size());w.events.push_back({0,ti,true});w.events.push_back({60,ti,false});w.tracks.push_back(std::move(tr));
        }
        w.active.reserve(w.tracks.size());std::sort(w.events.begin(),w.events.end(),[](const Event& a,const Event& b){return a.time<b.time||(a.time==b.time&&!a.start&&b.start);});
    }
    try{for(unsigned j=0;j<workers.size();++j)workers[j].thread=std::thread([this,j]{run(j);});}catch(...){{std::lock_guard<std::mutex> lock(mutex);stopping=true;}wake.notify_all();for(auto& w:workers)if(w.thread.joinable())w.thread.join();throw;}
}
Tracks::~Tracks(){{std::lock_guard<std::mutex> lock(mutex);stopping=true;}wake.notify_all();for(auto& w:workers)if(w.thread.joinable())w.thread.join();}
void Tracks::run(unsigned id){uint64_t seen=0;for(;;){std::unique_lock<std::mutex> lock(mutex);wake.wait(lock,[&]{return stopping||generation!=seen;});if(stopping)return;seen=generation;double t=time;Camera c=camera;lock.unlock();double begin=clockUs();evaluate(workers[id],t,c);workers[id].us=clockUs()-begin;lock.lock();if(--remaining==0){completedUs=clockUs();done.notify_all();}}}
void Tracks::start(double t,const Camera& c){std::lock_guard<std::mutex> lock(mutex);if(remaining)throw std::runtime_error("direct tracks busy");time=t;camera=c;remaining=unsigned(workers.size());++generation;wallUs=clockUs();wake.notify_all();}
void Tracks::wait(){std::unique_lock<std::mutex> lock(mutex);done.wait(lock,[&]{return remaining==0;});wallUs=completedUs-wallUs;sumUs=0;activeCount=0;for(auto& w:workers){sumUs+=w.us;activeCount+=w.active.size();}}
void Tracks::evaluate(Worker& w,double t,const Camera& c){
    auto remove=[&](Track& tr){if(tr.slot<0)return;unsigned slot=unsigned(tr.slot),back=w.active.back();w.active[slot]=back;w.tracks[back].slot=int(slot);w.active.pop_back();tr.slot=-1;};
    auto value=[&](Track& tr){auto& ss=tr.segments;while(tr.cursor+1<ss.size()&&t>=ss[tr.cursor+1].t0)++tr.cursor;auto& s=ss[tr.cursor];float u=(std::max)(0.0f,(std::min)(1.0f,float((t-s.t0)/s.duration)));objects[tr.instance].channel[tr.channel]=s.from+(s.to-s.from)*ease(s.easing,u);matrix(objects[tr.instance]);};
    if(t<w.last || w.last<0){w.active.clear();for(auto& tr:w.tracks){tr.slot=-1;auto it=std::upper_bound(tr.segments.begin(),tr.segments.end(),t,[](double at,const Segment& s){return at<s.t0;});tr.cursor=it==tr.segments.begin()?0:size_t(it-tr.segments.begin()-1);value(tr);}
        w.next=0; // Rebuild the event-driven active set, including exact boundary events.
    }
    while(w.next<w.events.size()&&w.events[w.next].time<=t){auto& e=w.events[w.next++];auto& tr=w.tracks[e.track];if(e.start){if(tr.slot<0){tr.slot=int(w.active.size());w.active.push_back(e.track);}}else{value(tr);remove(tr);}}
    for(unsigned ti:w.active)value(w.tracks[ti]);w.visible.clear();for(unsigned n=w.begin;n<w.end;++n)if(visible(objects[n].gpu,c))w.visible.push_back(objects[n].gpu);w.last=t;
}
}
