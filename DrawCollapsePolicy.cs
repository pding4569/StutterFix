#if DEV || AUTOTEST || GPUDECO || DRAWCOLLAPSE_CHECK
using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Pure painter-order and memory arithmetic shared by preparation and checks.
    internal static class DrawCollapsePolicy
    {
        internal static bool AdoptNativeTweens=true;
        // 마스크를 쓰는 장식만 원래대로 두고(구간을 끊는 장벽) 나머지를 맡는다. 스프라이트 마스크가 있다고 맵 전체를 거부하지 않는다.
        internal const bool PartialMaskAdmit=true;
        // Unity's loaded shader caps texture parameters at 64 on this D3D11
        // device, even though the native API accepts 128 SRVs.
        internal const int ArrayBindings=4;
        internal const int DirectBindings=48;
        internal const int FewDirectBindings=8; // DIRECT_FEW: 구간이 쓰는 직접 텍스처가 이 수 이하면 작은 변형
        internal static bool FewDirect=true; // 측정: fewdirect on|off
        internal static bool GlobalDirect=false; // 측정: globaldirect on|off (직접 텍스처를 전역으로)
        // 화면 밖까지 뻗은 초대형 사각형(월드 25 넘음)도 맡는다. 예전 거부 이유는 대각선 경계의 래스터 반올림 차이(최대 5/255, 눈으로 같음)였다. 측정: oversized on|off
        internal static bool AllowOversized=true;
        // 반복(tile) 장식을 맡는다(묶음 그리기만). 측정: repeat on|off
        internal static bool Repeat=false;   // 7777 사슬 등에서 그림이 원래와 달라 기본 끔(UV·랩 처리로는 해결 안 됨, 원인 조사 중)
        // 혼합 모드 장식(Blend Modes 에셋, UnifiedGrab)을 맡는다. 측정: blendmodes on|off
        // 곡 중 깊이(정렬 층·순서)가 바뀌는 장식: 쓰는 깊이마다 그리기 자리를 계획하고 지금 깊이의 자리만 켠다. 측정: depthinst on|off
        internal static bool DepthInstances=false;
        internal static int MaxDepthInstances=8;   // 측정: depthinst <수>
        internal static bool BlendModes=false;
        // 화면 복사(GrabPass)가 필요한 혼합 모드도 맡는다. 끄면 그 장식은 원래 렌더러로 남아 화면 밖일 때 유니티가 걸러낸다(구간 렌더러는 걸러지지 않아 매 프레임 전체 화면 복사). 측정: blendgrab on|off
        internal static bool BlendGrab=true;   // 진행 중: DD 에서 활성화 검사(이미지 비어 있음 등)를 아직 통과 못 함
        // 계획 직전에 채운다: 화면 복사(GrabPass)를 쓰는 소유 항목. 일반 항목 다음에 오면 구간을 끊어 복사 시점을 원래와 같게 한다.
        internal static bool[] GrabItems;
        // 화면 고정(Camera·CameraAspect 배치, clampToScreen) 장식을 맡는다. 측정: screenfixed on|off
        internal static bool ScreenFixed { get => DecoBakeCore.ScreenFixedSupported; set => DecoBakeCore.ScreenFixedSupported = value; }
        internal const int TextureBindings=ArrayBindings+DirectBindings;
        internal const int OwnedChecksPerFrame=4;
        // Orthographic camera translation changes every distance by this same
        // amount. Compare against the camera, not against the first barrier:
        // otherwise a lone moving barrier could cross a whole span undetected.
        internal static bool SameSortKey(SceneRenderOrder.Key prepared,SceneRenderOrder.Key current,float cameraShift)
            =>prepared.Layer==current.Layer && prepared.Order==current.Order && prepared.Queue==current.Queue
                && prepared.Material==current.Material && prepared.Shader==current.Shader && prepared.Slot==current.Slot
                && Math.Abs(prepared.Distance-current.Distance-cameraShift)<=1e-3f;
        internal static int[] AdmitArrayGroups(int[] counts,int maxSlices)
        {
            if(maxSlices<=0)throw new ArgumentOutOfRangeException(nameof(maxSlices));
            var admitted=new int[counts.Length];int slots=ArrayBindings;
            for(int i=0;i<counts.Length;i++)
            {
                if(counts[i]<0)throw new ArgumentOutOfRangeException(nameof(counts));
                admitted[i]=(int)Math.Min(counts[i],(long)slots*maxSlices);
                slots-=(int)((admitted[i]+(long)maxSlices-1)/maxSlices);
            }
            return admitted;
        }
        internal readonly struct Row
        {
            internal readonly SceneRenderOrder.Key Key;
            internal readonly bool Owned;
            internal readonly int Renderer, Texture, GameLayer;
            internal readonly string Signature;
            internal Row(SceneRenderOrder.Key key, bool owned, int renderer, int texture = 0,
                string signature = "", int gameLayer = 0)
            { Key=key;Owned=owned;Renderer=renderer;Texture=texture;Signature=signature;GameLayer=gameLayer; }
        }
        internal sealed class Counts
        {
            internal int OwnedVisible, BarrierVisible, Spans, SameTextureRuns, AmbiguousTies;
        }
        internal static Counts Count(List<Row> rows)
        {
            rows.Sort((a,b)=>a.Key.CompareTo(b.Key));
            var result=new Counts();var barriers=new HashSet<int>();
            for(int i=1;i<rows.Count;i++)
                if(rows[i].Key.SameMaterialTie(rows[i-1].Key) && (rows[i].Owned || rows[i-1].Owned))result.AmbiguousTies++;
            int first=rows.FindIndex(r=>r.Owned),last=rows.FindLastIndex(r=>r.Owned);
            bool inSpan=false;Row previous=default;
            for(int i=first;i>=0 && i<=last;i++)
            {
                var row=rows[i];
                if(!row.Owned){barriers.Add(row.Renderer);inSpan=false;continue;}
                result.OwnedVisible++;
                if(!inSpan)result.Spans++;
                if(!inSpan || row.Texture!=previous.Texture || row.Signature!=previous.Signature
                    || row.GameLayer!=previous.GameLayer || !row.Key.SameBucket(previous.Key))result.SameTextureRuns++;
                previous=row;inSpan=true;
            }
            result.BarrierVisible=barriers.Count;
            return result;
        }
        // Renderer stores the source item index for owned rows. Never regroup
        // A/B/A textures or move a quad across even a currently hidden barrier.
        // 정렬 순서가 바뀔 때마다 구간을 끊을지. 계획에는 지금 꺼진 장벽도 들어가므로(나중에 켜져도 그 자리는 이미 끊김) 끄면 장벽 없는 순서 사이를 합친다. 측정: gpudeco drawcollapse policy splitorder on|off
        internal static bool SplitByOrder=true;
        // 구간 디스패치를 미리 기록한 명령 버퍼 한 번 실행으로 한다(끄면 구간마다 즉시 디스패치).
        internal const bool RecordedDispatch=true;
        // 제품 경로: 버퍼는 한 번만 묶고 프레임마다 값 몇 개+디스패치만(명령 버퍼 실행 대체). 끄면 기록된 명령 버퍼.
        internal static bool MinimalDispatch=true;
        // 알파가 상수 0 으로 고정된 항목만 든 구간은 대표 렌더러를 꺼서 그리기·컬링 비용을 없앤다(알파 0 은 어차피 안 그려지므로 화면 동일).
        internal static bool CullIdleSpans=true;
        // 진단 전용: 대표 렌더러를 숨겨 그리기의 GPU 몫을 잰다(화면이 틀려진다).
        internal static bool HideProxies=false;
        // 진단 전용: 화면 밖 걸러내기 범위를 이 배율로 줄인다(0.0001 이면 쿼드를 전부 접어 채우기 비용만 뺀다).
        internal static float ClipScale=1f;
        // 화면 밖에 있는 쿼드는 컴퓨트에서 면적 0 으로 만든다(픽셀 변화 없음).
        // 모든 구간의 정점을 큰 버퍼 하나에 컴퓨트 한 번으로 쓰고, 대표 렌더러는 정점 셰이더에서 그 버퍼를 읽는다(구간마다 디스패치 80번 제거).
        internal static bool GlobalVerts=true;
        // 컴퓨트를 명령 버퍼 대신 즉시 디스패치로 실행한다(프레임당 ExecuteCommandBuffer 두 번 제거).
        internal static bool DirectDispatch=false; // 측정: 기록 버퍼와 차이 없음
        // GPU 소유 장식의 원본 렌더러를 forceRenderingOff 뿐 아니라 enabled=false 로 씬 렌더러 목록에서도 뺀다(컬링·변환 처리 CPU -0.45ms).
        // 소유 장식의 원본 렌더러를 enabled=false 로 씬 렌더러 목록에서 뺀다(3개 카메라 컬링이 2.8만 개를 훑지 않음). 해제 때 ReleaseOwnedEnabled 가 되돌린다.
        // 예전에 "실제 재생에서 느려져" 껐던 것은 CheckPlayerFrame 이 enabled 를 요구해 GPU 장식이 꺼지던 오류였다(2026-10-26 수정).
        // Arche 에디터 같은 구간 번갈아 측정: 가벼움 +9.7%, 중간 +8.4%, 무거움 +5.6%, 180초 화면 일치.
        internal static bool DisableOwnedRenderers=true;
        internal static bool ClipOffscreen=true;
        // 재생 중 프레임마다 하던 '장벽 ↔ 대표 렌더러' 순서 점검을 건너뛴다(진입 전수 점검은 유지, 안전장치는 로그 전용 방침).
        internal static bool SkipOrderSweep=true;
        // 진단 전용: 일시정지 프레임에서 컴퓨트 디스패치를 건너뛰어 컴퓨트의 GPU 몫을 잰다(첫 프레임 정점이 남아 화면은 같다).
        internal static bool SkipPausedCompute=false;
        internal static bool SkipComputeForTest=false; // 측정: gpudeco drawcollapse policy nocompute on
        // 곡 시작 키를 누르기 전(Start 상태, 정지된 화면)에 GPU 장식을 켠다: 켜는 약 150ms 가 카운트다운이 아니라 멈춰 있는 화면에서 일어난다.
        internal static bool PreStartActivate=true;
        // 죽음·완주 순간(FailAction/Fail2Action/OnLandOnPortal)에는 GPU 장식을 되돌리지 않고 다음 전환 때 되돌린다.
        internal static bool DeferEndOnResult=true;
        // 에디터 중간 시작: 따라잡는 60프레임을 기다리지 않고 바로 켠다(이어받기 필요).
        internal static bool ActivateInCatchUp=true; // 에디터 중간 시작: 되감기 몇 프레임 직후 바로 켜서 켜는 시간(약 330ms)을 로딩 멈춤 안에 둔다. 2026-10-26: 이어받기 194~236개 성공, FPS 325 대 323(켜기 대기와 같음), 같은 판 A/C 8초과 0.592%. 되감기 구분 없이 켜면(InCatchUp 전체) HookFrame 거절+복귀로 FPS 198
        // 곡 중간 시작이어도 CPU 처리 장식·동적 제외가 없으면 준비 때 만든 구간 계획을 먼저 검증해 보고, 맞으면 다시 계획하지 않는다(계획 재구성 약 220ms 절약). 어긋나면 예전처럼 다시 계획한다.
        internal static bool MidReusePlan=true;
        // 큰 이미지 장식의 쿼드를 불투명한 부분에만 맞게 줄인다(투명 여백 래스터라이즈 제거). 텍스처 긴 변이 TightMinSide 이상일 때만.
        internal static bool TightLargeQuads=true;
        internal static int TightMinSide=1025;
        // 투명 여백 깎기(쿼드를 알파>0 범위로 줄임)를 하는 텍스처의 최소 긴 변. 측정: gpudeco drawcollapse policy trimmin N
        internal static int TrimMinSide=512;
        // 형태 칸(GpuDecoShape): 투명 여백이 많은 텍스처를 칸 사각형 몇 개로만 그린다. 측정: gpudeco drawcollapse policy shape on|off, shapegrid N
        internal static bool ShapeCells=false;
        // RunLod 를 켠 판에서 RunLod 후보(일반 장식)는 RunLod 에 두고 GPU 장식은 나머지(혼합 등)만 맡는다. 측정: gpudeco drawcollapse policy runlodsplit on|off
        internal static bool RunLodSplit=false;
        // 이번 맵에서 나눠 맡는지(준비 때 정함). 장식이 많거나 이벤트 대상이 많은 맵(CPU 병목, Arche 28,835개·22.8만)은 GPU 장식이 전부 맡는다.
        // 측정(RunLod 켬, 2560): Arche 분담 102.9 대 전부 245.0, Windflower 210.4 대 196.1, HELLO 232.3 대 196.3(원래 103.0/211.7/233.2)
        internal static bool SplitNow=false;
        internal const int SplitAllDecos=12000, SplitAllTargets=150000;
        internal static int ShapeGrid=16, ShapeMinSide=64, ShapeMaxRects=48;
        internal static float ShapeMaxKeep=.6f;
        internal static string ShapeOnly=null;   // 측정 전용: 이름에 이 글자가 든 텍스처에만 형태 칸
        internal const int TightMarginTexels=4;
        internal const float TightMaxAreaRatio=.8f;
        // 2048 을 넘는 같은 크기 DXT5(4K 프레임 계열 등)를 정확한 크기의 배열 하나로 묶는다. 3개 미만이면 직접 슬롯을 쓴다.
        internal const bool ExactBigArray=true;
        internal const int ExactBigMin=3;
        // 프로토타입: 정렬 키 캐시(카메라 이동분 일괄 보정)와 전수 스캔 생략. 끄면 이전 방식으로 전부 다시 읽는다.
        internal const bool KeyCache=true;
        internal const bool FullScanOnReplan=false;
        // 같은 정렬 묶음에서 장벽보다 앞에 그려지던 장식을 구간에 남기는 규칙. 켜면 260초 픽셀 42만 개가 달라져 기본 끔.
        internal const bool KeepBeforeBarrier=true;
        // 장벽과 같은 정렬 묶음에서 시작하는 구간의 대표 렌더러 거리를 TieNudge 만큼 더 멀게 해 동점을 없앤다.
        internal static bool NudgeTieSpans=true;   // 측정: gpudeco drawcollapse policy nudge on|off
        // 같은 묶음에서 모든 활성 장벽보다 뒤에 그려지던 장식도 구간에 유지한다(대표 거리를 -미세 이동).
        internal static bool KeepAfterBarrier=true; // 262초 픽셀 최대 차이 4(0.4% 픽셀). GPU 병목에서는 폴백 14개의 전체 재평가를 없애 FPS +
        internal const float TieNudge=2e-4f;
        internal const int MaxSpans=3000; // 혼합 모드 장식은 하나씩 구간(원래도 장식마다 화면 복사 + 그리기)이라 DD 는 1276
        internal static List<int[]> Plan(List<Row> rows)
        {
            rows.Sort((a,b)=>a.Key.CompareTo(b.Key));
            for(int i=1;i<rows.Count;i++)
                if(rows[i].Key.SameMaterialTie(rows[i-1].Key) && (rows[i].Owned || rows[i-1].Owned))
                    throw new InvalidOperationException("drawcollapse ambiguous material tie layer="+rows[i].Key.Layer+" order="+rows[i].Key.Order+" queue="+rows[i].Key.Queue+" dist="+rows[i].Key.Distance.ToString("R")+" mat="+rows[i].Key.Material+" owned="+rows[i].Owned+"/"+rows[i-1].Owned+" dist0="+rows[i-1].Key.Distance.ToString("R"));
            var spans=new List<int[]>();var items=new List<int>();
            bool haveBucket=false;int bl=0,bo=0,bq=0;bool lastGrab=false;
            foreach(var row in rows)
            {
                if(row.Owned)
                {
                    // 정렬 순서(층,순서,큐)가 바뀔 때마다 구간을 끊는다: 다른 순서의 장벽이 곡 도중 활성화돼도 구간 한가운데로 들어올 수 없다.
                    if(SplitByOrder && haveBucket && items.Count>0 && (row.Key.Layer!=bl || row.Key.Order!=bo || row.Key.Queue!=bq)){spans.Add(items.ToArray());items.Clear();}
                    bool grab=GrabItems!=null && (uint)row.Renderer<(uint)GrabItems.Length && GrabItems[row.Renderer];
                    // 혼합 항목은 그리기마다 화면을 복사하므로(원래 Grab 셰이더와 같게) 구간 하나에 하나씩만 둔다
                    if((grab || lastGrab) && items.Count>0){spans.Add(items.ToArray());items.Clear();}
                    lastGrab=grab;
                    bl=row.Key.Layer;bo=row.Key.Order;bq=row.Key.Queue;haveBucket=true;
                    items.Add(row.Renderer);continue;
                }
                if(items.Count>0){spans.Add(items.ToArray());items.Clear();}
                haveBucket=false;lastGrab=false;
            }
            if(items.Count>0)spans.Add(items.ToArray());
            return spans;
        }
        // Tight dimensions are the resolution tiers: no 2048-square padding,
        // decompression, resampling, CPU readability or guessed compression ratio.
        internal static long TextureBytes(int width,int height,int mips,int blockWidth,int blockHeight,int blockBytes)
        {
            if(width<=0 || height<=0 || mips<=0 || blockWidth<=0 || blockHeight<=0 || blockBytes<=0)
                throw new ArgumentOutOfRangeException();
            long total=0;
            checked
            {
                for(int mip=0;mip<mips;mip++)
                {
                    total+=((width+(long)blockWidth-1)/blockWidth)*((height+(long)blockHeight-1)/blockHeight)*blockBytes;
                    if(width==1 && height==1 && mip+1<mips)throw new ArgumentException("excess mip count");
                    width=Math.Max(1,width/2);height=Math.Max(1,height/2);
                }
            }
            return total;
        }
        internal static int SmallDxtTier(int width,int height,bool dxt5,int mips)
        {
            if(width<=0 || height<=0)throw new ArgumentOutOfRangeException();
            if(!dxt5 || mips!=1 || width%4!=0 || height%4!=0)return 0;
            foreach(int size in new[]{512,1024,2048})if(width<=size && height<=size)return size;
            return 0; // Borrow the original 2D texture; never decompress or resample it.
        }
        internal static string SpanDecision(int spans)
        {
            if(spans<0)throw new ArgumentOutOfRangeException(nameof(spans));
            return spans<=150?"eligible":spans>400?"too_many_barriers":"hold_151_to_400";
        }
    }
    // Lazily allocate only on failure. AUTOTEST entry checks collect every
    // independent failure; playback checks fail fast into independent rendering.
    internal sealed class DrawCollapseGuard
    {
        readonly bool collect;
        readonly Action<string> orderLog;
        List<string> failures;
        internal DrawCollapseGuard(bool collect,Action<string> orderLog=null){this.collect=collect;this.orderLog=orderLog;}
        // 측정 전용(연구 빌드): 대표/장벽 순서 위반을 기록만 하고 끄지 않는다. 화면이 원본과 다를 수 있다.
        internal static bool IgnoreOrderViolations=false;
        internal static long IgnoredOrderViolations;
        internal void Check(bool valid,string reason)
        {
            if(valid)return;
            if(orderLog!=null && (reason.StartsWith("drawcollapse proxy/barrier order changed",StringComparison.Ordinal)
                || reason.Contains(" sort key changed"))) {orderLog(reason);return;}
            if(IgnoreOrderViolations && reason.StartsWith("drawcollapse proxy/barrier order changed")){IgnoredOrderViolations++;return;}
            if(!collect)throw new InvalidOperationException(reason);
            if(failures==null)failures=new List<string>();
            failures.Add(reason.Replace('\r',' ').Replace('\n',' '));
        }
        internal void Finish(Action<string> log)
        {
            if(failures==null)return;
            string all=string.Join(" | ",failures);
            log("DRAWCOLLAPSE_GUARD_FAILS="+all);
            throw new InvalidOperationException(all);
        }
    }
