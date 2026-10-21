#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static bool gpuExportOwnedLow;
        static int gpuExports;
        static Stopwatch gpuPrepareWatch;
        static void CheckGpuPrepareBudget()
        {
            if (gpuPrepareWatch == null || !gpuPrepareWatch.IsRunning || gpuPrepareWatch.Elapsed.TotalSeconds < 300) return;
            GpuLog("GPUDECO_G1=ERROR stage=prepare prepare_ms=" + gpuPrepareWatch.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture)
                + " reason=first_export_budget_300s status=" + status);
            gpuPrepareWatch.Stop();
            throw new InvalidOperationException("gpudeco: open부터 첫 export까지 300초 예산 초과");
        }
        internal static bool GpuStep(string arg)
        {
            string command = (arg ?? "").Trim();
            if (command == "ready") return Step("ready");
            if (command == "reset") { gpuPrepareWatch = null; gpuExportOwnedLow = false; gpuExports = 0; GpuLog("export_result_reset=True"); return true; }
            if (command == "prepare") { gpuPrepareWatch = Stopwatch.StartNew(); GpuLog("prepare_begin budget_ms=300000 includes=open,bake,seek,export"); return true; }
            if (command == "off") { gpuPrepareWatch = null; GpuLog("on=False owned=0"); return true; }
            if (command == "state") { GpuLog("on=False stage=export-lab ready=" + ready + " generation=" + Generation); return true; }
            if (command == "result") { GpuLog("GPUDECO_G1=" + (gpuExportOwnedLow ? "OWNED_LOW" : "ERROR") + " stage=export-lab exports=" + gpuExports + " reason=real_export_G0_and_game_pixels_required"); return true; }
            if (command == "on") throw new InvalidOperationException("gpudeco: 실제 export G0 및 번들 검증 전 게임 연결 금지");
#if AUTOTEST
            if (command.StartsWith("export ", StringComparison.OrdinalIgnoreCase)) { ExportGpu(command.Substring(7).Trim()); return true; }
#endif
            throw new ArgumentException("gpudeco off|state|ready|export <새 파일 경로>(AUTOTEST 전용)");
        }
        static void GpuLog(string s) => Main.Entry.Logger.Log("[gpudeco] " + s);
