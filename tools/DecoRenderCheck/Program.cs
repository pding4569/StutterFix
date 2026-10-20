// Control-flow fixture using the production incremental collector/job/upload code.
// Fake Unity/Jobs below: no engine, worker scheduling, native rounding, pixels or FPS evidence.
using System;
using System.Collections.Generic;
using System.Globalization;
using StutterFix;
using UnityEngine;

namespace Unity.Collections {
    enum Allocator { Persistent }
    sealed class ReadOnlyAttribute : Attribute {}
    static class Allocations { internal static int Live; }
    struct NativeArray<T> {
        sealed class Storage { internal T[] Values; internal bool Live = true; }
        Storage data;
        public NativeArray(int count, Allocator allocator) { data = new Storage { Values = new T[count] }; Allocations.Live++; }
        public bool IsCreated => data != null && data.Live;
        public int Length => data.Values.Length;
        public void CopyTo(T[] target) => Array.Copy(data.Values, target, data.Values.Length);
        public T this[int n] { get => data.Values[n]; set => data.Values[n] = value; }
        public void Dispose() { if (!IsCreated) throw new Exception("double dispose"); data.Live = false; Allocations.Live--; }
    }
}
namespace Unity.Collections.LowLevel.Unsafe { sealed class NativeDisableParallelForRestrictionAttribute : Attribute {} }
namespace Unity.Jobs {
    interface IJobParallelFor { void Execute(int n); }
    struct JobHandle { public void Complete() {} }
    static class Jobs { public static JobHandle Schedule<T>(this T job, int count, int batch) where T : struct, IJobParallelFor { for (int i=0;i<count;i++) job.Execute(i); return new JobHandle(); } }
}
namespace HarmonyLib {
    static class AccessTools {
        public delegate ref F FieldRef<T,F>(T instance);
        static ref Color ColorRef(scrDecoration d) => ref d.Color;
        static ref bool EnabledRef(scrDecoration d) => ref d.Enabled;
        public static FieldRef<T,F> FieldRefAccess<T,F>(string name) => name == "rendererColor"
            ? (FieldRef<T,F>)(object)(FieldRef<scrDecoration,Color>)ColorRef
            : (FieldRef<T,F>)(object)(FieldRef<scrDecoration,bool>)EnabledRef;
    }
}
namespace UnityEngine.Rendering { enum MeshUpdateFlags { DontRecalculateBounds } }
namespace UnityEngine {
    class Object { public static implicit operator bool(Object o) => o != null; }
    class GameObject : Object { public bool activeInHierarchy = true; }
    class Material : Object { public int renderQueue=3000; public int GetInstanceID()=>1; }
    class Texture : Object { public int width=8,height=8; }
    class Sprite : Object { public Bounds bounds = new Bounds(Vector3.zero,new Vector3(2,2,0)); }
    struct Vector2 {
        public float x,y; public Vector2(float x,float y) {this.x=x;this.y=y;}
    }
    struct Vector3 {
        public float x,y,z; public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new Vector3();
        public static Vector3 operator *(Vector3 a,float f)=>new Vector3(a.x*f,a.y*f,a.z*f);
    }
    struct Quaternion { public float x,y,z,w; public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;} }
    struct Color {
        public float r,g,b,a; public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color clear=>new Color();
    }
    struct Color32 {
        public byte r,g,b,a;
        public static implicit operator Color32(Color c)=>new Color32{r=(byte)(Math.Clamp(c.r,0,1)*255+.5f),g=(byte)(Math.Clamp(c.g,0,1)*255+.5f),b=(byte)(Math.Clamp(c.b,0,1)*255+.5f),a=(byte)(Math.Clamp(c.a,0,1)*255+.5f)};
    }
    struct Matrix4x4 {
        public float m00,m01,m02,m03,m10,m11,m12,m13,m20,m21,m22,m23,m30,m31,m32,m33;
        public static Matrix4x4 identity=>new Matrix4x4 {m00=1,m11=1,m22=1,m33=1};
        public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new Matrix4x4 {m00=s.x,m11=s.y,m22=s.z,m03=p.x,m13=p.y,m23=p.z,m33=1};
        public Vector3 MultiplyPoint3x4(Vector3 p)=>new Vector3(m00*p.x+m01*p.y+m02*p.z+m03,m10*p.x+m11*p.y+m12*p.z+m13,m20*p.x+m21*p.y+m22*p.z+m23);
        public Vector3 MultiplyVector(Vector3 p)=>new Vector3(m00*p.x+m01*p.y+m02*p.z,m10*p.x+m11*p.y+m12*p.z,m20*p.x+m21*p.y+m22*p.z);
        public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b) { var r=a; var p=a.MultiplyPoint3x4(new Vector3(b.m03,b.m13,b.m23));r.m03=p.x;r.m13=p.y;r.m23=p.z;r.m00*=b.m00;r.m11*=b.m11;r.m22*=b.m22;return r; }
    }
    struct Bounds {
        public Vector3 center,extents;
        public Bounds(Vector3 c,Vector3 size){center=c;extents=size*.5f;}
        public void Expand(float f) {}
        public void Encapsulate(Bounds b) {}
    }
    class Transform : Object {
        public Transform parent; public Vector3 localPosition,localScale = new Vector3(1,1,1);
        public Quaternion localRotation = new Quaternion(0,0,0,1);
        public Vector3 position=>localPosition; public Quaternion rotation=>localRotation;
        public Matrix4x4 localToWorldMatrix { get { var m=Matrix4x4.TRS(localPosition,localRotation,localScale);return parent?parent.localToWorldMatrix*m:m; } }
    }
    class Camera : Object {}
    class Renderer : Object {
        public bool enabled=true,forceRenderingOff; public GameObject gameObject=new GameObject();
        public int sortingLayerID,sortingOrder; public Material sharedMaterial=new Material();
        public float Distance;
    }
    class SpriteRenderer : Renderer {
        public Transform transform=new Transform(); public Color color=new Color(1,1,1,1);
        public bool flipX,flipY;
    }
    class MeshRenderer : Object { public Bounds bounds; public bool enabled; }
    class Mesh {
        internal int Calls; internal readonly Dictionary<int,object> Data=new Dictionary<int,object>();
        public void SetVertexBufferData<T>(Unity.Collections.NativeArray<T> array,int from,int to,int count,int stream,UnityEngine.Rendering.MeshUpdateFlags flags) {
            Calls++; for(int n=0;n<count;n++) Data[to+n]=array[from+n];
        }
    }
    static class Time { public static int frameCount; }
}
class scrDecoration : UnityEngine.Object { public SpriteRenderer spriteRenderer; public Transform pivotTrans; public Color Color=new Color(1,1,1,1); public bool Enabled=true; }
class scrDecorationManager { public List<scrDecoration> Awake=new List<scrDecoration>(); }
namespace StutterFix {
    static class Dormancy {
        internal static HashSet<scrDecoration> Sleeping=new HashSet<scrDecoration>();
        internal static bool ProbeDormant(scrDecoration d)=>Sleeping.Contains(d);
        internal static List<scrDecoration> RenderAwake(scrDecorationManager m)=>m.Awake;
    }
    sealed partial class DecoRender {
        static DecoRender session; bool stopping,comparing,floatColor;
        int preparedFrame=-1,changedItems,uploadedVertices,movingCandidates;
        double computeMs,uploadMs;
        double lastBuildMs; long buildTicks,buildCount,measuredFrames,frameTicks;
        readonly HashSet<string> fallbackKeys=new HashSet<string>();
        readonly List<Item> items=new List<Item>(); readonly List<Group> groups=new List<Group>();
        readonly scrDecorationManager manager=new scrDecorationManager();
        sealed class MaterialSnapshot {}
        MaterialSnapshot ReadUniforms(SpriteRenderer r)=>new MaterialSnapshot();
        struct FloatVertex {internal Vector3 Position;internal Color Color;internal Vector2 UV;}
        struct ByteVertex {internal Vector3 Position;internal Color32 Color;internal Vector2 UV;}
        sealed class Atlas {internal int Width=32,Height=32;}
        sealed class Image {internal Atlas Atlas=new Atlas();internal Texture Source=new Texture();internal int X=4,Y=4;}
        sealed class Item {
            internal scrDecoration D;internal SpriteRenderer R;internal Sprite Sprite=new Sprite();internal Image Image=new Image();
            internal Vector2[] Vertices,UV;internal int Slot,Start,Offset,PoseFrame=-1;internal Group Run;
            internal bool NativeFallback,PoseRejected,PoseValidated,ChildIdentity,Written,Visible,Dirty,Moved,FlipX,FlipY;
            internal Matrix4x4 Matrix,ChildMatrix;internal Color Color;internal Bounds LocalBounds;
            internal Transform PivotTransform,RenderTransform;internal MaterialSnapshot Uniforms;
        }
        sealed class BarrierWatch {internal scrDecoration Owner;internal bool Enabled,Active,ForceOff;internal string Key="0/0";internal Renderer R;internal int Queue,Material;internal float Distance;}
        Camera mainCamera=new Camera();
        static float SortDistance(Renderer r,Camera c)=>r.Distance;
        static string OrderKey(int layer,int order)=>layer+"/"+order;
        readonly List<BarrierWatch> barrierWatches=new List<BarrierWatch>();
        sealed class Group {
            internal List<Item> Items=new List<Item>(),VisibleItems=new List<Item>();internal bool HasVisible,Fresh,BoundsDirty;
            internal Mesh Mesh=new Mesh();internal MeshRenderer Renderer=new MeshRenderer();internal Bounds CullBounds;
        }
        static string F(double x)=>x.ToString("F6",CultureInfo.InvariantCulture);
        static void Log(string value) {}
        static string OrderKey(Item i)=>"0/0";
        static bool SourceOff(Item i)=>i.R.forceRenderingOff;
        static bool MeshOn(scrDecoration d)=>false;
        static bool MatrixEqual(Matrix4x4 a,Matrix4x4 b)=>a.Equals(b);
        static bool ColorEqual(Color a,Color b)=>a.Equals(b);
        static int checks;
        static void Assert(bool value,string name){checks++;if(!value)throw new Exception(name);}
        Item Add(Group g,float x) {
            var t=new Transform {localPosition=new Vector3(x,0,0)};
            var i=new Item {D=new scrDecoration {pivotTrans=t},R=new SpriteRenderer {transform=t},
                Vertices=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)},
                UV=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)}};
            items.Add(i);g.Items.Add(i);return i;
        }
        internal static void Fixtures() {
            foreach(bool floating in new[]{false,true}) {
                Time.frameCount=0;Dormancy.Sleeping.Clear();
                var s=session=new DecoRender {floatColor=floating};var g=new Group();s.groups.Add(g);
                var a=s.Add(g,10);var b=s.Add(g,20);var c=s.Add(g,30);
                s.manager.Awake.Add(a.D);s.manager.Awake.Add(b.D);Dormancy.Sleeping.Add(c.D);
                s.Build();Assert(s.changedItems==3,"initial topology initialized including sleepers");
                Assert(g.Mesh.Calls==1,"initial adjacent dirty ranges coalesced");
                Assert(g.HasVisible&&g.VisibleItems.Count==2,"visible membership");
                Assert(s.uploadedVertices==12,"initial full topology");
                if(floating)Assert(((FloatVertex)g.Mesh.Data[8]).Color.a==0,"hidden float alpha zero");
                else Assert(((ByteVertex)g.Mesh.Data[8]).Color.a==0,"hidden byte alpha zero");
                Time.frameCount++;s.Build();Assert(s.changedItems==0&&s.uploadedVertices==0,"stable frame uploads nothing");
                Assert(!s.work.Contains(c),"sleeping run member is not visited on stable frame");
                Time.frameCount++;Notify(a.D);Capture(a.D,a.D.pivotTrans.position,a.D.pivotTrans.rotation,a.D.pivotTrans.localScale);s.Build();
                Assert(s.changedItems==0,"notification and repeated identical capture do not mark dirty");
                Time.frameCount++;s.ResetLayout();s.Build();
                Assert(s.changedItems==0&&s.uploadedVertices==0,"unchanged topology retains written geometry");
                Assert(g.HasVisible&&g.VisibleItems.Count==2,"retained visibility survives layout refresh");
                var newRun=new Group();s.groups.Add(newRun);var extra=s.Add(newRun,40);
                Time.frameCount++;s.ResetLayout();s.Build();
                Assert(s.changedItems==1&&s.dirtyUnwritten==1,"new run writes only its members");
                Assert(s.uploadedVertices==4,"retained run is not reuploaded for new run");
                if(floating)Assert(s.floatOutput[a.Start].Position.x==9,"float output retained across allocation");
                else Assert(s.byteOutput[a.Start].Position.x==9,"byte output retained across allocation");
                Time.frameCount++;s.manager.Awake.Remove(a.D);Dormancy.Sleeping.Add(a.D);Notify(a.D);
                s.GatherWork(false); // PrepareFrame gathers once, Build must retain the retiring item.
                s.Build();Assert(s.changedItems==1&&g.VisibleItems.Count==1,"sleep clears only retiring geometry");
                Assert(s.uploadedVertices==4,"sleep exact upload range");
                Time.frameCount++;Dormancy.Sleeping.Remove(c.D);s.manager.Awake.Add(c.D);Notify(c.D);s.Build();
                Assert(s.changedItems==1&&g.VisibleItems.Contains(c),"wake emits only newly visible item");
                Time.frameCount++;c.R.transform.localPosition=new Vector3(31,0,0);
                Capture(c.D,c.R.transform.position,c.R.transform.rotation,c.R.transform.localScale);s.Build();
                Assert(s.poseHits>0&&s.changedItems==1,"captured pose feeds geometry");
                var vertex = floating ? ((FloatVertex)g.Mesh.Data[8]).Position : ((ByteVertex)g.Mesh.Data[8]).Position;
                Assert(vertex.x==30&&vertex.y==-1,"job output reaches the correct mesh range");
                Assert(s.soaMismatches==0&&s.soaChecks>0,"first pose checked before adoption");
                Time.frameCount++;Capture(c.D,new Vector3(99,0,0),c.R.transform.rotation,c.R.transform.localScale);
                c.PoseValidated=false;s.Build();Assert(c.PoseRejected&&s.soaMismatches==1,"bad pose uses native fallback");
                Time.frameCount++;b.R.enabled=false;s.Build();Assert(!g.VisibleItems.Contains(b),"renderer disabled removes geometry");
                Time.frameCount++;s.manager.Awake.Clear();Dormancy.Sleeping.Add(c.D);Notify(c.D);s.Build();
                Assert(!g.HasVisible&&g.VisibleItems.Count==0,"empty run disabled");
                s.DisposeBuffers();s.DisposeBuffers();Assert(Unity.Collections.Allocations.Live==0,"owned buffers released exactly once");
            }
            var stats=new Samples();for(int n=1;n<=3000;n++)stats.Add(n);
            Assert(stats.State().Contains("n=2048"),"bounded timing window");
            Assert(stats.State().Contains("max=3000.000000")&&stats.State().Contains("calls=3000"),"max and lifetime call count");
            stats.Clear();Assert(stats.State().Contains("calls=0"),"stage reset clears calls");
            {
                var s=new DecoRender();var owner=new scrDecoration();
                var child=new BarrierWatch{Owner=owner,Enabled=true,Active=true};
                var tile=new BarrierWatch{Active=true,Enabled=false};
                var dormant=new BarrierWatch{Active=false};
                s.barrierWatches.AddRange(new[]{child,tile,dormant});s.IndexBarriers();
                Assert(s.liveBarriers.Count==1&&s.liveBarriers[0]==tile,"only active non-decoration objects polled");
                Assert(s.ignoredBarriers==1,"inactive waiting objects classified once");
                var checksNow=new List<BarrierWatch>(s.LiveBarrierChecks());
                Assert(checksNow.Count==1,"child meshes never polled even when enabled");
                s.notifiedOwners.Add(owner);checksNow=new List<BarrierWatch>(s.LiveBarrierChecks());
                Assert(checksNow.Count==2&&checksNow.Contains(child),"child checked only on owner event");
                s.notifiedOwners.Clear();tile.R=new Renderer{enabled=false};
                Time.frameCount++;s.AuditLateBarriers();
                Assert(!s.topologyDirty,"waiting visibility unchanged skips topology");
                tile.R.enabled=true;s.AuditLateBarriers();
                Assert(!s.topologyDirty,"second camera does not audit again");
                Time.frameCount++;s.AuditLateBarriers();
                Assert(s.topologyDirty&&s.fallbackKeys.Contains("0/0"),"drawing transition invalidates affected key");
                s.topologyDirty=false;Time.frameCount++;s.AuditLateBarriers();
                Assert(!s.topologyDirty,"cached drawing state prevents repeated rebuild");
                tile.R.Distance=5;Time.frameCount++;s.AuditLateBarriers();
                Assert(s.topologyDirty,"drawing barrier order movement remains guarded");
                s.topologyDirty=false;tile.R.enabled=false;Time.frameCount++;s.AuditLateBarriers();
                Assert(s.topologyDirty,"drawing to waiting transition rebuilds");
                s.topologyDirty=false;tile.R.Distance=9;Time.frameCount++;s.AuditLateBarriers();
                Assert(!s.topologyDirty,"waiting movement does not reset layout");
            }
            {
                Time.frameCount=100;Dormancy.Sleeping.Clear();
                var s=session=new DecoRender();var g=new Group();s.groups.Add(g);var i=s.Add(g,10);
                var child=new Transform {parent=i.D.pivotTrans,localPosition=new Vector3(2,0,0)};
                i.R.transform=child;s.manager.Awake.Add(i.D);s.Build();
                Time.frameCount++;Capture(i.D,i.D.pivotTrans.position,i.D.pivotTrans.rotation,i.D.pivotTrans.localScale);s.Build();
                Assert(s.poseHits==1&&!i.NativeFallback,"direct-child matrix can use SoA");
                Time.frameCount++;Capture(i.D,i.D.pivotTrans.position,i.D.pivotTrans.rotation,i.D.pivotTrans.localScale);
                child.localPosition=new Vector3(3,0,0);RecordLocal(child,child.localPosition);s.Build();
                Assert(((ByteVertex)g.Mesh.Data[0]).Position.x==12,"child SetLocal recorded after pivot capture");
                s.preparedFrame=Time.frameCount;Notify(i.D);Assert(s.fallbackKeys.Contains("0/0"),"late notification falls back this frame");
                s.meshStates[i.D]=false;ObserveMesh(i.D,true);Assert(s.topologyDirty,"observed mesh field transition dirties topology");
                s.topologyDirty=false;ObserveMesh(i.D,true);Assert(!s.topologyDirty,"unchanged mesh field does not rebuild runs");
                s.DisposeBuffers();Assert(Unity.Collections.Allocations.Live==0,"child-path buffers released");
            }
            Console.WriteLine("DecoRender incremental control-flow checks="+checks+" failures=0; fake Unity/Jobs, no performance or pixel claim");
            session=null;
        }
    }
}
static class Program {
    static void Main(string[] args) {
        DecoRender.Fixtures();
        if(args.Length==0)return;
        if(args.Length!=3)throw new ArgumentException("developer player autotest DLL paths required");
        for(int n=0;n<3;n++) {
            using var file=System.IO.File.OpenRead(args[n]);
            using var pe=new System.Reflection.PortableExecutable.PEReader(file);
            var md=System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            bool renderer=false,job=false,playerReference=false;
            foreach(var handle in md.TypeDefinitions) {
                var t=md.GetTypeDefinition(handle);var name=md.GetString(t.Name);
                if(name=="DecoRender")renderer=true;
                if(name=="VerticesJob"&&!t.GetDeclaringType().IsNil
                    &&md.GetString(md.GetTypeDefinition(t.GetDeclaringType()).Name)=="DecoRender")job=true;
            }
            foreach(var handle in md.TypeReferences)
                if(md.GetString(md.GetTypeReference(handle).Name)=="DecoRender")playerReference=true;
            bool expected=n!=1;
            if(renderer!=expected||job!=expected||(n==1&&playerReference))throw new Exception("diagnostic boundary: "+args[n]);
            Console.WriteLine(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(args[n]))+": DecoRender="+renderer+" VerticesJob="+job+" boundary=PASS");
        }
    }
}
