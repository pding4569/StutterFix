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
