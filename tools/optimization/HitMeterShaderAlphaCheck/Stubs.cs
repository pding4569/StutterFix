using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object o) => o != null && !o.Destroyed;
        public static bool operator !(Object o) => !(bool)o;
        public static void Destroy(Object o) { if (o == null) return; o.Destroyed = true; if (o is Component c) c.gameObject.Items.Remove(c); }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T[] GetComponents<T>() where T : class => gameObject.Items.OfType<T>().ToArray();
        public T[] GetComponentsInParent<T>(bool include) where T : class
        {
            var result = new List<T>(); for (var t = transform; t != null; t = t.parent) result.AddRange(t.gameObject.Items.OfType<T>()); return result.ToArray();
        }
        public T GetComponent<T>() where T : class => gameObject.Items.OfType<T>().FirstOrDefault();
        public Component GetComponent(string name) => gameObject.Items.OfType<Component>().FirstOrDefault(c => c.GetType().FullName == name);
    }
    public class Transform : Component { public Transform parent; }
    public class RectTransform : Transform { }
    public class GameObject : Object
    {
        public readonly List<Component> Items = new List<Component>();
        public Transform transform;
        public GameObject() { transform = new RectTransform { gameObject = this }; Items.Add(transform); }
        public T AddComponent<T>() where T : Component, new() { var c = new T { gameObject = this }; Items.Add(c); return c; }
    }
    public class CanvasRenderer : Component
    {
        private float alpha = 1;
        public float Inherited = 1;
        public int Writes;
        public void SetAlpha(float a) { alpha = a; Writes++; }
        public float GetAlpha() => alpha;
        public float GetInheritedAlpha() => Inherited;
    }
    [Flags] public enum AdditionalCanvasShaderChannels { None = 0, TexCoord1 = 1, Normal = 2, Tangent = 4 }
    public class Canvas : Component
    {
        public static event Action willRenderCanvases;
        public AdditionalCanvasShaderChannels additionalShaderChannels;
        public static void Raise() => willRenderCanvases?.Invoke();
    }
    public class CanvasGroup : Component { }
    public class Shader : Object
    {
        public bool isSupported = true;
        public static bool ThrowUpload;
        public static readonly float[] Snapshot = new float[256];
        public static int PropertyToID(string name) => 1;
        public static void SetGlobalFloatArray(int id, float[] a) { if (ThrowUpload) throw new Exception("upload failure"); Array.Copy(a, Snapshot, a.Length); }
    }
    public enum HideFlags { HideAndDontSave }
    public class Material : Object
    {
        public HideFlags hideFlags;
        public Material(Shader shader) { }
        public void SetFloat(string name, float value) { }
    }
    public class AssetBundle : Object
    {
        public static bool ThrowLoad;
        public static AssetBundle LoadFromMemory(byte[] bytes) { if (ThrowLoad) throw new Exception("load failure"); return new AssetBundle(); }
        public T LoadAsset<T>(string name) where T : new() => new T();
        public void Unload(bool all) { }
    }
    public static class Time { public static int frameCount; }
    public struct Vector4 { public float x; }
    public struct UIVertex { public Vector4 uv1; }
}
namespace UnityEngine.Events { public delegate void UnityAction(); }
namespace UnityEngine.SceneManagement
{
    public struct Scene { }
    public static class SceneManager
    {
        public static event Action<Scene, Scene> activeSceneChanged;
        public static event Action<Scene> sceneUnloaded;
        public static void Change() => activeSceneChanged?.Invoke(default, default);
        public static void Unload() => sceneUnloaded?.Invoke(default);
    }
}
namespace UnityEngine.UI
{
    using UnityEngine;
    using UnityEngine.Events;
    public class Graphic : Component
    {
        protected Material m_Material;
        private static readonly Material stock = new Material(new Shader());
        public Material defaultMaterial => stock;
        public Material Raw => m_Material;
        public Material material { get => m_Material ?? stock; set { m_Material = value; materialDirty?.Invoke(); } }
        public Material materialForRendering => material;
        private event UnityAction vertexDirty, materialDirty;
        public void RegisterDirtyVerticesCallback(UnityAction a) => vertexDirty += a;
        public void UnregisterDirtyVerticesCallback(UnityAction a) => vertexDirty -= a;
        public void RegisterDirtyMaterialCallback(UnityAction a) => materialDirty += a;
        public void UnregisterDirtyMaterialCallback(UnityAction a) => materialDirty -= a;
        public void SetVerticesDirty() => vertexDirty?.Invoke();
    }
    public class Image : Graphic
    {
        public Canvas canvas;
        public CanvasRenderer canvasRenderer => GetComponent<CanvasRenderer>();
    }
    public class Mask : Component { }
    public class RectMask2D : Component { }
    public class BaseMeshEffect : Component
    {
        public bool enabled = true;
        public bool IsActive() => enabled;
        public virtual void ModifyMesh(VertexHelper vh) { }
        protected virtual void OnDisable() { }
    }
    public class VertexHelper
    {
        public UIVertex[] Vertices = new UIVertex[4];
        public int currentVertCount => Vertices.Length;
        public void PopulateUIVertex(ref UIVertex v, int i) { v = Vertices[i]; }
        public void SetUIVertex(UIVertex v, int i) { Vertices[i] = v; }
    }
}
namespace ADOFAI { public class ErrorMeterTick : UnityEngine.Component { } }
namespace StutterFix
{
    internal static class Lg { internal static void I(string s) => Console.WriteLine(s); }
    internal static class HitMeterAlphaProbe { internal static string Run() { HitMeterShaderAlpha.Load(); HitMeterShaderAlpha.Calibrate(1, "simulated probe"); return "stub"; } }
}
