#if DEV || AUTOTEST || DRAWCOLLAPSE_CHECK
using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Pure painter-order and memory arithmetic shared by preparation and checks.
    internal static class DrawCollapsePolicy
    {
        // Unity's loaded shader caps texture parameters at 64 on this D3D11
        // device, even though the native API accepts 128 SRVs.
        internal const int ArrayBindings=4;
        internal const int DirectBindings=48;
        internal const int TextureBindings=ArrayBindings+DirectBindings;
        internal const int OwnedChecksPerFrame=4;
        // Orthographic camera translation changes every distance by this same
        // amount. Compare against the camera, not against the first barrier:
        // otherwise a lone moving barrier could cross a whole span undetected.
        internal static bool SameSortKey(SceneRenderOrder.Key prepared,SceneRenderOrder.Key current,float cameraShift)
            =>prepared.Layer==current.Layer && prepared.Order==current.Order && prepared.Queue==current.Queue
                && prepared.Material==current.Material && prepared.Slot==current.Slot
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
        internal static bool SplitByOrder=true;
        // 구간 디스패치를 미리 기록한 명령 버퍼 한 번 실행으로 한다(끄면 구간마다 즉시 디스패치).
        internal static bool RecordedDispatch=true;
        // 진단 전용: 대표 렌더러를 숨겨 그리기의 GPU 몫을 잰다(화면이 틀려진다).
        internal static bool HideProxies=false;
        // 화면 밖에 있는 쿼드는 컴퓨트에서 면적 0 으로 만든다(픽셀 변화 없음).
        // 모든 구간의 정점을 큰 버퍼 하나에 컴퓨트 한 번으로 쓰고, 대표 렌더러는 정점 셰이더에서 그 버퍼를 읽는다(구간마다 디스패치 80번 제거).
        internal static bool GlobalVerts=true;
        internal static bool ClipOffscreen=true;
        // 재생 중 프레임마다 하던 '장벽 ↔ 대표 렌더러' 순서 점검을 건너뛴다(진입 전수 점검은 유지, 안전장치는 로그 전용 방침).
        internal static bool SkipOrderSweep=true;
        // 진단 전용: 일시정지 프레임에서 컴퓨트 디스패치를 건너뛰어 컴퓨트의 GPU 몫을 잰다(첫 프레임 정점이 남아 화면은 같다).
        internal static bool SkipPausedCompute=false;
        // 큰 이미지 장식의 쿼드를 불투명한 부분에만 맞게 줄인다(투명 여백 래스터라이즈 제거). 텍스처 긴 변이 TightMinSide 이상일 때만.
        internal static bool TightLargeQuads=true;
        internal static int TightMinSide=1025,TightMarginTexels=4;
        internal static float TightMaxAreaRatio=.8f;
        // 2048 을 넘는 같은 크기 DXT5(4K 프레임 계열 등)를 정확한 크기의 배열 하나로 묶는다. 3개 미만이면 직접 슬롯을 쓴다.
        internal static bool ExactBigArray=true;
        internal const int ExactBigMin=3;
        // 프로토타입: 정렬 키 캐시(카메라 이동분 일괄 보정)와 전수 스캔 생략. 끄면 이전 방식으로 전부 다시 읽는다.
        internal static bool KeyCache=true;
        internal static bool FullScanOnReplan=false;
        // 같은 정렬 묶음에서 장벽보다 앞에 그려지던 장식을 구간에 남기는 규칙. 켜면 260초 픽셀 42만 개가 달라져 기본 끔.
        internal static bool KeepBeforeBarrier=true;
        // 장벽과 같은 정렬 묶음에서 시작하는 구간의 대표 렌더러 거리를 TieNudge 만큼 더 멀게 해 동점을 없앤다.
        internal static bool NudgeTieSpans=true;
        // 같은 묶음에서 모든 활성 장벽보다 뒤에 그려지던 장식도 구간에 유지한다(대표 거리를 -미세 이동).
        internal static bool KeepAfterBarrier=true; // 262초 픽셀 최대 차이 4(0.4% 픽셀). GPU 병목에서는 폴백 14개의 전체 재평가를 없애 FPS +
        internal const float TieNudge=2e-4f;
        internal static int MaxSpans=700;
        internal static List<int[]> Plan(List<Row> rows)
        {
            rows.Sort((a,b)=>a.Key.CompareTo(b.Key));
            for(int i=1;i<rows.Count;i++)
                if(rows[i].Key.SameMaterialTie(rows[i-1].Key) && (rows[i].Owned || rows[i-1].Owned))
                    throw new InvalidOperationException("drawcollapse ambiguous material tie");
            var spans=new List<int[]>();var items=new List<int>();
            bool haveBucket=false;int bl=0,bo=0,bq=0;
            foreach(var row in rows)
            {
                if(row.Owned)
                {
                    // 정렬 순서(층,순서,큐)가 바뀔 때마다 구간을 끊는다: 다른 순서의 장벽이 곡 도중 활성화돼도 구간 한가운데로 들어올 수 없다.
                    if(SplitByOrder && haveBucket && items.Count>0 && (row.Key.Layer!=bl || row.Key.Order!=bo || row.Key.Queue!=bq)){spans.Add(items.ToArray());items.Clear();}
                    bl=row.Key.Layer;bo=row.Key.Order;bq=row.Key.Queue;haveBucket=true;
                    items.Add(row.Renderer);continue;
                }
                if(items.Count>0){spans.Add(items.ToArray());items.Clear();}
                haveBucket=false;
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
        List<string> failures;
        internal DrawCollapseGuard(bool collect){this.collect=collect;}
        // 측정 전용(연구 빌드): 대표/장벽 순서 위반을 기록만 하고 끄지 않는다. 화면이 원본과 다를 수 있다.
        internal static bool IgnoreOrderViolations;
        internal static long IgnoredOrderViolations;
        internal void Check(bool valid,string reason)
        {
            if(valid)return;
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
}
#endif
