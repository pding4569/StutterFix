using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public static class BuildZCull
{
    public static void Bundle()
    {
        try {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string output=Path.Combine(root,"bundle"); Directory.CreateDirectory(output);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild {
                assetBundleName="stutterfix_zcull",assetNames=new[]{"Assets/ZCull.shader","Assets/ZCullReduce.compute"}
            }},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
            if(!manifest)throw new Exception("bundle build failed");
            var bundle=AssetBundle.LoadFromFile(Path.Combine(output,"stutterfix_zcull"));
            if(!bundle)throw new Exception("bundle reload failed");
            try {
                var shader=bundle.LoadAsset<Shader>("Assets/ZCull.shader");
                if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("shader compile/support failed");
                var material=new Material(shader);
                try {if(material.passCount!=4)throw new Exception("expected four passes");}
                finally {UnityEngine.Object.DestroyImmediate(material);}
                var compute=bundle.LoadAsset<ComputeShader>("Assets/ZCullReduce.compute");
                if(!compute||!compute.HasKernel("Reduce"))throw new Exception("reduction kernel missing");
                ZCullGpuFixture.Run(shader,compute);
                Debug.Log("ZCULL_BUNDLE=PASS reloaded=True passes=4 kernel=Reduce");
            } finally {bundle.Unload(true);}
            EditorApplication.Exit(0);
        } catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
