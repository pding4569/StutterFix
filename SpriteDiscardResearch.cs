#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace StutterFix
{
    // Explicit paused experiment only. No new objects, subscriptions, Harmony
    // patches, texture changes, sprite changes or per-frame scanning.
    internal static class SpriteDiscardResearch
    {
        sealed class Saved { internal Material Material;internal Shader Shader;internal Renderer Renderer; }
        static AssetBundle bundle;
        static Shader candidate;
        static readonly List<Saved> saved=new List<Saved>();
        static int rejected,barriers;
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static (int,int,int) Bucket(Renderer r,Material m)=>(r.sortingLayerID,r.sortingOrder,m.renderQueue);
        internal static string Command(string arg)
        {
            if(arg=="state")return State();
            if(arg=="off"){Restore();return State();}
            if(arg=="stop"){Stop();return State();}
            if(arg=="control on"||arg=="control off"||arg=="early on"||arg=="early off")
            {
                if(saved.Count==0||!GpuPlaybackState.Paused)throw new InvalidOperationException("paused active sprite candidate required");
                Shader.SetGlobalFloat("_SfSpriteDiscardControl",arg=="early on"?2:arg=="control on"?1:0);return State();
            }
            if(arg=="on"||arg=="on vertex")
            {
                if(!candidate||!GpuPlaybackState.Paused||saved.Count!=0)throw new InvalidOperationException("loaded inactive sprite candidate and paused playback required");
                bool vertex=arg=="on vertex";
                var eligible=Eligible(out rejected,out barriers);
                try
                {
                    foreach(var row in eligible)
                    {
                        var m=row.Value;var old=m.shader;float x=m.GetFloat("RepeatX"),y=m.GetFloat("RepeatY");var color=m.GetColor("_Color");
                        if(!Finite(x)||!Finite(y)||!Finite(color.r)||!Finite(color.g)||!Finite(color.b)||!Finite(color.a))throw new InvalidOperationException("nonfinite source properties");
                        saved.Add(new Saved{Material=m,Shader=old,Renderer=row.Key});m.shader=candidate;
                        if(m.GetFloat("RepeatX")!=x||m.GetFloat("RepeatY")!=y||!m.GetColor("_Color").Equals(color)||m.renderQueue!=3000)throw new InvalidOperationException("material property retention failed");
                        m.EnableKeyword("SF_SPRITE_DISCARD");if(vertex)m.EnableKeyword("SF_REPEAT_IN_VERTEX");else m.DisableKeyword("SF_REPEAT_IN_VERTEX");
                    }
                    if(saved.Count==0)throw new InvalidOperationException("no safe sprite material buckets");
                }
                catch{Restore();throw;}
                return State()+" repeat_stage="+(vertex?"vertex":"fragment");
            }
            if(!Path.IsPathRooted(arg)||!File.Exists(arg))throw new ArgumentException("spritediscard <bundle>|on[ vertex]|off|state|control on/off|stop");
            Stop();
            try
            {
                bundle=AssetBundle.LoadFromFile(arg);if(!bundle)throw new InvalidOperationException("sprite bundle unavailable");
                candidate=bundle.LoadAsset<Shader>("Assets/Shaders/SpriteDiscard.shader");
                if(!candidate||!candidate.isSupported||candidate.name!="StutterFix/ResearchSpriteDiscard"||!candidate.keywordSpace.FindKeyword("SF_SPRITE_DISCARD").isValid)throw new InvalidOperationException("sprite shader unavailable");
                return State();
            }
            catch{Stop();throw;}
        }
        internal static Dictionary<Renderer,Material> Eligible(out int rejected,out int barriers)
        {
            var main=GpuDecoScene.FindMain();var all=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var users=new Dictionary<Material,int>();
            foreach(var r in all)if(r&&r.gameObject.scene.IsValid())foreach(var m in r.sharedMaterials)if(m){users.TryGetValue(m,out int n);users[m]=n+1;}
            var eligible=new Dictionary<Renderer,Material>();rejected=barriers=0;
            foreach(var r in all)
            {
                if(!r||!r.enabled||r.forceRenderingOff||!r.gameObject.activeInHierarchy||(main.cullingMask&(1<<r.gameObject.layer))==0)continue;
                var sr=r as SpriteRenderer;var d=sr?sr.GetComponentInParent<scrVisualDecoration>():null;var m=r.sharedMaterial;
                if(!d||!sr.sprite||sr.color.a<=0)continue;
                bool safe=m&&m.shader&&m.shader.name=="Sprites/Tile"&&m.passCount==1&&m.renderQueue==3000
                    &&users[m]==1&&r.sharedMaterials.Length==1&&m.shaderKeywords.Length==0&&!m.enableInstancing&&m.HasProperty("RepeatX")&&m.HasProperty("RepeatY")&&m.HasProperty("_Color")
                    &&m.mainTextureScale==Vector2.one&&m.mainTextureOffset==Vector2.zero&&m.GetShaderPassEnabled(m.GetPassName(0))
                    &&m.HasProperty("PixelSnap")&&m.GetFloat("PixelSnap")==0&&sr.drawMode==SpriteDrawMode.Simple&&!sr.flipX&&!sr.flipY
                    &&sr.maskInteraction==SpriteMaskInteraction.None&&!sr.sprite.associatedAlphaSplitTexture&&!sr.GetComponentInParent<SortingGroup>()
                    &&sr.sprite.texture.format==TextureFormat.RGBA32&&sr.sprite.texture.mipmapCount==1;
                if(!safe){rejected++;continue;}eligible[r]=m;
            }
            var forbidden=new HashSet<(int,int,int)>();
            // Conservatively exclude the entire layer/order/queue bucket
            // if any outside renderer participates, even at another depth.
            foreach(var r in all)
                if(r&&r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy&&(main.cullingMask&(1<<r.gameObject.layer))!=0&&!eligible.ContainsKey(r))
                    foreach(var m in r.sharedMaterials)if(m)forbidden.Add(Bucket(r,m));
            foreach(var row in eligible.ToArray())if(forbidden.Contains(Bucket(row.Key,row.Value))){eligible.Remove(row.Key);barriers++;}
            return eligible;
        }
        static string State()=>"SPRITE_DISCARD loaded="+(bool)candidate+" selected="+saved.Count+" bound="+saved.Count(s=>s.Material&&s.Material.shader==candidate&&s.Material.IsKeywordEnabled("SF_SPRITE_DISCARD"))
            +" renderer_bound="+saved.Count(s=>s.Renderer&&s.Material&&s.Renderer.sharedMaterial==s.Material&&s.Material.shader==candidate&&s.Material.IsKeywordEnabled("SF_SPRITE_DISCARD"))
            +" diagnostic_control="+Shader.GetGlobalFloat("_SfSpriteDiscardControl")
            +" rejected="+rejected+" barrier_rejected="+barriers+" renderer_material_IDs_unchanged=True renderer_writes=0 per_frame_work=0 default=False";
        static void Restore()
        {
            Shader.SetGlobalFloat("_SfSpriteDiscardControl",0);
            int failed=0;
            for(int i=saved.Count-1;i>=0;i--)
            {
                var s=saved[i];
                try
                {
                    if(s.Material&&s.Material.shader==candidate){s.Material.DisableKeyword("SF_SPRITE_DISCARD");s.Material.DisableKeyword("SF_REPEAT_IN_VERTEX");s.Material.shader=s.Shader;if(s.Material.shader!=s.Shader)throw new InvalidOperationException("shader restore failed");}
                    saved.RemoveAt(i);
                }
                catch(Exception e){failed++;Main.Entry.Logger.Log("[spritediscard] restore=UNCONFIRMED error="+e.GetType().Name);}
            }
            if(failed!=0)throw new InvalidOperationException("sprite restore failures retained="+failed);
        }
        internal static void Stop()
        {
            Restore();
            if(!candidate&&!bundle)return;
            // A foreign clone is outside ownership. Retain its shader rather
            // than unloading an asset still referenced by an unknown material.
            if(UnityEngine.Resources.FindObjectsOfTypeAll<Material>().Any(m=>m&&m.shader==candidate))
            {Main.Entry.Logger.Log("[spritediscard] release=UNCONFIRMED foreign_material_reference=True");return;}
            candidate=null;if(bundle)bundle.Unload(true);bundle=null;
            Main.Entry.Logger.Log("[spritediscard] released=True restored=True");
        }
    }
}
#endif
