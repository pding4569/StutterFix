#if DEV || AUTOTEST
using System.Diagnostics;
using UnityEngine;

namespace StutterFix
{
    // 시험 전용: 장식·타일을 큰 부모에서 떼어 Transform 계층을 잘게 나눈다.
    // 게임은 장식 전부를 "Decoration Container" 아래, 타일 전부를 "Floors" 아래에 둔다.
    // 계층 안의 하나만 바뀌어도 엔진이 그 계층 전체를 훑는다고 한다. 곡 구간 FPS 비교용이라 되돌리지 않는다.
    internal static class HierarchyFlat
    {
        internal static void Command(string arg)
        {
            foreach (string name in arg.Split(' '))
            {
                if (name == "deco") Flatten("Decoration Container");
                else if (name == "floor") Flatten("Floors");
                else throw new System.ArgumentException("flatten deco|floor");
            }
        }

        // 빈 배경 카메라를 끄면 카메라당 도는 화면 판정·정렬이 얼마나 주는지 잰다. 비용 비교용이라 그림 동일성은 보지 않는다.
        internal static void SkipCameras(string arg)
        {
            var c = scrCamera.instance;
            Color color = c.Bgcamstatic.backgroundColor;
            c.Bgcamstatic.enabled = false;
            c.BGcam.clearFlags = CameraClearFlags.SolidColor;
            c.BGcam.backgroundColor = color;
            if (arg.Contains("bg"))
            {
                c.BGcam.enabled = false;
                c.camobj.clearFlags = CameraClearFlags.SolidColor;
                c.camobj.backgroundColor = color;
            }
            Main.Entry.Logger.Log("[camskip] " + arg);
        }

        // 빛·반사 프로브·그림자·모션 벡터·가림 판정을 끈 비용 비교. 쓰는 곳이 없어 그림은 같아야 한다.
        internal static void RendererFlags()
        {
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                r.allowOcclusionWhenDynamic = false;
                n++;
            }
            foreach (var c in Camera.allCameras) { c.useOcclusionCulling = false; c.eventMask = 0; }
            Main.Entry.Logger.Log("[rflags] 렌더러 " + n);
        }

        // 키보드 입력은 Unity Input 을 쓰고 Rewired 는 조이스틱에만 쓴다. 곡 중 Rewired 갱신을 끈 비용 비교.
        internal static void RewiredOff()
        {
            int n = 0;
            foreach (var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b.GetType().FullName == "Rewired.InputManager") { b.enabled = false; n++; }
            Main.Entry.Logger.Log("[rewiredoff] 끔 " + n);
        }

        // OnMouse* 는 곡 중 객체에 없다. PreUpdate 의 SendMouseEvents 단계를 뺀 비용 비교.
        internal static void DropMouseEvents()
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            var top = loop.subSystemList;
            int removed = 0;
            for (int i = 0; i < top.Length; i++)
            {
                var subs = top[i].subSystemList;
                if (subs == null) continue;
                var keep = new System.Collections.Generic.List<UnityEngine.LowLevel.PlayerLoopSystem>();
                foreach (var s in subs) { if (s.type != null && s.type.Name == "SendMouseEvents") removed++; else keep.Add(s); }
                top[i].subSystemList = keep.ToArray();
            }
            loop.subSystemList = top;
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
            Main.Entry.Logger.Log("[mousedrop] 뺀 단계 " + removed);
        }

        private static void Flatten(string containerName)
        {
            var container = GameObject.Find(containerName);
            if (container == null) throw new System.InvalidOperationException("없는 부모: " + containerName);
            Transform t = container.transform;
            bool identity = t.position == Vector3.zero && t.rotation == Quaternion.identity && t.lossyScale == Vector3.one;
            long start = Stopwatch.GetTimestamp();
            int count = t.childCount;
            for (int i = count - 1; i >= 0; i--) t.GetChild(i).SetParent(null, true);
            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            Main.Entry.Logger.Log("[flatten] " + containerName + " 자식 " + count + "개 " + ms.ToString("F1") + "ms 부모=기본값:" + identity + " 남은 자식 " + t.childCount);
        }
    }
}
#endif
