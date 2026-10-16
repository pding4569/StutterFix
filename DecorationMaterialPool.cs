using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using UnityEngine;
using ADOFAI;

namespace StutterFix
{
#if DEV || AUTOTEST
    // Private canonical materials never escape through managed Renderer material getters.
    // Original mutable identities stay alive; writes detach their renderers before mutating.
    // Only constructor-proven Sprites/Tile materials with completely known state are eligible.
    internal sealed class DecorationMaterialPool:IOptimizationModule
    {
        internal static readonly DecorationMaterialPool Instance=new DecorationMaterialPool();
        public string Key=>"deco-material-pool";
        internal static bool Enabled,Requested;internal static string Status="off";
        private static bool attempted,hooked;
        internal static void Configure(bool wanted)
        {
#if !DEV && !AUTOTEST
            Requested=false;Enabled=false;Status="research-only: sorting tie mismatch";return;
#else
            Requested=wanted;
            if(!wanted){Enabled=false;attempted=false;return;}
            if(!attempted&&!Hitch.Playing)Enabled=true;
            else if(!Enabled)Status="waiting outside play";
#endif
        }
        internal static void Deferred()
        {
            if(Requested&&!attempted&&!Hitch.Playing){Enabled=true;OptimizationModules.Install(Instance);}
        }
        private sealed class Binding{internal SpriteRenderer Renderer;internal Material Original,View;}
        private sealed class Origin{internal bool Clean;internal string Root;internal Origin(bool clean,string root){Clean=clean;Root=root;}}
        private static long roots;
        private static ConditionalWeakTable<Material,Origin> provenance=new ConditionalWeakTable<Material,Origin>();
        private static readonly Dictionary<SpriteRenderer,Binding> bindings=new Dictionary<SpriteRenderer,Binding>();
        private static readonly Dictionary<Material,HashSet<Binding>> owners=new Dictionary<Material,HashSet<Binding>>();
        private static readonly Dictionary<string,Material> canonical=new Dictionary<string,Material>(StringComparer.Ordinal);
        private static readonly HashSet<Material> views=new HashSet<Material>();
        private static readonly HashSet<Binding> pending=new HashSet<Binding>();
        private static readonly List<Binding> work=new List<Binding>();
        [ThreadStatic] private static int inside;
        private static long rendererWrites;
        private static bool suspended;
        internal static long Detached,Assigned;internal static int Patches;
        private static readonly int MainTex=Shader.PropertyToID("_MainTex"),ColorId=Shader.PropertyToID("_Color"),
            Pixel=Shader.PropertyToID("PixelSnap"),RepeatX=Shader.PropertyToID("RepeatX"),RepeatY=Shader.PropertyToID("RepeatY");
        public void Install(Harmony h)
        {
#if !DEV && !AUTOTEST
            throw new NotSupportedException("Material sharing failed translucent sorting equivalence; research only");
#else
            attempted=true;
            var mutators=typeof(Material).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly)
                .Where(Mutating).ToArray();
            var getters=typeof(Renderer).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly)
                .Where(m=>m.Name=="get_material"||m.Name=="get_materials"||m.Name=="get_sharedMaterial"||m.Name=="get_sharedMaterials"||m.Name=="GetSharedMaterials"||m.Name=="GetMaterials").ToArray();
            var setters=typeof(Renderer).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly)
                .Where(m=>m.Name=="set_material"||m.Name=="set_materials"||m.Name=="set_sharedMaterial"||m.Name=="set_sharedMaterials"||m.Name=="SetSharedMaterials"||m.Name=="SetMaterials").ToArray();
            var constructors=typeof(Material).GetConstructors();
            // Extern/native methods cannot be guarded with an ordinary Harmony IL patch.
            foreach(var m in mutators.Concat(getters).Concat(setters))
                if(m.GetMethodBody()==null)throw new NotSupportedException("Unmanaged material API: "+m.Name);
            foreach(var c in constructors)if(c.GetMethodBody()==null)throw new NotSupportedException("Unmanaged material constructor");
            if(mutators.Length==0||getters.Length<4||setters.Length<4)throw new NotSupportedException("Incomplete material API contract");
            Patches=0;
            foreach(var c in constructors){h.Patch(c,postfix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(Created)));Patches++;}
            foreach(var m in mutators){h.Patch(m,prefix:new HarmonyMethod(typeof(DecorationMaterialPool),Writer(m)));Patches++;}
            foreach(var m in getters)
            {
                if(m.Name=="get_material")h.Patch(m,prefix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(MaterialReading)),postfix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(MaterialRead)));
                else h.Patch(m,prefix:new HarmonyMethod(typeof(DecorationMaterialPool),m.Name=="get_materials"||m.Name=="GetMaterials"?nameof(ReadingInstancedArray):nameof(Reading)));
                Patches++;
            }
            foreach(var m in setters){h.Patch(m,prefix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(Reading)),postfix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(Replaced)));Patches++;}
            var mask=typeof(SpriteRenderer).GetProperty("maskInteraction")?.SetMethod;
            if(mask==null||mask.GetMethodBody()==null)throw new NotSupportedException("Unguarded sprite mask API");
            h.Patch(mask,prefix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(ReadingInstanced)));Patches++;
            foreach(var m in typeof(UnityEngine.Object).GetMethods(BindingFlags.Public|BindingFlags.Static))
                if((m.Name=="Destroy"||m.Name=="DestroyImmediate")&&m.GetMethodBody()!=null)
                {h.Patch(m,prefix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(Destroying)));Patches++;}
            var load=AccessTools.DeclaredMethod(typeof(scnGame),"LoadLevel",new[]{typeof(string),typeof(LoadResult).MakeByRefType()});
            var ready=AccessTools.DeclaredMethod(typeof(scnGame),"FinishCustomLevelLoading",new[]{typeof(int),typeof(bool)});
            if(load==null||ready==null)throw new MissingMethodException("Game load contract changed");
            h.Patch(load,prefix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(ClearBindings)));Patches++;
            h.Patch(ready,postfix:new HarmonyMethod(typeof(DecorationMaterialPool),nameof(Loaded)));Patches++;
            Camera.onPreCull+=BeforeCull;hooked=true;Status="waiting for newly created level materials";
