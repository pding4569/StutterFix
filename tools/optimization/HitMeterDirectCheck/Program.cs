using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DG.Tweening;
using HarmonyLib;
using StutterFix;
using UnityEngine;
using UnityEngine.UI;

class Program
{
    const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int scenarios, frames, values, contracts;
    static long fast, slow, retired;
    static Action<Tick,string,Color,bool,float> showOriginal, showPatched;
    static Action<Hand,string,float,bool> movePatched;
    static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
    static int Main(string[] args)
    { try { Run(args); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; } }
    static void Run(string[] args)
    {
        Require(!HitMeterFade.HitMeterDirectKill,"direct kill defaults off"); contracts++;
        GameIlCheck.Check(args.Length==0 ? @"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed" : args[0]);
        typeof(DOTween).GetField("initialized", All).SetValue(null, true);
        DOTween.useSafeMode = false;
        DOTween.defaultUpdateType = UpdateType.Manual;
        DOTween.SetTweensCapacity(4096, 64);
        showOriginal = Compile<Action<Tick,string,Color,bool,float>>(typeof(Tick),nameof(Tick.Original),HitMeterFade.Transpiler);
        showPatched = Compile<Action<Tick,string,Color,bool,float>>(typeof(Tick),nameof(Tick.Patched),HitMeterFade.ShowTranspiler);
        movePatched = Compile<Action<Hand,string,float,bool>>(typeof(Hand),nameof(Hand.Patched),HitMeterFade.HandTranspiler);
        try
        {
            Transpilers();
            for (int seed = 0; seed < 2304; seed++) Compare(seed);
            TickContracts(); HandContracts(); FadeRegression();
            Console.WriteLine($"PASS scenarios={scenarios} frames={frames} bit_values={values} contracts={contracts}");
            Console.WriteLine($"comparison KillFast={fast} KillSlow={slow} HandKilled={retired}");
            Console.WriteLine("Installed DOTween.dll + production transpilers emitted as DynamicMethod; fake Unity storage/math/UI. Game Harmony installation, Unity native pixels, Mono and FPS NOT tested.");
        }
        finally { Clean(); }
    }
    static T Compile<T>(Type owner,string name,Func<IEnumerable<CodeInstruction>,IEnumerable<CodeInstruction>> transform) where T:Delegate
    {
        var method=AccessTools.Method(owner,name);
        var signature=new[]{owner}.Concat(method.GetParameters().Select(p=>p.ParameterType)).ToArray();
        var dynamic=new DynamicMethod(name+"Transpiled",typeof(void),signature,typeof(Program),true);
        var gen=dynamic.GetILGenerator();
        foreach(var local in method.GetMethodBody().LocalVariables) gen.DeclareLocal(local.LocalType,local.IsPinned);
        var code=transform(IlReader.Read(method,gen));
        foreach(var c in code)
        {
            Require(c.blocks.Count==0,"fixture must have no exception blocks");
            foreach(var label in c.labels) gen.MarkLabel(label);
            object o=c.operand;
            if(o==null) gen.Emit(c.opcode);
            else if(o is MethodInfo m) gen.Emit(c.opcode,m);
            else if(o is ConstructorInfo ctor) gen.Emit(c.opcode,ctor);
            else if(o is FieldInfo f) gen.Emit(c.opcode,f);
            else if(o is LocalBuilder l) gen.Emit(c.opcode,l);
            else if(o is Label label) gen.Emit(c.opcode,label);
            else if(o is Label[] labels) gen.Emit(c.opcode,labels);
            else if(o is Type t) gen.Emit(c.opcode,t);
            else if(o is string s) gen.Emit(c.opcode,s);
            else if(o is float v) gen.Emit(c.opcode,v);
            else if(o is int i) gen.Emit(c.opcode,i);
            else if(o is byte b) gen.Emit(c.opcode,b);
            else if(o is sbyte sb) gen.Emit(c.opcode,sb);
            else if(o is short s16) gen.Emit(c.opcode,s16);
            else throw new Exception("Unsupported IL operand "+o.GetType());
        }
        return (T)dynamic.CreateDelegate(typeof(T));
    }
    static void Clean() { DOTween.KillAll(); HitMeterFade.Shutdown(); }
    static void Step(float dt) => DOTween.ManualUpdate(dt, dt);
    static void Bits(float a, float b, string where)
    {
        values++;
        Require(BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b), $"{where}: {a:R} != {b:R}");
    }
    static void ColorBits(Color a, Color b, string where)
    { Bits(a.r,b.r,where+" r"); Bits(a.g,b.g,where+" g"); Bits(a.b,b.b,where+" b"); Bits(a.a,b.a,where+" a"); }
    static Color ClearWhite => new Color(1,1,1,0);
    class Tick
    {
        internal readonly Image Fill = new Image(), Outline = new Image();
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Original(string id, Color color, bool outline, float duration)
        {
            string other = id + "o";
            DOTween.Kill(id); DOTween.Kill(other);
            Fill.color = color; Outline.color = outline ? Color.white : ClearWhite;
            Fill.DOColor(new Color(color.r,color.g,color.b,0),duration).SetEase(Ease.InQuad).SetId(id).OnKill(()=>Fill.color=Color.clear);
            if (outline) Outline.DOColor(ClearWhite,duration).SetEase(Ease.InQuad).SetId(other).OnKill(()=>Outline.color=ClearWhite);
        }
        // The production transpilers transform this copy; KillTick/TrackTick are linked from the product.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Patched(string id, Color color, bool outline, float duration)
        {
            string other = id + "o";
            DOTween.Kill(id); DOTween.Kill(other);
            Fill.color = color; Outline.color = outline ? Color.white : ClearWhite;
            Fill.DOColor(new Color(color.r,color.g,color.b,0),duration).SetEase(Ease.InQuad).SetId(id).OnKill(()=>Fill.color=Color.clear);
            if (outline) Outline.DOColor(ClearWhite,duration).SetEase(Ease.InQuad).SetId(other).OnKill(()=>Outline.color=ClearWhite);
        }
    }
    class Hand
    {
        internal readonly RectTransform Rect = new RectTransform();
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Original(string id, float angle, bool curved)
        {
            if (curved) Rect.DORotateQuaternion(Quaternion.Euler(0,0,angle),.25f).SetEase(Ease.OutCubic).SetId(id);
            else Rect.DOAnchorPos(new Vector2(-angle*2.5f,-62),.25f).SetEase(Ease.OutCubic).SetId(id);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Patched(string id, float angle, bool curved)
        {
            if (curved) Rect.DORotateQuaternion(Quaternion.Euler(0,0,angle),.25f).SetEase(Ease.OutCubic).SetId(id);
            else Rect.DOAnchorPos(new Vector2(-angle*2.5f,-62),.25f).SetEase(Ease.OutCubic).SetId(id);
        }
    }
    static void Compare(int seed)
    {
        Clean(); HitMeterFade.HitMeterDirectKill = seed % 6 != 0; HitMeterFade.Enabled=seed%4!=0;
        DOTween.defaultRecyclable = (seed & 1) != 0;
        var rng = new Random(seed);
        int count = 1 + seed % 7;
        var baseline = Enumerable.Range(0,count).Select(_=>new Tick()).ToArray();
        var direct = Enumerable.Range(0,count).Select(_=>new Tick()).ToArray();
        var oldHand = new Hand(); var newHand = new Hand();
        bool curved = (seed & 2) != 0;
        for (int frame = 0; frame < 120; frame++)
        {
            if (seed % 6 == 2 && frame % 23 == 0) HitMeterFade.HitMeterDirectKill = !HitMeterFade.HitMeterDirectKill;
            if (seed % 6 == 3 && frame == 43) { HitMeterFade.Shutdown(); HitMeterFade.HitMeterDirectKill=true; }
            if (frame == 31 || frame == 67)
            {
                DOTween.Kill("old-hand"); DOTween.Kill("new-hand");
                for(int i=0;i<count;i++) { DOTween.Kill("old-"+i); DOTween.Kill("new-"+i); }
                oldHand.Rect.rotation = newHand.Rect.rotation = Quaternion.identity;
                oldHand.Rect.anchoredPosition = newHand.Rect.anchoredPosition = new Vector2(0,curved ? 0 : -62);
            }
            int hits = frame >= 96 ? 0 : seed % 4 == 0 ? (frame % 12 == 0 ? 1 : 0) : rng.Next(0,seed % 4 == 3 ? 8 : 3);
            for (int hit = 0; hit < hits; hit++)
            {
                int i = rng.Next(count);
                float angle = (float)(rng.NextDouble()*120-60);
                bool outline = seed % 8 != 0 && rng.Next(2) == 0;
                var color = new Color((float)rng.NextDouble(),(float)rng.NextDouble(),(float)rng.NextDouble(),seed % 5 == 0 ? 0 : .7f);
                float life = seed % 9 == 0 ? 0 : new[]{.03f,.1f,.75f,3f}[seed%4];
                if (frame == 0 && hit == 0)
                {
                    baseline[i].Fill.DOColor(Color.white,1).SetId("old-"+i);
                    direct[i].Fill.DOColor(Color.white,1).SetId("new-"+i);
                }
                showOriginal(baseline[i],"old-"+i,color,outline,life);
                showPatched(direct[i],"new-"+i,color,outline,life);
                oldHand.Original("old-hand",angle,curved); movePatched(newHand,"new-hand",angle,curved);
            }
            Step(new[]{0f,1f/240,1f/60,.071f,.3f}[rng.Next(5)]); frames++;
            for(int i=0;i<count;i++)
            {
                ColorBits(baseline[i].Fill.color,direct[i].Fill.color,$"seed {seed} frame {frame} fill {i}");
                ColorBits(baseline[i].Outline.color,direct[i].Outline.color,$"seed {seed} frame {frame} outline {i}");
                Bits(baseline[i].Fill.canvasRenderer.Alpha,direct[i].Fill.canvasRenderer.Alpha,"fill alpha");
                Bits(baseline[i].Outline.canvasRenderer.Alpha,direct[i].Outline.canvasRenderer.Alpha,"outline alpha");
            }
            Bits(oldHand.Rect.anchoredPosition.x,newHand.Rect.anchoredPosition.x,$"seed {seed} frame {frame} hand x");
            Bits(oldHand.Rect.anchoredPosition.y,newHand.Rect.anchoredPosition.y,"hand y");
            var a=oldHand.Rect.rotation; var b=newHand.Rect.rotation;
            Bits(a.x,b.x,"rotation x"); Bits(a.y,b.y,"rotation y"); Bits(a.z,b.z,$"seed {seed} frame {frame} rotation z"); Bits(a.w,b.w,"rotation w");
        }
        fast += HitMeterFade.KillFast; slow += HitMeterFade.KillSlow; retired += HitMeterFade.HandKilled; scenarios++;
    }
    static void TickContracts()
    {
        Clean(); HitMeterFade.HitMeterDirectKill=true;
        var tick=new Tick(); showPatched(tick,"tick",Color.white,false,3);
        Require(HitMeterFade.KillSlow==2,"cold tick two scans");
        long scans=HitMeterFade.KillSlow;
        for(int i=0;i<100;i++) showPatched(tick,"tick",Color.white,false,3);
        Require(HitMeterFade.KillSlow==scans && HitMeterFade.KillFast==200,"warm tick/absent outline no scans");
        Console.WriteLine($"warm tick 100 hits: KillFast={HitMeterFade.KillFast} KillSlow delta={HitMeterFade.KillSlow-scans}"); contracts++;
        int killed=0;
        var foreign=DOTween.To(()=>0f,_=>{},1,1).SetId("tick").OnKill(()=>killed++);
        showPatched(tick,"tick",Color.white,false,3);
        Require(foreign.IsActive() && killed==0,"known owner preserves foreign same-ID tween by design");
        DOTween.Kill("tick"); Require(!foreign.IsActive() && killed==1,"Reset retains global ID kill");
        ColorBits(Color.clear,tick.Fill.color,"reset OnKill"); contracts++;
        showPatched(tick,"different",Color.white,true,.01f);
        Require(HitMeterFade.KillSlow==scans+2,"changed IDs fall back"); Step(.1f);
        float value=0;
        var recycled=DOTween.To(()=>value,x=>value=x,1,1).SetRecyclable(true);
        showPatched(tick,"different",Color.white,false,1);
        Require(recycled.IsActive(),"completed/recycled handles never kill unrelated tween"); contracts++;
        HitMeterFade.HitMeterDirectKill=false; showPatched(tick,"different",Color.white,false,1);
        HitMeterFade.HitMeterDirectKill=true; scans=HitMeterFade.KillSlow;
        showPatched(tick,"different",Color.white,false,1);
        Require(HitMeterFade.KillSlow==scans+2,"toggle clears stale tracking"); contracts++;
        var movedId=HitMeterFade.Fade(tick.Fill,Color.clear,1).SetId("before");
        HitMeterFade.TrackTick(movedId,()=>{},tick,0);
        movedId.SetId("after"); scans=HitMeterFade.KillSlow;
        HitMeterFade.KillTick("before",false,tick,0);
        Require(movedId.IsActive() && HitMeterFade.KillSlow==scans+1,"foreign ID mutation falls back without killing changed tween"); contracts++;
        HitMeterFade.Shutdown(); scans=HitMeterFade.KillSlow;
        HitMeterFade.HitMeterDirectKill=true; showPatched(tick,"different",Color.white,false,1);
        Require(HitMeterFade.KillSlow==scans+2 && HitMeterFade.KillFast==0,"unload clears handles/counters"); contracts++;
    }
    static void HandContracts()
    {
        Clean(); HitMeterFade.HitMeterDirectKill=true;
        var r=new RectTransform();
        var a=HitMeterFade.MoveHand(r,new Vector2(20,-62),.25f,false).SetEase(Ease.OutCubic); Step(.02f);
        var b=HitMeterFade.MoveHand(r,new Vector2(-20,-62),.25f,false).SetEase(Ease.OutCubic);
        Require(a.IsActive(),"old hand retained until new startup"); Step(.02f);
        Require(!a.IsActive() && b.IsActive() && HitMeterFade.HandKilled==1,"old hand retired after getter"); contracts++;
        int callbacks=0; b.OnKill(()=>callbacks++);
        HitMeterFade.MoveHand(r,new Vector2(30,-62),.25f,false).SetEase(Ease.OutCubic); Step(.02f);
        Require(b.IsActive() && callbacks==0,"foreign callback hand not retired"); contracts++;
        Clean(); HitMeterFade.HitMeterDirectKill=true;
        a=HitMeterFade.MoveHand(r,new Vector2(20,-62),.25f,false); a.Pause();
        HitMeterFade.MoveHand(r,new Vector2(30,-62),.25f,false); Step(.02f);
        Require(a.IsActive(),"paused hand not retired"); contracts++;
        Clean(); HitMeterFade.HitMeterDirectKill=true;
        a=HitMeterFade.MoveHand(r,new Vector2(20,-62),1,false); Step(.02f);
        HitMeterFade.MoveHand(r,new Vector2(30,-62),.25f,false); Step(.02f);
        Require(a.IsActive(),"changed duration hand not retired"); contracts++;
        Clean(); HitMeterFade.HitMeterDirectKill=true;
        a=HitMeterFade.MoveHand(r,new Vector2(20,-62),.25f,false); Step(.02f);
        var scaled=HitMeterFade.MoveHand(r,new Vector2(30,-62),.25f,false); scaled.timeScale=2; Step(.02f);
        Require(a.IsActive(),"changed time scale hand not retired"); contracts++;
        Clean(); HitMeterFade.HitMeterDirectKill=true;
        a=HitMeterFade.MoveHand(r,new Vector2(20,-62),.25f,false); Step(.02f);
        HitMeterFade.MoveHand(r,new Vector2(30,-62),.25f,false); HitMeterFade.Shutdown(); Step(.02f);
        Require(a.IsActive() && HitMeterFade.HandKilled==0,"pending getter stops retiring after unload"); contracts++;
    }
    static void FadeRegression()
    {
        for(int seed=0;seed<256;seed++)
        {
            Clean(); HitMeterFade.Enabled=seed%4!=0;
            var a=new Image(); var b=new Image(); a.color=b.color=new Color(.2f,.4f,.6f,seed%5==0 ? 0 : .75f);
            var end=new Color(seed%3==0 ? .7f : .2f,.4f,.6f,0);
            a.DOColor(end,.3f).SetEase(Ease.InQuad); HitMeterFade.Fade(b,end,.3f).SetEase(Ease.InQuad);
            for(int frame=0;frame<24;frame++)
            {
                Step(.016f); Bits(a.color.r,b.color.r,"fade r"); Bits(a.color.g,b.color.g,"fade g"); Bits(a.color.b,b.color.b,"fade b");
                Require(Math.Abs(a.color.a-b.color.a*b.canvasRenderer.Alpha)<1e-6f,"existing fade effective alpha");
            }
            contracts++;
        }
        Console.WriteLine("HitMeterFade regression: 256 sequences / 6144 frames");
    }
    static void Transpilers()
    {
        var gen=new DynamicMethod("labels",typeof(void),Type.EmptyTypes).GetILGenerator();
        var il=IlReader.Read(AccessTools.Method(typeof(Tick),nameof(Tick.Patched)),gen);
        var result=HitMeterFade.TickTranspiler(il).ToList();
        Require(result.Count(c=>c.operand is MethodInfo m && m.Name==nameof(HitMeterFade.KillTick))==2,"two kill replacements");
        Require(result.Count(c=>c.operand is MethodInfo m && m.Name==nameof(HitMeterFade.TrackTick))==2,"two callback replacements"); contracts++;
        var changed=IlReader.Read(AccessTools.Method(typeof(Tick),nameof(Tick.Patched)),gen).Where(c=>!(c.operand is MethodInfo m && m.Name=="Kill")).ToList();
        Require(HitMeterFade.TickTranspiler(changed).SequenceEqual(changed),"unknown Show shape unchanged"); contracts++;
        var hand=HitMeterFade.HandTranspiler(IlReader.Read(AccessTools.Method(typeof(Hand),nameof(Hand.Patched)),gen)).ToList();
        Require(hand.Count(c=>c.operand is MethodInfo m && (m.Name==nameof(HitMeterFade.MoveHand)||m.Name==nameof(HitMeterFade.RotateHand)))==2,"two hand replacements"); contracts++;
    }
}
