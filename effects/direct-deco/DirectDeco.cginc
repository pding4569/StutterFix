// Generated from native/sfnative/dr2.hlsl; run generate-proxy-shader.py.
#include "UnityCG.cginc"
#define vp _SFDr2VP
#define cam _SFDr2Cam
#define ortho _SFDr2Ortho
float _Point, _Add;
float4x4 _SFDr2VP;
float4 _SFDr2Cam, _SFDr2Ortho;
StructuredBuffer<float4> channels, statics;
StructuredBuffer<uint> _Slots;
struct VIn { float2 st : POSITION; uint item : ITEM; };
struct VOut { float4 pos : SV_Position; float4 color : COLOR0; float2 uv : TEXCOORD0; float3 texel : TEXCOORD1; nointerpolation uint mode : TEXCOORD2; };
#include "dr2_math.hlsli"

float2 ParallaxWorld(float2 position, float2 pivot, float2 parallax, uint flags)
{
    if ((flags & 128u) != 0)
    {
        precise float2 v = position + pivot;
        if ((flags & 256u) != 0) v.x *= ortho.z;
        precise float2 srp = v / 20.0 + 0.5;
        precise float2 c = float2((2.0 * srp.x - 1.0) * ortho.x * ortho.y, (2.0 * srp.y - 1.0) * ortho.x);
        float sr, cr; sincos(radians(cam.z), sr, cr);
        return cam.xy + float2(cr * c.x - sr * c.y, sr * c.x + cr * c.y);
    }
    precise float2 world = (cam.xy - position) * parallax + position;
    return world;
}

VOut VS(VIn i)
{
    uint b = i.item * 4u, s = i.item * 8u;
    float4 c0 = channels[b], c1 = channels[b + 1u], c2 = channels[b + 2u], c3 = channels[b + 3u];
    float4 ux = statics[s], uy = statics[s + 1u], k0 = statics[s + 2u], k1 = statics[s + 3u], k2 = statics[s + 4u], uvr = statics[s + 5u], tint = statics[s + 6u], k3 = statics[s + 7u];
    uint flags = (uint)k2.z;
    precise float2 trueScale = k0.xy;
    precise float2 camScale = ((flags & 1u) != 0 ? cam.w : 1.0) * trueScale;
    precise float2 world = ParallaxWorld(c0.xy, c0.zw, c3.xy, flags);
    world += float2(CpuQuotient(c3.z * camScale.x, k1.z), CpuQuotient(c3.w * camScale.y, k1.w));
    precise float angle = k1.x + c1.x + ((flags & 2u) != 0 ? cam.z : 0.0);
    precise float4 rotation = ImageRotation(angle);
    precise float sn = rotation.z, cs = rotation.w;
    precise float2 scale = c1.yz * camScale;
    precise float2 p0 = float2(cs * scale.x, sn * scale.x), p1 = float2(-sn * scale.y, cs * scale.y);
    precise float2 q0 = p0 * ux.x + p1 * uy.x, q1 = p0 * ux.y + p1 * uy.y;
    precise float2 offset = float2(CpuQuotient(c0.z + k0.z, trueScale.x), CpuQuotient(c0.w + k0.w, trueScale.y));
    precise float2 q3 = (p0 * (offset.x + ux.z) + p1 * (offset.y + uy.z)) + world;
    precise float2 p = (q0 * i.st.x + q1 * i.st.y) + q3;
    VOut o;
    o.pos = mul(vp, float4(p, k1.y, 1));
    // SpriteRenderer 의 그리기 색은 UNorm8 로 반올림된다. 그 뒤 재질 색을 곱한다.
    float4 color = float4(c1.w, c2.x, c2.y, c2.z * c2.w * k3.x);
    color = floor(saturate(color) * 255.0 + 0.5) / 255.0;
    o.color = color * tint;
    o.uv = (uvr.xy + float2(i.st.x * uvr.z + i.st.y * ux.w, i.st.x * uy.w + i.st.y * uvr.w)) * k2.xy;
    o.mode = (uint)k2.w;
    o.texel = float3(k3.zw, k3.y);   // 텍스처 크기(점 샘플링 변형용), 마스크 알파 기준
    return o;
}


