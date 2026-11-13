// Diagnostic coverage only. Original research shader; no game shader source copied.
Shader "StutterFix/ResearchZCull"
{
    Properties {
        [PerRendererData] _MainTex("Sprite texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        [PerRendererData] _RendererColor("Renderer color",Color)=(1,1,1,1)
        [PerRendererData] _Flip("Flip",Vector)=(1,1,1,1)
        RepeatX("Repeat X",Float)=1
        RepeatY("Repeat Y",Float)=1
        _SfZTile("Tile UV",Float)=0
    }
    SubShader {
        Tags {"Queue"="Transparent" "CanUseSpriteAtlas"="True"}
        Cull Off
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "UnitySprites.cginc"
        float _SfZDepth, _SfZTile, RepeatX, RepeatY;
        v2f vert(appdata_t v) {
            v2f o;
            if (_SfZTile > .5) {
                o.vertex=UnityObjectToClipPos(v.vertex); o.texcoord=v.texcoord; o.color=v.color*_Color;
            } else o=SpriteVert(v);
            // Logical near=0, far=1. Unity reverses the depth comparison on reversed-Z devices.
            #if defined(UNITY_REVERSED_Z)
                o.vertex.z=(1-_SfZDepth)*o.vertex.w;
            #else
                o.vertex.z=lerp(UNITY_NEAR_CLIP_VALUE,1,_SfZDepth)*o.vertex.w;
            #endif
            return o;
        }
        float opaque(v2f v):SV_Target {
            float2 uv=v.texcoord;
            // Match Sprites/Tile's fragment-stage repeat, with the source texture's sampler.
            if (_SfZTile>.5) uv*=float2(RepeatX,RepeatY);
            float a=tex2D(_MainTex,uv).a*v.color.a;
            if (!(a==1.0)) discard; // Reject NaN and HDR alpha>1 as well.
            return 1;
        }
        float count(v2f v):SV_Target { return 1; } // Includes alpha-zero fragments.
        ENDCG
        Pass {
            Name "Depth" ZWrite On ZTest LEqual ColorMask 0 Blend Off
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment opaque
            ENDCG
        }
        Pass {
            Name "Alive" ZWrite Off ZTest Less Blend One One
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment count
            ENDCG
        }
        Pass {
            Name "Total" ZWrite Off ZTest Always Blend One One
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment count
            ENDCG
        }
        Pass {
            Name "Cover" ZWrite Off ZTest Always Blend Off
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment opaque
            ENDCG
        }
    }
}
