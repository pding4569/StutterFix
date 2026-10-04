using System;
using System.Threading;
using UnityEngine;

namespace StutterFix
{
    // 설정 창의 3D 물체: 얼불춤의 두 행성을 흑백으로 (흰 진주 행성, 검은 흑요석 행성). 서로의 둘레를 돌며 스스로도 돈다.
    // 장면에 물체나 카메라를 두지 않는다(게임 카메라가 볼 수 없게). 각 행성의 자전 장면 Frames 장을 다른 스레드에서 한 번 그려
    // (구 위의 점마다 표면 무늬 + 위 왼쪽에서 드는 빛 + 반사광 + 테두리 빛) 한 장의 텍스처에 모아 두고, 그릴 때는 그 칸만 고른다.
    // 공전은 기울어진 타원이라 뒤로 가는 행성은 작아지고 앞 행성에 가린다(원근). 그래서 매 프레임 비용은 그림 두 장뿐이다.
    internal static class UiOrbit
    {
        private const int N = 88, Frames = 72, Cols = 12, Rows = 6;   // 칸 하나 88px, 자전 72장
        private static Texture2D pearl, obsidian;
        private static Color32[] pearlPx, obsidianPx;
        private static volatile int state;   // 0 아직, 1 그리는 중, 2 다 그림(올리기 전), 3 준비됨

        internal static bool Ready { get { if (state == 2) Upload(); return state == 3; } }

        internal static void Prepare()
        {
            if (state != 0) return;
            state = 1;
            var th = new Thread(() =>
            {
                try
                {
                    pearlPx = Bake(false);
                    obsidianPx = Bake(true);
                    state = 2;
                }
                catch { state = 0; }
            }) { IsBackground = true, Name = "SF orbit bake", Priority = System.Threading.ThreadPriority.BelowNormal };
            th.Start();
        }

        private static void Upload()
        {
            pearl = Make(pearlPx); obsidian = Make(obsidianPx);
            pearlPx = obsidianPx = null;
            state = 3;
        }