VOut ProxyVS(float2 st : TEXCOORD0, uint id : SV_VertexID)
{
    VIn i; i.st=st; i.item=_Slots[id/4u];
    return VS(i);
}
Texture2D image;
SamplerState samplerimage;
#define imageSampler samplerimage
// Sprites/Tile 기본: 샘플 x 색, rgb 에 알파를 곱해 낸다 (블렌드 One / OneMinusSrcAlpha)
float4 PS(VOut i) : SV_Target
{
    float4 c = image.Sample(imageSampler, i.uv) * i.color;
    return float4(c.rgb * c.a, c.a);
}
// Sprites/Tile 점 샘플링 변형: uv = (floor(uv * 크기) + 0.5) / 크기
float4 PSPoint(VOut i) : SV_Target
{
    float2 uv = (floor(i.uv * i.texel.xy) + 0.5) / i.texel.xy;
    float4 c = image.Sample(imageSampler, uv) * i.color;
    return float4(c.rgb * c.a, c.a);
}

// 더하기(Legacy Particles/Additive): 샘플 x 색(재질 색 x2 는 정적 값 tint 에 들어 있다). 블렌드 SrcAlpha / One, RGB 만 쓴다.
float4 PSAdd(VOut i) : SV_Target { return image.Sample(imageSampler, i.uv) * i.color; }
float4 PSAddPoint(VOut i) : SV_Target { float2 uv = (floor(i.uv * i.texel.xy) + 0.5) / i.texel.xy; return image.Sample(imageSampler, uv) * i.color; }
// 스프라이트 마스크(Sprites/Mask): 알파가 기준보다 작으면 버린다. 색은 쓰지 않고 스텐실만 바꾼다.
float4 PSMask(VOut i) : SV_Target { clip(image.Sample(imageSampler, i.uv).a - i.texel.z); return 0; }
float4 PSMaskPoint(VOut i) : SV_Target { float2 uv = (floor(i.uv * i.texel.xy) + 0.5) / i.texel.xy; clip(image.Sample(imageSampler, uv).a - i.texel.z); return 0; }

// 직전 화면의 해당 장식 사각형만 복사한 동일 크기 텍스처. SV_Position은 RT 픽셀 좌표다.
sampler2D _GrabTexture;
float4 BlendGrab(VOut i, bool usePoint)
{
    float2 uv = usePoint ? (floor(i.uv * i.texel.xy) + 0.5) / i.texel.xy : i.uv;
    float4 c = image.Sample(imageSampler, uv) * i.color;
    float3 b = tex2D(_GrabTexture, float2(i.pos.x / _ScreenParams.x, i.pos.y / _ScreenParams.y)).rgb, sc = c.rgb, r = sc;
    uint bm = i.mode;
    if (bm == 1) r = min(b, sc);
    else if (bm == 2) r = b * sc;
    else if (bm == 4) r = b + sc - 1.0;
    else if (bm == 6) r = max(b, sc);
    else if (bm == 7) r = 1.0 - (1.0 - b) * (1.0 - sc);
    else if (bm == 9) r = b + sc;
    else if (bm == 11) r = (b > .5) ? (1.0 - 2.0 * (1.0 - b) * (1.0 - sc)) : (2.0 * b * sc);
    else if (bm == 12) r = (b * (1.0 - b)) * sc + (1.0 - (1.0 - b) * (1.0 - sc)) * b;
    else if (bm == 18) r = abs(b - sc);
    else if (bm == 19) r = b + sc - 2.0 * b * sc;
    else if (bm == 20) r = b - sc;
    else if (bm == 21) r = saturate(b / sc);
    return float4(r * c.a, c.a);
}
float4 PSGrab(VOut i) : SV_Target { return BlendGrab(i, false); }
float4 PSGrabPoint(VOut i) : SV_Target { return BlendGrab(i, true); }

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
