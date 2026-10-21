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
            internal int Width, Height;
            internal FilterMode Filter;
            internal TextureWrapMode U, V;
            internal bool Valid => Source && Source.width == Width && Source.height == Height && Source.filterMode == Filter
                && Source.wrapModeU == U && Source.wrapModeV == V && Source.mipmapCount == 1
                && Source.anisoLevel == Page.Aniso && GraphicsFormatUtility.IsSRGBFormat(Source.graphicsFormat) == Page.Srgb;
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
        readonly Dictionary<Texture2D, Entry> planned = new Dictionary<Texture2D, Entry>();
        readonly List<Page> pages = new List<Page>();
        internal int Pages { get; private set; }
        internal int Count => entries.Count;
        internal long Bytes { get; private set; }
        // Bounded experiment, no resizing/repacking an already referenced atlas.
        long reservedBytes;
        internal void Plan(IEnumerable<Texture2D> sources)
        {
            if (pages.Count != 0 || planned.Count != 0 || entries.Count != 0) throw new InvalidOperationException("atlas-already-planned");
            var all = sources.Distinct().ToArray();
            foreach (var group in all.GroupBy(t => Reason(t) ?? "eligible"))
                LiveDeco.Log("atlas_source reason=" + group.Key + " textures=" + group.Count());
            var textures = all.Where(t => Reason(t) == null).ToArray();
            var keys = new List<(FilterMode Filter, bool Srgb, int Aniso)>();
            var specs = new LiveDecoAtlasPolicy.Source[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                var t = textures[i]; var key = (t.filterMode, GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat), t.anisoLevel);
                int group = keys.IndexOf(key); if (group < 0) { group = keys.Count; keys.Add(key); }
                specs[i] = new LiveDecoAtlasPolicy.Source(t.width, t.height, group);
            }
            long paddedBytes = specs.Sum(s => (long)LiveDecoAtlasPolicy.Align(s.Width + 4) * LiveDecoAtlasPolicy.Align(s.Height + 4) * 4);
            LiveDeco.Log("atlas_plan version=5 textures=" + textures.Length + " sampler_groups=" + keys.Count
                + " padded_bytes=" + paddedBytes + " budget_bytes=" + LiveDecoAtlasPolicy.ByteLimit + " max_size=" + SystemInfo.maxTextureSize);
            foreach (var group in specs.GroupBy(s => s.Group))
            {
                var key = keys[group.Key];
                LiveDeco.Log("atlas_sampler group=" + group.Key + " filter=" + key.Filter + " srgb=" + key.Srgb + " aniso=" + key.Aniso
                    + " textures=" + group.Count() + " max_width=" + group.Max(s => s.Width) + " max_height=" + group.Max(s => s.Height)
                    + " padded_bytes=" + group.Sum(s => (long)LiveDecoAtlasPolicy.Align(s.Width + 4) * LiveDecoAtlasPolicy.Align(s.Height + 4) * 4));
            }
            LiveDecoAtlasPolicy.Layout layout;
            try { layout = LiveDecoAtlasPolicy.Plan(specs, SystemInfo.maxTextureSize); }
            catch (Exception e) { LiveDeco.Log("atlas_plan_failed reason=" + e.Message + " allocated_bytes=" + Bytes); throw; }
            for (int i = 0; i < layout.Pages.Count; i++)
            {
                var key = keys[layout.Groups[i]]; var shelf = layout.Pages[i];
                pages.Add(new Page { Shelf = shelf, Filter = key.Filter, Srgb = key.Srgb, Aniso = key.Aniso });
                LiveDeco.Log("atlas_plan_page index=" + i + " size=" + shelf.Size + " filter=" + key.Filter
                    + " srgb=" + key.Srgb + " aniso=" + key.Aniso + " bytes=" + (long)shelf.Size * shelf.Size * 4);
            }
            for (int i = 0; i < textures.Length; i++)
            { var slot = layout.Slots[i]; planned.Add(textures[i], MakeEntry(textures[i], pages[slot.Page], slot.X, slot.Y)); }
            reservedBytes = layout.Bytes;
            LiveDeco.Log("atlas_plan_ready pages=" + pages.Count + " reserved_bytes=" + reservedBytes + " allocated_bytes=" + Bytes);
        }
        static Entry MakeEntry(Texture2D t, Page p, int x, int y) => new Entry { Source = t, Page = p, X = x, Y = y,
            Width = t.width, Height = t.height, Filter = t.filterMode, U = t.wrapModeU, V = t.wrapModeV };
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
            if (entries.TryGetValue(t, out var entry))
            { if (!entry.Valid) throw new InvalidOperationException("atlas-input-changed"); return entry; }
            string why = Reason(t); if (why != null) throw new InvalidOperationException(why);
            bool srgb = GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat);
            int x = 0, y = 0; Page p = null;
            if (planned.TryGetValue(t, out entry))
            {
                if (!entry.Valid) throw new InvalidOperationException("atlas-plan-input-changed");
                p = entry.Page; x = entry.X; y = entry.Y;
            }
            else
            {
                // New live textures may use unreserved space, never coordinates
                // promised to an initial source, even if it has not been admitted.
                foreach (var candidate in pages)
                    if (candidate.Filter == t.filterMode && candidate.Srgb == srgb && candidate.Aniso == t.anisoLevel && candidate.Shelf.Place(t.width, t.height, out x, out y)) { p = candidate; break; }
                if (p == null)
                {
                    int size = Math.Max(Math.Min(2048, SystemInfo.maxTextureSize), Mathf.NextPowerOfTwo(Math.Max(t.width, t.height) + 4));
                    long bytes = (long)size * size * 4;
                    LiveDeco.Log("atlas_growth size=" + size + " request_bytes=" + bytes + " reserved_bytes=" + reservedBytes + " allocated_bytes=" + Bytes);
                    if (size > SystemInfo.maxTextureSize || reservedBytes + bytes > LiveDecoAtlasPolicy.ByteLimit)
                        throw new InvalidOperationException("atlas-vram-budget-512MiB");
                    p = new Page { Filter = t.filterMode, Srgb = srgb, Aniso = t.anisoLevel, Shelf = new LiveDecoAtlasPolicy.Shelf(size) };
                    pages.Add(p); reservedBytes += bytes;
                    if (!p.Shelf.Place(t.width, t.height, out x, out y)) throw new InvalidOperationException("atlas-placement");
                }
                entry = MakeEntry(t, p, x, y);
                planned.Add(t, entry);
            }
            if (!p.Texture)
            {
                int size = p.Shelf.Size;
                // Descriptor is owned before the allocation; Dispose also covers
                // failure during readback/extrusion/upload after construction.
                p.Texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb) {
                    name = "StutterFix.LiveDecoAtlas", filterMode = t.filterMode, wrapMode = TextureWrapMode.Clamp, anisoLevel = t.anisoLevel,
                    hideFlags = HideFlags.HideAndDontSave };
                Bytes += (long)size * size * 4; Pages++;
            }
            // Exact compressed interior copies cannot provide a true two-pixel
            // edge extrusion for arbitrary DXT blocks without re-encoding them.
            // Use the explicitly allowed RGBA32 path, never silently recompress.
            var pixels = Read(t, srgb);
            var extruded = LiveDecoAtlasPolicy.Extrude(pixels, t.width, t.height);
            p.Texture.SetPixels32(x - 2, y - 2, LiveDecoAtlasPolicy.Align(t.width + 4), LiveDecoAtlasPolicy.Align(t.height + 4), extruded);
            p.Dirty = true;
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
        { foreach (var p in pages) if (p.Texture) UnityEngine.Object.Destroy(p.Texture); pages.Clear(); entries.Clear(); planned.Clear(); Bytes = reservedBytes = 0; Pages = 0; }
    }
}
#endif
