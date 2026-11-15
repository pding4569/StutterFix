#pragma once
#include <cstdint>
#include <cstddef>
namespace drfull {
// All pointers are borrowed only for the duration of dispatch. Native owns copies
// and COM references. Opcodes are versioned behind ONE appended export so old
// export hints as well as ordinals remain stable.
enum Op { Create, Program, Geometry, Draw, Commit, Event, Stats, Destroy, Error, Abi, Composite, Perf, CommitPacked };
struct Performance { double applyUs,cullUs,constantsUs,drawUs,executeUs; uint64_t uploads,uploadBytes,layoutCreates,layoutHits,sampledFrames; };
struct CompositeIn { void *black, *white; };
struct AbiInfo { uint32_t version,create,program,geometry,draw,frame,stats,pose; };
struct CreateIn { void* resource; uint32_t capacity; uint32_t abi; };
struct Attribute { uint32_t semantic,index,format,stream,offset; };
struct ProgramIn { const void *vs,*ps; uint32_t vsBytes,psBytes; int32_t state[29]; };
struct GeometryIn {
    void* buffers[8]; uint32_t strides[8],offsets[8];
    void* indices; uint32_t indexFormat,indexStart,indexCount; int32_t baseVertex;
    const Attribute* attributes; uint32_t attributeCount;
    const void* vertices; uint32_t vertexBytes; const uint32_t* cpuIndices; uint32_t cpuIndexCount;
};
struct Buffer { uint32_t stage,slot,bytes,offset; };
struct Patch { uint32_t offset,kind; };
struct Texture { void* resource; int32_t stage,slot,sampler,filter,wrapU,wrapV,aniso,srgb; };
struct DrawIn {
    uint32_t program,geometry; const Buffer* buffers; uint32_t bufferCount;
    const Patch* patches; uint32_t patchCount; const Texture* textures; uint32_t textureCount;
    const void* constants; uint32_t constantBytes; uint32_t pose; uint32_t grab,sprite;
    float center[3],extent[3];
};
struct Pose { float world[16],inverse[16]; };
struct Frame {
    float vp[16],view[16],projection[16];
    float camera[4],screen[4],projectionParams[4],ortho[4],time[4],sinTime[4],cosTime[4],delta[4];
    const Pose* poses; const uint32_t* poseIds; uint32_t poseCount;
    const uint32_t* drawIds; const uint32_t* constantOffsets; const uint8_t* constants; uint32_t dirtyCount,constantBytes;
    const uint32_t* list; const uint32_t* segments; uint32_t listCount,segmentCount,listChanged,clearStencil;
};
struct Statistics { uint64_t frames,draws,errors,dropped,bytes,events; double renderUs,commitUs; int32_t lastHR; };
}
extern "C" intptr_t __cdecl sf_z_drfull_v1(int op,int token,const void* args) noexcept;
