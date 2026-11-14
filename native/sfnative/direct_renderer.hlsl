struct Instance { float3 a; float3 b; float4 color; float4 uv; uint layer; uint flags; float2 parallax; };
StructuredBuffer<Instance> items:register(t0);
cbuffer Camera:register(b0){column_major float4x4 vp;float4 camera;};
struct Vary {float4 pos:SV_Position;float3 uv:TEXCOORD0;float4 color:COLOR0;};
Vary VS(uint vid:SV_VertexID,uint iid:SV_InstanceID){
    const float2 corner[6]={float2(0,0),float2(0,1),float2(1,0),float2(1,0),float2(0,1),float2(1,1)};
    Instance i=items[iid];float2 p=corner[vid]-0.5;
    float2 world=float2(dot(i.a,float3(p,1)),dot(i.b,float3(p,1)));
    world+=camera.xy*(1-i.parallax);if(i.flags&1)world+=camera.xy*i.parallax;
    Vary o;o.pos=mul(vp,float4(world,0,1));o.uv=float3(lerp(i.uv.xy,i.uv.zw,corner[vid]),i.layer);o.color=i.color;return o;
}
Texture2DArray image:register(t0);SamplerState linearClamp:register(s0);
float4 PS(Vary i):SV_Target {float4 c=image.Sample(linearClamp,i.uv)*i.color;return float4(c.rgb*c.a,c.a);}
