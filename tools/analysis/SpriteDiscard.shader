// Original research shader, MIT, StutterFix contributors. No game shader code copied.
Shader "StutterFix/ResearchSpriteDiscard"
{
    Properties {
        [PerRendererData] _MainTex("Sprite Texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        PixelSnap("Pixel snap",Float)=0
        RepeatX("Repeat X",Float)=1
        RepeatY("Repeat Y",Float)=1
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
        Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ SF_SPRITE_DISCARD
            #pragma multi_compile_local _ SF_REPEAT_IN_VERTEX
            #include "UnityCG.cginc"
            sampler2D _MainTex;fixed4 _Color;float RepeatX,RepeatY;
            float _SfSpriteDiscardControl;
            struct Input {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Varying {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            Varying vert(Input v){
                Varying o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;
                #ifdef SF_REPEAT_IN_VERTEX
                o.uv*=float2(RepeatX,RepeatY);
                #endif
                return o;
            }
            fixed4 frag(Varying v):SV_Target{
                // Explicit counterfactual only: keep submissions/geometry,
                // omit sampling and blending. Never a quality optimization.
                if(_SfSpriteDiscardControl>1.5)discard;
                float2 uv=v.uv;
                #ifndef SF_REPEAT_IN_VERTEX
                uv*=float2(RepeatX,RepeatY);
                #endif
                fixed4 c=tex2D(_MainTex,uv)*v.color;
                #ifdef SF_SPRITE_DISCARD
                if(c.a==0)discard;
                #endif
                if(_SfSpriteDiscardControl>.5)c.rgb=fixed3(1,0,1);
                c.rgb*=c.a;return c;
            }
            ENDCG
        }
    }
}
