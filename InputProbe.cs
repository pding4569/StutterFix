using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (자동 시험 inputprobe begin|end|state, nofail on|off, asyncinput on|off) 키씹 조사용, 기본 꺼짐.
    //   hook_down      = SkyHook 훅 스레드가 받은 키 누름 수 (게임 SkyHookManager.KeyUpdated 에 우리 리스너를 더함)
    //   processed_down = scrController.ProcessKeyInputs 가 처리한 새 누름 수 (그 시점의 keyDownMask.Count 합)
    //   외부에서 SendInput 으로 넣은 수와 비교하면 훅 전달 단계/게임 처리 단계 어디서 줄었는지 갈린다.
    internal static class InputProbe
    {
        private static bool installed, active;
        private static long hookDown, hookUp, processedDown, processCalls, frames0, maxFrameTicks, lastFrameTs;
        private static int frameAt;
        private static long t0;
        private const int Cap = 40000;
        private static readonly long[] hTick = new long[Cap]; private static readonly ushort[] hKey = new ushort[Cap]; private static readonly bool[] hDown = new bool[Cap];
        private static int hN;
        private static readonly long[] pTick = new long[Cap]; private static readonly int[] pCount = new int[Cap]; private static readonly ushort[] pKeys = new ushort[Cap * 2];
        private static int pN; private static long unfocusedFrames, inactiveFrames;

        private sealed class Handler<T>
        {
            private static readonly FieldInfo typeF = typeof(T).GetField("Type");
            private static readonly FieldInfo labelF = typeof(T).GetField("Label");
            private static readonly FieldInfo keyF = typeof(T).GetField("Key");
            private static readonly MethodInfo tickM = typeof(T).GetMethod("GetTimeInTicks");
            public void On(T ev)
            {
                if (!active) return;
                object boxed = ev;
                string t = typeF != null ? typeF.GetValue(boxed).ToString() : "";
                string l = labelF != null ? labelF.GetValue(boxed).ToString() : "";
                if (l.StartsWith("Mouse")) return;
                if (t == "KeyPressed") Interlocked.Increment(ref hookDown); else Interlocked.Increment(ref hookUp);
                int i = Interlocked.Increment(ref hN) - 1;
                if (i < Cap) { hTick[i] = (long)tickM.Invoke(boxed, null); hKey[i] = (ushort)keyF.GetValue(boxed); hDown[i] = t == "KeyPressed"; }
            }
        }

        private static string Install()
        {
            if (installed) return "이미 설치됨";
            installed = true;
            var mgr = AccessTools.TypeByName("SkyHook.SkyHookManager");
            if (mgr == null) return "SkyHookManager 없음";
            var evType = AccessTools.TypeByName("SkyHook.SkyHookEvent");
            var ue = mgr.GetField("KeyUpdated", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var ueType = ue.GetType();
            var handlerType = typeof(Handler<>).MakeGenericType(evType);
            var handler = Activator.CreateInstance(handlerType);
            var actionType = typeof(UnityEngine.Events.UnityAction<>).MakeGenericType(evType);
            var del = Delegate.CreateDelegate(actionType, handler, handlerType.GetMethod("On"));
            ueType.GetMethod("AddListener").Invoke(ue, new object[] { del });
            var h = new Harmony("StutterFix.inputprobe");
            h.Patch(AccessTools.Method(typeof(scrController), "ProcessKeyInputs"), prefix: new HarmonyMethod(typeof(InputProbe), nameof(ProcessPre)));
            h.Patch(AccessTools.Method(typeof(scrController), "UpdateInput"), prefix: new HarmonyMethod(typeof(InputProbe), nameof(UpdatePre)));
            return "설치됨";
        }

        public static void ProcessPre(long eventTick)
        {
            if (!active) return;
            processCalls++;
            int c = AsyncInputManager.keyDownMask.Count;
            processedDown += c;
            if (c > 0 && pN < Cap)
            {
                pTick[pN] = eventTick; pCount[pN] = c; int k = 0;
                foreach (var kc in AsyncInputManager.keyDownMask) { if (k < 2) pKeys[pN * 2 + k] = kc.key; k++; }
                pN++;
            }
        }

        public static void UpdatePre()
        {
            if (!active || frameAt == Time.frameCount) return;
            frameAt = Time.frameCount;
            if (!Application.isFocused) unfocusedFrames++;
            if (!RDInput.asyncKeyboard.isActive) inactiveFrames++;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (lastFrameTs != 0) { long d = now - lastFrameTs; if (d > maxFrameTicks) maxFrameTicks = d; }
            lastFrameTs = now;
        }

        // 훅이 받은 누름 하나마다, 같은 틱에 ProcessKeyInputs 가 그 키를 처리했는지 찾아 안 처리된 것을 갈래별로 센다
        private static string Classify()
        {
            int n = Math.Min(hN, Cap), m = Math.Min(pN, Cap);
            var processed = new System.Collections.Generic.HashSet<long>();
            var byTick = new System.Collections.Generic.Dictionary<long, int>();
            for (int i = 0; i < m; i++) { processed.Add(pTick[i]); byTick[pTick[i]] = pCount[i]; }
            int dropped = 0, droppedTickSeen = 0, droppedNoTick = 0, sameTickGroup = 0, dropBurst = 0, dropRapid = 0, dropSingle = 0, downs = 0;
            long lastAny = 0; var lastDownByKey = new System.Collections.Generic.Dictionary<ushort, long>(); var lastUpByKey = new System.Collections.Generic.Dictionary<ushort, long>();
            var tickDownCount = new System.Collections.Generic.Dictionary<long, int>();
            for (int i = 0; i < n; i++) if (hDown[i]) { int c; tickDownCount.TryGetValue(hTick[i], out c); tickDownCount[hTick[i]] = c + 1; }
            long minGapAny = long.MaxValue; int sameTick = 0, firstDrop = -1, lastOk = -1;
            for (int i = 0; i < n; i++)
            {
                if (!hDown[i]) { lastUpByKey[hKey[i]] = hTick[i]; continue; }
                downs++;
                long gapAny = lastAny == 0 ? long.MaxValue : hTick[i] - lastAny; lastAny = hTick[i];
                long gapSame; if (!lastDownByKey.TryGetValue(hKey[i], out gapSame)) gapSame = long.MaxValue; else gapSame = hTick[i] - gapSame;
                lastDownByKey[hKey[i]] = hTick[i];
                if (gapAny < minGapAny) minGapAny = gapAny;
                if (tickDownCount[hTick[i]] > 1) sameTick++;
                bool ok = processed.Contains(hTick[i]);
                if (ok) lastOk = i;
                if (!ok)
                {
                    if (firstDrop < 0) firstDrop = i;
                    dropped++; droppedNoTick++;
                    if (gapSame < 100000) dropRapid++; else if (gapAny < 50000) dropBurst++; else dropSingle++;
                }
            }
            long groupsLost = Math.Max(0, downs - (long)processedDown);
            return "[inputprobe] CLASSIFY hook_events=" + n + " downs=" + downs + " no_process_at_tick=" + dropped + " (rapid_same_key_lt10ms=" + dropRapid
                + " near_other_key_lt5ms=" + dropBurst + " single=" + dropSingle + ") downs_sharing_tick=" + sameTick + " min_gap_any_us=" + (minGapAny == long.MaxValue ? -1 : minGapAny / 10)
                + " first_drop_event_index=" + firstDrop + " last_processed_event_index=" + lastOk + " of=" + n + " unfocused_frames=" + unfocusedFrames + " async_inactive_frames=" + inactiveFrames + " processed_ticks=" + m;
        }

        private static string Flags()
        {
            bool nf = GCS.useNoFail, async = false;
            try { async = Persistence.GetChosenAsynchronousInput(); } catch { }
            return "async_setting=" + async + " hook_active=" + AsyncInputManager.isActive + " nofail=" + nf
                + " timer_half=" + (Main.Config != null && Main.Config.TimerHalfMs);
        }

        public static string Command(string arg)
        {
            arg = (arg ?? "").Trim();
            string[] p = arg.Split(' ');
            switch (p[0])
            {
                case "begin":
                    {
                        string r = Install();
                        hookDown = hookUp = processedDown = processCalls = 0; hN = pN = 0; unfocusedFrames = inactiveFrames = 0; maxFrameTicks = 0; lastFrameTs = 0; frameAt = -1;
                        frames0 = Time.frameCount; t0 = System.Diagnostics.Stopwatch.GetTimestamp(); active = true;
                        return "[inputprobe] begin " + r + " " + Flags() + " frame0=" + frames0;
                    }
                case "end":
                    {
                        active = false;
                        double sec = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) / (double)System.Diagnostics.Stopwatch.Frequency;
                        long frames = Time.frameCount - frames0;
                        string cls = Classify();
                        return cls + " | [inputprobe] RESULT hook_down=" + hookDown + " hook_up=" + hookUp + " processed_down=" + processedDown
                            + " process_calls=" + processCalls + " seconds=" + sec.ToString("F2") + " frames=" + frames
                            + " fps=" + (frames / Math.Max(sec, 0.001)).ToString("F1")
                            + " max_frame_ms=" + (maxFrameTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("F2") + " " + Flags();
                    }
                case "nofail":
                    {
                        bool on = p.Length > 1 && p[1] == "on";
                        GCS.useNoFail = on;
                        var c = ADOBase.controller; if (c != null) c.noFail = on;
                        return "[inputprobe] nofail=" + on;
                    }
                case "asyncinput":
                    {
                        bool on = p.Length > 1 && p[1] == "on";
                        Persistence.SetChosenAsynchronousInput(on);
                        AsyncInputManager.ToggleHook(on);
                        return "[inputprobe] asyncinput=" + on + " " + Flags();
                    }
                default: return "[inputprobe] " + Flags() + " active=" + active + " hook_down=" + hookDown + " processed_down=" + processedDown;
            }
        }
    }
}
