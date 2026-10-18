using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace StutterFix
{
    // (개발자용 자동 시험 tilesig) 지금 맵의 모든 타일의 색·스타일·메시 값을 한 줄씩 파일로 남긴다.
    // 밀린 색 바꾸기(RecolorDefer)를 먼저 모두 적용한 뒤 적는다. 미루기 켠 판과 끈 판의 파일을 비교해 같은지 본다.
    internal static class TileSig
    {
        private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        private static string C(Color c) { return F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a); }
        private static string Tex(Texture t) { return t == null ? "null" : t.name + ":" + t.width + "x" + t.height; }

        internal static string Run(string dir, string name)
        {
            RecolorDefer.FlushAll();
            try { FloorMesh.UpdateAllRequired(); } catch { }
            var list = ADOBase.lm == null ? null : ADOBase.lm.listFloors;
            if (list == null) return "타일 목록 없음";
            var path = Path.Combine(dir, "tilesig-" + name + ".txt");
            int n = 0, meshless = 0;
            ulong all = 14695981039346656037UL;
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    if (f == null) { w.WriteLine(i + "|null"); continue; }
                    var sb = new StringBuilder(256);
                    sb.Append(f.styleNum).Append('|').Append((int)f.specialColorType).Append('|').Append(f.specialColorTypeStart).Append('|')
                      .Append(C(f.specialColor1)).Append('|').Append(C(f.specialColor2)).Append('|').Append(F(f.specialAnimDuration)).Append('|')
                      .Append((int)f.specialColorPulse).Append('|').Append(F(f.specialPulseLength)).Append('|').Append(F(f.specialTimeOffset)).Append('|')
                      .Append(Tex(f.customTexture)).Append('|').Append(F(f.customTextureScale)).Append('|').Append(F(f.opacity));
                    var fr = f.floorRenderer;
                    sb.Append("|c=").Append(fr == null ? "null" : C(fr.color));
                    var fm = fr as FloorMeshRenderer;
                    if (fm == null) { meshless++; sb.Append("|nomesh"); }
                    else
                    {
                        var m = fm.material;
                        var mesh = fm.floorMesh;
                        sb.Append("|mesh=").Append(F(mesh._length)).Append(',').Append(F(mesh._width)).Append(',').Append(mesh._isSprite ? 1 : 0).Append(',').Append(mesh._curvaturePoints)
                          .Append(',').Append(F(mesh._angle0)).Append(',').Append(F(mesh._angle1));
                        if (m != null)
                        {
                            sb.Append("|m=").Append(Tex(m.GetTexture("_MainTex"))).Append(',').Append(Tex(m.GetTexture("_TileTex"))).Append(',').Append(Tex(m.GetTexture("_PerlinTex")))
                              .Append(',').Append(F(m.GetFloat("_TileScale"))).Append(',').Append(F(m.GetFloat("_TextureRatio"))).Append(',').Append(F(m.GetFloat("_ShadowTune")))
                              .Append(',').Append(C(m.GetColor("_ShadowColor"))).Append(',').Append(m.GetInt("_Sprite"));
                        }
                    }
                    string line = sb.ToString();
                    w.Write(i); w.Write('|'); w.WriteLine(line);
                    for (int k = 0; k < line.Length; k++) { all ^= line[k]; all *= 1099511628211UL; }
                    n++;
                }
            }
            return "타일 " + n + "개 (모양 렌더러 아님 " + meshless + "), 전체 해시 " + all.ToString("X16") + ", 파일 " + path;
        }
    }
}
