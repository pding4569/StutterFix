#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Events;
using Stopwatch=System.Diagnostics.Stopwatch;
namespace StutterFix
{
    // Command-only research. Live preparation/activation is explicitly named;
    // it does not add per-frame polling. Unobserved live intervals are not a
    // whole-song equivalence guarantee. Originals stay allocated/unmodified.
    // One preparation dispatch per texture, four asynchronous validation bytes.
    // This representation lowers sampling payload, NOT total allocated VRAM.
    internal static class SpriteChannelResearch
    {
        sealed class Image { internal Texture2D Source;internal RenderTexture Packed;internal ComputeBuffer Invalid;internal bool Valid;internal uint Cells;internal int Width,Height,Aniso;internal FilterMode Filter;internal TextureWrapMode WrapU,WrapV;internal Vector4 AlphaRows; }
        sealed class Entry { internal Renderer Renderer;internal Material Material;internal Shader Original;internal Image Image;internal bool Retired,CallbackRegistered;internal (int,int,int) InitialBucket; }
        static readonly Dictionary<Texture2D,Image> images=new Dictionary<Texture2D,Image>();
        static readonly List<Entry> entries=new List<Entry>(),active=new List<Entry>();
        static readonly HashSet<(int,int,int)> excludedBuckets=new HashSet<(int,int,int)>();
        static readonly Dictionary<SpriteRenderer,Entry> watched=new Dictionary<SpriteRenderer,Entry>();
        static readonly UnityAction<SpriteRenderer> changedCallback=SpriteChanged;
        static long callbackCount,callbackRestored,callbackTicks,callbackMaxTicks,registered,unregistered;
        static string callbackError="none";
        static AssetBundle bundle;static Shader shader;static ComputeShader pack;
        static bool stopping,maskMode,rowMode;static int pending,invalid,errors,rejected,barriers;static long payload;
        const int Grid=128;
        static string Keyword=>rowMode?"SF_ALPHA_ROWS":maskMode?"SF_EMPTY_MASK":"SF_PACKED_GRAY";
        static readonly int PackedId=Shader.PropertyToID("_SfPackedTex"),MainId=Shader.PropertyToID("_MainTex");
        static readonly int RowsId=Shader.PropertyToID("_SfAlphaRows");
        static (int,int,int) Bucket(Entry e)=>(e.Renderer.sortingLayerID,e.Renderer.sortingOrder,e.Material.renderQueue);
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static Texture2D Source(Renderer r)
        {
            var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);
            if(block.GetTexture(PackedId))throw new InvalidOperationException("foreign packed texture property");
            var texture=block.GetTexture(MainId) as Texture2D;
            return texture?texture:((SpriteRenderer)r).sprite.texture;
        }
        internal static string Command(string arg)
        {
            if(arg=="state"){DescribeChangedSources();return State();}
            if(arg=="stop"){Stop();return State();}
            if(arg=="off"){Restore();return State();}
            if(arg=="prepare"||arg=="prepare mask"||arg=="prepare mask live"||arg=="prepare rows")
            {
                bool live=arg=="prepare mask live";
                if(!shader||!pack||(!GpuPlaybackState.Paused&&!(live&&Hitch.Playing))||entries.Count!=0||pending!=0||images.Count!=0||stopping)throw new InvalidOperationException("inactive loaded paused or explicit live candidate required");
                rowMode=arg=="prepare rows";maskMode=arg.StartsWith("prepare mask",StringComparison.Ordinal);
                var format=maskMode?GraphicsFormat.R8_UNorm:GraphicsFormat.R8G8_UNorm;
                if(!shader.keywordSpace.FindKeyword(Keyword).isValid)throw new InvalidOperationException("requested representation shader unavailable");
                if(QualitySettings.activeColorSpace!=ColorSpace.Gamma||!SystemInfo.supportsAsyncGPUReadback||!SystemInfo.supportsComputeShaders
                    ||(!rowMode&&(!SystemInfo.IsFormatSupported(format,FormatUsage.LoadStore)
                    ||!SystemInfo.IsFormatSupported(format,FormatUsage.Sample))))throw new InvalidOperationException("unsupported auxiliary format/color space");
                var eligible=SpriteDiscardResearch.Eligible(out rejected,out barriers);
                try
                {
                    foreach(var row in eligible)
                    {
                        var source=Source(row.Key);
                        if(!source||source.graphicsFormat!=GraphicsFormat.R8G8B8A8_UNorm||source.mipmapCount!=1
                            ||((maskMode||rowMode)&&(source.anisoLevel>1||(source.wrapModeU!=TextureWrapMode.Clamp&&source.wrapModeU!=TextureWrapMode.Repeat)||(source.wrapModeV!=TextureWrapMode.Clamp&&source.wrapModeV!=TextureWrapMode.Repeat)))
                            ||(rowMode&&source.filterMode!=FilterMode.Bilinear&&source.filterMode!=FilterMode.Point))
                        {rejected++;excludedBuckets.Add((row.Key.sortingLayerID,row.Key.sortingOrder,row.Value.renderQueue));continue;}
                        if(!images.TryGetValue(source,out var image))
                        {
                            int width=maskMode?Grid:source.width,height=maskMode?Grid:source.height;
                            long bytes=rowMode?8:checked((long)width*height*(maskMode?1:2));
                            if(images.Count>=32||payload+bytes>128L*1024*1024)throw new InvalidOperationException("bounded channel preparation budget exceeded");
                            image=new Image{Source=source,Width=source.width,Height=source.height,Filter=source.filterMode,WrapU=source.wrapModeU,WrapV=source.wrapModeV,Aniso=source.anisoLevel};images.Add(source,image);payload+=bytes;
                            int kernel=pack.FindKernel(rowMode?"AlphaRows":maskMode?"MaskCells":"Pack");
                            pack.SetInt("Width",source.width);pack.SetInt("Height",source.height);
                            if(rowMode)
                            {
                                image.Invalid=new ComputeBuffer(2,4);
                                image.Invalid.SetData(new uint[]{(uint)source.height,0});
                                pack.SetBuffer(kernel,"Rows",image.Invalid);
                            }
                            else
                            {
                                image.Packed=new RenderTexture(new RenderTextureDescriptor(width,height){graphicsFormat=format,depthBufferBits=0,msaaSamples=1,enableRandomWrite=true,useMipMap=false,autoGenerateMips=false})
                                {name=maskMode?"SF research empty-cell mask":"SF research gray-alpha",filterMode=maskMode?FilterMode.Point:source.filterMode,wrapModeU=source.wrapModeU,wrapModeV=source.wrapModeV,anisoLevel=maskMode?0:source.anisoLevel};
                                if(!image.Packed.Create())throw new InvalidOperationException("channel texture creation failed");
                                image.Invalid=new ComputeBuffer(1,4);image.Invalid.SetData(new uint[]{0});
                                pack.SetTexture(kernel,maskMode?"Mask":"Packed",image.Packed);
                                pack.SetBuffer(kernel,"Invalid",image.Invalid);
                            }
                            if(maskMode){pack.SetInt("Grid",Grid);pack.SetInt("WrapU",source.wrapModeU==TextureWrapMode.Repeat?1:0);pack.SetInt("WrapV",source.wrapModeV==TextureWrapMode.Repeat?1:0);}
                            pack.SetTexture(kernel,"Source",source);
                            pack.Dispatch(kernel,(width+7)/8,(height+7)/8,1);
                            var captured=image;pending++;
                            try{AsyncGPUReadback.Request(image.Invalid,request=>Completed(captured,request));}
                            catch{pending--;throw;}
                        }
                        entries.Add(new Entry{Renderer=row.Key,Material=row.Value,Original=row.Value.shader,Image=image,InitialBucket=(row.Key.sortingLayerID,row.Key.sortingOrder,row.Value.renderQueue)});
                    }
                    if(entries.Count==0)throw new InvalidOperationException("no compatible channel entries");
                }
                catch{Stop();throw;}
                return State();
            }
            if(arg=="on"||arg=="on live"||arg=="control on"||arg=="control off")
            {
                bool live=arg=="on live";
                if(!shader||(!GpuPlaybackState.Paused&&!(live&&maskMode&&Hitch.Playing))||pending!=0||errors!=0||stopping)throw new InvalidOperationException("validated paused or explicit live mask candidate required");
                if(arg!="on"&&!live)
                {
                    if(active.Count==0)throw new InvalidOperationException("active channel candidate required");
                    foreach(var e in active)if(e.Material&&e.Material.shader==shader){if(arg=="control on")e.Material.EnableKeyword("SF_CHANNEL_CONTROL");else e.Material.DisableKeyword("SF_CHANNEL_CONTROL");}
                    return State();
                }
                if(active.Count!=0)throw new InvalidOperationException("channel candidate already active");
                var forbidden=new HashSet<(int,int,int)>(entries.Where(e=>!e.Image.Valid||e.Retired).Select(Bucket));
                forbidden.UnionWith(excludedBuckets);
                try
                {
                    foreach(var e in entries)
                    {
                        if(e.Retired||!e.Image.Valid||forbidden.Contains(Bucket(e)))continue;
                        if(!e.Renderer||e.Renderer.sharedMaterial!=e.Material||e.Material.shader!=e.Original||!MatchesSource(e))throw new InvalidOperationException("source ownership/sampler changed during preparation");
                        float x=e.Material.GetFloat("RepeatX"),y=e.Material.GetFloat("RepeatY");var c=e.Material.GetColor("_Color");
                        if(!Finite(x)||!Finite(y)||!Finite(c.r)||!Finite(c.g)||!Finite(c.b)||!Finite(c.a))throw new InvalidOperationException("nonfinite source properties");
                        active.Add(e);var sr=(SpriteRenderer)e.Renderer;watched.Add(sr,e);e.CallbackRegistered=true;sr.RegisterSpriteChangeCallback(changedCallback);registered++;
                        e.Material.shader=shader;
                        if(rowMode)e.Material.SetVector(RowsId,e.Image.AlphaRows);
                        else e.Material.SetTexture(PackedId,e.Image.Packed);
                        e.Material.EnableKeyword(Keyword);
                        if(e.Material.GetFloat("RepeatX")!=x||e.Material.GetFloat("RepeatY")!=y||!e.Material.GetColor("_Color").Equals(c)||e.Material.renderQueue!=3000)throw new InvalidOperationException("source material values changed");
                    }
                    if(active.Count==0)throw new InvalidOperationException("no validated gray buckets");
                }
                catch{Restore();throw;}
                return State();
            }
            if(!Path.IsPathRooted(arg)||!File.Exists(arg))throw new ArgumentException("spritechannels <bundle>|prepare|on|off|state|control on/off|stop");
            Stop();if(pending!=0||bundle)throw new InvalidOperationException("previous pending/foreign resources retained");stopping=false;
            try
            {
                bundle=AssetBundle.LoadFromFile(arg);if(!bundle)throw new InvalidOperationException("channel bundle unavailable");
                shader=bundle.LoadAsset<Shader>("Assets/Shaders/SpriteChannels.shader");pack=bundle.LoadAsset<ComputeShader>("Assets/Shaders/SpriteChannels.compute");
                if(!shader||!shader.isSupported||!pack||!shader.keywordSpace.FindKeyword("SF_PACKED_GRAY").isValid)throw new InvalidOperationException("channel shaders unavailable");
            }
            catch{Stop();throw;}
            return State();
        }
        static void Completed(Image image,AsyncGPUReadbackRequest request)
        {
            try
            {
                if(request.hasError){errors++;return;}
                var data=request.GetData<uint>();
                if(rowMode)
                {
                    int first=(int)data[0],end=(int)data[1],height=image.Height;
                    if(first<0||first>=end||end>height){invalid++;return;}
                    float lower=Mathf.Max(0,first-2)/(float)height,upper=Mathf.Min(height,end+2)/(float)height;
                    if(upper-lower>=.97f){invalid++;return;}
                    image.AlphaRows=new Vector4(lower,upper,2f/height,image.WrapV==TextureWrapMode.Repeat?1:0);
                    image.Valid=true;
                }
                else
                {
                    image.Cells=data[0];
                    if(maskMode?image.Cells>Grid*Grid*3/4:image.Cells!=0)invalid++;else image.Valid=true;
                }
            }
            catch{errors++;}
            finally{pending--;if(stopping&&pending==0)Release();}
        }
        static bool MatchesSource(Entry e)
        {
            if(!e.Renderer||!e.Image.Source||Source(e.Renderer)!=e.Image.Source)return false;
            var t=e.Image.Source;return t.width==e.Image.Width&&t.height==e.Image.Height&&t.filterMode==e.Image.Filter&&t.wrapModeU==e.Image.WrapU&&t.wrapModeV==e.Image.WrapV&&t.anisoLevel==e.Image.Aniso&&t.mipmapCount==1&&t.graphicsFormat==GraphicsFormat.R8G8B8A8_UNorm;
        }
        static void SpriteChanged(SpriteRenderer renderer)
        {
            if(!watched.TryGetValue(renderer,out var changed))return;
            long begin=Stopwatch.GetTimestamp();callbackCount++;
            try
            {
                // Restore whole connected old/current sorting buckets, never
                // leave a newly mixed shader tie. No texture build/readback,
                // scene scan, per-frame polling, file IO or log in this path.
                var buckets=new HashSet<(int,int,int)>{changed.InitialBucket,Bucket(changed)};
                bool expanded;do{expanded=false;foreach(var e in active)if(buckets.Contains(e.InitialBucket)||buckets.Contains(Bucket(e))){expanded|=buckets.Add(e.InitialBucket);expanded|=buckets.Add(Bucket(e));}}while(expanded);
                for(int i=active.Count-1;i>=0;i--)
                {
                    var e=active[i];if(!buckets.Contains(e.InitialBucket)&&!buckets.Contains(Bucket(e)))continue;
                    e.Retired=true;RestoreEntry(e);active.RemoveAt(i);callbackRestored++;
                }
            }
            catch(Exception ex)
            {
                errors++;callbackError=ex.GetType().Name;
                try{Restore();}catch(Exception restore){callbackError+="/"+restore.GetType().Name;}
            }
            finally{long ticks=Stopwatch.GetTimestamp()-begin;callbackTicks+=ticks;if(ticks>callbackMaxTicks)callbackMaxTicks=ticks;}
        }
        static void RestoreEntry(Entry e)
        {
            Exception first=null;
            try{if(e.CallbackRegistered){var sr=e.Renderer as SpriteRenderer;if(sr)sr.UnregisterSpriteChangeCallback(changedCallback);e.CallbackRegistered=false;if(!ReferenceEquals(sr,null))watched.Remove(sr);unregistered++;}}
            catch(Exception ex){first=ex;}
            try{if(e.Material&&e.Material.shader==shader){e.Material.DisableKeyword("SF_PACKED_GRAY");e.Material.DisableKeyword("SF_EMPTY_MASK");e.Material.DisableKeyword("SF_ALPHA_ROWS");e.Material.DisableKeyword("SF_CHANNEL_CONTROL");if(rowMode)e.Material.SetVector(RowsId,new Vector4(0,1,0,0));else e.Material.SetTexture(PackedId,null);e.Material.shader=e.Original;if(e.Material.shader!=e.Original)throw new InvalidOperationException("channel shader restoration failed");}}
            catch(Exception ex){if(first==null)first=ex;}
            if(first!=null)throw new InvalidOperationException("callback/material restoration unconfirmed",first);
        }
        static void DescribeChangedSources()
        {
            // Explicit state command only, outside FPS markers; never a hook.
            foreach(var e in active)
            {
                try
                {
                    if(MatchesSource(e))continue;var now=e.Renderer?Source(e.Renderer):null;var old=e.Image;
                    Main.Entry.Logger.Log("[spritechannels] SOURCE_CHANGED renderer="+(e.Renderer?e.Renderer.GetInstanceID():0)
                        +" expected_texture="+(old.Source?old.Source.GetInstanceID():0)+":"+(old.Source?Uri.EscapeDataString(old.Source.name):"destroyed")
                        +" expected_size="+old.Width+"x"+old.Height+" expected_sampler="+old.Filter+","+old.WrapU+","+old.WrapV+","+old.Aniso
                        +" actual_texture="+(now?now.GetInstanceID():0)+":"+(now?Uri.EscapeDataString(now.name):"missing")
                        +" actual_size="+(now?now.width+"x"+now.height:"missing")+" actual_sampler="+(now?now.filterMode+","+now.wrapModeU+","+now.wrapModeV+","+now.anisoLevel:"missing")
                        +" actual_format="+(now?now.graphicsFormat.ToString():"missing"));
                }
                catch(Exception ex){Main.Entry.Logger.Log("[spritechannels] SOURCE_CHANGED detail_error="+ex.GetType().Name);}
            }
        }
        static string State()=>"SPRITE_CHANNELS loaded="+(bool)shader+" pending="+pending+" prepared="+images.Count+" valid="+images.Values.Count(i=>i.Valid)+" nongray="+(maskMode||rowMode?0:invalid)+" errors="+errors
            +" selected="+active.Count+" renderer_bound="+active.Count(e=>e.Renderer&&e.Material&&e.Renderer.sharedMaterial==e.Material&&e.Material.shader==shader
                &&(rowMode?e.Material.GetVector(RowsId).Equals(e.Image.AlphaRows):e.Material.GetTexture(PackedId)==e.Image.Packed)&&e.Material.IsKeywordEnabled(Keyword))
            +" representation="+(rowMode?"conservative_alpha_rows":maskMode?"conservative_empty_cells":"gray_alpha")+" dense_masks="+(maskMode?invalid:0)+" mask_occupied_cells="+(maskMode?images.Values.Sum(i=>(long)i.Cells):0)
            +" source_sampler_valid="+active.Count(MatchesSource)
            +(rowMode?" active_row_textures="+string.Join(",",active.Select(e=>e.Image).Distinct().Select(i=>
                Uri.EscapeDataString(i.Source.name)+":"+i.AlphaRows.x.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)
                +".."+i.AlphaRows.y.ToString("F4",System.Globalization.CultureInfo.InvariantCulture))):"")
            +" retired="+entries.Count(e=>e.Retired)+" callback_active="+entries.Count(e=>e.CallbackRegistered)+" callback_events="+callbackCount+" callback_restored="+callbackRestored
            +" callback_error="+callbackError+" callback_total_ms="+(callbackTicks*1000.0/Stopwatch.Frequency).ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" callback_max_ms="+(callbackMaxTicks*1000.0/Stopwatch.Frequency).ToString("F6",System.Globalization.CultureInfo.InvariantCulture)
            +" prepared_payload_bytes="+payload+" rejected="+rejected+" barrier_rejected="+barriers+" resolution_changed=False original_textures_changed=False per_frame_cpu_work=0 default=False";
        static void Restore()
        {
            int failed=0;
            for(int i=active.Count-1;i>=0;i--)
            {
                var e=active[i];try{RestoreEntry(e);active.RemoveAt(i);}
                catch{failed++;}
            }
            if(failed!=0)throw new InvalidOperationException("channel restore=UNCONFIRMED retained="+failed);
        }
        static void Release()
        {
            if(entries.Any(e=>e.CallbackRegistered)||watched.Count!=0){Main.Entry.Logger.Log("[spritechannels] release=UNCONFIRMED callbacks_retained=True");return;}
            if(shader&&Resources.FindObjectsOfTypeAll<Material>().Any(m=>m&&(m.shader==shader||(m.HasProperty(PackedId)&&images.Values.Any(i=>i.Packed&&m.GetTexture(PackedId)==i.Packed))))){Main.Entry.Logger.Log("[spritechannels] release=UNCONFIRMED foreign_material_reference=True");return;}
            foreach(var i in images.Values){i.Invalid?.Dispose();if(i.Packed){i.Packed.Release();UnityEngine.Object.Destroy(i.Packed);}}
            Main.Entry.Logger.Log("[spritechannels] callback_release registered="+registered+" unregistered="+unregistered+" active=0 events="+callbackCount+" restored="+callbackRestored+" error="+callbackError);
            images.Clear();entries.Clear();excludedBuckets.Clear();payload=0;invalid=errors=0;pack=null;shader=null;if(bundle)bundle.Unload(true);bundle=null;
            callbackCount=callbackRestored=callbackTicks=callbackMaxTicks=registered=unregistered=0;callbackError="none";
            Main.Entry.Logger.Log("[spritechannels] released=True restored=True pending=0");
        }
        internal static void Stop(){Restore();stopping=true;if(pending==0)Release();else Main.Entry.Logger.Log("[spritechannels] release_deferred=True pending="+pending);}
    }
}
#endif
