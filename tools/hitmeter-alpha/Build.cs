using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class HitMeterAlphaBuild
{
    public static void Run()
    {
        int exit = 0;
        try
        {
            if (Application.unityVersion != "6000.3.10f1") throw new Exception("wrong Unity version");
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../bundle"));
            Directory.CreateDirectory(output);
            var build = new AssetBundleBuild { assetBundleName = "hitmeteralpha.bundle", assetNames = new[] { "Assets/HitMeterAlpha.shader" } };
            var result = BuildPipeline.BuildAssetBundles(output, new[] { build }, BuildAssetBundleOptions.ForceRebuildAssetBundle
                | BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (result == null) throw new Exception("bundle build failed");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/HitMeterAlpha.shader");
            if (UnityEditor.ShaderUtil.ShaderHasError(shader)) throw new Exception("shader compile errors");
            Debug.Log("HITMETER_ALPHA_BUNDLE_OK " + output);
        }
        catch (Exception ex) { Debug.LogException(ex); exit = 1; }
        EditorApplication.Exit(exit);
    }
}
