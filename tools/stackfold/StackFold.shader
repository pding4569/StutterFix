// Offline prototype only; not bundled or connected to game renderers.
// Fullscreen quad: _DstTex is an immutable copy of the destination immediately
// BEFORE this stack. Never bind the render target itself as _DstTex.
Shader "StutterFix/ResearchStackFold"
{
    Properties {
        _MainTex("Shared circle RGBA (linear sampler, mip 0)", 2D) = "white" {}
        _DstTex("Destination snapshot", 2D) = "black" {}
    }
    SubShader {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            Texture2D<float4> _MainTex, _DstTex;
            SamplerState sampler_MainTex, sampler_DstTex;
            int _LayerCount; // host must reject outside 0..256
            // dot(float3(screenUV,1), row.xyz) -> local sprite UV.
            float4 _UvRowX[256], _UvRowY[256];
            float4 _LayerColor[256]; // straight tint; alpha in [0,1]
            // xy = centre in UV, z = conservative opaque radius, w = enabled.
            // Set w=1 only if alpha=1 AND RGB=_CoreColor throughout the disk,
            // including every bilinear tap. compare.py provides alpha radius.
            float4 _Core;
            float _OuterRadius; // >= furthest nonzero alpha texel + bilinear footprint
            float _EmptyInnerRadius; // <= nearest nonzero alpha texel - footprint
            float3 _CoreColor;
            // 0 = Blend SrcAlpha OneMinusSrcAlpha on RGBA (source alpha squared)
            // 1 = premultiplied Blend One OneMinusSrcAlpha (sprite default)
            int _Premultiplied;

            float4 frag(v2f_img input) : SV_Target
            {
                float4 dst = _DstTex.SampleLevel(sampler_DstTex, input.uv, 0);
                float transmittance = 1;
                float4 contribution = 0;
                // Back-to-front order supplied by host. No early opacity cutoff:
                // dropping a small remaining T introduces an additional error.
                [loop] for (int i = 0; i < min(max(_LayerCount, 0), 256); i++) {
                    float3 p = float3(input.uv, 1);
                    float2 uv = float2(dot(p, _UvRowX[i].xyz), dot(p, _UvRowY[i].xyz));
                    if (any(uv < 0) || any(uv > 1)) continue;
                    float2 delta = uv - _Core.xy;
                    if (dot(delta, delta) < _EmptyInnerRadius * _EmptyInnerRadius) continue;
                    if (_OuterRadius > 0 && dot(delta, delta) > _OuterRadius * _OuterRadius) continue;
                    float4 sampleColor;
                    [branch] if (_Core.w > .5 && dot(delta, delta) < _Core.z * _Core.z)
                        sampleColor = float4(_CoreColor, 1);
                    else sampleColor = _MainTex.SampleLevel(sampler_MainTex, uv, 0);
                    float a = sampleColor.a * _LayerColor[i].a;
                    float4 src = float4(sampleColor.rgb * _LayerColor[i].rgb * a,
                        _Premultiplied != 0 ? a : a * a);
                    contribution = contribution * (1 - a) + src;
                    transmittance *= 1 - a;
                }
                return dst * transmittance + contribution;
            }
            ENDCG
        }
    }
    Fallback Off
}