        private static Texture2D Make(Color32[] px)
        {
            var t = new Texture2D(N * Cols, N * Rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        internal static void Destroy()
        {
            if (pearl != null) UnityEngine.Object.Destroy(pearl);
            if (obsidian != null) UnityEngine.Object.Destroy(obsidian);
            pearl = obsidian = null;
            if (state == 3) state = 0;
        }

        // ── 그리기 ──
        // area 가운데를 중심으로 두 행성이 서로 반대편에서 돈다. size 는 앞에 왔을 때의 지름.
        internal static void Draw(Rect area, float size, float alpha)
        {
            if (!Ready || Event.current.type != EventType.Repaint) return;
            float t = Time.unscaledTime;
            float orbit = t * 0.55f;                  // 공전 (라디안/초)
            var c = area.center;
            float rx = area.width * 0.5f - size * 0.62f, ry = rx * 0.30f;
            var oc = GUI.color;
            // 궤도: 아주 옅은 타원 (점 몇 개로)
            GUI.color = new Color(1f, 1f, 1f, 0.07f * alpha * oc.a);
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2f / 64f;
                GUI.DrawTexture(new Rect(c.x + Mathf.Cos(a) * rx - 0.75f, c.y + Mathf.Sin(a) * ry - 0.75f, 1.5f, 1.5f), Texture2D.whiteTexture);
            }
            // 두 행성: 뒤(z<0)부터 그린다
            float za = Mathf.Sin(orbit), zb = -za;
            if (za < zb) { One(obsidian, orbit, c, rx, ry, size, t, 1.0f, alpha, oc); One(pearl, orbit + Mathf.PI, c, rx, ry, size, t, 0.8f, alpha, oc); }
            else { One(pearl, orbit + Mathf.PI, c, rx, ry, size, t, 0.8f, alpha, oc); One(obsidian, orbit, c, rx, ry, size, t, 1.0f, alpha, oc); }
            GUI.color = oc;
        }

        private static void One(Texture2D tex, float ang, Vector2 c, float rx, float ry, float size, float t, float spin, float alpha, Color oc)
        {
            float z = Mathf.Sin(ang);                 // 1 이 앞
            float k = 0.78f + 0.22f * (z + 1f) * 0.5f; // 뒤로 가면 작아진다
            float d = size * k;
            float x = c.x + Mathf.Cos(ang) * rx, y = c.y + Mathf.Sin(ang) * ry;
            int f = ((int)(t * spin * Frames / 9f) % Frames + Frames) % Frames;   // 9초에 한 바퀴 자전 (흰 행성은 조금 느리게)
            var uv = new Rect((f % Cols) / (float)Cols, 1f - (f / Cols + 1) / (float)Rows, 1f / Cols, 1f / Rows);
            float dim = 0.55f + 0.45f * (z + 1f) * 0.5f;   // 뒤로 가면 조금 어둡게 (깊이)
            GUI.color = new Color(dim, dim, dim, alpha * oc.a);
            GUI.DrawTextureWithTexCoords(new Rect(x - d / 2f, y - d / 2f, d, d), tex, uv);
        }

        // ── 미리 그리기 (다른 스레드) ──
        private static Color32[] Bake(bool dark)
        {
            var px = new Color32[N * Cols * N * Rows];
            // 빛: 위 왼쪽 앞에서 (화면 좌표: x 오른쪽, y 아래, z 보는 쪽)
            Vector3 L = new Vector3(-0.55f, -0.62f, 0.56f).normalized;
            Vector3 H = (L + new Vector3(0, 0, 1)).normalized;
            float tilt = 0.38f;   // 자전축 기울기
            float ct = Mathf.Cos(tilt), st = Mathf.Sin(tilt);
            for (int f = 0; f < Frames; f++)
            {
                float rot = f * Mathf.PI * 2f / Frames;
                float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
                int ox = (f % Cols) * N, oy = (Rows - 1 - f / Cols) * N;   // 텍스처는 아래가 0
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        // 칸 안에서 구는 지름 N/2 가 아니라 거의 가득 (가장자리 1px 여유)
                        float sx = (x + 0.5f) / N * 2f - 1f, sy = (y + 0.5f) / N * 2f - 1f;   // 텍스처 줄은 아래가 0 이라 sy 는 위가 +
                        float rr = (sx * sx + sy * sy) * 1.04f;
                        int idx = (oy + y) * (N * Cols) + ox + x;
                        if (rr >= 1f) { px[idx] = new Color32(0, 0, 0, 0); continue; }
                        float sz = Mathf.Sqrt(1f - rr);
                        // 화면 법선 (y 아래 방향 기준으로 바꿈)
                        Vector3 n = new Vector3(sx, -sy, sz);
                        // 물체 좌표: 기울기(x 축) 뒤 자전(y 축)
                        float ly = n.y * ct - n.z * st, lz = n.y * st + n.z * ct;
                        float lx = n.x * cr + lz * sr; lz = -n.x * sr + lz * cr;
                        float surf = Fbm(lx * 2.1f + 7.3f, ly * 2.1f, lz * 2.1f);          // 0~1 표면 무늬
                        float band = Mathf.Sin(ly * 9f + surf * 3f) * 0.5f + 0.5f;         // 가로 결
                        float diff = Mathf.Max(0f, Vector3.Dot(n, L));
                        float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, H)), dark ? 46f : 22f);
                        float rim = Mathf.Pow(1f - sz, 3f);
                        float c;
                        if (dark)
                        {
                            float alb = 0.09f + 0.07f * surf + 0.03f * band;
                            c = alb * (0.14f + 0.86f * diff) + spec * 0.8f + rim * 0.42f;   // 검은 바탕에서도 윤곽이 보이게 뒤에서 드는 빛
                        }
                        else
                        {
                            float alb = 0.72f + 0.24f * surf - 0.08f * band;
                            c = alb * (0.16f + 0.84f * diff) + spec * 0.25f + rim * 0.06f;
                        }
                        c = Mathf.Clamp01(c);
                        // 가장자리 부드럽게
                        float edge = Mathf.Clamp01((1f - rr) * N * 0.25f);
                        byte v = (byte)(c * 255f);
                        px[idx] = new Color32(v, v, (byte)Mathf.Min(255, v + (dark ? 2 : 0)), (byte)(edge * 255f));
                    }
            }
            return px;
        }

        // 값 잡음 (3D, 3겹)
        private static float Fbm(float x, float y, float z)
        {
            float a = 0f, amp = 0.55f, sum = 0f;
            for (int o = 0; o < 3; o++) { a += Noise(x, y, z) * amp; sum += amp; x *= 2.03f; y *= 2.03f; z *= 2.03f; amp *= 0.5f; }
            return a / sum;
        }
        private static float Noise(float x, float y, float z)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
            float xf = x - xi, yf = y - yi, zf = z - zi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf), w = zf * zf * (3 - 2 * zf);
            float c000 = H3(xi, yi, zi), c100 = H3(xi + 1, yi, zi), c010 = H3(xi, yi + 1, zi), c110 = H3(xi + 1, yi + 1, zi);
            float c001 = H3(xi, yi, zi + 1), c101 = H3(xi + 1, yi, zi + 1), c011 = H3(xi, yi + 1, zi + 1), c111 = H3(xi + 1, yi + 1, zi + 1);
            float x00 = c000 + (c100 - c000) * u, x10 = c010 + (c110 - c010) * u, x01 = c001 + (c101 - c001) * u, x11 = c011 + (c111 - c011) * u;
            float y0 = x00 + (x10 - x00) * v, y1 = x01 + (x11 - x01) * v;
            return y0 + (y1 - y0) * w;
        }
        private static float H3(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1442695040);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }
    }
}