#if AUTOTEST
        static void Put(float[] output, int index, Vector3 v) { output[index] = v.x; output[index+1] = v.y; output[index+2] = v.z; }
        static void ExportGpu(string path)
        {
            CheckGpuPrepareBudget();
            var watch = Stopwatch.StartNew();
            if (!ready || table == null || Adopting || LiveDecoRenderer.Active) throw new InvalidOperationException("export: bake ready, adopt/livedeco off 필요");
            if (!ADOBase.controller || !ADOBase.controller.paused || Time.timeScale != 0) throw new InvalidOperationException("export: framecmp pause 필요");
            ValidateBaseline();
            var cam = GpuSplit.FindMain(); var camy = ADOBase.controller.camy;
            if (!cam || !cam.orthographic || !camy || !Dormancy.LiveTrackingReady) throw new InvalidOperationException("export: 직교 카메라/Dormancy 필요");
            if (!manager || manager.transform.position != Vector3.zero || manager.transform.rotation != Quaternion.identity || manager.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("export: 관리자 변환이 단위 변환이 아님");
            var data = new GpuDecoData { RawTime = scrConductor.instance.songposition_minusi, BakeTime = BakeTime, Pitch = table.Pitch,
                TweenScale = table.TweenScale, Frame = Time.frameCount, Awake = Dormancy.AwakeList.Count, Supported = table.Supported, Fingerprint = TableFingerprint(),
                PixelsPerWorld = Mathf.Max(cam.pixelHeight / (2 * cam.orthographicSize), cam.pixelWidth / (2 * cam.orthographicSize * cam.aspect)) };
            var ct = camy.transform; data.Camera = new[] { ct.position.x, ct.position.y, ct.eulerAngles.z,
                camy.camobj.orthographicSize * .2f / (ADOBase.customLevel.levelData.camZoom / 100f) };
            var awakeSet = new HashSet<scrDecoration>(Dormancy.AwakeList); var items = new List<GpuDecoData.Item>();
            var reasons = new SortedDictionary<string, int>();
            for (int index = 0; index < baselineObjects.Length; index++)
            {
                var d = baselineObjects[index] as scrVisualDecoration;
                if (!d || !awakeSet.Contains(d)) continue;
                string why = table.Decorations[index].Reasons.Count != 0 ? "bake-unsupported" : LiveDecoRenderer.ExportReason(d, cam, manager);
                if (why == null && d.parallax.clampToScreen) why = "clamp-screen"; // bake rejects Camera placement; do not invent an inverse projection.
                var r = d.spriteRenderer; LiveDecoGeometry geometry = null;
                if (why == null)
                {
                    var s = r.sprite;
                    if (s.vertices.Length != 4 || s.uv.Length != 4 || s.triangles.Length != 6) why = "nonquad";
                    else geometry = LiveDecoGeometry.Capture(d.pivotTrans, d.childTransform, r.transform, s.vertices, r.flipX, r.flipY);
                    if (geometry == null) why = "child-chain";
                }
                if (why != null) { reasons.TryGetValue(why, out int n); reasons[why] = n + 1; continue; }
                var item = new GpuDecoData.Item { Index = index, Id = d.GetInstanceID(), MaterialId = r.sharedMaterial.GetInstanceID(), TextureId = r.sprite.texture.GetInstanceID(),
                    Layer = r.sortingLayerID, Order = r.sortingOrder, Placement = d.placementType.ToString(),
                    Flags = (d.lockScale ? 1 : 0) | (d.lockRotation ? 2 : 0) | (d.parallax.dontAlterX ? 4 : 0) | (d.parallax.dontAlterY ? 8 : 0) | (r.flipX ? 16 : 0) | (r.flipY ? 32 : 0) };
                var ts = d.scaleMultiplier * d.textureScaleMultiplier;
                item.Constants = new[] { ts.x, ts.y, d.textureScaleMultiplier.x, d.textureScaleMultiplier.y, d.startRot, d.croppedPivotOffset.x, d.croppedPivotOffset.y, d.pivotTrans.position.z };
                if (ts.x == 0 || ts.y == 0 || d.textureScaleMultiplier.x == 0 || d.textureScaleMultiplier.y == 0) throw new InvalidOperationException("export: zero static scale");
                var c = d.color; var p = d.parallax.multiplier;
                item.Actual = new[] { d.pivotPosVec.x, d.pivotPosVec.y, d.pivotOffsetVec.x, d.pivotOffsetVec.y, d.rotAngle, d.scaleVec.x, d.scaleVec.y,
                    c.r, c.g, c.b, c.a, d.opacity, p.x, p.y, d.parallaxOffset.x, d.parallaxOffset.y };
                var color = r.color; item.Color = new[] { color.r, color.g, color.b, color.a };
                item.Initial = (float[])table.Decorations[index].Initial.Clone();
                for (int ch = 0; ch < 16; ch++) item.Tracks[ch] = table.Tracks[index * 16 + ch];
                var corners = r.sprite.vertices; var uv = r.sprite.uv;
                for (int v = 0; v < 4; v++)
                {
                    Put(item.Shape, v*3, geometry.Shape[v]); item.Corners[v*2] = corners[v].x; item.Corners[v*2+1] = corners[v].y;
                    item.UV[v*2] = uv[v].x; item.UV[v*2+1] = uv[v].y;
                    var local = corners[v]; if (r.flipX) local.x = -local.x; if (r.flipY) local.y = -local.y;
                    Put(item.World, v*3, r.transform.TransformPoint(local));
                }
                var origin = geometry.Offset(Vector2.zero); Put(item.OffsetBasis, 0, origin);
                Put(item.OffsetBasis, 3, geometry.Offset(Vector2.right) - origin); Put(item.OffsetBasis, 6, geometry.Offset(Vector2.up) - origin);
                item.Triangles = r.sprite.triangles.Select(x => (int)x).ToArray(); items.Add(item);
            }
            data.Items = items.ToArray();
            string output = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(output));
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None)) data.Write(stream);
            CheckGpuPrepareBudget();
            if (gpuExports == 0 && gpuPrepareWatch != null)
            {
                gpuPrepareWatch.Stop();
                GpuLog("prepare_ms=" + gpuPrepareWatch.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture)
                    + " phase=first_export includes=open,bake,seek,export bake_wall_ms=" + workerWallMs.ToString("F6", CultureInfo.InvariantCulture));
            }
            GpuLog("prepare_ms=" + watch.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture) + " phase=export owned=" + items.Count + " awake=" + data.Awake
                + " raw=" + data.RawTime.ToString("R", CultureInfo.InvariantCulture) + " tb=" + data.BakeTime.ToString("R", CultureInfo.InvariantCulture) + " path=" + output);
            GpuLog("export_reject " + string.Join(" ", reasons.Select(x => x.Key + "=" + x.Value)));
            gpuExports++;
            if (items.Count < 800)
            {
                gpuExportOwnedLow = true; GpuLog("GPUDECO_G1=OWNED_LOW stage=export owned=" + items.Count);
                throw new InvalidOperationException("GPUDECO_G1=OWNED_LOW; 이후 시험 중단");
            }
        }
#endif
    }
}
#endif
