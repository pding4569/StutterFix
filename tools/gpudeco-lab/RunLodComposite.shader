// 묶음 LOD 합치기: 저해상도 그림(미리 곱한 알파)을 _Rect(클립 좌표) 사각형에 그대로 섞는다. 정점 x 는 모서리 번호 0~3.
Shader "StutterFix/RunLodComposite"
{
    Properties { _MainTex ("LOD", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            Texture2D _MainTex; SamplerState sampler_linear_clamp; float4 _LodTarget; float4 _Rect;
            struct a2v { float3 vertex:POSITION; };
            float4 vert(a2v v):SV_POSITION
            {
                uint c=(uint)(v.vertex.x+0.5);
                return float4((c==1||c==2)?_Rect.z:_Rect.x,c>=2?_Rect.w:_Rect.y,UNITY_NEAR_CLIP_VALUE,1);
            }
            float4 frag(float4 p:SV_POSITION):SV_Target { return _MainTex.SampleLevel(sampler_linear_clamp,p.xy*_LodTarget.zw,0); }
            ENDHLSL
        }
    }
}
