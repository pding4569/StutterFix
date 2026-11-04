#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 시험 전용 일회성 조사: 지금 켜진 객체, 매 프레임 불리는 스크립트, 카메라, 다른 모드 패치 목록을 파일로 남긴다.
    // 찍는 프레임이 느려지므로 성능 구간과 겹치지 않게 부른다. RenderDoc 으로 켰으면 프레임 캡처도 건다.
    internal static class RuntimeCensus
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly string[] Messages = { "Update", "LateUpdate", "FixedUpdate", "OnGUI", "OnBecameVisible", "OnWillRenderObject", "OnRenderImage", "OnPreRender", "OnPostRender", "OnPreCull" };

        internal static void Run(string tag)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var o = new StringBuilder();
            o.AppendLine("# frame=" + Time.frameCount + " tag=" + tag + " screen=" + Screen.width + "x" + Screen.height + " gfxjobs=" + SystemInfo.graphicsMultiThreaded + " worker=" + Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount);
            Behaviours(o); Renderers(o); Transforms(o); Cameras(o); Other(o); Patches(o);
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StutterFix");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "rtcensus-" + tag + ".txt");
            File.WriteAllText(path, o.ToString(), new UTF8Encoding(false));
            Main.Entry.Logger.Log("[rtcensus] " + path + " " + sw.ElapsedMilliseconds + "ms");
        }

        private static void Behaviours(StringBuilder o)
        {
            var seen = new Dictionary<Type, string>();
            var counts = new Dictionary<Type, int>(); var total = new Dictionary<Type, int>();
            foreach (var b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!b) continue;
                var t = b.GetType();
                total.TryGetValue(t, out int a); total[t] = a + 1;
                if (!b.isActiveAndEnabled) continue;
                counts.TryGetValue(t, out int n); counts[t] = n + 1;
                if (!seen.ContainsKey(t))
                {
                    var found = new List<string>();
                    for (var x = t; x != null && x != typeof(MonoBehaviour); x = x.BaseType)
                        foreach (string m in Messages)
                            if (!found.Contains(m) && x.GetMethods(Any).Any(k => k.Name == m)) found.Add(m);
                    seen[t] = string.Join(",", found);
                }
            }
            o.AppendLine("## 스크립트: 켜짐\t전체\t프레임 함수\t형식");
            foreach (var p in counts.OrderByDescending(p => p.Value))
                o.AppendLine(p.Value + "\t" + total[p.Key] + "\t" + seen[p.Key] + "\t" + p.Key.FullName);
        }

        private static void Renderers(StringBuilder o)
        {
            var rows = new Dictionary<string, int[]>();
            var materials = new HashSet<int>(); var textures = new HashSet<int>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r) continue;
                string k = r.GetType().Name;
                if (!rows.TryGetValue(k, out var c)) rows[k] = c = new int[5];
                c[0]++;
                if (!r.gameObject.activeInHierarchy) continue;
                c[1]++;
                if (!r.enabled) continue;
                c[2]++;
                if (r.forceRenderingOff) continue;
                c[3]++;
                if (r.isVisible) c[4]++;
                var m = r.sharedMaterial;
                if (m) { materials.Add(m.GetInstanceID()); if (m.HasProperty("_MainTex") && m.mainTexture) textures.Add(m.mainTexture.GetInstanceID()); }
            }
            o.AppendLine("## 렌더러: 전체\t활성\t켜짐\t강제끔아님\t보임\t형식");
            foreach (var p in rows.OrderByDescending(p => p.Value[0])) o.AppendLine(string.Join("\t", p.Value) + "\t" + p.Key);
            o.AppendLine("# 재질(켜진 렌더러) " + materials.Count + " 주 텍스처 " + textures.Count);
        }

        private static void Transforms(StringBuilder o)
        {
            var roots = new Dictionary<Transform, int>();
            int all = 0, active = 0, maxDepth = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t) continue;
                all++;
                if (t.gameObject.activeInHierarchy) active++;
                roots.TryGetValue(t.root, out int n); roots[t.root] = n + 1;
                int d = 0; for (var x = t.parent; x; x = x.parent) d++;
                if (d > maxDepth) maxDepth = d;
            }
            o.AppendLine("## Transform 전체 " + all + " 활성 " + active + " 계층 " + roots.Count + " 최대 깊이 " + maxDepth);
            foreach (var p in roots.OrderByDescending(p => p.Value).Take(15)) o.AppendLine(p.Value + "\t" + p.Key.name + "\t용량 " + p.Key.hierarchyCapacity);
        }

        private static void Cameras(StringBuilder o)
        {
            o.AppendLine("## 카메라: 이름 켜짐 순서 지움 mask 대상 깊이텍스처 HDR MSAA 켜진효과");
            foreach (var c in Camera.allCameras.OrderBy(c => c.depth))
            {
                var fx = c.GetComponents<MonoBehaviour>().Where(b => b.enabled && b.GetType().GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null).Select(b => b.GetType().Name);
                o.AppendLine(c.name + "\t" + c.enabled + "\t" + c.depth + "\t" + c.clearFlags + "\t" + c.cullingMask + "\t" + (c.targetTexture ? c.targetTexture.name + c.targetTexture.width : "화면") + "\t" + c.depthTextureMode + "\t" + c.allowHDR + "\t" + c.allowMSAA + "\t" + string.Join(",", fx));
            }
        }

        private static void Other(StringBuilder o)
        {
            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            o.AppendLine("## 오디오 소스 활성 " + sources.Length + " 재생 중 " + sources.Count(s => s.isPlaying));
            var ps = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            o.AppendLine("## 입자 활성 " + ps.Length + " 살아 있음 " + ps.Count(p => p.IsAlive(false)));
            o.AppendLine("## 캔버스 " + UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length + " 그래픽 " + UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length);
            o.AppendLine("## 트윈 활성 " + DOTween.TotalActiveTweens() + " 재생 중 " + DOTween.TotalPlayingTweens());
            o.AppendLine("## 재질 객체 전체 " + Resources.FindObjectsOfTypeAll<Material>().Length + " 텍스처 " + Resources.FindObjectsOfTypeAll<Texture2D>().Length);
        }

        private static void Patches(StringBuilder o)
        {
            o.AppendLine("## 패치: 함수\t접두\t접미\t옮김\t주인");
            foreach (var m in Harmony.GetAllPatchedMethods())
            {
                var i = Harmony.GetPatchInfo(m);
                if (i == null) continue;
                var who = i.Owners.Select(s => s).OrderBy(s => s);
                o.AppendLine((m.DeclaringType != null ? m.DeclaringType.FullName : "?") + "." + m.Name + "\t" + i.Prefixes.Count + "\t" + i.Postfixes.Count + "\t" + i.Transpilers.Count + "\t" + string.Join(",", who));
            }
        }

        // RenderDoc 이 주입된 경우에만 다음 프레임을 캡처한다.
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetApi(int version, out IntPtr api);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void TriggerCapture();

        internal static void Capture()
        {
            IntPtr module = GetModuleHandleW("renderdoc.dll");
            if (module == IntPtr.Zero) throw new InvalidOperationException("RenderDoc 으로 켜지 않았다");
            var get = Marshal.GetDelegateForFunctionPointer<GetApi>(GetProcAddress(module, "RENDERDOC_GetAPI"));
            if (get(10400, out IntPtr api) != 1) throw new InvalidOperationException("RenderDoc API 1.4 없음");
            // renderdoc_app.h: 함수표에서 TriggerCapture 는 GetAPIVersion=0 부터 15번째
            IntPtr fn = Marshal.ReadIntPtr(api, 15 * IntPtr.Size);
            Marshal.GetDelegateForFunctionPointer<TriggerCapture>(fn)();
            Main.Entry.Logger.Log("[rdcapture] 다음 프레임 캡처 요청 frame=" + Time.frameCount);
        }
    }
}
#endif
