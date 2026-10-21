#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Vertex = StutterFix.GpuDecoGpu.Vertex;

namespace StutterFix
{
    // G1 diagnostic: evaluate a fresh paused snapshot, then draw GPU-written
    // buffers with borrowed game materials. Playback always restores originals.
    // No live animation ownership or per-frame scan is installed by this gate.
    internal sealed class GpuDecoSnapshot : MonoBehaviour
    {
        sealed class Row { internal Renderer R; internal Material M; internal int Item=-1; internal SceneRenderOrder.Key Key; }
        static GpuDecoSnapshot instance;
        readonly List<SpriteRenderer> originals=new List<SpriteRenderer>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<GraphicsBuffer> buffers=new List<GraphicsBuffer>();
        readonly List<MeshRenderer> renderers=new List<MeshRenderer>();
        AssetBundle bundle; GpuDecoGpu gpu; CommandBuffer commands;
        int generation; double song; bool g0;
        internal static bool Active => instance;
        internal static int Owned => instance?instance.originals.Count:0;
        static void Log(string s) => Main.Entry.Logger.Log("[gpudeco] "+s);
        static void Require(bool ok,string reason) { if(!ok) throw new InvalidOperationException("gpudeco G1: "+reason); }
        internal static bool Start(GpuDecoData data, scrDecoration[] baseline, string bundlePath)
        {
            Require(!instance,"already drawing");
            Require(FrameCompare.Paused && ADOBase.controller && ADOBase.controller.paused && Time.timeScale==0,"framecmp pause required");
            Require(data!=null && data.Schema==2 && data.Items.Length>0,"fresh export required");
            Require(Math.Abs(scrConductor.instance.songposition_minusi-data.RawTime)<1e-6,"export song time changed");
            Require(!GpuDecoCoverage.OwnedLow(data.Awake,data.Items.Length),"OWNED_LOW");
            var root=new GameObject("GpuDeco.G1") {hideFlags=HideFlags.HideAndDontSave};
            instance=root.AddComponent<GpuDecoSnapshot>();
            try { instance.Build(data,baseline,bundlePath); return instance.g0; }
            catch { Stop(); throw; }
        }
        void Build(GpuDecoData data,scrDecoration[] baseline,string bundlePath)
        {
            var timer=Stopwatch.StartNew(); generation=DecoBake.Generation; song=data.RawTime;
            Require(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11 && SystemInfo.supportsComputeShaders,"D3D11 compute required");
            var camera=GpuSplit.FindMain(); Require(camera && camera.orthographic,"orthographic camera required");
            var mode=camera.transparencySortMode;
            if(mode==TransparencySortMode.Default) mode=GraphicsSettings.transparencySortMode;
            var axis=mode==TransparencySortMode.CustomAxis
                ? (camera.transparencySortMode==TransparencySortMode.CustomAxis?camera.transparencySortAxis:GraphicsSettings.transparencySortAxis)
                : camera.transform.forward;
            Require(Mathf.Abs(axis.x)<1e-6f && Mathf.Abs(axis.y)<1e-6f && Mathf.Abs(axis.z)>0,"Z sorting axis required");
            var candidates=new Dictionary<int,int>();
            for(int i=0;i<data.Items.Length;i++) {
                var item=data.Items[i]; var d=baseline[item.Index] as scrVisualDecoration;
                Require(d && d.GetInstanceID()==item.Id && d.spriteRenderer,"export object changed");
                var r=d.spriteRenderer;
                Require(r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff && r.sharedMaterial
                    && r.sharedMaterial.GetInstanceID()==item.MaterialId && r.sprite && r.sprite.texture.GetInstanceID()==item.TextureId,"export renderer changed");
                candidates.Add(r.GetInstanceID(),i);
            }
            var rows=new List<Row>();
            foreach(var r in Resources.FindObjectsOfTypeAll<Renderer>()) {
                if(!r || !r.gameObject.scene.IsValid() || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff
                    || (camera.cullingMask&(1<<r.gameObject.layer))==0) continue;
                Require(!r.GetComponentInParent<SortingGroup>(),"live SortingGroup unsupported");
                var materials=r.sharedMaterials; Require(materials.Length>0,"missing material slot");
                for(int slot=0;slot<materials.Length;slot++) {
                    var m=materials[slot]; Require(m && m.renderQueue>2500,"opaque/missing material in transparent census renderer="+r.GetInstanceID()+" slot="+slot);
                    int index; bool owned=candidates.TryGetValue(r.GetInstanceID(),out index);
                    Require(!owned || materials.Length==1,"owned material slots changed");
                    rows.Add(new Row {R=r,M=m,Item=owned?index:-1,Key=MatShare.SortKey(r,m,camera,slot)});
                }
            }
            rows.Sort((a,b)=>a.Key.CompareTo(b.Key));
            Require(rows.Count(r=>r.Item>=0)==data.Items.Length,"owned census changed");
            for(int i=1;i<rows.Count;i++) if(rows[i-1].Key.SameMaterialTie(rows[i].Key))
                Require(rows[i-1].Item<0 && rows[i].Item<0,"ambiguous owned material tie material="+rows[i].Key.Material+" renderers="+rows[i-1].R.GetInstanceID()+","+rows[i].R.GetInstanceID());
            var originalItems=data.Items;
            data.Items=rows.Where(r=>r.Item>=0).Select(r=>originalItems[r.Item]).ToArray();
            var map=new Dictionary<int,int>(); for(int i=0;i<data.Items.Length;i++) map.Add(data.Items[i].Id,i);
            foreach(var r in rows) if(r.Item>=0) r.Item=map[originalItems[r.Item].Id];
            bundle=AssetBundle.LoadFromFile(Path.GetFullPath(bundlePath)); Require(bundle,"bundle load failed");
            var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute"); Require(shader,"compute asset missing");
            gpu=new GpuDecoGpu(shader,data); var evaluated=gpu.Dispatch(data.BakeTime,true);
            double maximum=0,sum=0,colorMax=0,colorSum=0; int over=0,colorOver=0;
            for(int i=0;i<data.Items.Length;i++) for(int k=0;k<4;k++) {
                var d=data.Items[i]; var v=evaluated.vertices[i*4+k];
                var expected=new Vector3(d.World[k*3],d.World[k*3+1],d.World[k*3+2]);
                double error=Vector3.Distance(v.position,expected)*data.PixelsPerWorld;
                Require(DecoBakeCore.Finite(error),"nonfinite GPU vertex"); maximum=Math.Max(maximum,error); sum+=error; if(error>.01) over++;
                for(int c=0;c<4;c++) {error=Math.Abs((double)v.color[c]-d.Color[c]); Require(DecoBakeCore.Finite(error),"nonfinite GPU color");
                    colorMax=Math.Max(colorMax,error); colorSum+=error; if(error>1.0/255) colorOver++;}
            }
            for(int ch=0;ch<16;ch++) {
                double max=0,total=0; for(int i=0;i<data.Items.Length;i++){double error=Math.Abs((double)evaluated.values[i*16+ch]-data.Items[i].Actual[ch]);max=Math.Max(max,error);total+=error;}
                Log("G0 t="+song.ToString("R",CultureInfo.InvariantCulture)+" slot="+ch+" max="+max.ToString("G12",CultureInfo.InvariantCulture)+" mean="+(total/data.Items.Length).ToString("G12",CultureInfo.InvariantCulture)+" source=paused_gpu_snapshot");
            }
            Log("G0_snapshot vertices="+(data.Items.Length*4)+" max_px="+maximum.ToString("G12",CultureInfo.InvariantCulture)
                +" mean_px="+(sum/(data.Items.Length*4)).ToString("G12",CultureInfo.InvariantCulture)+" over_tol="+over
                +" rgba_max="+colorMax.ToString("G12",CultureInfo.InvariantCulture)+" rgba_mean="+(colorSum/(data.Items.Length*16)).ToString("G12",CultureInfo.InvariantCulture)+" rgba_over_tol="+colorOver);
            g0=over==0 && colorOver==0;
            Require(g0 || GpuDecoCoverage.Auxiliary(data.Awake),"G0_FAIL before renderer ownership");
            commands=new CommandBuffer {name="GpuDeco.G1 copy runs"}; int copy=shader.FindKernel("CopyRun");
            commands.SetComputeBufferParam(shader,copy,"_CopySource",gpu.VertexBuffer);
            int runCount=0,submeshCount=0;
            for(int a=0;a<rows.Count;) {
                if(rows[a].Item<0){a++;continue;}
                int b=a+1; while(b<rows.Count && rows[b].Item>=0 && rows[a].Key.SameBucket(rows[b].Key)
                    && rows[a].R.gameObject.layer==rows[b].R.gameObject.layer) b++;
                BuildRun(data,rows,a,b,shader,copy,evaluated.vertices[rows[a].Item*4].position.z); runCount++; submeshCount+=b-a; a=b;
            }
            Graphics.ExecuteCommandBuffer(commands);
            // Read back the actual run buffers; no CPU vertex upload can hide a bad copy.
            int first=0; for(int n=0;n<buffers.Count;n++) {
                var actual=new Vertex[meshes[n].vertexCount]; buffers[n].GetData(actual);
                for(int i=0;i<actual.Length;i++) Require(actual[i].position.Equals(evaluated.vertices[first+i].position)
                    && actual[i].color.Equals(evaluated.vertices[first+i].color) && actual[i].uv.Equals(evaluated.vertices[first+i].uv),"GPU run copy mismatch");
                first+=actual.Length;
            }
            foreach(var r in rows) if(r.Item>=0) originals.Add((SpriteRenderer)r.R);
            foreach(var r in originals) r.forceRenderingOff=true;
            foreach(var r in renderers) r.enabled=true;
            Log("on=True scope=paused_G1_only owned="+originals.Count+" awake="+data.Awake+" runs="+runCount+" submeshes="+submeshCount
                +" dispatches="+(runCount+1)+" prepare_ms="+timer.Elapsed.TotalMilliseconds.ToString("F6",CultureInfo.InvariantCulture)
                +" prepare_scope=paused_diagnostic steady_cost=UNMEASURED G0="+(g0?"PASS":"FAIL_AUXILIARY"));
        }
        void BuildRun(GpuDecoData data,List<Row> rows,int a,int b,ComputeShader shader,int copy,float z)
        {
            var first=rows[a]; int count=b-a;
            var mesh=new Mesh {name="GpuDeco.G1 run",indexFormat=IndexFormat.UInt32,vertexBufferTarget=GraphicsBuffer.Target.Raw}; meshes.Add(mesh);
            mesh.SetVertexBufferParams(count*4,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
            mesh.SetVertexBufferData(new Vertex[count*4],0,0,count*4); mesh.subMeshCount=count;
            for(int n=0;n<count;n++) mesh.SetIndices(data.Items[rows[a+n].Item].Triangles.Select(x=>x+n*4).ToArray(),MeshTopology.Triangles,n,false);
            mesh.bounds=new Bounds(new Vector3(0,0,z),new Vector3(2000000,2000000,0));
            mesh.UploadMeshData(true); var buffer=mesh.GetVertexBuffer(0); buffers.Add(buffer);
            var go=new GameObject("GpuDeco.G1 run") {hideFlags=HideFlags.HideAndDontSave,layer=first.R.gameObject.layer}; go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.enabled=false;
            renderers.Add(renderer);
            renderer.sharedMaterials=Enumerable.Range(a,count).Select(n=>rows[n].M).ToArray();
            renderer.sortingLayerID=first.R.sortingLayerID; renderer.sortingOrder=first.R.sortingOrder;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            for(int n=0;n<count;n++) {
                var props=new MaterialPropertyBlock(); var sprite=(SpriteRenderer)rows[a+n].R;
                props.SetTexture("_MainTex",sprite.sprite.texture); props.SetColor("_RendererColor",Color.white); props.SetVector("_Flip",Vector4.one);
                renderer.SetPropertyBlock(props,n);
            }
            commands.SetComputeIntParam(shader,"_CopyFirstVertex",first.Item*4); commands.SetComputeIntParam(shader,"_CopyVertexCount",count*4);
            commands.SetComputeBufferParam(shader,copy,"_Vertices",buffer); commands.DispatchCompute(shader,copy,(count*4+63)/64,1,1);
        }
        void Update() {if(!FrameCompare.Paused || !ADOBase.controller || !ADOBase.controller.paused || Time.timeScale!=0
            || generation!=DecoBake.Generation || !scrConductor.instance || scrConductor.instance.songposition_minusi!=song) Stop();}
        internal static void Stop()
        {
            var p=instance; instance=null; if(ReferenceEquals(p,null))return;
            foreach(var r in p.renderers) if(r) r.enabled=false; p.renderers.Clear();
            bool restored=true;
            foreach(var r in p.originals) try {if(r)r.forceRenderingOff=false;} catch(Exception e){restored=false;Log("restore_error="+e.Message);}
            p.originals.Clear(); Release(()=>p.commands?.Dispose()); p.commands=null;
            foreach(var b in p.buffers)Release(b.Dispose); p.buffers.Clear(); Release(()=>p.gpu?.Dispose()); p.gpu=null;
            foreach(var m in p.meshes)if(m)Destroy(m); p.meshes.Clear(); if(p.bundle)Release(()=>p.bundle.Unload(true)); p.bundle=null;
            if(p)Destroy(p.gameObject); Log("draw_off=True originals_restored="+restored);
        }
        static void Release(Action action) {try{action();}catch(Exception e){Log("release_error="+e.Message);}}
        void OnDestroy() {if(instance==this)Stop();}
    }
}
#endif
