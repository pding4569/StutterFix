"""Keep the production Unity proxy arithmetic identical to dr2.hlsl."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
out = root / 'effects/direct-deco'
out.mkdir(parents=True, exist_ok=True)
source = (root / 'native/sfnative/dr2.hlsl').read_text(encoding='utf-8-sig')
vertex = source[source.index('struct VIn'):source.index('Texture2D image')]
vertex = vertex.replace('#include "dr2_math.hlsli"', '#include "dr2_math.hlsli"')
common = '''// Generated from native/sfnative/dr2.hlsl; run generate-proxy-shader.py.
#include "UnityCG.cginc"
#define vp _SFDr2VP
#define cam _SFDr2Cam
#define ortho _SFDr2Ortho
float4x4 _SFDr2VP;
float4 _SFDr2Cam, _SFDr2Ortho;
StructuredBuffer<float4> channels, statics;
StructuredBuffer<uint> _Slots;
'''+vertex+'''
VOut ProxyVS(float2 st : TEXCOORD0, uint id : SV_VertexID)
{
    VIn i; i.st=st; i.item=_Slots[id/4u];
    return VS(i);
}
'''
pixel = source[source.index('Texture2D image'):]
# Unity binds the grab texture and sampler through ShaderLab.
pixel = pixel.replace('Texture2D background : register(t1);', 'sampler2D _GrabTexture;')
pixel = pixel.replace('background.Load(int3(int2(i.pos.xy), 0)).rgb', 'tex2D(_GrabTexture, float2(i.pos.x / _ScreenParams.x, i.pos.y / _ScreenParams.y)).rgb')
pixel = pixel.replace('Texture2D image : register(t0);', 'Texture2D image;')
pixel = pixel.replace('SamplerState imageSampler : register(s0);', 'SamplerState samplerimage;\n#define imageSampler samplerimage')
common += pixel + '''
float4 ProxyPS(VOut i) : SV_Target
{
    #if defined(SF_GRAB)
        return BlendGrab(i, _Point != 0);
    #else
        float2 uv = _Point != 0 ? (floor(i.uv*i.texel.xy)+0.5)/i.texel.xy : i.uv;
        float4 c=image.Sample(imageSampler,uv)*i.color;
        return _Add != 0 ? c : float4(c.rgb*c.a,c.a);
    #endif
}
'''
common = common.replace('float4x4 _SFDr2VP;', 'float _Point, _Add;\nfloat4x4 _SFDr2VP;')
(out / 'DirectDeco.cginc').write_text(common, encoding='utf-8')
(out / 'dr2_math.hlsli').write_bytes((root / 'native/sfnative/dr2_math.hlsli').read_bytes())
for grab in (False, True):
    name = 'DirectDecoGrab' if grab else 'DirectDeco'
    text = '''Shader "StutterFix/NAME"
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
  GRAB
  Pass {
   HLSLPROGRAM
   #pragma target 5.0
   #pragma only_renderers d3d11
   #pragma vertex ProxyVS
   #pragma fragment ProxyPS
   DEFINE
   #include "DirectDeco.cginc"
   ENDHLSL
  }
 }
}
'''.replace('NAME',name).replace('GRAB', 'GrabPass { }' if grab else '').replace('DEFINE','#define SF_GRAB 1' if grab else '')
    (out / (name+'.shader')).write_text(text, encoding='utf-8')
print('Generated Unity proxy shaders from production DR2 vertex/fragment arithmetic')
