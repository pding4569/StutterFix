#if DEV || AUTOTEST
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using HarmonyLib;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        private static BinaryWriter signature;
        private static int[] signatureIndices;
        private static float[] signatureValues;
        private static byte[] signatureBytes;
        private static int signatureFrame, signatureCount, signatureGeneration;
        private static double signatureStart, signatureSeconds;
        private static float signatureHz;
        private static bool signatureAdopt;
        private static string signaturePath;
        private static readonly AccessTools.FieldRef<scrDecoration, Transform> signaturePivot = AccessTools.FieldRefAccess<scrDecoration, Transform>("pivotTrans");
        // Explicit correctness capture: large and intrusive; never use it for performance runs.
        private static void AdoptSignature(string arg)
        {
            if (arg.Trim().Equals("off", StringComparison.OrdinalIgnoreCase)) { StopSignature(true); return; }
            if (!ready || table == null || !TightLate.CaptureReady) throw new InvalidOperationException("adoptsig: ready/장식 LateUpdate 후크 필요");
            ValidateBaseline(); StopSignature();
            var parts = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0].IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || parts[0] == "." || parts[0] == "..") throw new ArgumentException("adoptsig 이름 [초=8] [표시Hz=120]");
            signatureSeconds = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 8;
            signatureHz = parts.Length > 2 ? float.Parse(parts[2], CultureInfo.InvariantCulture) : 120;
            if (!DecoBakeCore.Finite(signatureSeconds) || signatureSeconds <= 0 || signatureSeconds > 30
                || !DecoBakeCore.Finite(signatureHz) || signatureHz < 20 || signatureHz > 1000) throw new ArgumentException("초 0~30, Hz 20~1000");
            signatureIndices = Enumerable.Range(0, table.Decorations.Length).Where(d => table.Decorations[d].Reasons.Count == 0).ToArray();
            if (signatureIndices.Length == 0) throw new InvalidOperationException("adoptsig: O=0");
            signaturePath = Path.Combine(Main.Entry.Path, "adoptsig-" + parts[0] + ".bin.gz");
            // Refuse silent replacement of evidence from a prior run.
            signatureGeneration = generation; signatureFrame = -1; signatureCount = 0; signatureStart = double.NaN; signatureAdopt = Adopting;
            string header = "{\"schema\":8,\"fingerprint\":\"" + TableFingerprint() + "\",\"adopt\":" + (Adopting ? "true" : "false")
                + ",\"hz\":" + N(signatureHz) + ",\"indices\":[" + string.Join(",", signatureIndices) + "],\"channels\":35}";
            signatureValues = new float[signatureIndices.Length * 35]; signatureBytes = new byte[signatureValues.Length * 4];
            var file = new FileStream(signaturePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            signature = new BinaryWriter(new GZipStream(file, System.IO.Compression.CompressionLevel.Fastest), Encoding.UTF8);
            try { signature.Write(Encoding.UTF8.GetBytes(header + "\n")); }
            catch { StopSignature(); throw; }
            Log("adoptsig 시작 " + signaturePath + " O=" + signatureIndices.Length + "; 성능 측정과 분리");
        }
        internal static void SignatureFrame()
        {
            if (signature == null || signatureFrame == Time.frameCount) return;
            try
            {
                if (!ready || signatureGeneration != generation || signatureAdopt != Adopting)
                    throw new InvalidOperationException("서명 중 세대/채택 상태 변경");
                if (!Hitch.Playing || ADOBase.controller.paused) return;
                double time = scrConductor.instance.songposition_minusi;
                if (!DecoBakeCore.Finite(time)) throw new InvalidOperationException("서명 시각 비유한");
                if (!DecoBakeCore.Finite(signatureStart)) signatureStart = time;
                if (time - signatureStart >= signatureSeconds) { StopSignature(true); return; }
                if (time < signatureStart || signatureCount >= 2000) throw new InvalidOperationException("서명 되감기/2000프레임 상한");
                var cam = scrCamera.instance != null ? scrCamera.instance.camobj : Camera.main;
                if (cam == null) throw new InvalidOperationException("서명 카메라 없음");
                signatureFrame = Time.frameCount;
                signature.Write((byte)1); signature.Write(time); signature.Write(clock.Time); signature.Write(Time.frameCount);
                int row = 0;
                foreach (int index in signatureIndices)
                {
                    var d = baselineObjects[index];
                    if (d == null) throw new InvalidOperationException("서명 장식 소멸");
                    var color = d.color; var par = d.parallax.multiplier;
                    var pivot = signaturePivot(d);
                    if (pivot == null) throw new InvalidOperationException("서명 pivot 없음");
                    var position = pivot.position; var screen = cam.WorldToScreenPoint(position);
                    var dx = cam.WorldToScreenPoint(position + Vector3.right) - screen;
                    var dy = cam.WorldToScreenPoint(position + Vector3.up) - screen;
                    // Logical channels plus rendered screen position and local world->pixel factors.
                    int o = row++ * 35; var values = signatureValues;
                    values[o] = d.pivotPosVec.x; values[o+1] = d.pivotPosVec.y;
                    values[o+2] = d.pivotOffsetVec.x; values[o+3] = d.pivotOffsetVec.y; values[o+4] = d.rotAngle;
                    values[o+5] = d.scaleVec.x; values[o+6] = d.scaleVec.y;
                    values[o+7] = color.r; values[o+8] = color.g; values[o+9] = color.b; values[o+10] = color.a;
                    values[o+11] = d.opacity; values[o+12] = par.x; values[o+13] = par.y;
                    values[o+14] = d.parallaxOffset.x; values[o+15] = d.parallaxOffset.y;
                    values[o+16] = screen.x; values[o+17] = screen.y;
                    values[o+18] = new Vector2(dx.x,dx.y).magnitude; values[o+19] = new Vector2(dy.x,dy.y).magnitude;
                    // Three actual sprite corners capture rendered translation/rotation/scale,
                    // including deferred Transform writes that logical fields alone cannot prove.
                    var renderer = ((scrVisualDecoration)d).spriteRenderer;
                    var sprite = renderer == null ? null : renderer.sprite;
                    var rendered = renderer == null ? Color.clear : renderer.color;
                    for (int c = 20; c < 26; c++) values[o+c] = 0;
                    if (sprite != null)
                    {
                        var bounds = sprite.bounds; var trans = renderer.transform;
                        var a = cam.WorldToScreenPoint(trans.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, 0)));
                        var b = cam.WorldToScreenPoint(trans.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, 0)));
                        var c = cam.WorldToScreenPoint(trans.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, 0)));
                        values[o+20] = a.x; values[o+21] = a.y; values[o+22] = b.x;
                        values[o+23] = b.y; values[o+24] = c.x; values[o+25] = c.y;
                    }
                    values[o+26] = rendered.r; values[o+27] = rendered.g; values[o+28] = rendered.b; values[o+29] = rendered.a;
                    values[o+30] = renderer != null && renderer.enabled ? 1 : 0;
                    values[o+31] = renderer != null && renderer.forceRenderingOff ? 1 : 0;
                    values[o+32] = renderer != null && renderer.gameObject.activeInHierarchy ? 1 : 0;
                    values[o+33] = sprite != null ? 1 : 0; values[o+34] = renderer != null ? 1 : 0;
                }
                foreach (float value in signatureValues) if (!DecoBakeCore.Finite(value)) throw new InvalidOperationException("서명 값 비유한");
                Buffer.BlockCopy(signatureValues, 0, signatureBytes, 0, signatureBytes.Length);
                signature.Write(signatureBytes);
                signatureCount++;
            }
            catch (Exception e) { Log("adoptsig 중단: " + e.Message); StopSignature(); }
        }
        private static void StopSignature(bool complete = false)
        {
            if (signature == null) return;
            var closing = signature; signature = null;
            try { closing.Write((byte)0); closing.Write(complete && signatureCount > 0); closing.Write(signatureCount); }
            catch (Exception e) { Log("adoptsig footer 실패: " + e.Message); }
            finally
            {
                try { closing.Dispose(); } catch (Exception e) { Log("adoptsig 닫기 실패: " + e.Message); }
                signatureValues = null; signatureBytes = null; signatureIndices = null;
                Log("adoptsig 종료 " + signaturePath + " frames=" + signatureCount + " complete=" + complete);
            }
        }
    }
}
#endif
