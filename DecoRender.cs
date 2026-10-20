#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StutterFix
{
    // Explicit visual experiment: never patches decoration evaluation or rewinds animation.
    internal sealed class DecoRender : MonoBehaviour
    {
        static DecoRender session;
        static string lastCompare = "not run", lastStop = "none";
        static bool compareFinished;
        static readonly AccessTools.FieldRef<scrVisualDecoration, bool> MeshOn =
            AccessTools.FieldRefAccess<scrVisualDecoration, bool>("meshRendererEnabled");
        static readonly AccessTools.FieldRef<scrDecoration, bool> RendererOn =
            AccessTools.FieldRefAccess<scrDecoration, bool>("rendererEnabled");
        static readonly AccessTools.FieldRef<scrDecoration, Color> RendererColor =
            AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        const int Padding = 4;
        sealed class Image
        {
            internal Texture2D Source;
            internal Atlas Atlas;
            internal int X, Y;
            internal bool PreserveUV;
        }
        sealed class Atlas
        {
            internal Texture2D Texture;
            internal readonly List<Image> Images = new List<Image>();
            internal int Width, Height, Mips, X, Y, Row;
            internal bool Linear, Dedicated;
            internal string Sampler;
        }
        sealed class Item
        {
            internal scrVisualDecoration D;
            internal SpriteRenderer R;
            internal Sprite Sprite;
            internal Material Source;
            internal string Signature;
            internal Image Image;
            internal Vector2[] Vertices, UV;
            internal ushort[] Triangles;
            internal int Layer, Order, UnityLayer, Id;
            internal bool WasOff;
            internal float Distance;
            internal float RepeatX, RepeatY;
            internal Matrix4x4 Matrix;
            internal Color Color;
            internal bool FlipX, FlipY, Visible;
        }
        sealed class Group
        {
            internal GameObject Object;
            internal MeshRenderer Renderer;
            internal Mesh Mesh;
            internal Material Material;
            internal readonly List<Item> Items = new List<Item>();
            internal readonly List<Vector3> Positions = new List<Vector3>();
            internal readonly List<Color> Colors = new List<Color>();
            internal readonly List<Vector2> UV = new List<Vector2>();
            internal readonly List<int> Indices = new List<int>();
            internal Bounds CullBounds;
        }
        readonly List<Item> items = new List<Item>();
        readonly List<Atlas> atlases = new List<Atlas>();
        readonly List<Group> groups = new List<Group>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Dictionary<int, int> cameras = new Dictionary<int, int>();
        scrDecorationManager manager;
        object decorationList;
        Camera mainCamera;
        int scene, decorationCount, vertexCount, pending;
        long resetCount, buildCount, buildTicks, measuredFrames;
        int measuredFrame = -1;
        long frameTicks;
        double lastBuildMs;
        bool stopping, comparing, initialized, startedPlaying;
        string outputDir;

        static void Log(string s) => Main.Entry.Logger.Log("[decorender] " + s);
        static string F(double x) => x.ToString("F6", CultureInfo.InvariantCulture);
        internal static bool Step(string arg, string modDir)
        {
            arg = arg.Trim().ToLowerInvariant();
            if (arg == "compare")
            {
                if (compareFinished) { compareFinished = false; Log(lastCompare); return true; }
                if (!session) { Log("inactive; use decorender on"); return true; }
                if (session.pending == 0) { session.pending = 1; Log("compare queued for main-camera post-render"); }
                return false; // AutoTest retries this step; only ONE comparison is submitted.
            }
            if (arg == "off") { Stop(); Log(State()); return true; }
            if (arg == "state") { Log(State()); return true; }
            if (arg != "on") { Log("decorender on|off|compare|state"); return true; }
            if (session) { Log(State()); return true; }
            compareFinished = false;
            try
            {
                var go = new GameObject("StutterFix.DecoRender") { hideFlags = HideFlags.HideAndDontSave };
                session = go.AddComponent<DecoRender>();
                session.Begin(modDir);
                lastStop = "none";
                Log(State());
            }
            catch (Exception e) { Stop("on failed: " + e); Log(lastStop); }
            return true;
        }

        static bool Candidate(scrVisualDecoration d)
        {
            if (!d || d.GetType() != typeof(scrVisualDecoration) || d.hitbox != 0 || d.isMask() || MeshOn(d)) return false;
            var r = d.spriteRenderer;
            return r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff && r.sprite
                && RendererOn(d) && RendererColor(d).a != 0 && r.color.a != 0
                && r.maskInteraction == SpriteMaskInteraction.None && r.drawMode == SpriteDrawMode.Simple
                && !r.GetComponentInParent<SortingGroup>();
        }

        void Begin(string modDir)
        {
            if (GraphicsSettings.currentRenderPipeline != null) throw new InvalidOperationException("Built-in RP required");
            manager = scrDecorationManager.instance;
            mainCamera = scrCamera.instance ? scrCamera.instance.camobj : Camera.main;
            if (!manager || !mainCamera || SceneReset.Resetting) throw new InvalidOperationException("decoration manager/camera not ready");
            scene = SceneManager.GetActiveScene().handle; resetCount = SceneReset.Count;
            decorationCount = manager.allDecorations.Count; decorationList = manager.allDecorations; startedPlaying = Hitch.Playing;
            outputDir = Path.Combine(modDir, "shots");
            var images = new Dictionary<Texture2D, Image>();
            int excluded = 0;
            foreach (var d in manager.allDecorations)
            {
                var v = d as scrVisualDecoration;
                if (!Candidate(v)) { excluded++; continue; }
                var r = v.spriteRenderer; var sprite = r.sprite; var source = r.sharedMaterial;
                if (!source || !source.shader || sprite.associatedAlphaSplitTexture)
                    throw new InvalidOperationException("missing material/shader or split-alpha sprite " + r.name);
                Image image;
                if (!images.TryGetValue(sprite.texture, out image))
                {
                    image = new Image { Source = sprite.texture }; images.Add(sprite.texture, image);
                    var t = image.Source;
                    Log("texture id=" + t.GetInstanceID() + " name=" + t.name + " size=" + t.width + "x" + t.height
                        + " format=" + t.format + "/" + t.graphicsFormat + " wrap=" + t.wrapMode + " U=" + t.wrapModeU
                        + " V=" + t.wrapModeV + " filter=" + t.filterMode + " mips=" + t.mipmapCount + " readable=" + t.isReadable
                        + " aniso=" + t.anisoLevel + " bias=" + t.mipMapBias);
                }
                // Tiling shaders require original normalized texture coordinates/wrap.
                if (v.repeatX != 1 || v.repeatY != 1) image.PreserveUV = true;
                var item = new Item { D = v, R = r, Sprite = sprite, Source = source, Image = image,
                    Vertices = sprite.vertices, Triangles = sprite.triangles, UV = sprite.uv,
                    Layer = r.sortingLayerID, Order = r.sortingOrder, UnityLayer = r.gameObject.layer,
                    Id = r.GetInstanceID(), WasOff = r.forceRenderingOff, RepeatX = v.repeatX, RepeatY = v.repeatY };
                item.Signature = MaterialState(r, null);
                items.Add(item); vertexCount += item.Vertices.Length;
            }
            if (items.Count == 0) throw new InvalidOperationException("no eligible visible image decorations");
            foreach (var image in images.Values) Place(image);
            foreach (var atlas in atlases) BuildAtlas(atlas);
            var map = new Dictionary<string, Group>();
            foreach (var item in items)
            {
                string key = item.Layer + "/" + item.Order + "/" + atlases.IndexOf(item.Image.Atlas)
                    + "/" + item.UnityLayer + "/" + item.Signature;
                Group g;
                if (!map.TryGetValue(key, out g))
                {
                    g = new Group(); groups.Add(g); map.Add(key, g); // Register before allocations, for failure cleanup.
                    g.Object = new GameObject("decorender-group-" + (groups.Count - 1)) { hideFlags = HideFlags.HideAndDontSave, layer = item.UnityLayer };
                    g.Renderer = g.Object.AddComponent<MeshRenderer>(); g.Renderer.enabled = false;
                    g.Mesh = new Mesh { name = "DecoRender world vertices", indexFormat = IndexFormat.UInt32 };
                    g.Mesh.MarkDynamic(); g.Object.AddComponent<MeshFilter>().sharedMesh = g.Mesh;
                    g.Material = new Material(item.Source) { name = "DecoRender atlas material", hideFlags = HideFlags.HideAndDontSave };
                    MaterialState(item.R, g.Material);
                    g.Material.mainTexture = item.Image.Atlas.Texture;
                    g.Material.enableInstancing = false; // Use uniform _RendererColor, not sprite instancing arrays.
                    if (g.Material.HasProperty("_RendererColor")) g.Material.SetColor("_RendererColor", Color.white);
                    if (g.Material.HasProperty("_Flip")) g.Material.SetVector("_Flip", new Vector4(1, 1, 1, 1));
                    g.Renderer.sharedMaterial = g.Material;
                    g.Renderer.sortingLayerID = item.Layer; g.Renderer.sortingOrder = item.Order;
                    g.Renderer.shadowCastingMode = ShadowCastingMode.Off; g.Renderer.receiveShadows = false;
                    g.Renderer.lightProbeUsage = LightProbeUsage.Off; g.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    Log("group=" + (groups.Count - 1) + " layerID=" + item.Layer + " order=" + item.Order
                        + " unityLayer=" + item.UnityLayer + " atlas=" + atlases.IndexOf(item.Image.Atlas)
                        + " shader=" + item.Source.shader.name + " keywords=" + string.Join(",", item.Source.shaderKeywords)
                        + " queue=" + item.Source.renderQueue + " passes=" + item.Source.passCount);
                }
                g.Items.Add(item);
            }
            // Supply nonempty topology before the first camera cull. Positions are placeholders;
            // the replacement stays disabled until PreCull, then receives world positions in PreRender.
            foreach (var g in groups)
            {
                foreach (var i in g.Items)
                {
                    int offset = g.Positions.Count;
                    foreach (var v in i.Vertices) { g.Positions.Add(Vector3.zero); g.Colors.Add(Color.clear); g.UV.Add(Vector2.zero); }
                    foreach (var t in i.Triangles) g.Indices.Add(offset + t);
                }
                g.Mesh.SetVertices(g.Positions); g.Mesh.SetColors(g.Colors); g.Mesh.SetUVs(0, g.UV); g.Mesh.SetTriangles(g.Indices, 0, false);
            }
            initialized = true;
            Camera.onPreCull += PreCull; Camera.onPreRender += PreRender; Camera.onPostRender += PostRender;
            Log("selected=" + items.Count + " excluded=" + excluded + " source textures=" + images.Count
                + "; order=sortingLayer/order,queue,descending camera distance,ascending original Renderer instanceID"
                + "; instanceID tie is an experimental heuristic, NOT Unity's guaranteed internal tie order"
                + "; same-order external renderer/atlas/material interleaving not preserved; cameras=" + Camera.allCamerasCount);
        }

        // Compare all shader-declared effective values, not material instance IDs. This preserves
        // independent instances with identical state while splitting distinct material/MPB state.
        string MaterialState(SpriteRenderer r, Material copy)
        {
            var m = r.sharedMaterial; var shader = m.shader;
            block.Clear(); r.GetPropertyBlock(block);
            if (block.HasColor("_RendererColor") && block.GetColor("_RendererColor") != Color.white
                && block.GetColor("_RendererColor") != r.color)
                throw new InvalidOperationException("custom MPB _RendererColor unsupported");
            if (block.HasVector("_Flip"))
            {
                var flip = block.GetVector("_Flip");
                if ((flip.x != 1 || flip.y != 1) && (flip.x != (r.flipX ? -1 : 1) || flip.y != (r.flipY ? -1 : 1)))
                    throw new InvalidOperationException("custom MPB _Flip unsupported");
            }
            if (block.HasVector("_MainTex_ST") && block.GetVector("_MainTex_ST") != new Vector4(1, 1, 0, 0))
                throw new InvalidOperationException("custom MPB _MainTex_ST unsupported");
            if (block.HasTexture("_AlphaTex") && block.GetTexture("_AlphaTex"))
                throw new InvalidOperationException("MPB split-alpha unsupported");
            var key = new StringBuilder().Append(shader.GetInstanceID()).Append('|').Append(m.renderQueue)
                .Append('|').Append(string.Join(",", m.shaderKeywords));
            for (int p = 0; p < shader.GetPropertyCount(); p++)
            {
                string n = shader.GetPropertyName(p); int id = Shader.PropertyToID(n);
                if (n == "_RendererColor" || n == "_Flip") continue;
                key.Append('|').Append(n).Append('=');
                switch (shader.GetPropertyType(p))
                {
                    case ShaderPropertyType.Color:
                        Color c = block.HasColor(id) ? block.GetColor(id) : m.GetColor(id);
                        key.Append(c.r.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(c.g.ToString("R", CultureInfo.InvariantCulture))
                            .Append(',').Append(c.b.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(c.a.ToString("R", CultureInfo.InvariantCulture));
                        if (copy) copy.SetColor(id, c); break;
                    case ShaderPropertyType.Vector:
                        var v = block.HasVector(id) ? block.GetVector(id) : m.GetVector(id);
                        for (int k = 0; k < 4; k++) key.Append(v[k].ToString("R", CultureInfo.InvariantCulture)).Append(',');
                        if (copy) copy.SetVector(id, v); break;
                    case ShaderPropertyType.Texture:
                        var t = block.HasTexture(id) ? block.GetTexture(id) : m.GetTexture(id);
                        if (n == "_MainTex")
                        {
                            if (block.HasTexture(id) && t != r.sprite.texture) throw new InvalidOperationException("MPB _MainTex override");
                            if (m.GetTextureScale(n) != Vector2.one || m.GetTextureOffset(n) != Vector2.zero)
                                throw new InvalidOperationException("nonidentity _MainTex scale/offset unsupported by atlas remapping");
                        }
                        else key.Append(t ? t.GetInstanceID() : 0);
                        key.Append(m.GetTextureScale(n).ToString("R")).Append(m.GetTextureOffset(n).ToString("R"));
                        if (copy) copy.SetTexture(id, t); break;
                    case ShaderPropertyType.Int:
                        int integer = block.HasInteger(id) ? block.GetInteger(id) : m.GetInteger(id);
                        key.Append(integer); if (copy) copy.SetInteger(id, integer); break;
                    default:
                        float f = block.HasFloat(id) ? block.GetFloat(id) : m.GetFloat(id);
                        key.Append(f.ToString("R", CultureInfo.InvariantCulture)); if (copy) copy.SetFloat(id, f); break;
                }
            }
            if ((m.HasProperty("_EnableExternalAlpha") && m.GetFloat("_EnableExternalAlpha") != 0)
                || (block.HasFloat("_EnableExternalAlpha") && block.GetFloat("_EnableExternalAlpha") != 0))
                throw new InvalidOperationException("external alpha unsupported");
            return key.ToString();
        }

        static string Sampler(Texture2D t) => t.graphicsFormat + "/" + t.wrapModeU + "/" + t.wrapModeV + "/"
            + t.filterMode + "/" + t.anisoLevel + "/" + t.mipMapBias.ToString("R", CultureInfo.InvariantCulture);
        void Place(Image image)
        {
            var t = image.Source; string sampler = Sampler(t);
            // Mipmapped textures use dedicated atlases: exact source dimensions/LOD and wrap,
            // no cross-image mip bleed. Mipless sources are shelf-packed without downscaling.
            bool dedicated = image.PreserveUV || t.mipmapCount > 1 || t.width + Padding * 2 > SystemInfo.maxTextureSize
                || t.height + Padding * 2 > SystemInfo.maxTextureSize;
            Atlas a = null;
            if (!dedicated)
            {
                foreach (var candidate in atlases)
                {
                    if (candidate.Dedicated || candidate.Sampler != sampler) continue;
                    int x = candidate.X, y = candidate.Y;
                    if (x + t.width + Padding * 2 > candidate.Width) { x = 0; y += candidate.Row; }
                    if (y + t.height + Padding * 2 > candidate.Height) continue;
                    if (x == 0 && candidate.X != 0) { candidate.Y = y; candidate.Row = 0; }
                    candidate.X = x; a = candidate; break;
                }
            }
            if (a == null)
            {
                // Begin with a bounded shelf, growing for larger sources without downscaling.
                int side = Mathf.Min(SystemInfo.maxTextureSize, Mathf.Max(2048,
                    Mathf.NextPowerOfTwo(Mathf.Max(t.width, t.height) + Padding * 2)));
                a = new Atlas { Width = dedicated ? t.width : side, Height = dedicated ? t.height : side,
                    Mips = t.mipmapCount, Dedicated = dedicated, Sampler = sampler,
                    Linear = !GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat) };
                atlases.Add(a);
            }
            image.Atlas = a; image.X = a.Dedicated ? 0 : a.X + Padding; image.Y = a.Dedicated ? 0 : a.Y + Padding;
            a.Images.Add(image);
            if (!a.Dedicated) { a.X += t.width + Padding * 2; a.Row = Mathf.Max(a.Row, t.height + Padding * 2); }
        }

        static int Wrapped(int i, int length, TextureWrapMode wrap)
        {
            if (wrap == TextureWrapMode.Clamp) return Mathf.Clamp(i, 0, length - 1);
            if (wrap == TextureWrapMode.MirrorOnce) return Mathf.Clamp(i < 0 ? -i - 1 : i, 0, length - 1);
            if (wrap == TextureWrapMode.Mirror)
            { int k = ((i % (length * 2)) + length * 2) % (length * 2); return k < length ? k : length * 2 - k - 1; }
            return ((i % length) + length) % length;
        }
        void BuildAtlas(Atlas a)
        {
            var source = a.Images[0].Source;
            if (!a.Dedicated)
            {
                // Trim empty shelf space; power-of-two sizes keep UV scaling stable.
                int w = 1, h = 1;
                foreach (var image in a.Images) { w = Mathf.Max(w, image.X + image.Source.width + Padding); h = Mathf.Max(h, image.Y + image.Source.height + Padding); }
                a.Width = Mathf.Min(a.Width, Mathf.NextPowerOfTwo(w)); a.Height = Mathf.Min(a.Height, Mathf.NextPowerOfTwo(h));
            }
            a.Texture = new Texture2D(a.Width, a.Height, TextureFormat.RGBA32, a.Mips, a.Linear)
            { name = "DecoRender atlas", hideFlags = HideFlags.HideAndDontSave, filterMode = source.filterMode,
                wrapModeU = source.wrapModeU, wrapModeV = source.wrapModeV, anisoLevel = source.anisoLevel, mipMapBias = source.mipMapBias };
            long bytes = 0;
            for (int mip = 0; mip < a.Mips; mip++)
            {
                int w = Mathf.Max(1, a.Width >> mip), h = Mathf.Max(1, a.Height >> mip);
                var pixels = new Color32[w * h]; bytes += (long)pixels.Length * 4;
                foreach (var image in a.Images)
                {
                    var t = image.Source; int sw = Mathf.Max(1, t.width >> mip), sh = Mathf.Max(1, t.height >> mip);
                    var decoded = Decode(t, mip, a.Linear);
                    int pad = a.Dedicated ? 0 : Padding;
                    for (int y = -pad; y < sh + pad; y++) for (int x = -pad; x < sw + pad; x++)
                        pixels[(image.Y + y) * w + image.X + x] = decoded[Wrapped(y, sh, t.wrapModeV) * sw + Wrapped(x, sw, t.wrapModeU)];
                }
                a.Texture.SetPixels32(pixels, mip);
            }
            a.Texture.Apply(false, true);
            Log("atlas=" + atlases.IndexOf(a) + " size=" + a.Width + "x" + a.Height + " RGBA32 mips=" + a.Mips
                + " gpu-bytes=" + bytes + " gpu-MiB=" + F(bytes / 1048576.0) + " sources=" + a.Images.Count
                + " padding=" + (a.Dedicated ? 0 : Padding) + " dedicated=" + a.Dedicated + " linear=" + a.Linear);
        }

        static Color32[] Decode(Texture2D source, int mip, bool linear)
        {
            int w = Mathf.Max(1, source.width >> mip), h = Mathf.Max(1, source.height >> mip);
            Texture2D single = null, read = null;
            RenderTexture rt = null; var old = RenderTexture.active; bool oldSRGB = GL.sRGBWrite;
            try
            {
                Texture input = source;
                if (source.mipmapCount > 1)
                {
                    // Isolate a source mip BEFORE Blit so LOD is explicit, including compressed sources.
                    single = new Texture2D(w, h, source.format, 1, linear);
                    Graphics.CopyTexture(source, 0, mip, single, 0, 0); input = single;
                }
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32,
                    linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = !linear && QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(input, rt); RenderTexture.active = rt;
                read = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false); read.Apply(false, false);
                return read.GetPixels32();
            }
            finally
            {
                GL.sRGBWrite = oldSRGB; RenderTexture.active = old;
                if (read) Destroy(read); if (single) Destroy(single); if (rt) RenderTexture.ReleaseTemporary(rt);
            }
        }

        void Update()
        {
            if (!initialized || stopping || comparing) return;
            if (Hitch.Playing) startedPlaying = true;
            if (!ValidSession()) Stop("scene/manager/list/camera/playback transition");
        }
        bool ValidSession() => manager && manager == scrDecorationManager.instance && mainCamera
            && scrCamera.instance && mainCamera == scrCamera.instance.camobj && !SceneReset.Resetting
            && SceneReset.Count == resetCount && SceneManager.GetActiveScene().handle == scene
            && ReferenceEquals(manager.allDecorations, decorationList) && manager.allDecorations.Count == decorationCount
            && (!startedPlaying || Hitch.Playing);

        void PreCull(Camera camera)
        {
            if (stopping || comparing || !initialized) return;
            try
            {
                if (!ValidSession()) { Stop("pre-cull transition"); return; }
                // Unity culls BEFORE onPreRender. Register current source bounds and enable the
                // replacement before that cull, including the very first frame. Vertex transforms
                // are still sampled later, exclusively in onPreRender after game LateUpdate.
                foreach (var g in groups)
                {
                    bool haveBounds = false;
                    foreach (var i in g.Items)
                    {
                        if (!i.D || !i.R) { Stop("owned decoration destroyed before cull"); return; }
                        if (!i.R.enabled || !i.R.gameObject.activeInHierarchy) continue;
                        if (!haveBounds) { g.CullBounds = i.R.bounds; haveBounds = true; }
                        else g.CullBounds.Encapsulate(i.R.bounds);
                    }
                    if (haveBounds)
                    {
                        g.CullBounds.Expand(0.01f);
                        g.Renderer.bounds = g.CullBounds; // Explicit world bounds survive mesh uploads.
                    }
                    g.Renderer.enabled = haveBounds;
                }
            }
            catch (Exception e) { Stop("pre-cull failed: " + e); }
        }

        void PreRender(Camera camera)
        {
            if (stopping || comparing || !initialized) return;
            try
            {
                if (!ValidSession()) { Stop("pre-render transition"); return; }
                int id = camera.GetInstanceID(); int count; cameras.TryGetValue(id, out count); cameras[id] = count + 1;
                if (count == 0) Log("camera id=" + id + " name=" + camera.name + " mask=" + camera.cullingMask
                    + " orthographic=" + camera.orthographic + " sortMode=" + camera.transparencySortMode
                    + " sortAxis=" + camera.transparencySortAxis + " graphicsSortMode=" + GraphicsSettings.transparencySortMode
                    + " graphicsSortAxis=" + GraphicsSettings.transparencySortAxis);
                Build(camera, true);
            }
            catch (Exception e) { Stop("pre-render failed: " + e); }
        }

        void Build(Camera camera, bool measure)
        {
            long start = Stopwatch.GetTimestamp();
            var mode = camera.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = camera.orthographic ? TransparencySortMode.Orthographic : TransparencySortMode.Perspective;
            var axis = camera.transparencySortMode == TransparencySortMode.CustomAxis ? camera.transparencySortAxis : GraphicsSettings.transparencySortAxis;
            foreach (var i in items)
            {
                if (!i.D || !i.R || i.R.sprite != i.Sprite || i.R.sharedMaterial != i.Source
                    || i.R.sortingLayerID != i.Layer || i.R.sortingOrder != i.Order || i.R.gameObject.layer != i.UnityLayer
                    || i.D.hitbox != 0 || i.D.isMask() || MeshOn(i.D) || i.R.maskInteraction != SpriteMaskInteraction.None
                    || i.D.repeatX != i.RepeatX || i.D.repeatY != i.RepeatY
                    || i.R.drawMode != SpriteDrawMode.Simple || i.R.GetComponentInParent<SortingGroup>()
                    || MaterialState(i.R, null) != i.Signature)
                    throw new InvalidOperationException("owned renderer destroyed or sprite/material/sorting/mask changed; re-run on");
                i.Matrix = i.R.transform.localToWorldMatrix; i.Color = i.R.color;
                i.FlipX = i.R.flipX; i.FlipY = i.R.flipY;
                i.Visible = i.R.enabled && i.R.gameObject.activeInHierarchy && RendererOn(i.D) && RendererColor(i.D).a != 0 && i.Color.a != 0;
                var point = i.R.spriteSortPoint == SpriteSortPoint.Pivot ? i.R.transform.position : i.R.bounds.center;
                i.Distance = mode == TransparencySortMode.CustomAxis ? Vector3.Dot(point, axis)
                    : mode == TransparencySortMode.Orthographic ? Vector3.Dot(point - camera.transform.position, camera.transform.forward)
                    : (point - camera.transform.position).sqrMagnitude;
            }
            foreach (var g in groups)
            {
                g.Items.Sort((a, b) => { int d = b.Distance.CompareTo(a.Distance); return d != 0 ? d : a.Id.CompareTo(b.Id); });
                g.Positions.Clear(); g.Colors.Clear(); g.UV.Clear(); g.Indices.Clear();
                foreach (var i in g.Items)
                {
                    if (!i.Visible) continue;
                    int offset = g.Positions.Count; var a = i.Image.Atlas;
                    for (int v = 0; v < i.Vertices.Length; v++)
                    {
                        var p = i.Vertices[v]; p.x *= i.FlipX ? -1 : 1; p.y *= i.FlipY ? -1 : 1;
                        g.Positions.Add(i.Matrix.MultiplyPoint3x4(new Vector3(p.x, p.y, 0))); g.Colors.Add(i.Color);
                        var uv = i.UV[v]; g.UV.Add(new Vector2((i.Image.X + uv.x * i.Image.Source.width) / a.Width,
                            (i.Image.Y + uv.y * i.Image.Source.height) / a.Height));
                    }
                    foreach (var t in i.Triangles) g.Indices.Add(offset + t);
                }
                g.Mesh.Clear(); g.Mesh.SetVertices(g.Positions); g.Mesh.SetColors(g.Colors); g.Mesh.SetUVs(0, g.UV);
                g.Mesh.SetTriangles(g.Indices, 0, true); g.Renderer.enabled = g.Indices.Count != 0;
            }
            foreach (var i in items) i.R.forceRenderingOff = true;
            if (measure)
            {
                long ticks = Stopwatch.GetTimestamp() - start; buildTicks += ticks; buildCount++;
                if (measuredFrame != Time.frameCount) { measuredFrame = Time.frameCount; measuredFrames++; frameTicks = 0; }
                frameTicks += ticks;
                lastBuildMs = ticks * 1000.0 / Stopwatch.Frequency;
            }
        }

        void PostRender(Camera camera)
        {
            if (stopping || comparing || pending == 0 || camera != mainCamera) return;
            pending = 0;
            try { Compare(); }
            catch (Exception e) { lastCompare = "compare failed: " + e; Stop(lastCompare); }
            finally { compareFinished = true; }
        }

        void Takeover(bool on)
        {
            if (on && stopping) return;
            foreach (var i in items) if (i.R) i.R.forceRenderingOff = on || i.WasOff;
            foreach (var g in groups) if (g.Renderer) g.Renderer.enabled = on && g.Indices.Count != 0;
        }
        bool SnapshotUnchanged()
        {
            foreach (var i in items)
                if (!i.D || !i.R || i.R.transform.localToWorldMatrix != i.Matrix || i.R.color != i.Color
                    || i.R.flipX != i.FlipX || i.R.flipY != i.FlipY || i.R.sprite != i.Sprite
                    || i.R.sharedMaterial != i.Source || i.R.sortingLayerID != i.Layer || i.R.sortingOrder != i.Order
                    || i.R.gameObject.layer != i.UnityLayer
                    || (i.R.enabled && i.R.gameObject.activeInHierarchy && RendererOn(i.D) && RendererColor(i.D).a != 0 && i.R.color.a != 0) != i.Visible
                    || MaterialState(i.R, null) != i.Signature) return false;
            return ValidSession();
        }
        void Compare()
        {
            comparing = true;
            GameObject go = null; RenderTexture rt = null; Texture2D a = null, b = null, diff = null;
            var old = RenderTexture.active; bool oldSRGB = GL.sRGBWrite;
            try
            {
                // Snapshot once after LateUpdate. No decoration UpdatePosition/logic calls here.
                Build(mainCamera, false);
                int w = mainCamera.pixelWidth, h = mainCamera.pixelHeight;
                if (w <= 0 || h <= 0) throw new InvalidOperationException("empty camera dimensions");
                go = new GameObject("DecoRender compare camera") { hideFlags = HideFlags.HideAndDontSave };
                var c = go.AddComponent<Camera>(); c.enabled = false; c.CopyFrom(mainCamera); c.enabled = false;
                c.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
                c.worldToCameraMatrix = mainCamera.worldToCameraMatrix; c.projectionMatrix = mainCamera.projectionMatrix;
                c.rect = new Rect(0, 0, 1, 1); c.stereoTargetEye = StereoTargetEyeMask.None;
                rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 1; c.targetTexture = rt;
                Takeover(false); ClearTarget(rt, c.backgroundColor); c.Render(); a = Read(rt);
                if (stopping) throw new InvalidOperationException("compare interrupted: " + lastStop);
                bool same = SnapshotUnchanged();
                Takeover(true); ClearTarget(rt, c.backgroundColor); c.Render(); b = Read(rt); same &= SnapshotUnchanged();
                if (stopping) throw new InvalidOperationException("compare interrupted: " + lastStop);
                var aa = a.GetPixels32(); var bb = b.GetPixels32(); var dd = new Color32[aa.Length];
                int[] thresholds = { 0, 1, 2, 4, 8 }; long[] counts = new long[5]; int[] max = new int[4];
                int greatest = -1, location = 0;
                for (int n = 0; n < aa.Length; n++)
                {
                    int dr = Math.Abs(aa[n].r - bb[n].r), dg = Math.Abs(aa[n].g - bb[n].g),
                        db = Math.Abs(aa[n].b - bb[n].b), da = Math.Abs(aa[n].a - bb[n].a);
                    max[0] = Math.Max(max[0], dr); max[1] = Math.Max(max[1], dg); max[2] = Math.Max(max[2], db); max[3] = Math.Max(max[3], da);
                    int d = Math.Max(Math.Max(dr, dg), Math.Max(db, da)); if (d > greatest) { greatest = d; location = n; }
                    for (int k = 0; k < 5; k++) if (d > thresholds[k]) counts[k]++;
                    // Include alpha-only differences in blue; output alpha remains opaque for inspection.
                    dd[n] = new Color32((byte)Math.Min(255, dr * 16), (byte)Math.Min(255, dg * 16), (byte)Math.Min(255, Math.Max(db, da) * 16), 255);
                }
                diff = new Texture2D(w, h, TextureFormat.RGBA32, false); diff.SetPixels32(dd); diff.Apply(false);
                Directory.CreateDirectory(outputDir);
                string name = "decorender-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture) + "-f" + Time.frameCount;
                string prefix = Path.Combine(outputDir, name);
                File.WriteAllBytes(prefix + "-original.png", a.EncodeToPNG()); File.WriteAllBytes(prefix + "-group.png", b.EncodeToPNG());
                File.WriteAllBytes(prefix + "-diff16.png", diff.EncodeToPNG());
                var result = new StringBuilder("frame=").Append(Time.frameCount).Append(" song=")
                    .Append(scrConductor.instance ? F(scrConductor.instance.songposition_minusi) : "n/a")
                    .Append(" size=").Append(w).Append('x').Append(h).Append(" maxRGBA=").Append(string.Join(",", max))
                    .Append(" max=").Append(greatest).Append(" xy-bottom-left=").Append(location % w).Append(',').Append(location / w)
                    .Append(" snapshotStable=").Append(same);
                for (int k = 0; k < 5; k++) result.Append(" >").Append(thresholds[k]).Append('=').Append(counts[k]).Append('/').Append(aa.Length)
                    .Append(" ratio=").Append(F((double)counts[k] / aa.Length));
                result.Append(" files=").Append(prefix).Append("-{original,group,diff16}.png")
                    .Append("; clone camera only; source image effects/command buffers/other cameras NOT copied; temporal/global callbacks may differ; performance UNMEASURED");
                lastCompare = result.ToString(); File.WriteAllText(prefix + "-metrics.txt", lastCompare, Encoding.UTF8);
            }
            finally
            {
                if (!stopping) Takeover(true);
                RenderTexture.active = old; GL.sRGBWrite = oldSRGB;
                if (go) { var c = go.GetComponent<Camera>(); if (c) c.targetTexture = null; Destroy(go); }
                if (rt) RenderTexture.ReleaseTemporary(rt);
                if (a) Destroy(a); if (b) Destroy(b); if (diff) Destroy(diff); comparing = false;
            }
        }
        static void ClearTarget(RenderTexture rt, Color color)
        { RenderTexture.active = rt; GL.Clear(true, true, color); }
        static Texture2D Read(RenderTexture rt)
        {
            RenderTexture.active = rt; var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            try { t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false); t.Apply(false); return t; }
            catch { Destroy(t); throw; }
        }
        internal static string State()
        {
            var s = session; var result = new StringBuilder("active=").Append((bool)s);
            if (s)
            {
                result.Append(" managed=").Append(s.items.Count).Append(" groups=").Append(s.groups.Count)
                    .Append(" vertices=").Append(s.vertexCount).Append(" atlases=").Append(s.atlases.Count);
                foreach (var a in s.atlases) result.Append(' ').Append(a.Width).Append('x').Append(a.Height).Append("/mips=").Append(a.Mips);
                result.Append(" build-ms/last-camera=").Append(F(s.lastBuildMs)).Append(" build-ms/mean-camera=")
                    .Append(F(s.buildTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, s.buildCount)))
                    .Append(" build-ms/current-frame=").Append(F(s.frameTicks * 1000.0 / Stopwatch.Frequency))
                    .Append(" build-ms/mean-frame=").Append(F(s.buildTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, s.measuredFrames)))
                    .Append(" builds=").Append(s.buildCount).Append(" cameras=").Append(s.cameras.Count).Append(" comparePending=").Append(s.pending);
            }
            return result.Append(" stop=").Append(lastStop).Append(" lastCompare={").Append(lastCompare).Append('}').ToString();
        }
        internal static void Stop() => Stop("off/unload/reset");
        static void Stop(string reason)
        {
            var s = session; if (!s || s.stopping) return;
            s.stopping = true; lastStop = reason;
            Camera.onPreCull -= s.PreCull; Camera.onPreRender -= s.PreRender; Camera.onPostRender -= s.PostRender;
            if (s.pending != 0) { lastCompare = "cancelled: " + reason; compareFinished = true; }
            // Disable immediately: Destroy is deferred until end of frame.
            foreach (var g in s.groups) if (g.Renderer) g.Renderer.enabled = false;
            foreach (var i in s.items) if (i.R) i.R.forceRenderingOff = i.WasOff;
            foreach (var g in s.groups)
            { if (g.Object) Destroy(g.Object); if (g.Mesh) Destroy(g.Mesh); if (g.Material) Destroy(g.Material); }
            foreach (var a in s.atlases) if (a.Texture) Destroy(a.Texture);
            session = null; Destroy(s.gameObject); Log("stopped: " + reason + "; original forceRenderingOff restored");
        }
        void OnDestroy() { if (session == this) Stop("session object destroyed"); }
    }
}
#endif
