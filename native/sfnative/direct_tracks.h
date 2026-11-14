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
struct Object { float channel[9]; Instance gpu; };
struct Event { float time; unsigned track; bool start; };
struct Worker {
    std::vector<Track> tracks; std::vector<Event> events; std::vector<unsigned> active;
    std::vector<Instance> visible; size_t next=0; unsigned begin=0,end=0;
    double last=-1,us=0; std::thread thread;
};
class Tracks {
    std::mutex mutex; std::condition_variable wake,done; bool stopping=false;
    uint64_t generation=0; unsigned remaining=0;
    double time=0,completedUs=0; Camera camera{};
    void run(unsigned id);
public:
    std::vector<Object> objects; std::vector<Worker> workers;
    double wallUs=0,sumUs=0; uint64_t activeCount=0;
    Tracks(unsigned count,unsigned layers,unsigned animated,unsigned threads=3);
    ~Tracks();
    Tracks(const Tracks&)=delete; Tracks& operator=(const Tracks&)=delete;
    void start(double t,const Camera& c); void wait();
    void evaluate(Worker& w,double t,const Camera& c);
};
double clockUs();
}
