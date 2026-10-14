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
        internal sealed class Candidate
        {
            internal Type Type;
            internal Func<object, bool> Neutral;
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
            string[] names = { "Color_Chromatic_Aberration", "Colors_Brightness", "Distortion_Dream2", "TV_Chromatical", "Color_BrightContrastSaturation", "Color_Contrast" };
            string[][] fields = { new[]{"Offset"}, new[]{"_Brightness"}, new[]{"Distortion"}, new[]{"Intensity"}, new[]{"Brightness", "Contrast", "Saturation"}, new[]{"Contrast"} };
            float[] values = { 0, 1, 0, 0, 1, 1 };
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
                var c = new Candidate { Type = t, Neutral = Expression.Lambda<Func<object,bool>>(test, p).Compile() };
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
            if (!Enabled || __0 == null || __0.format != RenderTextureFormat.ARGB32 || !Candidates.TryGetValue(__instance.GetType(), out var c) || c.Approved.Count == 0 || !c.Neutral(__instance)) return true;
#if DEV || AUTOTEST
            if (FilterIdentityProbe.Running) return true;
#endif
            if (__1 != null && (__1.format != __0.format || __1.sRGB != __0.sRGB || __1.width != __0.width || __1.height != __0.height)) return true;
            if (!c.Approved.Contains(Key(__0))) return true;
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
