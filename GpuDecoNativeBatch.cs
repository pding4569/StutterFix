#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Vertex=StutterFix.GpuDecoGpu.Vertex;

namespace StutterFix
{
    // Research only, default off. Unity keeps the independent renderer sort
    // keys; a GPU index map targets its combined mesh vertex buffers.
    internal sealed class GpuDecoNativeBatch : IDisposable
    {
        internal static bool Enabled;
        internal static bool StaticCombine;
        internal static bool RestoreSortBounds;
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<GameObject> objects=new List<GameObject>();
        readonly List<MeshRenderer> renderers=new List<MeshRenderer>();
        readonly List<GraphicsBuffer> buffers=new List<GraphicsBuffer>();
        CommandBuffer commands;
        internal int BufferCount {get;private set;}
        internal int RendererCount=>renderers.Count;
        internal IReadOnlyList<MeshRenderer> LiveRenderers=>renderers;
        internal IReadOnlyList<Mesh> LiveMeshes=>meshes;
        static void Require(bool value,string reason) {if(!value)throw new InvalidOperationException("gpudeco native batch: "+reason);}

        internal static GpuDecoNativeBatch Build(Transform parent,GpuDecoData data,Vertex[] initial,SpriteRenderer[] originals,ComputeShader shader,GraphicsBuffer source,MeshRenderer[] exactState=null,bool rawColor=false,bool splitSortBuckets=false)
        {
            var batch=new GpuDecoNativeBatch();
            try {batch.Prepare(parent,data,initial,originals,shader,source,exactState,rawColor,splitSortBuckets);return batch;}
            catch {batch.Dispose();throw;}
        }

