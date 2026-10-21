using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

namespace StutterFix
{
    // (자동 시험 decoparent off|root|begin|end|restore|state) 진단 전용, 일반 Player 에 없음.
    // 가설(Gemini 제안): 장식 28,828개(Transform 약 11.5만 개)가 scrDecorationManager.transform 한 부모 밑에 있어 엔진의 Transform 계층 처리 비용이 크다.
    //   root: 모든 장식 루트를 부모에서 떼어(SetParent(null, true)) 각자 작은 계층으로 만든다 (월드 위치는 같음, 매니저가 원점 항등이면 화면 동일).
    // 두 상태 모두 TightLate 를 끈다(부모 가정이 달라 TightLate 가 빠지면 결과가 섞이므로 기준선도 같은 조건).
    internal static class DecoParent
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly List<Transform> moved = new List<Transform>();
        private static readonly List<int> index = new List<int>();
        private static Transform managerT;
        private static bool tightWas, tightSaved;
        private static string mode = "off";
        private static long t0; private static int f0; private static double song0;
        private static void Log(string s) { Main.Entry.Logger.Log("[decoparent] " + s); }

        private static void Restore()
        {
            if (managerT != null)
                for (int i = 0; i < moved.Count; i++)
                    if (moved[i] != null) { moved[i].SetParent(managerT, true); moved[i].SetSiblingIndex(Math.Min(index[i], managerT.childCount - 1)); }
            Log("restore moved=" + moved.Count);
            moved.Clear(); index.Clear();
        }
        private static void SaveTight() { if (!tightSaved) { tightWas = TightLate.Enabled; tightSaved = true; } TightLate.Enabled = false; TightLate.Forget(); }
        internal static void Shutdown() { try { Restore(); } catch { } if (tightSaved) { TightLate.Enabled = tightWas; tightSaved = false; } mode = "off"; }
        internal static void Command(string arg)
        {
            arg = (arg ?? "").Trim().ToLowerInvariant();
            switch (arg)
            {
                case "off": Restore(); SaveTight(); mode = "off"; Log("mode=off tightlate=off"); return;
                case "root":
                    {
                        var mgr = scrDecorationManager.instance; if (mgr == null) throw new InvalidOperationException("no manager");
                        if (moved.Count != 0) Restore();
                        SaveTight(); managerT = mgr.transform;
                        var sw = Stopwatch.StartNew();
                        var kids = new List<Transform>(managerT.childCount);
                        foreach (Transform c in managerT) kids.Add(c);
                        for (int i = 0; i < kids.Count; i++) { moved.Add(kids[i]); index.Add(kids[i].GetSiblingIndex()); }
                        for (int i = 0; i < kids.Count; i++) kids[i].SetParent(null, true);
                        mode = "root"; Log("mode=root moved=" + kids.Count + " ms=" + sw.Elapsed.TotalMilliseconds.ToString("F1", Inv) + " manager_identity=" + (managerT.position == Vector3.zero && managerT.rotation == Quaternion.identity && managerT.lossyScale == Vector3.one));
                        return;
                    }
                case "begin": f0 = Time.frameCount; t0 = Stopwatch.GetTimestamp(); song0 = scrConductor.instance != null ? scrConductor.instance.songposition_minusi : -1; return;
                case "end":
                    {
                        double sec = (Stopwatch.GetTimestamp() - t0) / (double)Stopwatch.Frequency; int frames = Time.frameCount - f0;
                        double song1 = scrConductor.instance != null ? scrConductor.instance.songposition_minusi : -1;
                        Log(string.Format(Inv, "RESULT mode={0} fps={1:F3} ms={2:F6} frames={3} seconds={4:F3} song_begin={5:F3} song_end={6:F3} valid={7} tightlate={8}", mode, frames / sec, 1000 * sec / Math.Max(1, frames), frames, sec, song0, song1, song1 >= 276 && song0 >= 261.5 && song0 < 264, TightLate.Enabled));
                        return;
                    }
                case "restore": Restore(); return;
                default: Log("mode=" + mode + " moved=" + moved.Count + " tightlate=" + TightLate.Enabled); return;
            }
        }
    }
}
