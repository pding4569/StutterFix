#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace StutterFix
{
    internal sealed class LiveDecoAtlas : IDisposable
    {
        internal sealed class Entry
        {
            internal Page Page;
            internal Texture2D Source;
            internal int X, Y;
            internal FilterMode Filter;
            internal TextureWrapMode U, V;
            internal bool Valid => Source && Source.filterMode == Filter && Source.wrapModeU == U && Source.wrapModeV == V && Source.mipmapCount == 1;
            internal Vector2 Map(Vector2 uv) => new Vector2(LiveDecoAtlasPolicy.UV(X, Source.width, Page.Texture.width, uv.x), LiveDecoAtlasPolicy.UV(Y, Source.height, Page.Texture.height, uv.y));
        }
        internal sealed class Page
        {
            internal Texture2D Texture;
            internal LiveDecoAtlasPolicy.Shelf Shelf;
            internal FilterMode Filter;
            internal bool Srgb;
            internal int Aniso;
            internal bool Dirty;
        }
        readonly Dictionary<Texture2D, Entry> entries = new Dictionary<Texture2D, Entry>();
        readonly List<Page> pages = new List<Page>();
        internal int Pages => pages.Count;
        internal int Count => entries.Count;
        internal long Bytes { get; private set; }
        // Bounded experiment, no resizing/repacking an already referenced atlas.
        const long ByteLimit = 512L * 1024 * 1024;
        int preferredSize = 2048;
        internal void Plan(IEnumerable<Texture2D> sources)
        {
            // Prefer one page for the initial live set. Never grow an atlas that
            // has already supplied UVs. Later live entries may need another page.
            var textures = sources.Where(t => Reason(t) == null).Distinct()
                .OrderByDescending(t => t.height).ThenByDescending(t => t.width).ToArray();
            int limit = SystemInfo.maxTextureSize;
            for (int size = Math.Min(2048, limit); size <= limit; size *= 2)
            {
                var shelf = new LiveDecoAtlasPolicy.Shelf(size); bool fits = true;
                foreach (var t in textures) if (!shelf.Place(t.width, t.height, out _, out _)) { fits = false; break; }
                preferredSize = size;
                if (fits || size > limit / 2) break;
            }
        }
        internal static string Reason(Texture2D t)
        {
            if (!t) return "atlas-missing-texture";
            if (t.mipmapCount != 1) return "atlas-mip-chain";
            if (t.wrapModeU != TextureWrapMode.Clamp || t.wrapModeV != TextureWrapMode.Clamp) return "atlas-wrap-nonclamp";
            if (t.filterMode == FilterMode.Trilinear) return "atlas-trilinear";
            if (t.width + 4 > SystemInfo.maxTextureSize || t.height + 4 > SystemInfo.maxTextureSize) return "atlas-max-size";
            return null;
        }
        internal Entry Get(Texture2D t)
        {
            if (entries.TryGetValue(t, out var entry)) return entry;
            string why = Reason(t); if (why != null) throw new InvalidOperationException(why);
            bool srgb = GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat);
            int x = 0, y = 0; Page p = null;
            foreach (var candidate in pages)
                if (candidate.Filter == t.filterMode && candidate.Srgb == srgb && candidate.Aniso == t.anisoLevel && candidate.Shelf.Place(t.width, t.height, out x, out y)) { p = candidate; break; }
            if (p == null)
            {
                int size = Math.Min(SystemInfo.maxTextureSize, Math.Max(preferredSize, Mathf.NextPowerOfTwo(Math.Max(t.width, t.height) + 4)));
                long bytes = (long)size * size * 4;
                if (Bytes + bytes > ByteLimit) throw new InvalidOperationException("atlas-vram-budget-512MiB");
                p = new Page { Filter = t.filterMode, Srgb = srgb, Aniso = t.anisoLevel, Shelf = new LiveDecoAtlasPolicy.Shelf(size) };
                pages.Add(p); // every allocation is owned before the next operation can throw
                p.Texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb) {
                    name = "StutterFix.LiveDecoAtlas", filterMode = t.filterMode, wrapMode = TextureWrapMode.Clamp, anisoLevel = t.anisoLevel,
                    hideFlags = HideFlags.HideAndDontSave };
                if (!p.Shelf.Place(t.width, t.height, out x, out y)) throw new InvalidOperationException("atlas-placement");
                Bytes += bytes;
            }
            // Exact compressed interior copies cannot provide a true two-pixel
            // edge extrusion for arbitrary DXT blocks without re-encoding them.
            // Use the explicitly allowed RGBA32 path, never silently recompress.
            var pixels = Read(t, srgb);
            var extruded = LiveDecoAtlasPolicy.Extrude(pixels, t.width, t.height);
            p.Texture.SetPixels32(x - 2, y - 2, LiveDecoAtlasPolicy.Align(t.width + 4), LiveDecoAtlasPolicy.Align(t.height + 4), extruded);
            p.Dirty = true;
            entry = new Entry { Source = t, Page = p, X = x, Y = y, Filter = t.filterMode, U = t.wrapModeU, V = t.wrapModeV };
            entries.Add(t, entry); return entry;
        }
        static Color32[] Read(Texture2D t, bool srgb)
        {
            if (t.isReadable && t.format == TextureFormat.RGBA32) return t.GetPixels32();
            RenderTexture rt = null; Texture2D read = null;
            var previous = RenderTexture.active; bool previousWrite = GL.sRGBWrite;
            try
            {
                rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                GL.sRGBWrite = srgb && QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(t, rt); RenderTexture.active = rt;
                read = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false, !srgb);
                read.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0, false);
                return read.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous; GL.sRGBWrite = previousWrite;
                if (read) UnityEngine.Object.Destroy(read);
                if (rt) RenderTexture.ReleaseTemporary(rt);
            }
        }
        internal void Upload()
        { foreach (var p in pages) if (p.Dirty) { p.Texture.Apply(false, false); p.Dirty = false; } }
        public void Dispose()
        { foreach (var p in pages) if (p.Texture) UnityEngine.Object.Destroy(p.Texture); pages.Clear(); entries.Clear(); Bytes = 0; }
    }
}
#endif
