Shader "Hidden/GpuDecoLab/CompositeBench"
{
 SubShader
 {
  Cull Off ZWrite Off ZTest Always
  HLSLINCLUDE
  #pragma target 5.0
  #include "UnityCG.cginc"
  struct Layer { float4 row0; float4 row1; float4 tint; float4 info; };
  StructuredBuffer<Layer> _Layers;
  StructuredBuffer<uint> _TileCounts;
  StructuredBuffer<uint> _TileIndices;
  Texture2DArray<float4> _LayerTex;
  SamplerState sampler_LayerTex;
  int _Count, _LayerId, _Mode;
  float4 _Size;
  struct VIn { float4 vertex:POSITION; float2 uv:TEXCOORD0; float id:TEXCOORD1; };
  struct VOut { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; nointerpolation uint id:TEXCOORD1; };
  VOut vert(VIn v) { VOut o; o.pos=float4(v.vertex.xy*2-1,0,1); o.uv=v.uv; o.id=(uint)v.id; return o; }
  float4 sampleLayer(Layer l,float2 uv) {
   float4 s=_LayerTex.SampleLevel(sampler_LayerTex,float3(uv,l.info.x),0);
   float a=s.a*l.tint.a;
   return float4(s.rgb*l.tint.rgb*a,l.info.y>0.5?0:a);
  }
  float4 fragLayer(VOut i):SV_Target { return sampleLayer(_Layers[i.id],i.uv); }
  float4 fragBackground(VOut i):SV_Target {
   float2 p=i.uv;
   return float4(.08+.35*p.x,.12+.3*p.y,.15+.12*sin(p.x*37)*cos(p.y*23),.35+.55*p.x*p.y);
  }
  // Pixel center -> the same bottom-up screen coordinates as the mesh vertices.
  float2 screenUV(VOut i) { return float2(i.pos.x/_Size.x,1-i.pos.y/_Size.y); }
  float4 composite(VOut input, bool stats):SV_Target {
   float2 p=screenUV(input);
   uint tile=(uint)(input.pos.y/32)*(uint)_Size.z+(uint)(input.pos.x/32);
   uint count=_Mode==3?_TileCounts[tile]:(uint)_Count;
   float3 c=0; float t=1; uint visited=0, used=0;
   [loop] for(uint j=0;j<count;j++) {
    uint id=_Mode==3?_TileIndices[tile*64+j]:(uint)_Count-1-j;
    Layer l=_Layers[id];
    float2 uv=float2(dot(l.row0.xyz,float3(p,1)),dot(l.row1.xyz,float3(p,1)));
    visited++;
    if(all(uv>=0)&&all(uv<=1)) { float4 s=sampleLayer(l,uv); c+=t*s.rgb; t*=1-s.a; used++; }
    if(_Mode>=2 && t<(.5/255.0)) break;
   }
   // Stats: number of list entries omitted by early exit, iterations, samples, T.
   if(stats) return float4(count-visited,visited,used,t);
   return float4(c,1-t);
  }
  float4 fragComposite(VOut i):SV_Target { return composite(i,false); }
  float4 fragStats(VOut i):SV_Target { return composite(i,true); }
  ENDHLSL
  Pass { Blend Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment fragBackground
   ENDHLSL
  }
  Pass { Blend One OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment fragLayer
   ENDHLSL
  }
  Pass { Blend One OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment fragComposite
   ENDHLSL
  }
  Pass { Blend Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment fragStats
   ENDHLSL
  }
 }
}
