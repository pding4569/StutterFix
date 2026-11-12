using System;
using UnityEngine.LowLevel;

namespace StutterFix
{
    // 곡 중에만 엔진의 PreUpdate/SendMouseEvents 단계를 PlayerLoop 에서 뺀다.
    // 이 단계는 카메라마다 화면 좌표 검사로 OnMouseDown 같은 메시지를 보낸다(프레임당 0.04~0.13ms).
    // 곡 중에는 마우스 메시지를 받는 게임 객체가 없다(OnMouse* 는 편집기 기즈모 등에만 있음). 곡이 아니면 그대로 되돌린다.
    internal static class MouseSkip
    {
        internal static bool Enabled;
        static bool removed;
        static PlayerLoopSystem saved;
        static int savedIndex = -1;
        internal static long Toggles;

        internal static void Tick(bool playing)
        {
            if (playing && Enabled) { if (!removed) Remove(); }
            else if (removed) Restore();
        }

        static void Remove()
        {
            try
            {
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                var top = loop.subSystemList;
                for (int i = 0; i < top.Length; i++)
                {
                    if (top[i].type != typeof(UnityEngine.PlayerLoop.PreUpdate)) continue;
                    var subs = top[i].subSystemList;
                    for (int j = 0; j < subs.Length; j++)
                    {
                        if (subs[j].type != typeof(UnityEngine.PlayerLoop.PreUpdate.SendMouseEvents)) continue;
                        saved = subs[j]; savedIndex = j;
                        var keep = new PlayerLoopSystem[subs.Length - 1];
                        Array.Copy(subs, 0, keep, 0, j);
                        Array.Copy(subs, j + 1, keep, j, subs.Length - j - 1);
                        top[i].subSystemList = keep;
                        loop.subSystemList = top;
                        PlayerLoop.SetPlayerLoop(loop);
                        removed = true; Toggles++;
                        Main.Entry.Logger.Log("[마우스단계] 곡 중 SendMouseEvents 뺌");
                        return;
                    }
                }
            }
            catch (Exception e) { Main.Entry.Logger.Log("[마우스단계] 빼기 실패: " + e.Message); Enabled = false; }
        }

        static void Restore()
        {
            try
            {
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                var top = loop.subSystemList;
                for (int i = 0; i < top.Length; i++)
                {
                    if (top[i].type != typeof(UnityEngine.PlayerLoop.PreUpdate)) continue;
                    var subs = top[i].subSystemList;
                    bool has = false;
                    for (int j = 0; j < subs.Length; j++) if (subs[j].type == typeof(UnityEngine.PlayerLoop.PreUpdate.SendMouseEvents)) has = true;
                    if (!has)
                    {
                        int at = Math.Min(Math.Max(savedIndex, 0), subs.Length);
                        var put = new PlayerLoopSystem[subs.Length + 1];
                        Array.Copy(subs, 0, put, 0, at);
                        put[at] = saved;
                        Array.Copy(subs, at, put, at + 1, subs.Length - at);
                        top[i].subSystemList = put;
                        loop.subSystemList = top;
                        PlayerLoop.SetPlayerLoop(loop);
                    }
                }
                removed = false; Toggles++;
                Main.Entry.Logger.Log("[마우스단계] SendMouseEvents 되돌림");
            }
            catch (Exception e) { Main.Entry.Logger.Log("[마우스단계] 되돌리기 실패: " + e.Message); }
        }

        internal static void Shutdown() { if (removed) Restore(); Enabled = false; }
    }
}
