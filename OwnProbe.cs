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
