// Own synthetic scene; no game assets/code. Camera vectors: x, y, zoom, rotation (radians).
cbuffer State : register(b0) {
    float4 sourceCamera;
    float4 currentCamera;
    float4 clock; // source seconds, output seconds, aspect, overscan
    float4 flags; // mode: 0 none / 1 reproj / 2 hybrid, source pass, reserved
};
Texture2D history : register(t0);
SamplerState linearSampler : register(s0);
struct Vertex { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
Vertex VS(uint id : SV_VertexID) {
    Vertex v;
    v.uv = float2((id << 1) & 2, id & 2);
    v.pos = float4(v.uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return v;
}
float2 rotate2(float2 p, float a) {
    float c = cos(a), s = sin(a);
    return float2(c*p.x-s*p.y, s*p.x+c*p.y);
}
float2 world(float2 uv, float4 cam, float scale) {
    float2 p = (uv - 0.5) * float2(clock.z, -1) * 10 * scale / cam.z;
    return cam.xy + rotate2(p, cam.w);
}
float2 sourceUV(float2 p) {
    float2 q = rotate2(p - sourceCamera.xy, -sourceCamera.w) * sourceCamera.z;
    return q / (float2(clock.z, -1) * 10 * clock.w) + 0.5;
}
float tileY(float x) { return 1.6 * sin(x * 0.38); }
float3 planetColor(float2 p, float t, float3 col) {
    float k = floor(t / 0.4), f = frac(t / 0.4);
    float2 a = float2(k, tileY(k)), b = float2(k+1, tileY(k+1));
    float2 moving = a + rotate2(b-a, (1-f)*3.14159265);
    float d0 = length(p-a), d1 = length(p-moving);
    bool redPivot = fmod(k, 2) < 1;
    if (d0 < 0.29) col = redPivot ? float3(0.94,0.25,0.18) : float3(0.15,0.52,1);
    if (d1 < 0.29) col = redPivot ? float3(0.15,0.52,1) : float3(0.94,0.25,0.18);
    return col;
}
float3 scene(float2 p, float t, bool withPlanets) {
    float3 col = float3(0.025,0.032,0.05);
    float k = floor(p.x + 0.5);
    float2 d = abs(p - float2(k,tileY(k)));
    if (max(d.x,d.y) < 0.4) col = float3(0.17,0.21,0.29);
    if (max(d.x,d.y) < 0.35) col = float3(0.11,0.14,0.20);
    float2 deco = float2(t*2.5+2.5*cos(t*2), 2.8+0.6*sin(t*3));
    if (max(abs(p.x-deco.x), abs(p.y-deco.y)) < 0.3) col = float3(0.7,0.55,0.16);
    return withPlanets ? planetColor(p,t,col) : col;
}
float4 PS(Vertex v) : SV_TARGET {
    if (flags.y > 0.5) return float4(scene(world(v.uv,sourceCamera,clock.w),clock.x,flags.x < 1.5),1);
    float4 cam = flags.x < 0.5 ? sourceCamera : currentCamera;
    float2 p = world(v.uv,cam,1);
    float2 uv = sourceUV(p);
    float3 col = history.Sample(linearSampler,uv).rgb;
    if (flags.x > 1.5) col = planetColor(p,clock.y,col);
    // UI is composited after reprojection and stays fixed; this does not solve Unity UI capture.
    if (v.uv.y < 0.018) col = float3(0.23,0.25,0.29);
    if (v.uv.y < 0.018 && v.uv.x < frac(clock.y/8)) col = float3(0.8,0.82,0.86);
    return float4(col,1);
}
// Offscreen GPU check before timing: compare affine UV coordinates against independent CPU math.
float4 VerifyPS(Vertex v) : SV_TARGET {
    return float4(sourceUV(world(v.uv,currentCamera,1)),0,1);
}
