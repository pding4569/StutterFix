// Original fixture shader, MIT, StutterFix contributors.
Shader "StutterFix/ResearchWrapSamplerFixture"
{
    Properties {
        _Source2D("Native source",2D)="white"{}
        _Candidate2D("Candidate source",2D)="white"{}
        _CandidateArray("Exact size copy",2DArray)=""{}
        _Flags("Flags",Integer)=0
        _Mode("Mode",Integer)=0
    }
    SubShader { Pass {
        Cull Off ZWrite Off ZTest Always Blend Off
        HLSLPROGRAM
        #pragma target 5.0
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        Texture2D<float4> _Source2D;
        SamplerState sampler_Source2D;
        Texture2D<float4> _Candidate2D;
        Texture2DArray<float4> _CandidateArray;
        SamplerState sampler_point_clamp, sampler_linear_clamp;
        SamplerState sampler_point_repeat, sampler_linear_repeat;
        SamplerState sampler_point_RepeatU_ClampV, sampler_linear_RepeatU_ClampV;
        SamplerState sampler_point_ClampU_RepeatV, sampler_linear_ClampU_RepeatV;
        float4 _ScaleOffset,_HalfTexel;
        int _Flags,_Mode;
        struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
        struct Varying {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
        Varying vert(Input v){Varying o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv*_ScaleOffset.xy+_ScaleOffset.zw;return o;}
        float4 Sample2D(float2 uv,bool pointSample,bool u,bool v)
        {
            if(pointSample){
                if(u){if(v)return _Candidate2D.SampleLevel(sampler_point_repeat,uv,0);return _Candidate2D.SampleLevel(sampler_point_RepeatU_ClampV,uv,0);}
                if(v)return _Candidate2D.SampleLevel(sampler_point_ClampU_RepeatV,uv,0);return _Candidate2D.SampleLevel(sampler_point_clamp,uv,0);
            }
            if(u){if(v)return _Candidate2D.SampleLevel(sampler_linear_repeat,uv,0);return _Candidate2D.SampleLevel(sampler_linear_RepeatU_ClampV,uv,0);}
            if(v)return _Candidate2D.SampleLevel(sampler_linear_ClampU_RepeatV,uv,0);return _Candidate2D.SampleLevel(sampler_linear_clamp,uv,0);
        }
        float4 SampleArray(float2 uv,bool pointSample,bool u,bool v)
        {
            float3 p=float3(uv,0);
            if(pointSample){
                if(u){if(v)return _CandidateArray.SampleLevel(sampler_point_repeat,p,0);return _CandidateArray.SampleLevel(sampler_point_RepeatU_ClampV,p,0);}
                if(v)return _CandidateArray.SampleLevel(sampler_point_ClampU_RepeatV,p,0);return _CandidateArray.SampleLevel(sampler_point_clamp,p,0);
            }
            if(u){if(v)return _CandidateArray.SampleLevel(sampler_linear_repeat,p,0);return _CandidateArray.SampleLevel(sampler_linear_RepeatU_ClampV,p,0);}
            if(v)return _CandidateArray.SampleLevel(sampler_linear_ClampU_RepeatV,p,0);return _CandidateArray.SampleLevel(sampler_linear_clamp,p,0);
        }
        float4 frag(Varying v):SV_Target
        {
            bool pointSample=(_Flags&1)!=0,u=(_Flags&2)!=0,w=(_Flags&4)!=0;
            if(_Mode==0)return _Source2D.SampleLevel(sampler_Source2D,v.uv,0);
            if(_Mode==1)return Sample2D(v.uv,pointSample,u,w);
            if(_Mode==2)return SampleArray(v.uv,pointSample,u,w);
            // Deliberately retain the old fract+clamp-centres model as a
            // negative control, not as a candidate for correct repetition.
            float2 uv=float2(u?frac(v.uv.x):v.uv.x,w?frac(v.uv.y):v.uv.y);
            uv=clamp(uv,_HalfTexel.xy,1-_HalfTexel.xy);
            return pointSample?_CandidateArray.SampleLevel(sampler_point_clamp,float3(uv,0),0):_CandidateArray.SampleLevel(sampler_linear_clamp,float3(uv,0),0);
        }
        ENDHLSL
    }}
}
