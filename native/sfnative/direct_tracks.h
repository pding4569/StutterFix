// M0 research only. No Unity object access, persistent worker pool.
#pragma once
#include <array>
#include <vector>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <cstdint>
namespace dr {
enum Ease { Linear, InQuad, OutQuad, InOutQuad, InSine, OutSine, InOutSine, OutBack, OutExpo, EaseCount };
float ease(int id,float t);
struct Segment { float t0,duration,from,to; int easing; };
struct Track { unsigned instance,channel; std::vector<Segment> segments; size_t cursor=0; int slot=-1; };
struct Instance {
    float matrix[6]; float color[4]; float uv[4]; unsigned layer,flags; float parallax[2];
};
static_assert(sizeof(Instance)==72,"GPU instance stride");
struct Camera { float vp[16]; float position[4]; };
static_assert(sizeof(Camera)==80,"camera ABI");
struct Object { float channel[9]; Instance gpu; float cosine=1,sine=0; };
struct Event { float time; unsigned track; bool start; };
// Cold Track/Segment storage is visited only at a boundary. Hot SoA slots are
// grouped by easing so arithmetic loops have no per-track easing dispatch.
struct HotGroup {
    std::vector<double> t0,invDuration,next;
    std::vector<float> from,delta;
    std::vector<unsigned> track;
    std::vector<Object*> object;
    void clear(){t0.clear();invDuration.clear();next.clear();from.clear();delta.clear();track.clear();object.clear();}
};
struct Worker {
    std::vector<Track> tracks; std::vector<Event> events; std::vector<unsigned> active;
    std::vector<Instance> visible; size_t visibleCount=0,next=0; unsigned begin=0,end=0;
    double last=-1,us=0; std::thread thread;
    uint64_t alpha=0,outside=0,other=0;
    double evalUs=0,cullUs=0;
    std::array<HotGroup,EaseCount*9> hot;
    bool hotValid=false;
};
class Tracks {
    std::mutex mutex; std::condition_variable wake,done; bool stopping=false;
    uint64_t generation=0; unsigned remaining=0;
    double time=0,completedUs=0; Camera camera{};
    void run(unsigned id);
public:
    std::vector<Object> objects; std::vector<Worker> workers;
    double wallUs=0,sumUs=0; uint64_t activeCount=0;
    Tracks(unsigned count,unsigned layers,unsigned animated,unsigned threads=1);
    ~Tracks();
    Tracks(const Tracks&)=delete; Tracks& operator=(const Tracks&)=delete;
    void start(double t,const Camera& c); void wait();
    void evaluate(Worker& w,double t,const Camera& c);
};
double clockUs();
}
