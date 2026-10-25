// 배리어 없이 이어지는 타일 장식 레이어(최대 8개, 텍스처 최대 4장)를 한 패스에서 순서대로 섞는다.
// 원본 Sprites/Tile 과 같은 블렌드(One OneMinusSrcAlpha, 선곱 알파)이며, 텍스처의 자기 샘플러(wrap/filter)를 그대로 쓴다.
Shader "StutterFix/GpuTileComposite"
{
    Properties
    {
        _Tex0 ("Tex 0", 2D) = "white" {}
        _Tex1 ("Tex 1", 2D) = "white" {}
        _Tex2 ("Tex 2", 2D) = "white" {}
        _Tex3 ("Tex 3", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            Texture2D _Tex0; SamplerState sampler_Tex0;
            Texture2D _Tex1; SamplerState sampler_Tex1;
            Texture2D _Tex2; SamplerState sampler_Tex2;
            Texture2D _Tex3; SamplerState sampler_Tex3;

            int _LayerCount;
            float4 _Cam;     // 카메라 중심 xy, 평면 z, 0
            float4 _Right;   // 화면 가로 반폭 벡터 xy
            float4 _Up;      // 화면 세로 반높이 벡터 xy
            float4 _Rect;    // 합성 쿼드가 덮는 화면 사각형(NDC 단위): xMin, yMin, xMax, yMax
            // 레이어별: s = M*dp + s0 (스프라이트 정규화 좌표), uv = K*(M*dp) + uv0 (타일 UV)
            float4 _P0[8];   // m00, m01, s0x, kx
            float4 _P1[8];   // m10, m11, s0y, ky
            float4 _P2[8];   // uv0x, uv0y, texIndex, 0
            float4 _P3[8];   // 불투명 범위 s: xMin, yMin, xMax, yMax
            float4 _Col[8];  // 렌더러 색 * 재질 색

            struct appdata { float3 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uv[8] : TEXCOORD0;   // xy = 타일 UV, zw = 스프라이트 정규화 좌표 s
            };

            v2f vert(appdata v)
            {
                v2f o;
                float2 ndc = lerp(_Rect.xy, _Rect.zw, (v.vertex.xy + 1.0) * 0.5);
                float2 dp = _Right.xy * ndc.x + _Up.xy * ndc.y; // 카메라 중심 기준 월드 오프셋
                float3 world = float3(_Cam.xy + dp, _Cam.z);
                o.pos = UnityObjectToClipPos(float4(world, 1));
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float2 m = float2(dot(_P0[i].xy, dp), dot(_P1[i].xy, dp));
                    float2 s = m + float2(_P0[i].z, _P1[i].z);
                    float2 uv = float2(_P0[i].w, _P1[i].w) * m + _P2[i].xy;
                    o.uv[i] = float4(uv, s);
                }
                return o;
            }

            float4 SampleTex(int idx, float2 uv)
            {
                if (idx <= 0) return _Tex0.SampleLevel(sampler_Tex0, uv, 0);
                if (idx == 1) return _Tex1.SampleLevel(sampler_Tex1, uv, 0);
                if (idx == 2) return _Tex2.SampleLevel(sampler_Tex2, uv, 0);
                return _Tex3.SampleLevel(sampler_Tex3, uv, 0);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 accRGB = 0; float accA = 0;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    if (k >= _LayerCount) break;
                    float2 s = i.uv[k].zw;
                    float4 b = _P3[k];
                    if (s.x < b.x || s.x > b.z || s.y < b.y || s.y > b.w) continue; // 이 레이어 쿼드 바깥
                    float4 t = SampleTex((int)(_P2[k].z + 0.5), i.uv[k].xy);
                    float4 col = _Col[k];
                    float a = t.a * col.a;
                    float3 c = (t.rgb * col.rgb) * a;
                    accRGB = c + (1.0 - a) * accRGB;
                    accA = a + (1.0 - a) * accA;
                }
                return float4(accRGB, accA);
            }
            ENDHLSL
        }
    }
}
