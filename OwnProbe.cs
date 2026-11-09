#if AUTOTEST
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    // 진단: 같은 멈춘 장면에서 "GPU 장식이 맡은 장식"의 GPU 비용을 원래 방식과 바로 비교한다.
    //   ownprobe save  - 지금 GPU 장식이 맡은 원래 렌더러 목록을 적어 둔다(GPU 장식이 켜져 있을 때)
    //   ownprobe stop  - GPU 장식을 끄고 원래 방식으로 되돌린다(상태 동기화 포함)
    //   ownprobe hide  - 적어 둔 원래 렌더러를 카메라가 안 보는 층(31)으로 옮긴다 / ownprobe show - 되돌린다
    internal static partial class DecoBake
    {
        static readonly List<Renderer> ownProbeList = new List<Renderer>();
        static readonly List<int> ownProbeLayer = new List<int>();
        static int upFrame;
        static readonly Dictionary<Renderer, int> ownHideLayer = new Dictionary<Renderer, int>();
        static readonly HashSet<Renderer> ownSpanWas = new HashSet<Renderer>();
        internal static string OwnProbe(string arg)
        {
            switch (arg.Trim())
            {
                case "save":
                    ownProbeList.Clear(); ownProbeLayer.Clear();
                    foreach (var r in gpuLiveOwnedRenderers) if (r) ownProbeList.Add(r);
                    return "saved=" + ownProbeList.Count + " active=" + gpuLiveActive;
                case "stop":
                    if (gpuLiveActive) StopGpuLive("ownprobe");
                    GpuDecoPlayer.End("ownprobe");
                    return "stopped active=" + gpuLiveActive;
                case "hide":
                    ownProbeLayer.Clear();
                    foreach (var r in ownProbeList) { ownProbeLayer.Add(r ? r.gameObject.layer : 0); if (r) r.gameObject.layer = 31; }
                    return "hidden=" + ownProbeList.Count;
                case "show":
                    for (int i = 0; i < ownProbeList.Count && i < ownProbeLayer.Count; i++) if (ownProbeList[i]) ownProbeList[i].gameObject.layer = ownProbeLayer[i];
                    return "shown=" + ownProbeList.Count;
            }
            if (arg.Trim() == "spansoff" || arg.Trim() == "spanson")
            {
                bool on = arg.Trim() == "spanson"; int n = 0;
                foreach (var sp in gpuCollapseSpans) if (sp.Renderer) { if (on) { if (ownSpanWas.Contains(sp.Renderer)) sp.Renderer.enabled = true; } else if (sp.Renderer.enabled) { ownSpanWas.Add(sp.Renderer); sp.Renderer.enabled = false; n++; } }
                if (on) ownSpanWas.Clear();
                return (on ? "spans restored" : "spans off=" + n);
            }
            if (arg.Trim().StartsWith("blend "))
            {
                // 측정 전용: 모든 구간 재질의 섞기 계수를 바꾼다(그림은 틀림). 예: blend 1 10 (기본), blend 1 1, blend 1 0, blend 5 1
                var q = arg.Trim().Split(' '); float sb = float.Parse(q[1]), db = float.Parse(q[2]); int n = 0;
                var seen = new HashSet<Material>();
                foreach (var sp in gpuCollapseSpans) if (sp.Material && seen.Add(sp.Material)) { sp.Material.SetFloat("_SrcBlend", sb); sp.Material.SetFloat("_DstBlend", db); n++; }
                return "blend set src=" + sb + " dst=" + db + " materials=" + n;
            }
            if (arg.Trim() == "rtinfo")
            {
                var cam = Camera.allCameras.FirstOrDefault(c => c.name == "Camera");
                var rt = cam ? cam.targetTexture : null;
                return "main_camera target=" + (rt ? rt.width + "x" + rt.height + " " + rt.graphicsFormat + " depth=" + rt.depthStencilFormat + " aa=" + rt.antiAliasing : "screen " + Screen.width + "x" + Screen.height) + " hdr=" + (cam && cam.allowHDR);
            }
            if (arg.Trim().StartsWith("items "))
            {
                string want = arg.Trim().Substring(6).Trim(); var src = gpuLiveSlices.Count > 0 ? gpuLiveSlices[0] : null; if (src == null) return "no slice";
                float[] vals = null; try { vals = src.Gpu.ReadValuesForDiag(); } catch { }
                int shown = 0;
                for (int i = 0; i < src.Data.Items.Length && shown < 12; i++)
                {
                    var row = gpuCollapseOwnedRows != null ? gpuCollapseOwnedRows[i] : null; var sr = row?.R as SpriteRenderer;
                    if (!sr || !sr.sprite || !sr.sprite.texture.name.Contains(want)) continue;
                    int m = src.MasterItems[i]; var d = sr.GetComponentInParent<scrVisualDecoration>();
                    int slot = gpuCollapseSlotOfItem != null ? gpuCollapseSlotOfItem[i] : -1;
                    if (vals != null) Main.Entry.Logger.Log("[ownprobe-item]   ch=" + string.Join(",", Enumerable.Range(0, 16).Select(c => vals[i * 16 + c].ToString("G6"))));
                    Main.Entry.Logger.Log("[ownprobe-item] " + sr.sprite.texture.name + " item=" + i + " incl=" + (gpuCollapseIncluded != null && gpuCollapseIncluded[i]) + " fb=" + DrawCollapseTextureFallback(i)
                        + " g1hidden=" + G1DrawHidden(m) + " map=" + (slot >= 0 ? gpuCollapseMapCpu[slot].ToString("X") : "-") + " hiddenState=" + (gpuItemHiddenState != null && gpuItemHiddenState[i])
                        + " gpuAlpha=" + (vals != null ? (vals[i * 16 + 10] * vals[i * 16 + 11]).ToString("F3") : "?") + " deco_visible=" + (d ? g1RendererEnabled(d).ToString() : "?") + " opacity=" + (d ? d.opacity.ToString("F2") : "?") + " color.a=" + (d ? d.color.a.ToString("F2") : "?") + " srEnabled=" + sr.enabled + " forceOff=" + sr.forceRenderingOff);
                    shown++;
                }
                return "items " + want + " shown=" + shown;
            }
            if (arg.Trim() == "tieprobe2") return TieProbe2.Run(null);
            if (arg.Trim() == "tieprobe3") return TieProbe2.RunPair();
            if (arg.Trim().StartsWith("hidetex ") || arg.Trim().StartsWith("showtex "))
            {
                // GPU 장식이 켜져 있으면 그 텍스처 항목의 자리를 비우고, 꺼져 있으면 원래 렌더러를 31층으로 옮긴다(되돌리기 showtex)
                bool hide = arg.Trim().StartsWith("hidetex "); string want = arg.Trim().Substring(8).Trim(); int n0 = 0;
                if (gpuLiveActive && gpuCollapseOn && gpuLiveSlices.Count > 0)
                {
                    var src = gpuLiveSlices[0];
                    for (int i = 0; i < src.Data.Items.Length; i++)
                    {
                        var sr = gpuCollapseOwnedRows[i]?.R as SpriteRenderer; if (!sr || !sr.sprite || !sr.sprite.texture.name.Contains(want)) continue;
                        if (hide) WriteItemSlots(i, false, true); else SyncG1CollapseItem(i); n0++;
                    }
                    return (hide ? "gpu hid " : "gpu shown ") + want + " n=" + n0;
                }
                foreach (var d in Object.FindObjectsByType<scrVisualDecoration>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var sr = d.spriteRenderer; if (!sr || !sr.sprite || !sr.sprite.texture.name.Contains(want)) continue;
                    if (hide) { if (sr.gameObject.layer != 31) { ownHideLayer[sr] = sr.gameObject.layer; sr.gameObject.layer = 31; } }
                    else if (ownHideLayer.TryGetValue(sr, out int l0)) { sr.gameObject.layer = l0; ownHideLayer.Remove(sr); }
                    n0++;
                }
                return (hide ? "native hid " : "native shown ") + want + " n=" + n0;
            }
            if (arg.Trim().StartsWith("deco "))
            {
                string want = arg.Trim().Substring(5).Trim(); int n0 = 0;
                foreach (var d in Object.FindObjectsByType<scrVisualDecoration>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var sr = d.spriteRenderer; if (!sr || !sr.sprite || !sr.sprite.texture.name.Contains(want) || n0++ > 6) continue;
                    var fx = d.blendModeEffect; var m = sr.sharedMaterial;
                    Main.Entry.Logger.Log("[ownprobe-deco] " + sr.sprite.texture.name + " active=" + sr.gameObject.activeInHierarchy + " en=" + sr.enabled + " forceOff=" + sr.forceRenderingOff + " vis=" + sr.isVisible
                        + " color=" + sr.color + " mat=" + (m ? m.shader.name + "#" + m.GetInstanceID() + " kw=" + string.Join(",", m.shaderKeywords) : "null") + " maskInt=" + sr.maskInteraction
                        + " fx=" + (fx ? fx.enabled + "/" + fx.BlendMode + "/" + fx.MaskMode + "/" + fx.RenderMode : "none") + " layer=" + sr.gameObject.layer + " order=" + sr.sortingOrder + " pos=" + sr.transform.position + " bounds=" + sr.bounds.size);
                    Main.Entry.Logger.Log("[ownprobe-deco]   placement=" + d.placementType + " parallax=" + (d.parallax != null ? d.parallax.multiplier + " clamp=" + d.parallax.clampToScreen + " dontX=" + d.parallax.dontAlterX + " dontY=" + d.parallax.dontAlterY : "null")
                        + " pivotPos=" + d.pivotPosVec + " pivotOff=" + d.pivotOffsetVec + " parallaxOff=" + d.parallaxOffset + " rot=" + d.rotAngle + " scale=" + d.scaleVec + " lockScale=" + d.lockScale + " lockRot=" + d.lockRotation + " pivotTrans=" + (d.pivotTrans ? d.pivotTrans.position.ToString() : "-"));
                }
                return "deco " + want + " n=" + n0;
            }
            if (arg.Trim() == "natorder")
            {
                var cam = Camera.allCameras.FirstOrDefault(c => c.name == "Camera");
                var rows = new List<(SceneRenderOrder.Key k, string s)>();
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!r || !r.enabled || r.forceRenderingOff || !r.isVisible || !r.sharedMaterial || r.sharedMaterial.renderQueue <= 2500) continue;
                    if ((cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                    var k = GpuDecoScene.SortKey(r, r.sharedMaterial, cam);
                    string nm = r is SpriteRenderer sr && sr.sprite ? sr.sprite.texture.name : r.name;
                    rows.Add((k, (ownProbeList.Contains(r) ? "OWN " : "oth ") + r.GetType().Name + ":" + nm + " L" + k.Layer + " O" + k.Order + " d" + k.Distance.ToString("F3") + " mat" + k.Material + " sh=" + r.sharedMaterial.shader.name));
                }
                rows.Sort((x, y) => x.k.CompareTo(y.k));
                foreach (var r in rows) Main.Entry.Logger.Log("[ownprobe-nat] " + r.s);
                return "native listed=" + rows.Count;
            }
            if (arg.Trim() == "spans")
            {
                // 켜진 구간(그리는 순서 = 정렬 키 순)과 담긴 항목 텍스처, 원래 렌더러의 정렬 키
                var cam = Camera.allCameras.FirstOrDefault(c => c.name == "Camera");
                var rows = new List<(SceneRenderOrder.Key k, string s)>();
                for (int si = 0; si < gpuCollapseSpans.Count; si++)
                {
                    var sp = gpuCollapseSpans[si]; if (!sp.Renderer || !sp.Renderer.enabled) continue;
                    var names = new Dictionary<string, int>();
                    foreach (int id in sp.Items) { var r = CRow(id)?.R as SpriteRenderer; string nm = r && r.sprite ? r.sprite.texture.name : "?"; names.TryGetValue(nm, out int c0); names[nm] = c0 + 1; }
                    var k = GpuDecoScene.SortKey(sp.Renderer, sp.Material, cam);
                    rows.Add((k, "SPAN#" + si + " L" + k.Layer + " O" + k.Order + " d" + k.Distance.ToString("F3") + " mat" + k.Material + (sp.Material && sp.Material.shader == gpuCollapseBlendShader ? " grab" : "") + " [" + string.Join(",", names.Select(x => x.Key + "x" + x.Value)) + "] nudged=" + sp.Nudged + " dyn=" + sp.DynState + " first=L" + sp.First.Layer + "/O" + sp.First.Order + "/m" + sp.First.Material));
                }
                foreach (var b in gpuCollapseBarriers) if (b.R && DrawCollapseBarrierActive(b) && b.R.isVisible) { var k = GpuDecoScene.SortKey(b.R, b.M, cam); rows.Add((k, "BAR " + b.R.GetType().Name + ":" + b.R.name + " L" + k.Layer + " O" + k.Order + " d" + k.Distance.ToString("F3") + " mat" + k.Material + " sh=" + (b.M ? b.M.shader.name : "-"))); }
                rows.Sort((x, y) => x.k.CompareTo(y.k));
                foreach (var r in rows) Main.Entry.Logger.Log("[ownprobe-order] " + r.s);
                return "spans listed=" + rows.Count;
            }
            if (arg.Trim() == "live")
            {
                int indep = 0, indepVis = 0, spans = 0, spansVis = 0, orig = 0;
                foreach (var sl in gpuLiveSlices) foreach (var r in sl.Renderers) if (r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy) { indep++; if (r.isVisible) indepVis++; }
                foreach (var sp in gpuCollapseSpans) if (sp.Renderer && sp.Renderer.enabled) { spans++; if (sp.Renderer.isVisible) spansVis++; }
                foreach (var r in gpuLiveOwnedRenderers) if (r && r.enabled && !r.forceRenderingOff) orig++;
                return "independent_enabled=" + indep + " (visible " + indepVis + ") spans_enabled=" + spans + " (visible " + spansVis + ") owned_originals_enabled=" + orig + " collapse=" + gpuCollapseOn;
            }
            if (arg.Trim().StartsWith("mark-")) return arg.Trim();
            if (arg.Trim() == "up0") { GpuDecoGpu.UpBytes = 0; GpuDecoGpu.UpCalls = 0; upFrame = Time.frameCount; return "up reset"; }
            if (arg.Trim() == "up") { int fr = Mathf.Max(1, Time.frameCount - upFrame); return "upload bytes/frame=" + (GpuDecoGpu.UpBytes / fr) + " calls/frame=" + ((double)GpuDecoGpu.UpCalls / fr).ToString("F1") + " frames=" + fr + " channels_total=" + (gpuLiveSlices.Count > 0 && gpuLiveSlices[0].Data != null ? gpuLiveSlices[0].Data.Items.Length * 16 : 0); }
            return "ownprobe save|stop|hide|show";
        }
    }
}
#endif
