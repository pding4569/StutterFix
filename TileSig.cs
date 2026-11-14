using System;
using System.Globalization;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (개발자용 자동 시험 tilesig) 지금 맵의 모든 타일의 색·스타일·메시 값을 한 줄씩 파일로 남긴다.
    // 밀린 색 바꾸기(RecolorDefer)를 먼저 모두 적용한 뒤 적는다. 미루기 켠 판과 끈 판의 파일을 비교해 같은지 본다.
    // (개발자용 자동 시험 decosig) 모든 이미지 장식의 스프라이트·충돌 상자·재질 키워드·텍스처 감싸기 모드를 한 줄씩. 미룬 이미지를 먼저 모두 적용한다.
    internal static class DecoSig
    {
        private static readonly AccessTools.FieldRef<scrVisualDecoration, TextureManager.CustomSprite> spriteField = AccessTools.FieldRefAccess<scrVisualDecoration, TextureManager.CustomSprite>("_sprite");
        private static readonly System.Reflection.FieldInfo colField = AccessTools.Field(typeof(scrDecoration), "editorCollider") ?? AccessTools.Field(typeof(scrVisualDecoration), "editorCollider");
        private static readonly AccessTools.FieldRef<scrDecoration, Vector2> ppRef = AccessTools.FieldRefAccess<scrDecoration, Vector2>("pivotPosVec");
        private static readonly AccessTools.FieldRef<scrDecoration, Vector2> poRef = AccessTools.FieldRefAccess<scrDecoration, Vector2>("pivotOffsetVec");
        private static readonly AccessTools.FieldRef<scrDecoration, float> rotRef = AccessTools.FieldRefAccess<scrDecoration, float>("rotAngle");
        private static readonly AccessTools.FieldRef<scrDecoration, Vector2> scRef = AccessTools.FieldRefAccess<scrDecoration, Vector2>("scaleVec");
        private static readonly AccessTools.FieldRef<scrDecoration, Transform> pivField = AccessTools.FieldRefAccess<scrDecoration, Transform>("pivotTrans");
        private static readonly AccessTools.FieldRef<scrDecoration, Transform> childField = AccessTools.FieldRefAccess<scrDecoration, Transform>("childTransform");
        private static string V2(Vector2 v) { return F(v.x) + "," + F(v.y); }
        private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        internal static string Run(string dir, string name)
        {
            LazySprite.FlushAll();
            var mgr = scrDecorationManager.instance;
            if (mgr == null) return "관리자 없음";
            var list = mgr.allDecorations;
            var path = Path.Combine(dir, "decosig-" + name + ".txt");
            int n = 0;
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var v = list[i] as scrVisualDecoration;
                    if (v == null) { w.WriteLine(i + "|-"); continue; }
                    var sb = new StringBuilder(160);
                    var sr = v.spriteRenderer; var sp = sr != null ? sr.sprite : null;
                    var cs = spriteField(v);
                    sb.Append(sp != null ? sp.name : "null").Append('|').Append(v.spriteMask != null && v.spriteMask.sprite != null ? v.spriteMask.sprite.name : "null")
                      .Append('|').Append(v.spriteAlphaMask != null && v.spriteAlphaMask.sprite != null ? v.spriteAlphaMask.sprite.name : "null")
                      .Append('|').Append(cs == null ? "null" : (cs.sprite != null ? cs.sprite.name : "cs-nosprite"))
                      .Append('|').Append(F(v.spriteUnscaledSize.x)).Append(',').Append(F(v.spriteUnscaledSize.y));
                    var col = colField.GetValue(v) as UnityEngine.Object;
                    if (col != null) { var sz = (Vector2)col.GetType().GetProperty("size").GetValue(col, null); sb.Append("|col=").Append(F(sz.x)).Append(',').Append(F(sz.y)); }
                    if (sr != null && sr.sharedMaterial != null) sb.Append("|kw=").Append(string.Join(",", sr.sharedMaterial.shaderKeywords));
                    if (sp != null && sp.texture != null) sb.Append("|wrap=").Append((int)sp.texture.wrapModeU).Append(',').Append((int)sp.texture.wrapModeV);
                    var pt = pivField(v); var ct = childField(v);
                    sb.Append("|pp=").Append(V2(ppRef(v))).Append("|po=").Append(V2(poRef(v))).Append("|rot=").Append(F(rotRef(v))).Append("|sc=").Append(V2(scRef(v)));
                    if (ct != null) sb.Append("|cl=").Append(F(ct.localPosition.x)).Append(',').Append(F(ct.localPosition.y));
                    if (pt != null) sb.Append("|pw=").Append(F(pt.position.x)).Append(',').Append(F(pt.position.y)).Append("|pr=").Append(F(pt.eulerAngles.z)).Append("|ps=").Append(F(pt.localScale.x)).Append(',').Append(F(pt.localScale.y));
                    w.Write(i); w.Write('|'); w.WriteLine(sb.ToString());
                    n++;
                }
            }
            return "이미지 장식 " + n + "개, 파일 " + path;
        }
    }

    internal static class TileSig
    {
        private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        private static string C(Color c) { return F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a); }
        private static string Tex(Texture t) { return t == null ? "null" : t.name + ":" + t.width + "x" + t.height; }

        // Read-only snapshot: unlike tilesig, do not flush deferred color/mesh
        // work here. That would hide the very ordering problem being observed.
        internal static string State(string arg)
        {
            var ids = arg.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length == 0 || ids.Length > 32) throw new ArgumentException("floorstate: give 1..32 tile IDs");
            var list = ADOBase.lm == null ? null : ADOBase.lm.listFloors;
            if (list == null) throw new InvalidOperationException("floorstate: no level");
            var selected = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                if (!int.TryParse(ids[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out selected[i]) || selected[i] < 0 || selected[i] >= list.Count)
                    throw new ArgumentException("floorstate: invalid tile ID " + ids[i]);
            var cond = scrConductor.instance;
            var sb = new StringBuilder("[floorstate] frame=" + Time.frameCount + " song_s=" +
                (cond == null ? "null" : cond.songposition_minusi.ToString("R", CultureInfo.InvariantCulture)) +
                " catchup=" + EffectBudget.InCatchUp + " effect_pending=" + EffectBudget.QueueLength + " recolor_pending=" + RecolorSplit.Pending);
            foreach (int id in selected)
            {
                var floor = list[id];
                if (floor == null) { sb.Append("\n tile=").Append(id).Append(" null"); continue; }
                var tr = floor.transform;
                var fr = floor.floorRenderer;
                var r = fr == null ? null : fr.renderer;
                // FloorRenderer.material is the game's cached material field.
                // Do not read Renderer.material (it could instantiate a copy).
                var mat = fr == null ? null : fr.material;
                sb.Append("\n tile=").Append(id).Append(" script=").Append(floor.enabled)
                  .Append(" active=").Append(floor.gameObject.activeInHierarchy).Append(" culled=").Append(TileCull.IsCulled(floor))
                  .Append(" opacity=").Append(F(floor.opacity)).Append(" opacityVal=").Append(F(floor.opacityVal))
                  .Append(" dontChangeMySprite=").Append(floor.dontChangeMySprite)
                  .Append(" pos=").Append(tr.position.ToString("R")).Append(" scale=").Append(tr.localScale.ToString("R"))
                  .Append(" startScale=").Append(floor.startScale.ToString("R")).Append(" style=").Append(floor.styleNum)
                  .Append(" renderer=").Append(r == null ? "null" : r.enabled.ToString())
                  .Append(" visible=").Append(r == null ? "null" : r.isVisible.ToString())
                  .Append(" material=").Append(mat == null ? "null" : mat.name)
                  .Append(" alpha=").Append(mat != null && mat.HasProperty("_Alpha") ? F(mat.GetFloat("_Alpha")) : "NA")
                  .Append(" color=").Append(mat != null && mat.HasProperty("_Color") ? C(mat.GetColor("_Color")) : "NA");
            }
            return sb.ToString();
        }

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
