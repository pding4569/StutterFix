using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 타일 머티리얼 누수 막기.
    //
    // 타일 렌더러는 준비될 때(FloorRenderer.Awake) renderer.material 로 타일 머티리얼을 하나씩 복제하고, 커스텀 월드 머티리얼이 있으면
    // scrFloor.Start 가 SetMaterial(new Material(..)) 로 또 만든다. 에디터에서 다른 맵을 열면 게임은 타일을 새로 만드는데, 지워진 옛 타일의
    // 복제 머티리얼은 유니티가 자동으로 지우지 않고 Resources.UnloadUnusedAssets 로도 풀리지 않았다(이 모드 패치를 전부 떼도 같음 -
    // 게임 원래 동작). 2026-09-28 측정(9만 타일 맵): 맵을 열 때마다 "FloorMeshDefault (Instance)" 9만 개(128MB)씩 쌓였다.
    //   조사: 모든 정적 필드·장면 오브젝트·새 타일에서 출발해 관리 객체를 끝까지 따라가도 그 머티리얼에 닿는 경로가 없었고,
    //   그 머티리얼을 쓰는 렌더러도 없었다. 직접 지운 뒤 재생·편집·다른 맵 열기에서 오류 0.
    // 그래서 타일 렌더러마다 "지금 가진 런타임 머티리얼"(인스턴스 id 음수 = 에셋이 아님)을 기록해 두었다가, 타일 만들기(MakeLevel)가 끝날
    // 때마다 (1) 렌더러가 지워진 타일의 머티리얼, (2) SetMaterial 로 바뀌어 버려진 머티리얼 중 지금 타일들이 쓰지 않는 것을 지운다.
    internal static class MatReuse
    {
        internal static bool Enabled = true;
        internal static long Freed;
        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly RefEq<T> I = new RefEq<T>();
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly Dictionary<FloorRenderer, Material> owned = new Dictionary<FloorRenderer, Material>(RefEq<FloorRenderer>.I);
        private static readonly List<Material> dropped = new List<Material>();

        internal static void Install(Harmony h)
        {
            var awake = AccessTools.Method(typeof(FloorRenderer), "Awake");
            var set = AccessTools.Method(typeof(FloorRenderer), "SetMaterial");
            var make = AccessTools.Method(typeof(scrLevelMaker), "MakeLevel");
            if (awake == null || set == null || make == null) { Main.Entry.Logger.Log("[타일 머티리얼] 게임 코드 모양이 달라 쓰지 않음"); return; }
            h.Patch(awake, postfix: new HarmonyMethod(typeof(MatReuse), nameof(Track)));
            h.Patch(set, postfix: new HarmonyMethod(typeof(MatReuse), nameof(Track)));
            h.Patch(make, finalizer: new HarmonyMethod(typeof(MatReuse), nameof(Sweep)));
        }

        private static bool Runtime(Material m) { return !ReferenceEquals(m, null) && m != null && m.GetInstanceID() < 0; }

        // 타일 렌더러가 머티리얼을 갖게 된 뒤: 바뀌었으면 이전 것은 버려진 후보
        public static void Track(FloorRenderer __instance)
        {
            if (!Enabled) return;
            var m = __instance.material;
            Material old;
            if (owned.TryGetValue(__instance, out old) && !ReferenceEquals(old, m) && Runtime(old)) dropped.Add(old);
            if (Runtime(m)) owned[__instance] = m;
            else owned.Remove(__instance);
        }

        public static Exception Sweep(Exception __exception)
        {
            if (!Enabled) { owned.Clear(); dropped.Clear(); return __exception; }
            try
            {
                var dead = new List<FloorRenderer>();
                foreach (var kv in owned) if (kv.Key == null) { dead.Add(kv.Key); dropped.Add(kv.Value); }
                foreach (var k in dead) owned.Remove(k);
                if (dropped.Count == 0) return __exception;
                // 지금 타일들이 쓰는 것은 지우지 않는다
                var inUse = new HashSet<Material>(RefEq<Material>.I);
                foreach (var kv in owned) inUse.Add(kv.Value);
                var lm = ADOBase.lm;
                if (lm != null && lm.listFloors != null)
                    foreach (var f in lm.listFloors)
                    {
                        if (f == null || f.floorRenderer == null) continue;
                        if (f.floorRenderer.material != null) inUse.Add(f.floorRenderer.material);
                        var r = f.floorRenderer.renderer;
                        if (r != null) foreach (var m in r.sharedMaterials) if (m != null) inUse.Add(m);
                    }
                var seen = new HashSet<Material>(RefEq<Material>.I);
                int n = 0;
                foreach (var m in dropped)
                {
                    if (!seen.Add(m) || m == null || inUse.Contains(m)) continue;
                    UnityEngine.Object.Destroy(m);
                    n++;
                }
                Freed += n;
                if (n > 0 && (Edition.Dev || n > 1000)) Main.Entry.Logger.Log("[타일 머티리얼] 버려진 타일 머티리얼 " + n + "개 풀어 줌");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[타일 머티리얼] 실패: " + ex.Message); }
            finally { dropped.Clear(); }
            return __exception;
        }
    }
}