#endif
        }
        private static bool Mutating(MethodInfo m)=>!m.Name.StartsWith("get_",StringComparison.Ordinal)&&!m.Name.StartsWith("Get",StringComparison.Ordinal)&&
            !m.Name.StartsWith("Has",StringComparison.Ordinal)&&!m.Name.StartsWith("Is",StringComparison.Ordinal)&&m.Name!="ComputeCRC"&&m.Name!="ToString"&&m.Name!="Equals"&&m.Name!="SetPass";
        private static void Created(Material __instance,object[] __args)
        {
            if(inside!=0||!Enabled)return;
            try
            {
                bool clean=__args.Length==1&&__args[0] is Shader;
                string root=clean?"shader:"+((Shader)__args[0]).GetInstanceID():"private:"+(++roots);
                if(__args.Length==1&&__args[0] is Material source){var p=OriginOf(source);clean=p.Clean;root=p.Root;}
                provenance.Remove(__instance);provenance.Add(__instance,new Origin(clean,root));
            }catch{Status="constructor provenance unavailable";}
        }
        private static string Writer(MethodInfo m)
        {
            string n=m.Name;var p=m.GetParameters();
            if(p.Length==2&&(p[0].ParameterType==typeof(int)||p[0].ParameterType==typeof(string)))
            {
                string kind=n=="SetFloat"?"Float":n=="SetColor"?"Color":n=="SetTexture"||n=="SetTextureOffset"||n=="SetTextureScale"?"Texture":null;
                if(kind!=null)return "Write"+kind+(p[0].ParameterType==typeof(int)?"Id":"Name");
            }
            bool supported=n=="set_shader"||n=="set_renderQueue"||n=="set_enableInstancing"||n=="set_doubleSidedGI"||n=="set_globalIlluminationFlags"||
                n=="set_mainTexture"||n=="set_mainTextureOffset"||n=="set_mainTextureScale"||n=="set_shaderKeywords"||n=="set_enabledKeywords"||
                n=="EnableKeyword"||n=="DisableKeyword"||n=="SetKeyword"||n=="SetShaderPassEnabled"||n=="set_color";
            return supported?nameof(WriteKnown):nameof(WriteUnknown);
        }
        private static void BeforeWrite(Material m,bool known)
        {
            if(inside!=0||!Enabled)return;
            try
            {
                if(views.Contains(m)){EscapedView();return;}
                if(owners.TryGetValue(m,out var list))foreach(var b in list)Detach(b);
                var origin=OriginOf(m);if(!known)origin.Clean=false;
            }catch{FailClosed();}
        }
        // Typed prefixes avoid Harmony __args arrays/boxing on hot SetFloat/SetColor calls.
        private static void WriteKnown(Material __instance)=>BeforeWrite(__instance,true);
        private static void WriteUnknown(Material __instance)=>BeforeWrite(__instance,false);
        private static void WriteFloatId(Material __instance,int __0)=>BeforeWrite(__instance,__0==Pixel||__0==RepeatX||__0==RepeatY);
        private static void WriteColorId(Material __instance,int __0)=>BeforeWrite(__instance,__0==ColorId);
        private static void WriteTextureId(Material __instance,int __0)=>BeforeWrite(__instance,__0==MainTex);
        private static void WriteFloatName(Material __instance,string __0){try{WriteFloatId(__instance,Shader.PropertyToID(__0));}catch{BeforeWrite(__instance,false);}}
        private static void WriteColorName(Material __instance,string __0){try{WriteColorId(__instance,Shader.PropertyToID(__0));}catch{BeforeWrite(__instance,false);}}
        private static void WriteTextureName(Material __instance,string __0){try{WriteTextureId(__instance,Shader.PropertyToID(__0));}catch{BeforeWrite(__instance,false);}}
        private static Origin OriginOf(Material m)
        {
            if(!provenance.TryGetValue(m,out var origin)){origin=new Origin(true,"template:"+(++roots));provenance.Add(m,origin);}return origin;
        }
        private struct ReadState{internal Origin Source;internal long Writes;}
        private static void MaterialReading(Renderer __instance,out ReadState __state)
        {
            __state=default;if(inside!=0||!Enabled)return;
            try{var source=RestoreInstanceAliases(__instance);if(source!=null)__state=new ReadState{Source=OriginOf(source),Writes=rendererWrites};}catch{FailClosed();}
        }
        private static void MaterialRead(Renderer __instance,Material __result,ReadState __state,bool __runOriginal)
        {
            if(inside!=0||!Enabled||__state.Source==null||__result==null||!__runOriginal||__state.Writes!=rendererWrites)return;
            inside++;
            try
            {
                // Unity's native get_material clone has no public Material constructor call.
                // Share only descendants of the SAME template: unread override tags/buffers
                // are inherited identically; unknown subsequent writes disqualify the source.
                if(__instance.sharedMaterial==__result&&!provenance.TryGetValue(__result,out _))provenance.Add(__result,new Origin(__state.Source.Clean,__state.Source.Root));
            }catch{FailClosed();}finally{inside--;}
        }
        private static void Reading(Renderer __instance)
        {
            if(inside!=0||!Enabled)return;
            try{if(__instance is SpriteRenderer r&&bindings.TryGetValue(r,out var b))Detach(b);}catch{FailClosed();}
        }
        private static void ReadingInstanced(Renderer __instance)
        {
            if(inside!=0||!Enabled)return;
            try{RestoreInstanceAliases(__instance);}catch{FailClosed();}
        }
        private static Material RestoreInstanceAliases(Renderer renderer)
        {
            Reading(renderer);Material source;inside++;
            try{source=renderer.sharedMaterial;}finally{inside--;}
            // Include non-pooled MeshRenderers/sprites that shared an original with our
            // sprites: native get_material must see the original total renderer aliases.
            if(source!=null&&owners.TryGetValue(source,out var list))foreach(var peer in list)Detach(peer);
            return source;
        }
        private static void ReadingInstancedArray(Renderer __instance)
        {
            if(inside!=0||!Enabled)return;
            try
            {
                Reading(__instance);Material[] sources;inside++;
                try{sources=__instance.sharedMaterials;}finally{inside--;}
                foreach(var source in sources)
                    if(source!=null&&owners.TryGetValue(source,out var list))foreach(var peer in list)Detach(peer);
            }catch{FailClosed();}
        }
        private static void Replaced(Renderer __instance)
        {
            if(inside!=0||!Enabled)return;
            rendererWrites++;
            try
            {
                if(!(__instance is SpriteRenderer r)||!bindings.TryGetValue(r,out var b))return;
                inside++;
                try
                {
                    if(r.sharedMaterials.Length!=1){RemoveOwner(b);bindings.Remove(r);pending.Remove(b);return;}
                    RemoveOwner(b);b.Original=r.sharedMaterial;b.View=null;
                    AddOwner(b);pending.Add(b);
                }finally{inside--;}
            }catch{FailClosed();}
        }
        private static void Destroying(UnityEngine.Object __0)
        {
            if(inside!=0||!Enabled||!(__0 is Material material))return;
            try
            {
                if(views.Contains(material)){EscapedView();return;}
                if(owners.TryGetValue(material,out var list))foreach(var b in list)Detach(b);
                provenance.Remove(material);
            }catch{FailClosed();}
        }
        private static void AddOwner(Binding b)
        {
            if(b.Original==null)return;
            if(!owners.TryGetValue(b.Original,out var list))owners[b.Original]=list=new HashSet<Binding>();list.Add(b);
        }
        private static void RemoveOwner(Binding b)
        {
            if(ReferenceEquals(b.Original,null))return;
            if(owners.TryGetValue(b.Original,out var list)){list.Remove(b);if(list.Count==0)owners.Remove(b.Original);}
        }
        private static void Detach(Binding b)
        {
            inside++;
            try
            {
                if(b.Renderer!=null&&b.View!=null&&b.Renderer.sharedMaterial==b.View)
                {b.Renderer.sharedMaterial=b.Original;Detached++;}
                b.View=null;pending.Add(b);
            }finally{inside--;}
        }
        private static void Loaded()
        {
            if(!Enabled)return;
            try
            {
                ClearBindings();inside++;
                try
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    foreach(var d in Resources.FindObjectsOfTypeAll<scrVisualDecoration>())
                    {
                        if(d==null||d.gameObject.scene!=scene)continue;
                        var r=d.spriteRenderer;if(r==null||bindings.ContainsKey(r))continue;
                        if(r.sharedMaterials.Length!=1||HasNativeAnimation(r.transform))continue;
                        var b=new Binding{Renderer=r,Original=r.sharedMaterial};bindings.Add(r,b);AddOwner(b);pending.Add(b);
                    }
                }finally{inside--;}
                int proven=0;foreach(var b in bindings.Values)if(b.Original!=null&&provenance.TryGetValue(b.Original,out var p)&&p.Clean)proven++;
                Status="tracked "+bindings.Count+" proven "+proven;
                FlushPending(); // Initial grouping belongs to preparation, not the first gameplay camera.
            }catch{FailClosed();}
        }
        private static int Bits(float value)=>BitConverter.SingleToInt32Bits(value);
        private static readonly Type AnimatorType=Type.GetType("UnityEngine.Animator, UnityEngine.AnimationModule",false),AnimationType=Type.GetType("UnityEngine.Animation, UnityEngine.AnimationModule",false);
        private static bool HasNativeAnimation(Transform root)
        {
            for(var t=root;t!=null;t=t.parent)
                if(AnimatorType!=null&&t.GetComponent(AnimatorType)!=null||AnimationType!=null&&t.GetComponent(AnimationType)!=null)return true;
            return false;
        }
        private static void Value(StringBuilder s,Vector4 v){s.Append('/').Append(Bits(v.x)).Append('/').Append(Bits(v.y)).Append('/').Append(Bits(v.z)).Append('/').Append(Bits(v.w));}
        private static string ExactKey(Material m)
        {
            if(m==null||!provenance.TryGetValue(m,out var origin)||!origin.Clean||m.shader==null||m.shader!=scrDecorationManager.tileShader)return null;
            var shader=m.shader;
            if(shader.name!="Sprites/Tile"||shader.GetPropertyCount()!=5||m.passCount!=1)return null;
            for(int i=0;i<5;i++)
            {
                int id=shader.GetPropertyNameId(i);var t=shader.GetPropertyType(i);
                if(id==MainTex){if(t!=UnityEngine.Rendering.ShaderPropertyType.Texture)return null;}
                else if(id==ColorId){if(t!=UnityEngine.Rendering.ShaderPropertyType.Color)return null;}
                else if(id!=Pixel&&id!=RepeatX&&id!=RepeatY||t!=UnityEngine.Rendering.ShaderPropertyType.Float)return null;
            }
            var s=new StringBuilder(160);s.Append(origin.Root).Append('/').Append(shader.GetInstanceID()).Append('/').Append(m.renderQueue).Append('/').Append(m.enableInstancing).Append('/').Append(m.doubleSidedGI).Append('/').Append((int)m.globalIlluminationFlags);
            s.Append('/').Append(m.GetShaderPassEnabled(m.GetPassName(0)));
            var tex=m.GetTexture(MainTex);s.Append('/').Append(tex!=null?tex.GetInstanceID():0);
            if(tex!=null&&!(tex is Texture2D))return null;
            Value(s,m.GetColor(ColorId));Value(s,m.GetTextureOffset(MainTex));Value(s,m.GetTextureScale(MainTex));
            s.Append('/').Append(Bits(m.GetFloat(Pixel))).Append('/').Append(Bits(m.GetFloat(RepeatX))).Append('/').Append(Bits(m.GetFloat(RepeatY)));
            var keywords=m.shaderKeywords;Array.Sort(keywords,StringComparer.Ordinal);
            foreach(var keyword in keywords)s.Append('/').Append(keyword.Length).Append(':').Append(keyword);
            return s.ToString();
        }
        private static void BeforeCull(Camera camera)
        {
            if(!Enabled||suspended||pending.Count==0||!Hitch.Playing||camera==null||!camera.gameObject.scene.IsValid())return;
            FlushPending();
        }
        private static void FlushPending()
        {
            try
            {
                work.Clear();work.AddRange(pending);pending.Clear();inside++;
                try
                {
                    foreach(var b in work)
                    {
                        if(b.Renderer==null)continue;
                        if(b.Renderer.maskInteraction!=SpriteMaskInteraction.None)continue;
                        var current=b.Renderer.sharedMaterial;
                        if(current!=b.Original){RemoveOwner(b);b.Original=current;AddOwner(b);}
                        string key=ExactKey(b.Original);if(key==null)continue;
                        if(!canonical.TryGetValue(key,out var view))
                        {
                            // Bound long maps; reaching the bound keeps private originals.
                            if(canonical.Count>=4096)continue;
                            view=new Material(b.Original){name="StutterFix.SharedDecoration",hideFlags=HideFlags.HideAndDontSave};canonical.Add(key,view);views.Add(view);
                        }
                        b.Renderer.sharedMaterial=view;b.View=view;Assigned++;
                    }
                }finally{inside--;work.Clear();}
            }catch{FailClosed();}
        }
