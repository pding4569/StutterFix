// Original research code, MIT, StutterFix contributors. No game code copied.
Shader "StutterFix/ResearchSpriteChannels"
{
    Properties {
        [PerRendererData] _MainTex("Sprite Texture",2D)="white"{}
        _SfPackedTex("Gray and alpha",2D)="white"{}
        _SfAlphaRows("Nonzero alpha row bounds",Vector)=(0,1,0,0)
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
            #pragma multi_compile_local _ SF_PACKED_GRAY
            #pragma multi_compile_local _ SF_CHANNEL_CONTROL
            #pragma multi_compile_local _ SF_EMPTY_MASK
            #pragma multi_compile_local _ SF_ALPHA_ROWS
            #include "UnityCG.cginc"
            sampler2D _MainTex,_SfPackedTex;fixed4 _Color;float RepeatX,RepeatY;float4 _SfAlphaRows;
            struct Input {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Varying {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            Varying vert(Input v){Varying o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv*float2(RepeatX,RepeatY);return o;}
            fixed4 frag(Varying v):SV_Target{
                #ifdef SF_ALPHA_ROWS
                float y=v.uv.y;
                bool insideSeam=false;
                if(_SfAlphaRows.w!=0)
                {
                    y=frac(y);
                    insideSeam=y<=_SfAlphaRows.z||y>=1-_SfAlphaRows.z;
                }
                else y=saturate(y);
                // Retain both bilinear footprints at a repeat seam. Only
                // wholly transparent rows can bypass the original sample.
                [branch] if(abs(v.uv.y)<=8&&!insideSeam&&(y<_SfAlphaRows.x||y>_SfAlphaRows.y))discard;
                fixed4 c=tex2Dlod(_MainTex,float4(v.uv,0,0))*v.color;
                #elif defined(SF_EMPTY_MASK)
                if(tex2Dlod(_SfPackedTex,float4(v.uv,0,0)).r==0)discard;
                fixed4 c=tex2Dlod(_MainTex,float4(v.uv,0,0))*v.color;
                #else
                #ifdef SF_PACKED_GRAY
                fixed2 p=tex2D(_SfPackedTex,v.uv).rg;fixed4 c=fixed4(p.rrr,p.g)*v.color;
                #else
                fixed4 c=tex2D(_MainTex,v.uv)*v.color;
                #endif
                #endif
                #ifdef SF_CHANNEL_CONTROL
                c.rgb=fixed3(1,0,1);
                #endif
                c.rgb*=c.a;return c;
            }
            ENDCG
        }
    }
}
