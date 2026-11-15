// Independent, named arithmetic oracle for the M4b WARP fixture. Production
// executes Unity's original DXBC; this source is NEVER used by the game renderer.
#ifndef FLOOR
#define FLOOR 0
#endif
#ifndef SNAP
#define SNAP 0
#endif
#ifndef POINT
#define POINT 0
#endif
cbuffer Reference : register(b0) {
    float4x4 World;
    float4x4 VP;
    float4 Screen;
    float4 P[12];
};
#define Color P[0]
#define Repeat P[1].xy
#define FlashColor P[1].z
#define TileST P[2]
#define Tint P[3]
#define ShadowColor P[4]
#define IconColor P[5]
#define CommentColor P[6]
#define UV2Offset P[7]
#define Flash P[8].x
#define Alpha P[8].y
#define IconAngle P[8].z
#define Active P[8].w
#define TileScale P[9].x
#define Inset P[9].y
#define AbsoluteInset P[9].z
#define PerlinStrength P[9].w
#define ShadowTune P[10].x
#define PerlinScale P[10].y
#define TextureRatio P[10].z
#define IconScale P[10].w
#define IconFlipped P[11].x
#define SpriteMode P[11].y
Texture2D<float4> Main : register(t0);
Texture2D<float4> Perlin : register(t1);
Texture2D<float4> Tile : register(t2);
Texture2D<float4> Icon : register(t3);
Texture2D<float4> Outline : register(t4);
SamplerState MainSampler : register(s0);
SamplerState TileSampler : register(s1);
SamplerState PerlinSampler : register(s2);
SamplerState IconSampler : register(s3);
SamplerState OutlineSampler : register(s4);
struct Input { float3 position : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 tileUv : TEXCOORD1; };
struct Varying { float4 position : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 tileUv : TEXCOORD1; };
Varying vertex(Input v) {
    Varying o;
    float4 w = mul(World, float4(v.position, 1));
    o.position = mul(VP, w);
#if SNAP
    float2 halfSize = Screen.xy * 0.5;
    o.position.xy = round(o.position.xy / o.position.w * halfSize) / halfSize * o.position.w;
#endif
#if FLOOR
    o.color = float4(lerp(Color.rgb, 1, FlashColor) * v.color.rgb, Color.a * v.color.a);
    o.uv = v.uv;
#else
    o.color = v.color * Color;
    o.uv = v.uv * Repeat;
#endif
    o.tileUv = v.tileUv;
    return o;
}
float2 rotateUv(float2 uv, float radians) {
    float s, c; sincos(radians, s, c);
    return float2(c * uv.x - s * uv.y, s * uv.x + c * uv.y);
}
float4 pixel(Varying v) : SV_TARGET {
#if !FLOOR
    float2 uv = v.uv;
#if POINT
    uint width, height; Main.GetDimensions(width, height);
    uv = (floor(uv * float2(width, height)) + 0.5) / float2(width, height);
#endif
    float4 color = Main.Sample(MainSampler, uv) * v.color;
    return float4(color.rgb * color.a, color.a);
#else
    if (v.uv.y > 1) {
        float fade = exp2(log2(2 - v.uv.y) * 1.3);
        return fade * float4(Color.rgb * ShadowColor.rgb, 1) * ShadowColor.a * Alpha * Active * v.color.a;
    }
    float2 center = SpriteMode != 0 ? 0.5 : TileST.zw;
    float3 offset = SpriteMode != 0 ? 0 : UV2Offset.xyz;
    float scale = SpriteMode != 0 ? 1 : TileScale;
    float2 tileUv = center + offset.xy + rotateUv(v.tileUv, offset.z * 3.141529) / scale;
    tileUv.y *= TextureRatio;
    float4 main = v.uv.y > Inset || AbsoluteInset != 0 ? Main.Sample(MainSampler, v.uv) * Tint : 1;
    float noise = Perlin.Sample(PerlinSampler, center + v.tileUv / PerlinScale).r * PerlinStrength;
    main.rgb = min(main.rgb * ShadowTune + noise * noise * noise, 1);
    float4 tile = Tile.Sample(TileSampler, tileUv) * v.color;
    tile.a = CommentColor.a * (1 - tile.a) + tile.a;
    tile.rgb = CommentColor.rgb * CommentColor.a + (1 - CommentColor.a) * tile.rgb;
    float4 base = main * tile;
    float2 iconUv = rotateUv(v.tileUv, IconAngle) / IconScale + 0.5;
    iconUv.x = lerp(iconUv.x, 1 - iconUv.x, IconFlipped);
    float inside = all(abs(iconUv - 0.5) <= 0.5) ? 1 : 0;
    float4 icon = Icon.Sample(IconSampler, iconUv) * IconColor;
    float4 outline = Outline.Sample(OutlineSampler, iconUv) * inside;
    float4 glyph = icon * inside;
    float glyphAlpha = glyph.a * (1 - outline.a) + outline.a;
    float3 glyphRgb = glyph.rgb * glyph.a + (1 - glyph.a) * outline.rgb;
    float alpha = glyphAlpha * (1 - base.a * Active) + base.a * Active;
    float3 rgb = glyphRgb * glyphAlpha + (1 - glyphAlpha) * base.rgb;
    alpha *= v.color.a * Alpha;
    rgb = lerp(rgb, 1, Flash);
    return float4(rgb * alpha, alpha);
#endif
}