#if DEV || AUTOTEST
        internal static Material Peek(SpriteRenderer r){inside++;try{return r.sharedMaterial;}finally{inside--;}}
        internal static void BindFixture(SpriteRenderer r)
        {
            inside++;try{var b=new Binding{Renderer=r,Original=r.sharedMaterial};bindings.Add(r,b);AddOwner(b);pending.Add(b);}finally{inside--;}
        }
        internal static void UnbindFixture(SpriteRenderer r)
        {
            if(!bindings.TryGetValue(r,out var b))return;Detach(b);RemoveOwner(b);pending.Remove(b);bindings.Remove(r);
        }
        internal static void SuspendFixture(bool value)
        {
            suspended=value;
            if(value)foreach(var b in bindings.Values)Detach(b);
        }
        internal static string EscapeFixture()
        {
            Binding chosen=null;foreach(var b in bindings.Values)if(b.View!=null){chosen=b;break;}
            if(chosen==null)return "applied=0 no-shared-renderer";
            var renderer=chosen.Renderer;var source=chosen.Original;var view=chosen.View;var color=source.GetColor(ColorId);
            // Model a foreign Resources.FindObjectsOfTypeAll<Material>() write, not a source write.
            view.SetColor(ColorId,Color.magenta);
            bool restored=Peek(renderer)==source&&source.GetColor(ColorId)==color&&!Enabled&&!Requested&&bindings.Count==0;
            if(!restored)throw new InvalidOperationException("Escaped canonical material was not isolated");
            return "conditions=1 errors=0 source_values_preserved=1 original_renderers_restored=1 dangling_view_destroyed_during_write=0";
        }
