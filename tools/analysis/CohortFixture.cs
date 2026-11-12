using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using StutterFix;
public static class CohortFixture
{
    [Serializable] sealed class Result
    {
        public string editor,error,dotweenSha256,scope="installed DOTween/Unity Mono cohort arithmetic and release; game/FPS UNMEASURED";
        public long comparisons,mismatches,hits,misses,invalidReads,invalidReleases;public int easeKinds,groupsAfterRelease;public bool passed;
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
            var wrapper=new DynamicMethod("InstalledCohortEase",typeof(float),new[]{typeof(int),typeof(float),typeof(float),typeof(float),typeof(float)},typeof(CohortFixture),true);
            var il=wrapper.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldnull);for(short i=1;i<5;i++)il.Emit(OpCodes.Ldarg,i);il.Emit(OpCodes.Call,method);il.Emit(OpCodes.Ret);
            var evaluate=(ExactEaseMemo.Evaluator)wrapper.CreateDelegate(typeof(ExactEaseMemo.Evaluator));var pool=new ExactEaseCohorts(evaluate,256);
            var random=new System.Random(334532);var leases=new ExactEaseCohorts.Lease[8];
            foreach(object kind in Enum.GetValues(ease))
            {
                if(kind.ToString().StartsWith("INTERNAL_",StringComparison.Ordinal))continue;int e=Convert.ToInt32(kind);result.easeKinds++;
                for(int frame=0;frame<128;frame++)
                {
                    float duration=.0001f+(float)random.NextDouble()*10,over=(float)random.NextDouble()*5,period=(float)random.NextDouble()*2;
                    for(int i=0;i<leases.Length;i++)leases[i]=pool.Acquire(frame,frame,0,e,duration,over,period);
                    foreach(float time in new[]{0f,duration*.000001f,duration*.25f,duration*.5f,duration*.999999f,duration})
                    for(int i=0;i<leases.Length;i++)
                    {
                        float original=evaluate(e,time,duration,over,period);
                        if(!pool.TryEvaluate(leases[i],time,out var actual))throw new InvalidOperationException("valid lease rejected");
                        result.comparisons++;if(Bits(original)!=Bits(actual))result.mismatches++;
                    }
                    // Reference removal does not invalidate live siblings.
                    for(int i=0;i<leases.Length;i++)if(!pool.Release(leases[i]))throw new InvalidOperationException("release failed");
                }
            }
            result.hits=pool.Hits;result.misses=pool.Misses;result.invalidReads=pool.InvalidReads;result.invalidReleases=pool.InvalidReleases;result.groupsAfterRelease=pool.LiveGroups;
            result.passed=result.easeKinds>=30&&result.comparisons>100000&&result.mismatches==0&&result.hits>0&&result.invalidReads==0&&result.invalidReleases==0&&pool.LiveGroups==0;
            if(!result.passed)throw new InvalidOperationException("cohort parity/lifecycle failed");
        }
        catch(Exception e){result.error=e.ToString();exit=1;UnityEngine.Debug.LogException(e);}
        File.WriteAllText(Path.Combine(Application.dataPath,"../cohort-result.json"),JsonUtility.ToJson(result,true));EditorApplication.Exit(exit);
    }
}
