#if AUTOTEST
using System;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static GpuDecoData.Item ImageFixtureItem(scrVisualDecoration d,GpuDecoImageVariant v)=>ImageFixtureItem(d,v,out _);
        static GpuDecoData.Item ImageFixtureItem(scrVisualDecoration d,GpuDecoImageVariant v,out LiveDecoGeometry g)
        {
            var r=d.spriteRenderer;
            g=LiveDecoGeometry.Capture(d.pivotTrans,d.childTransform,r.transform,v.Vertices,r.flipX,r.flipY);
            RequireLive(g!=null,"image fixture unsupported child chain");
            var ts=d.scaleMultiplier*d.textureScaleMultiplier;var c=d.color;var px=d.parallax.multiplier;
            var item=new GpuDecoData.Item
            {
                Id=d.GetInstanceID(),TextureId=v.TextureId,
                Flags=(d.lockScale?1:0)|(d.lockRotation?2:0)|(d.parallax.dontAlterX?4:0)|(d.parallax.dontAlterY?8:0)|(r.flipX?16:0)|(r.flipY?32:0),
                Constants=new[]{ts.x,ts.y,d.textureScaleMultiplier.x,d.textureScaleMultiplier.y,d.startRot,d.croppedPivotOffset.x,d.croppedPivotOffset.y,d.pivotTrans.position.z},
                Initial=new[]{d.pivotPosVec.x,d.pivotPosVec.y,d.pivotOffsetVec.x,d.pivotOffsetVec.y,d.rotAngle,d.scaleVec.x,d.scaleVec.y,c.r,c.g,c.b,c.a,d.opacity,px.x,px.y,d.parallaxOffset.x,d.parallaxOffset.y},
                Triangles=v.Triangles.Select(x=>(int)x).ToArray()
            };
            for(int ch=0;ch<16;ch++)item.Tracks[ch]=Array.Empty<DecoBakeCore.Segment>();
            for(int n=0;n<4;n++)
            {
                PutVec(item.Shape,n*3,g.Shape[n]);item.UV[n*2]=v.UV[n].x;item.UV[n*2+1]=v.UV[n].y;
            }
            var origin=g.Offset(Vector2.zero);PutVec(item.OffsetBasis,0,origin);
            PutVec(item.OffsetBasis,3,g.Offset(Vector2.right)-origin);PutVec(item.OffsetBasis,6,g.Offset(Vector2.up)-origin);
            return item;
        }
        static GpuDecoData ImageFixtureData(GpuDecoData.Item a,GpuDecoData.Item b)=>new GpuDecoData
        {Items=new[]{a,b},Pitch=1,TweenScale=1,Camera=new[]{0f,0f,0f,1f}};
        static int ImageVertexDifferences(GpuDecoGpu.Vertex[] a,GpuDecoGpu.Vertex[] b)
        {
            int bad=0;
            for(int i=0;i<a.Length;i++)
            {
                var x=a[i];var y=b[i];
                // Exact fresh-vs-update GPU equality, not a screen-pixel gate.
                if(!x.position.Equals(y.position)||!x.color.Equals(y.color)||!x.uv.Equals(y.uv))bad++;
                if(!DecoBakeCore.Finite(x.position.x)||!DecoBakeCore.Finite(x.position.y)||!DecoBakeCore.Finite(x.position.z))bad++;
            }
            return bad;
        }
        static void RunGpuImageGeometryFixture()
        {
            RequireLive(FrameCompare.Paused&&!gpuLiveActive&&!gpuLivePrepared&&!gpuImageShadow&&g2Window<0,"image fixture requires paused CPU and no prepared GPU resources");
            RequireLive(gpuImageVariants!=null&&!string.IsNullOrEmpty(gpuBundlePath),"image fixture requires compiled variants and bundle");
            var variants=gpuImageVariants.Where(v=>v.Status==GpuDecoImageVariant.Kind.ReadyQuad).ToArray();
            RequireLive(variants.Length>0,"no quad image variants");
            scrVisualDecoration d=null;
            foreach(var obj in baselineObjects)
            {
                var candidate=obj as scrVisualDecoration;
                if(!candidate||!candidate.spriteRenderer||!candidate.spriteRenderer.sprite)continue;
                var ts=candidate.scaleMultiplier*candidate.textureScaleMultiplier;
                if(ts.x==0||ts.y==0||candidate.textureScaleMultiplier.x==0||candidate.textureScaleMultiplier.y==0)continue;
                if(LiveDecoGeometry.Capture(candidate.pivotTrans,candidate.childTransform,candidate.spriteRenderer.transform,
                    variants[0].Vertices,candidate.spriteRenderer.flipX,candidate.spriteRenderer.flipY)==null)continue;
                d=candidate;break;
            }
            RequireLive(d,"no valid image fixture child chain");
            AssetBundle bundle=null;
            int updated=0,topologyRejected=0,bad=0,rejected=0,pointerChanges=0;
            try
            {
                bundle=AssetBundle.LoadFromFile(System.IO.Path.GetFullPath(gpuBundlePath));RequireLive(bundle,"image fixture bundle load");
                var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");RequireLive(shader,"image fixture compute asset");
                var original=ImageFixtureItem(d,variants[0]);var neighbor=ImageFixtureItem(d,variants[0]);
                neighbor.Initial=(float[])neighbor.Initial.Clone();neighbor.Initial[0]+=17f;neighbor.Initial[7]=.3125f;
                var data=ImageFixtureData(original,neighbor);
                using(var gpu=new GpuDecoGpu(shader,data){UseAnchors=false})
                {
                    var pointer=gpu.VertexBuffer.GetNativeBufferPtr();
                    foreach(var variant in variants)
                    {
                        for(int lane=0;lane<2;lane++)
                        {
                            var next=ImageFixtureItem(d,variant);
                            if(!next.Triangles.SequenceEqual(original.Triangles)){topologyRejected++;continue;}
                            next.Initial=data.Items[lane].Initial;next.Tracks=data.Items[lane].Tracks;next.Anchors=data.Items[lane].Anchors;
                            var before=gpu.Dispatch(0,true).vertices;
                            gpu.UpdateImageGeometry(lane,next);
                            var actual=gpu.Dispatch(0,true).vertices;
                            using(var fresh=new GpuDecoGpu(shader,ImageFixtureData(data.Items[0],data.Items[1])){UseAnchors=false})
                                bad+=ImageVertexDifferences(actual,fresh.Dispatch(0,true).vertices);
                            for(int i=0;i<4;i++)
                            {
                                int n=(1-lane)*4+i;
                                if(!before[n].position.Equals(actual[n].position)||!before[n].color.Equals(actual[n].color)||!before[n].uv.Equals(actual[n].uv))bad++;
                            }
                            if(pointer!=gpu.VertexBuffer.GetNativeBufferPtr())pointerChanges++;
                            updated++;
                        }
                    }
                    // Invalid input must not alter either lane or publish next.
                    for(int test=0;test<12;test++)
                    {
                        var next=ImageFixtureItem(d,variants[0]);int lane=0;
                        next.Initial=data.Items[0].Initial;next.Tracks=data.Items[0].Tracks;next.Anchors=data.Items[0].Anchors;
                        switch(test)
                        {
                            case 0:lane=-1;break;case 1:lane=2;break;case 2:next=null;break;
                            case 3:next.Shape=new float[11];break;case 4:next.Triangles[0]=(next.Triangles[0]+1)%4;break;
                            case 5:next.UV[0]=float.NaN;break;case 6:next.Shape[0]=float.PositiveInfinity;break;
                            case 7:next.OffsetBasis[0]=float.NaN;break;case 8:next.Constants[0]=float.NaN;break;
                            case 9:next.Constants[2]=0;break;case 10:next.Flags|=128;break;case 11:next.Initial=(float[])next.Initial.Clone();break;
                        }
                        var before=gpu.Dispatch(0,true).vertices;var prior=data.Items[0];bool threw=false;
                        try{gpu.UpdateImageGeometry(lane,next);}catch(InvalidOperationException){threw=true;rejected++;}
                        RequireLive(threw,"invalid image geometry accepted");
                        bad+=ImageVertexDifferences(before,gpu.Dispatch(0,true).vertices);
                        RequireLive(ReferenceEquals(prior,data.Items[0]),"rejected image metadata published");
                    }
                    // Restore geometry while retaining each lane's original logical channel arrays.
                    gpu.UpdateImageGeometry(0,original);gpu.UpdateImageGeometry(1,neighbor);
                    using(var fresh=new GpuDecoGpu(shader,ImageFixtureData(original,neighbor)){UseAnchors=false})
                        bad+=ImageVertexDifferences(gpu.Dispatch(0,true).vertices,fresh.Dispatch(0,true).vertices);
                    if(pointer!=gpu.VertexBuffer.GetNativeBufferPtr())pointerChanges++;
                }
            }
            finally{if(bundle)bundle.Unload(true);}
            GpuLog("image_geometry_fixture variants="+variants.Length+" lane_updates="+updated+" topology_rejected="+topologyRejected
                +" vertex_mismatch="+bad+" invalid_rejected="+rejected+" vertex_buffer_replaced="+pointerChanges
                +" CPU_setters_changed=0 GPU_texture_binding=False physical_pixels=UNMEASURED performance=False");
            RequireLive(updated>0&&bad==0&&rejected==12&&pointerChanges==0,"image geometry fixture failed");
        }
    }
}
#endif
