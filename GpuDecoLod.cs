#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // ADOFAI 렌더러 B: 스크린 공간 LOD.
    // 화면에서 많이 확대된(텍셀 하나가 화면 여러 픽셀) 구간은 1/2·1/4 해상도 그림에 먼저 그리고(본 카메라의 투명 그리기 직전),
    // 원래 그리기 순서 자리에서는 그 구간 렌더러가 같은 재질로 화면 사각형 하나를 그려 저해상도 그림을 섞는다.
    // 재질·렌더러·정렬 중심이 그대로라 엔진의 정렬 순서는 바뀌지 않는다. 확대된 그림은 원래 저주파라 눈으로는 같다(정확히 같지는 않다).
    internal static partial class DecoBake
    {
        internal static class RenderLod
        {
            internal static bool Enabled;          // 측정: renderlod <배율 여유>|off
            internal static float Quality = 1.5f;  // 1/d 해상도는 최소 확대 배율이 d*Quality 이상일 때만
            internal static int MaxSpans = 48;
            internal static int Frames, LodSpanFrames, Readbacks;
        }

        static ComputeShader lodCompute;
        static int lodKClear = -1, lodK = -1;
        static ComputeBuffer lodOut, lodSpanOfSlot, lodBox;
        static readonly int pLodSpan = Shader.PropertyToID("_LodSpan"), pLodBox = Shader.PropertyToID("_LodBox");
        static uint[] lodLatest;
        static int lodLatestSpans;
        static int lodInFlight;
        static CommandBuffer lodCb;
        static Camera lodCbCamera;
        static int[] lodLevel;
        static RenderTexture[] lodRt;
        static Mesh[] lodQuad;
        static MaterialPropertyBlock lodMpb;
        static Material lodClearMaterial;
        static readonly int pLodMode = Shader.PropertyToID("_LodMode"), pLodTex = Shader.PropertyToID("_LodTex"), pLodTarget = Shader.PropertyToID("_LodTarget");

        static bool LodBlendOk(Material m)
        {
            if (!m) return false;
            float sb = m.GetFloat("_SrcBlend"), db = m.GetFloat("_DstBlend"), op = m.GetFloat("_BlendOp");
            // 미리 곱한 알파 "덮기"(One, OneMinusSrcAlpha) 와 더하기(One, One) 만: 투명 검정 위에 모아 그린 뒤 같은 섞기로 붙여도 결과가 같다.
            return sb == 1f && op == 0f && (db == 10f || db == 1f);
        }

        static void LodPrepare()
        {
            LodRelease();
            if (!RenderLod.Enabled || gpuCollapseSpans.Count == 0 || gpuCollapseMapCpu == null || gpuLiveBundle == null || !gpuLiveCamera) return;
            lodCompute = gpuLiveBundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDecoLod.compute");
            if (!lodCompute || gpuCollapseShader.FindPropertyIndex("_LodMode") < 0) { GpuLog("RENDERLOD unsupported bundle"); lodCompute = null; return; }
            var clearShader = gpuLiveBundle.LoadAsset<Shader>("Assets/Shaders/GpuDecoLodClear.shader");
            if (!clearShader || !clearShader.isSupported) { GpuLog("RENDERLOD unsupported clear shader"); lodCompute = null; return; }
            lodClearMaterial = new Material(clearShader) { name = "StutterFix.RenderLod clear" };
            lodKClear = lodCompute.FindKernel("SpanLodClear"); lodK = lodCompute.FindKernel("SpanLod");
            int spans = gpuCollapseSpans.Count;
            var spanOfSlot = new uint[Math.Max(1, gpuCollapseAllCount)];
            for (int k = 0; k < spans; k++)
            {
                var s = gpuCollapseSpans[k];
                uint id = LodBlendOk(s.Material) ? (uint)k : 0xFFFFFFFFu;
                for (int n = 0; n < s.Items.Length; n++) spanOfSlot[s.Base + n] = id;
            }
            lodSpanOfSlot = new ComputeBuffer(spanOfSlot.Length, 4); lodSpanOfSlot.SetData(spanOfSlot);
            lodOut = new ComputeBuffer(spans * 2, 4); lodBox = new ComputeBuffer(spans * 4, 4); Shader.SetGlobalBuffer(pLodBox, lodBox);
            lodLatest = null; lodLatestSpans = spans; lodInFlight = 0;
            lodLevel = new int[spans]; lodRt = new RenderTexture[spans]; lodQuad = new Mesh[spans];
            lodMpb = new MaterialPropertyBlock();
            lodCb = new CommandBuffer { name = "StutterFix RenderLod" };
            lodCbCamera = gpuLiveCamera; lodCbCamera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, lodCb);
            GpuLog("RENDERLOD ready spans=" + spans);
            if (GpuDecoPlayer.Product) Main.Entry.Logger.Log("[GPU 장식] 저해상도 그리기 준비: 구간 " + spans + "개");
        }

        internal static void LodRelease()
        {
            if (lodCbCamera && lodCb != null) lodCbCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, lodCb);
            lodCb?.Release(); lodCb = null; lodCbCamera = null;
            if (lodClearMaterial) UnityEngine.Object.Destroy(lodClearMaterial); lodClearMaterial = null;
            if (lodLevel != null)
                for (int k = 0; k < lodLevel.Length && k < gpuCollapseSpans.Count; k++) if (lodLevel[k] != 0) LodLeave(k);
            if (lodRt != null) foreach (var rt in lodRt) if (rt) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            if (lodQuad != null) foreach (var q in lodQuad) if (q) UnityEngine.Object.Destroy(q);
            lodOut?.Dispose(); lodOut = null; lodBox?.Dispose(); lodBox = null; lodSpanOfSlot?.Dispose(); lodSpanOfSlot = null;
            lodLevel = null; lodRt = null; lodQuad = null; lodLatest = null; lodCompute = null;
        }

        static void LodLeave(int k)
        {
            lodLevel[k] = 0;
            var s = gpuCollapseSpans[k];
            if (!s.Renderer) return;
            var mf = s.Renderer.GetComponent<MeshFilter>(); if (mf && s.Mesh) mf.sharedMesh = s.Mesh;
            s.Renderer.SetPropertyBlock(null);
        }

        // 매 프레임 구간 정점 계산 바로 뒤에 부른다.
        static void LodAfterDispatch()
        {
            if (lodCompute == null) { if (RenderLod.Enabled && lodCb == null && gpuCollapseOn) LodPrepare(); if (lodCompute == null) return; }
            if (!RenderLod.Enabled || !gpuLiveCamera || gpuLiveCamera != lodCbCamera) { LodRelease(); return; }
            RenderLod.Frames++;
            var cam = gpuLiveCamera; var target = cam.targetTexture;
            int w = target ? target.width : Screen.width, h = target ? target.height : Screen.height;
            // 이 경로의 픽셀 좌표는 전체 타깃 기준이다. 분할 화면/부분 viewport는 원래 메시로 되돌린다.
            if (cam.pixelRect != new Rect(0, 0, w, h)) { LodRelease(); return; }
            int spans = lodLatestSpans;
            var drawVp = GL.GetGPUProjectionMatrix(cam.projectionMatrix, target != null) * cam.worldToCameraMatrix;
            // 1) 이번 프레임 배율 계산(결과는 비동기로 1~2프레임 뒤 도착)
            // 배율·화면 범위 계산은 매 프레임(합성 사각형이 이번 프레임 범위를 읽는다). 비동기 읽기만 밀린 개수로 제한한다.
            {
                var root = gpuCollapseSpans[0].Renderer ? gpuCollapseSpans[0].Renderer.localToWorldMatrix : Matrix4x4.identity;
                var vp = drawVp * root;
                lodCompute.SetInt("_LodSpans", spans); lodCompute.SetInt("_LodSlots", gpuCollapseAllCount);
                lodCompute.SetMatrix("_LodVP", vp); lodCompute.SetVector("_LodScreen", new Vector4(w, h, 0, 0));
                lodCompute.SetBuffer(lodKClear, "_LodOut", lodOut); lodCompute.SetBuffer(lodKClear, "_LodBox", lodBox); lodCompute.SetBuffer(lodK, "_LodBox", lodBox);
                lodCompute.Dispatch(lodKClear, (spans + 63) / 64, 1, 1);
                lodCompute.SetBuffer(lodK, "_LodOut", lodOut); lodCompute.SetBuffer(lodK, "_LodSpanOfSlot", lodSpanOfSlot);
                lodCompute.SetBuffer(lodK, "_LodVerts", gpuCollapseAllVerts);
                lodCompute.Dispatch(lodK, (gpuCollapseAllCount + 63) / 64, 1, 1);
                var buf = lodOut;
                if (lodInFlight < 3) { lodInFlight++;
                AsyncGPUReadback.Request(lodOut, r =>
                {
                    if (buf != lodOut) return; // 이전 준비 세대의 콜백은 현재 진행 중인 읽기 수를 바꾸지 않는다.
                    lodInFlight = Math.Max(0, lodInFlight - 1);
                    if (r.hasError) return;
                    var d = r.GetData<uint>(); if (lodLatest == null || lodLatest.Length != d.Length) lodLatest = new uint[d.Length];
                    d.CopyTo(lodLatest); RenderLod.Readbacks++;
                }); }
            }
            // 2) 마지막으로 도착한 배율로 등급을 정하고, 저해상도 그리기 명령을 다시 적는다.
            lodCb.Clear();
            if (lodLatest == null) return;
            int used = 0;
            float q = RenderLod.Quality;
            for (int k = 0; k < spans; k++)
            {
                var s = gpuCollapseSpans[k];
                int want = 0;
                if (s.Renderer && s.Renderer.enabled && lodLatest[k * 2 + 1] > 0 && used < RenderLod.MaxSpans)
                {
                    float mag = BitConverter.Int32BitsToSingle((int)lodLatest[k * 2]);
                    // 들어갈 때보다 나올 때 기준을 조금 낮춰 경계에서 깜빡이지 않게 한다.
                    float keep = lodLevel[k] != 0 ? 0.85f : 1f;
                    if (mag >= 4f * q * keep) want = 4; else if (mag >= 2f * q * keep) want = 2;
                }
                if (want == 0) { if (lodLevel[k] != 0) LodLeave(k); continue; }
                used++;
                int lw = Math.Max(1, w / want), lh = Math.Max(1, h / want);
                var rt = lodRt[k];
                if (!rt || rt.width != lw || rt.height != lh)
                {
                    if (rt) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                    RenderTextureDescriptor desc = target ? target.descriptor : new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0);
                    desc.width = lw; desc.height = lh; desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.useMipMap = false; desc.autoGenerateMips = false;
                    rt = new RenderTexture(desc) { name = "StutterFix.RenderLod", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    rt.Create(); lodRt[k] = rt;
                }
                var quad = lodQuad[k];
                if (!quad)
                {
                    quad = new Mesh { name = "StutterFix.RenderLod quad" };
                    quad.SetVertices(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0), new Vector3(3, 0, 0) });
                    quad.SetIndices(new[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0, false);
                    lodQuad[k] = quad;
                }
                // 정렬 거리는 렌더러 경계 중심으로 정해지므로 원래 구간 메시와 같은 경계를 준다.
                quad.bounds = s.Mesh.bounds;
                lodCb.SetRenderTarget(rt);
                lodCb.SetViewport(new Rect(0, 0, lw, lh));
                lodMpb.Clear(); lodMpb.SetFloat(pLodSpan, k);
                lodMpb.SetVector(pLodTarget, new Vector4(w, h, 1f / w, 1f / h));
                lodMpb.SetVector("_LodClearSize", new Vector4(lw, lh, 0, 0));
                lodCb.DrawMesh(quad, Matrix4x4.identity, lodClearMaterial, 0, 0, lodMpb);
                lodMpb.Clear(); lodMpb.SetFloat(pLodMode, 2f); lodMpb.SetMatrix("_LodDrawVP", drawVp);
                lodCb.DrawMesh(s.Mesh, s.Renderer.localToWorldMatrix, s.Material, 0, 0, lodMpb);
                if (lodLevel[k] == 0)
                {
                    var mf = s.Renderer.GetComponent<MeshFilter>(); if (mf) mf.sharedMesh = quad;
                }
                lodLevel[k] = want;
                lodMpb.Clear(); lodMpb.SetFloat(pLodMode, 1f); lodMpb.SetFloat(pLodSpan, k); lodMpb.SetTexture(pLodTex, rt); lodMpb.SetVector(pLodTarget, new Vector4(w, h, 1f / w, 1f / h));
                s.Renderer.SetPropertyBlock(lodMpb);
            }
            if (used > 0) { lodCb.SetRenderTarget(BuiltinRenderTextureType.CameraTarget); lodCb.SetViewport(cam.pixelRect); RenderLod.LodSpanFrames += used; }
        }

        internal static string LodState()
        {
            int l2 = 0, l4 = 0;
            if (lodLevel != null) foreach (int l in lodLevel) { if (l == 2) l2++; else if (l == 4) l4++; }
            return "renderlod enabled=" + RenderLod.Enabled + " q=" + RenderLod.Quality + " ready=" + (lodCompute != null) + " frames=" + RenderLod.Frames
                + " readbacks=" + RenderLod.Readbacks + " span_frames=" + RenderLod.LodSpanFrames + " now_half=" + l2 + " now_quarter=" + l4
                + " spans=" + lodLatestSpans;
        }
    }
}
#endif
