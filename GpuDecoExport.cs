#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;
using Vertex = StutterFix.GpuDecoGpu.Vertex;
using LiveChannel = StutterFix.GpuDecoGpu.LiveChannel;
using Anchor = StutterFix.GpuDecoGpu.Anchor;
using GpuSegment = StutterFix.GpuDecoGpu.Segment;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static bool gpuExportOwnedLow;
        static int gpuExports;
        static Stopwatch gpuPrepareWatch;
        static bool gpuCapture;
        static int gpuCaptureEpoch;
        static ffxMoveDecorationsPlus gpuSource;
        static string gpuBundlePath;
        static readonly float[] gpuFrameIntervals = new float[256];
        static readonly float[] gpuProgressSteps = new float[256];
        static int gpuFrameCount, gpuFrameCursor;
        static double gpuFrameSum, gpuProgressSum;
#if AUTOTEST
        static GpuDecoData gpuLastSnapshot;
        static int gpuSnapshotGeneration = -1;
        static bool gpuLastG0;
        static GpuDecoPixelGate gpuPixelGate = new GpuDecoPixelGate();
#endif
        sealed class GpuLiveRow
        {
            internal Renderer R;
            internal Material M;
            internal int Item = -1;
            internal SceneRenderOrder.Key Key;
            internal string Signature;
        }
        // Request B: Shared per-effect source clock (one per active MoveDecorations invocation).
        sealed class GpuLiveSource
        {
            internal int Slot;
            internal long Token;
            internal int SourceId;
            internal int StartFrame;
            internal double StartSong;
            internal float Dur;
            internal float Pos;
            internal Ease Ease;
            internal float Over, Period;
            internal float CurrentE;
            internal bool Active;
            internal readonly List<int> MasterSlots = new List<int>(64);
        }
        sealed class GpuLiveSlice
        {
            bool active;
            bool[] alphaDirty;
            internal GpuDecoCopyMask CopyMask;
            readonly List<int> pendingAlpha=new List<int>();
            // Shared static vertex storage still retains one renderer and sort
            // center per item. Visibility must remain independently selectable.
            internal bool Independent=>GpuDecoNativeBatch.Enabled && Renderers.Count==MasterItems.Length;
            bool HiddenAlpha(int item)
            {
                var a=Channels[item*16+10];var b=Channels[item*16+11];
                return GpuDecoAlphaPolicy.Hidden(a.end,a.sourceIndex,b.end,b.sourceIndex);
            }
            void ApplyAlpha(int item)
            {
                var r=Renderers[item];if(!r)return;
                bool wanted=active && !gpuRenderMergeOn && (!Independent || !GpuCpuFallback(MasterItems[item])) && (!gpuZeroAlpha || !Independent || !HiddenAlpha(item));
                CopyMask?.Set(item,wanted);
                if(r.enabled!=wanted){if(GpuDecoCostProbe.Enabled)GpuDecoCostProbe.AlphaToggle();r.enabled=wanted;}
            }
            internal void RefreshAlpha()
            {
                for(int i=0;i<Renderers.Count;i++)ApplyAlpha(i);
                if(alphaDirty!=null)Array.Clear(alphaDirty,0,alphaDirty.Length);
                pendingAlpha.Clear();
            }
            internal const int DirtyBlockSize = 256;
            internal double Start, End;
            internal GpuDecoData Data;
            internal GpuDecoGpu Gpu;
            internal CommandBuffer Commands;
            internal GpuDecoCopy NativeCopy;
            internal GpuDecoNativeBatch SharedBatch;
            internal int[] MasterItems = Array.Empty<int>();
            internal LiveChannel[] Channels = Array.Empty<LiveChannel>();
            internal Anchor[] FallbackAnchors = Array.Empty<Anchor>();
            internal bool[] DirtyBlocks = Array.Empty<bool>();
            internal int DirtyBlockMin = int.MaxValue, DirtyBlockMax = -1;
            internal readonly List<Mesh> Meshes = new List<Mesh>();
            internal readonly List<GraphicsBuffer> Buffers = new List<GraphicsBuffer>();
            internal readonly List<MeshRenderer> Renderers = new List<MeshRenderer>();
            internal int RunCount, SubmeshCount;
            internal void InitChannels(int channelCount)
            {
                Channels = new LiveChannel[channelCount];
                alphaDirty=new bool[channelCount/16];
                FallbackAnchors = new Anchor[channelCount];
                int blocks = (channelCount + DirtyBlockSize - 1) / DirtyBlockSize;
                DirtyBlocks = new bool[blocks];
                DirtyBlockMin = int.MaxValue;
                DirtyBlockMax = -1;
            }
            internal void MarkDirty(int slot)
            {
                if((gpuZeroAlpha || gpuRenderMergePrepared) && ((slot&15)==10 || (slot&15)==11))
                {
                    int item=slot/16;
                    if((uint)item<(uint)alphaDirty.Length && !alphaDirty[item]) {alphaDirty[item]=true;pendingAlpha.Add(item);}
                }
                int b = slot >> 8;
                if ((uint)b >= (uint)DirtyBlocks.Length) return;
                DirtyBlocks[b] = true;
                if (b < DirtyBlockMin) DirtyBlockMin = b;
                if (b > DirtyBlockMax) DirtyBlockMax = b;
            }
            internal void FlushDirty()
            {
                if(pendingAlpha.Count!=0)
                {
                    if(active && Independent)foreach(int item in pendingAlpha)ApplyAlpha(item);
                    if(active && gpuRenderMergeOn)foreach(int item in pendingAlpha)RefreshGpuMergeItem(item);
                    if(active && gpuRenderMergeOn)FlushGpuMergeGroups();
                    foreach(int item in pendingAlpha)alphaDirty[item]=false;
                    pendingAlpha.Clear();
                }
                if (Gpu == null || DirtyBlockMin > DirtyBlockMax) return;
                int n = Channels.Length;
                int b = DirtyBlockMin;
                while (b <= DirtyBlockMax)
                {
                    if (!DirtyBlocks[b]) { b++; continue; }
                    int bStart = b;
                    DirtyBlocks[b++] = false;
                    while (b <= DirtyBlockMax && DirtyBlocks[b])
                        DirtyBlocks[b++] = false;
                    int slotStart = bStart * DirtyBlockSize;
                    int slotEnd = Math.Min(n, b * DirtyBlockSize) - 1;
                    if (slotStart <= slotEnd)
                    {
                        if(GpuDecoCostProbe.Enabled)GpuDecoCostProbe.Upload(slotEnd-slotStart+1,Gpu.HasNativeLiveKernel?16:48);
                        Gpu.UploadDirtyLiveChannels(Channels, FallbackAnchors, slotStart, slotEnd);
                    }
                }
                DirtyBlockMin = int.MaxValue;
                DirtyBlockMax = -1;
            }
            internal void SetEnabled(bool on)
            {
                active=on;RefreshAlpha();
                if(!on)HideGpuMerge();
            }
            internal void Dispose()
            {
                SetEnabled(false);
                Renderers.Clear();
                try { NativeCopy?.Dispose(); } catch { }
                NativeCopy=null;
                if(SharedBatch!=null)
                {
                    SharedBatch.Dispose();SharedBatch=null;
                    // The batch owns these meshes; they are listed here for
                    // resource observation, not a second disposal owner.
                    Meshes.Clear();
                }
                try { Commands?.Dispose(); } catch { }
                Commands = null;
                for (int i = Buffers.Count - 1; i >= 0; i--)
                    try { Buffers[i]?.Dispose(); } catch { }
                Buffers.Clear();
                try { Gpu?.Dispose(); } catch { }
                Gpu = null;
                for (int i = Meshes.Count - 1; i >= 0; i--)
                    if (Meshes[i]) UnityEngine.Object.Destroy(Meshes[i]);
                Meshes.Clear();
            }
        }

        // Whole-song simultaneous effects exceed the old 2048-slot window
        // budget. Fixed preparation-time storage; never recycle a live clock.
        const int MaxLiveSources = 8192;
        static GpuDecoScopePlan gpuScope = GpuDecoScopePlan.Reference();
        static GpuDecoScopePlan gpuPreparedScope;
        static readonly List<GpuLiveSlice> gpuLiveSlices = new List<GpuLiveSlice>();
        static readonly HashSet<scrDecoration> gpuLiveOwnedSet = new HashSet<scrDecoration>();
        static readonly HashSet<scrVisualDecoration> gpuLiveOwnedVisuals = new HashSet<scrVisualDecoration>();
        static readonly Dictionary<scrDecoration, int> gpuLiveMasterByDeco = new Dictionary<scrDecoration, int>();
        static int[] gpuLiveOwnedIndices = Array.Empty<int>();
        static SpriteRenderer[] gpuLiveOwnedRenderers = Array.Empty<SpriteRenderer>();
        static bool[] gpuLiveOriginalOff = Array.Empty<bool>();
        static bool[] gpuLiveExternalOff = Array.Empty<bool>();
        static int[][] gpuLiveMasterToSliceItem = Array.Empty<int[]>();

        // Unified master channel & source state (no binary search; updated only on effect start/end)
        static LiveChannel[] gpuMasterChannels = Array.Empty<LiveChannel>();
        static Anchor[] gpuMasterAnchors = Array.Empty<Anchor>();
        static int[] gpuMasterSlotSource = Array.Empty<int>();
        static long[] gpuMasterRecOrder=Array.Empty<long>(),gpuMasterEngineOrder=Array.Empty<long>();
        static bool[] gpuMasterOrderObserved=Array.Empty<bool>();
        static readonly GpuLiveSource[] gpuSourcePool = new GpuLiveSource[MaxLiveSources];
        static readonly Stack<int> gpuFreeSourceSlots = new Stack<int>(MaxLiveSources);
        static readonly List<int> gpuActiveSourceSlots = new List<int>(256);
        static readonly float[] gpuSourceE = new float[MaxLiveSources];
        static readonly float[] gpuSourceProgress = new float[MaxLiveSources];
        static int gpuSourceHighWater = 1,gpuSourcePeakAllocated;

        // Current effect context during FastMove.Run
        static ffxMoveDecorationsPlus gpuCurrentFx;
        static DecoBakeCore.Event gpuCurrentEvent;
        static GpuLiveSource gpuCurrentAnimSource;
        static bool gpuCurrentFxParsed;

        static GameObject gpuLiveRoot;
        static AssetBundle gpuLiveBundle;
        static ComputeShader gpuLiveShader;
        static Camera gpuLiveCamera;
        static scrDecorationManager gpuLiveManager;
        static scrDecoration[] gpuLiveBaseline;
        static string gpuLiveBundleKey;
        static bool gpuLivePrepared, gpuLiveActive;
        static bool gpuLivePreparedNative;
        static bool gpuLivePreparedStatic,gpuLivePreparedBounds;
        static bool gpuLivePreparedCopy;
        static bool gpuLivePreparedEight;
        static bool gpuLivePreparedMasked;
        static GpuDecoCopy.Totals g2NativeStart;
        static GpuDecoThreadCpu g2ThreadCpu;
        static GpuDecoRenderMetrics g2RenderMetrics;
        static bool gpuZeroAlpha; // explicit research candidate, default off
        static bool gpuCopyMaskBypass; // paused validation only; reset on dispose
        static int gpuLiveCurrentSlice = -1, gpuLiveLastDispatchFrame = -1, gpuLiveLastSourceStepFrame = -1;
        static bool gpuLivePortalObserved;
        static double gpuLivePrepareMs, gpuLiveTransitionMs, gpuLiveTurnOnBakeTime;
        static float gpuLiveEnabledAt, gpuLiveZoomFactor = 1f;
        static long gpuLiveSkippedRecs;
        static long gpuCostFrames, gpuCostProgressTicks, gpuCostUniformTicks, gpuCostDispatchTicks, gpuCostMiscTicks;
        static double gpuCostMaxMs;
        static readonly float[] gpuRestoreValues = new float[16];

        // G2 ABBA + stability gate state
        static int g2Window = -1, g2WindowFrame;
        static long g2WindowClock;
        static bool g2WindowMode;
        static double g2WindowSongStart;
        static long g2CostFrames0, g2ProgressTicks0, g2UniformTicks0, g2DispatchTicks0, g2MiscTicks0;
        static double g2CostMax0;
        static readonly bool[] g2WindowSeen = new bool[4];
        static readonly bool[] g2WindowValid = new bool[4];
        static readonly double[] g2WindowFps = new double[4];
        static readonly double[] g2WindowCostMs = new double[4];
        static readonly double[] g2WindowProgressMs = new double[4];
        static readonly double[] g2WindowUniformMs = new double[4];
        static readonly double[] g2WindowDispatchMs = new double[4];
        static readonly double[] g2WindowMiscMs = new double[4];
        static readonly double[] g2WindowMaxCostMs = new double[4];
        static readonly int[] g2WindowSpikes = new int[4];
        static readonly double[] g2WindowWorstMs = new double[4];
        static bool g2StabPauseResume, g2StabSongEnd, g2StabRetryRestart, g2StabReopenToggle;
        static string g2Error;

        internal static bool GpuLiveActive => gpuLiveActive;
        internal static int GpuLiveOwned => gpuLiveActive ? gpuLiveOwnedSet.Count : 0;
        internal static bool SkipGpuLate(scrDecoration d) => gpuLiveActive && gpuLiveOwnedSet.Contains(d);
        internal static bool GpuLiveOwns(scrVisualDecoration d) => gpuLiveActive && gpuLiveOwnedVisuals.Contains(d);

        // Request B.2: Called from FastMove.Run -> DecoBake.SkipAdopt for each target decoration.
        // Updates the GPU channel table and shared source clock on effect start without creating CPU Recs.
        internal static bool GpuLiveSkip(ffxMoveDecorationsPlus fx, scrDecoration d, int keys)
        {
            if (!gpuLiveActive || !gpuLiveMasterByDeco.TryGetValue(d, out int masterIdx) || GpuCpuFallback(masterIdx)) return false;
            bool captured=false;
            try
            {
#if AUTOTEST
                PreflightGpuImageTarget(fx,d,masterIdx);
#endif
                PrepareGpuEffect(fx);SaveGpuTarget(masterIdx,d);captured=true;
                ApplyGpuLiveEffectToTarget(fx, masterIdx, d);
                gpuLiveSkippedRecs+=keys;return true;
            }
            catch(Exception ex)
            {
                if(captured)
                    try {RollbackGpuTarget();}
                    catch(Exception rollback)
                    {
                        GpuLog("recovery_target_rollback_rejected="+rollback.GetType().Name+" continuity=DEGRADED");
                        // A partially rolled-back target cannot form a valid
                        // continuation plan. Release ownership without guessing.
                        StopGpuLive("target_rollback_rejected",false);
                        gpuRecoveryBlocked=true;return false;
                    }
                RecoverGpuLive("effect:"+ex.GetType().Name,!(ex is NotSupportedException));
                // The current/remaining targets go through normal FastMove once.
                // Already committed earlier targets are restored as CPU records.
                return false;
            }
            finally {gpuTargetUndoCount=0;gpuTargetUndoDecoration=null;}
        }

        internal static void InvalidateGpuSnapshot()
        {
#if AUTOTEST
            GpuDecoSnapshot.Stop(); GpuDecoSnapshot.SetDiagnostic("off"); gpuLastSnapshot = null; gpuSnapshotGeneration = -1; gpuLastG0 = false;
#endif
        }
        internal static void ResetGpuFrames() { gpuFrameCount = gpuFrameCursor = 0; gpuFrameSum = gpuProgressSum = 0; }
        internal static void ObserveGpuFrame()
        {
#if AUTOTEST
            ObserveGpuMergeFrame();
#endif
            float rawDt = Time.unscaledDeltaTime;
            if (g2Window >= 0 && !FrameCompare.Paused && Hitch.Playing && Time.frameCount > g2WindowFrame && rawDt > 0 && DecoBakeCore.Finite(rawDt))
            {
                double ms = rawDt * 1000.0;
                if (ms < 500.0)
                {
                    if (ms > 30.0) g2WindowSpikes[g2Window]++;
                    if (ms > g2WindowWorstMs[g2Window]) g2WindowWorstMs[g2Window] = ms;
                }
            }
            // This method is called only AFTER DecoAnim's paused early-return.
            // Hitch.Playing is the previous monitor tick, not an end event;
            // the old "15 frames since pause" value was never updated in pause.
            if (!gpuCapture || !ADOBase.controller || ADOBase.controller.paused || Time.timeScale == 0) return;
            float dt = rawDt; if (dt <= 0 || !DecoBakeCore.Finite(dt)) return;
            float step = (DOTween.useSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime) * DOTween.timeScale;
            if (step < 1e-6f || !DecoBakeCore.Finite(step)) return;
            if (gpuFrameCount == gpuFrameIntervals.Length) { gpuFrameSum -= gpuFrameIntervals[gpuFrameCursor]; gpuProgressSum -= gpuProgressSteps[gpuFrameCursor]; } else gpuFrameCount++;
            gpuProgressSteps[gpuFrameCursor] = step; gpuProgressSum += step;
            gpuFrameIntervals[gpuFrameCursor] = dt; gpuFrameSum += dt; gpuFrameCursor = (gpuFrameCursor + 1) % gpuFrameIntervals.Length;
        }
        internal static void NotifyGpuPlaybackEnded(string method)
        {
            if(!gpuLiveActive)return;
            gpuLivePortalObserved=method=="OnLandOnPortal";
            GpuLog("lifecycle_end source=existing_game_callback method="+method+" song="
                +(scrConductor.instance?scrConductor.instance.songposition_minusi:double.NaN).ToString("F6",Inv));
            StopGpuLive("GameCallback:"+method,method=="OnLandOnPortal");
        }
        internal static void BeginGpuSource(ffxMoveDecorationsPlus fx)
        {
            gpuSource = (enabled || gpuCapture || gpuLivePrepared || gpuLiveActive) ? fx : null;
            gpuCurrentFx = fx;
            gpuCurrentFxParsed = false;
            gpuCurrentEvent = null;
            gpuCurrentAnimSource = null;
        }
        internal static void EndGpuSource()
        {
            gpuPacketCurrent=null;
            gpuPacketTargetExpected=null;
            gpuSource = null;
            gpuCurrentFx = null;
            gpuCurrentFxParsed = false;
            gpuCurrentEvent = null;
            gpuCurrentAnimSource = null;
        }
        internal static void StampGpuRec(DecoAnim.Rec r)
        {
            r.GpuOrder=DecoAnim.NextGpuCreationOrder();
            r.GpuOrderObserved=true;
            r.GpuStartFrame = -1; r.GpuSource = 0; r.GpuStartSong = 0;
            r.GpuGeneration = -1;
            r.GpuCaptureEpoch = -1;
            if (!gpuSource) return;
            r.GpuSource = gpuSource.GetInstanceID(); r.GpuStartFrame = Time.frameCount;
            r.GpuGeneration = Generation;
            r.GpuCaptureEpoch = gpuCaptureEpoch;
            r.GpuStartSong = scrConductor.instance.songposition_minusi;
        }
        static void CheckGpuPrepareBudget()
        {
            if (gpuPrepareWatch == null || !gpuPrepareWatch.IsRunning || gpuPrepareWatch.Elapsed.TotalSeconds < 300) return;
            GpuLog("GPUDECO_G1=ERROR stage=prepare prepare_ms=" + gpuPrepareWatch.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture)
                + " reason=first_export_budget_300s status=" + status);
            gpuPrepareWatch.Stop();
            throw new InvalidOperationException("gpudeco: open부터 첫 export까지 300초 예산 초과");
        }

        static void RequireLive(bool ok, string reason)
        {
            if (!ok) throw new InvalidOperationException("gpudeco G2: " + reason);
        }

        // ── Request B.2: Source Pool & Incremental Channel Table Management ──
        static void ResetGpuLiveSourcePool()
        {
            gpuActiveSourceSlots.Clear();
            gpuFreeSourceSlots.Clear();
            for (int i = MaxLiveSources - 1; i >= 0; i--)
            {
                if (gpuSourcePool[i] == null) gpuSourcePool[i] = new GpuLiveSource { Slot = i };
                else { gpuSourcePool[i].Active = false; gpuSourcePool[i].MasterSlots.Clear(); }
                gpuFreeSourceSlots.Push(i);
                gpuSourceE[i] = 0f;
                gpuSourceProgress[i] = 0f;
            }
            gpuSourceHighWater = 1;gpuSourcePeakAllocated=0;
        }

        static GpuLiveSource AllocateGpuLiveSource(int sourceId, int startFrame, float dur, Ease ease, float over, float period, float initialPos)
        {
            if (gpuFreeSourceSlots.Count == 0)
            {
                // Reusing a still-referenced source jumps its animation to the
                // endpoint. Only abandoned sources can be recycled mid-frame.
                int reclaim=-1;
                for(int i=0;i<gpuActiveSourceSlots.Count;i++)
                    if(gpuSourcePool[gpuActiveSourceSlots[i]].MasterSlots.Count==0) {reclaim=i;break;}
                if(reclaim<0)
                {
                    GpuLog("source_capacity_exhausted capacity="+MaxLiveSources+" allocated="+gpuActiveSourceSlots.Count
                        +" referenced="+gpuActiveSourceSlots.Count(s=>gpuSourcePool[s].MasterSlots.Count>0));
                    throw new InvalidOperationException("gpudeco source_pool_exhausted: no unreferenced source; unfinished animation retained");
                }
                CompleteGpuLiveSource(gpuSourcePool[gpuActiveSourceSlots[reclaim]]);
                gpuActiveSourceSlots.RemoveAt(reclaim);
            }
            int slot = gpuFreeSourceSlots.Pop();
            if (slot + 1 > gpuSourceHighWater) gpuSourceHighWater = slot + 1;
            var s = gpuSourcePool[slot];
            s.SourceId = sourceId;
            s.StartFrame = startFrame;
            s.StartSong=gpuSource && scrConductor.instance?scrConductor.instance.songposition_minusi:0;
            s.Dur = dur;
            s.Pos = initialPos;
            s.Ease = ease;
            s.Over = over;
            s.Period = period;
            s.CurrentE = dur > 0f && initialPos > 0f
                ? (initialPos >= dur ? 1f : ZeroTween.Eval(ease, initialPos, dur, over, period))
                : 0f;
            s.Active = true;
            s.MasterSlots.Clear();
            gpuSourceE[slot] = s.CurrentE;
            gpuSourceProgress[slot] = initialPos;
            gpuActiveSourceSlots.Add(slot);
            gpuSourcePeakAllocated=Math.Max(gpuSourcePeakAllocated,gpuActiveSourceSlots.Count);
            UpdatePacketClock(s);
            return s;
        }

        // Evaluates the exact current float value of a master channel slot (matching DecoAnim.Apply).
        static float EvalMasterSlotCurrent(int masterSlot)
        {
            var ch = gpuMasterChannels[masterSlot];
            if (ch.sourceIndex < 0) return ch.end;
            var src = gpuSourcePool[ch.sourceIndex];
            if (!src.Active || src.Pos >= src.Dur) return ch.end;
            if (src.Pos <= 0f) return ch.start;
            return (float)((double)ch.start + (double)ch.change * (double)src.CurrentE);
        }

        static void PropagateMasterSlotToSlices(int masterIdx, int c, LiveChannel lc, Anchor anc)
        {
            var map = gpuLiveMasterToSliceItem[masterIdx];
            for (int s = 0; s < gpuLiveSlices.Count; s++)
            {
                int sliceItem = map[s];
                if (sliceItem < 0) continue;
                int sliceSlot = sliceItem * 16 + c;
                var slice = gpuLiveSlices[s];
                slice.Channels[sliceSlot] = lc;
                // The immutable live-kernel path uploads only LiveChannel (16 B).
                // Keep the old anchor mirror for legacy bundles/preparation only;
                // master anchors above remain authoritative for CPU restoration.
                if(slice.Gpu==null || !slice.Gpu.HasNativeLiveKernel)
                    slice.FallbackAnchors[sliceSlot] = anc;
                slice.MarkDirty(sliceSlot);
            }
        }

        static void DetachMasterSlotFromSource(int masterSlot)
        {
            int oldSlot = gpuMasterSlotSource[masterSlot];
            if (oldSlot < 0) return;
            gpuMasterSlotSource[masterSlot] = -1;
            var src = gpuSourcePool[oldSlot];
            if(GpuDecoCostProbe.Enabled)
            {
                // Same order and removal semantics as List.Remove, with one
                // search (not a second diagnostic search) and no clock reads.
                int index=src.MasterSlots.IndexOf(masterSlot);
                GpuDecoCostProbe.SourceRemoval(index,src.MasterSlots.Count);
                if(index>=0)src.MasterSlots.RemoveAt(index);
            }
            else src.MasterSlots.Remove(masterSlot);
        }

        static void SetMasterSlotStatic(int masterIdx, int c, float value)
        {
            int masterSlot = masterIdx * 16 + c;
            DetachMasterSlotFromSource(masterSlot);
            var lc = new LiveChannel { start = value, change = 0f, end = value, sourceIndex = -1 };
            var anc = new Anchor
            {
                valid = 1u,
                clock = 0u,
                segment = new GpuSegment { duration = 0f, start = value, end = value, target = value, ease = 1u, scale = (c == 5 || c == 6 || c == 12 || c == 13) ? 1u : 0u }
            };
            gpuMasterChannels[masterSlot] = lc;
            gpuMasterAnchors[masterSlot] = anc;
            PropagateMasterSlotToSlices(masterIdx, c, lc, anc);
        }

        static void SetMasterSlotAnimated(int masterIdx, int c, float start, float target, float end, GpuLiveSource src, bool isScale)
        {
            int masterSlot = masterIdx * 16 + c;
            DetachMasterSlotFromSource(masterSlot);
            float change = target - start;
            gpuMasterSlotSource[masterSlot] = src.Slot;
            src.MasterSlots.Add(masterSlot);
            var lc = new LiveChannel { start = start, change = change, end = end, sourceIndex = src.Slot };
            var anc = new Anchor
            {
                valid = 1u,
                clock = (uint)src.Slot,
                segment = new GpuSegment
                {
                    duration = src.Dur, start = start, end = end, target = target,
                    over = src.Over, period = src.Period, ease = (uint)src.Ease, scale = isScale ? 1u : 0u
                }
            };
            gpuMasterChannels[masterSlot] = lc;
            gpuMasterAnchors[masterSlot] = anc;
            PropagateMasterSlotToSlices(masterIdx, c, lc, anc);
        }

        static void CompleteGpuLiveSource(GpuLiveSource src)
        {
            if (!src.Active) return;
            src.Active = false;
            src.Pos = src.Dur;
            src.CurrentE = 1f;
            gpuSourceE[src.Slot] = 1f;
            gpuSourceProgress[src.Slot] = src.Dur;
            UpdatePacketClock(src);
            for (int i = 0; i < src.MasterSlots.Count; i++)
            {
                int masterSlot = src.MasterSlots[i];
                if (gpuMasterSlotSource[masterSlot] != src.Slot) continue;
                gpuMasterSlotSource[masterSlot] = -1;
                float endVal = gpuMasterChannels[masterSlot].end;
                int masterIdx = masterSlot / 16, c = masterSlot % 16;
                var lc = new LiveChannel { start = endVal, change = 0f, end = endVal, sourceIndex = -1 };
                var anc = new Anchor
                {
                    valid = 1u, clock = 0u,
                    segment = new GpuSegment { duration = 0f, start = endVal, end = endVal, target = endVal, ease = 1u, scale = (c == 5 || c == 6 || c == 12 || c == 13) ? 1u : 0u }
                };
                gpuMasterChannels[masterSlot] = lc;
                gpuMasterAnchors[masterSlot] = anc;
                PropagateMasterSlotToSlices(masterIdx, c, lc, anc);
            }
            src.MasterSlots.Clear();
            gpuFreeSourceSlots.Push(src.Slot);
        }

        // Request B.1 & B.2: Advance all active sources once per frame by shared dt and evaluate double easing on CPU.
        static void AdvanceGpuLiveSources()
        {
            if (gpuLiveLastSourceStepFrame == Time.frameCount) return;
            gpuLiveLastSourceStepFrame = Time.frameCount;
            if (!ADOBase.controller || ADOBase.controller.paused || Time.timeScale == 0) return;
            float dt = (DOTween.useSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime) * DOTween.timeScale;
            if (dt < 1e-6f && dt > -1e-6f) return;
            if (!DecoBakeCore.Finite(dt) || dt <= 0f) return;
            int fc = Time.frameCount;
            for (int i = 0; i < gpuActiveSourceSlots.Count;)
            {
                int slot = gpuActiveSourceSlots[i];
                var src = gpuSourcePool[slot];
                if (!src.Active || src.MasterSlots.Count == 0)
                {
                    CompleteGpuLiveSource(src);
                    int last = gpuActiveSourceSlots.Count - 1;
                    gpuActiveSourceSlots[i] = gpuActiveSourceSlots[last];
                    gpuActiveSourceSlots.RemoveAt(last);
                    continue;
                }
                // Sources created in scrVfxPlus.Update during frame F take their first Pos += dt step on frame F + 1,
                // matching DOTweenComponent.Update -> DecoAnim.UpdatePrefix ordering.
                if (src.StartFrame >= fc)
                {
                    i++;
                    continue;
                }
                float nextPos = src.Pos + dt;
                if (nextPos >= src.Dur)
                {
                    CompleteGpuLiveSource(src);
                    int last = gpuActiveSourceSlots.Count - 1;
                    gpuActiveSourceSlots[i] = gpuActiveSourceSlots[last];
                    gpuActiveSourceSlots.RemoveAt(last);
                    continue;
                }
                src.Pos = nextPos;
                float e = ZeroTween.Eval(src.Ease, nextPos, src.Dur, src.Over, src.Period);
                src.CurrentE = e;
                gpuSourceE[slot] = e;
                gpuSourceProgress[slot] = nextPos;
                i++;
            }
        }

        static void ApplyGpuLiveEffectToTarget(ffxMoveDecorationsPlus fx, int masterIdx, scrDecoration d)
        {
            PrepareGpuEffect(fx);
            var e = gpuCurrentEvent;
            int baseIdx = gpuLiveOwnedIndices[masterIdx];
            var decRow = table.Decorations[baseIdx];
            if (e.ResetGlobalOrigin)
            {
                d.placementType = DecPlacementType.Global;
                d.startPos = new Vector2(decRow.GlobalX, decRow.GlobalY);
            }
            bool anim = e.Duration > 0f && gpuCurrentAnimSource != null;
            float easeEnd = gpuEffectCache ? gpuEffectEaseEnd : (!anim && (!float.IsNaN(e.Values[5]) || !float.IsNaN(e.Values[6]) || !float.IsNaN(e.Values[12]) || !float.IsNaN(e.Values[13])))
                ? ZeroTween.Eval((Ease)e.Ease, 1f, 1f, e.Over, e.Period)
                : 1f;
            float easeDurEnd = gpuEffectCache ? gpuEffectEaseDurEnd : anim && (!float.IsNaN(e.Values[5]) || !float.IsNaN(e.Values[6]) || !float.IsNaN(e.Values[12]) || !float.IsNaN(e.Values[13]))
                ? ZeroTween.Eval((Ease)e.Ease, e.Duration, e.Duration, e.Over, e.Period)
                : 1f;

            for (int i=0,n=gpuEffectCache?gpuEffectChannelCount:16;i<n;i++)
            {
                int c=gpuEffectCache?gpuEffectChannels[i]:i;
                float rawTarget = e.Values[c];
                if (!gpuEffectCache&&float.IsNaN(rawTarget)) continue;
                int masterSlot = masterIdx * 16 + c;
                // In DOTween/FastMove: relative target is captured BEFORE Kill(true), and start is read AFTER Kill(true) completes the previous tween.
                float currentBeforeKill = EvalMasterSlotCurrent(masterSlot);
                float afterKill = gpuMasterChannels[masterSlot].end;
                float target = rawTarget;
                if (c < 2)
                {
                    float origin = e.RelativePosition ? currentBeforeKill : (c == 0 ? d.startPos.x : d.startPos.y);
                    target = origin + rawTarget;
                }
                bool isScaleOrPar = c == 5 || c == 6 || c == 12 || c == 13;
                if (!anim)
                {
                    float finalVal = isScaleOrPar ? afterKill + (target - afterKill) * easeEnd : target;
                    SetMasterSlotStatic(masterIdx, c, finalVal);
                }
                else
                {
                    float startVal = afterKill;
                    float endVal = isScaleOrPar ? startVal + (target - startVal) * easeDurEnd : target;
                    SetMasterSlotAnimated(masterIdx, c, startVal, target, endVal, gpuCurrentAnimSource, isScaleOrPar);
                }
                GpuRecoveryFault("target_after_channel");
            }
            // Original FastMove constructor order, including parallax offsets
            // before pivot/rotation. Render-item order is never used here.
            if(anim)foreach(int c in gpuRecFirstChannels)
                if(!float.IsNaN(e.Values[c]))StampGpuMasterRecOrder(masterIdx,c);
        }

        static readonly int[] gpuRecFirstChannels={0,1,14,15,2,3,4,5,6,7,11,12};
        static void StampGpuMasterRecOrder(int master,int channel,long order=0,long engine=0,bool observed=true)
        {
            int n=channel==7?4:channel==12?2:1;
            if(order==0){order=DecoAnim.NextGpuCreationOrder();engine=DecoEngine.NextOrder();}
            for(int c=channel;c<channel+n;c++)
            {gpuMasterRecOrder[master*16+c]=order;gpuMasterEngineOrder[master*16+c]=engine;gpuMasterOrderObserved[master*16+c]=observed;}
        }

        static bool ChannelPositiveInWindow(DecoBakeCore.Table t, int index, int ch, double a, double b)
        {
            var track = t.Tracks[index * 16 + ch];
            float init = t.Decorations[index].Initial[ch];
            if (track.Length == 0) return init > 0f;
            int idxA = DecoBakeCore.Find(track, a);
            if (idxA < 0)
            {
                if (init > 0f) return true;
            }
            else
            {
                var s0 = track[idxA];
                double end0 = s0.T0 + (double)s0.Duration * t.Pitch / t.TweenScale;
                if (a < end0) { if (s0.Start > 0f || s0.Target > 0f || s0.End > 0f) return true; }
                else if (s0.End > 0f) return true;
            }
            for (int i = Math.Max(0, idxA); i < track.Length; i++)
            {
                var s = track[i];
                if (s.T0 > b) break;
                double end = s.T0 + (double)s.Duration * t.Pitch / t.TweenScale;
                if (end >= a && (s.Start > 0f || s.Target > 0f || s.End > 0f)) return true;
            }
            return false;
        }
        static bool VisibleInWindow(DecoBakeCore.Table t, int index, double a, double b)
            => ChannelPositiveInWindow(t, index, 10, a, b) && ChannelPositiveInWindow(t, index, 11, a, b);

        // Preparation-only locality hint, NOT an admission/culling rule. Renderer
        // creation/order and source vertex indices stay unchanged. Even a wrong
        // hint merely copies more chunks; actual alpha selects every frame.
        static double CopyLocalityHint(int index)
        {
            double a=double.PositiveInfinity,b=double.PositiveInfinity;
            for(int c=10;c<=11;c++)
            {
                double first=ChannelPositiveInWindow(table,index,c,.5,.5)? .5:double.PositiveInfinity;
                foreach(var s in table.Tracks[index*16+c])
                    if(s.T0>.5 && (s.Start>0 || s.Target>0 || s.End>0)){first=Math.Min(first,s.T0);break;}
                if(c==10)a=first;else b=first;
            }
            return Math.Max(a,b);
        }

        static SceneRenderOrder.Key CandidateSortKey(scrVisualDecoration d, SpriteRenderer r, Material m, Camera cam)
        {
            if (!r.forceRenderingOff) return MatShare.SortKey(r, m, cam, 0);
            Vector3 pos = d.pivotTrans.position;
            float distance;
            var mode = cam.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = cam.orthographic ? TransparencySortMode.Orthographic : TransparencySortMode.Perspective;
            if (mode == TransparencySortMode.CustomAxis)
                distance = Vector3.Dot(pos, cam.transparencySortMode == TransparencySortMode.CustomAxis ? cam.transparencySortAxis : GraphicsSettings.transparencySortAxis);
            else
                distance = mode == TransparencySortMode.Orthographic ? Vector3.Dot(pos - cam.transform.position, cam.transform.forward) : (pos - cam.transform.position).sqrMagnitude;
            return new SceneRenderOrder.Key(SortingLayer.GetLayerValueFromID(r.sortingLayerID), r.sortingOrder,
                m ? m.renderQueue : -1, distance, m ? m.GetInstanceID() : 0, r.GetInstanceID(), 0);
        }
        static string MaterialSignature(Material m)
        {
            var props = MatShare.Properties(m);
            if (props.TryGetValue("_MainTex", out var val))
            {
                int colon = val.IndexOf(':');
                if (colon >= 0) props["_MainTex"] = "tex" + val.Substring(colon);
            }
            return string.Join(";", props.Select(p => p.Key + "=" + p.Value));
        }
        static void PutVec(float[] output, int index, Vector3 v) { output[index] = v.x; output[index + 1] = v.y; output[index + 2] = v.z; }
        static GpuDecoData.Item CloneSliceItem(GpuDecoData.Item src)
        {
            return new GpuDecoData.Item
            {
                Index = src.Index, Id = src.Id, MaterialId = src.MaterialId, TextureId = src.TextureId,
                Layer = src.Layer, Order = src.Order, Flags = src.Flags, Placement = src.Placement,
                Initial = src.Initial, Actual = src.Actual, Tracks = src.Tracks,
                Anchors = new GpuDecoData.Anchor[16],
                Constants = src.Constants, Shape = src.Shape, OffsetBasis = src.OffsetBasis,
                Corners = src.Corners, UV = src.UV, World = src.World, Color = src.Color, Triangles = src.Triangles
            };
        }

        // Request B.4: Pre-playback preparation right after map open & bake ready.
        static void CompleteGpuLivePreparationBudget()
        {
            CheckGpuPrepareBudget();
            if(gpuPrepareWatch==null || !gpuPrepareWatch.IsRunning)return;
            gpuPrepareWatch.Stop();
            GpuLog("prepare_complete stage=live_prepare budget_ms=300000 elapsed_ms="
                +gpuPrepareWatch.Elapsed.TotalMilliseconds.ToString("F6",Inv));
        }
        static void PrepareGpuLive()
        {
            CheckGpuPrepareBudget();
            RequireLive(!GpuDecoNativeBatch.StaticCombine,"static_batch_live_not_validated; use paused storage comparison");
            RequireLive(ready && table != null && baselineObjects != null && manager != null, "decobake ready 필요");
            RequireLive(!string.IsNullOrEmpty(gpuBundlePath) && File.Exists(gpuBundlePath), "gpudeco bundle <path> 필요");
            if (!gpuRecoveryBlocked && gpuLivePrepared && gpuRenderMergePrepared==gpuRenderMergePrepare && ReferenceEquals(gpuPreparedScope,gpuScope) && gpuLivePreparedStatic==GpuDecoNativeBatch.StaticCombine && gpuLivePreparedBounds==GpuDecoNativeBatch.RestoreSortBounds && gpuLivePreparedMasked==GpuDecoCopy.MaskedEnabled && gpuLivePreparedEight==GpuDecoCopy.EightUavEnabled && gpuLivePreparedCopy==GpuDecoCopy.Enabled && gpuLivePreparedNative==GpuDecoNativeBatch.Enabled && gpuLiveManager == manager && ReferenceEquals(gpuLiveBaseline, baselineObjects) && gpuLiveBundleKey == gpuBundlePath)
            {
                CompleteGpuLivePreparationBudget();
                GpuLog("prepare_ms=" + gpuLivePrepareMs.ToString("F6", Inv) + " phase=live_prepare_cached slices=" + gpuLiveSlices.Count
                    + " owned_union=" + gpuLiveOwnedRenderers.Length);
                return;
            }
            DisposeGpuLive();
            var timer = Stopwatch.StartNew();
            LazySprite.FlushAll();
            InvisibleSkip.ApplyAllLazy();
            ValidateBaseline();
            RequireLive(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 && SystemInfo.supportsComputeShaders, "D3D11 compute required");
            var cam = GpuSplit.FindMain(); var camy = ADOBase.controller ? ADOBase.controller.camy : null;
            RequireLive(cam && cam.orthographic && camy, "orthographic camera required");
            var mode = cam.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            var axis = mode == TransparencySortMode.CustomAxis
                ? (cam.transparencySortMode == TransparencySortMode.CustomAxis ? cam.transparencySortAxis : GraphicsSettings.transparencySortAxis)
                : cam.transform.forward;
            RequireLive(Mathf.Abs(axis.x) < 1e-6f && Mathf.Abs(axis.y) < 1e-6f && Mathf.Abs(axis.z) > 0, "Z sorting axis required");
            RequireLive(manager.transform.position == Vector3.zero && manager.transform.rotation == Quaternion.identity && manager.transform.lossyScale == Vector3.one, "manager transform must be identity");
            try
            {
                gpuLiveBundle = AssetBundle.LoadFromFile(Path.GetFullPath(gpuBundlePath));
                RequireLive(gpuLiveBundle, "bundle load failed");
                gpuLiveShader = gpuLiveBundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");
                RequireLive(gpuLiveShader, "compute asset missing");
                gpuLiveRoot = new GameObject("GpuDeco.Live") { hideFlags = HideFlags.HideAndDontSave };
                Application.quitting-=OnGpuLiveQuit;
                Application.quitting+=OnGpuLiveQuit;
                gpuLiveCamera = cam;
                gpuLiveZoomFactor = ADOBase.customLevel != null && ADOBase.customLevel.levelData != null
                    ? ADOBase.customLevel.levelData.camZoom / 100f : 1f;
                if (gpuLiveZoomFactor == 0f) gpuLiveZoomFactor = 1f;
                var ct = camy.transform;
                float camScale = camy.camobj.orthographicSize * .2f / gpuLiveZoomFactor;
                float[] camInit = { ct.position.x, ct.position.y, ct.eulerAngles.z, camScale };
                float ppw = Mathf.Max(cam.pixelHeight / (2 * cam.orthographicSize), cam.pixelWidth / (2 * cam.orthographicSize * cam.aspect));
                string fp = TableFingerprint();

                var blockCensus = new LiveDeco.BlockCensus();
                var protoItems = new Dictionary<int, GpuDecoData.Item>();
                var sliceMasks = new Dictionary<int, int>();
                var candidateByRenderer = new Dictionary<int, int>();
                var rejectCounts = new SortedDictionary<string, int>();
                int sliceCount = gpuScope.Starts.Length, windowCandidates = 0;
#if AUTOTEST
                PrepareGpuImageAdmission();
#endif
                for (int index = 0; index < baselineObjects.Length; index++)
                {
                    if (table.Decorations[index].Reasons.Count != 0) continue;
#if AUTOTEST
                    if(!GpuImageAdmit(index))continue;
#endif
                    int mask = 0;
                    for (int s = 0; s < sliceCount; s++)
                        if (gpuScope.WholeSong || VisibleInWindow(table, index, gpuScope.Starts[s] - 0.25, gpuScope.Ends[s] + 0.25))
                            mask |= 1 << s;
                    if (mask == 0) continue;
                    windowCandidates++;
                    var d = baselineObjects[index] as scrVisualDecoration;
                    if (!d || d.parallax.clampToScreen)
                    {
                        rejectCounts.TryGetValue("clamp-or-null", out int rc0); rejectCounts["clamp-or-null"] = rc0 + 1;
                        continue;
                    }
                    string liveWhy = LiveDecoRenderer.LiveGpuReason(d, cam, manager, blockCensus);
                    if (liveWhy != null)
                    {
                        rejectCounts.TryGetValue(liveWhy, out int rc1); rejectCounts[liveWhy] = rc1 + 1;
                        continue;
                    }
                    var r = d.spriteRenderer; var sp = r.sprite;
                    if (sp.vertices.Length != 4 || sp.uv.Length != 4 || sp.triangles.Length != 6) continue;
                    var geometry = LiveDecoGeometry.Capture(d.pivotTrans, d.childTransform, r.transform, sp.vertices, r.flipX, r.flipY);
                    if (geometry == null) continue;
                    var ts = d.scaleMultiplier * d.textureScaleMultiplier;
                    if (ts.x == 0 || ts.y == 0 || d.textureScaleMultiplier.x == 0 || d.textureScaleMultiplier.y == 0) continue;
                    var item = new GpuDecoData.Item
                    {
                        Index = index, Id = d.GetInstanceID(), MaterialId = r.sharedMaterial.GetInstanceID(), TextureId = sp.texture.GetInstanceID(),
                        Layer = r.sortingLayerID, Order = r.sortingOrder, Placement = d.placementType.ToString(),
                        Flags = (d.lockScale ? 1 : 0) | (d.lockRotation ? 2 : 0) | (d.parallax.dontAlterX ? 4 : 0) | (d.parallax.dontAlterY ? 8 : 0) | (r.flipX ? 16 : 0) | (r.flipY ? 32 : 0),
                        Constants = new[] { ts.x, ts.y, d.textureScaleMultiplier.x, d.textureScaleMultiplier.y, d.startRot, d.croppedPivotOffset.x, d.croppedPivotOffset.y, d.pivotTrans.position.z },
                        Initial = (float[])table.Decorations[index].Initial.Clone(),
                        Triangles = sp.triangles.Select(x => (int)x).ToArray()
                    };
                    // Live G2 never binary-searches Tracks on GPU; pass empty Tracks to save VRAM and guarantee 0 fallback searches.
                    for (int ch = 0; ch < 16; ch++) item.Tracks[ch] = Array.Empty<DecoBakeCore.Segment>();
                    var corners = sp.vertices; var uv = sp.uv;
                    for (int v = 0; v < 4; v++)
                    {
                        PutVec(item.Shape, v * 3, geometry.Shape[v]);
                        item.Corners[v * 2] = corners[v].x; item.Corners[v * 2 + 1] = corners[v].y;
                        item.UV[v * 2] = uv[v].x; item.UV[v * 2 + 1] = uv[v].y;
                    }
                    var origin = geometry.Offset(Vector2.zero); PutVec(item.OffsetBasis, 0, origin);
                    PutVec(item.OffsetBasis, 3, geometry.Offset(Vector2.right) - origin);
                    PutVec(item.OffsetBasis, 6, geometry.Offset(Vector2.up) - origin);
                    protoItems.Add(index, item);
                    gpuScope.CheckItemBudget(protoItems.Count);
                    sliceMasks.Add(index, mask);
                    candidateByRenderer.Add(r.GetInstanceID(), index);
                }

                var sigCache = new Dictionary<int, string>();
                var masterRows = new List<GpuLiveRow>();
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    // Merged runs must also respect currently hidden barriers:
                    // a CPU decoration can wake later inside this time window.
                    if (!r || !r.gameObject.scene.IsValid() || (!gpuRenderMergePrepare && (!r.enabled || !r.gameObject.activeInHierarchy))
                        || (cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                    int candIdx;
                    bool isOwned = candidateByRenderer.TryGetValue(r.GetInstanceID(), out candIdx) && r.enabled && r.gameObject.activeInHierarchy;
                    if (!isOwned && r.forceRenderingOff && !gpuRenderMergePrepare) continue;
                    if (r.GetComponentInParent<SortingGroup>()) continue;
                    var materials = r.sharedMaterials;
                    if (materials == null || materials.Length == 0) continue;
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        var m = materials[slot];
                        if (!m || m.renderQueue <= 2500) continue;
                        if (isOwned && slot > 0) continue;
                        string sig = "";
                        if (isOwned)
                        {
                            int matId = m.GetInstanceID();
                            if (!sigCache.TryGetValue(matId, out sig)) { sig = MaterialSignature(m); sigCache.Add(matId, sig); }
                        }
                        var key = isOwned
                            ? CandidateSortKey((scrVisualDecoration)baselineObjects[candIdx], (SpriteRenderer)r, m, cam)
                            : MatShare.SortKey(r, m, cam, slot);
                        masterRows.Add(new GpuLiveRow { R = r, M = m, Item = isOwned ? candIdx : -1, Key = key, Signature = sig });
                    }
                }
                masterRows.Sort((a, b) => a.Key.CompareTo(b.Key));
                if(gpuRenderMergePrepare)for(int i=1;i<masterRows.Count;i++)
                    RequireLive(!masterRows[i-1].Key.SameMaterialTie(masterRows[i].Key)
                        || masterRows[i-1].Item<0 && masterRows[i].Item<0,"rendermerge ambiguous material tie before barrier compaction");
                // Compact consecutive non-owned barrier rows so slice construction does not copy 30,000 barriers 9 times
                var compactedRows = new List<GpuLiveRow>(protoItems.Count * 2 + 16);
                for (int i = 0; i < masterRows.Count; i++)
                {
                    if (masterRows[i].Item < 0 && compactedRows.Count > 0 && compactedRows[compactedRows.Count - 1].Item < 0)
                        continue;
                    compactedRows.Add(masterRows[i]);
                }
                masterRows = compactedRows;
                GpuLog("live_prepare_candidates window_visible=" + windowCandidates + " admitted=" + protoItems.Count
                    + " rejects=" + (rejectCounts.Count == 0 ? "none" : string.Join(",", rejectCounts.Select(kv => kv.Key + ":" + kv.Value))));

                var ownedIndices = new List<int>();
                var ownedRenderers = new List<SpriteRenderer>();
                var baselineToMaster = new Dictionary<int, int>();
                gpuLiveOwnedSet.Clear();
                gpuLiveOwnedVisuals.Clear();
                gpuLiveMasterByDeco.Clear();
                foreach (var row in masterRows)
                {
                    if (row.Item < 0) continue;
                    int masterIdx = ownedIndices.Count;
                    var vd = (scrVisualDecoration)baselineObjects[row.Item];
                    baselineToMaster[row.Item] = masterIdx;
                    ownedIndices.Add(row.Item);
                    ownedRenderers.Add(vd.spriteRenderer);
                    gpuLiveOwnedSet.Add(vd);
                    gpuLiveOwnedVisuals.Add(vd);
                    gpuLiveMasterByDeco[vd] = masterIdx;
                }
                gpuLiveOwnedIndices = ownedIndices.ToArray();
                gpuLiveOwnedRenderers = ownedRenderers.ToArray();
                gpuLiveOriginalOff = new bool[gpuLiveOwnedRenderers.Length];
                gpuLiveExternalOff = new bool[gpuLiveOwnedRenderers.Length];
                int masterCount = gpuLiveOwnedIndices.Length;
                gpuMasterChannels = new LiveChannel[masterCount * 16];
                gpuMasterAnchors = new Anchor[masterCount * 16];
                gpuMasterSlotSource = new int[masterCount * 16];
                gpuMasterRecOrder=new long[masterCount*16];gpuMasterEngineOrder=new long[masterCount*16];
                gpuMasterOrderObserved=new bool[masterCount*16];
                for (int i = 0; i < gpuMasterSlotSource.Length; i++) gpuMasterSlotSource[i] = -1;
                gpuLiveMasterToSliceItem = new int[masterCount][];
                for (int m = 0; m < masterCount; m++)
                {
                    gpuLiveMasterToSliceItem[m] = new int[sliceCount];
                    for (int s = 0; s < sliceCount; s++) gpuLiveMasterToSliceItem[m][s] = -1;
                }
                ResetGpuLiveSourcePool();

                int copyKernel = gpuLiveShader.FindKernel("CopyRun");
                int totalRuns = 0, totalSubmeshes = 0;
                for (int s = 0; s < sliceCount; s++)
                {
                    int bit = 1 << s;
                    var sliceRows = new List<GpuLiveRow>();
                    var sliceItems = new List<GpuDecoData.Item>();
                    var sliceMasters = new List<int>();
                    foreach (var row in masterRows)
                    {
                        if (row.Item < 0)
                        {
                            sliceRows.Add(new GpuLiveRow { R = row.R, M = row.M, Item = -1, Key = row.Key, Signature = row.Signature });
                        }
                        else if ((sliceMasks[row.Item] & bit) != 0)
                        {
                            int newIdx = sliceItems.Count;
                            int masterIdx = baselineToMaster[row.Item];
                            gpuLiveMasterToSliceItem[masterIdx][s] = newIdx;
                            sliceMasters.Add(masterIdx);
                            var cloned = CloneSliceItem(protoItems[row.Item]);
                            for (int ch = 0; ch < 16; ch++)
                            {
                                float initVal = cloned.Initial[ch];
                                cloned.Anchors[ch] = new GpuDecoData.Anchor
                                {
                                    Valid = true, Clock = -1,
                                    Segment = new DecoBakeCore.Segment { Duration = 0f, Start = initVal, End = initVal, Target = initVal, Ease = 1, IsScale = (ch == 5 || ch == 6) }
                                };
                            }
                            sliceItems.Add(cloned);
                            sliceRows.Add(new GpuLiveRow { R = row.R, M = row.M, Item = newIdx, Key = row.Key, Signature = row.Signature });
                        }
                    }
                    var slice = new GpuLiveSlice
                    {
                        Start = gpuScope.Starts[s],
                        End = gpuScope.Ends[s],
                        MasterItems = sliceMasters.ToArray()
                    };
                    // Own every allocation from this point, including a failed
                    // GPU constructor or mesh build before native registration.
                    gpuLiveSlices.Add(slice);
                    slice.InitChannels(sliceItems.Count * 16);
                    if (sliceItems.Count > 0)
                    {
                        slice.Data = new GpuDecoData
                        {
                            RawTime = gpuScope.WholeSong ? 0 : gpuScope.Starts[s], BakeTime = gpuScope.WholeSong ? 0 : gpuScope.Starts[s], Pitch = table.Pitch, TweenScale = table.TweenScale,
                            PixelsPerWorld = ppw, Frame = Time.frameCount, Awake = sliceItems.Count, Supported = table.Supported,
                            Fingerprint = fp, Camera = (float[])camInit.Clone(), Items = sliceItems.ToArray(),
                            Sources = new GpuDecoData.SourceClock[MaxLiveSources]
                        };
                        for (int k = 0; k < MaxLiveSources; k++)
                            slice.Data.Sources[k] = new GpuDecoData.SourceClock { Id = k + 1, StartFrame = 0, StartSong = 0, Progress = 0f };
                        slice.Gpu = new GpuDecoGpu(gpuLiveShader, slice.Data, MaxLiveSources) { UseAnchors = true, UseLiveMode = true };
                        slice.Commands = new CommandBuffer { name = "GpuDeco.Live copy slice " + s };
                        slice.Commands.SetComputeBufferParam(gpuLiveShader, copyKernel, "_CopySource", slice.Gpu.VertexBuffer);
                        slice.Commands.SetComputeIntParam(gpuLiveShader, "_CopyQuantizeColor", 1);
                        for (int a = 0; a < sliceRows.Count;)
                        {
                            if (sliceRows[a].Item < 0) { a++; continue; }
                            int b = a + 1;
                            while (!GpuDecoNativeBatch.Enabled && b < sliceRows.Count && sliceRows[b].Item >= 0 && sliceRows[a].Key.SameBucket(sliceRows[b].Key)
                                && sliceRows[a].R.gameObject.layer == sliceRows[b].R.gameObject.layer) b++;
                            float z = slice.Data.Items[sliceRows[a].Item].Constants[7];
                            slice.SubmeshCount += BuildLiveRun(slice, sliceRows, a, b, copyKernel, z);
                            slice.RunCount++;
                            a = b;
                        }
                        totalRuns += slice.RunCount;
                        totalSubmeshes += slice.SubmeshCount;
                        if(GpuDecoCopy.Enabled)
                        {
                            RequireLive(GpuDecoNativeBatch.Enabled && !GpuDecoNativeBatch.StaticCombine,"native copy requires independent renderers");
                            int[] copyOrder=null;
                            if(GpuDecoCopy.MaskedEnabled)
                            {
                                copyOrder=Enumerable.Range(0,slice.Data.Items.Length).OrderBy(i=>CopyLocalityHint(slice.Data.Items[i].Index)).ThenBy(i=>i).ToArray();
                                slice.CopyMask=new GpuDecoCopyMask(slice.Data.Items.Length,copyOrder);
                            }
                            slice.NativeCopy=GpuDecoCopy.TryCreate(gpuLiveShader,slice.Gpu.VertexBuffer,slice.Buffers,slice.Data.Items.Length*4,copyOrder);
                            RequireLive(slice.NativeCopy!=null,"native copy unavailable: "+GpuDecoCopy.Status);
                        }
                    }
                }
                PrepareGpuMerge(masterRows);
                CompleteGpuLivePreparationBudget();
                gpuLiveManager = manager;
                gpuLiveBaseline = baselineObjects;
                gpuLiveBundleKey = gpuBundlePath;
                gpuLivePrepared = true;
                if(gpuPacketMode!=0)CompileGpuPackets();
                gpuRecoveryBlocked=false;
                gpuPreparedScope = gpuScope;
                gpuLivePreparedNative=GpuDecoNativeBatch.Enabled;
                gpuLivePreparedStatic=GpuDecoNativeBatch.StaticCombine;gpuLivePreparedBounds=GpuDecoNativeBatch.RestoreSortBounds;
                gpuLivePreparedCopy=GpuDecoCopy.Enabled;
                gpuLivePreparedEight=GpuDecoCopy.EightUavEnabled;
                gpuLivePreparedMasked=GpuDecoCopy.MaskedEnabled;
                gpuLivePrepareMs = timer.Elapsed.TotalMilliseconds;
                GpuLog("prepare_ms=" + gpuLivePrepareMs.ToString("F6", Inv) + " phase=live_prepare slices=" + gpuLiveSlices.Count
                    + " owned_union=" + gpuLiveOwnedRenderers.Length + " total_runs=" + totalRuns + " total_submeshes=" + totalSubmeshes
                    + " live_kernel=" + (gpuLiveSlices.Count > 0 && gpuLiveSlices[0].Gpu != null && gpuLiveSlices[0].Gpu.HasNativeLiveKernel ? "EvaluateLive_sourceE" : "Evaluate_anchored_no_bsearch")
                    +" native_sort="+gpuLivePreparedNative+" native_copy="+gpuLivePreparedCopy+" eight_uav="+gpuLivePreparedEight+" static_batch="+gpuLivePreparedStatic
                    +" scope="+(gpuScope.WholeSong?"song_union":"reference")+" item_limit="+GpuDecoScopePlan.SongItemLimit+" masked_copy="+gpuLivePreparedMasked);
            }
            catch
            {
                DisposeGpuLive();
                throw;
            }
        }

        static int BuildLiveRun(GpuLiveSlice slice, List<GpuLiveRow> rows, int a, int b, int copyKernel, float z, bool mapped=false)
        {
            var first = rows[a]; int count = b - a;
            var groups = new List<KeyValuePair<int, int>>();
            for (int n = 0; n < count;)
            {
                int start = n;
                int texId = slice.Data.Items[rows[a + start].Item].TextureId;
                string sig = rows[a + start].Signature;
                n++;
                while (n < count
                    && slice.Data.Items[rows[a + n].Item].TextureId == texId
                    && rows[a + n].Signature == sig) n++;
                groups.Add(new KeyValuePair<int, int>(start, n - start));
            }
            var mesh = new Mesh { name = "GpuDeco.Live run", indexFormat = IndexFormat.UInt32, vertexBufferTarget = GraphicsBuffer.Target.Raw };
            slice.Meshes.Add(mesh);
            mesh.SetVertexBufferParams(count * 4,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            mesh.SetVertexBufferData(new Vertex[count * 4], 0, 0, count * 4);
            mesh.subMeshCount = groups.Count;
            for (int g = 0; g < groups.Count; g++)
            {
                int start = groups[g].Key, len = groups[g].Value;
                var indices = new int[len * 6];
                for (int k = 0; k < len; k++)
                {
                    var tri = slice.Data.Items[rows[a + start + k].Item].Triangles;
                    int baseVertex = (start + k) * 4;
                    for (int t = 0; t < 6; t++) indices[k * 6 + t] = tri[t] + baseVertex;
                }
                mesh.SetIndices(indices, MeshTopology.Triangles, g, false);
            }
            Vector3 sortCenter = first.R.bounds.center;
            var sr = first.R as SpriteRenderer;
            if (sr && sr.spriteSortPoint == SpriteSortPoint.Pivot) sortCenter = first.R.transform.position;
            mesh.bounds = new Bounds(sortCenter, new Vector3(2000000, 2000000, 0));
            mesh.UploadMeshData(!mapped);
            var buffer = mesh.GetVertexBuffer(0);
            slice.Buffers.Add(buffer);
            var go = new GameObject("GpuDeco.Live run") { hideFlags = HideFlags.HideAndDontSave, layer = first.R.gameObject.layer };
            go.transform.SetParent(gpuLiveRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            slice.Renderers.Add(renderer);
            renderer.sharedMaterials = groups.Select(g => rows[a + g.Key].M).ToArray();
            if(mapped)
            {
                var mats=renderer.sharedMaterials;
                for(int i=1;i<mats.Length;i++)RequireLive(mats[i-1].GetInstanceID()<mats[i].GetInstanceID(),"merge submesh material IDs must increase strictly");
            }
            renderer.sortingLayerID = first.R.sortingLayerID;
            renderer.sortingOrder = first.R.sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int g = 0; g < groups.Count; g++)
            {
                var props = new MaterialPropertyBlock();
                var sprite = (SpriteRenderer)rows[a + groups[g].Key].R;
                props.SetTexture("_MainTex", sprite.sprite.texture);
                props.SetColor("_RendererColor", Color.white);
                props.SetVector("_Flip", Vector4.one);
                renderer.SetPropertyBlock(props, g);
            }
            if(mapped)
            {
                var map=new uint[count*4];
                for(int n=0;n<count;n++)for(int k=0;k<4;k++)map[n*4+k]=(uint)(rows[a+n].Item*4+k);
                var mapBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,map.Length,4);slice.Buffers.Add(mapBuffer);mapBuffer.SetData(map);
                slice.Commands.SetComputeBufferParam(gpuLiveShader,copyKernel,"_CopyMap",mapBuffer);
            }
            slice.Commands.SetComputeIntParam(gpuLiveShader, "_CopyFirstVertex", first.Item * 4);
            slice.Commands.SetComputeIntParam(gpuLiveShader, "_CopyVertexCount", count * 4);
            slice.Commands.SetComputeBufferParam(gpuLiveShader, copyKernel, "_Vertices", buffer);
            slice.Commands.DispatchCompute(gpuLiveShader, copyKernel, (count * 4 + 63) / 64, 1, 1);
            return groups.Count;
        }

        static int FindGpuLiveSlice(double time)
        {
            if (gpuLiveSlices.Count == 0) return -1;
            if (gpuScope.Find(time)<0) return -1;
            if (gpuLiveCurrentSlice >= 0 && gpuLiveCurrentSlice < gpuLiveSlices.Count)
            {
                var cur = gpuLiveSlices[gpuLiveCurrentSlice];
                if (time >= cur.Start && time < cur.End) return gpuLiveCurrentSlice;
            }
            for (int i = 0; i < gpuLiveSlices.Count; i++)
                if (time >= gpuLiveSlices[i].Start && time < gpuLiveSlices[i].End) return i;
            return -1;
        }

        // Request B.1: Frame loop executing once per frame at scrDecorationManager.LateUpdate postfix (TightLate.ManagerEnd).
        static void DispatchGpuLiveSlice(double time)
        {
            GpuRecoveryFault("render_before");
            long t0 = Stopwatch.GetTimestamp();
            int s = FindGpuLiveSlice(time);
            if (s < 0) {StopGpuLive("outside_prepared_scope");return;}
            if (s != gpuLiveCurrentSlice)
            {
                if (gpuLiveCurrentSlice >= 0 && gpuLiveCurrentSlice < gpuLiveSlices.Count)
                    gpuLiveSlices[gpuLiveCurrentSlice].SetEnabled(false);
                gpuLiveCurrentSlice = s;
                var next = gpuLiveSlices[s];
                if (next.Gpu != null)
                    next.Gpu.Bind();
                next.SetEnabled(true);
            }
            var active = gpuLiveSlices[gpuLiveCurrentSlice];
            if (active.Gpu == null) return;
            long t1 = Stopwatch.GetTimestamp();
            AdvanceGpuLiveSources();
#if AUTOTEST
            FlushGpuImagesBeforeVisible();
            if(!gpuLiveActive)return;
#endif
            long probeAdvance=GpuDecoCostProbe.Enabled?Stopwatch.GetTimestamp():0;
            active.FlushDirty();
            long probeDirty=GpuDecoCostProbe.Enabled?Stopwatch.GetTimestamp():0;
            active.Gpu.UploadSourceE(gpuSourceE, gpuSourceProgress, gpuSourceHighWater);
            long t2 = Stopwatch.GetTimestamp();
            if(GpuDecoCostProbe.Enabled)GpuDecoCostProbe.Frame(probeAdvance-t1,probeDirty-probeAdvance,t2-probeDirty,
                gpuSourceHighWater,active.CopyMask?.Low??0,active.CopyMask?.High??0);
            var camy = ADOBase.controller.camy;
            var ct = camy.transform;
            float camScale = camy.camobj.orthographicSize * .2f / gpuLiveZoomFactor;
            active.Gpu.SetLiveUniforms(new Vector4(ct.position.x, ct.position.y, ct.eulerAngles.z, camScale), time);
            long t3 = Stopwatch.GetTimestamp();
            active.Gpu.DispatchEvaluate();
            // If the optional backend rejects a callback, write the same current
            // vertices through the original path and invalidate the research gate.
            if(gpuRenderMergeOn)DispatchGpuMerge(time);
            else if(active.SharedBatch!=null)active.SharedBatch.Dispatch();
            else if(active.NativeCopy==null || !active.NativeCopy.Execute(gpuCopyMaskBypass?ulong.MaxValue:active.CopyMask?.Low??ulong.MaxValue,
                gpuCopyMaskBypass?ulong.MaxValue:active.CopyMask?.High??ulong.MaxValue))Graphics.ExecuteCommandBuffer(active.Commands);
            GpuRecoveryFault("render_after");
            long t4 = Stopwatch.GetTimestamp();
            gpuCostMiscTicks += t1 - t0;
            gpuCostProgressTicks += t2 - t1;
            gpuCostUniformTicks += t3 - t2;
            gpuCostDispatchTicks += t4 - t3;
            gpuCostFrames++;
            double totalMs = (t4 - t0) * 1000.0 / Stopwatch.Frequency;
            if (totalMs > gpuCostMaxMs) gpuCostMaxMs = totalMs;
        }

        // Primary hook: called at the end of scrDecorationManager.LateUpdate (TightLate.ManagerEnd)
        // after all Update/FastMove/DecoAnim and camera LateUpdate work, before any Camera renders.
        internal static void GpuLiveFrame()
        {
            if (!gpuLiveActive || gpuLiveLastDispatchFrame == Time.frameCount) return;
            gpuLiveLastDispatchFrame = Time.frameCount;
            try
            {
                AdvanceClock();
                DispatchGpuLiveSlice(BakeTime);
            }
            catch (Exception e)
            {
                RecoverGpuLive("LateUpdateException:"+e.GetType().Name);
            }
        }

        // Fallback hook: only runs if a frame was rendered while paused without scrDecorationManager.LateUpdate running.
        static void OnGpuLivePreCull(Camera cam)
        {
            if (!gpuLiveActive || cam != gpuLiveCamera || gpuLiveLastDispatchFrame == Time.frameCount) return;
            GpuLiveFrame();
        }

        // Captures all 16 live logical channels and any in-flight DecoAnim.Recs on owned decorations at turn-on.
        static void CaptureInitialGpuLiveState()
        {
            ResetGpuLiveSourcePool();
            Array.Clear(gpuMasterRecOrder,0,gpuMasterRecOrder.Length);Array.Clear(gpuMasterEngineOrder,0,gpuMasterEngineOrder.Length);
            Array.Clear(gpuMasterOrderObserved,0,gpuMasterOrderObserved.Length);
            var recSourceMap = new Dictionary<long, GpuLiveSource>();
            int masterCount = gpuLiveOwnedIndices.Length;
            for (int m = 0; m < masterCount; m++)
            {
                int baseIdx = gpuLiveOwnedIndices[m];
                var d = baselineObjects[baseIdx] as scrVisualDecoration;
                if (!d) continue;
                var c = d.color;
                var p = d.parallax != null ? d.parallax.multiplier : Vector2.zero;
                gpuRestoreValues[0] = d.pivotPosVec.x;
                gpuRestoreValues[1] = d.pivotPosVec.y;
                gpuRestoreValues[2] = d.pivotOffsetVec.x;
                gpuRestoreValues[3] = d.pivotOffsetVec.y;
                gpuRestoreValues[4] = d.rotAngle;
                gpuRestoreValues[5] = d.scaleVec.x;
                gpuRestoreValues[6] = d.scaleVec.y;
                gpuRestoreValues[7] = c.r;
                gpuRestoreValues[8] = c.g;
                gpuRestoreValues[9] = c.b;
                gpuRestoreValues[10] = c.a;
                gpuRestoreValues[11] = d.opacity;
                gpuRestoreValues[12] = p.x;
                gpuRestoreValues[13] = p.y;
                gpuRestoreValues[14] = d.parallaxOffset.x;
                gpuRestoreValues[15] = d.parallaxOffset.y;
                for (int ch = 0; ch < 16; ch++)
                    SetMasterSlotStatic(m, ch, gpuRestoreValues[ch]);

                if(GpuCpuFallback(m))continue;

                if (d.eventTweens != null && d.eventTweens.Count > 0)
                {
                    foreach (var pair in d.eventTweens)
                    {
                        var tween = pair.Value;
                        var r = tween?.id as DecoAnim.Rec;
                        if (r == null)
                        {
                            if (tween != null && tween.active)throw new NotSupportedException("native tween appeared during GPU activation");
                            continue;
                        }
                        if (!r.Running || !ReferenceEquals(r.D, d) || r.Dur <= 0f || !GpuDecoGpu.Supported((int)r.E))
                            continue;
                        SaveGpuActivationRecord(r);
                        if (!r.Started && (r.Key == 7 || r.Key == 8))
                        {
                            r.VStart = d.scaleVec;
                            r.VChange = r.VEnd - r.VStart;
                            r.Started = true;
                        }
                        // Detach r from DecoEngine and DecoAnim.Batch (syncing r.Pos) without jumping to endpoint
                        if (tween.active) tween.Kill(false);
                        else r.Running = false;
                        GpuRecoveryFault("activation_after_record");
                        float currentPos = r.Pos;
                        int srcId = r.GpuSource != 0 ? r.GpuSource : (((int)r.E << 24) ^ BitConverter.SingleToInt32Bits(r.Dur) ^ BitConverter.SingleToInt32Bits(currentPos));
                        int startFrame = r.GpuStartFrame >= 0 ? r.GpuStartFrame : Time.frameCount - 1;
                        long token = ((long)srcId << 32) | (uint)startFrame;
                        if (!recSourceMap.TryGetValue(token, out var liveSrc) || Math.Abs(liveSrc.Pos - currentPos) > 1e-6f || liveSrc.Dur != r.Dur || liveSrc.Ease != r.E)
                        {
                            liveSrc = AllocateGpuLiveSource(srcId, startFrame, r.Dur, r.E, r.Over, r.Period, currentPos);
                            liveSrc.StartSong=r.GpuStartSong;
                            recSourceMap[token] = liveSrc;
                        }
                        float easeEnd = ZeroTween.Eval(r.E, r.Dur, r.Dur, r.Over, r.Period);
                        switch (r.Key)
                        {
                            case 1: case 2: case 3: case 4: case 5: case 10: case 12: case 13:
                                int ch = r.Key <= 5 ? r.Key - 1 : r.Key == 10 ? 11 : r.Key + 2;
                                SetMasterSlotAnimated(m, ch, r.FStart, r.FEnd, r.FEnd, liveSrc, false);
                                break;
                            case 7:
                                SetMasterSlotAnimated(m, 5, r.VStart.x, r.VEnd.x, r.VStart.x + (r.VEnd.x - r.VStart.x) * easeEnd, liveSrc, true);
                                break;
                            case 8:
                                SetMasterSlotAnimated(m, 6, r.VStart.y, r.VEnd.y, r.VStart.y + (r.VEnd.y - r.VStart.y) * easeEnd, liveSrc, true);
                                break;
                            case 9:
                                for (int comp = 0; comp < 4; comp++)
                                    SetMasterSlotAnimated(m, 7 + comp, r.CStart[comp], r.CEnd[comp], r.CEnd[comp], liveSrc, false);
                                break;
                            case 11:
                                SetMasterSlotAnimated(m, 12, r.VStart.x, r.VEnd.x, r.VStart.x + (r.VEnd.x - r.VStart.x) * easeEnd, liveSrc, true);
                                SetMasterSlotAnimated(m, 13, r.VStart.y, r.VEnd.y, r.VStart.y + (r.VEnd.y - r.VStart.y) * easeEnd, liveSrc, true);
                                break;
                        }
                        int first=r.Key<=5?r.Key-1:r.Key==7?5:r.Key==8?6:r.Key==9?7:r.Key==10?11:r.Key==11?12:r.Key+2;
                        if((uint)first<16)StampGpuMasterRecOrder(m,first,r.GpuOrder,r.EngineOrder,r.GpuOrderObserved);
                    }
                }
                // No clock is inferred from table/song time. A channel without
                // a running captured record keeps the actual CPU value above.
            }
        }

        static void StartGpuLive()
        {
            if (gpuLiveActive) return;
            RequireLive(!gpuRecoveryBlocked,"GPU recovery requires live prepare before enabling again");
            if (!gpuLivePrepared || !ReferenceEquals(gpuPreparedScope,gpuScope) || gpuLivePreparedStatic!=GpuDecoNativeBatch.StaticCombine || gpuLivePreparedBounds!=GpuDecoNativeBatch.RestoreSortBounds || gpuLivePreparedMasked!=GpuDecoCopy.MaskedEnabled || gpuLivePreparedEight!=GpuDecoCopy.EightUavEnabled || gpuLivePreparedCopy!=GpuDecoCopy.Enabled || gpuLivePreparedNative!=GpuDecoNativeBatch.Enabled || gpuLiveManager != manager || !ReferenceEquals(gpuLiveBaseline, baselineObjects) || gpuLiveBundleKey != gpuBundlePath)
                PrepareGpuLive();
            var sw = Stopwatch.StartNew();
            InvisibleSkip.ApplyAllLazy();
            AdvanceClock();
            RequireLive(FindGpuLiveSlice(BakeTime)>=0,"time outside prepared scope; originals retained");
            ClassifyGpuActivationOwnership();
            ClassifyPacketTargetGuards();
            for (int i = 0; i < gpuLiveOwnedRenderers.Length; i++)
            {
                var r = gpuLiveOwnedRenderers[i];
                gpuLiveOriginalOff[i] = r && r.forceRenderingOff;
                var d=baselineObjects[gpuLiveOwnedIndices[i]];
                gpuLiveExternalOff[i]=gpuLiveOriginalOff[i] && !(InvisibleSkip.Enabled && d && InvisibleSkip.IsHidden(d));
            }
            bool activationCommitted=false;
            try
            {
            BeginGpuActivationUndo();
            CaptureInitialGpuLiveState();
#if AUTOTEST
            CaptureGpuImageActivation();
#endif
            GpuRecoveryFault("activation_after_capture");
            for (int s = 0; s < gpuLiveSlices.Count; s++)
                gpuLiveSlices[s].FlushDirty();
            gpuLiveTurnOnBakeTime = BakeTime;
            gpuLiveEnabledAt = Time.realtimeSinceStartup;
            gpuLivePortalObserved = false;
            gpuLiveCurrentSlice = -1;
            gpuLiveLastDispatchFrame = -1;
            gpuLiveLastSourceStepFrame = Time.frameCount; // Do not advance Pos on the paused turn-on call itself
            gpuLiveActive = true;
            for (int i = 0; i < gpuLiveOwnedRenderers.Length; i++)
            {
                var r = gpuLiveOwnedRenderers[i];
                if (r && !GpuCpuFallback(i)) r.forceRenderingOff = true;
            }
            Camera.onPreCull -= OnGpuLivePreCull;
            Camera.onPreCull += OnGpuLivePreCull;
            DispatchGpuLiveSlice(BakeTime);
            GpuRecoveryFault("activation_after_dispatch");
            gpuLiveLastDispatchFrame = Time.frameCount;
            gpuLiveTransitionMs = sw.Elapsed.TotalMilliseconds;
            var slice = gpuLiveCurrentSlice >= 0 ? gpuLiveSlices[gpuLiveCurrentSlice] : null;
            CommitGpuActivationUndo();activationCommitted=true;
            GpuLog("on=True scope="+(gpuScope.WholeSong?"song_union":"live_G2")+" owned=" + GpuLiveOwned
                + " active_sources=" + gpuActiveSourceSlots.Count
                + " slice=" + gpuLiveCurrentSlice
                + " slice_owned=" + (slice?.Data?.Items?.Length ?? 0)
                + " runs=" + (slice?.RunCount ?? 0)
                + " submeshes=" + (slice?.SubmeshCount ?? 0)
                + " transition_ms=" + gpuLiveTransitionMs.ToString("F6", Inv)
                + " prepare_ms=" + gpuLivePrepareMs.ToString("F6", Inv));
            }
            catch(Exception ex)
            {
                if(activationCommitted)RecoverGpuLive("activation_committed:"+ex.GetType().Name);
                else
                {
                    RollbackGpuActivation(ex.GetType().Name);
                    if(!GpuRecoveryTesting)Resilience.RecordFeatureFailure("GpuDecoLive","activation:"+ex.GetType().Name);
                }
                if(!GpuRecoveryTesting)throw new InvalidOperationException("GPU activation refused; CPU rollback attempted",ex);
            }
        }

        static void LogGpuLiveCost(string scope)
        {
            long f = Math.Max(1L, gpuCostFrames);
            double tf = Stopwatch.Frequency / 1000.0;
            double pMs = gpuCostProgressTicks / tf / f;
            double uMs = gpuCostUniformTicks / tf / f;
            double dMs = gpuCostDispatchTicks / tf / f;
            double mMs = gpuCostMiscTicks / tf / f;
            double totMs = pMs + uMs + dMs + mMs;
            GpuLog("cost scope=" + scope + " frames=" + gpuCostFrames
                + " cpu_scope=managed_only native_copy_accounting=G2_window_stats"
                + " total_ms=" + totMs.ToString("F6", Inv)
                + " progress_ms=" + pMs.ToString("F6", Inv)
                + " uniform_ms=" + uMs.ToString("F6", Inv)
                + " dispatch_ms=" + dMs.ToString("F6", Inv)
                + " misc_ms=" + mMs.ToString("F6", Inv)
                + " max_ms=" + gpuCostMaxMs.ToString("F6", Inv)
                + " budget_le_0.5ms=" + (totMs <= 0.5));
        }

        // Request B.3: Full restoration of owned decorations on off, song end, retry, restart, editor return, map reopen, and unload.
        internal static void StopGpuLive(string reason, bool syncState = true)
        {
            HideGpuMerge();
#if AUTOTEST
            ResetGpuAblation();
            ResetGpuMicroWindow();
            ResetGpuStorage(false);
#endif
            gpuLastRestoreComplete=true;
            double time = BakeTime;
            bool canSync = syncState && ready && table != null && baselineObjects != null && !SceneReset.Resetting
                && (Hitch.Playing || FrameCompare.PlaybackContinuity || gpuLivePortalObserved)
                && ReferenceEquals(gpuLiveBaseline,baselineObjects) && gpuLiveManager==manager;
            bool syncValues=canSync && !(FrameCompare.Paused && Math.Abs(time-gpuLiveTurnOnBakeTime)<=1e-6);
            List<DecoAnim.GpuResumeSpec> resume=null;
            int unknownOrders=0;
            if(gpuLiveActive && canSync && !gpuLivePortalObserved)
            {
                // CPU Update has already passed in LateUpdate/pre-cull. Catch
                // up once there, but never pre-step an upcoming CPU Update.
                if(DecoAnim.GpuCpuUpdateFrame==Time.frameCount)AdvanceGpuLiveSources();
                resume=BuildGpuResumePlan(out unknownOrders);
            }
            Camera.onPreCull -= OnGpuLivePreCull;
            for (int i = 0; i < gpuLiveSlices.Count; i++)
                gpuLiveSlices[i].SetEnabled(false);
            gpuLiveCurrentSlice = -1;
            if (!gpuLiveActive) return;
            gpuLiveActive = false;
#if AUTOTEST
            gpuImagePending.Clear();
#endif
            long restoreBegin=Stopwatch.GetTimestamp();
            bool restored = true;
            if(resume!=null)DecoAnim.BeginGpuResume();
            for (int i = 0; i < gpuLiveOwnedRenderers.Length; i++)
            {
                if(GpuCpuFallback(i))continue;
                try
                {
                    var r = gpuLiveOwnedRenderers[i];
                    if (!r) continue;
                    if (!syncValues)
                    {
                        r.forceRenderingOff = RestoredGpuRendererOff(i);
                        continue;
                    }
                    int idx = gpuLiveOwnedIndices[i];
                    var d = baselineObjects[idx] as scrVisualDecoration;
                    if (!d) { r.forceRenderingOff = false; continue; }
                    for (int ch = 0; ch < 16; ch++)
                        gpuRestoreValues[ch] = EvalMasterSlotCurrent(i * 16 + ch);
                    int mask = 0;
                    if (d.pivotPosVec.x != gpuRestoreValues[0]) mask |= 1 << 0;
                    if (d.pivotPosVec.y != gpuRestoreValues[1]) mask |= 1 << 1;
                    if (d.pivotOffsetVec.x != gpuRestoreValues[2]) mask |= 1 << 2;
                    if (d.pivotOffsetVec.y != gpuRestoreValues[3]) mask |= 1 << 3;
                    if (d.rotAngle != gpuRestoreValues[4]) mask |= 1 << 4;
                    if (d.scaleVec.x != gpuRestoreValues[5]) mask |= 1 << 5;
                    if (d.scaleVec.y != gpuRestoreValues[6]) mask |= 1 << 6;
                    if (d.color.r != gpuRestoreValues[7] || d.color.g != gpuRestoreValues[8]
                        || d.color.b != gpuRestoreValues[9] || d.color.a != gpuRestoreValues[10])
                        mask |= (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10);
                    if (d.opacity != gpuRestoreValues[11]) mask |= 1 << 11;
                    if (d.parallax.multiplier.x != gpuRestoreValues[12]) mask |= 1 << 12;
                    if (d.parallax.multiplier.y != gpuRestoreValues[13]) mask |= 1 << 13;
                    if (d.parallaxOffset.x != gpuRestoreValues[14]) mask |= 1 << 14;
                    if (d.parallaxOffset.y != gpuRestoreValues[15]) mask |= 1 << 15;
                    float alpha = gpuRestoreValues[10] * gpuRestoreValues[11];
                    bool wasHidden = InvisibleSkip.Enabled && InvisibleSkip.IsHidden(d);
                    bool nowHidden = InvisibleSkip.Enabled && alpha <= 0f;
                    r.forceRenderingOff = wasHidden;
                    if (wasHidden != nowHidden)
                        mask |= (15 << 7) | (1 << 11);
                    if (mask != 0) DecoAnim.BakeWrite(d, gpuRestoreValues, 0, mask);
#if AUTOTEST
                    // Image setters read a managed mirror while the Transform
                    // remains GPU-owned. A same-value restore still needs pose.
                    if(gpuImageCandidate&&GpuImageActor(idx))d.UpdatePosition();
#endif
                    if (nowHidden) InvisibleSkip.After(d);
                    else if (Dormancy.Enabled) Dormancy.WakePost(d);
                    r.forceRenderingOff=gpuLiveExternalOff[i] || nowHidden;
                }
                catch (Exception e)
                {
                    restored = false;
                    GpuLog("restore_error=" + e.Message);
                }
            }
            int resumed=0;
            if(resume!=null)
            {
                foreach(var spec in resume)
                    try {DecoAnim.ResumeGpuRecord(spec);resumed++;}
                    catch(Exception e){restored=false;GpuLog("resume_error="+e.GetType().Name+":"+e.Message);}
                DecoAnim.EndGpuResume();
            }
            GpuLog("source_capacity capacity="+MaxLiveSources+" peak_allocated="+gpuSourcePeakAllocated
                +" high_water="+gpuSourceHighWater+" scope=allocated_clocks_not_per_decoration_cost reason="+reason);
            ResetGpuLiveSourcePool();
            gpuLastRestoreComplete=restored;
#if AUTOTEST
            ObserveGpuMergeRestore(restored);
#endif
            LogGpuLiveCost(reason);
            GpuLog("on=False owned=0 originals_restored=" + restored + " reason=" + reason
                +" song="+time.ToString("F6",Inv)
                +" resumed_recs="+resumed+" resume_expected="+(resume?.Count??0)+" unobserved_order="+unknownOrders
                +" restore_ms="+((Stopwatch.GetTimestamp()-restoreBegin)*1000.0/Stopwatch.Frequency).ToString("F6",Inv));
        }

        static void OnGpuLiveQuit()
        {
            try {StopGpuLive("Application.quitting",false);DisposeGpuLive();GpuLog("shutdown_disposed=True native_contexts="+GpuDecoCopy.ActiveContexts);}
            catch(Exception e) {GpuLog("shutdown_disposed=False reason="+e.GetType().Name);}
        }
        internal static void DisposeGpuLive()
        {
#if AUTOTEST
            ResetGpuAblation();
#endif
            ClearGpuPackets();
            ClearGpuActivationUndo();
            gpuTargetUndoDecoration=null;gpuTargetUndoCount=0;
#if AUTOTEST
            gpuRecoveryFaultStage=null;
#endif
#if AUTOTEST
            DecoAnim.CancelGpuResumeAudit();
#endif
            GpuDecoCostProbe.Enabled=false;
            gpuCopyMaskBypass=false;
            Application.quitting-=OnGpuLiveQuit;
            g2ThreadCpu?.Dispose();g2ThreadCpu=null;
            g2RenderMetrics?.Dispose();g2RenderMetrics=null;
            try {StopGpuLive("Dispose");}
            catch(Exception e)
            {
                // Unload must quiesce callbacks/resources even if the strict
                // resume preflight rejected externally changed animation data.
                GpuLog("resume_dispose_rejected="+e.GetType().Name+" forced_quiescence=True");
                StopGpuLive("dispose_forced",false);
            }
            DisposeGpuMerge();
            gpuRenderMergeOn=false;
            gpuMergeRows=null;
            for (int i = gpuLiveSlices.Count - 1; i >= 0; i--)
                gpuLiveSlices[i].Dispose();
            gpuLiveSlices.Clear();
            if (gpuLiveRoot) UnityEngine.Object.Destroy(gpuLiveRoot);
            gpuLiveRoot = null;
            if (gpuLiveBundle)
            {
                try { gpuLiveBundle.Unload(true); } catch { }
                gpuLiveBundle = null;
            }
            gpuLiveShader = null;
            gpuLiveCamera = null;
            gpuLiveManager = null;
            gpuLiveBaseline = null;
            gpuLiveBundleKey = null;
            gpuLiveOwnedSet.Clear();
            gpuLiveOwnedVisuals.Clear();
            gpuLiveMasterByDeco.Clear();
            gpuLiveCpuFallback=Array.Empty<bool>();gpuLiveCpuFallbackCount=0;
            gpuLiveOwnedIndices = Array.Empty<int>();
            gpuLiveOwnedRenderers = Array.Empty<SpriteRenderer>();
            gpuLiveOriginalOff = Array.Empty<bool>();
            gpuLiveExternalOff = Array.Empty<bool>();
            gpuLiveMasterToSliceItem = Array.Empty<int[]>();
            gpuMasterChannels = Array.Empty<LiveChannel>();
            gpuMasterAnchors = Array.Empty<Anchor>();
            gpuMasterSlotSource = Array.Empty<int>();
            gpuMasterRecOrder=gpuMasterEngineOrder=Array.Empty<long>();
            gpuMasterOrderObserved=Array.Empty<bool>();
            gpuLivePrepared = false;
            gpuPreparedScope = null;
            gpuLivePreparedNative=false;
            gpuLivePreparedStatic=gpuLivePreparedBounds=false;
            gpuLivePreparedCopy=false;
            gpuLivePreparedEight=false;
            gpuLivePreparedMasked=false;
        }

        static bool RestoredGpuRendererOff(int i)
        {
            var d=baselineObjects!=null && i<gpuLiveOwnedIndices.Length && gpuLiveOwnedIndices[i]<baselineObjects.Length
                ?baselineObjects[gpuLiveOwnedIndices[i]]:null;
            bool external=i<gpuLiveExternalOff.Length && gpuLiveExternalOff[i];
            return external || (InvisibleSkip.Enabled && d?InvisibleSkip.IsHidden(d):i<gpuLiveOriginalOff.Length && gpuLiveOriginalOff[i]);
        }
        static int CountUnrestoredOwned()
        {
            int bad = 0;
            for (int i = 0; i < gpuLiveOwnedRenderers.Length; i++)
            {
                if(GpuCpuFallback(i))continue;
                var r = gpuLiveOwnedRenderers[i];
                if (!r) continue;
                if (r.forceRenderingOff!=RestoredGpuRendererOff(i)) bad++;
            }
            return bad;
        }

        // Request B.5: G2 commands matching tools/optimization/gpudeco-perf-steps.py
        static void LogGpuRenderCensus(int window,string edge)
        {
            if(!gpuLiveActive || gpuLiveCurrentSlice<0 || gpuLiveCurrentSlice>=gpuLiveSlices.Count)return;
            var slice=gpuLiveSlices[gpuLiveCurrentSlice];int zero=0,unknown=0,enabledCount=0;
            for(int i=0;i<slice.MasterItems.Length;i++)
            {
                var a=slice.Channels[i*16+10];var b=slice.Channels[i*16+11];
                if(GpuDecoAlphaPolicy.Hidden(a.end,a.sourceIndex,b.end,b.sourceIndex))zero++;
                else if(a.sourceIndex>=0 || b.sourceIndex>=0)unknown++;
            }
            int visibleCount=0;foreach(var renderer in slice.Renderers)if(renderer && renderer.enabled){enabledCount++;if(renderer.isVisible)visibleCount++;}
            GpuLog("render_census window="+window+" edge="+edge+" slice="+gpuLiveCurrentSlice+" allocated_renderers="+slice.Renderers.Count+" enabled_renderers="+enabledCount+" visible_renderers="+visibleCount
                +" items="+slice.MasterItems.Length+" proven_constant_zero_alpha="+zero+" animated_alpha="+unknown+" zeroalpha="+gpuZeroAlpha+" skipped_by_this_probe=0");
        }
        static bool HandleG2Command(string sub)
        {
            var a = sub.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 1 && a[0] == "reset")
            {
                g2ThreadCpu?.Dispose();g2ThreadCpu=null;
                g2RenderMetrics?.Dispose();g2RenderMetrics=null;
                StopGpuLive("g2_reset");
                g2Window = -1; g2Error = null;
                Array.Clear(g2WindowSeen, 0, 4); Array.Clear(g2WindowValid, 0, 4);
                Array.Clear(g2WindowFps, 0, 4); Array.Clear(g2WindowCostMs, 0, 4);
                Array.Clear(g2WindowProgressMs, 0, 4); Array.Clear(g2WindowUniformMs, 0, 4);
                Array.Clear(g2WindowDispatchMs, 0, 4); Array.Clear(g2WindowMiscMs, 0, 4);
                Array.Clear(g2WindowMaxCostMs, 0, 4);
                Array.Clear(g2WindowSpikes, 0, 4); Array.Clear(g2WindowWorstMs, 0, 4);
                g2StabPauseResume = g2StabSongEnd = g2StabRetryRestart = g2StabReopenToggle = false;
                gpuCostFrames = gpuCostProgressTicks = gpuCostUniformTicks = gpuCostDispatchTicks = gpuCostMiscTicks = 0;
                gpuCostMaxMs = 0;
                GpuLog("g2_reset=True");
                return true;
            }
            if (a.Length == 2 && a[0] == "ready")
            {
                bool on = a[1] == "on";
                if (a[1] != "on" && a[1] != "off") throw new ArgumentException("gpudeco g2 ready on|off");
                double song = scrConductor.instance ? scrConductor.instance.songposition_minusi : double.NaN;
                RequireLive(FrameCompare.Paused && gpuLiveActive == on && (!on || GpuLiveOwned > 0) && Math.Abs(song - 258.0) <= 0.6,
                    "ready requires paused state at 258s with matching mode");
                GpuLog("g2_prepared mode=" + a[1] + " song=" + song.ToString("F6", Inv) + " paused=True owned=" + GpuLiveOwned
                    + " transition_ms=" + (on ? gpuLiveTransitionMs : 0.0).ToString("F6", Inv)
                    + " prepare_ms=" + gpuLivePrepareMs.ToString("F6", Inv)
                    + " resume_then_play_to=262 minimum_on_warmup_s=3");
                return true;
            }
            if (a.Length == 3 && a[0] == "begin")
            {
                bool nextMode = a[1] == "on";
                int idx = int.Parse(a[2], Inv);
                double song = scrConductor.instance ? scrConductor.instance.songposition_minusi : double.NaN;
                float warmup = nextMode ? Time.realtimeSinceStartup - gpuLiveEnabledAt : 0f;
                bool expectedMode = idx == 1 || idx == 2;
                RequireLive(!GpuDecoCostProbe.Enabled && g2Window == -1 && idx >= 0 && idx <= 3 && nextMode == expectedMode && !FrameCompare.Paused
                    && gpuLiveActive == nextMode && (!nextMode || (GpuLiveOwned > 0 && warmup >= 3f))
                    && Math.Abs(song - 262.0) <= 0.6,
                    "invalid ABBA window begin idx=" + idx + " mode=" + a[1] + " song=" + song.ToString("F6", Inv) + " warmup=" + warmup.ToString("F3", Inv));
                g2Window = idx; g2WindowMode = nextMode; g2WindowSongStart = song;
                g2NativeStart=GpuDecoCopy.ReadTotals();
                if(GpuDecoThreadCpu.Enabled)GpuLog("diagnostic_screen window="+idx+" "+GpuDecoRenderMetrics.ScreenState());
                if(GpuDecoThreadCpu.Enabled)LogGpuRenderCensus(idx,"begin");
                g2RenderMetrics?.Dispose();g2RenderMetrics=GpuDecoThreadCpu.Enabled?new GpuDecoRenderMetrics():null;
                g2ThreadCpu?.Dispose();g2ThreadCpu=GpuDecoThreadCpu.Enabled?GpuDecoThreadCpu.Begin(GpuLog):null;
                g2WindowFrame = Time.frameCount; g2WindowClock = Stopwatch.GetTimestamp();
                g2WindowSpikes[idx] = 0; g2WindowWorstMs[idx] = 0;
                g2CostFrames0 = gpuCostFrames; g2ProgressTicks0 = gpuCostProgressTicks;
                g2UniformTicks0 = gpuCostUniformTicks; g2DispatchTicks0 = gpuCostDispatchTicks;
                g2MiscTicks0 = gpuCostMiscTicks; g2CostMax0 = gpuCostMaxMs; gpuCostMaxMs = 0;
                GpuLog("g2_window_begin index=" + idx + " mode=" + a[1] + " song=" + song.ToString("F6", Inv)
                    + " warmup_excluded_s=" + warmup.ToString("F3", Inv)
                    + " minimum_on_warmup_s=3 transition_ms=" + (nextMode ? gpuLiveTransitionMs : 0.0).ToString("F6", Inv));
                return true;
            }
            if (a.Length == 1 && a[0] == "end")
            {
                RequireLive(g2Window >= 0 && g2Window <= 3, "no active G2 window");
                int idx = g2Window; g2Window = -1;
                double elapsed = (Stopwatch.GetTimestamp() - g2WindowClock) / (double)Stopwatch.Frequency;
                int frames = Time.frameCount - g2WindowFrame;
                // Snapshot after the FPS window; this may include boundary callbacks.
                // Count it as an upper bound, never omit render-thread copy CPU.
                var nativeEnd=GpuDecoCopy.ReadTotals();
                var threadCpu=g2ThreadCpu;g2ThreadCpu=null;
                threadCpu?.Complete(idx,frames,elapsed,GpuDecoCopy.RenderThreadId(),GpuLog);
                var renderMetrics=g2RenderMetrics;g2RenderMetrics=null;
                renderMetrics?.Report(idx,GpuLog);
                if(renderMetrics!=null)LogGpuRenderCensus(idx,"end");
                double fps = frames / Math.Max(1e-6, elapsed);
                double songEnd = scrConductor.instance ? scrConductor.instance.songposition_minusi : double.NaN;
                bool valid = !FrameCompare.Paused && gpuLiveActive == g2WindowMode && (!g2WindowMode || GpuLiveOwned > 0)
                    && frames > 0 && elapsed > 0 && Math.Abs(songEnd - 276.0) <= 0.6 && songEnd > g2WindowSongStart && Hitch.Playing;
                valid &= nativeEnd.Errors==0 && (!g2WindowMode || !GpuDecoCopy.Enabled || nativeEnd.Executed>g2NativeStart.Executed);
                g2WindowSeen[idx] = true; g2WindowValid[idx] = valid; g2WindowFps[idx] = fps;
                if (g2WindowMode)
                {
                    long cf = Math.Max(1L, gpuCostFrames - g2CostFrames0);
                    double tf = Stopwatch.Frequency / 1000.0;
                    double pMs = (gpuCostProgressTicks - g2ProgressTicks0) / tf / cf;
                    double uMs = (gpuCostUniformTicks - g2UniformTicks0) / tf / cf;
                    double dMs = (gpuCostDispatchTicks - g2DispatchTicks0) / tf / cf;
                    double mMs = (gpuCostMiscTicks - g2MiscTicks0) / tf / cf;
                    double nativeMs=(nativeEnd.Ticks-g2NativeStart.Ticks)/tf/cf;
                    double managedMs=pMs+uMs+dMs+mMs;
                    double totMs = managedMs+nativeMs;
                    g2WindowCostMs[idx] = totMs; g2WindowProgressMs[idx] = pMs;
                    g2WindowUniformMs[idx] = uMs; g2WindowDispatchMs[idx] = dMs;
                    g2WindowMiscMs[idx] = mMs; g2WindowMaxCostMs[idx] = gpuCostMaxMs;
                    GpuLog("cost window=" + idx + " frames=" + cf
                        + " total_ms=" + totMs.ToString("F6", Inv)
                        + " progress_ms=" + pMs.ToString("F6", Inv)
                        + " uniform_ms=" + uMs.ToString("F6", Inv)
                        + " dispatch_ms=" + dMs.ToString("F6", Inv)
                        + " misc_ms=" + mMs.ToString("F6", Inv)
                        + " managed_ms="+managedMs.ToString("F6",Inv)+" native_upper_ms="+nativeMs.ToString("F6",Inv)
                        + " native_callbacks="+(nativeEnd.Executed-g2NativeStart.Executed)+" native_submitted="+(nativeEnd.Submitted-g2NativeStart.Submitted)
                        + " native_errors="+(nativeEnd.Errors-g2NativeStart.Errors)
                        + " native_max_lifetime_ms="+(nativeEnd.MaxTicks/tf).ToString("F6",Inv)
                        + " max_ms=" + gpuCostMaxMs.ToString("F6", Inv)
                        + " budget_le_0.5ms=" + (totMs <= 0.5));
                }
                gpuCostMaxMs = Math.Max(g2CostMax0, gpuCostMaxMs);
                GpuLog("FPS mode=" + (g2WindowMode ? "on" : "off") + " index=" + idx
                    + " fps=" + fps.ToString("F6", Inv) + " ms=" + (1000.0 / fps).ToString("F6", Inv)
                    + " frames=" + frames + " elapsed_s=" + elapsed.ToString("F6", Inv)
                    + " song_begin=" + g2WindowSongStart.ToString("F6", Inv) + " song_end=" + songEnd.ToString("F6", Inv)
                    + " spikes_30ms=" + g2WindowSpikes[idx] + " worst_frame_ms=" + g2WindowWorstMs[idx].ToString("F6", Inv)
                    + " valid=" + valid);
                return true;
            }
            if (a.Length == 2 && a[0] == "stability")
            {
                string kind = a[1];
                int sfErr = Resilience.TotalErrors;
                if (kind == "pause_resume")
                {
                    bool ok = gpuLiveActive && GpuLiveOwned > 0 && !FrameCompare.Paused && Hitch.Playing && sfErr == 0;
                    g2StabPauseResume = ok;
                    if (!ok && g2Error == null) g2Error = "stability_pause_resume_failed";
                    GpuLog("stability checkpoint=pause_resume pass=" + ok + " active=" + gpuLiveActive + " owned=" + GpuLiveOwned + " sf_errors=" + sfErr);
                    return true;
                }
                if (kind == "song_end")
                {
                    int unrestored = CountUnrestoredOwned();
                    bool ok = gpuLivePortalObserved && !gpuLiveActive && unrestored == 0 && sfErr == 0;
                    g2StabSongEnd = ok;
                    if (!ok && g2Error == null) g2Error = "stability_song_end_failed";
                    GpuLog("stability checkpoint=song_end pass=" + ok + " portal_observed="+gpuLivePortalObserved+" active=" + gpuLiveActive + " unrestored=" + unrestored + " sf_errors=" + sfErr);
                    return true;
                }
                if (kind == "retry_restart")
                {
                    int unrestored = CountUnrestoredOwned();
                    bool ok = !gpuLiveActive && unrestored == 0 && sfErr == 0;
                    g2StabRetryRestart = ok;
                    if (!ok && g2Error == null) g2Error = "stability_retry_restart_failed";
                    GpuLog("stability checkpoint=retry_restart pass=" + ok + " active=" + gpuLiveActive + " unrestored=" + unrestored + " sf_errors=" + sfErr);
                    return true;
                }
                if (kind == "reopen_toggle")
                {
                    int unrestoredBefore = CountUnrestoredOwned();
                    DisposeGpuLive();
                    GC.Collect();
                    int activeBuffers = gpuLiveSlices.Sum(s => s.Buffers.Count);
                    int activeMeshes = gpuLiveSlices.Sum(s => s.Meshes.Count);
                    bool ok = !gpuLiveActive && !gpuLivePrepared && unrestoredBefore == 0 && activeBuffers == 0 && activeMeshes == 0 && sfErr == 0;
                    g2StabReopenToggle = ok;
                    if (!ok && g2Error == null) g2Error = "stability_reopen_toggle_failed";
                    GpuLog("stability checkpoint=reopen_toggle pass=" + ok + " active=" + gpuLiveActive
                        + " unrestored=" + unrestoredBefore + " leaked_buffers=" + activeBuffers
                        + " leaked_meshes=" + activeMeshes + " heap_mb=" + (GC.GetTotalMemory(false) >> 20) + " sf_errors=" + sfErr);
                    return true;
                }
                throw new ArgumentException("gpudeco g2 stability pause_resume|song_end|retry_restart|reopen_toggle");
            }
            if (a.Length == 1 && a[0] == "result")
            {
                DisposeGpuLive();
                int sfErr = Resilience.TotalErrors;
                bool allWindows = g2WindowSeen.All(x => x) && g2WindowValid.All(x => x);
                bool allStab = g2StabPauseResume && g2StabSongEnd && g2StabRetryRestart && g2StabReopenToggle && sfErr == 0;
                double offFps = (g2WindowFps[0] + g2WindowFps[3]) * 0.5;
                double onFps = (g2WindowFps[1] + g2WindowFps[2]) * 0.5;
                double gainPct = offFps > 0 ? (onFps - offFps) / offFps * 100.0 : 0.0;
                double costMs = (g2WindowCostMs[1] + g2WindowCostMs[2]) * 0.5;
                double progMs = (g2WindowProgressMs[1] + g2WindowProgressMs[2]) * 0.5;
                double unifMs = (g2WindowUniformMs[1] + g2WindowUniformMs[2]) * 0.5;
                double dispMs = (g2WindowDispatchMs[1] + g2WindowDispatchMs[2]) * 0.5;
                double miscMs = (g2WindowMiscMs[1] + g2WindowMiscMs[2]) * 0.5;
                int spikesOff = g2WindowSpikes[0] + g2WindowSpikes[3];
                int spikesOn = g2WindowSpikes[1] + g2WindowSpikes[2];
                int newSpikes = Math.Max(0, spikesOn - spikesOff);
                string verdict, reason;
                if (!allWindows)
                {
                    verdict = "ERROR";
                    reason = g2Error ?? "missing_or_invalid_abba_window";
                }
                else if (!allStab || newSpikes > 0)
                {
                    verdict = "STABILITY_FAIL";
                    reason = !allStab ? (g2Error ?? "stability_checkpoint_failed") : "new_frame_spikes_gt_30ms";
                }
                else if (gainPct < 25.0 || costMs > 0.5)
                {
                    verdict = "FPS_FAIL";
                    reason = gainPct < 25.0 ? "fps_gain_lt_25pct" : "cpu_cost_gt_0.5ms";
                }
                else
                {
                    verdict = "PASS";
                    reason = "abba_fps_cpu_cost_and_stability_passed";
                }
                GpuLog("GPUDECO_G2=" + verdict + " reason=" + reason
                    + " on_fps=" + onFps.ToString("F6", Inv)
                    + " off_fps=" + offFps.ToString("F6", Inv)
                    + " gain_pct=" + gainPct.ToString("F6", Inv)
                    + " cost_ms=" + costMs.ToString("F6", Inv)
                    + " progress_ms=" + progMs.ToString("F6", Inv)
                    + " uniform_ms=" + unifMs.ToString("F6", Inv)
                    + " dispatch_ms=" + dispMs.ToString("F6", Inv)
                    + " misc_ms=" + miscMs.ToString("F6", Inv)
                    + " prepare_ms=" + gpuLivePrepareMs.ToString("F6", Inv)
                    + " transition_ms=" + gpuLiveTransitionMs.ToString("F6", Inv)
                    + " spikes30ms_on=" + spikesOn + " spikes30ms_off=" + spikesOff
                    + " sf_errors=" + sfErr
                    + " stability=" + allStab);
                return true;
            }
            throw new ArgumentException("gpudeco g2 reset|ready|begin|end|stability|result");
        }

        internal static bool GpuStep(string arg)
        {
            string command = (arg ?? "").Trim();
            if (command == "ready") return Step("ready");
            if (command == "scheduled")
            {
                CheckGpuPrepareBudget();
                if (!enabled || status.StartsWith("중단:")) throw new InvalidOperationException("gpudeco scheduled: " + status);
                bool scheduled = collector != null || job != null || ready;
                if (scheduled) GpuLog("prepare_stage=PrepVfx-observed generation=" + generation + " status=" + status);
                return scheduled;
            }
            if (command.StartsWith("bundle ", StringComparison.OrdinalIgnoreCase))
            {
                gpuBundlePath = Path.GetFullPath(command.Substring(7).Trim());
                if (!File.Exists(gpuBundlePath)) throw new FileNotFoundException("gpudeco bundle", gpuBundlePath);
                return true;
            }
            if (command == "live prepare") { PrepareGpuLive(); return true; }
            if (command == "live scope song" || command == "live scope reference")
            {
                RequireLive(!gpuLiveActive && g2Window<0,"stop playback ownership before changing scope");
                DisposeGpuLive();
                gpuScope=command.EndsWith(" song",StringComparison.Ordinal)?GpuDecoScopePlan.Song():GpuDecoScopePlan.Reference();
                GpuLog("scope="+(gpuScope.WholeSong?"song_union":"reference")+" default_off=True Player_included=False");
                return true;
            }
            if (command == "live on") { StartGpuLive(); return true; }
            if (command == "live off") { StopGpuLive("live_off"); return true; }
            if (command == "live state")
            {
                // Boundary-only query; render-thread time is not GPU time.
                var native=GpuDecoCopy.ReadTotals();
                GpuLog("live_state active=" + gpuLiveActive + " prepared=" + gpuLivePrepared
                    + " owned=" + GpuLiveOwned + " active_sources=" + gpuActiveSourceSlots.Count
                    + " slice=" + gpuLiveCurrentSlice + " skipped_recs=" + gpuLiveSkippedRecs
                    + " native_submitted="+native.Submitted+" native_executed="+native.Executed+" native_errors="+native.Errors
                    + " native_cpu_total_ms="+(native.Ticks*1000.0/Stopwatch.Frequency).ToString("F6",Inv)
                    + " native_cpu_lifetime_max_ms="+(native.MaxTicks*1000.0/Stopwatch.Frequency).ToString("F6",Inv));
                return true;
            }
            if(command=="nombp on"||command=="nombp off"){RequireLive(!gpuLiveActive,"nombp must be set before live prepare/on");GpuDecoNativeBatch.NoMpb=command.EndsWith("on");GpuLog("nombp="+GpuDecoNativeBatch.NoMpb);return true;}
            if(command=="live census") {LogGpuRenderCensus(-1,"explicit");return true;}
            // rendermerge playbackready 같은 대기형은 false 가 "아직" 이다(다른 명령처럼 사용법 예외로 떨어지지 않게).
            if(command.StartsWith("rendermerge ",StringComparison.Ordinal))return GpuRenderMergeCommand(command);
#if AUTOTEST
            if(GpuAblationCommand(command))return true;
            if(GpuStorageCommand(command))return true;
            if(GpuImagePlanCommand(command))return true;
            if(command=="resources mark"){MarkGpuResources();return true;}
            if(command=="effectcache fixture"){TestGpuEffectCache();return true;}
            if(command=="packetfixture"){GpuLog(GpuDecoPacketFixture.Run());return true;}
            if(command=="packetguardfixture"){GpuLog(PacketGuardFixture());return true;}
            if(GpuPacketCommand(command))return true;
            if(command=="effectcache on"||command=="effectcache off")
            {
                RequireLive(!gpuLiveActive,"effect cache toggle requires inactive GPU ownership");
                gpuEffectCache=command.EndsWith(" on",StringComparison.Ordinal);gpuCurrentFxParsed=false;
                GpuLog("effect_cache="+gpuEffectCache+" research_only=True default=False");return true;
            }
            if(command=="micro begin"||command=="micro end"){GpuMicroWindow(command.EndsWith(" begin",StringComparison.Ordinal)?"begin":"end");return true;}
            if(command=="resources clean" || command=="resources cleanreload"){CheckGpuResources(command.EndsWith("reload",StringComparison.Ordinal));return true;}
            if(command=="resources restoreauto"){RestoreGpuProbeAuto();return true;}
            if(command=="reload")return AutoTest.ReloadGpuTrace();
            if(command=="originaltrace on" || command=="originaltrace off")
            {FastMove.SetGpuOriginalTrace(command.EndsWith(" on",StringComparison.Ordinal));GpuLog("original_guard_trace="+FastMove.GpuOriginalTrace+" one_fallback_only=True performance=False");return true;}
            if(command=="live values"){ProbeGpuActivationValues();return true;}
            if(command=="resumecheck supported" || command=="resumecheck handoff")
            {
                RequireLive(FrameCompare.Paused && !Hitch.Playing && gpuLiveActive && g2Window==-1,
                    "resume fixture requires paused active GPU and stale playing sample");
                RunGpuResumeFrameFixture(command.EndsWith(" handoff",StringComparison.Ordinal));return true;
            }
            if(command.StartsWith("transitioncheck ",StringComparison.Ordinal))
            {
                RequireLive(FrameCompare.Paused && gpuLiveActive && g2Window==-1,"transition fixture requires paused GPU ownership");
                string kind=command.Substring(16);
                RequireLive(new[]{"off","reset","generation","shutdown"}.Contains(kind),"unknown CPU transition");
                var plan=BuildGpuResumePlan(out int unknown);GpuLog("transition_audit kind="+kind+" unobserved_order_excluded="+unknown);
                GpuLog(DecoAnim.TestGpuResumeHandoff(plan,()=>RunGpuCpuTransition(kind)));return true;
            }
            if(command.StartsWith("activationcheck ",StringComparison.Ordinal))
            {
                RequireLive(FrameCompare.Paused && !gpuLiveActive && gpuLivePrepared && g2Window==-1,"activation fixture requires paused prepared CPU ownership");
                string stage="activation_"+command.Substring(16);
                RequireLive(new[]{"activation_after_record","activation_after_capture","activation_after_dispatch"}.Contains(stage),"unknown activation stage");
                GpuLog(DecoAnim.TestGpuResumeRoundtrip(()=>RunGpuActivationFault(stage),d=>gpuLiveMasterByDeco.ContainsKey(d)));return true;
            }
            if(command.StartsWith("recovery ",StringComparison.Ordinal))
            {
                RequireLive(FrameCompare.Paused && gpuLiveActive && g2Window==-1,"recovery fixture requires paused active ownership");
                string stage=command.Substring(9);
                if(stage=="force"){TestGpuForcedQuiescence();return true;}
                RequireLive(new[]{"render_before","render_after","preflight","target_after_channel"}.Contains(stage),"unknown recovery fixture");
                var plan=BuildGpuResumePlan(out int unknown);
                GpuLog("recovery_audit stage="+stage+" unobserved_order_excluded="+unknown);
                GpuLog(DecoAnim.TestGpuResumeHandoff(plan,()=>RunGpuRecoveryFault(stage)));return true;
            }
            if(command=="cpu census")
            {
                RequireLive(gpuLivePrepared && g2Window==-1,"CPU census is a preparation/boundary diagnostic only");
                // Boundary-only lookup; never re-run admission here, as it can
                // update an uninitialized parallax Transform. No frame probes.
                var baselineIndex=new Dictionary<scrDecoration,int>();
                for(int i=0;i<baselineObjects.Length;i++)if(baselineObjects[i])baselineIndex[baselineObjects[i]]=i;
                var visualFlags=new int[baselineObjects.Length];
                bool visualKnown=baselineVisualFlags!=null&&baselineEvents!=null&&baselineVisualFlags.Length==baselineEvents.Length;
                if(visualKnown)for(int e=0;e<baselineEvents.Length;e++)
                    foreach(int i in baselineEvents[e].Targets)visualFlags[i]|=baselineVisualFlags[e];
                string Exclusion(scrDecoration d)
                {
                    if(gpuLiveMasterByDeco.TryGetValue(d,out int master))
                        return GpuCpuFallback(master)?"live_CPU_fallback":"allocated_not_owned";
                    if(!baselineIndex.TryGetValue(d,out int i))return "outside_baseline";
                    var rs=table.Decorations[i].Reasons;
                    return (rs.Count==0?"live_geometry_or_scope_rejection":string.Join("+",rs.OrderBy(r=>r,StringComparer.Ordinal)))
                        +"[visual="+(visualKnown?visualFlags[i].ToString(Inv):"UNMEASURED")+"]";
                }
                GpuLog(DecoAnim.GpuCpuCensus(d=>gpuLiveActive?gpuLiveOwnedSet.Contains(d):gpuLiveMasterByDeco.ContainsKey(d),Exclusion)+" source_clocks="+gpuActiveSourceSlots.Count
                    +" allocated_total="+gpuLiveOwnedIndices.Length+" GPU_owned="+GpuLiveOwned+" GPU_active="+gpuLiveActive
                    +" raw_song="+(scrConductor.instance?scrConductor.instance.songposition_minusi.ToString("R",Inv):"UNMEASURED")
                    +" visual_flag_bits=image1,original_size2,smoothing4,crop8");return true;
            }
            if(command=="resumetest keys" || command=="resumetest gpukeys")
            {
                RequireLive(FrameCompare.Paused && !gpuLiveActive && g2Window==-1,"key fixture requires paused CPU ownership");
                bool gpu=command=="resumetest gpukeys";
                RequireLive(!gpu || gpuLivePrepared,"GPU key fixture requires prepared scope");
                var idle=baselineObjects?.OfType<scrVisualDecoration>().FirstOrDefault(d=>d && d.pivotTrans!=null && d.eventTweens!=null
                    && d.eventTweens.Count==0 && DecoAnim.HasParallax(d) && (!gpu || gpuLiveMasterByDeco.ContainsKey(d)));
                RequireLive(idle!=null,"key fixture has no idle decoration; not measured");
                GpuLog(DecoAnim.GpuResumeKeyFixture(idle,gpu?(Action)(()=>{StartGpuLive();StopGpuLive("live_off");}):null));return true;
            }
            if(command=="resumetest handoff")
            {
                RequireLive(FrameCompare.Paused && gpuLiveActive && g2Window==-1,"resume handoff fixture requires paused GPU ownership");
                var plan=BuildGpuResumePlan(out int unknown);
                GpuLog("resume_handoff unobserved_order_excluded="+unknown);
                GpuLog(DecoAnim.TestGpuResumeHandoff(plan,()=>StopGpuLive("live_off")));return true;
            }
            if(command=="resumetest")
            {
                RequireLive(FrameCompare.Paused && !gpuLiveActive && gpuLivePrepared && g2Window==-1,"resume fixture requires paused prepared CPU playback");
                GpuLog(DecoAnim.TestGpuResumeRoundtrip(()=>{StartGpuLive();StopGpuLive("live_off");},d=>gpuLiveMasterByDeco.ContainsKey(d)));return true;
            }
            if(command=="resumetest result")
            {
                string result=DecoAnim.GpuResumeAuditResult;GpuLog(result);
                RequireLive(result.Contains("frames=12 ") && result.Contains("mismatch=0 ") && !result.Contains("continued=0 "),"resume continuation audit failed");return true;
            }
#endif
            if(command.StartsWith("costprobe ",StringComparison.Ordinal))
            {
                string action=command.Substring(10);
                RequireLive(g2Window==-1,"cost probe is not a G2 acceptance measurement");
                if(action=="report") {GpuLog(GpuDecoCostProbe.Report());return true;}
                RequireLive(!gpuLiveActive || FrameCompare.Paused,"cost probe changes require stopped or paused playback");
                if(action=="on" || action=="reset") {GpuDecoCostProbe.Reset();GpuDecoCostProbe.Enabled=true;}
                else if(action=="off") {GpuLog(GpuDecoCostProbe.Report());GpuDecoCostProbe.Enabled=false;}
                else throw new ArgumentException("costprobe on|off|reset|report");
                GpuLog("cost_probe_enabled="+GpuDecoCostProbe.Enabled+" acceptance=False");return true;
            }
            if(command=="copymask all" || command=="copymask active")
            {
                RequireLive(FrameCompare.Paused && gpuLiveActive && gpuLivePreparedMasked,"paused masked-copy ownership required");
                gpuCopyMaskBypass=command.EndsWith(" all",StringComparison.Ordinal);
                var mask=gpuLiveSlices[gpuLiveCurrentSlice].CopyMask;
                int chunks=0;ulong low=mask.Low,high=mask.High;
                while(low!=0){low&=low-1;chunks++;}while(high!=0){high&=high-1;chunks++;}
                GpuLog("copy_mask bypass="+gpuCopyMaskBypass+" active_chunks="+chunks+" total_chunks="
                    +((gpuLiveOwnedRenderers.Length+511)/512)+" renderer_order_changed=False");return true;
            }
            if(command=="zeroalpha on" || command=="zeroalpha off")
            {
                RequireLive(g2Window==-1 && (!gpuLiveActive || FrameCompare.Paused),"zeroalpha changes require stopped or paused playback");
                bool next=command.EndsWith(" on",StringComparison.Ordinal);
                RequireLive(!next || GpuDecoNativeBatch.Enabled,"zeroalpha requires independent renderer path");
                gpuZeroAlpha=next;foreach(var slice in gpuLiveSlices)slice.RefreshAlpha();
                GpuLog("zeroalpha="+gpuZeroAlpha+" rule=both_factors_constant_one_exactly_zero count="+gpuLiveSlices.Count+" collisions_changed=0");return true;
            }
            if (command.StartsWith("g2 ", StringComparison.OrdinalIgnoreCase)) return HandleG2Command(command.Substring(3));
            if (command=="threadcpu on" || command=="threadcpu off")
            {
                RequireLive(g2Window==-1,"threadcpu changes require no active G2 window");
#if AUTOTEST
                RequireLive(gpuMicroTick==0,"threadcpu changes require no active micro window");
#endif
                g2ThreadCpu?.Dispose();g2ThreadCpu=null;
                g2RenderMetrics?.Dispose();g2RenderMetrics=null;
                GpuDecoThreadCpu.Enabled=command.EndsWith(" on",StringComparison.Ordinal);
                GpuLog("threadcpu enabled="+GpuDecoThreadCpu.Enabled+" sampling=G2_or_explicit_micro_boundaries_only scheduling_changes=0");return true;
            }
            if (command == "reset")
            {
                InvalidateGpuSnapshot(); StopGpuLive("reset");
#if AUTOTEST
                gpuPixelGate = new GpuDecoPixelGate();
                GpuDecoSnapshot.SetDiagnostic("off");
#endif
                gpuCapture = false; gpuSource = null; gpuPrepareWatch = null; gpuExportOwnedLow = false; gpuExports = 0; GpuLog("export_result_reset=True"); return true;
            }
            if (command == "prepare") { gpuCaptureEpoch++; gpuCapture = true; ResetGpuFrames(); gpuPrepareWatch = Stopwatch.StartNew(); GpuLog("prepare_begin budget_ms=300000 includes=open,bake,seek,export source_clock_capture=True"); return true; }
            if (command == "off") { InvalidateGpuSnapshot(); StopGpuLive("off"); gpuCapture = false; gpuSource = null; gpuPrepareWatch = null; GpuLog("on=False owned=0"); return true; }
            if (command == "state")
            {
#if AUTOTEST
                bool anyOn = GpuDecoSnapshot.Active || gpuLiveActive;
                int anyOwned = GpuDecoSnapshot.Active ? GpuDecoSnapshot.Owned : GpuLiveOwned;
                string stg = gpuLiveActive ? "live-G2" : "paused-G1";
                GpuLog("on=" + anyOn + " owned=" + anyOwned + " stage=" + stg + " ready=" + ready + " generation=" + Generation);
#else
                GpuLog("on=" + gpuLiveActive + " owned=" + GpuLiveOwned + " stage=" + (gpuLiveActive ? "live-G2" : "export-lab") + " ready=" + ready + " generation=" + Generation);
#endif
                return true;
            }
#if AUTOTEST
            // Request A.1: 1-command 5-candidate isolator on the current paused frame (multi-frame async readback state machine)
            if(command.StartsWith("alpha pixel ",StringComparison.Ordinal))
            {
                int sample=int.Parse(command.Substring(12),Inv);
                RequireLive(FrameCompare.Paused && gpuLiveActive && !gpuZeroAlpha,"alpha pixel requires restored unculled live state");
                var diff=FrameCompare.GatePixel(out long restored,out bool stable,false);
                FrameCompare.SaveGateShots("gpudeco-alpha-live-"+sample+"-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff",Inv));
                GpuLog("zeroalpha_pixel sample="+sample+" different="+diff.Changed+" pixels="+diff.OutsidePixels+" maxdiff="+diff.Max.Max()+" aa2="+restored+" stable="+stable
                    +" scope=live_cull_off_on_off_same_frozen_frame "+GpuDecoRenderMetrics.ScreenState());
                RequireLive(diff.Changed==0 && diff.Max.Max()==0 && restored==0 && stable,"zeroalpha paired pixels changed");return true;
            }
            if(command=="orderprobe") {GpuDecoOrderProbe.Run();return true;}
            if(command=="orderprobe scene") {GpuDecoOrderProbe.Run(true);return true;}
            if(command=="nativecopy on" || command=="nativecopy off" || command=="nativecopy on8" || command=="nativecopy on8masked" || command=="nativecopy onmasked")
            {
                RequireLive(!gpuLiveActive && !GpuDecoSnapshot.Active,"stop drawing before changing native copy mode");
                bool enabled=command!="nativecopy off";
                RequireLive(!enabled || (GpuDecoNativeBatch.Enabled && !GpuDecoNativeBatch.StaticCombine),"native copy requires nativebatch on");
                GpuDecoCopy.Enabled=enabled;
                GpuDecoCopy.EightUavEnabled=command=="nativecopy on8" || command=="nativecopy on8masked";
                GpuDecoCopy.MaskedEnabled=command=="nativecopy on8masked" || command=="nativecopy onmasked";
                GpuLog("native_copy="+enabled+" eight_uav="+GpuDecoCopy.EightUavEnabled+" masked="+GpuDecoCopy.MaskedEnabled+" active_contexts="+GpuDecoCopy.ActiveContexts);return true;
            }
            if(command=="nativebatch on" || command=="nativebatch off" || command=="nativebatch static" || command=="nativebatch staticbounds")
            {
                RequireLive(!gpuLiveActive && !GpuDecoSnapshot.Active,"stop drawing before changing native batch mode");
                GpuDecoNativeBatch.Enabled=command!="nativebatch off";
                GpuDecoNativeBatch.StaticCombine=command=="nativebatch static" || command=="nativebatch staticbounds";
                GpuDecoNativeBatch.RestoreSortBounds=command=="nativebatch staticbounds";
                GpuLog("native_batch="+GpuDecoNativeBatch.Enabled+" static_batch="+GpuDecoNativeBatch.StaticCombine+" restore_sort_bounds="+GpuDecoNativeBatch.RestoreSortBounds+" live_validated=False");return true;
            }
            if (command == "diag5")
                return StepG1Diag5();
            // Request A.2: Automated binary search isolating the minimal offending decoration set in the top diff region
            if (command.StartsWith("bisect", StringComparison.OrdinalIgnoreCase))
            {
                int regIdx = 0;
                if (command.Length > 6) int.TryParse(command.Substring(6).Trim(), NumberStyles.Integer, Inv, out regIdx);
                return StepG1Bisect(regIdx);
            }
            // Request A.2: CPU vs GPU vertex world/clip/screen & RGBA oracle comparison
            if (command.StartsWith("oracle", StringComparison.OrdinalIgnoreCase))
            {
                int targetId = 0;
                if (command.Length > 6) int.TryParse(command.Substring(6).Trim(), NumberStyles.Integer, Inv, out targetId);
                bool startedHere = false;
                if (!GpuDecoSnapshot.Active)
                {
                    GpuDecoSnapshot.Start(gpuLastSnapshot, baselineObjects, gpuBundlePath);
                    startedHere = true;
                }
                try { GpuDecoSnapshot.CompareSpriteVsGpuOracle(baselineObjects, targetId, 32); }
                finally { if (startedHere) GpuDecoSnapshot.Stop(); }
                return true;
            }
            if (command.StartsWith("diagnostic ", StringComparison.OrdinalIgnoreCase))
            {
                string mode = command.Substring(11).Trim();
                if (mode == "pixel")
                {
                    if (GpuDecoSnapshot.Active || gpuLastSnapshot == null || !FrameCompare.Paused
                        || gpuSnapshotGeneration != Generation || !scrConductor.instance
                        || Math.Abs(scrConductor.instance.songposition_minusi - gpuLastSnapshot.RawTime) > 1e-6)
                        throw new InvalidOperationException("diagnostic pixel requires draw off + unchanged paused export");
                    gpuPixelGate.Diagnostic();
                    var r = FrameCompare.GatePixel(out long restored, out bool stable, false);
                    FrameCompare.SaveGateShots("gpudeco-attribution-" + GpuDecoSnapshot.DiagnosticMode + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture));
                    GpuLog("GPUDECO_ATTRIBUTION=MEASURED mode=" + GpuDecoSnapshot.DiagnosticMode + " raw=" + gpuLastSnapshot.RawTime.ToString("R", CultureInfo.InvariantCulture)
                        + " changed=" + r.Changed + " total=" + r.OutsidePixels + " max=" + r.Max.Max() + " aa2=" + restored + " stable=" + stable + " regions=" + r.Regions
                        + " acceptance=NOT_EVALUATED live_animation=UNMEASURED");
                    TraceGpuPixelRegions(r.Regions);
                    return true;
                }
                GpuDecoSnapshot.SetDiagnostic(mode);
                if (mode != "off") gpuPixelGate.Diagnostic();
                GpuLog("diagnostic=" + mode + " acceptance=BLOCKED_UNTIL_RESET"); return true;
            }
            if (command == "draw off") { GpuDecoSnapshot.Stop(); return true; }
            if (command == "on")
            {
                if (gpuSnapshotGeneration == Generation && gpuLastSnapshot != null)
                {
                    gpuLastG0 = false;
                    try
                    {
                        if (string.IsNullOrEmpty(gpuBundlePath)) throw new InvalidOperationException("gpudeco: fresh export + bundle <path> required");
                        gpuLastG0 = GpuDecoSnapshot.Start(gpuLastSnapshot, baselineObjects, gpuBundlePath);
                    }
                    catch (Exception e) { gpuPixelGate.Error = e.Message.Contains("G0_FAIL") ? "G0_FAIL" : "renderer_error"; GpuLog("GPUDECO_G1=" + (gpuPixelGate.Error == "G0_FAIL" ? "G0_FAIL" : "ERROR") + " stage=paused-render reason=" + e.GetType().Name + ":" + e.Message); throw; }
                    return true;
                }
                if (gpuLivePrepared) { StartGpuLive(); return true; }
                gpuPixelGate.Error = "renderer_error";
                GpuLog("GPUDECO_G1=ERROR stage=paused-render reason=InvalidOperationException:gpudeco: fresh export + bundle <path> required");
                throw new InvalidOperationException("gpudeco: fresh export + bundle <path> required");
            }
            if (command.StartsWith("pixel ", StringComparison.OrdinalIgnoreCase))
            {
                int index = int.Parse(command.Substring(6), CultureInfo.InvariantCulture);
                if (GpuDecoSnapshot.Active || gpuLastSnapshot == null || index < 0 || index > 2 || Math.Abs(gpuLastSnapshot.RawTime - new[] { 262, 270, 276 }[index]) > .25
                    || gpuSnapshotGeneration != Generation || !scrConductor.instance || Math.Abs(scrConductor.instance.songposition_minusi - gpuLastSnapshot.RawTime) > 1e-6)
                { gpuPixelGate.Error = "invalid_pixel_state"; GpuLog("GPUDECO_G1=ERROR stage=pixel reason=invalid_pixel_state"); throw new InvalidOperationException("gpudeco pixel: draw off + same paused export/sample required"); }
                var r = FrameCompare.GatePixel(out long restored, out bool stable, false);
                FrameCompare.SaveGateShots("gpudeco-" + new[] { 262, 270, 276 }[index] + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture));
                gpuPixelGate.Pixel(index, gpuLastSnapshot.Awake, gpuLastSnapshot.Items.Length, gpuLastG0, r.Changed, r.OutsidePixels, r.Max.Max(), restored, stable);
                GpuLog("G1 sample=" + index + " raw=" + gpuLastSnapshot.RawTime.ToString("R", CultureInfo.InvariantCulture) + " different=" + r.Changed + " pixels=" + r.OutsidePixels + " maxdiff=" + r.Max.Max() + " aa2=" + restored + " stable=" + stable + " regions=" + r.Regions + " mask=none auxiliary=" + GpuDecoCoverage.Auxiliary(gpuLastSnapshot.Awake));
                if (r.Max.Max() > 3 || r.Ratio > .003) TraceGpuPixelRegions(r.Regions);
                return true;
            }
            if (command == "result") { string verdict = gpuPixelGate.Verdict(out string reason); GpuLog("GPUDECO_G1=" + (gpuExportOwnedLow ? "OWNED_LOW" : verdict) + " scope=paused_G1 exports=" + gpuExports + " reason=" + reason + " live_animation=" + (g2WindowSeen.Any(x => x) ? "MEASURED" : "UNMEASURED") + " G2=" + (g2WindowSeen.Any(x => x) ? "MEASURED" : "UNMEASURED")); return true; }
            if (command.StartsWith("export ", StringComparison.OrdinalIgnoreCase)) { ExportGpu(command.Substring(7).Trim()); return true; }
