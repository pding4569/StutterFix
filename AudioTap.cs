#if AUTOTEST
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace StutterFix
{
    // 시험 전용(AutoTest 빌드에만 들어감): AudioListener 의 OnAudioFilterRead 로 게임 소리 출력을 그대로 받아 파일로 쓴다.
    //   audiotap begin <초> [mute]  : 녹음 시작(mute 면 곡 음량 0, 히트음만 남김). 끝나면 audiotap end <이름> 으로 저장.
    //   audiotap end <이름>         : 모드 폴더 audiotap-<이름>.f32(float32 인터리브) + .json(시작 dsp 시각·채널·샘플레이트·히트음 묶기 정보)
    // 히트음 묶기의 구간 경계가 소리에 남기는 '틱'(경계 불연속)을 귀 없이 찾기 위한 것. 성능 판에서는 쓰지 않는다.
    internal sealed class AudioTapBehaviour : MonoBehaviour
    {
        internal float[] Buf;
        internal volatile int Len;
        internal double T0 = -1;
        internal int Channels;
        internal volatile bool On;
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!On) return;
            if (T0 < 0) { T0 = AudioSettings.dspTime; Channels = channels; }
            int n = data.Length, l = Len;
            if (l + n > Buf.Length) { On = false; return; }
            Array.Copy(data, 0, Buf, l, n);
            Len = l + n;
        }
    }

    internal static class AudioTap
    {
        private static AudioTapBehaviour tap;
        private static GameObject tapObject;
        private static AudioListener oldListener;
        private static float songVolume = -1;

        internal static string Command(string arg)
        {
            var a = (arg ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length >= 1 && a[0] == "begin")
            {
                if (tapObject != null) { UnityEngine.Object.Destroy(tapObject); if (oldListener != null) oldListener.enabled = true; }
                AudioListener listener = null; var sbl = new StringBuilder();
                foreach (var l in UnityEngine.Object.FindObjectsOfType<AudioListener>())
                {
                    sbl.Append(l.gameObject.name).Append(l.isActiveAndEnabled ? "(켜짐) " : "(꺼짐) ");
                    if (listener == null && l.isActiveAndEnabled) listener = l;
                }
                if (listener == null) throw new InvalidOperationException("켜진 AudioListener 없음: " + sbl);
                Lg.I("[audiotap] 리스너 목록 " + sbl);
                double sec = a.Length >= 2 ? double.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture) : 20;
                int rate = AudioSettings.outputSampleRate;
                // 게임 리스너 오브젝트에는 AudioSource 가 이미 있어 OnAudioFilterRead 를 둘 다에 걸 수 없다 → 새 오브젝트에 리스너를 만들고 옛 리스너는 잠시 끈다.
                tapObject = new GameObject("StutterFix AudioTap");
                UnityEngine.Object.DontDestroyOnLoad(tapObject);
                oldListener = listener; oldListener.enabled = false;
                tapObject.AddComponent<AudioListener>();
                tap = tapObject.AddComponent<AudioTapBehaviour>();
                tap.Buf = new float[(int)(sec * rate * 2) + 65536];
                tap.Len = 0; tap.T0 = -1; tap.On = true;
                bool mute = a.Length >= 3 && a[2] == "mute";
                var song = scrConductor.instance != null ? scrConductor.instance.song : null;
                if (mute && song != null) { songVolume = song.volume; song.volume = 0f; } else songVolume = -1;
                return "시작 " + sec + "초 rate=" + rate + " mute=" + mute + " listener=" + listener.gameObject.name;
            }
            if (a.Length >= 2 && a[0] == "end")
            {
                if (tap == null) throw new InvalidOperationException("녹음 중이 아님");
                tap.On = false;
                var song = scrConductor.instance != null ? scrConductor.instance.song : null;
                if (songVolume >= 0 && song != null) song.volume = songVolume;
                songVolume = -1;
                string dir = Main.Entry.Path;
                string raw = Path.Combine(dir, "audiotap-" + a[1] + ".f32");
                int len = tap.Len;
                var bytes = new byte[len * 4];
                Buffer.BlockCopy(tap.Buf, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(raw, bytes);
                var sb = new StringBuilder("{");
                sb.Append("\"t0\":").Append(tap.T0.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"channels\":").Append(tap.Channels);
                sb.Append(",\"rate\":").Append(AudioSettings.outputSampleRate);
                sb.Append(",\"samples\":").Append(len / Math.Max(1, tap.Channels));
                sb.Append(Session());
                sb.Append("}");
                File.WriteAllText(Path.Combine(dir, "audiotap-" + a[1] + ".json"), sb.ToString());
                UnityEngine.Object.Destroy(tapObject); tapObject = null; tap = null;
                if (oldListener != null) oldListener.enabled = true; oldListener = null;
                return "저장 " + raw + " 샘플 " + len / Math.Max(1, 2) + " (히트음 묶기 정보 " + (Session().Length > 0 ? "있음" : "없음") + ")";
            }
            throw new ArgumentException("audiotap begin <초> [mute] | audiotap end <이름>");
        }

        // HitSoundBatch 의 지금 세션(비공개)에서 구간 계산에 필요한 값을 리플렉션으로 읽는다(새·옛 빌드 모두 같은 코드로 읽으려고).
        private static string Session()
        {
            try
            {
                var f = typeof(HitSoundBatch).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
                var s = f != null ? f.GetValue(null) : null;
                if (s == null) return "";
                var t = s.GetType();
                double origin = (double)t.GetField("Origin", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s);
                int rate = (int)t.GetField("Rate", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s);
                int frames = (int)t.GetField("Frames", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s);
                int segments = (int)t.GetField("Segments", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s);
                var mix = typeof(HitSoundBatch).Assembly.GetType("StutterFix.HitSoundMix");
                var lf = mix != null ? mix.GetField("LeadFrames", BindingFlags.NonPublic | BindingFlags.Static) : null;
                int lead = lf != null ? (int)lf.GetValue(null) : 0;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                return ",\"hit\":{\"origin\":" + origin.ToString("R", inv) + ",\"rate\":" + rate + ",\"frames\":" + frames + ",\"segments\":" + segments + ",\"lead\":" + lead + "}";
            }
            catch { return ""; }
        }
    }
}
#endif
