using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StutterFix
{
    // Research opt-in. A successful probe belongs to this process/device, never to saved settings.
    internal static class HitMeterShaderAlpha
    {
        internal const int Capacity = 256;
        internal static readonly float[] Alphas = new float[Capacity];
        private static readonly Entry[] entries = new Entry[Capacity];
        private static readonly List<HitMeterAlphaSlot> effects = new List<HitMeterAlphaSlot>(Capacity);
        private static readonly int arrayId = Shader.PropertyToID("_SFHitMeterAlpha");
        private static readonly FieldInfo rawMaterial = typeof(Graphic).GetField("m_Material", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Material material;
        private static AssetBundle bundle;
        private static bool enabled, subscribed, changing, failed;
        private static int calibrated = -1, activeCount;
        private static int uploadFrame = -1, checkCursor;
        private static bool pendingUpload;
        internal static long Uploads, Attached, Fallbacks;
        internal static string ProbeResult = "not run";
        internal sealed class Entry
        {
            internal Image Image;
            internal CanvasRenderer Renderer;
            internal Material Original;
            internal HitMeterAlphaSlot Effect;
            internal Canvas Canvas;
            internal AdditionalCanvasShaderChannels Channels;
            internal int Slot;
            internal bool Active;
            internal float Alpha = 1;
            internal UnityEngine.Events.UnityAction Dirty;
        }
        internal static bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled == value) return;
                enabled = value;
                if (!value) { RestoreAll(); return; }
                failed = false;
                Subscribe();
                if (calibrated < 0) HitMeterAlphaProbe.Run(quick: true);
            }
        }
        private static void Subscribe()
        {
            if (subscribed) return;
            Canvas.willRenderCanvases += Flush;
            SceneManager.activeSceneChanged += SceneChanged;
            SceneManager.sceneUnloaded += SceneUnloaded;
            subscribed = true;
        }
        private static void SceneChanged(Scene a, Scene b) { RestoreAll(); }
        private static void SceneUnloaded(Scene s) { RestoreAll(); }
        internal static bool Load()
        {
            if (material != null) return true;
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("StutterFix.hitmeteralpha"))
                {
                    if (stream == null) throw new InvalidOperationException("shader bundle missing");
                    var bytes = new byte[stream.Length];
                    int at = 0, n;
                    while (at < bytes.Length && (n = stream.Read(bytes, at, bytes.Length - at)) > 0) at += n;
                    if (at != bytes.Length) throw new EndOfStreamException();
                    bundle = AssetBundle.LoadFromMemory(bytes);
                }
                var shader = bundle.LoadAsset<Shader>("assets/hitmeteralpha.shader");
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("shader unavailable");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                for (int i = 0; i < Capacity; i++) Alphas[i] = 1;
                Shader.SetGlobalFloatArray(arrayId, Alphas);
                return true;
            }
            catch (Exception ex)
            {
                Fail(ex);
                if (bundle != null) bundle.Unload(true);
                bundle = null;
                return false;
            }
        }
        private static void Fail(Exception ex)
        {
            failed = true;
            RestoreAll();
            Lg.I("[meteralpha] fallback: " + ex.Message);
        }
        internal static Entry TryAttach(Image image)
        {
            if (!enabled || failed || calibrated < 0) return null;
            try
            {
                // Only the known tick hierarchy, with the unmodified stock UI material/mesh path.
                if (image == null || image.GetType() != typeof(Image) || image.transform.parent == null
                    || image.transform.parent.parent == null
                    || image.transform.parent.parent.GetComponent("ADOFAI.ErrorMeterTick") == null || rawMaterial == null
                    || image.material != image.defaultMaterial || image.materialForRendering != image.defaultMaterial
                    || image.GetComponentsInParent<Mask>(true).Length != 0
                    || image.GetComponentsInParent<RectMask2D>(true).Length != 0
                    || image.GetComponentsInParent<CanvasGroup>(true).Length != 0) return null;
                var components = image.GetComponents<Component>();
                HitMeterAlphaSlot reusable = null;
                foreach (var c in components)
                {
                    if (c is HitMeterAlphaSlot old && old.Owner == null && !old.enabled) { reusable = old; continue; }
                    if (!(c is RectTransform) && !(c is CanvasRenderer) && c != image) return null;
                }
                var canvas = image.canvas;
                if (canvas == null || !Load()) return null;
                int slot = -1;
                for (int i = 0; i < Capacity; i++) if (entries[i] == null) { slot = i; break; }
                if (slot < 0) return null;
                var entry = new Entry { Image = image, Renderer = image.canvasRenderer, Original = (Material)rawMaterial.GetValue(image),
                    Canvas = canvas, Channels = canvas.additionalShaderChannels, Slot = slot, Active = true };
                // All entries on a canvas retain the same pre-ownership channel mask.
                foreach (var other in entries) if (other != null && other.Canvas == canvas) { entry.Channels = other.Channels; break; }
                entries[slot] = entry;
                activeCount++;
                changing = true;
                try
                {
                    if ((canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
                        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
                    entry.Effect = reusable != null ? reusable : image.gameObject.AddComponent<HitMeterAlphaSlot>();
                    if (reusable == null) effects.Add(entry.Effect);
                    entry.Effect.Owner = entry;
                    entry.Effect.enabled = true;
                    image.material = material;
                    image.SetVerticesDirty();
                    entry.Dirty = () => { if (!changing) Restore(entry); };
                    image.RegisterDirtyVerticesCallback(entry.Dirty);
                    image.RegisterDirtyMaterialCallback(entry.Dirty);
                    Alphas[slot] = 1;
                    pendingUpload = true;
                    entry.Renderer.SetAlpha(1);
                }
                finally { changing = false; }
                Attached++;
                return entry;
            }
            catch (Exception ex) { Fail(ex); return null; }
        }
        internal static void Write(Entry entry, CanvasRenderer renderer, float alpha)
        {
            if (entry != null) entry.Alpha = alpha;
            // The calibration domain is [0, 1]. Preserve external overshooting/NaN tween behavior natively.
            if (entry != null && entry.Active && !(alpha >= 0f && alpha <= 1f)) Restore(entry);
            if (entry != null && entry.Active)
            {
                if (Alphas[entry.Slot] != alpha) pendingUpload = true;
                Alphas[entry.Slot] = alpha;
            }
            else if (renderer != null) renderer.SetAlpha(alpha);
        }
        internal static void Detach(Image image)
        {
            if (activeCount == 0) return;
            foreach (var entry in entries) if (entry != null && entry.Image == image) Restore(entry);
        }
        private static void Flush()
        {
            if (!enabled || failed || activeCount == 0) return;
            try
            {
                // 전체 검사를 매 프레임 하면 엔진 호출이 눈금 120개 × 8번이라 이득을 다 먹는다(HELLO 프로파일 Flush 2.89%,
                // GetInheritedAlpha 1.64%). 재질·정점 변경은 dirty 콜백이 바로 잡고, 붙일 때 CanvasGroup/마스크가 없는 것만 받는다.
                // 나머지(외부 SetAlpha, 캔버스 이동 등 드문 변경)는 프레임마다 한 칸씩 돌아가며 확인한다.
                var entry = entries[checkCursor];
                checkCursor = (checkCursor + 1) % Capacity;
                if (entry != null && (entry.Image == null || entry.Canvas == null || entry.Image.material != material
                    || entry.Image.canvas != entry.Canvas || !entry.Effect || !entry.Effect.enabled
                    || entry.Renderer.GetAlpha() != 1f || entry.Renderer.GetInheritedAlpha() != 1f)) Restore(entry);
                bool any = activeCount > 0;
                // Normally once per frame. A forced earlier canvas draw must not hide later tween writes.
                if (any && pendingUpload)   // 전역 배열은 프레임을 넘어 남는다: 바뀐 때만 올린다
                { Shader.SetGlobalFloatArray(arrayId, Alphas); Uploads++; uploadFrame = Time.frameCount; pendingUpload = false; }
            }
            catch (Exception ex) { Fail(ex); }
        }
        internal static void Restore(Entry entry)
        {
            if (entry == null || !entry.Active) return;
            entry.Active = false;
            if (ReferenceEquals(entries[entry.Slot], entry)) { entries[entry.Slot] = null; activeCount--; }
            changing = true;
            try
            {
                if (entry.Image != null)
                {
                    if (entry.Dirty != null)
                    {
                        entry.Image.UnregisterDirtyVerticesCallback(entry.Dirty);
                        entry.Image.UnregisterDirtyMaterialCallback(entry.Dirty);
                    }
                    // Respect a material already installed by another mod.
                    if (entry.Image.material == material) entry.Image.material = entry.Original;
                    if (entry.Renderer != null) entry.Renderer.SetAlpha(entry.Alpha);
                }
                // Keep the disabled modifier until global cleanup: Show can Kill and start a new fade
                // in the same frame, before Unity's deferred Destroy would remove the old component.
                if (entry.Effect != null) { entry.Effect.Owner = null; entry.Effect.enabled = false; }
                bool owned = false;
                foreach (var other in entries) if (other != null && other.Canvas == entry.Canvas) { owned = true; break; }
                if (!owned && entry.Canvas != null)
                    entry.Canvas.additionalShaderChannels = (entry.Canvas.additionalShaderChannels & ~AdditionalCanvasShaderChannels.TexCoord1)
                        | (entry.Channels & AdditionalCanvasShaderChannels.TexCoord1);
                Fallbacks++;
            }
            finally { changing = false; }
        }
        internal static void RestoreAll()
        {
            foreach (var entry in entries) Restore(entry);
            foreach (var effect in effects) if (effect != null) { effect.Owner = null; effect.enabled = false; UnityEngine.Object.Destroy(effect); }
            effects.Clear();
        }
        internal static void Shutdown()
        {
            enabled = false;
            RestoreAll();
            if (subscribed)
            {
                Canvas.willRenderCanvases -= Flush;
                SceneManager.activeSceneChanged -= SceneChanged;
                SceneManager.sceneUnloaded -= SceneUnloaded;
                subscribed = false;
            }
            if (material != null) UnityEngine.Object.Destroy(material);
            material = null;
            if (bundle != null) bundle.Unload(true);
            bundle = null;
            calibrated = -1;
        }
        internal static string State()
        {
            int count = 0; foreach (var e in entries) if (e != null) count++;
            return "enabled=" + enabled + " calibrated=" + calibrated + " failed=" + failed + " active=" + count
                + " attached=" + Attached + " fallback=" + Fallbacks + " uploads=" + Uploads + " probe=" + ProbeResult;
        }
#if DEV || AUTOTEST
        internal static string Command(string arg)
        {
            switch (arg.Trim().ToLowerInvariant())
            {
                case "on": Main.Config.HitMeterShaderAlpha = true; Enabled = true; break;
                case "off": Main.Config.HitMeterShaderAlpha = false; Enabled = false; break;
                case "probe": return HitMeterAlphaProbe.Run();
                case "state": case "": break;
                default: return "meteralpha on|off|state|probe";
            }
            return State();
        }
#endif
        internal static Material ProbeMaterial { get { return material; } }
        internal static void Calibrate(int mode, string result)
        {
            calibrated = mode;
            ProbeResult = result;
            if (material != null && mode >= 0) material.SetFloat("_SFAlphaMode", mode);
        }
    }

    internal sealed class HitMeterAlphaSlot : BaseMeshEffect
    {
        internal HitMeterShaderAlpha.Entry Owner;
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Owner == null || !Owner.Active) return;
            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv1.x = Owner.Slot;
                vh.SetUIVertex(vertex, i);
            }
        }
        protected override void OnDisable() { HitMeterShaderAlpha.Restore(Owner); base.OnDisable(); }
    }
}
