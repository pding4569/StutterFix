#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (개발자용 자동 시험 decocensus begin [간격=12] / end) "장식을 한 덩어리로 다룰 수 있나"의 타당성 조사. 읽기만 하고 아무것도 바꾸지 않는다.
    // 렌더된 프레임 간격마다 연속 두 프레임의 보이는 이미지 장식 전체를 찍어, 두 프레임 사이에 값이 바뀐 장식(움직이는 장식)에 대해
    //  - 서로 다른 이미지/텍스처/재질이 몇 종류인지, 그리기 순서(sortingLayer, sortingOrder)대로 줄 세웠을 때 같은 텍스처·재질이 이어지는 구간(=한 번에 그릴 수 있는 최대 묶음 수)이 몇 개인지
    //  - 같은 움직임(자기 좌표계의 위치·회전·크기 변화량이 같은)을 하는 장식 그룹이 몇 개이고 그룹 크기 분포가 어떤지(시차·카메라와 무관한 장식 자신의 값)
    //  - 같은 이미지이면서 같은 움직임인 그룹, 같은 자리 겹침 중복
    // 을 센다. 곡 중에만 쓰고 성능 판에서는 끈다(표본 프레임마다 2.9만 개 훑음).
    internal static class DecoCensus
    {
        private struct Snap { internal bool Vis; internal float Px, Py, Rot, Sx, Sy, R, G, B, A; internal int Sprite, Tex, Mat, Var, Layer, Order; internal float Ext; }
        private static readonly AccessTools.FieldRef<scrDecoration, Vector2> ppRef = AccessTools.FieldRefAccess<scrDecoration, Vector2>("pivotPosVec");
        private static readonly AccessTools.FieldRef<scrDecoration, float> rotRef = AccessTools.FieldRefAccess<scrDecoration, float>("rotAngle");
        private static readonly AccessTools.FieldRef<scrDecoration, Vector2> scRef = AccessTools.FieldRefAccess<scrDecoration, Vector2>("scaleVec");
        private static readonly AccessTools.FieldRef<scrDecoration, Color> colRef = AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        private static Snap[] a = new Snap[0], b = new Snap[0];
        private static bool active, haveA;
        private static int gap = 12, frame = -1, lastTaken = -100, pairs;
        private static string aborted;
        private static double sumVis, sumMoved, sumTex, sumMat, sumSprite, sumOrder, sumRunsAll, sumRunsMoved, sumMotionGroups, sumMotionSpriteGroups, sumDup;
        private static readonly double[] hist = new double[5], histSp = new double[5];
        private static int minMoved = int.MaxValue, maxMoved;
        private static float firstSong, lastSong;
        private static readonly Dictionary<int, string> matKey = new Dictionary<int, string>();
        private static readonly Dictionary<string, int> varIds = new Dictionary<string, int>();
        private static double sumVarN, sumRunsAllTexVar, sumRunsMovedTexVar, sumDrawGroups, sumPure, sumPosRot, sumPosScale, sumPosRotScale, sumOther, sumColor, sumPureGroups;
        private static readonly double[] histPure = new double[5];
        private static string topVars = "";
        private static readonly double[] pxBins = new double[6];   // 움직인 장식의 (위치+회전+크기) 화면 변화(픽셀) <0.01 <0.03 <0.1 <0.3 <1 그 이상
        private static double sumColorQEq, sumColorDif, sumAllSmall03, sumAllSmall10, sumMovedForPx, sumColorQEqSmall, sumColorStep1, sumPxTotal;
        private static float ppw;

        internal static string Begin(string arg)
        {
            Stop(); int g; if (int.TryParse((arg ?? "").Trim(), out g) && g >= 2) gap = g; else gap = 12;
            pairs = 0; haveA = false; lastTaken = -100; aborted = null; minMoved = int.MaxValue; maxMoved = 0;
            sumVis = sumMoved = sumTex = sumMat = sumSprite = sumOrder = sumRunsAll = sumRunsMoved = sumMotionGroups = sumMotionSpriteGroups = sumDup = 0;
            Array.Clear(hist, 0, 5); Array.Clear(histSp, 0, 5); Array.Clear(histPure, 0, 5); sumVarN = sumRunsAllTexVar = sumRunsMovedTexVar = sumDrawGroups = sumPure = sumPosRot = sumPosScale = sumPosRotScale = sumOther = sumColor = sumPureGroups = 0; matKey.Clear(); varIds.Clear(); Array.Clear(pxBins, 0, 6); sumColorQEq = sumColorDif = sumAllSmall03 = sumAllSmall10 = sumMovedForPx = sumColorQEqSmall = sumColorStep1 = sumPxTotal = 0;
            firstSong = Song(); lastSong = firstSong; active = true; Camera.onPreRender += Capture;
            return "시작 (간격 " + gap + "프레임, 곡 " + firstSong.ToString("F1") + "초)";
        }
        private static float Song() { return scrConductor.instance == null ? float.NaN : (float)scrConductor.instance.songposition_minusi; }
        internal static void Stop() { active = false; Camera.onPreRender -= Capture; }
        private static void Capture(Camera c)
        {
            if (!active || c == null || scrCamera.instance == null || c != scrCamera.instance.camobj) return;
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            try
            {
                if (!haveA) { if (frame - lastTaken < gap) return; a = Fit(a); Take(a); haveA = true; lastTaken = frame; return; }
                if (frame - lastTaken != 1) { haveA = false; return; }   // 연속 프레임이 아니면 버림
                b = Fit(b); Take(b); haveA = false; Analyze(); lastSong = Song();
            }
            catch (Exception e) { aborted = e.GetType().Name + ": " + e.Message; Stop(); }
        }
        private static Snap[] Fit(Snap[] s) { int n = scrDecorationManager.instance.allDecorations.Count; return s.Length >= n ? s : new Snap[n + 1024]; }
        private static void Take(Snap[] s)
        {
            var list = scrDecorationManager.instance.allDecorations;
            for (int i = 0; i < list.Count; i++)
            {
                var v = list[i] as scrVisualDecoration; s[i].Vis = false;
                if (v == null) continue;
                var sr = v.spriteRenderer; if (sr == null || !sr.enabled || sr.sprite == null) continue;
                var col = colRef(v); if (col.a <= 0f) continue;
                var pp = ppRef(v); var sc = scRef(v); var mat = sr.sharedMaterial; var tex = sr.sprite.texture;
                s[i].Vis = true; s[i].Px = pp.x; s[i].Py = pp.y; s[i].Rot = rotRef(v); s[i].Sx = sc.x; s[i].Sy = sc.y; s[i].R = col.r; s[i].G = col.g; s[i].B = col.b; s[i].A = col.a;
                int mid = mat != null ? mat.GetInstanceID() : 0; string mk;
                if (!matKey.TryGetValue(mid, out mk)) { mk = mat == null ? "null" : mat.shader.name + "|" + string.Join(",", mat.shaderKeywords) + "|q" + mat.renderQueue; matKey[mid] = mk; }
                string vk = mk + "|m" + (int)sr.maskInteraction + (sr.flipX ? "|fx" : "") + (sr.flipY ? "|fy" : ""); int vid; if (!varIds.TryGetValue(vk, out vid)) { vid = varIds.Count + 1; varIds[vk] = vid; }
                s[i].Var = vid; s[i].Sprite = sr.sprite.GetInstanceID(); s[i].Tex = tex != null ? tex.GetInstanceID() : 0; s[i].Mat = mat != null ? mat.GetInstanceID() : 0;
                var ex = sr.sprite.bounds.extents; s[i].Ext = Math.Max(ex.x * Math.Abs(sc.x), ex.y * Math.Abs(sc.y));
                s[i].Layer = SortingLayer.GetLayerValueFromID(sr.sortingLayerID); s[i].Order = sr.sortingOrder;
            }
        }
        private static long Q(float d, float k) { return (long)Math.Round(d * k); }
        private static int Bin(int n) { return n == 1 ? 0 : n == 2 ? 1 : n <= 5 ? 2 : n <= 20 ? 3 : 4; }
        private static int RunsTV(List<int> l, Snap[] s)
        {
            int r = 0;
            for (int k = 0; k < l.Count; k++) if (k == 0 || s[l[k]].Tex != s[l[k - 1]].Tex || s[l[k]].Var != s[l[k - 1]].Var || s[l[k]].Layer != s[l[k - 1]].Layer || s[l[k]].Order != s[l[k - 1]].Order) r++;
            return r;
        }
        private static int Runs(List<int> l, Snap[] s)
        {
            int r = 0;
            for (int k = 0; k < l.Count; k++) if (k == 0 || s[l[k]].Tex != s[l[k - 1]].Tex || s[l[k]].Mat != s[l[k - 1]].Mat) r++;
            return r;
        }
        private static void Analyze()
        {
            int n = scrDecorationManager.instance.allDecorations.Count; int vis = 0;
            var cam = scrCamera.instance.camobj; ppw = Screen.height / (2f * Math.Max(cam.orthographicSize, 1e-4f));
            var moved = new List<int>(4096); var visIdx = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                if (!b[i].Vis) continue;
                vis++; visIdx.Add(i);
                if (!a[i].Vis) continue;
                if (a[i].Px != b[i].Px || a[i].Py != b[i].Py || a[i].Rot != b[i].Rot || a[i].Sx != b[i].Sx || a[i].Sy != b[i].Sy
                    || a[i].R != b[i].R || a[i].G != b[i].G || a[i].B != b[i].B || a[i].A != b[i].A) moved.Add(i);
            }
            var sprites = new HashSet<int>(); var texs = new HashSet<int>(); var mats = new HashSet<int>(); var orders = new HashSet<long>();
            var mg = new Dictionary<ValueTuple<long, long, long, long, long>, int>();
            var mgs = new Dictionary<ValueTuple<long, long, long, long, long, int>, int>();
            var dup = new Dictionary<ValueTuple<long, long, long, long, long, int>, int>();
            var pure = new Dictionary<ValueTuple<long, long>, int>(); var vars = new HashSet<int>();
            foreach (int i in moved)
            {
                bool dp = a[i].Px != b[i].Px || a[i].Py != b[i].Py, dr = a[i].Rot != b[i].Rot, ds = a[i].Sx != b[i].Sx || a[i].Sy != b[i].Sy;
                bool dc = a[i].R != b[i].R || a[i].G != b[i].G || a[i].B != b[i].B || a[i].A != b[i].A;
                if (dc) sumColor++;
                if (dp && !dr && !ds) { sumPure++; var pk = ValueTuple.Create(Q(b[i].Px - a[i].Px, 1000), Q(b[i].Py - a[i].Py, 1000)); int pc; pure.TryGetValue(pk, out pc); pure[pk] = pc + 1; }
                else if (dp && dr && !ds) sumPosRot++; else if (dp && !dr && ds) sumPosScale++; else if (dp && dr && ds) sumPosRotScale++; else sumOther++;
                vars.Add(b[i].Var);
                {
                    double ext = b[i].Ext * ppw;
                    double dpx = Math.Sqrt((double)(b[i].Px - a[i].Px) * (b[i].Px - a[i].Px) + (double)(b[i].Py - a[i].Py) * (b[i].Py - a[i].Py)) * ppw;
                    double drx = Math.Abs(b[i].Rot - a[i].Rot) * Math.PI / 180.0 * ext;
                    double sx = Math.Max(Math.Abs(a[i].Sx), 1e-4f), sy = Math.Max(Math.Abs(a[i].Sy), 1e-4f);
                    double dsx = (Math.Abs(b[i].Sx - a[i].Sx) / sx + Math.Abs(b[i].Sy - a[i].Sy) / sy) * 0.5 * ext;
                    double tot = dpx + drx + dsx; sumMovedForPx++; sumPxTotal += tot;
                    int bin = tot < 0.01 ? 0 : tot < 0.03 ? 1 : tot < 0.1 ? 2 : tot < 0.3 ? 3 : tot < 1 ? 4 : 5; pxBins[bin]++;
                    int cd = Math.Max(Math.Max(Math.Abs(Mathf.RoundToInt(b[i].R * 255) - Mathf.RoundToInt(a[i].R * 255)), Math.Abs(Mathf.RoundToInt(b[i].G * 255) - Mathf.RoundToInt(a[i].G * 255))),
                                      Math.Max(Math.Abs(Mathf.RoundToInt(b[i].B * 255) - Mathf.RoundToInt(a[i].B * 255)), Math.Abs(Mathf.RoundToInt(b[i].A * 255) - Mathf.RoundToInt(a[i].A * 255))));
                    bool cq = cd == 0; if (cq) sumColorQEq++; else sumColorDif++; if (cd == 1) sumColorStep1++;
                    if (tot < 0.03 && cq) sumAllSmall03++; if (tot < 0.1 && cq) sumAllSmall10++; if (cq && tot < 0.1) sumColorQEqSmall++;
                }
                sprites.Add(b[i].Sprite); texs.Add(b[i].Tex); mats.Add(b[i].Mat); orders.Add(((long)b[i].Layer << 32) | (uint)b[i].Order);
                var key = ValueTuple.Create(Q(b[i].Px - a[i].Px, 1000), Q(b[i].Py - a[i].Py, 1000), Q(b[i].Rot - a[i].Rot, 100), Q(b[i].Sx - a[i].Sx, 1000), Q(b[i].Sy - a[i].Sy, 1000));
                int c; mg.TryGetValue(key, out c); mg[key] = c + 1;
                var ks = ValueTuple.Create(key.Item1, key.Item2, key.Item3, key.Item4, key.Item5, b[i].Sprite); mgs.TryGetValue(ks, out c); mgs[ks] = c + 1;
                var kd = ValueTuple.Create(Q(b[i].Px, 1000), Q(b[i].Py, 1000), Q(b[i].Rot, 100), Q(b[i].Sx, 1000), Q(b[i].Sy, 1000), b[i].Sprite); dup.TryGetValue(kd, out c); dup[kd] = c + 1;
            }
            // 그리기 순서 줄에서 같은 (텍스처,재질)이 이어지는 구간 수
            var bs = b;
            Comparison<int> cmp = (x, y) => { int d = bs[x].Layer.CompareTo(bs[y].Layer); if (d != 0) return d; d = bs[x].Order.CompareTo(bs[y].Order); return d != 0 ? d : x.CompareTo(y); };
            visIdx.Sort(cmp); moved.Sort(cmp);
            pairs++; sumVis += vis; sumMoved += moved.Count; sumTex += texs.Count; sumMat += mats.Count; sumSprite += sprites.Count; sumOrder += orders.Count;
            sumVarN += vars.Count; sumRunsAllTexVar += RunsTV(visIdx, b); sumRunsMovedTexVar += RunsTV(moved, b); sumPureGroups += pure.Count; foreach (var kv in pure) histPure[Bin(kv.Value)] += kv.Value;
            sumRunsAll += Runs(visIdx, b); sumRunsMoved += Runs(moved, b); sumMotionGroups += mg.Count; sumMotionSpriteGroups += mgs.Count;
            int dupDecos = 0; foreach (var kv in dup) if (kv.Value > 1) dupDecos += kv.Value;
            sumDup += dupDecos;
            foreach (var kv in mg) hist[Bin(kv.Value)] += kv.Value;
            foreach (var kv in mgs) histSp[Bin(kv.Value)] += kv.Value;
            if (moved.Count < minMoved) minMoved = moved.Count;
            if (moved.Count > maxMoved) maxMoved = moved.Count;
        }
        internal static string End()
        {
            bool was = active; Stop();
            if (aborted != null) return "중단: " + aborted;
            if (pairs == 0) return was ? "표본 0쌍 (곡 중이 아니거나 연속 프레임 못 잡음)" : "시작 안 함";
            double p = pairs; var sb = new StringBuilder();
            sb.Append("표본 ").Append(pairs).Append("쌍 (곡 ").Append(firstSong.ToString("F1")).Append("~").Append(lastSong.ToString("F1")).Append("초, 간격 ").Append(gap).Append("프레임) | 보이는 이미지 장식 ").Append((sumVis / p).ToString("F0"));
            sb.Append(" | 한 프레임 사이에 값이 바뀐 장식 평균 ").Append((sumMoved / p).ToString("F0")).Append(" (최소 ").Append(minMoved).Append(" 최대 ").Append(maxMoved).Append(")\n");
            sb.Append("  움직인 장식의 서로 다른 스프라이트 ").Append((sumSprite / p).ToString("F0")).Append(" 텍스처 ").Append((sumTex / p).ToString("F0")).Append(" 재질 ").Append((sumMat / p).ToString("F0")).Append(" 그리기 순서값 ").Append((sumOrder / p).ToString("F0")).Append('\n');
            sb.Append("  그리기 순서 줄에서 (텍스처,재질)이 이어지는 구간: 보이는 전체 ").Append((sumRunsAll / p).ToString("F0")).Append(" / 움직인 것만 ").Append((sumRunsMoved / p).ToString("F0")).Append('\n');
            sb.Append("  같은 움직임(위치·회전·크기 변화량 같음) 그룹 ").Append((sumMotionGroups / p).ToString("F0")).Append("개, 장식 수 기준 그룹 크기 분포 1:").Append(Pct(hist, 0)).Append(" 2:").Append(Pct(hist, 1)).Append(" 3~5:").Append(Pct(hist, 2)).Append(" 6~20:").Append(Pct(hist, 3)).Append(" 21+:").Append(Pct(hist, 4)).Append('\n');
            sb.Append("  같은 스프라이트+같은 움직임 그룹 ").Append((sumMotionSpriteGroups / p).ToString("F0")).Append("개, 분포 1:").Append(Pct(histSp, 0)).Append(" 2:").Append(Pct(histSp, 1)).Append(" 3~5:").Append(Pct(histSp, 2)).Append(" 6~20:").Append(Pct(histSp, 3)).Append(" 21+:").Append(Pct(histSp, 4)).Append('\n');
            sb.Append("  재질 종류(셰이더·키워드·큐·마스크·뒤집기 기준, 인스턴스 아님) 움직인 장식 중 ").Append((sumVarN / p).ToString("F1")).Append(" | (레이어·순서값·텍스처·재질종류)가 이어지는 구간(=정렬대로 그릴 때 최소 그리기 횟수) 보이는 전체 ").Append((sumRunsAllTexVar / p).ToString("F0")).Append(" / 움직인 것만 ").Append((sumRunsMovedTexVar / p).ToString("F0")).Append((char)10);
            sb.Append("  바뀐 값 종류(움직인 장식 평균): 위치만 ").Append((sumPure / p).ToString("F0")).Append(" 위치+회전 ").Append((sumPosRot / p).ToString("F0")).Append(" 위치+크기 ").Append((sumPosScale / p).ToString("F0")).Append(" 위치+회전+크기 ").Append((sumPosRotScale / p).ToString("F0")).Append(" 그 밖 ").Append((sumOther / p).ToString("F0")).Append(" | 색도 바뀐 것 ").Append((sumColor / p).ToString("F0")).Append((char)10);
            sb.Append("  위치만 바뀌는 장식의 같은 변화량 그룹 ").Append((sumPureGroups / p).ToString("F0")).Append("개, 분포 1:").Append(Pct(histPure, 0)).Append(" 2:").Append(Pct(histPure, 1)).Append(" 3~5:").Append(Pct(histPure, 2)).Append(" 6~20:").Append(Pct(histPure, 3)).Append(" 21+:").Append(Pct(histPure, 4)).Append((char)10);
            double mp = Math.Max(sumMovedForPx, 1);
            sb.Append("  [오차 허용 가능성] 움직인 장식의 프레임당 화면 변화(위치+회전+크기, 픽셀 합) 분포: <0.01px ").Append((100 * pxBins[0] / mp).ToString("F0")).Append("% <0.03 ").Append((100 * pxBins[1] / mp).ToString("F0")).Append("% <0.1 ").Append((100 * pxBins[2] / mp).ToString("F0")).Append("% <0.3 ").Append((100 * pxBins[3] / mp).ToString("F0")).Append("% <1 ").Append((100 * pxBins[4] / mp).ToString("F0")).Append("% 그 이상 ").Append((100 * pxBins[5] / mp).ToString("F0")).Append("% (평균 ").Append((sumPxTotal / mp).ToString("F2")).Append("px, 화면 ").Append(ppw.ToString("F1")).Append("px/월드단위)").Append((char)10);
            sb.Append("  색(8비트로 반올림했을 때) 안 바뀐 것 ").Append((100 * sumColorQEq / mp).ToString("F0")).Append("% / 바뀐 것 ").Append((100 * sumColorDif / mp).ToString("F0")).Append("% (그중 1단계만 ").Append((100 * sumColorStep1 / mp).ToString("F0")).Append("%) | 위치·회전·크기 변화 <0.03px 이고 색도 안 바뀐 것 ").Append((100 * sumAllSmall03 / mp).ToString("F0")).Append("%, <0.1px 이고 색도 안 바뀐 것 ").Append((100 * sumAllSmall10 / mp).ToString("F0")).Append("%").Append((char)10);
            sb.Append("  같은 자리·같은 모양·같은 스프라이트로 겹친 중복 장식 ").Append((sumDup / p).ToString("F0")).Append("개(움직인 것 중)");
            return sb.ToString();
        }
        private static string Pct(double[] h, int i) { double t = h[0] + h[1] + h[2] + h[3] + h[4]; return t <= 0 ? "-" : (100 * h[i] / t).ToString("F0") + "%"; }
    }
}
#endif
