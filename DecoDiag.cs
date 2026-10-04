using System;
using System.IO;
using HarmonyLib;

namespace StutterFix
{
    // (개발자용, 모드 폴더에 decodiag.txt 가 있을 때만) 게임의 scnGame.UpdateDecorationObjects 본문을 그대로 따라 하며
    // 어느 줄에서 예외가 나는지 적는다. 2026-10-04 ALPHA WYSI EX: 맵 열기 때 이 함수 안에서 null 예외가 나 맵 열기가 멈췄는데,
    // 유니티 스택에는 패치된 함수 하나만 찍혀 자리를 알 수 없었다. 원래 함수 대신 돈다(우선순위 맨 뒤 앞부분: 이미지 미리 풀기 Begin 다음).
    internal static class DecoDiag
    {
        internal static void Install(Harmony h)
        {
            if (!Edition.Dev || !File.Exists(Path.Combine(Main.Entry.Path, "decodiag.txt"))) return;
            var m = AccessTools.Method(typeof(scnGame), "UpdateDecorationObjects", new[] { typeof(bool) });
            if (m == null) return;
            h.Patch(m, prefix: new HarmonyMethod(typeof(DecoDiag), nameof(Prefix)) { priority = Priority.Last });
            Main.Entry.Logger.Log("[장식 불러오기 진단] 설치");
        }

        private static void Log(string s) { Main.Entry.Logger.Log("[장식 불러오기 진단] " + s); }

        public static bool Prefix(scnGame __instance, bool reloadDecorations)
        {
            var g = __instance;
            string step = "시작";
            int i = -1;
            try
            {
                step = "화질 확인";
                if (ADOBase.controller.visualQuality == VisualQuality.Low && (ADOBase.isOfficialLevel || Persistence.forceVisualSettings) && !ADOBase.levelIsMikoSkip) return false;
                step = "ClearDecorations";
                g.decManager.ClearDecorations();
                i = 0;
                foreach (ADOFAI.LevelEvent decoration in g.decorations)
                {
                    step = "장식 " + i + " active";
                    if (!decoration.active) { i++; continue; }
                    bool spritesLoaded = false;
                    if (reloadDecorations)
                    {
                        step = "장식 " + i + " CreateDecoration (" + SafeImg(decoration) + ")";
                        g.decManager.CreateDecoration(decoration, out spritesLoaded);
                    }
                    else
                    {
                        string output;
                        step = "장식 " + i + " TryGet";
                        if (decoration.TryGet<string>("decorationImage", out output))
                        {
                            bool flag = output.StartsWith("prefab:", StringComparison.CurrentCultureIgnoreCase);
                            if (!string.IsNullOrEmpty(output) && !flag && !ADOBase.IsNotAMikoSkipMandatorySprite(output))
                            {
                                step = "장식 " + i + " GetOrAddSprite " + output;
                                string filePath = Path.Combine(Path.GetDirectoryName(g.levelPath), output);
                                ADOFAI.LoadResult status;
                                g.imgHolder.GetOrAddSprite(output, filePath, out status);
                                step = "장식 " + i + " UpdateImageLoadResult " + output;
                                if (ADOBase.editor != null) ADOBase.editor.UpdateImageLoadResult(output, status);
                            }
                        }
                    }
                    i++;
                }
                Log("장식 " + i + "개 끝");
                i = 0;
                int n = 0;
                foreach (ADOFAI.LevelEvent ev in g.events)
                {
                    if (ev.eventType == ADOFAI.LevelEventType.MoveDecorations)
                    {
                        string output2 = null;
                        step = "장식 이동 " + i + " (타일 " + ev.floor + ") TryGetAndSet";
                        if (ev.TryGetAndSet("decorationImage", ref output2, onlyIfEnabled: true) && !string.IsNullOrEmpty(output2))
                        {
                            step = "장식 이동 " + i + " (타일 " + ev.floor + ") GetOrAddSprite " + output2;
                            string filePath2 = Path.Combine(Path.GetDirectoryName(g.levelPath), output2);
                            ADOFAI.LoadResult status2;
                            g.imgHolder.GetOrAddSprite(output2, filePath2, out status2);
                            step = "장식 이동 " + i + " (타일 " + ev.floor + ") UpdateImageLoadResult " + output2 + " " + status2;
                            if (ADOBase.editor != null) ADOBase.editor.UpdateImageLoadResult(output2, status2);
                            n++;
                        }
                    }
                    i++;
                }
                Log("이벤트 " + i + "개(이미지 있는 장식 이동 " + n + "개) 끝, 예외 없음");
            }
            catch (Exception ex)
            {
                Log("예외 자리: " + step + " -> " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
                throw;
            }
            return false;
        }

        private static string SafeImg(ADOFAI.LevelEvent e)
        {
            try { string o; return e.TryGet<string>("decorationImage", out o) ? o : "-"; } catch { return "?"; }
        }
    }
}
