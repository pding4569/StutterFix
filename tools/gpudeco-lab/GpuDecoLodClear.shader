// Clear only the low-resolution texels that the current composite can sample.
Shader "StutterFix/GpuDecoLodClear"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One Zero
            HLSLPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vert
            #pragma fragment frag
            StructuredBuffer<int> _LodBox;
            float _LodSpan;
            float4 _LodTarget; // full-resolution width, height, reciprocal width, height
            float4 _LodClearSize; // actual low-resolution width, height
            float4 vert(float3 v:POSITION):SV_POSITION
            {
                uint s=(uint)(_LodSpan+0.5)*4u,c=(uint)(v.x+0.5);
                int4 b=int4(_LodBox[s],_LodBox[s+1],_LodBox[s+2],_LodBox[s+3]);
                // Include both bilinear neighbours, also for odd target sizes.
                float2 lo=max(0,floor(float2(b.xy)*_LodTarget.zw*_LodClearSize.xy-1));
                float2 hi=min(_LodClearSize.xy,ceil(float2(b.zw)*_LodTarget.zw*_LodClearSize.xy+1));
                float2 p=float2((c==1||c==2)?hi.x:lo.x,c>=2?lo.y:hi.y);
                if(b.z<b.x||b.w<b.y)p=0;
                return float4(p.x/_LodClearSize.x*2-1,1-p.y/_LodClearSize.y*2,0,1);
            }
            float4 frag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
