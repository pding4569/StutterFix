Shader "Hidden/StutterFix/DecoGpu" {
 Properties { _MainTex("Sprite", 2D)="white"{} }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.5
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceTint)
   UNITY_INSTANCING_BUFFER_END(Props)
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 tint:COLOR0; };
   v2f vert(appdata v) {
    UNITY_SETUP_INSTANCE_ID(v);
    v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
    o.tint=UNITY_ACCESS_INSTANCED_PROP(Props,_InstanceTint); return o;
   }
   float4 frag(v2f i):SV_Target { float4 c=tex2D(_MainTex,i.uv)*i.tint; c.rgb*=c.a; return c; }
   ENDCG
  }
 }
}
