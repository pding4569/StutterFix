#pragma once
#include <cstdint>
#include <cstddef>
namespace drfull {
// All pointers are borrowed only for the duration of dispatch. Native owns copies
// and COM references. Opcodes are versioned behind ONE appended export so old
// export hints as well as ordinals remain stable.
enum Op { Create, Program, Geometry, Draw, Commit, Event, Stats, Destroy, Error, Abi, Composite, Perf, CommitPacked, CommitActive, BatchPerf, GeometryCpu, CommitActiveWorld, UploadPerf, GpuPerf, TimelineEvent, TimelinePerf, MemoryPerf, Configure, SubmissionPerf, DrawProbeConfigure, DrawProbeRead, TextureProbe, ResourceConfigure, ResourcePerf, DrawProbeDetailRead, DrawProbeStatus, ReplaceDraw, CacheConfigure, CachePerf, CacheDetail, CacheTuning, LocalPlanPerf, NativeStagePerf, Residency };
struct LocalPlanPerformance {
    uint64_t fullPlans,segmentPlans,visitedSources,visitedOps,unchangedSegments,indexSources;
    double segmentUs,maxSegmentUs,batchUs,indexUs,maxPlanUs,maxRecordUs;
};
static_assert(sizeof(LocalPlanPerformance)==96,"M4n append-only diagnostic ABI");
struct NativeStagePerformance { double us[8];uint64_t samples,overflow; };
// Per category (memory categories 0..8, 9 = Unity textures bound by live draws): bytes by DXGI residency.
struct ResidencyInfo { uint64_t resident[10],shared[10],evicted[10],count[10]; uint64_t queries,failures; };
static_assert(sizeof(NativeStagePerformance)==80,"M4n GPU breakdown ABI");
// M4m: never enlarge CachePerformance (old callers own that payload).
struct CacheDetails {
    uint64_t sharedBindings,privateBindings,frameEvaluations,drawEvaluations,frameBytes,drawBytes;
    uint64_t sameLists,sameRuns,orderPlans,batchPlans,directDraws,indirectDraws,cullItems,materialDetaches;
    uint64_t variableEvaluations[18],variableChanges[18]; // 0 material, 1..17 Patch.kind
};
static_assert(sizeof(CacheDetails)==400,"M4m diagnostic ABI");
struct CacheTune { uint32_t indirectBatches; uint32_t directSingles; };
struct CacheConfiguration { uint32_t enabled,chunkSize; };
struct CachePerformance {
    uint64_t available,segments,replays,records,planBuilds,constantPages,constantBytes,constantEvaluations,cullDispatches,indirectDraws,fallbackSegments;
    double applyUs,poseUs,constantsUs,cullUs,planUs,recordUs,executeUs;
    uint64_t rectTests,rectMismatches,rectFallbacks,rectCopies,grabGroups,grabMerged;
};
// M4k append-only payloads: old callers and export ordinals remain unchanged.
struct ResourceConfiguration { uint32_t skipGrab,pretransform,stableUploads,trimBindings; };
struct ResourcePerformance { uint64_t discardMaps,noOverwriteMaps,defaultUpdates,defaultBytes,grabSkips,srvUnbinds,constantCandidates,constantVisible,ringRotations,retiredDraws; };
struct DrawProbeDetail { uint32_t index;double prepareGpuUs,prepareConstantsUs,grabUs; };
struct DrawProbeState { uint64_t lastVisible,maxVisible,emptyWindows,sampledSegments,completedSegments; };
struct DrawProbeConfiguration { uint32_t enabled,first,count,interval; };
struct DrawProbeRow {
    uint64_t frame,usage,budget;double us,segmentUs;
    uint32_t segment,ordinal,draw,first,count,indices;
};
struct DrawProbeReadback { uint32_t index,total;uint64_t pending,skipped,invalid,overflow;DrawProbeRow row; };
struct TextureProbeInfo {
    uint32_t draw,index,total,stage,slot,samplerSlot,width,height,mips,resourceFormat,viewFormat,mostDetailedMip,viewMips,filter,wrapU,wrapV,aniso;
    float mipBias,minLod,maxLod;
};
struct Configuration { uint32_t submission,constantRing; }; // 0 immediate, 1 deferred; before first event only
struct SubmissionPerformance {
    uint64_t immediateSegments,deferredSegments,stateRestores,orderBytes,orderDispatches,ringMaps,ringBytes,ringBindings,immediateAvailable,ringAvailable;
    double stateUs,recordUs;
};
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
// Kinds 16/17 preserve per-source RendererColor/Flip and normalize them only in a verified batch.
struct Patch { uint32_t offset,kind; };
struct Texture { void* resource; int32_t stage,slot,sampler,filter,wrapU,wrapV,aniso,srgb; };
struct DrawIn {
    uint32_t program,geometry; const Buffer* buffers; uint32_t bufferCount;
    const Patch* patches; uint32_t patchCount; const Texture* textures; uint32_t textureCount;
    const void* constants; uint32_t constantBytes; uint32_t pose;
    uint32_t grab,sprite; // grab: 0 none, 1 unnamed/per-draw, >=2 frame/name ID; sprite bits: 1 color, 2 verified batch, 4 verified screen Grab.
    float center[3],extent[3];
};
struct DrawReplacement { uint32_t previous; DrawIn draw; };
struct Pose { float world[16],inverse[16]; };
struct Frame {
    float vp[16],view[16],projection[16];
    float camera[4],screen[4],projectionParams[4],ortho[4],time[4],sinTime[4],cosTime[4],delta[4];
    const Pose* poses; const uint32_t* poseIds; uint32_t poseCount;
    const uint32_t* drawIds; const uint32_t* constantOffsets; const uint8_t* constants; uint32_t dirtyCount,constantBytes;
    const uint32_t* list; const uint32_t* segments; uint32_t listCount,segmentCount,listChanged,clearStencil;
};
struct CpuGeometry { uint32_t geometry; const void* vertices; uint32_t count; const uint32_t* indices; uint32_t indexCount; };
// CommitActiveWorld borrows tightly packed world[16] matrices in Frame.poses.
// Caller guarantees active programs do not need inverse matrices; old Frame/Commit remain unchanged.
struct FrameActive { Frame frame; const uint32_t* ids; const uint8_t* values; uint32_t count; };
struct BatchPerformance { uint64_t batches,sources,vertices,singleDraws,liveEdits,constantCreates; double buildUs; uint64_t gpuDispatches,gpuPoseUploads,gpuGeometryUploads,gpuAvailable; };
// Appended query: never enlarge an existing caller-owned ABI payload.
struct UploadPerformance {
    uint64_t geometryBytes,poseBytes,drawBytes,indexBytes,constantBytes,colorBytes,cpuBatchBytes;
    uint64_t poseRanges,drawRanges,indexUploads,constantUploads;
    uint64_t singleUnverified,singleGeometry,singleProgram,singleConstants,singleTextures,singleSegment,singleLimit;
    uint64_t workBytes,bakedVertices;
};
struct GpuPerformance { uint64_t grabCopies,grabBytes,namedGrabHits,culledDraws,culledSources; double eventUs,maxEventUs; uint64_t samples,pending,disjoint; };
// Append-only ABI. All category/phase values use the SAME completed camera frames.
// Auxiliary is a separate scope and is never subtracted from cameraUs.
struct TimelinePerformance {
    double cameraUs,nativeUs,explicitUs,compositeUs,otherUs,auxiliaryUs;
    double opaqueUs,gapUs,alphaUs,postAlphaUs,effectsUs,tailUs,maxCameraUs;
    uint64_t samples,auxiliarySamples,pending,disjoint,invalid,overflow,skipped,intervals;
    uint64_t constantEvaluations,constantReuses,constantBindSkips,emptySegments,visibleGrabs,culledGrabs;
    double lastCameraUs;
    uint64_t lastUsage,lastBudget,lastNonLocal,lastSerial,maxUsage,maxBudget,maxNonLocal,maxSerial;
};
struct MemoryPerformance {
    uint64_t ownedBytes,peakOwnedBytes,geometryBytes,poseBytes,drawBytes,indexBytes,pretransformBytes,constantBytes,colorBytes,grabBytes,canvasColorBytes;
    uint64_t beforeUsage,beforeBudget,preparedUsage,preparedBudget,currentUsage,currentBudget,peakUsage,nonLocalUsage,nonLocalBudget,queries,failures,supported;
    uint64_t overBudgetSamples,maxOverBudgetBytes,minHeadroomBytes;
};
struct Statistics { uint64_t frames,draws,errors,dropped,bytes,events; double renderUs,commitUs; int32_t lastHR; };
}
extern "C" intptr_t __cdecl sf_z_drfull_v1(int op,int token,const void* args) noexcept;
