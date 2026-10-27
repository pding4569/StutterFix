// Headless storage/math adapters. These do not exercise Unity native rendering or its quaternion arithmetic.
using System;
using N = System.Numerics;
namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public int GetInstanceID() => GetHashCode();
        public static implicit operator bool(Object o) => !ReferenceEquals(o,null);
        public static bool operator ==(Object a,Object b) => ReferenceEquals(a,b);
        public static bool operator !=(Object a,Object b) => !ReferenceEquals(a,b);
        public override bool Equals(object o) => ReferenceEquals(this,o);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Component : Object { public Transform transform { get; set; } public GameObject gameObject { get; set; } }
    public class Behaviour : Component { public bool enabled { get; set; } }
    public class MonoBehaviour : Behaviour { }
    public class Camera : Behaviour { }
    public class Light : Behaviour { }
    public class Material : Object { }
    public class ScriptableObject : Object { }
    public class AnimationCurve { public float Evaluate(float t) => t; }
    public class RectOffset { public int left,right,top,bottom; }
    public class GameObject : Object { public bool activeInHierarchy => true; }
    public class Transform : Component { public Quaternion rotation { get; set; } = Quaternion.identity; }
    public class RectTransform : Transform { public Vector2 anchoredPosition { get; set; } }
    public class CanvasRenderer { public float Alpha = 1; public void SetAlpha(float a) => Alpha = a; }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x=x; this.y=y; }
        public static Vector2 zero => new Vector2();
        public float magnitude => MathF.Sqrt(x*x+y*y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x,a.y-b.y);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x*b,a.y*b);
    }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; } }
    public struct Vector4 { public float x,y,z,w; }
    public struct Rect { public float x,y,width,height; }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1) { this.r=r;this.g=g;this.b=b;this.a=a; }
        public static Color clear => new Color();
        public static Color white => new Color(1,1,1,1);
        public static Color operator +(Color a,Color b) => new Color(a.r+b.r,a.g+b.g,a.b+b.b,a.a+b.a);
        public static Color operator -(Color a,Color b) => new Color(a.r-b.r,a.g-b.g,a.b-b.b,a.a-b.a);
        public static Color operator *(Color a,float b) => new Color(a.r*b,a.g*b,a.b*b,a.a*b);
    }
    public struct Color32 { public byte r,g,b,a; }
    public struct Quaternion
    {
        public float x,y,z,w;
        public Quaternion(float x,float y,float z,float w) { this.x=x;this.y=y;this.z=z;this.w=w; }
        public static Quaternion identity => new Quaternion(0,0,0,1);
        private N.Quaternion Native => new N.Quaternion(x,y,z,w);
        private static Quaternion From(N.Quaternion q) => new Quaternion(q.X,q.Y,q.Z,q.W);
        public static Quaternion Euler(float x,float y,float z) => From(N.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));
        public static Quaternion SlerpUnclamped(Quaternion a,Quaternion b,float t) => From(N.Quaternion.Slerp(a.Native,b.Native,t));
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t) => SlerpUnclamped(a,b,Math.Clamp(t,0,1));
        public static float Angle(Quaternion a,Quaternion b) => 2*MathF.Acos(MathF.Min(1,MathF.Abs(N.Quaternion.Dot(a.Native,b.Native))))*180/MathF.PI;
        public static Quaternion operator *(Quaternion a,Quaternion b) => From(a.Native*b.Native);
    }
    public enum LogType { Error, Assert, Warning, Log, Exception }
    public static class Debug { public static void Log(object x) => Console.WriteLine(x); public static void LogWarning(object x) => Console.WriteLine(x); public static void LogError(object x) => throw new Exception(x.ToString()); }
    public static class Application { public static bool isPlaying => true; public static bool isEditor => false; }
    public static class Time { public static int frameCount => 0; public static float realtimeSinceStartup => 0; }
    public static class Resources { public static Object Load(string path) => null; }
    public static class Mathf
    {
        public const float PI = MathF.PI;
        public static float Abs(float v) => MathF.Abs(v);
        public static float Clamp(float v,float min,float max) => Math.Clamp(v,min,max);
        public static float Clamp01(float v) => Clamp(v,0,1);
        public static float Pow(float x,float y) => MathF.Pow(x,y);
        public static float Sqrt(float v) => MathF.Sqrt(v);
        public static float Sin(float v) => MathF.Sin(v);
        public static float Cos(float v) => MathF.Cos(v);
        public static float Asin(float v) => MathF.Asin(v);
        public static int FloorToInt(float v) => (int)MathF.Floor(v);
        public static int CeilToInt(float v) => (int)MathF.Ceiling(v);
        public static float Max(float a,float b) => MathF.Max(a,b);
        public static int Max(int a,int b) => Math.Max(a,b);
        public static float Min(float a,float b) => MathF.Min(a,b);
    }
}