#if DEV || AUTOTEST || GPUDECO_FIXTURE || DRAWCOLLAPSE_CHECK
    internal sealed class DrawCollapseGatePolicy
    {
        internal readonly bool[] Windows=new bool[4],Pixels=new bool[3],Restores=new bool[4];
        internal readonly double[] Fps=new double[4];
        internal bool Failed;
        internal void Window(int i,double fps,int draws,int spikes,bool valid)
        {
            if(i<0 || i>3 || Windows[i] || double.IsNaN(fps) || double.IsInfinity(fps) || fps<=0
                || draws<=0 || spikes<0 || !valid){Failed=true;return;}
            Windows[i]=true;Fps[i]=fps;
            if((i==1 || i==2) && (fps<220 || draws>300 || spikes>0))Failed=true;
        }
        internal void Pixel(int i,int maximum,bool stable)
        {
            if(i<0 || i>2 || Pixels[i] || maximum<0 || maximum>255){Failed=true;return;}
            Pixels[i]=true;if(maximum>3 || !stable)Failed=true;
        }
        internal void Restore(int i,bool valid)
        {
            if(i<0 || i>3 || Restores[i]){Failed=true;return;}
            Restores[i]=true;if(!valid)Failed=true;
        }
        internal bool Complete=>Array.TrueForAll(Windows,x=>x) && Array.TrueForAll(Pixels,x=>x) && Array.TrueForAll(Restores,x=>x);
        internal bool Passed=>Complete && !Failed;
    }
#endif
}
#endif