        void Prepare(Transform parent,GpuDecoData data,Vertex[] initial,SpriteRenderer[] originals,ComputeShader shader,GraphicsBuffer source,MeshRenderer[] exactState,bool rawColor,bool splitSortBuckets)
        {
            Require(parent.localToWorldMatrix==Matrix4x4.identity,"identity batch root required");
            Require(originals.Length==data.Items.Length && initial.Length==originals.Length*4,"input count changed");
            Require(exactState==null||exactState.Length==originals.Length,"state count changed");
            for(int i=0;i<originals.Length;i++)
            {
                var original=originals[i];var mesh=new Mesh {name="GpuDeco native source",indexFormat=IndexFormat.UInt32};meshes.Add(mesh);
                mesh.SetVertexBufferParams(4,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                    new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
                var points=new Vertex[4];
                for(int k=0;k<4;k++) {points[k]=initial[i*4+k];if(!rawColor)points[k].color=GpuDecoGpu.DrawColor(points[k].color);}
                mesh.SetVertexBufferData(points,0,0,4);mesh.SetIndices(data.Items[i].Triangles,MeshTopology.Triangles,0,false);
                Renderer state=exactState==null?(Renderer)original:exactState[i];Require(state,"missing renderer state");
                var center=exactState!=null?state.bounds.center:original.spriteSortPoint==SpriteSortPoint.Pivot?original.transform.position:original.bounds.center;
                mesh.bounds=new Bounds(center,new Vector3(2000000,2000000,0));mesh.UploadMeshData(false);
                var go=new GameObject("GpuDeco native sorted") {hideFlags=HideFlags.HideAndDontSave,layer=state.gameObject.layer};objects.Add(go);
                go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderers.Add(renderer);
                renderer.sharedMaterial=state.sharedMaterial;renderer.sortingLayerID=state.sortingLayerID;renderer.sortingOrder=state.sortingOrder;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                var props=new MaterialPropertyBlock();props.SetTexture("_MainTex",original.sprite.texture);props.SetColor("_RendererColor",Color.white);props.SetVector("_Flip",Vector4.one);
                if(exactState!=null)
                {
                    Require(state.sharedMaterials.Length==1,"multi-material renderer state unsupported");
                    state.GetPropertyBlock(props,0);
                    if(props.isEmpty)state.GetPropertyBlock(props);
                    renderer.SetPropertyBlock(props,0);
                }
                else renderer.SetPropertyBlock(props);
            }
            var camera=GpuSplit.FindMain();Require(camera,"missing sort camera");int bucketCount=1;
            if(splitSortBuckets)
            {
                // Do not let Unity create one static draw range across explicit
                // layer/order/queue/distance boundaries. Same-bucket tie order
                // still requires the pixel gate; this is not a proof of order.
                var buckets=new Dictionary<(int,int,int,int),List<GameObject>>();
                for(int i=0;i<objects.Count;i++)
                {
                    Renderer state=exactState==null?(Renderer)originals[i]:exactState[i];
                    var k=MatShare.SortKey(state,state.sharedMaterial,camera);
                    var id=(k.Layer,k.Order,k.Queue,BitConverter.SingleToInt32Bits(k.Distance));
                    if(!buckets.TryGetValue(id,out var list))buckets.Add(id,list=new List<GameObject>());
                    list.Add(objects[i]);
                }
                bucketCount=buckets.Count;
                foreach(var list in buckets.Values)if(list.Count>1)StaticBatchingUtility.Combine(list.ToArray(),parent.gameObject);
            }
            else StaticBatchingUtility.Combine(objects.ToArray(),parent.gameObject);
            int mismatches=0;
            for(int i=0;i<renderers.Count;i++)
            {
                var original=originals[i];var renderer=renderers[i];Renderer state=exactState==null?(Renderer)original:exactState[i];
                if(!MatShare.SortKey(state,state.sharedMaterial,camera).SameBucket(MatShare.SortKey(renderer,renderer.sharedMaterial,camera)))mismatches++;
                if(RestoreSortBounds||exactState!=null)
                {
                    var center=exactState!=null?state.bounds.center:original.spriteSortPoint==SpriteSortPoint.Pivot?original.transform.position:original.bounds.center;
                    // Static batching creates a shared mesh. Override each
                    // renderer's world bounds, rather than that shared mesh.
                    renderer.bounds=new Bounds(center,new Vector3(2000000,2000000,0));
                    Require(MatShare.SortKey(state,state.sharedMaterial,camera).SameBucket(MatShare.SortKey(renderer,renderer.sharedMaterial,camera)),"restored static sort bucket changed");
                }
            }
            Main.Entry.Logger.Log("[gpudeco] static_sort_audit owned="+renderers.Count+" bucket_mismatch_before="+mismatches+" restored_world_bounds="+RestoreSortBounds+" split_sort_buckets="+splitSortBuckets+" combine_groups="+bucketCount);
            var indicesByMesh=new Dictionary<Mesh,List<int>>();
            for(int i=0;i<renderers.Count;i++)
            {
                var mesh=renderers[i].GetComponent<MeshFilter>().sharedMesh;
                if(!indicesByMesh.TryGetValue(mesh,out var list))indicesByMesh.Add(mesh,list=new List<int>());
                list.Add(i);
            }
            var combined=indicesByMesh.Keys.ToArray();var knownMeshes=new HashSet<Mesh>(meshes);
            foreach(var mesh in combined)if(knownMeshes.Add(mesh))meshes.Add(mesh);
            foreach(var renderer in renderers) {renderer.enabled=false;Require(splitSortBuckets||renderer.isPartOfStaticBatch,"native batching unavailable");}
            commands=new CommandBuffer {name="GpuDeco native mapped copy"};int kernel=shader.FindKernel("CopyMappedRun");
            commands.SetComputeBufferParam(shader,kernel,"_CopySource",source);commands.SetComputeIntParam(shader,"_CopyQuantizeColor",rawColor?0:1);
            foreach(var mesh in combined)
            {
                Require(mesh.GetVertexBufferStride(0)==36 && mesh.GetVertexAttributeOffset(VertexAttribute.Color)==12
                    && mesh.GetVertexAttributeFormat(VertexAttribute.Color)==VertexAttributeFormat.Float32
                    && mesh.GetVertexAttributeOffset(VertexAttribute.TexCoord0)==28,"combined vertex layout unsupported");
                var map=Enumerable.Repeat(uint.MaxValue,mesh.vertexCount).ToArray();
                uint[] indices;
                using(var indexBuffer=mesh.GetIndexBuffer())
                {
                    if(mesh.indexFormat==IndexFormat.UInt16) {var values=new ushort[indexBuffer.count];indexBuffer.GetData(values);indices=values.Select(x=>(uint)x).ToArray();}
                    else {indices=new uint[indexBuffer.count];indexBuffer.GetData(indices);}
                }
                foreach(int i in indicesByMesh[mesh])
                {
                    var renderer=renderers[i];
                    var submesh=mesh.GetSubMesh(renderer.subMeshStartIndex);
                    Require(submesh.indexCount==6,"combined draw range unsupported");
                    for(int t=0;t<6;t++)
                    {
                        int destination=checked((int)indices[submesh.indexStart+t]+submesh.baseVertex);
                        uint from=(uint)(i*4+data.Items[i].Triangles[t]);
                        Require(destination>=0 && destination<map.Length,"combined index outside vertex buffer");
                        Require(map[destination]==uint.MaxValue || map[destination]==from,"combined vertex alias changes animation ownership");
                        map[destination]=from;
                    }
                }
                Require(map.All(x=>x!=uint.MaxValue),"unmapped combined vertices");
                Vertex[] seed;
                using(var old=mesh.GetVertexBuffer(0)) {seed=new Vertex[mesh.vertexCount];old.GetData(seed);}
                for(int i=0;i<seed.Length;i++)
                {
                    var expected=initial[map[i]];
                    Require(seed[i].position.Equals(expected.position) && seed[i].uv.Equals(expected.uv)
                        && seed[i].color.Equals(rawColor?expected.color:GpuDecoGpu.DrawColor(expected.color)),"combined vertex map does not match evaluated source");
                }
                // Recreating a non-readable static mesh buffer loses its data.
                // The immediately executed mapped GPU copy initializes the new
                // buffer before any camera can draw these renderers.
                mesh.vertexBufferTarget|=GraphicsBuffer.Target.Raw;
                var destinationBuffer=mesh.GetVertexBuffer(0);buffers.Add(destinationBuffer);
                var mapBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,map.Length,4);buffers.Add(mapBuffer);mapBuffer.SetData(map);
                commands.SetComputeBufferParam(shader,kernel,"_CopyMap",mapBuffer);commands.SetComputeBufferParam(shader,kernel,"_Vertices",destinationBuffer);
                commands.SetComputeIntParam(shader,"_CopyVertexCount",map.Length);commands.DispatchCompute(shader,kernel,(map.Length+63)/64,1,1);
            }
            BufferCount=combined.Length;Graphics.ExecuteCommandBuffer(commands);
            // Verify the exact map and quantized copy, not a CPU truth upload.
            for(int b=0;b<combined.Length;b++)
            {
                var actual=new Vertex[combined[b].vertexCount];buffers[b*2].GetData(actual);
                var map=new uint[actual.Length];buffers[b*2+1].GetData(map);
                for(int i=0;i<actual.Length;i++)
                {
                    var expected=initial[map[i]];
                    Require(actual[i].position.Equals(expected.position) && actual[i].uv.Equals(expected.uv)
                        && (rawColor?actual[i].color.Equals(expected.color):GpuDecoGpu.EquivalentDrawColor(actual[i].color,expected.color)),"mapped GPU copy mismatch");
                }
            }
        }

        internal void Dispatch() {Graphics.ExecuteCommandBuffer(commands);}
        internal void SetEnabled(bool value) {foreach(var renderer in renderers)if(renderer)renderer.enabled=value;}
        public void Dispose()
        {
            int errors=0;
            try {SetEnabled(false);}catch {errors++;}
            try {commands?.Dispose();}catch {errors++;}finally {commands=null;}
            foreach(var buffer in buffers)try {buffer.Dispose();}catch {errors++;} buffers.Clear();
            foreach(var go in objects)try {if(go)UnityEngine.Object.Destroy(go);}catch {errors++;} objects.Clear();renderers.Clear();
            foreach(var mesh in meshes)try {if(mesh)UnityEngine.Object.Destroy(mesh);}catch {errors++;} meshes.Clear();
            BufferCount=0;
            if(errors!=0)Main.Entry.Logger.Log("[gpudeco] shared_static_dispose_errors="+errors);
        }
    }
}
#endif
