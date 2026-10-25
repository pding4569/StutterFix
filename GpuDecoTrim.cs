#if AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // 큰 이미지 장식의 쿼드를 불투명한 부분에만 맞게 줄인다(투명 여백을 래스터라이즈하지 않으려고).
    // 텍스처의 알파>0 칸 범위를 컴퓨트로 한 번 구하고(여백 더함), 스프라이트 정점과 UV 를 같은 비율로 안쪽으로 옮긴다.
    internal static partial class DecoBake
    {
        static readonly Dictionary<int,Vector4> gpuTrimBounds=new Dictionary<int,Vector4>();
        static int gpuTrimTrimmed,gpuTrimKept;
        static long gpuTrimPixelsSaved;

        static void PrepareGpuTrimBounds(Dictionary<int,Texture2D> textures)
        {
            gpuTrimBounds.Clear();gpuTrimTrimmed=gpuTrimKept=0;gpuTrimPixelsSaved=0;
            if(!DrawCollapsePolicy.TightLargeQuads||gpuLiveSlices==null||gpuLiveSlices.Count==0)return;
            var cs=gpuLiveSlices[0].Gpu?.Shader;if(!cs||!cs.HasKernel("AlphaBounds"))return;
            int kernel=cs.FindKernel("AlphaBounds");
            long t0=System.Diagnostics.Stopwatch.GetTimestamp();int n=0;
            var buffer=new ComputeBuffer(4,4);var result=new uint[4];
            try
            {
                foreach(var pair in textures)
                {
                    var t=pair.Value;
                    if(!t||Math.Max(t.width,t.height)<DrawCollapsePolicy.TightMinSide||t.mipmapCount!=1)continue;
                    buffer.SetData(new uint[]{uint.MaxValue,uint.MaxValue,0,0});
                    cs.SetTexture(kernel,"_TrimTex",t);cs.SetBuffer(kernel,"_TrimOut",buffer);
                    cs.SetInts("_TrimSize",t.width,t.height);
                    cs.Dispatch(kernel,(t.width+15)/16,(t.height+15)/16,1);
                    buffer.GetData(result);
                    if(result[0]==uint.MaxValue||result[2]<result[0]||result[3]<result[1])continue; // 완전 투명: 줄이지 않음
                    int m=DrawCollapsePolicy.TightMarginTexels;
                    float u0=Mathf.Max(0,(result[0]-m))/(float)t.width,u1=Mathf.Min(t.width,result[2]+1+m)/(float)t.width;
                    float v0=Mathf.Max(0,(result[1]-m))/(float)t.height,v1=Mathf.Min(t.height,result[3]+1+m)/(float)t.height;
                    gpuTrimBounds[pair.Key]=new Vector4(u0,v0,u1,v1);n++;
                }
            }
            finally{buffer.Release();}
            GpuLog("GPUTRIM_BOUNDS textures="+n+" min_side="+DrawCollapsePolicy.TightMinSide+" margin="+DrawCollapsePolicy.TightMarginTexels
                +" ms="+(System.Diagnostics.Stopwatch.GetTimestamp()-t0)*1000.0/System.Diagnostics.Stopwatch.Frequency);
        }

        // 원본 변형의 정점·UV 를 불투명 범위로 좁힌 복사본을 돌려준다(줄일 수 없으면 원본 그대로).
        static GpuDecoImageVariant TightVariant(GpuDecoImageVariant v,Texture2D texture)
        {
            if(!DrawCollapsePolicy.TightLargeQuads||v==null||!texture||!gpuTrimBounds.TryGetValue(texture.GetInstanceID(),out var b)||v.Vertices==null||v.UV==null||v.Vertices.Length!=4||v.UV.Length!=4)return v;
            var uv=v.UV;
            float uA=Mathf.Min(Mathf.Min(uv[0].x,uv[1].x),Mathf.Min(uv[2].x,uv[3].x)),uB=Mathf.Max(Mathf.Max(uv[0].x,uv[1].x),Mathf.Max(uv[2].x,uv[3].x));
            float vA=Mathf.Min(Mathf.Min(uv[0].y,uv[1].y),Mathf.Min(uv[2].y,uv[3].y)),vB=Mathf.Max(Mathf.Max(uv[0].y,uv[1].y),Mathf.Max(uv[2].y,uv[3].y));
            float du=uB-uA,dv=vB-vA;
            if(du<=1e-5f||dv<=1e-5f||uA<0||vA<0||uB>1||vB>1){gpuTrimKept++;return v;}
            float u0=Mathf.Max(uA,b.x),u1=Mathf.Min(uB,b.z),v0=Mathf.Max(vA,b.y),v1=Mathf.Min(vB,b.w);
            if(u0>=u1||v0>=v1||(u1-u0)*(v1-v0)>=DrawCollapsePolicy.TightMaxAreaRatio*du*dv){gpuTrimKept++;return v;}
            // 각 모서리가 (uA..uB, vA..vB) 의 어느 쪽 끝인지(0/1)를 알아내 새 범위의 같은 쪽 끝으로 옮기고, 정점은 쌍선형으로 내분한다.
            var pv=new Vector2[4];var puv=new Vector2[4];
            for(int k=0;k<4;k++)
            {
                float sk=(uv[k].x-uA)/du,tk=(uv[k].y-vA)/dv;
                float nu=Mathf.Lerp(u0,u1,sk),nv=Mathf.Lerp(v0,v1,tk);
                float sn=(nu-uA)/du,tn=(nv-vA)/dv;
                Vector2 acc=Vector2.zero;
                for(int j=0;j<4;j++)
                {
                    float sj=(uv[j].x-uA)/du,tj=(uv[j].y-vA)/dv;
                    float w=(1f-Mathf.Abs(sn-sj))*(1f-Mathf.Abs(tn-tj));
                    acc+=v.Vertices[j]*w;
                }
                pv[k]=acc;puv[k]=new Vector2(nu,nv);
            }
            gpuTrimTrimmed++;
            return v.WithGeometry(pv,puv);
        }
    }
}
#endif
