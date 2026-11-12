// Own fixture: load the installed game's unmodified DOTween evaluator; cache
// source is copied verbatim except for its DEV/AUTOTEST file guard.
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using StutterFix;
public static class EaseMemoFixture
{
    [Serializable] sealed class Result
    {
        public string editor,error,dotweenSha256,scope="installed DOTween/Unity Mono input-output cache parity; game clocks/pixels/FPS UNMEASURED";
        public long comparisons,mismatches,hits,misses;public int easeKinds;public bool passed;
        public Cost[] syntheticCosts;
    }
    [Serializable] sealed class Cost {public string ease;public int groupsPer1024Calls;public double directMedianNs,memoMedianNs;public string scope="synthetic Mono calls; not game CPU or FPS";}
    static volatile float sink;
    static double Batch(ExactEaseMemo.Evaluator eval,ExactEaseMemo memo,int ease,int groups)
    {
        const int n=131072;float sum=0;long begin=Stopwatch.GetTimestamp();
        for(int i=0;i<n;i++)
        {
            if(memo!=null && (i&1023)==0)memo.BeginFrame();
            float t=.123f+(i%groups)*.00001f;
            sum+=memo==null?eval(ease,t,.9f,1.70158f,.3f):memo.Evaluate(ease,t,.9f,1.70158f,.3f);
        }
        long elapsed=Stopwatch.GetTimestamp()-begin;sink=sum;return elapsed*1e9/Stopwatch.Frequency/n;
    }
    static int Bits(float f)=>BitConverter.SingleToInt32Bits(f);
    public static void Run()
    {
        var result=new Result{editor=Application.unityVersion};int exit=0;
        try
        {
            string path=@"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed\DOTween.dll";
            using(var hash=SHA256.Create())result.dotweenSha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
            var assembly=Assembly.LoadFrom(path);var ease=assembly.GetType("DG.Tweening.Ease",true);
            var method=assembly.GetType("DG.Tweening.Core.Easing.EaseManager",true).GetMethod("Evaluate",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,
                new[]{ease,assembly.GetType("DG.Tweening.EaseFunction",true),typeof(float),typeof(float),typeof(float),typeof(float)},null);
            var wrapper=new DynamicMethod("ExactInstalledEase",typeof(float),new[]{typeof(int),typeof(float),typeof(float),typeof(float),typeof(float)},typeof(EaseMemoFixture),true);
            var il=wrapper.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldnull);
            for(short i=1;i<5;i++)il.Emit(OpCodes.Ldarg,i);il.Emit(OpCodes.Call,method);il.Emit(OpCodes.Ret);
            var evaluate=(ExactEaseMemo.Evaluator)wrapper.CreateDelegate(typeof(ExactEaseMemo.Evaluator));var memo=new ExactEaseMemo(evaluate);
            var random=new System.Random(722414);
            foreach(object value in Enum.GetValues(ease))
            {
                string name=value.ToString();if(name.StartsWith("INTERNAL_",StringComparison.Ordinal))continue;
                int e=Convert.ToInt32(value);result.easeKinds++;
                for(int frame=0;frame<128;frame++)
                {
                    memo.BeginFrame();float duration=.0001f+(float)random.NextDouble()*10;
                    float over=(float)random.NextDouble()*5,period=(float)random.NextDouble()*2;
                    foreach(float time in new[]{0f,duration*.000001f,duration*.25f,duration*.5f,duration*.999999f,duration})
                    for(int repeat=0;repeat<3;repeat++)
                    {
                        float original=evaluate(e,time,duration,over,period),actual=memo.Evaluate(e,time,duration,over,period);
                        result.comparisons++;if(Bits(original)!=Bits(actual))result.mismatches++;
                    }
                }
            }
            result.hits=memo.Hits;result.misses=memo.Misses;
            var costs=new List<Cost>();
            foreach(string name in new[]{"Linear","InOutQuad","InOutSine","OutBounce","InOutElastic","InOutExpo"})
            foreach(int groups in new[]{1,8,64,1024})
            {
                int e=Convert.ToInt32(Enum.Parse(ease,name));var candidate=new ExactEaseMemo(evaluate);
                Batch(evaluate,null,e,groups);Batch(evaluate,candidate,e,groups);
                var direct=new double[5];var cached=new double[5];
                for(int i=0;i<5;i++)
                    if((i&1)==0){direct[i]=Batch(evaluate,null,e,groups);cached[i]=Batch(evaluate,candidate,e,groups);}
                    else {cached[i]=Batch(evaluate,candidate,e,groups);direct[i]=Batch(evaluate,null,e,groups);}
                Array.Sort(direct);Array.Sort(cached);costs.Add(new Cost{ease=name,groupsPer1024Calls=groups,directMedianNs=direct[2],memoMedianNs=cached[2]});
            }
            result.syntheticCosts=costs.ToArray();
            result.passed=result.easeKinds>=30 && result.comparisons>50000 && result.mismatches==0 && result.hits>0;
            if(!result.passed)throw new InvalidOperationException("DOTween cache parity failed");
        }
        catch(Exception e){result.error=e.ToString();exit=1;UnityEngine.Debug.LogException(e);}
        File.WriteAllText(Path.Combine(Application.dataPath,"../ease-memo-result.json"),JsonUtility.ToJson(result,true));EditorApplication.Exit(exit);
    }
}
