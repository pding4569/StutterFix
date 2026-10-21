// Managed adapter for the production hierarchy algorithm. No Unity/game loads.
using N = System.Numerics;
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3();
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.Native + b.Native);
        internal N.Vector3 Native => new N.Vector3(x, y, z);
        internal static Vector3 From(N.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        internal N.Quaternion Native => new N.Quaternion(x, y, z, w);
        internal static Quaternion Z(float angle)
        { var q = N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitZ, angle); return new Quaternion { x = q.X, y = q.Y, z = q.Z, w = q.W }; }
        public static Quaternion Euler(float x, float y, float z) => Z(z * MathF.PI / 180);
        public static Quaternion Normalize(Quaternion q)
        { var n = N.Quaternion.Normalize(q.Native); return new Quaternion { x = n.X, y = n.Y, z = n.Z, w = n.W }; }
        public static Vector3 operator *(Quaternion q, Vector3 v) => Vector3.From(N.Vector3.Transform(v.Native, q.Native));
    }
    public struct Bounds
    {
        public Vector3 min, max;
        public Bounds(Vector3 center, Vector3 size)
        { min = Vector3.From(center.Native - size.Native / 2); max = Vector3.From(center.Native + size.Native / 2); }
        public void Encapsulate(Vector3 v)
        { min = Vector3.From(N.Vector3.Min(min.Native, v.Native)); max = Vector3.From(N.Vector3.Max(max.Native, v.Native)); }
    }
    public struct Matrix4x4
    {
        internal N.Matrix4x4 Native;
        public static Matrix4x4 identity => new Matrix4x4 { Native = N.Matrix4x4.Identity };
        public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) => new Matrix4x4 {
            Native = N.Matrix4x4.CreateScale(s.Native) * N.Matrix4x4.CreateFromQuaternion(q.Native) * N.Matrix4x4.CreateTranslation(p.Native) };
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new Matrix4x4 { Native = b.Native * a.Native };
        public Vector3 MultiplyPoint3x4(Vector3 v) => Vector3.From(N.Vector3.Transform(v.Native, Native));
        public Vector3 MultiplyVector(Vector3 v) => Vector3.From(N.Vector3.TransformNormal(v.Native, Native));
    }
    public class Transform
    {
        public Transform parent;
        public Vector3 localPosition, localScale = new Vector3(1, 1, 1);
        public Quaternion localRotation = Quaternion.Z(0);
        public bool Destroyed;
        public static implicit operator bool(Transform t) => t != null && !t.Destroyed;
    }
}
