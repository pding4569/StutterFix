#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 직통 렌더러가 켜져 있으면 유니티 카메라가 맵 레이어를 그리지 않아 renderer.isVisible 과
    // OnBecameVisible/OnBecameInvisible 이 "안 보임"으로 굳는다. 게임은 이것으로 갱신을 켜고 끈다:
    //   scrFloor(타일 Update 켜기/끄기), ToggleVisible, Mawaru_Sprite, scrVisualDecoration.UpdateShader(필터 장식),
    //   scrFloor.Start/OnBecameInvisible(topGlow.isVisible), scrOptionsWindows.SetIcons.
    // 여기서는 메인 카메라 기준 CullingGroup(경계 구, 넉넉함 = 일찍 보임)으로 보임을 대신 판정하고
    // 콜백·isVisible 을 우리 판정으로 바꾼다. 켜져 있는 동안 유니티가 보내는 콜백은 막는다.
    internal static unsafe partial class DirectFull
    {
        const string VisibilityId = "StutterFix.drfull.visibility";
        static Harmony visibilityHooks;
        static CullingGroup visGroup;
        static BoundingSphere[] visSpheres = new BoundingSphere[0];
        static bool[] visState = new bool[0], visSphere = new bool[0];
        static Renderer[] visRenderers = new Renderer[0];
        static Vector3[] visLocalCenter = new Vector3[0]; static float[] visLocalRadius = new float[0];
        static Action[][] visOn = new Action[0][], visOff = new Action[0][];
        static int visCount;
        static readonly Dictionary<Renderer, int> visIndex = new Dictionary<Renderer, int>(RefEq<Renderer>.I);
        static readonly List<int> visDirty = new List<int>();
        static bool visEmulating, visEnabled;
        static long visCallbacks, visShimHits, visRegistered;
        static readonly Type[] visCallbackTypes = { typeof(scrFloor), typeof(ToggleVisible), typeof(Mawaru_Sprite) };

        static void StartVisibility(Camera main)
        {
            visEnabled = !System.IO.File.Exists(System.IO.Path.Combine(Main.Entry.Path, "drfull-novis.txt"));
            if (!visEnabled) { Log("visibility emulation OFF (drfull-novis.txt)"); return; }
            visIndex.Clear(); visDirty.Clear(); visCount = 0; visCallbacks = visShimHits = visRegistered = 0;
            visGroup = new CullingGroup { targetCamera = main };
            visGroup.onStateChanged = VisStateChanged;
            // 콜백을 가진 컴포넌트의 렌더러(같은 GameObject)를 모두 등록. 초기 상태 = 지금 유니티 판정.
            foreach (var t in visCallbackTypes)
                foreach (var mb in Resources.FindObjectsOfTypeAll(t))
                {
                    var c = mb as Component; if (!c || !c.gameObject.scene.IsValid()) continue;
                    var r = c.GetComponent<Renderer>(); if (r) VisRegister(r);
                }
            visGroup.SetBoundingSpheres(visSpheres); visGroup.SetBoundingSphereCount(visCount);
            if (visibilityHooks == null)
            {
                visibilityHooks = new Harmony(VisibilityId);
                var block = new HarmonyMethod(typeof(DirectFull), nameof(VisBlockPrefix));
                foreach (var t in visCallbackTypes)
                    foreach (var n in new[] { "OnBecameVisible", "OnBecameInvisible" })
                    {
                        var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (m != null) visibilityHooks.Patch(m, prefix: block);
                    }
                var shim = new HarmonyMethod(typeof(DirectFull), nameof(VisShimTranspiler));
                foreach (var (t, n) in new[] { (typeof(scrFloor), "Start"), (typeof(scrFloor), "OnBecameInvisible"), (typeof(scrVisualDecoration), "UpdateShader"), (typeof(scrOptionsWindows), "SetIcons") })
                    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (m.Name == n && m.GetMethodBody() != null) visibilityHooks.Patch(m, transpiler: shim);
            }
            Log("visibility emulation ON tracked=" + visCount);
        }
        static void StopVisibility()
        {
            visibilityHooks?.UnpatchAll(VisibilityId); visibilityHooks = null;
            if (visGroup != null) { visGroup.onStateChanged = null; visGroup.Dispose(); visGroup = null; }
            visIndex.Clear(); visDirty.Clear(); visCount = 0; visEnabled = false;
        }
        static int VisRegister(Renderer r)
        {
            if (visIndex.TryGetValue(r, out int i)) return i;
            i = visCount++;
            if (i >= visSpheres.Length)
            {
                int n = Math.Max(256, visSpheres.Length * 2);
                Array.Resize(ref visSpheres, n); Array.Resize(ref visState, n); Array.Resize(ref visSphere, n); Array.Resize(ref visRenderers, n);
                Array.Resize(ref visLocalCenter, n); Array.Resize(ref visLocalRadius, n); Array.Resize(ref visOn, n); Array.Resize(ref visOff, n);
                visGroup?.SetBoundingSpheres(visSpheres);
            }
            visIndex.Add(r, i); visRenderers[i] = r; visRegistered++;
            var lb = r.localBounds; visLocalCenter[i] = lb.center; visLocalRadius[i] = lb.extents.magnitude;
            var wb = r.bounds; visSpheres[i] = new BoundingSphere(wb.center, wb.extents.magnitude);
            visState[i] = visSphere[i] = r.isVisible;
            var on = new List<Action>(); var off = new List<Action>();
            foreach (var mb in r.GetComponents<MonoBehaviour>())
            {
                if (!mb || Array.IndexOf(visCallbackTypes, mb.GetType()) < 0) continue;
                var mv = mb.GetType().GetMethod("OnBecameVisible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var mi = mb.GetType().GetMethod("OnBecameInvisible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mv != null) on.Add((Action)Delegate.CreateDelegate(typeof(Action), mb, mv));
                if (mi != null) off.Add((Action)Delegate.CreateDelegate(typeof(Action), mb, mi));
            }
            visOn[i] = on.ToArray(); visOff[i] = off.ToArray();
            visGroup?.SetBoundingSphereCount(visCount);
            return i;
        }
        // 렌더러 하나가 움직였을 때(포즈 결과에서) 구를 갱신.
        static void VisMoved(Renderer r, Matrix4x4 world)
        {
            if (!visEnabled || !visIndex.TryGetValue(r, out int i)) return;
            Vector3 c = world.MultiplyPoint3x4(visLocalCenter[i]);
            float s = Mathf.Max(((Vector3)world.GetColumn(0)).magnitude, Mathf.Max(((Vector3)world.GetColumn(1)).magnitude, ((Vector3)world.GetColumn(2)).magnitude));
            visSpheres[i] = new BoundingSphere(c, visLocalRadius[i] * s);
        }
        static void VisStateChanged(CullingGroupEvent e)
        {
            if (e.index >= visCount) return;
            visSphere[e.index] = e.isVisible; visDirty.Add(e.index);
        }
        static void VisLiveChanged(Renderer r) { if (visEnabled && visIndex.TryGetValue(r, out int i)) visDirty.Add(i); }
        // PreCull 에서: 바뀐 것만 보임 = 구 보임 && 켜짐 으로 다시 계산해 콜백.
        static void VisFlush()
        {
            if (!visEnabled || visDirty.Count == 0) return;
            visEmulating = true;
            try
            {
                foreach (int i in visDirty)
                {
                    var r = visRenderers[i];
                    bool now = r && visSphere[i] && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff;
                    if (now == visState[i]) continue;
                    visState[i] = now;
                    var list = now ? visOn[i] : visOff[i];
                    foreach (var a in list) { try { a(); visCallbacks++; } catch (Exception ex) { Log("visibility callback failed: " + ex.Message); } }
                }
            }
            finally { visEmulating = false; visDirty.Clear(); }
        }
        static bool VisBlockPrefix() => !Rendering || visEmulating || !visEnabled;
        public static bool VisibleShim(Renderer r)
        {
            if (!Rendering || !visEnabled || !r) return r && r.isVisible;
            visShimHits++;
            if (!visIndex.TryGetValue(r, out int i))
            {
                i = VisRegister(r);
                // 처음 묻는 렌더러: 지금 카메라 평면으로 바로 판정(유니티 판정은 우리 렌더 중엔 틀림).
                GeometryUtility.CalculateFrustumPlanes(camera, visPlanes);
                visSphere[i] = GeometryUtility.TestPlanesAABB(visPlanes, r.bounds);
                visState[i] = visSphere[i] && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff;
            }
            return visState[i];
        }
        static readonly Plane[] visPlanes = new Plane[6];
        static IEnumerable<CodeInstruction> VisShimTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var getter = AccessTools.PropertyGetter(typeof(Renderer), nameof(Renderer.isVisible));
            var shim = AccessTools.Method(typeof(DirectFull), nameof(VisibleShim));
            foreach (var ins in instructions)
            {
                if ((ins.opcode == OpCodes.Callvirt || ins.opcode == OpCodes.Call) && ins.operand is MethodInfo m && m == getter) { ins.opcode = OpCodes.Call; ins.operand = shim; }
                yield return ins;
            }
        }
        static string VisibilityState() => FormattableString.Invariant($"vis_enabled={visEnabled} vis_tracked={visCount} vis_callbacks={visCallbacks} vis_shim_hits={visShimHits}");
    }
}
#endif