#endif
            if (command == "result") { GpuLog("GPUDECO_G1=" + (gpuExportOwnedLow ? "OWNED_LOW" : "ERROR") + " exports=" + gpuExports + " reason=AUTOTEST_paused_pixel_gate_required"); return true; }
            if (command == "on")
            {
                if (gpuLivePrepared) { StartGpuLive(); return true; }
                throw new InvalidOperationException("gpudeco on: live prepare or AUTOTEST paused G1 gate required");
            }
            throw new ArgumentException("gpudeco off|state|scheduled|ready|live prepare|live on|live off|live state|g2 ...|diag5|bisect|oracle|export <새 파일 경로>(AUTOTEST 전용)");
        }
        static void GpuLog(string s) => Main.Entry.Logger.Log("[gpudeco] " + s);
#if AUTOTEST
        // Request A.1: Multi-frame state machine running FrameCompare.Step across all 6 diagnostic modes on the same paused frame
        static readonly string[] G1DiagModes = { "unorm8", "floatcolor", "single", "world", "localz", "float_single_localz" };
        static int g1DiagIndex = -1, g1DiagPhase = 0, g1DiagWait = 0;

        static bool StepG1CaptureCycle(HashSet<int> excludedIds, out FrameCompareMetrics.Result result, out long restored, out bool stable)
        {
            result = null; restored = 0; stable = false;
            switch (g1DiagPhase)
            {
                case 0:
                    if (GpuDecoSnapshot.Active) GpuDecoSnapshot.Stop();
                    g1DiagPhase = 1;
                    return false;
                case 1:
                    if (!FrameCompare.Step("grab A")) return false;
                    GpuDecoSnapshot.Start(gpuLastSnapshot, baselineObjects, gpuBundlePath, excludedIds);
                    g1DiagWait = 0;
                    g1DiagPhase = 2;
                    return false;
                case 2:
                    if (++g1DiagWait < 3) return false;
                    g1DiagPhase = 3;
                    return false;
                case 3:
                    if (!FrameCompare.Step("grab B")) return false;
                    GpuDecoSnapshot.Stop();
                    g1DiagWait = 0;
                    g1DiagPhase = 4;
                    return false;
                case 4:
                    if (++g1DiagWait < 3) return false;
                    g1DiagPhase = 5;
                    return false;
                case 5:
                    if (!FrameCompare.Step("grab A2")) return false;
                    g1DiagPhase = 6;
                    return false;
                case 6:
                    if (!FrameCompare.Step("assertzero A A2 png")) return false;
                    g1DiagPhase = 7;
                    return false;
                case 7:
                    if (!FrameCompare.Step("diff A B 0 png")) return false;
                    result = FrameCompare.GatePixel(out restored, out stable, false);
                    g1DiagPhase = 0;
                    return true;
                default:
                    g1DiagPhase = 0;
                    return true;
            }
        }

        static bool StepG1Diag5()
        {
            if (gpuLastSnapshot == null || !FrameCompare.Paused || gpuSnapshotGeneration != Generation || string.IsNullOrEmpty(gpuBundlePath))
                throw new InvalidOperationException("gpudeco diag5 requires paused frame after gpudeco export + bundle");
            if (g1DiagIndex < 0)
            {
                gpuPixelGate.Diagnostic();
                g1DiagIndex = 0;
                g1DiagPhase = 0;
                GpuDecoSnapshot.SetDiagnostic(G1DiagModes[0]);
            }
            if (!StepG1CaptureCycle(null, out var r, out long restored, out bool stable))
                return false;
            string m = G1DiagModes[g1DiagIndex];
            GpuLog("G1_diag5 mode=" + m
                + " raw=" + gpuLastSnapshot.RawTime.ToString("R", Inv)
                + " changed=" + r.Changed
                + " maxdiff=" + r.Max.Max()
                + " max_rgba=" + string.Join(",", r.Max)
                + " aa2=" + restored
                + " stable=" + stable
                + " regions=" + r.Regions);
            g1DiagIndex++;
            if (g1DiagIndex < G1DiagModes.Length)
            {
                GpuDecoSnapshot.SetDiagnostic(G1DiagModes[g1DiagIndex]);
                return false;
            }
            g1DiagIndex = -1;
            GpuDecoSnapshot.SetDiagnostic("off");
            return true;
        }

        // Request A.2: Detailed diff-region dump (both owned items AND non-owned barrier renderers)
        static void TraceGpuPixelRegions(string regions)
        {
            var cam = GpuSplit.FindMain(); if (!cam || gpuLastSnapshot == null || string.IsNullOrEmpty(regions)) return;
            var ownedByRenderer = new Dictionary<int, GpuDecoData.Item>();
            foreach (var item in gpuLastSnapshot.Items)
            {
                var d = baselineObjects[item.Index] as scrVisualDecoration;
                if (d && d.spriteRenderer) ownedByRenderer[d.spriteRenderer.GetInstanceID()] = item;
            }
            foreach (string token in regions.Split(';'))
            {
                int close = token.IndexOf(')'); if (!token.StartsWith("(") || close < 0) continue;
                var v = token.Substring(1, close - 1).Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                if (v.Length != 4) continue;
                var area = new Rect(v[0], v[1], v[2], v[3]);
                int count = 0;
                foreach (var item in gpuLastSnapshot.Items)
                {
                    var p = Enumerable.Range(0, 4).Select(k => cam.WorldToScreenPoint(new Vector3(item.World[k * 3], item.World[k * 3 + 1], item.World[k * 3 + 2]))).ToArray();
                    var box = Rect.MinMaxRect(p.Min(x => x.x), p.Min(x => x.y), p.Max(x => x.x), p.Max(x => x.y));
                    if (!box.Overlaps(area)) continue;
                    var d = baselineObjects[item.Index] as scrVisualDecoration;
                    var sr = d ? d.spriteRenderer : null;
                    var m = sr ? sr.sharedMaterial : null;
                    string blend = m ? string.Join(",", new[] { "_SrcBlend", "_DstBlend", "_BlendOp" }.Select(n => n + "=" + (m.HasProperty(n) ? m.GetFloat(n).ToString("R", CultureInfo.InvariantCulture) : "absent"))) : "missing";
                    int spriteVerts = (sr && sr.sprite) ? sr.sprite.vertices.Length : -1;
                    float boundsZ = sr ? sr.bounds.center.z : float.NaN;
                    GpuLog("G1_region_candidate role=OWNED region=" + token + " index=" + item.Index + " id=" + item.Id
                        + " tex=" + item.TextureId + " mat=" + item.MaterialId
                        + " layer=" + item.Layer + " order=" + item.Order
                        + " z=" + item.World[2].ToString("R", CultureInfo.InvariantCulture)
                        + " boundsZ=" + boundsZ.ToString("R", CultureInfo.InvariantCulture)
                        + " alpha=" + item.Color[3].ToString("R", CultureInfo.InvariantCulture)
                        + " spriteVerts=" + spriteVerts
                        + " screenRect=(" + box.xMin.ToString("F1", Inv) + "," + box.yMin.ToString("F1", Inv) + "," + box.width.ToString("F1", Inv) + "," + box.height.ToString("F1", Inv) + ")"
                        + " blend=" + blend);
                    if (++count >= 16) break;
                }
                int barrierCount = 0;
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    if (!r || !r.gameObject.scene.IsValid() || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff
                        || (cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                    if (ownedByRenderer.ContainsKey(r.GetInstanceID())) continue;
                    var b = r.bounds;
                    Vector3 sMin = cam.WorldToScreenPoint(b.min), sMax = cam.WorldToScreenPoint(b.max);
                    var box = Rect.MinMaxRect(Mathf.Min(sMin.x, sMax.x), Mathf.Min(sMin.y, sMax.y), Mathf.Max(sMin.x, sMax.x), Mathf.Max(sMin.y, sMax.y));
                    if (!box.Overlaps(area)) continue;
                    var m = r.sharedMaterial;
                    GpuLog("G1_region_candidate role=BARRIER region=" + token + " rendererId=" + r.GetInstanceID()
                        + " type=" + r.GetType().Name + " name=" + r.name
                        + " mat=" + (m ? m.GetInstanceID() : 0) + " shader=" + (m && m.shader ? m.shader.name : "none")
                        + " layer=" + r.sortingLayerID + " order=" + r.sortingOrder
                        + " boundsZ=" + b.center.z.ToString("R", Inv));
                    if (++barrierCount >= 8) break;
                }
            }
        }

        // Request A.2: Multi-frame automated binary search excluding subsets of region-overlapping decorations
        static int g1BisectStage = -1, g1BisectLo = 0, g1BisectHi = 0, g1BisectStepCount = 0, g1BisectBaseMax = 0;
        static string g1BisectTargetPrefix;
        static readonly List<int> g1BisectCandidates = new List<int>();
        static HashSet<int> g1BisectActiveExcl;

        static bool StepG1Bisect(int regionIndex)
        {
            if (gpuLastSnapshot == null || !FrameCompare.Paused || gpuSnapshotGeneration != Generation || string.IsNullOrEmpty(gpuBundlePath))
                throw new InvalidOperationException("gpudeco bisect requires paused frame after gpudeco export + bundle");
            if (g1BisectStage < 0)
            {
                gpuPixelGate.Diagnostic();
                GpuDecoSnapshot.SetDiagnostic("off");
                g1BisectStage = 0;
                g1DiagPhase = 0;
                g1BisectStepCount = 0;
                g1BisectCandidates.Clear();
                g1BisectActiveExcl = null;
            }
            if (!StepG1CaptureCycle(g1BisectActiveExcl, out var res, out _, out _))
                return false;

            if (g1BisectStage == 0)
            {
                g1BisectBaseMax = res.Max.Max();
                GpuLog("G1_bisect_start base_changed=" + res.Changed + " base_max=" + g1BisectBaseMax + " regions=" + res.Regions);
                if (g1BisectBaseMax <= 3 || string.IsNullOrEmpty(res.Regions))
                {
                    GpuLog("G1_bisect_done status=already_within_tolerance max=" + g1BisectBaseMax);
                    g1BisectStage = -1;
                    return true;
                }
                var tokens = res.Regions.Split(';').Where(t => t.StartsWith("(") && t.Contains(")")).ToArray();
                if (tokens.Length == 0) { g1BisectStage = -1; return true; }
                string targetToken = tokens[Math.Min(Math.Max(0, regionIndex), tokens.Length - 1)];
                int close = targetToken.IndexOf(')');
                g1BisectTargetPrefix = targetToken.Substring(0, close + 1);
                var coords = targetToken.Substring(1, close - 1).Split(',').Select(x => float.Parse(x, Inv)).ToArray();
                var area = new Rect(coords[0], coords[1], coords[2], coords[3]);
                var cam = GpuSplit.FindMain();
                foreach (var item in gpuLastSnapshot.Items)
                {
                    var p = Enumerable.Range(0, 4).Select(k => cam.WorldToScreenPoint(new Vector3(item.World[k * 3], item.World[k * 3 + 1], item.World[k * 3 + 2]))).ToArray();
                    var box = Rect.MinMaxRect(p.Min(x => x.x), p.Min(x => x.y), p.Max(x => x.x), p.Max(x => x.y));
                    if (box.Overlaps(area)) g1BisectCandidates.Add(item.Id);
                }
                GpuLog("G1_bisect_candidates region=" + targetToken + " count=" + g1BisectCandidates.Count + " ids=" + string.Join(",", g1BisectCandidates));
                if (g1BisectCandidates.Count == 0) { g1BisectStage = -1; return true; }
                g1BisectLo = 0;
                g1BisectHi = g1BisectCandidates.Count - 1;
                if (g1BisectLo == g1BisectHi)
                {
                    FinishG1Bisect();
                    return true;
                }
                g1BisectStage = 1;
                PrepareNextG1BisectHalf();
                return false;
            }

            int mid = (g1BisectLo + g1BisectHi) / 2;
            bool regionGone = string.IsNullOrEmpty(res.Regions) || !res.Regions.Contains(g1BisectTargetPrefix);
            GpuLog("G1_bisect_step step=" + (++g1BisectStepCount) + " range=[" + g1BisectLo + ".." + mid + "]/" + g1BisectHi
                + " excluded=" + g1BisectActiveExcl.Count + " max=" + res.Max.Max() + " changed=" + res.Changed
                + " region_cleared=" + regionGone + " regions=" + res.Regions);
            if (regionGone || res.Max.Max() < g1BisectBaseMax)
                g1BisectHi = mid;
            else
                g1BisectLo = mid + 1;

            if (g1BisectLo < g1BisectHi)
            {
                PrepareNextG1BisectHalf();
                return false;
            }
            FinishG1Bisect();
            return true;
        }

        static void PrepareNextG1BisectHalf()
        {
            int mid = (g1BisectLo + g1BisectHi) / 2;
            g1BisectActiveExcl = new HashSet<int>();
            for (int i = g1BisectLo; i <= mid; i++) g1BisectActiveExcl.Add(g1BisectCandidates[i]);
        }

        static void FinishG1Bisect()
        {
            int culpritId = g1BisectCandidates[g1BisectLo];
            var culpritItem = gpuLastSnapshot.Items.First(x => x.Id == culpritId);
            GpuLog("G1_bisect_culprit id=" + culpritId + " index=" + culpritItem.Index
                + " tex=" + culpritItem.TextureId + " mat=" + culpritItem.MaterialId
                + " order=" + culpritItem.Order + " z=" + culpritItem.World[2].ToString("R", Inv)
                + " alpha=" + culpritItem.Color[3].ToString("R", Inv));
            GpuDecoSnapshot.Start(gpuLastSnapshot, baselineObjects, gpuBundlePath);
            try { GpuDecoSnapshot.CompareSpriteVsGpuOracle(baselineObjects, culpritId, 4); }
            finally { GpuDecoSnapshot.Stop(); }
            g1BisectStage = -1;
            g1BisectActiveExcl = null;
        }

        static void Put(float[] output, int index, Vector3 v) { output[index] = v.x; output[index + 1] = v.y; output[index + 2] = v.z; }
        static void ExportGpuAnchors(scrDecoration d, GpuDecoData.Item item, List<GpuDecoData.SourceClock> clocks,
            Dictionary<long, int> indices, Dictionary<string, int> counters)
        {
            foreach (var pair in d.eventTweens)
            {
                var r = pair.Value?.id as DecoAnim.Rec;
                if (r == null || r.GpuStartFrame < 0 || r.GpuGeneration != Generation || r.GpuCaptureEpoch != gpuCaptureEpoch || !ReferenceEquals(r.D, d)) continue;
                if (!r.Started && (!r.Running || r.Pos != 0 || r.Key == 7 || r.Key == 8)) continue;
                if (r.Pos < 0 || r.Dur <= 0 || (!r.Running && r.Pos < r.Dur)) continue;
                bool completed = !r.Running && r.Pos >= r.Dur;
                int clock = -1;
                long token = ((long)r.GpuSource << 32) | (uint)r.GpuStartFrame;
                if (!completed && !indices.TryGetValue(token, out clock))
                {
                    indices.Add(token, clock = clocks.Count);
                    clocks.Add(new GpuDecoData.SourceClock { Id = r.GpuSource, StartFrame = r.GpuStartFrame, StartSong = r.GpuStartSong, Progress = r.Pos });
                }
                if (!completed && clocks[clock].Progress != r.Pos) throw new InvalidOperationException("export: 같은 소스의 실제 진행값 불일치 source=" + r.GpuSource);
                Action<int, float, float, bool> add = (ch, start, target, scale) =>
                {
                    var s = new DecoBakeCore.Segment
                    {
                        Duration = r.Dur, Start = start, Target = target, End = target,
                        Ease = (int)r.E, Over = r.Over, Period = r.Period, IsScale = scale
                    };
                    if (scale) s.End = start + (target - start) * ZeroTween.Eval(r.E, r.Dur, r.Dur, r.Over, r.Period);
                    if (completed) s.Duration = 0;
                    item.Anchors[ch] = new GpuDecoData.Anchor { Valid = true, Clock = clock, Segment = s };
                    if (!r.Started) { counters.TryGetValue("pending_start", out int pending); counters["pending_start"] = pending + 1; }
                };
                switch (r.Key)
                {
                    case 1: case 2: case 3: case 4: case 5: case 10: case 12: case 13:
                        int ch = r.Key <= 5 ? r.Key - 1 : r.Key == 10 ? 11 : r.Key + 2;
                        add(ch, r.FStart, r.FEnd, false); break;
                    case 7: add(5, r.VStart.x, r.VEnd.x, true); break;
                    case 8: add(6, r.VStart.y, r.VEnd.y, true); break;
                    case 9:
                        for (int c = 0; c < 4; c++) add(7 + c, r.CStart[c], r.CEnd[c], false); break;
                    case 11:
                        add(12, r.VStart.x, r.VEnd.x, true); add(13, r.VStart.y, r.VEnd.y, true); break;
                }
            }
            for (int ch = 0; ch < 16; ch++)
            {
                string key = !item.Anchors[ch].Valid ? "table_fallback" : item.Anchors[ch].Segment.Duration == 0 ? "completed_endpoint" : "anchored";
                counters.TryGetValue(key, out int n); counters[key] = n + 1;
            }
        }
        static void ExportGpu(string path)
        {
            CheckGpuPrepareBudget();
            string output = Path.GetFullPath(path);
            if (!gpuCapture) throw new InvalidOperationException("export: gpudeco prepare 후 실제 효과 시작 관찰 필요");
            if (File.Exists(output))
            {
                GpuLog("GPUDECO_G1=ERROR stage=export reason=output_exists path=" + output);
                throw new IOException("export: 기존 정답 파일 보존; 생성기로 새 출력 폴더를 지정하세요: " + output);
            }
            var watch = Stopwatch.StartNew();
            if (!ready || table == null || Adopting || LiveDecoRenderer.Active) throw new InvalidOperationException("export: bake ready, adopt/livedeco off 필요");
            if (!ADOBase.controller || !ADOBase.controller.paused || Time.timeScale != 0) throw new InvalidOperationException("export: framecmp pause 필요");
            ValidateBaseline();
            var cam = GpuSplit.FindMain(); var camy = ADOBase.controller.camy;
            if (!cam || !cam.orthographic || !camy || !Dormancy.LiveTrackingReady) throw new InvalidOperationException("export: 직교 카메라/Dormancy 필요");
            if (!manager || manager.transform.position != Vector3.zero || manager.transform.rotation != Quaternion.identity || manager.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("export: 관리자 변환이 단위 변환이 아님");
            var data = new GpuDecoData
            {
                RawTime = scrConductor.instance.songposition_minusi, BakeTime = BakeTime, Pitch = table.Pitch,
                TweenScale = table.TweenScale, Frame = Time.frameCount, Awake = Dormancy.AwakeList.Count, Supported = table.Supported, Fingerprint = TableFingerprint(),
                PixelsPerWorld = Mathf.Max(cam.pixelHeight / (2 * cam.orthographicSize), cam.pixelWidth / (2 * cam.orthographicSize * cam.aspect))
            };
            var ct = camy.transform; data.Camera = new[] { ct.position.x, ct.position.y, ct.eulerAngles.z,
                camy.camobj.orthographicSize * .2f / (ADOBase.customLevel.levelData.camZoom / 100f) };
            var awakeSet = new HashSet<scrDecoration>(Dormancy.AwakeList); var items = new List<GpuDecoData.Item>();
            var reasons = new SortedDictionary<string, int>();
            var sources = new List<GpuDecoData.SourceClock>(); var sourceIndices = new Dictionary<long, int>();
            var anchorCounts = new Dictionary<string, int>();
            for (int index = 0; index < baselineObjects.Length; index++)
            {
                var d = baselineObjects[index] as scrVisualDecoration;
                if (!d || !awakeSet.Contains(d)) continue;
                string why = table.Decorations[index].Reasons.Count != 0 ? "bake-unsupported" : LiveDecoRenderer.ExportReason(d, cam, manager);
                if (why == null && d.parallax.clampToScreen) why = "clamp-screen";
                if (why == null && !GpuDecoCoverage.Auxiliary(data.Awake) && d.spriteRenderer != null)
                {
                    var bSize = d.spriteRenderer.bounds.size;
                    if (bSize.x > 25f || bSize.y > 25f) why = "oversized-world-quad";
                }
                var r = d.spriteRenderer; LiveDecoGeometry geometry = null;
                if (why == null)
                {
                    var s = r.sprite;
                    if (s.vertices.Length != 4 || s.uv.Length != 4 || s.triangles.Length != 6) why = "nonquad";
                    else geometry = LiveDecoGeometry.Capture(d.pivotTrans, d.childTransform, r.transform, s.vertices, r.flipX, r.flipY);
                    if (geometry == null) why = "child-chain";
                }
                if (why != null) { reasons.TryGetValue(why, out int n); reasons[why] = n + 1; continue; }
                var item = new GpuDecoData.Item
                {
                    Index = index, Id = d.GetInstanceID(), MaterialId = r.sharedMaterial.GetInstanceID(), TextureId = r.sprite.texture.GetInstanceID(),
                    Layer = r.sortingLayerID, Order = r.sortingOrder, Placement = d.placementType.ToString(),
                    Flags = (d.lockScale ? 1 : 0) | (d.lockRotation ? 2 : 0) | (d.parallax.dontAlterX ? 4 : 0) | (d.parallax.dontAlterY ? 8 : 0) | (r.flipX ? 16 : 0) | (r.flipY ? 32 : 0)
                };
                var ts = d.scaleMultiplier * d.textureScaleMultiplier;
                item.Constants = new[] { ts.x, ts.y, d.textureScaleMultiplier.x, d.textureScaleMultiplier.y, d.startRot, d.croppedPivotOffset.x, d.croppedPivotOffset.y, d.pivotTrans.position.z };
                if (ts.x == 0 || ts.y == 0 || d.textureScaleMultiplier.x == 0 || d.textureScaleMultiplier.y == 0) throw new InvalidOperationException("export: zero static scale");
                var c = d.color; var p = d.parallax.multiplier;
                item.Actual = new[] { d.pivotPosVec.x, d.pivotPosVec.y, d.pivotOffsetVec.x, d.pivotOffsetVec.y, d.rotAngle, d.scaleVec.x, d.scaleVec.y,
                    c.r, c.g, c.b, c.a, d.opacity, p.x, p.y, d.parallaxOffset.x, d.parallaxOffset.y };
                var color = r.color; item.Color = new[] { color.r, color.g, color.b, color.a };
                item.Initial = (float[])table.Decorations[index].Initial.Clone();
                for (int ch = 0; ch < 16; ch++) item.Tracks[ch] = table.Tracks[index * 16 + ch];
                var corners = r.sprite.vertices; var uv = r.sprite.uv;
                for (int v = 0; v < 4; v++)
                {
                    Put(item.Shape, v * 3, geometry.Shape[v]); item.Corners[v * 2] = corners[v].x; item.Corners[v * 2 + 1] = corners[v].y;
                    item.UV[v * 2] = uv[v].x; item.UV[v * 2 + 1] = uv[v].y;
                    var local = corners[v]; if (r.flipX) local.x = -local.x; if (r.flipY) local.y = -local.y;
                    Put(item.World, v * 3, r.transform.TransformPoint(local));
                }
                var origin = geometry.Offset(Vector2.zero); Put(item.OffsetBasis, 0, origin);
                Put(item.OffsetBasis, 3, geometry.Offset(Vector2.right) - origin); Put(item.OffsetBasis, 6, geometry.Offset(Vector2.up) - origin);
                item.Triangles = r.sprite.triangles.Select(x => (int)x).ToArray();
                ExportGpuAnchors(d, item, sources, sourceIndices, anchorCounts);
                int missing = data.MotionWithoutClock(item);
                if (missing >= 0) { string reason = "source-clock-missing-slot-" + missing; reasons.TryGetValue(reason, out int count); reasons[reason] = count + 1; continue; }
                items.Add(item);
            }
            data.Items = items.ToArray();
            data.Sources = sources.ToArray();
            data.FrameSamples = gpuFrameCount; data.MeanFrameSeconds = gpuFrameCount == 0 ? 0 : (float)(gpuFrameSum / gpuFrameCount);
            data.MeanProgressStep = gpuFrameCount == 0 ? 0 : (float)(gpuProgressSum / gpuFrameCount);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None)) data.Write(stream);
            gpuLastSnapshot = data; gpuSnapshotGeneration = Generation; gpuLastG0 = false;
            CheckGpuPrepareBudget();
            if (gpuExports == 0 && gpuPrepareWatch != null)
            {
                gpuPrepareWatch.Stop();
                GpuLog("prepare_ms=" + gpuPrepareWatch.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture)
                    + " phase=first_export includes=open,bake,seek,export bake_wall_ms=" + workerWallMs.ToString("F6", CultureInfo.InvariantCulture));
            }
            GpuLog("prepare_ms=" + watch.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture) + " phase=export owned=" + items.Count + " awake=" + data.Awake
                + " raw=" + data.RawTime.ToString("R", CultureInfo.InvariantCulture) + " tb=" + data.BakeTime.ToString("R", CultureInfo.InvariantCulture) + " path=" + output);
            GpuLog("export_reject " + string.Join(" ", reasons.Select(x => x.Key + "=" + x.Value)));
            GpuLog("export_schema=2 sources=" + sources.Count + " " + string.Join(" ", anchorCounts.Select(x => x.Key + "=" + x.Value))
                + " anchor_input=actual_Rec_Pos_and_start_parameters fallback=unobserved,unstarted,native_or_direct start_phase=recorded_not_inferred");
            GpuLog("coverage=" + ((double)items.Count / Math.Max(1, data.Awake)).ToString("F6", CultureInfo.InvariantCulture)
                + " auxiliary=" + GpuDecoCoverage.Auxiliary(data.Awake));
            gpuExports++;
            if (GpuDecoCoverage.OwnedLow(data.Awake, items.Count))
            {
                gpuExportOwnedLow = true; GpuLog("GPUDECO_G1=OWNED_LOW stage=export owned=" + items.Count + " awake=" + data.Awake + " required_coverage=0.7");
                throw new InvalidOperationException("GPUDECO_G1=OWNED_LOW; 이후 시험 중단");
            }
        }
#endif
    }
}
#endif
