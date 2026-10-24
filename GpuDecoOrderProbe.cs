#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Vertex=StutterFix.GpuDecoGpu.Vertex;

namespace StutterFix
{
    // Explicit synthetic diagnostic with the loaded game shader. No game
    // material is modified and no renderer hook or persistent callback exists.
    internal static class GpuDecoOrderProbe
    {
        static void Require(bool condition,string reason) {if(!condition)throw new InvalidOperationException("gpudeco orderprobe: "+reason);}
        internal static void Run(bool scene=false)
        {
            var owned=new List<UnityEngine.Object>();var before=RenderTexture.active;
            try
            {
                var sourceGroups=scene?Resources.FindObjectsOfTypeAll<SpriteRenderer>()
                    .Where(r=>r && r.gameObject.scene.IsValid() && r.sharedMaterial && r.sharedMaterial.shader.name=="Sprites/Tile")
                    .Select(r=>r.sharedMaterial).GroupBy(m=>m.GetInstanceID()).Select(g=>g.First())
                    .GroupBy(GpuDecoSnapshot.MaterialSignature).OrderByDescending(g=>g.Count()).Select(g=>g.OrderBy(m=>m.GetInstanceID()).ToArray()).ToArray():null;
                Require(!scene || (sourceGroups.Length>0 && sourceGroups.Sum(g=>g.Length)>=3),"scene materials unavailable");
                var used=new HashSet<int>(Resources.FindObjectsOfTypeAll<Renderer>().Where(r=>r && r.gameObject.scene.IsValid()).Select(r=>r.gameObject.layer));
                int layer=Enumerable.Range(8,24).Reverse().FirstOrDefault(i=>!used.Contains(i));
                Require(layer>=8,"no unused diagnostic layer");
                var shader=Shader.Find("Sprites/Tile");Require(shader,"Sprites/Tile not loaded");
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);owned.Add(texture);
                texture.SetPixels(Enumerable.Repeat(Color.white,4).ToArray());texture.Apply();
                var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),2,0,SpriteMeshType.FullRect);owned.Add(sprite);
                var patterns=new Sprite[3];
                for(int n=0;n<3;n++) {
                    int side=16<<n;var tex=new Texture2D(side,side,TextureFormat.RGBA32,false) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(tex);
                    var values=new Color32[side*side];for(int p=0;p<values.Length;p++)values[p]=new Color32((byte)(p*17+n*53),(byte)(p*37+n*19),(byte)(p*11+n*97),255);
                    tex.SetPixels32(values);tex.Apply();patterns[n]=Sprite.Create(tex,new Rect(0,0,side,side),new Vector2(.5f,.5f),side,0,SpriteMeshType.FullRect);owned.Add(patterns[n]);
                }
                var go=new GameObject("GpuDeco order camera") {hideFlags=HideFlags.HideAndDontSave};owned.Add(go);
                var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;
                camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.black;camera.cullingMask=1<<layer;camera.allowHDR=false;camera.allowMSAA=false;
                var rt=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32);owned.Add(rt);camera.targetTexture=rt;
                var image=new Texture2D(64,64,TextureFormat.RGBA32,false);owned.Add(image);
                Func<Color32[]> capture=()=>{camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();return image.GetPixels32();};
                int forwardMatches=0,reverseMatches=0,texelMatches=0,crcMatches=0,crcReverseMatches=0,cases=0,negative=0;
                int permutationMatches=0,individualMatches=0,isolatedMatches=0,isolatedTotal=0;
                int staticMatches=0,staticBatchedCases=0;
                var loggedMaterials=new HashSet<int>();
                int[][] orders={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
                foreach(bool patterned in scene?new[]{false}:new[]{false,true})foreach(float alpha in new[]{.5f,.007863485f})foreach(var order in orders)
                {
                    var local=new List<UnityEngine.Object>();
                    try
                    {
                        var mats=new Material[3];var sprites=new SpriteRenderer[3];
                        var colors=new[]{new Color(.9f,.1f,.2f,alpha),new Color(.2f,.8f,.1f,alpha),new Color(.1f,.2f,.9f,alpha)};
                        foreach(int i in order)
                        {
                            if(scene) {
                                var group=sourceGroups[order[i]%sourceGroups.Length];
                                mats[i]=group[(cases+i*group.Length/3)%group.Length];
                            } else {
                                mats[i]=new Material(shader);local.Add(mats[i]);mats[i].mainTexture=texture;
                                if(mats[i].HasProperty("_Color"))mats[i].SetColor("_Color",Color.white);
                            }
                        }
                        if(scene && mats.Select(m=>m.GetInstanceID()).Distinct().Count()!=3)continue;
                        if(scene)foreach(var mat in mats)if(loggedMaterials.Add(mat.GetInstanceID()))
                            Main.Entry.Logger.Log("[gpudeco-order] material="+mat.GetInstanceID()+" signature="+GpuDecoSnapshot.MaterialSignature(mat));
                        for(int i=0;i<3;i++)
                        {
                            var item=new GameObject("GpuDeco native order") {hideFlags=HideFlags.HideAndDontSave,layer=layer};local.Add(item);
                            sprites[i]=item.AddComponent<SpriteRenderer>();sprites[i].sprite=patterned?patterns[i]:sprite;sprites[i].sharedMaterial=mats[i];sprites[i].color=colors[i];
                        }
                        var expected=capture();Require(expected.Any(c=>c.r!=0 || c.g!=0 || c.b!=0),"blank positive control");
                        foreach(var s in sprites)s.enabled=false;
                        var sorted=Enumerable.Range(0,3).OrderBy(i=>mats[i].renderQueue).ThenBy(i=>mats[i].GetInstanceID()).ToArray();
                        int[] changed=new int[11],maxima=new int[11];Color32[] forward=null;
                        string matchingPermutations="";
                        for(int mode=0;mode<11;mode++)
                        {
                            var sequence=mode==1?sorted.Reverse().ToArray():sorted;
                            if(mode==3)sequence=Enumerable.Range(0,3).OrderBy(i=>mats[i].renderQueue).ThenBy(i=>mats[i].ComputeCRC()).ThenBy(i=>mats[i].GetInstanceID()).ToArray();
                            if(mode==4)sequence=Enumerable.Range(0,3).OrderBy(i=>mats[i].renderQueue).ThenByDescending(i=>mats[i].ComputeCRC()).ThenBy(i=>mats[i].GetInstanceID()).ToArray();
                            if(mode>=5)sequence=orders[mode-5];
                            var mr=CreateMesh(local,layer,sequence,sprites,mats,mode==2);
                            var actual=capture();
                            if(mode==0)forward=actual;else if(mode==1 && !forward.SequenceEqual(actual))negative++;
                            for(int p=0;p<actual.Length;p++) {int delta=0;for(int c=0;c<4;c++)delta=Math.Max(delta,Math.Abs(actual[p][c]-expected[p][c]));if(delta>0)changed[mode]++;maxima[mode]=Math.Max(maxima[mode],delta);}
                            if(mode>=5 && changed[mode]==0)matchingPermutations+=(matchingPermutations.Length==0?"":"|")+string.Join(",",sequence);
                            mr.enabled=false;
                        }
                        var individuals=new MeshRenderer[3];
                        for(int i=0;i<3;i++)individuals[i]=CreateMesh(local,layer,new[]{i},sprites,mats,false);
                        var individual=capture();
                        int individualChanged=0,individualMax=0,isolatedChanged=0,isolatedMax=0;
                        Compare(expected,individual,out individualChanged,out individualMax);
                        foreach(var mr in individuals)mr.enabled=false;
                        for(int i=0;i<3;i++)
                        {
                            sprites[i].enabled=true;var oneNative=capture();sprites[i].enabled=false;
                            individuals[i].enabled=true;var oneMesh=capture();individuals[i].enabled=false;
                            int oneChanged,oneMax;Compare(oneNative,oneMesh,out oneChanged,out oneMax);
                            if(oneChanged==0)isolatedMatches++;isolatedTotal++;
                            isolatedChanged+=oneChanged;isolatedMax=Math.Max(isolatedMax,oneMax);
                        }
                        // A separate diagnostic keeps transparent ordering in
                        // Unity's native renderer sorter instead of guessing its key.
                        // No game objects/materials enter this temporary batch.
                        foreach(var mr in individuals)mr.enabled=true;
                        StaticBatchingUtility.Combine(individuals.Select(mr=>mr.gameObject).ToArray(),null);
                        var staticImage=capture();int staticChanged,staticMax;
                        Compare(expected,staticImage,out staticChanged,out staticMax);
                        bool staticallyBatched=individuals.All(mr=>mr.isPartOfStaticBatch);
                        if(staticChanged==0)staticMatches++;if(staticallyBatched)staticBatchedCases++;
                        var combined=individuals.Select(mr=>mr.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
                        foreach(var shared in combined)if(shared && !local.Contains(shared))local.Add(shared);
                        int staticGpuShifted=-1,staticGpuRestored=-1;
                        if(staticallyBatched && combined.Length==1)
                            CheckStaticGpuBuffer(combined[0],capture,expected,out staticGpuShifted,out staticGpuRestored);
                        foreach(var mr in individuals)mr.enabled=false;
                        if(individualChanged==0)individualMatches++;
                        if(matchingPermutations.Length>0)permutationMatches++;
                        foreach(var s in sprites)s.enabled=true;
                        var restored=capture();Require(expected.SequenceEqual(restored),"native A/A2 unstable");
                        if(changed[0]==0)forwardMatches++;if(changed[1]==0)reverseMatches++;if(changed[2]==0)texelMatches++;if(changed[3]==0)crcMatches++;if(changed[4]==0)crcReverseMatches++;
                        cases++;
                        Main.Entry.Logger.Log("[gpudeco-order] scene="+scene+" patterned="+patterned+" alpha="+alpha.ToString("R",System.Globalization.CultureInfo.InvariantCulture)
                            +" ids="+string.Join(",",sorted.Select(i=>mats[i].GetInstanceID()))+" forward_changed="+changed[0]+" forward_max="+maxima[0]
                            +" reverse_changed="+changed[1]+" reverse_max="+maxima[1]+" texel_changed="+changed[2]+" texel_max="+maxima[2]+" aa2=0 shader=Sprites/Tile"
                            +" queues="+string.Join(",",sorted.Select(i=>mats[i].renderQueue))+" shaders="+string.Join(",",sorted.Select(i=>mats[i].shader.GetInstanceID()))
                            +" crc="+string.Join(",",sorted.Select(i=>mats[i].ComputeCRC()))+" crc_changed="+changed[3]+" crc_max="+maxima[3]+" crc_reverse_changed="+changed[4]+" crc_reverse_max="+maxima[4]
                            +" permutations_changed="+string.Join(",",changed.Skip(5))+" permutations_max="+string.Join(",",maxima.Skip(5))
                            +" matching_permutations="+(matchingPermutations.Length==0?"none":matchingPermutations)
                            +" individual_changed="+individualChanged+" individual_max="+individualMax+" isolated_changed="+isolatedChanged+" isolated_max="+isolatedMax
                            +" static_changed="+staticChanged+" static_max="+staticMax+" statically_batched="+staticallyBatched+" combined_meshes="+combined.Length
                            +" static_gpu_shifted="+staticGpuShifted+" static_gpu_restored="+staticGpuRestored
                            +" keywords="+string.Join("|",sorted.Select(i=>string.Join(",",mats[i].shaderKeywords.OrderBy(x=>x)))));
                    }
                    finally {for(int i=local.Count-1;i>=0;i--)if(local[i])UnityEngine.Object.DestroyImmediate(local[i]);}
                }
                Main.Entry.Logger.Log("[gpudeco-order] result=MEASURED scene="+scene+" signatures="+(scene?sourceGroups.Length:0)+" cases="+cases+" forward_matches="+forwardMatches+" reverse_matches="+reverseMatches+" texel_matches="+texelMatches+" crc_matches="+crcMatches+" crc_reverse_matches="+crcReverseMatches+" permutation_cases_matched="+permutationMatches+" individual_cases_matched="+individualMatches+" isolated_matches="+isolatedMatches+" isolated_total="+isolatedTotal+" static_matches="+staticMatches+" static_batched_cases="+staticBatchedCases+" order_sensitive_cases="+negative+" G1=NOT_EVALUATED");
            }
            finally {RenderTexture.active=before;for(int i=owned.Count-1;i>=0;i--)if(owned[i])UnityEngine.Object.DestroyImmediate(owned[i]);}
        }

        static void Compare(Color32[] expected,Color32[] actual,out int changed,out int maximum)
        {
            Require(expected.Length==actual.Length,"capture length changed");changed=0;maximum=0;
            for(int p=0;p<actual.Length;p++)
            {
                int delta=0;for(int c=0;c<4;c++)delta=Math.Max(delta,Math.Abs(actual[p][c]-expected[p][c]));
                if(delta>0)changed++;maximum=Math.Max(maximum,delta);
            }
        }

        static void CheckStaticGpuBuffer(Mesh mesh,Func<Color32[]> capture,Color32[] expected,out int shifted,out int restored)
        {
            Require(mesh.GetVertexAttributeStream(VertexAttribute.Position)==0 && mesh.GetVertexAttributeFormat(VertexAttribute.Position)==VertexAttributeFormat.Float32,"static position layout unsupported");
            uint[] original;
            int oldStride,oldCount;
            using(var oldBuffer=mesh.GetVertexBuffer(0))
            {
                oldStride=oldBuffer.stride;oldCount=oldBuffer.count;
                original=new uint[oldCount*oldStride/4];oldBuffer.GetData(original);
            }
            mesh.vertexBufferTarget|=GraphicsBuffer.Target.Raw|GraphicsBuffer.Target.CopyDestination;
            using(var destination=mesh.GetVertexBuffer(0))
            {
                int words=destination.count*destination.stride/4;
                Require(original.Length==words && oldStride==destination.stride && oldCount==destination.count,"static buffer layout changed");
                var changed=(uint[])original.Clone();
                int stride=destination.stride/4,offset=mesh.GetVertexAttributeOffset(VertexAttribute.Position)/4;
                for(int i=0;i<mesh.vertexCount;i++)
                {
                    int p=i*stride+offset;
                    changed[p]=unchecked((uint)BitConverter.SingleToInt32Bits(BitConverter.Int32BitsToSingle(unchecked((int)changed[p]))+.125f));
                }
                using(var source=new GraphicsBuffer(GraphicsBuffer.Target.Raw|GraphicsBuffer.Target.CopySource,words,4))
                {
                    source.SetData(original);Graphics.CopyBuffer(source,destination);
                    int setupChanged,setupMaximum;
                    Compare(expected,capture(),out setupChanged,out setupMaximum);
                    Main.Entry.Logger.Log("[gpudeco-order] static_buffer vertices="+mesh.vertexCount+" stride="+destination.stride+" count="+destination.count+" readable="+mesh.isReadable
                        +" original_nonzero_words="+original.Count(w=>w!=0)+" setup_changed="+setupChanged+" setup_max="+setupMaximum);
                    try
                    {
                        source.SetData(changed);Graphics.CopyBuffer(source,destination);
                        int maximum;Compare(expected,capture(),out shifted,out maximum);
                    }
                    finally {source.SetData(original);Graphics.CopyBuffer(source,destination);}
                    int restoredMax;Compare(expected,capture(),out restored,out restoredMax);
                }
            }
        }

        static MeshRenderer CreateMesh(List<UnityEngine.Object> owned,int layer,int[] sequence,SpriteRenderer[] sprites,Material[] mats,bool texel)
        {
            int count=sequence.Length;
            var mesh=new Mesh {indexFormat=IndexFormat.UInt32,vertexBufferTarget=GraphicsBuffer.Target.Raw};owned.Add(mesh);
            mesh.SetVertexBufferParams(count*4,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2));
            var points=new Vertex[count*4];
            for(int n=0;n<count;n++)for(int k=0;k<4;k++)
            {
                var sprite=sprites[sequence[n]];
                points[n*4+k]=new Vertex {position=sprite.sprite.vertices[k],uv=sprite.sprite.uv[k],color=GpuDecoGpu.DrawColor(sprite.color)};
            }
            mesh.SetVertexBufferData(points,0,0,points.Length);mesh.subMeshCount=count;
            for(int n=0;n<count;n++)mesh.SetIndices(sprites[sequence[n]].sprite.triangles.Select(i=>(int)i+n*4).ToArray(),MeshTopology.Triangles,n,false);
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(2000000,2000000,0));mesh.UploadMeshData(false);
            var item=new GameObject("GpuDeco submesh order") {hideFlags=HideFlags.HideAndDontSave,layer=layer};owned.Add(item);
            item.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mr=item.AddComponent<MeshRenderer>();mr.sharedMaterials=sequence.Select(i=>mats[i]).ToArray();
            mr.shadowCastingMode=ShadowCastingMode.Off;mr.lightProbeUsage=LightProbeUsage.Off;mr.reflectionProbeUsage=ReflectionProbeUsage.Off;
            for(int n=0;n<count;n++)
            {
                var block=new MaterialPropertyBlock();var texture=sprites[sequence[n]].sprite.texture;
                block.SetTexture("_MainTex",texture);block.SetColor("_RendererColor",Color.white);block.SetVector("_Flip",Vector4.one);
                if(texel)block.SetVector("_MainTex_TexelSize",new Vector4(1f/texture.width,1f/texture.height,texture.width,texture.height));
                mr.SetPropertyBlock(block,n);
            }
            return mr;
        }
    }
}
#endif
