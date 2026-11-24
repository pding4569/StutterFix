using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // No certificate is shipped: only measurements made by the explicit test command authorize a bypass.
    internal static class FilterIdentity
    {
        internal static bool Enabled = true;
        // 증명된 중립 건너뛰기(기본 켬): Blur_Movie Radius==0·FastFilter==1·Factor>=0, ARGB32 같은 크기 대상.
        // 100개 샘플이 모두 같은 텍셀 c 이고 결과는 Σ(c·w)/Σw = c·(1±6e-6) 이라 8비트로는 0.0015/255 안: 원래 바이트와 같다.
        // Windflower 곡 전체 Blur_Movie 호출의 32%(1만 번, 한 번 약 1ms GPU), 같은 입력 대조 83표본 최대 차이 0/255(2026-11-24).
        internal static bool ProvenEnabled = true;
        internal static long ProvenSkips, DbgFmt, DbgNotAllowed, DbgNeutral, DbgNullDest, DbgDestMismatch;
        internal static string Dbg() { var sb = new System.Text.StringBuilder(); foreach (var kv in Candidates) sb.Append(kv.Key.Name).Append(kv.Value.Proven ? "(proven)" : "").Append(' '); return "fmt_or_type=" + DbgFmt + " notallowed=" + DbgNotAllowed + " neutral=" + DbgNeutral + " nulldest=" + DbgNullDest + " destmismatch=" + DbgDestMismatch + " cands=" + sb; }
        internal sealed class Candidate
        {
            internal Type Type;
            internal Func<object, bool> Neutral;
            internal bool Proven;   // 인증서 없이 건너뛰어도 되는 것(수학적 근거 + 게임 대조): 아래 Blur_Movie
            internal Action<object> SideEffects;   // 원래 OnRenderImage 의 그림 아닌 상태 변화(건너뛸 때도 똑같이)
            internal readonly HashSet<(int,int,int,bool,int)> Approved = new HashSet<(int,int,int,bool,int)>();
        }
        internal static readonly Dictionary<Type, Candidate> Candidates = new Dictionary<Type, Candidate>();
        internal static string EnvironmentKey, CertificatePath;
        internal static (int,int,int,bool,int) Key(RenderTexture src) => (src.width, src.height, (int)src.format, src.sRGB, src.antiAliasing);
        internal static bool Parse(string text, out (int,int,int,bool,int) key)
        {
            key = default; var p = text.Split(':');
            if (p.Length != 5 || !int.TryParse(p[0],out var w) || !int.TryParse(p[1],out var h) || !int.TryParse(p[2],out var f) || !bool.TryParse(p[3],out var srgb) || !int.TryParse(p[4],out var aa)) return false;
            key = (w,h,f,srgb,aa); return true;
        }
        internal static string Context(RenderTexture src) => src.width + ":" + src.height + ":" + (int)src.format + ":" + src.sRGB + ":" + src.antiAliasing;
        internal static void Install(Harmony h)
        {
            CertificatePath = Path.Combine(Main.Entry.Path, "filteridentity-approved.txt");
            EnvironmentKey = Application.unityVersion + "|" + SystemInfo.graphicsDeviceName + "|" + SystemInfo.graphicsDeviceVersion + "|" + QualitySettings.activeColorSpace;
            string[] names = { "Color_Chromatic_Aberration", "Colors_Brightness", "Distortion_Dream2", "TV_Chromatical", "Color_BrightContrastSaturation", "Color_Contrast", "Blur_Radial", "Blur_Radial_Fast", "Blur_Movie", "Color_Noise", "EXTRA_Rotation" };
            string[][] fields = { new[]{"Offset"}, new[]{"_Brightness"}, new[]{"Distortion"}, new[]{"Intensity"}, new[]{"Brightness", "Contrast", "Saturation"}, new[]{"Contrast"}, new[]{"Intensity"}, new[]{"Intensity"}, new[]{"Radius"}, new[]{"Noise"}, new[]{"Rotation"} };
            float[] values = { 0, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0 };
            for (int i = 0; i < names.Length; i++)
            {
                var t = AccessTools.TypeByName("CameraFilterPack_" + names[i]);
                if (t == null) continue;
                var m = t.GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[]{typeof(RenderTexture), typeof(RenderTexture)}, null);
                if (m == null) continue;
                var p = Expression.Parameter(typeof(object));
                Expression test = Expression.Constant(true);
                bool valid = true;
                foreach (var name in fields[i])
                {
                    var f = t.GetField(name);
                    if (f == null || f.FieldType != typeof(float)) { valid = false; break; }
                    test = Expression.AndAlso(test, Expression.Equal(Expression.Field(Expression.Convert(p, t), f), Expression.Constant(values[i])));
                }
                if (!valid) continue;
                // Blur_Movie: 반경 0 이어도 FastFilter>1 이면 반해상도로 줄였다 키워 원본과 다르다(WF 대조 95표본 중 13 실패, 최대 48/255, 2026-11-24).
                // Factor<0 이면 가중치(c^3*k+1)가 0 근처가 될 수 있다. 둘 다 원래 경로로.
                if (names[i] == "Blur_Movie")
                {
                    var ff = t.GetField("FastFilter"); var fa = t.GetField("Factor");
                    if (ff == null || ff.FieldType != typeof(int) || fa == null || fa.FieldType != typeof(float)) continue;
                    test = Expression.AndAlso(test, Expression.Equal(Expression.Field(Expression.Convert(p, t), ff), Expression.Constant(1)));
                    test = Expression.AndAlso(test, Expression.GreaterThanOrEqual(Expression.Field(Expression.Convert(p, t), fa), Expression.Constant(0f)));
                }
                var c = new Candidate { Type = t, Neutral = Expression.Lambda<Func<object,bool>>(test, p).Compile() };
                if (names[i] == "Blur_Movie")
                {
                    var tx = t.GetField("TimeX", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (tx != null && tx.FieldType == typeof(float))
                    {
                        // 원래: SCShader 가 있으면 TimeX += Time.deltaTime; if (TimeX > 100) TimeX = 0; (셰이더는 _TimeX 를 안 쓰지만 상태는 같게)
                        var scf = t.GetField("SCShader", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        c.SideEffects = o =>
                        {
                            var shader = scf != null ? scf.GetValue(o) as UnityEngine.Object : null;
                            if (scf != null && shader == null) return;
                            float v = (float)tx.GetValue(o) + Time.deltaTime; if (v > 100f) v = 0f; tx.SetValue(o, v);
                        };
                        c.Proven = scf != null;
                    }
                }
                Candidates.Add(t, c);
                EnvironmentKey += "|" + t.Module.ModuleVersionId;
                h.Patch(m, prefix: new HarmonyMethod(typeof(FilterIdentity), nameof(Prefix)));
            }
            if (File.Exists(CertificatePath))
            {
                var lines = File.ReadAllLines(CertificatePath);
                if (lines.Length > 0 && lines[0] == EnvironmentKey)
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|');
                        if (parts.Length != 2) continue;
                        if (Parse(parts[1], out var key))
                            foreach (var c in Candidates.Values) if (c.Type.Name == parts[0]) c.Approved.Add(key);
                    }
            }
        }
        public static bool Prefix(object __instance, RenderTexture __0, RenderTexture __1)
        {
            if (__0 == null || __0.format != RenderTextureFormat.ARGB32 || !Candidates.TryGetValue(__instance.GetType(), out var c)) { DbgFmt++; return true; }
            bool proven = c.Proven && ProvenEnabled;
            if (!proven && (!Enabled || c.Approved.Count == 0)) { DbgNotAllowed++; return true; }
            if (!c.Neutral(__instance)) return true;
            DbgNeutral++;
#if DEV || AUTOTEST
            if (FilterIdentityProbe.Running) return true;
#endif
            if (__1 == null ? proven : (__1.format != __0.format || __1.sRGB != __0.sRGB || __1.width != __0.width || __1.height != __0.height)) { if (__1 == null) DbgNullDest++; else DbgDestMismatch++; return true; }
            if (!proven && !c.Approved.Contains(Key(__0))) return true;
            if (proven) { c.SideEffects?.Invoke(__instance); ProvenSkips++; }
            Graphics.Blit(__0, __1);
            return false;
        }
        internal static void Shutdown()
        {
#if DEV || AUTOTEST
            FilterIdentityProbe.Stop();
#endif
            Candidates.Clear();
        }
    }
}
