using System;
using System.Collections.Generic;
using System.Text;

namespace StutterFix
{
    // 게임의 JSON 해석기(Json.Deserialize)와 같은 결과를 내는 빠른 해석기. 설명과 규칙은 FastJson.cs. 유니티·Harmony 를 쓰지 않는다(검증 도구가 같은 파일을 쓴다).
    internal static class FastJsonParser
    {
        // 원래 해석기는 배열 값 자리에 ':' 가 오면 그 글자를 읽지 않고 null 을 계속 넣어 끝나지 않는다(원래 게임도 같음, 여기도 같게 둠).
        // 검증 도구는 한도를 두어 그런 입력을 알아본다. 게임에서는 int.MaxValue(원래와 같이 메모리가 찰 때까지).
        internal static int ArrayLimit = int.MaxValue;
        internal const string HangMessage = "FastJsonParser: array limit";

        // Json.Deserialize(json) 와 같다 (json 은 null 아님)
        internal static object Parse(string json) { return new P(json).ParseValue(); }

        private sealed class P
        {
            private readonly string s; private readonly int len; private int pos;
            internal P(string json) { s = json; len = json.Length; if (len > 0 && s[0] == (char)0xFEFF) pos = 1; }

            // StringReader.Peek / Read 와 같은 값. 글자로 바꿀 때 끝(-1)이면 Convert.ToChar(-1) 과 같은 예외.
            private char PeekChar { get { if (pos >= len) return Convert.ToChar(-1); return s[pos]; } }
            private char NextChar { get { if (pos >= len) return Convert.ToChar(-1); return s[pos++]; } }
            private bool AtEnd { get { return pos >= len; } }
            private void Read() { if (pos < len) pos++; }

            // 공백 " \t\n\r" + U+FEFF, 낱말 끝 " \t\n\r{}[],:\"" (게임 해석기의 문자열 상수와 같음. 원래 MiniJSON 에 U+FEFF 를 더한 것)
            private static bool IsWs(char c) { return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == (char)0xFEFF; }
            private static bool IsBreak(char c) { return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '{' || c == '}' || c == '[' || c == ']' || c == ',' || c == ':' || c == '"'; }

            private void EatWhitespace() { while (IsWs(PeekChar)) { Read(); if (AtEnd) break; } }

            private string NextWord()
            {
                int start = pos;
                while (!IsBreak(PeekChar)) { pos++; if (AtEnd) break; }
                return s.Substring(start, pos - start);
            }

            // 0 없음, 1 {, 2 }, 3 [, 4 ], 5 :, 6 ',', 7 문자열, 8 숫자, 9 true, 10 false, 11 null
            private int NextToken()
            {
                EatWhitespace();
                if (AtEnd) return 0;
                char c = PeekChar;
                switch (c)
                {
                    case '{': return 1;
                    case '}': Read(); return 2;
                    case '[': return 3;
                    case ']': Read(); return 4;
                    case ',': Read(); return 6;
                    case '"': return 7;
                    case ':': return 5;
                    case '-': case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9': return 8;
                }
                string w = NextWord();
                if (w == "false") return 10;
                if (w == "true") return 9;
                if (w == "null") return 11;
                return 0;
            }

            internal object ParseValue() { return ParseByToken(NextToken()); }

            private object ParseByToken(int t)
            {
                switch (t)
                {
                    case 7: return ParseString();
                    case 8: return ParseNumber();
                    case 1: return ParseObject();
                    case 3: return ParseArray();
                    case 9: return true;
                    case 10: return false;
                    default: return null;
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                Read();   // '{'
                while (true)
                {
                    int t = NextToken();
                    if (t == 0) return null;
                    if (t == 2) return table;
                    if (t == 6) continue;
                    string name = ParseString();
                    if (NextToken() != 5) return null;
                    Read();   // ':'
                    table[name] = ParseValue();
                }
            }

            private List<object> ParseArray()
            {
                var array = new List<object>();
                Read();   // '['
                while (true)
                {
                    int t = NextToken();
                    if (t == 0) return null;
                    if (t == 4) return array;
                    if (t == 6) continue;
                    array.Add(ParseByToken(t));
                    if (array.Count > ArrayLimit) throw new InvalidOperationException(HangMessage);   // 검증 도구용 (기본은 한도 없음)
                }
            }

            private string ParseString()
            {
                Read();   // 여는 따옴표 자리(무슨 글자든 하나 건너뜀)
                // 이스케이프가 없으면 한 번에 잘라 낸다
                int start = pos, i = pos;
                while (i < len) { char c = s[i]; if (c == '"' || c == '\\') break; i++; }
                if (i >= len) { pos = len; return s.Substring(start, len - start); }   // 닫는 따옴표 없이 끝: 끝까지
                if (s[i] == '"') { pos = i + 1; return s.Substring(start, i - start); }
                var sb = new StringBuilder(s, start, i - start, Math.Max(16, (i - start) * 2));
                pos = i;
                while (true)
                {
                    if (AtEnd) break;
                    char c = NextChar;
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) break;
                    c = NextChar;
                    switch (c)
                    {
                        case '"': case '\\': case '/': sb.Append(c); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            {
                                var hex = new StringBuilder();
                                for (int k = 0; k < 4; k++) hex.Append(NextChar);
                                sb.Append((char)(ushort)Convert.ToInt32(hex.ToString(), 16));
                                break;
                            }
                    }
                }
                return sb.ToString();
            }

            private object ParseNumber()
            {
                string number = NextWord();
                if (number.IndexOf('.') == -1)
                {
                    int parsedInt;
                    int.TryParse(number, out parsedInt);
                    return parsedInt;
                }
                float parsedFloat;
                float.TryParse(number, out parsedFloat);
                return parsedFloat;
            }
        }
    }
}
