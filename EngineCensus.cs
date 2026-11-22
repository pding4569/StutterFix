#if DEV || AUTOTEST
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StutterFix
{
    // 진단 전용(enginecensus): 엔진이 매 프레임 훑는 것들(2D 충돌체·스프라이트 마스크·오디오 소스)의 수를 센다.
    // HELLO 프로파일(2026-11-22)에서 PhysicsManager2D::SyncTransforms 3%, SpriteMaskPrepareRendererNodes 3.7%, AudioManager::Update 6.1% 였다.
    internal static class EngineCensus
    {
        static string Owner(Component c)
        {
            if (c.GetComponentInParent<scrFloor>() != null) return "floor";
            if (c.GetComponentInParent<scrDecoration>() != null) return "deco";
            if (c.GetComponentInParent<scrPlanet>() != null) return "planet";
            return "other:" + c.transform.root.name;
        }
        static string Top(Dictionary<string, int> d, int n) => string.Join(", ", d.OrderByDescending(kv => kv.Value).Take(n).Select(kv => kv.Key + "=" + kv.Value));
        static void Add(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int v); d[k] = v + 1; }

        private static readonly List<AudioSource> disabled = new List<AudioSource>();
        // 진단: 이름에 key 가 들어간 루트 아래의 켜져 있지만 재생 중이 아닌 AudioSource 를 끈다(on 으로 되돌림). 측정 전용.
        internal static string AudioOff(string arg)
        {
            var a = (arg ?? "").Trim();
            if (a == "on") { int n0 = 0; foreach (var s in disabled) if (s) { s.enabled = true; n0++; } disabled.Clear(); return "restored " + n0; }
            int n = 0; bool all = a.StartsWith("all ");   // "all <이름>": 재생 중인 것까지 끈다(측정 전용, 소리가 빠진다)
            if (all) a = a.Substring(4).Trim();
            foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (s.enabled && (all || !s.isPlaying) && s.transform.root.name.Contains(a)) { s.enabled = false; disabled.Add(s); n++; }
            return "disabled " + n + " idle sources under '" + a + "'";
        }

        // 진단: 판정 오차 막대가 실제로 화면에 보이는지(캔버스·그룹 투명도·화면 위치·보이는 그림 수).
        internal static string MeterInfo()
        {
            var sb = new StringBuilder();
            foreach (var m in Object.FindObjectsByType<scrHitErrorMeter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.Append("meter '").Append(m.name).Append("' active=").Append(m.gameObject.activeInHierarchy).Append(" enabled=").Append(m.enabled);
                foreach (var c in m.GetComponentsInChildren<Canvas>(true))
                    sb.Append(" | canvas ").Append(c.name).Append(" en=").Append(c.enabled).Append(" act=").Append(c.gameObject.activeInHierarchy)
                      .Append(" mode=").Append(c.renderMode).Append(" order=").Append(c.sortingOrder).Append(" scale=").Append(c.scaleFactor.ToString("F3"));
                for (var t = m.transform; t != null; t = t.parent)
                    foreach (var g in t.GetComponents<CanvasGroup>()) sb.Append(" | group ").Append(t.name).Append(" a=").Append(g.alpha.ToString("F3")).Append(" en=").Append(g.enabled);
                foreach (var g in m.GetComponentsInChildren<CanvasGroup>(true)) sb.Append(" | childgroup ").Append(g.name).Append(" a=").Append(g.alpha.ToString("F3")).Append(" en=").Append(g.enabled);
                var corners = new Vector3[4];
                var rt = m.transform as RectTransform;
                if (rt != null)
                {
                    rt.GetWorldCorners(corners);
                    sb.Append(" | rect ").Append(corners[0].ToString("F0")).Append("..").Append(corners[2].ToString("F0")).Append(" lossy=").Append(rt.lossyScale.ToString("F3"));
                }
                int gTotal = 0, gEnabled = 0, gVisible = 0; var vis = new Dictionary<string, int>();
                foreach (var g in m.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    gTotal++;
                    if (!g.enabled || !g.gameObject.activeInHierarchy) continue;
                    gEnabled++;
                    float a = g.color.a * g.canvasRenderer.GetAlpha() * g.canvasRenderer.GetInheritedAlpha();
                    if (a > 0f && !g.canvasRenderer.cull) { gVisible++; Add(vis, g.name); }
                    if (g.name == "StraightMeter" || g.name == "Hand" || (g.name == "Body" && gEnabled < 6))
                    {
                        g.rectTransform.GetWorldCorners(corners);
                        sb.Append(" | ").Append(g.name).Append(" ").Append(corners[0].ToString("F0")).Append("..").Append(corners[2].ToString("F0"))
                          .Append(" a=").Append(g.color.a.ToString("F2")).Append("/").Append(g.canvasRenderer.GetAlpha().ToString("F2"))
                          .Append(" sprite=").Append(g is UnityEngine.UI.Image im && im.sprite != null ? im.sprite.name : "-");
                    }
                }
                sb.Append(" | graphics total=").Append(gTotal).Append(" enabled=").Append(gEnabled).Append(" visible=").Append(gVisible).Append(" [").Append(Top(vis, 6)).Append("]");
                sb.Append(" | screen=").Append(Screen.width).Append("x").Append(Screen.height).Append("\n");
            }
            return sb.Length == 0 ? "no meter" : sb.ToString();
        }

        internal static string Run()
        {
            var sb = new StringBuilder();
            var cols = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int active = 0; var byLayer = new Dictionary<string, int>(); var byOwner = new Dictionary<string, int>(); var byType = new Dictionary<string, int>();
            foreach (var c in cols)
            {
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                active++;
                Add(byLayer, c.gameObject.layer.ToString());
                Add(byOwner, Owner(c));
                Add(byType, c.GetType().Name + (c.isTrigger ? "(trigger)" : ""));
            }
            int bodies = Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            sb.Append("collider2d total=").Append(cols.Length).Append(" active=").Append(active)
              .Append(" layers[").Append(Top(byLayer, 8)).Append("] owners[").Append(Top(byOwner, 6)).Append("] types[").Append(Top(byType, 6)).Append("]")
              .Append(" rigidbody2d=").Append(bodies)
              .Append(" autoSync=").Append(Physics2D.autoSyncTransforms).Append(" simMode=").Append(Physics2D.simulationMode);

            var masks = Object.FindObjectsByType<SpriteMask>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int mActive = 0; var mOwner = new Dictionary<string, int>();
            foreach (var m in masks) if (m.enabled && m.gameObject.activeInHierarchy) { mActive++; Add(mOwner, Owner(m)); }
            int interacting = 0;
            foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.enabled && r.maskInteraction != SpriteMaskInteraction.None) interacting++;
            sb.Append(" | spritemask total=").Append(masks.Length).Append(" active=").Append(mActive).Append(" owners[").Append(Top(mOwner, 4))
              .Append("] renderers_with_mask_interaction=").Append(interacting);

            var srcs = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int sActive = 0, playing = 0; var sOwner = new Dictionary<string, int>();
            foreach (var s in srcs)
            {
                if (!s.enabled || !s.gameObject.activeInHierarchy) continue;
                sActive++; if (s.isPlaying) playing++;
                Add(sOwner, s.transform.root.name);
            }
            sb.Append(" | audiosource total=").Append(srcs.Length).Append(" active=").Append(sActive).Append(" playing=").Append(playing)
              .Append(" roots[").Append(Top(sOwner, 6)).Append("] listenerVol=").Append(AudioListener.volume.ToString("F3"));
            return sb.ToString();
        }
    }
}
#endif
