using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace UnityEngine
{
    [StructLayout(LayoutKind.Sequential)] public struct Vector4 { public float x,y,z,w; public Vector4(float a,float b,float c,float d) {x=a;y=b;z=c;w=d;} }
    [StructLayout(LayoutKind.Sequential)] public struct Matrix4x4 { public Vector4 a,b,c,d; }
    public static class Shader
    {
        static readonly Dictionary<string,int> ids = new Dictionary<string,int>();
        public static int PropertyToID(string key) { if (!ids.TryGetValue(key,out int n)) ids.Add(key,n=ids.Count+1);return n; }
        public static readonly HashSet<string> Keywords = new HashSet<string>();
        public static bool IsKeywordEnabled(string name) => Keywords.Contains(name);
    }
    public class Material
    {
        public readonly HashSet<string> Keywords = new HashSet<string>();
        public bool IsKeywordEnabled(string name) => Keywords.Contains(name);
        public static implicit operator bool(Material m) => m != null;
    }
}
namespace StutterFix { internal static class DirectFull { } }
