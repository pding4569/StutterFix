Shader "StutterFix/DirectDeco"
{
 Properties {
  image ("Texture", 2D) = "white" {}
  _SrcBlend ("Source", Float) = 1
  _DstBlend ("Destination", Float) = 10
  _ColorMask ("Color mask", Float) = 15
  _StencilRef ("Stencil ref", Float) = 0
  _StencilComp ("Stencil comparison", Float) = 8
  _Point ("Point", Float) = 0
  _Add ("Add", Float) = 0
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
  Cull Off ZWrite Off ZTest LEqual
  Blend [_SrcBlend] [_DstBlend]
  ColorMask [_ColorMask]
  Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass Keep ReadMask 255 WriteMask 0 }
  
  Pass {
   HLSLPROGRAM
   #pragma target 5.0
   #pragma only_renderers d3d11
   #pragma vertex ProxyVS
   #pragma fragment ProxyPS
   
   #include "DirectDeco.cginc"
   ENDHLSL
  }
 }
}
