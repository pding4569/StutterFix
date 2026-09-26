using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StutterFix
{
    // 실행 옵션 없이 그래픽 작업 분산(legacy)을 켠다.
    //
    // 측정 결과 (같은 맵, 같은 PC):
    //   D3D12 + native 작업    : 평균 프레임 높음, 곡 중 그래픽 메모리를 새로 잡다가 60~80ms씩 멈춤
    //   D3D11 기본(Threaded)   : 멈춤 없음, 프레임 140
    //   D3D11 + legacy 작업     : 멈춤 없음, 프레임 160, 곡 전체 끊김 150번대 -> 93번
    //
    // 유니티는 boot.config 의 줄을 실행 옵션처럼 읽는다(wait-for-native-debugger 같은 키가 실행 옵션과 같은 이름이다).
    // 그래서 `-force-gfx-jobs legacy` 를 `force-gfx-jobs=legacy` 한 줄로 넣는다.
    //
    // 처음에는 gfx-threading-mode 를 3(ClientWorkerJobs)으로 바꿨는데 엔진이 되돌림 메시지도 없이
    // Threaded 로 떴다. 그 키는 그래픽 작업이 따로 켜져 있을 때만 쓰이는 것으로 보인다. 그 값은 원래대로 둔다.
    //
    // 유니티는 시작할 때만 이 파일을 읽으므로 바꾼 뒤 한 번 재시작해야 적용된다.
    // 원래 파일은 백업해 두고, 설정에서 끄거나 모드를 끄면 원래 내용으로 돌려놓는다.
    // 게임 업데이트나 Steam 파일 검사로 되돌아가도 다음 실행 때 다시 적용한다.
    public static class BootConfig
    {
        private const string JobsKey = "force-gfx-jobs";
        private const string JobsValue = "legacy";
        private const string ModeKey = "gfx-threading-mode";

        internal static string Status = "";

        private static string ConfigPath { get { return Path.Combine(Application.dataPath, "boot.config"); } }
        private static string BackupPath { get { return ConfigPath + ".stutterfix-backup"; } }

        private static int Find(List<string> lines, string key)
        {
            return lines.FindIndex(l => l.StartsWith(key + "=", StringComparison.Ordinal));
        }

        private static string Value(List<string> lines, string key)
        {
            int i = Find(lines, key);
            return i >= 0 ? lines[i].Substring(key.Length + 1).Trim() : null;
        }

        private static bool Set(List<string> lines, string key, string value)
        {
            int i = Find(lines, key);
            string cur = i >= 0 ? lines[i].Substring(key.Length + 1).Trim() : null;
            if (cur == value) return false;
            if (value == null) lines.RemoveAt(i);
            else if (i >= 0) lines[i] = key + "=" + value;
            else lines.Insert(0, key + "=" + value);
            return true;
        }

        // ── 화면 출력 방식 (실험) ──
        // 게임의 boot.config 에는 "force-d3d11-bitblt-model=" 줄이 들어 있어서, D3D11 에서 화면을 옛 방식(BitBlt: 윈도우가 게임 화면을
        // 통째로 복사해 합성)으로 내보낸다(PresentMon: "Composed: Copy with GPU GDI"). 이 줄을 빼면 최신 방식(Flip)이 된다.
        // 측정(2026-09-26, Arche, 같은 PC): 두 번째 판 300 -> 318 FPS, 화면에 나오기까지 약 6.2 -> 4.4ms. 게임 위에 다른 창이 없으면
        // 윈도우가 합성 없이 바로 내보낼 수 있어(Independent Flip) 더 줄 수 있고, 그때는 수직동기가 꺼져 있으면 화면이 찢어져 보일 수 있다.
        // 개발사가 일부러 옛 방식을 골랐을 수 있어 기본은 끔. 끄거나 모드를 끄면 원래 줄을 되살린다.
        private const string BitbltKey = "force-d3d11-bitblt-model";

        // 지금 boot.config 에 옛 방식 줄이 없고 원래(백업)에는 있었으면 최신 방식으로 바꿔 둔 상태다 (설정 처음 읽을 때 지금 상태를 따른다)
        internal static bool FlipNow()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return false;
                bool now = Find(new List<string>(File.ReadAllLines(ConfigPath)), BitbltKey) >= 0;
                bool orig = File.Exists(BackupPath) ? Find(new List<string>(File.ReadAllLines(BackupPath)), BitbltKey) >= 0 : now;
                return orig && !now;
            }
            catch { return false; }
        }

        internal static void Apply(bool enable, bool flip)
        {
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path)) { Status = "boot.config 없음"; return; }

                var lines = new List<string>(File.ReadAllLines(path));
                if ((enable || flip) && !File.Exists(BackupPath)) File.Copy(path, BackupPath);

                // 원래 값은 백업에서 가져온다. 백업이 없으면 바꾼 적이 없다는 뜻이다.
                string originalMode = null, originalBitblt = null;
                if (File.Exists(BackupPath))
                {
                    var orig = new List<string>(File.ReadAllLines(BackupPath));
                    originalMode = Value(orig, ModeKey);
                    originalBitblt = Value(orig, BitbltKey);
                }

                bool changed = false;
                changed |= Set(lines, JobsKey, enable ? JobsValue : null);
                if (originalMode != null) changed |= Set(lines, ModeKey, originalMode);   // 예전 버전이 3으로 바꿔 둔 것을 되돌린다
                if (flip) changed |= Set(lines, BitbltKey, null);                         // 최신 방식: 옛 방식 줄을 뺀다
                else if (File.Exists(BackupPath)) changed |= Set(lines, BitbltKey, originalBitblt);   // 원래대로 (원래 없었으면 없음)

                // 줄 끝은 원래 파일 그대로(게임의 boot.config 는 LF). 예전에는 File.WriteAllLines 가 CRLF 로 바꿔 썼다.
                // (옛 방식 화면 출력의 원인인지 확인했지만 아니었다: LF 로 되돌려도 같은 방식. 원래 모양을 지키려고 둔다.)
                string nl = "\n";
                try { string src = File.ReadAllText(File.Exists(BackupPath) ? BackupPath : path); if (src.Contains("\r\n")) nl = "\r\n"; } catch { }   // 원본(백업)의 줄 끝
                bool repair = false;
                try { repair = nl == "\n" && File.ReadAllText(path).IndexOf('\r') >= 0; } catch { }

                if (changed || repair)
                {
                    File.WriteAllText(path, string.Join(nl, lines.ToArray()) + nl);
                    if (repair && !changed) Main.Entry.Logger.Log("boot.config 줄 끝을 원래대로(LF) 고침 (다음 실행부터 적용)");
                    Main.Entry.Logger.Log("boot.config 수정: " + (enable ? JobsKey + "=" + JobsValue + " 추가" : JobsKey + " 제거") + ", 화면 출력 " + (flip ? "최신(Flip)" : "원래대로") + " (다음 실행부터 적용)");
                    Status = Describe() + " | 다음 실행부터 적용됩니다";
                }
                else Status = Describe();
            }
            catch (Exception ex)
            {
                Status = "boot.config 수정 실패: " + ex.Message;
                Main.Entry.Logger.Error(Status);
            }
        }

        // 지금 실제로 돌고 있는 방식. 실행 옵션이 boot.config 보다 우선한다.
        internal static string Describe()
        {
            return "지금 " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.renderingThreadingMode;
        }
    }
}
