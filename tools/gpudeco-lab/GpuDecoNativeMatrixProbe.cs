#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class GpuDecoLab
{
    public static void NativeMatrixProbe()=>Run(()=>
    {
        var args=Environment.GetCommandLineArgs();
        string Arg(string name){int i=Array.IndexOf(args,name);Require(i>=0&&i+1<args.Length,name);return args[i+1];}
        var actual=File.ReadAllLines(Arg("-angles")).Select(x=>float.Parse(x,Inv));
        var angles=Enumerable.Range(0,2049).Select(i=>-720f+i*1440f/2048).Concat(actual).ToArray();
        var bundle=AssetBundle.LoadFromFile(Arg("-imageBundle"));Require(bundle,"bundle");
        var go=new GameObject("native matrix oracle");
        try
        {
            var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");
            int k=shader.FindKernel(shader.HasKernel("ImageRotationProbe")?"ImageRotationProbe":"RotationProbe"),rawBad=0,normBad=0,basisBad=0;float maxBasis=0;
            var candidateBad=new int[4];var divideBad=new int[5];var pointBad=new int[5];int pointCount=0;
            float R(float x)=>BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(x));
            using(var input=new ComputeBuffer(angles.Length,16))using(var output=new ComputeBuffer(angles.Length,16))
            {
                input.SetData(angles.Select(a=>new Vector4(a,0,0,0)).ToArray());shader.SetBuffer(k,"_Probe",input);shader.SetBuffer(k,"_RotationOut",output);shader.SetInt("_Count",angles.Length);
                shader.Dispatch(k,(angles.Length+63)/64,1,1);var gpu=new Vector4[angles.Length];output.GetData(gpu);
                for(int i=0;i<angles.Length;i++)
                {
                    var q=Quaternion.Euler(0,0,angles[i]);go.transform.rotation=q;var native=go.transform.rotation;
                    var normal=Quaternion.Normalize(q);if(!q.Equals(native))rawBad++;if(!normal.Equals(native))normBad++;
                    float length2=q.z*q.z+q.w*q.w;
                    var factors=new[]{1f/Mathf.Sqrt(length2),(float)(1/Math.Sqrt(length2)),(float)(1/Math.Sqrt((double)q.z*q.z+(double)q.w*q.w)),1f};
                    for(int c=0;c<factors.Length;c++)if(!new Quaternion(0,0,q.z*factors[c],q.w*factors[c]).Equals(native))candidateBad[c]++;
                    var dots=new[]{R(R(q.z*q.z)+R(q.w*q.w)),R((float)((double)q.z*q.z+(double)q.w*q.w)),R((float)((double)q.z*q.z+R(q.w*q.w))),R((float)(R(q.z*q.z)+(double)q.w*q.w))};
                    for(int c=0;c<dots.Length;c++){float norm=R(Mathf.Sqrt(dots[c]));if(!new Quaternion(0,0,R(q.z/norm),R(q.w/norm)).Equals(native))divideBad[c]++;}
                    if(!Quaternion.Normalize(normal).Equals(native))divideBad[4]++;
                    var matrix=go.transform.localToWorldMatrix;
                    float error=Mathf.Max(Mathf.Abs(matrix.m00-gpu[i].w),Mathf.Abs(matrix.m10-gpu[i].z));
                    if(error>0)basisBad++;maxBasis=Mathf.Max(maxBasis,error);
                    go.transform.position=new Vector3(624.137f,1098.213f,0);go.transform.localScale=new Vector3(.289277315f,.264232f,1);
                    var world=go.transform.localToWorldMatrix;
                    foreach(float x in new[]{-3.41f,3.41f})foreach(float y in new[]{-3.41f,3.41f})
                    {
                        var truth=go.transform.TransformPoint(new Vector3(x,y,0));
                        for(int axis=0;axis<2;axis++)
                        {
                            float a=world[axis,0],b=world[axis,1],p=world[axis,3],expected=truth[axis];pointCount++;
                            var points=new[]{R(R(R(a*x)+R(b*y))+p),R(R(a*x)+R(R(b*y)+p)),R((float)((double)a*x+R(R(b*y)+p))),R((float)(R(a*x)+((double)b*y+p))),R((float)((double)a*x+(double)b*y+p))};
                            for(int mode=0;mode<points.Length;mode++)if(points[mode]!=expected)pointBad[mode]++;
                        }
                    }
                    go.transform.position=Vector3.zero;go.transform.localScale=Vector3.one;
                    if(i>=2049)Log("NATIVE_MATRIX angle="+N(angles[i])+" raw="+q.ToString("G9")+" native="+native.ToString("G9")+" normalized="+normal.ToString("G9")+" basis="+N(matrix.m00)+","+N(matrix.m10)+" gpu="+N(gpu[i].w)+","+N(gpu[i].z)+" max_error="+N(error));
                }
            }
            Log("NATIVE_MATRIX samples="+angles.Length+" raw_native_different="+rawBad+" normalized_native_different="+normBad+" GPU_basis_different="+basisBad+" max_basis_error="+N(maxBasis)+" game_pixels=UNMEASURED");
            Log("NATIVE_NORMALIZATION mismatch_float_recip_sqrt="+candidateBad[0]+" mismatch_double_recip_float_norm="+candidateBad[1]+" mismatch_double_recip_double_norm="+candidateBad[2]+" mismatch_identity="+candidateBad[3]);
            Log("NATIVE_DIVIDE mismatch_rounded_dot="+divideBad[0]+" mismatch_double_dot="+divideBad[1]+" mismatch_fma_z="+divideBad[2]+" mismatch_fma_w="+divideBad[3]+" mismatch_twice_normalized="+divideBad[4]);
            Log("NATIVE_POINT samples="+pointCount+" separate_left="+pointBad[0]+" separate_right="+pointBad[1]+" fma_left="+pointBad[2]+" fma_right="+pointBad[3]+" double_all="+pointBad[4]);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);bundle.Unload(true);}
    },"native-matrix");
}
#endif
