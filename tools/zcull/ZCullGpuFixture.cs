using System;
using UnityEngine;
using UnityEngine.Rendering;

// Standalone Editor synthetic raster/depth/readback validation; never starts the game.
public static class ZCullGpuFixture
{
    static void Require(bool condition,string message) {if(!condition)throw new Exception("ZCULL_GPU_FIXTURE: "+message);}
    public static void Run(Shader shader,ComputeShader reduce)
    {
        var go=new GameObject("ZCullFixtureCamera");var camera=go.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=1;camera.aspect=1;go.transform.position=new Vector3(0,0,-10);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var texels=new[]{new Color32(255,255,255,255),new Color32(255,255,255,254),new Color32(255,255,255,255),new Color32(255,255,255,254)};
        texture.SetPixels32(texels);texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),1,0,SpriteMeshType.FullRect);
        var a=new GameObject("back");var back=a.AddComponent<SpriteRenderer>();back.sprite=sprite;
        var b=new GameObject("front");var front=b.AddComponent<SpriteRenderer>();front.sprite=sprite;
        var material=new Material(shader);material.SetTexture("_MainTex",texture);
        var color=new RenderTexture(16,16,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear);
        var depth=new RenderTexture(16,16,24,RenderTextureFormat.Depth);
        var buffer=new ComputeBuffer(4,sizeof(uint));color.Create();depth.Create();
        int kernel=reduce.FindKernel("Reduce");
        var previous=RenderTexture.active;
        try {
            // Validate point and bilinear sampling, both UV variants, and alpha-zero counting.
            foreach(var filter in new[]{FilterMode.Point,FilterMode.Bilinear})
            foreach(int tile in new[]{0,1})
            foreach(byte alpha in new byte[]{254,0,255}) {
                texture.filterMode=filter;
                texels[1].a=texels[3].a=alpha;texture.SetPixels32(texels);texture.Apply();
                material.SetFloat("_SfZTile",tile);
                material.SetFloat("RepeatX",tile==1?2:1);material.SetFloat("RepeatY",1);
                buffer.SetData(new uint[4]);
                using(var cmd=new CommandBuffer()) {
                    cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,true));
                    cmd.SetRenderTarget(color.colorBuffer,depth.depthBuffer);cmd.SetViewport(new Rect(0,0,16,16));
                    cmd.ClearRenderTarget(true,true,Color.clear,1);
                    cmd.SetGlobalFloat("_SfZDepth",1f/3);cmd.DrawRenderer(front,material,0,0);
                    cmd.SetComputeIntParam(reduce,"Width",16);cmd.SetComputeIntParam(reduce,"Height",16);
                    cmd.SetComputeTextureParam(reduce,kernel,"Coverage",color);cmd.SetComputeBufferParam(reduce,kernel,"Counts",buffer);
                    // Back alive, front alive, and front opaque coverage, reduced separately.
                    for(int slot=0;slot<4;slot++) {
                        cmd.ClearRenderTarget(false,true,Color.clear);
                        cmd.SetGlobalFloat("_SfZDepth",slot==0?1.75f/3:.75f/3);
                        cmd.DrawRenderer(slot==0?back:front,material,0,slot==2?3:slot==3?2:1);
                        cmd.SetComputeIntParam(reduce,"Slot",slot);cmd.DispatchCompute(reduce,kernel,2,2,1);
                    }
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                var request=AsyncGPUReadback.Request(buffer);request.WaitForCompletion();
                Require(!request.hasError,"readback failed");var data=request.GetData<uint>();
                // Clamp sampler: 8 opaque columns with point, 4 with bilinear.
                // Fragment-stage 2x UV repeat halves those counts (outside UV clamps to alpha<1).
                uint opaque=alpha==255?256u:filter==FilterMode.Point?128u:64u;
                if(alpha!=255 && tile==1)opaque/=2;
                Require(data[0]==256-opaque && data[1]==256 && data[2]==opaque && data[3]==256,
                    "filter="+filter+" tile="+tile+" alpha="+alpha+" got="+data[0]+","+data[1]+","+data[2]+" expected="+(256-opaque)+",256,"+opaque);
            }
            Debug.Log("ZCULL_GPU_FIXTURE=PASS cases=12 self-retained=True alpha254-rejected=True alpha0-counted=True tile-repeat=True reversedZ="+SystemInfo.usesReversedZBuffer);
        } finally {
            RenderTexture.active=previous;buffer.Release();color.Release();depth.Release();
            foreach(var obj in new UnityEngine.Object[]{color,depth,material,a,b,sprite,texture,go})UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