#endif
        private static void EscapedView()
        {
            Enabled=false;Requested=false;Status="fallback: external shared-view mutation";inside++;
            try
            {
                foreach(var b in bindings.Values)
                    if(b.Renderer!=null&&b.View!=null&&b.Renderer.sharedMaterial==b.View)b.Renderer.sharedMaterial=b.Original;
                bindings.Clear();owners.Clear();pending.Clear();work.Clear();
                // Do not destroy a material while a foreign caller is still writing it (or
                // iterating a Resources array). Keep owned views until ordinary Uninstall.
            }finally{inside--;}
        }
        private static void FailClosed(){Enabled=false;Status="fallback; original materials";try{ClearBindings();}catch{Status="fallback; cleanup pending";}}
        private static void ClearBindings()
        {
            inside++;
            try
            {
                foreach(var b in bindings.Values)
                    if(b.Renderer!=null&&b.View!=null&&b.Renderer.sharedMaterial==b.View)b.Renderer.sharedMaterial=b.Original;
                bindings.Clear();owners.Clear();pending.Clear();work.Clear();
                foreach(var m in canonical.Values)if(m!=null)UnityEngine.Object.Destroy(m);canonical.Clear();views.Clear();
            }finally{inside--;}
        }
        internal static string State
        {
            get
            {
                int shared=0;foreach(var b in bindings.Values)if(b.View!=null)shared++;
                return "enabled="+Enabled+" status="+Status+" tracked="+bindings.Count+" shared_renderers="+shared+" canonical="+canonical.Count+" detached="+Detached+" assignments="+Assigned+" patches="+Patches;
            }
        }
        public void Uninstall(){Enabled=false;Requested=false;if(hooked)attempted=false;hooked=false;Camera.onPreCull-=BeforeCull;ClearBindings();provenance=new ConditionalWeakTable<Material,Origin>();Status="off";}
    }
#else
    // The actual implementation failed sorting equivalence and is not compiled into Player.
    internal sealed class DecorationMaterialPool:IOptimizationModule
    {
        internal static readonly DecorationMaterialPool Instance=new DecorationMaterialPool();
        internal static bool Enabled,Requested;
        internal static string Status="research-only: sorting tie mismatch";
        public string Key=>"deco-material-pool";
        internal static void Configure(bool wanted){Requested=false;Enabled=false;}
        internal static void Deferred(){}
        public void Install(Harmony harmony){throw new NotSupportedException("Material sharing failed translucent sorting equivalence; research only");}
        public void Uninstall(){Requested=false;Enabled=false;}
    }
#endif
}
