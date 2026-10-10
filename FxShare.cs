using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace StutterFix
{
    // 화면 효과 설정 공유: Settings 의 Fx* 필드를 한 줄 코드("SFX1:...")로 내보내고 받는다.
    // LUT 파일 경로(FxLutPath)는 내 PC 경로라 공유하지 않는다. 모르는 필드는 무시해 앞으로 필드가 늘어도 옛 코드를 받을 수 있다.
    // 받은 값은 형식·범위를 확인한 뒤에만 쓴다(NaN·너무 큰 값·모르는 형식은 버림).
    internal static class FxShare
    {
        private const string Prefix = "SFX1:";
        private static readonly string[] Skip = { "FxLutPath" };

        private static List<FieldInfo> Fields()
        {
            var list = new List<FieldInfo>();
            foreach (var f in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!f.Name.StartsWith("Fx", StringComparison.Ordinal) || Array.IndexOf(Skip, f.Name) >= 0) continue;
                var t = f.FieldType;
                if (t == typeof(bool) || t == typeof(int) || t == typeof(float)) list.Add(f);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        internal static string Export(Settings c)
        {
            var sb = new StringBuilder();
            foreach (var f in Fields())
            {
                object v = f.GetValue(c);
                string s = v is bool ? ((bool)v ? "1" : "0") : v is float ? ((float)v).ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture);
                if (sb.Length > 0) sb.Append(';');
                sb.Append(f.Name.Substring(2)).Append('=').Append(s);
            }
            return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        // true 면 c 에 적용했다. 하나라도 못 읽은 값이 있으면 아무것도 바꾸지 않는다.
        internal static bool Import(string code, Settings c, out string message)
        {
            message = "";
            try
            {
                code = (code ?? "").Trim();
                int at = code.IndexOf(Prefix, StringComparison.Ordinal);
                if (at < 0) { message = SettingsWindow.T("공유 코드가 아닙니다. 'SFX1:' 로 시작하는 줄을 복사해 오세요.", "Not a share code; copy a line starting with 'SFX1:'."); return false; }
                code = code.Substring(at + Prefix.Length);
                int end = 0; while (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] == '+' || code[end] == '/' || code[end] == '=')) end++;
                string text = Encoding.UTF8.GetString(Convert.FromBase64String(code.Substring(0, end)));
                var byName = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
                foreach (var f in Fields()) byName[f.Name.Substring(2)] = f;
                var staged = new List<KeyValuePair<FieldInfo, object>>();
                foreach (var part in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    FieldInfo field;
                    if (!byName.TryGetValue(part.Substring(0, eq), out field)) continue;   // 모르는 필드(다른 버전)는 건너뜀
                    string val = part.Substring(eq + 1);
                    object parsed;
                    if (field.FieldType == typeof(bool)) { if (val != "0" && val != "1") throw new FormatException(field.Name); parsed = val == "1"; }
                    else if (field.FieldType == typeof(int)) { int i; if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) || i < 0 || i > 64) throw new FormatException(field.Name); parsed = i; }
                    else
                    {
                        float x; if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out x) || float.IsNaN(x) || float.IsInfinity(x) || x < -1000f || x > 1000f) throw new FormatException(field.Name);
                        parsed = x;
                    }
                    staged.Add(new KeyValuePair<FieldInfo, object>(field, parsed));
                }
                if (staged.Count == 0) { message = SettingsWindow.T("적용할 효과 값이 없습니다.", "The code holds no effect values."); return false; }
                foreach (var kv in staged) kv.Key.SetValue(c, kv.Value);
                message = string.Format(SettingsWindow.T("{0}개 값을 적용했습니다.", "Applied {0} values."), staged.Count);
                return true;
            }
            catch (Exception ex)
            {
                message = SettingsWindow.T("코드를 읽지 못했습니다(깨졌거나 다른 형식): ", "Could not read the code (damaged or another format): ") + ex.Message;
                return false;
            }
        }
    }
}
