// 직통 렌더러(DR2) 셰이더. 장식 상태(채널 16개)와 정적 기록을 정점 셰이더가 직접 읽어 화면 위치를 계산한다.
// 식은 게임의 scrDecoration.UpdatePosition / scrParallax.SetTrans / SetRotation / SetScale 과 같다.
cbuffer Frame : register(b0)
{
    column_major float4x4 vp;   // GPU 투영 x 뷰 (렌더 텍스처용)
    float4 cam;                 // 장식 계산 시점의 카메라 x, y, 회전(도), lockScale 배율
    float4 ortho;               // orthographicSize, 픽셀 가로/세로, 화면 세로/가로, 0
};
Buffer<float4> channels : register(t0);   // 항목당 4개: (pos.xy, pivot.xy) (rot, scale.xy, r) (g, b, a, opacity) (parallax.xy, parallaxOffset.xy)
Buffer<float4> statics : register(t1);    // 항목당 8개
struct VIn { float2 st : POSITION; uint item : ITEM; };
struct VOut { float4 pos : SV_Position; float4 color : COLOR0; float2 uv : TEXCOORD0; float3 texel : TEXCOORD1; };

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
    world += c3.zw * camScale / k1.zw;
    precise float angle = k1.x + c1.x + ((flags & 2u) != 0 ? cam.z : 0.0);
    float sn, cs; sincos(radians(angle), sn, cs);
    precise float2 scale = c1.yz * camScale;
    precise float2 local = (c0.zw + k0.zw) / trueScale + float2(dot(ux.xyz, float3(i.st, 1)), dot(uy.xyz, float3(i.st, 1)));
    local *= scale;
    precise float2 p = world + float2(cs * local.x - sn * local.y, sn * local.x + cs * local.y);
    VOut o;
    o.pos = mul(vp, float4(p, k1.y, 1));
    // SpriteRenderer 의 그리기 색은 UNorm8 로 반올림된다. 그 뒤 재질 색을 곱한다.
    float4 color = float4(c1.w, c2.x, c2.y, c2.z * c2.w * k3.x);
    color = floor(saturate(color) * 255.0 + 0.5) / 255.0;
    o.color = color * tint;
    o.uv = (uvr.xy + i.st * uvr.zw) * k2.xy;
    o.texel = float3(k3.zw, k3.y);   // 텍스처 크기(점 샘플링 변형용), 마스크 알파 기준
    return o;
}

Texture2D image : register(t0);
SamplerState imageSampler : register(s0);
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
