// Original shader parity fixture, MIT, StutterFix contributors.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SpriteRowFixture
{
    [Serializable]
    public sealed class Result
    {
        public string editor, device, error;
        public int conditions, differences, negativeControls;
        public bool passed, bundleBuilt;
    }

    static void Require(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException(reason);
    }

    public static void Run()
    {
        var result = new Result { editor = Application.unityVersion, device = SystemInfo.graphicsDeviceName };
        Material reference = null, candidate = null;
        Mesh mesh = null;
        Texture2D source = null, read = null;
        RenderTexture target = null;
        ComputeBuffer rows = null;
        int exit = 0;
        try
        {
            Require(result.editor == "6000.3.10f1" && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11,
                "unexpected fixture Editor/API");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/SpriteChannels.shader");
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/SpriteChannels.compute");
            Require(shader && shader.isSupported && compute, "shader unavailable");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.message);
            foreach (var message in ShaderUtil.GetComputeShaderMessages(compute))
                Require(message.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error, message.message);
            reference = new Material(shader);
            candidate = new Material(shader);
            candidate.EnableKeyword("SF_ALPHA_ROWS");
            mesh = new Mesh
            {
                vertices = new[] { new Vector3(-1,-1), new Vector3(1,-1), new Vector3(1,1), new Vector3(-1,1) },
                uv = new[] { new Vector2(-.3f,-.4f), new Vector2(1.2f,-.4f), new Vector2(1.2f,1.3f), new Vector2(-.3f,1.3f) },
                triangles = new[] { 0,1,2,0,2,3 },
                colors = new[] { Color.white, Color.white, Color.white, Color.white }
            };
            const int width = 65, height = 33;
            source = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            // Shared Draw reads the complete 64x64 target.
            target = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Require(target.Create(), "render target failed");
            read = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            rows = new ComputeBuffer(2, 4);
            int kernel = compute.FindKernel("AlphaRows");
            compute.SetInt("Width", width);
            compute.SetInt("Height", height);
            compute.SetTexture(kernel, "Source", source);
            compute.SetBuffer(kernel, "Rows", rows);
            reference.SetTexture("_MainTex", source);
            candidate.SetTexture("_MainTex", source);
            var pixels = new Color32[width * height];
            foreach (var pattern in new[] { (0,7), (9,20), (25,33) })
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        pixels[y*width+x] = new Color32((byte)(x*17+y), (byte)(x+y*11), 180,
                            y >= pattern.Item1 && y < pattern.Item2 ? (byte)(1+(x*23+y)%255) : (byte)0);
                source.SetPixels32(pixels);
                source.Apply(false, false);
                rows.SetData(new uint[] { height, 0 });
                compute.Dispatch(kernel, (width+7)/8, (height+7)/8, 1);
                var bounds = new uint[2];
                rows.GetData(bounds);
                Require(bounds[0] == pattern.Item1 && bounds[1] == pattern.Item2, "row reduction disagrees with CPU bounds");
                for (int wrap = 0; wrap < 4; wrap++)
                {
                    source.wrapModeU = (wrap & 1) == 0 ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                    source.wrapModeV = (wrap & 2) == 0 ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                    var guard = new Vector4(Mathf.Max(0,(int)bounds[0]-2)/(float)height,
                        Mathf.Min(height,(int)bounds[1]+2)/(float)height, 2f/height, (wrap & 2) == 0 ? 0 : 1);
                    candidate.SetVector("_SfAlphaRows", guard);
                    foreach (var filter in new[] { FilterMode.Point, FilterMode.Bilinear })
                        foreach (float repeat in new[] { -3f, .55f, 1f, 8f, 100f })
                        {
                            source.filterMode = filter;
                            foreach (var material in new[] { reference, candidate })
                            {
                                material.SetFloat("RepeatX", 100f);
                                material.SetFloat("RepeatY", repeat);
                                material.SetColor("_Color", new Color(.77f,.63f,.81f,.71f));
                            }
                            var expected = SpriteChannelFixture.Draw(reference, mesh, target, read);
                            int delta = SpriteChannelFixture.Difference(expected, SpriteChannelFixture.Draw(candidate, mesh, target, read));
                            result.conditions++;
                            result.differences += delta;
                            Require(delta == 0, $"row mask pixel mismatch {pattern} wrap={wrap} filter={filter} repeat={repeat}");
                            if (wrap != 0 || filter != FilterMode.Bilinear || repeat != 1f) continue;
                            candidate.SetVector("_SfAlphaRows", new Vector4(1,0,0,0));
                            if (SpriteChannelFixture.Difference(expected, SpriteChannelFixture.Draw(candidate, mesh, target, read)) > 0)
                                result.negativeControls++;
                            candidate.SetVector("_SfAlphaRows", guard);
                        }
                }
            }
            Require(result.conditions == 120 && result.negativeControls == 3, "incomplete parity controls");
            result.passed = true;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../row-bundle"));
            Directory.CreateDirectory(path);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            Require(BuildPipeline.BuildAssetBundles(path, new[] { new AssetBundleBuild
            {
                assetBundleName = "stutterfix_sprite_rows",
                assetNames = new[] { "Assets/Shaders/SpriteChannels.shader", "Assets/Shaders/SpriteChannels.compute" }
            } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64), "row shader bundle failed");
            result.bundleBuilt = true;
        }
        catch (Exception error)
        {
            result.error = error.ToString();
            exit = 1;
            Debug.LogException(error);
        }
        finally
        {
            rows?.Dispose();
            if (target) target.Release();
            foreach (var asset in new UnityEngine.Object[] { reference, candidate, mesh, source, read, target })
                if (asset) UnityEngine.Object.DestroyImmediate(asset);
            File.WriteAllText(Path.Combine(Application.dataPath, "../sprite-row-result.json"), JsonUtility.ToJson(result, true));
            EditorApplication.Exit(exit);
        }
    }
}
