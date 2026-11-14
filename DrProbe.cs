#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // 직통 렌더러 구조 가정 검증(진단 전용).
    //   drx census            : 메인 카메라가 그리는 렌더러를 (GameObject 층 x 정렬 층 x 종류 x 셰이더)로 센다
    //   drx split on|off      : 앞 카메라(P)가 "배경 장식 층 + 타일 층"을 먼저 그리고 메인 카메라는 나머지를 그린다
    //   drx cb on|off <N>     : 장식 스프라이트 N 개를 forceRenderingOff 로 끄고 같은 순서로 CommandBuffer.DrawRenderer 로 그린다
    internal static class DrProbe
    {
        static Camera main, pre; static int savedMask; static CommandBuffer cb; static readonly List<Renderer> cbOff = new List<Renderer>();
        static void Log(string s) => Main.Entry.Logger.Log("[drx] " + s);
        internal static bool Step(string arg)
        {
            var a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0) throw new ArgumentException("drx census|split on|off|cb on|off");
            switch (a[0])
            {
                case "census": Census(); return true;
                case "split": if (a.Length > 1 && a[1] == "on") SplitOn(); else SplitOff(); return true;
                case "cb": if (a.Length > 1 && a[1] == "on") CbOn(a.Length > 2 ? int.Parse(a[2]) : 100000); else CbOff(); return true;
            }
            throw new ArgumentException("drx " + arg);
        }
        internal static void Stop() { try { SplitOff(); } catch { } try { CbOff(); } catch { } }

        static void Census()
        {
            var cam = GpuDecoScene.FindMain();
            var count = new Dictionary<string, int>();
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
                if ((cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                var m = r.sharedMaterial;
                string key = "go=" + r.gameObject.layer + "(" + LayerMask.LayerToName(r.gameObject.layer) + ") sort=" + r.sortingLayerName + "(" + SortingLayer.GetLayerValueFromID(r.sortingLayerID) + ") " + r.GetType().Name + " " + (m && m.shader ? m.shader.name : "-") + (r.GetComponentInParent<scrDecoration>() ? " deco" : "") + (r.GetComponentInParent<scrFloor>() ? " floor" : "");
                count.TryGetValue(key, out int n); count[key] = n + 1;
            }
            Log("census camera mask=" + cam.cullingMask + " depth=" + cam.depth + " clear=" + cam.clearFlags + " layers=" + string.Join(",", SortingLayer.layers.Select(l => l.name + "=" + l.value)));
            foreach (var kv in count.OrderBy(k => k.Key)) Log("census " + kv.Value + " " + kv.Key);
            int canv = 0; foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>()) if (c && c.gameObject.scene.IsValid() && c.isActiveAndEnabled && c.renderMode == RenderMode.WorldSpace && (cam.cullingMask & (1 << c.gameObject.layer)) != 0) { canv++; if (canv <= 8) Log("census canvas " + c.name + " go=" + c.gameObject.layer + " sort=" + c.sortingLayerName + "/" + c.sortingOrder); }
            Log("census worldCanvases=" + canv);
        }

        static void CopyPose() { if (pre && main) { pre.orthographic = main.orthographic; pre.orthographicSize = main.orthographicSize; pre.nearClipPlane = main.nearClipPlane; pre.farClipPlane = main.farClipPlane; pre.rect = main.rect; if (pre.targetTexture != main.targetTexture) pre.targetTexture = main.targetTexture; } }
        static void PreCull(Camera c) { if (c == pre) CopyPose(); }
        static void SplitOn()
        {
            if (pre) throw new InvalidOperationException("split already on");
            main = GpuDecoScene.FindMain(); int floor = LayerMask.NameToLayer("Floor"); if (floor < 0) throw new InvalidOperationException("Floor layer missing");
            int moved = (1 << 9) | (1 << floor);
            var go = new GameObject("StutterFix.DR.PreCamera"); go.transform.SetParent(main.transform, false);
            pre = go.AddComponent<Camera>(); pre.CopyFrom(main); pre.clearFlags = CameraClearFlags.Nothing; pre.depth = main.depth - 0.005f;
            pre.cullingMask = main.cullingMask & moved; savedMask = main.cullingMask; main.cullingMask = savedMask & ~moved;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            Camera.onPreCull += PreCull;
            Log("split on floorLayer=" + floor + " preMask=" + pre.cullingMask + " mainMask=" + main.cullingMask + " preDepth=" + pre.depth + " mainDepth=" + main.depth + " target=" + (main.targetTexture ? main.targetTexture.width + "x" + main.targetTexture.height : "screen"));
        }
        static void SplitOff()
        {
            Camera.onPreCull -= PreCull;
            if (main && savedMask != 0) main.cullingMask = savedMask; savedMask = 0;
            if (pre) { UnityEngine.Object.Destroy(pre.gameObject); Log("split off"); }
            pre = null;
        }

        // 순서 재현: 장식 스프라이트를 유니티 정렬 키 순서로 CommandBuffer 에 넣는다. 원래 렌더러는 forceRenderingOff.
        static void CbOn(int max)
        {
            if (cb != null) throw new InvalidOperationException("cb already on");
            main = GpuDecoScene.FindMain();
            var list = new List<SpriteRenderer>();
            foreach (var d in scrDecorationManager.instance.allDecorations)
            {
                var v = d as scrVisualDecoration; if (!v || !v.spriteRenderer) continue; var r = v.spriteRenderer;
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff || r.maskInteraction != SpriteMaskInteraction.None) continue;
                if (r.sortingLayerName != "Default") continue;
                list.Add(r);
            }
            list = list.OrderBy(r => r.sortingOrder).ThenBy(r => r.sharedMaterial ? r.sharedMaterial.GetInstanceID() : 0).Take(max).ToList();
            cb = new CommandBuffer { name = "DR.cbtest" };
            foreach (var r in list) { cb.DrawRenderer(r, r.sharedMaterial, 0, 0); r.forceRenderingOff = true; cbOff.Add(r); }
            main.AddCommandBuffer(CameraEvent.AfterForwardAlpha, cb);
            Log("cb on renderers=" + list.Count + " (Default 층 장식 스프라이트, AfterForwardAlpha: 행성 위에 그려지므로 순서 대조용이 아니라 DrawRenderer 동작 확인용)");
        }
        static void CbOff()
        {
            if (cb != null) { if (main) main.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, cb); cb.Release(); cb = null; Log("cb off restored=" + cbOff.Count); }
            foreach (var r in cbOff) if (r) r.forceRenderingOff = false; cbOff.Clear();
        }
    }
}
#endif
