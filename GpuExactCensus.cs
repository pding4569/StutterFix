#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Read-only diagnostic. No renderer state, property block or material is changed.
    internal sealed class GpuExactCensus : MonoBehaviour
    {
        sealed class Item
        {
            internal SceneRenderOrder.Key Key;
            internal string Signature, Run;
            internal double Fragments, Interior;
            internal bool Safe;
        }
        static GpuExactCensus active;
        static bool ready;
        static Exception failure;
        Camera camera;
        int remaining, requested, lastFrame = -1;
        string previousCamera;
        List<Item> previous;
        readonly List<double> prefix = new List<double>(), unchanged = new List<double>(), runs = new List<double>(), interiors = new List<double>();
        readonly Dictionary<string, double> histogram = new Dictionary<string, double>();
        double totalFragments;
        int barriers, unknownHistograms;
        static string Bits(float x) => BitConverter.SingleToInt32Bits(x).ToString("X8", CultureInfo.InvariantCulture);
        static string Vec(Vector4 v) => Bits(v.x)+","+Bits(v.y)+","+Bits(v.z)+","+Bits(v.w);
        static string Matrix(Matrix4x4 m) { var b = new StringBuilder(); for (int i=0;i<16;i++) b.Append(Bits(m[i])).Append(','); return b.ToString(); }
        static void Log(string s) => Main.Entry.Logger.Log("[gpuexact] " + s);
        internal static bool Step(string arg)
        {
            if (arg == "stop") { Stop(); ready=false; failure=null; return true; }
            var words=arg.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if(words.Length!=2 || words[0]!="census" || !int.TryParse(words[1],out int n) || n<2 || n>10000)
                throw new ArgumentException("gpuexact census <2..10000 frames>|stop");
            if(failure!=null) { var e=failure; failure=null; throw e; }
            if(ready) { ready=false; return true; }
            if(active) return false;
            if(ZCull.Busy || GpuCensus.Busy || GpuHide.Busy || GpuSplit.Busy || GpuSplitPixelDiff.Busy || Occlude.Busy)
                throw new InvalidOperationException("stop other GPU diagnostics before gpuexact");
            if(Main.Config.GpuDeco!=0 || Main.Config.RunLod) throw new InvalidOperationException("gpuexact requires rlNR original renderers");
            var c=GpuDecoScene.FindMain();
            if(!c || !c.orthographic || c.stereoEnabled) throw new InvalidOperationException("mono orthographic main camera required");
            active=new GameObject("StutterFix.GpuExactCensus").AddComponent<GpuExactCensus>();
            DontDestroyOnLoad(active.gameObject); active.camera=c; active.remaining=active.requested=n;
            Camera.onPreRender+=active.Capture;
            return false;
        }
        internal static void Stop()
        {
            ready=false;failure=null;
            if(!active)return;
            var a=active; active=null; Camera.onPreRender-=a.Capture; Destroy(a.gameObject);
        }
        void OnDestroy() { Camera.onPreRender-=Capture; if(active==this)active=null; }
        void Update() { if(!camera) { Stop(); failure=new InvalidOperationException("gpuexact camera lost"); } }
        string CameraSignature(Camera c)
        {
            var t=c.targetTexture;
            var rect=c.pixelRect;
            // Camera motion belongs in each VP*localToWorld signature: a camera-attached
            // sprite can remain screen-fixed while world layers change.
            return c.GetInstanceID()+"|"+(int)c.clearFlags+"|"+Vec(c.backgroundColor)
                +"|"+c.cullingMask+"|"+Vec(new Vector4(rect.x,rect.y,rect.width,rect.height))+"|"+(t?t.GetInstanceID():0)+"|"+(t?t.graphicsFormat.ToString():"backbuffer")
                +"|"+(t?t.antiAliasing:QualitySettings.antiAliasing)+"|"+QualitySettings.activeColorSpace
                +"|"+Bits(c.nearClipPlane)+"|"+Bits(c.farClipPlane)+"|"+c.orthographic+"|"+c.allowHDR;
        }
        // Histogram is a texture-space estimate. Bilinear interior requires all four neighbours alpha=255.
        // No claim of screen-space UV footprint or mip/anisotropic coverage.
        double Interior(Sprite s)
        {
            var t=s.texture;
            string id=s.GetInstanceID()+"|"+(t?t.updateCount:0)+"|"+(t?(int)t.filterMode:0)+"|"+(t?(int)t.wrapMode:0);
            if(histogram.TryGetValue(id,out double value))return value;
            if(!t || s.packed || t.mipmapCount!=1 || t.anisoLevel>1 || t.filterMode==FilterMode.Trilinear)
            { unknownHistograms++; return histogram[id]=-1; }
            RenderTexture rt=null; Texture2D copy=null; var old=RenderTexture.active;
            try
            {
                rt=RenderTexture.GetTemporary(t.width,t.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                Graphics.Blit(t,rt); RenderTexture.active=rt;
                copy=new Texture2D(t.width,t.height,TextureFormat.RGBA32,false,true);
                copy.ReadPixels(new Rect(0,0,t.width,t.height),0,0); copy.Apply(); var px=copy.GetPixels32();
                var r=s.rect; int x0=(int)r.x,y0=(int)r.y,w=(int)r.width,h=(int)r.height; long ones=0;
                for(int y=y0;y<y0+h;y++)for(int x=x0;x<x0+w;x++)
                {
                    bool one=px[y*t.width+x].a==255;
                    if(t.filterMode!=FilterMode.Point) one &= x+1<x0+w && y+1<y0+h
                        && px[y*t.width+Math.Min(x+1,t.width-1)].a==255
                        && px[Math.Min(y+1,t.height-1)*t.width+x].a==255
                        && px[Math.Min(y+1,t.height-1)*t.width+Math.Min(x+1,t.width-1)].a==255;
                    if(one)ones++;
                }
                value=(double)ones/(w*h); histogram[id]=value; return value;
            }
            catch { unknownHistograms++; return histogram[id]=-1; }
            finally { RenderTexture.active=old; if(rt)RenderTexture.ReleaseTemporary(rt); if(copy)Destroy(copy); }
        }
        // Same visible renderer/material-slot enumeration and SceneRenderOrder model as ZCull.
        // Projected clipped AABB area is an explicit estimate, never a GPU fragment counter.
        List<Item> Discover(Camera c)
        {
            var list=new List<Item>(); var planes=GeometryUtility.CalculateFrustumPlanes(c.cullingMatrix);
            var vp=c.projectionMatrix*c.worldToCameraMatrix;
            foreach(var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if(!r || !r.gameObject.scene.IsValid() || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff
                    || r is SpriteMask || r.shadowCastingMode==ShadowCastingMode.ShadowsOnly
                    || (c.cullingMask&(1<<r.gameObject.layer))==0 || !GeometryUtility.TestPlanesAABB(planes,r.bounds))continue;
                var sr=r as SpriteRenderer; var group=r.GetComponentInParent<SortingGroup>();
                var bounds=r.bounds; float x0=1,y0=1,x1=0,y1=0;
                for(int v=0;v<8;v++) { var p=c.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((v&1)==0?-1:1,(v&2)==0?-1:1,(v&4)==0?-1:1)));x0=Math.Min(x0,p.x);y0=Math.Min(y0,p.y);x1=Math.Max(x1,p.x);y1=Math.Max(y1,p.y); }
                double area=Math.Max(0,Math.Min(1,x1)-Math.Max(0,x0))*Math.Max(0,Math.Min(1,y1)-Math.Max(0,y0))*c.pixelWidth*c.pixelHeight;
                var mats=r.sharedMaterials;
                for(int slot=0;slot<mats.Length;slot++)
                {
                    var m=mats[slot]; if(!m || !m.shader)continue;
                    var key=GpuDecoScene.SortKey(r,m,c,slot);
                    if(group) { for(var parent=group.transform.parent;parent;parent=parent.parent) { var outer=parent.GetComponent<SortingGroup>();if(outer && outer.enabled)group=outer; }
                        var sortMode=c.transparencySortMode==TransparencySortMode.Default?GraphicsSettings.transparencySortMode:c.transparencySortMode;
                        var axis=c.transparencySortMode==TransparencySortMode.Default?GraphicsSettings.transparencySortAxis:c.transparencySortAxis;
                        float distance=sortMode==TransparencySortMode.CustomAxis?Vector3.Dot(group.transform.position,axis):Vector3.Dot(group.transform.position-c.transform.position,c.transform.forward);
                        key=new SceneRenderOrder.Key(SortingLayer.GetLayerValueFromID(group.sortingLayerID),group.sortingOrder,m.renderQueue,distance,m.GetInstanceID(),r.GetInstanceID(),slot,SceneRenderOrder.ShaderRank(m)); }
                    bool safe=sr && sr.sprite && !group && mats.Length==1 && m.passCount==1 && !r.HasPropertyBlock()
                        && sr.maskInteraction==SpriteMaskInteraction.None && sr.drawMode==SpriteDrawMode.Simple
                        && !sr.sprite.packed && !sr.sprite.associatedAlphaSplitTexture
                        && (m.shader.name=="Sprites/Default" || m.shader.name=="Sprites/Tile")
                        && m.shaderKeywords.Length==0;
                    var deco=r.GetComponentInParent<scrVisualDecoration>();
                    if(deco && deco.blendModeEffect && deco.blendModeEffect.enabled)safe=false;
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block,slot);
                    string content=TileRender.Content(m);
                    string color=sr?Vec(sr.color):"none";
                    var geometry=new StringBuilder();
                    if(sr && sr.sprite) { foreach(var v in sr.sprite.vertices)geometry.Append(Vec(v));foreach(var uv in sr.sprite.uv)geometry.Append(Vec(uv));foreach(var triangle in sr.sprite.triangles)geometry.Append(triangle).Append(','); }
                    foreach(var prop in new[]{"_MainTex","_AlphaTex"}) { var tex=m.HasProperty(prop)?m.GetTexture(prop):null;if(tex)geometry.Append(prop).Append(tex.updateCount).Append(':').Append((int)tex.filterMode).Append(':').Append((int)tex.wrapMode).Append(':').Append(tex.anisoLevel); }
                    if(sr && sr.sprite && sr.sprite.texture)geometry.Append("spriteTex:").Append(sr.sprite.texture.updateCount).Append(':').Append((int)sr.sprite.texture.filterMode).Append(':').Append((int)sr.sprite.texture.wrapMode);
                    string sig=r.GetInstanceID()+"|"+slot+"|"+Matrix(vp*r.localToWorldMatrix)+"|"+content+"|"+color
                        +"|"+geometry
                        +"|"+(sr&&sr.sprite?sr.sprite.GetInstanceID():0)+"|"+(sr?sr.flipX+","+sr.flipY:"-")
                        +"|"+Vec(block.GetColor("_Color"))+"|"+Vec(block.GetColor("_RendererColor"))
                        +"|"+(block.GetTexture("_MainTex")?block.GetTexture("_MainTex").GetInstanceID():0)+"|mpb="+r.HasPropertyBlock()
                        +"|enabled=1,active=1|"+key.Layer+","+key.Order+","+key.Queue+","+Bits(key.Distance)+","+key.Shader+","+key.Material;
                    list.Add(new Item { Key=key,Signature=sig,Fragments=area,Safe=safe,
                        Run=safe?sr.sprite.texture.GetInstanceID()+"|"+color+"|"+content:null,
                        Interior=safe?Interior(sr.sprite):-1 });
                }
            }
            list.Sort((a,b)=>a.Key.CompareTo(b.Key));
            var unsafeBuckets=new HashSet<(int,int,int)>(list.Where(x=>!x.Safe).Select(x=>(x.Key.Layer,x.Key.Order,x.Key.Queue)));
            foreach(var d in list)if(unsafeBuckets.Contains((d.Key.Layer,d.Key.Order,d.Key.Queue)))d.Safe=false;
            for(int i=0;i<list.Count;i++)if((i>0&&list[i].Key.SameMaterialTie(list[i-1].Key)) || (i+1<list.Count&&list[i].Key.SameMaterialTie(list[i+1].Key)))list[i].Safe=false;
            return list;
        }
        void Capture(Camera c)
        {
            if(c!=camera || lastFrame==Time.frameCount)return; lastFrame=Time.frameCount;
            try
            {
                if(Main.Config.GpuDeco!=0 || Main.Config.RunLod)throw new InvalidOperationException("original renderer configuration changed during census");
                var now=Discover(c); string cs=CameraSignature(c);
                double total=now.Sum(x=>x.Fragments),p=0,u=0,run=0,inside=0;
                if(previous!=null)
                {
                    bool open=cs==previousCamera && c.clearFlags==CameraClearFlags.SolidColor;
                    var old=previous.ToDictionary(x=>x.Key.Renderer+":"+x.Key.Slot,x=>x);
                    for(int i=0;i<now.Count;i++)
                    {
                        var d=now[i]; bool same=d.Safe && old.TryGetValue(d.Key.Renderer+":"+d.Key.Slot,out var prior) && prior.Safe && d.Signature==prior.Signature && cs==previousCamera;
                        if(same)u+=d.Fragments;
                        open &= same && i<previous.Count && d.Signature==previous[i].Signature;
                        if(open)p+=d.Fragments;
                    }
                    prefix.Add(total>0?p/total:0); unchanged.Add(total>0?u/total:0);
                }
                for(int i=0;i<now.Count;)
                {
                    int end=i+1; while(end<now.Count && now[end].Safe && now[i].Safe && now[end].Run==now[i].Run)end++;
                    if(end-i>=2 && now[i].Safe) for(int j=i;j<end;j++) { run+=now[j].Fragments; if(now[j].Interior>=0)inside+=now[j].Fragments*now[j].Interior; }
                    i=end;
                }
                runs.Add(total>0?run/total:0);interiors.Add(total>0?inside/total:0);totalFragments+=total;barriers+=now.Count(x=>!x.Safe);
                previous=now;previousCamera=cs;
                if(--remaining==0)
                {
                    Log("frames="+requested+" transitions="+prefix.Count+" estimator=clipped_projected_AABB_area not_hardware_fragments=True first_frame_excluded_from_temporal=True"
                        +" prefix="+Distribution(prefix)+" unchanged_all="+Distribution(unchanged)+" runs="+Distribution(runs)+" runs_texel_alpha1_estimate="+Distribution(interiors)
                        +" fragments_estimated_sum="+totalFragments.ToString("F0",CultureInfo.InvariantCulture)+" barriers_sum="+barriers+" histogram_unknown="+unknownHistograms
                        +" histogram_cache=texture_updateCount_sampler_key MPB_unknown_group_particle_mask_tie=barrier sample_not_for_FPS=True");
                    Stop();ready=true;
                }
            }
            catch(Exception e) { Stop();failure=e; }
        }
        static string Distribution(List<double> v)
        {
            if(v.Count==0)return "none"; var a=v.OrderBy(x=>x).ToArray();
            return string.Join(",",new[]{v.Average(),a[0],a[(a.Length-1)/10],a[(a.Length-1)/2],a[(a.Length-1)*9/10],a[a.Length-1]}.Select(x=>x.ToString("F6",CultureInfo.InvariantCulture)))+"(mean,min,p10,p50,p90,max)";
        }
    }
}
#endif
