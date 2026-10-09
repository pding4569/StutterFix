// Original StutterFix implementation. MIT; see license.txt.
Shader "StutterFix/ScreenEffects" {
 Properties { _MainTex("Scene",2D)="white"{} _GlowTex("Glow",2D)="black"{} _Lut("LUT",2D)="white"{} }
 SubShader {
 Cull Off ZWrite Off ZTest Always
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex, _GlowTex, _Lut;
 float4 _MainTex_TexelSize, _Color, _Controls;
 float4 _LutInfo; // size, enabled, unused, unused
 float4 _Glow; // threshold, strength, unused, unused
 float2 _Direction;
 float4 _Rays; // centre XY, length, decay
 float4 _StyleA; // chromatic pixels, grain, CRT, pixel size
 float4 _StyleB; // posterize levels, tone map, brightness ceiling, noise clock
 float luma(float3 c) { return dot(c,float3(.2126,.7152,.0722)); }
 float4 colorPass(v2f_img i):SV_Target {
   float4 src=tex2D(_MainTex,i.uv); float3 c=src.rgb;
   // Temperature and exposure, contrast about middle gray, then vibrance.
   c*=exp2(_Color.z)*float3(1+.12*_Color.w,1,1-.12*_Color.w);
   c=(c-.5)*_Color.y+.5;
   float sat=max(c.r,max(c.g,c.b))-min(c.r,min(c.g,c.b));
   c=lerp(luma(c).xxx,c,1+_Color.x*(1-saturate(sat)));
   if(_LutInfo.y>.5) {
     float n=_LutInfo.x;float3 q=saturate(c)*(n-1);float z=floor(q.b);
     // PNG strip convention: top row green=0; Unity texture V starts at bottom.
     float2 uv=float2((q.r+z*n+.5)/(n*n),1-(q.g+.5)/n);
     float3 a=tex2D(_Lut,uv).rgb;uv.x=min((q.r+min(z+1,n-1)*n+.5)/(n*n),1);
     c=lerp(a,tex2D(_Lut,uv).rgb,frac(q.b));
   }
   float2 p=i.uv*2-1; c*=1-_Controls.x*smoothstep(.25,1.4,dot(p,p));
   return float4(saturate(c),src.a);
 }
 float4 sharpenPass(v2f_img i):SV_Target {
   float2 t=_MainTex_TexelSize.xy;float4 c=tex2D(_MainTex,i.uv);
   float3 n=tex2D(_MainTex,i.uv+float2(t.x,0)).rgb;
   float3 s=tex2D(_MainTex,i.uv-float2(t.x,0)).rgb;
   float3 e=tex2D(_MainTex,i.uv+float2(0,t.y)).rgb;
   float3 w=tex2D(_MainTex,i.uv-float2(0,t.y)).rgb;
   float3 lo=min(c.rgb,min(min(n,s),min(e,w))),hi=max(c.rgb,max(max(n,s),max(e,w)));
   return float4(clamp(c.rgb+_Controls.y*(c.rgb-(n+s+e+w)*.25),lo,hi),c.a);
 }
 // Original FXAA-style directional edge pass; no third-party source copied.
 float4 aaPass(v2f_img i):SV_Target {
   float2 t=_MainTex_TexelSize.xy;float4 c=tex2D(_MainTex,i.uv);
   float nw=luma(tex2D(_MainTex,i.uv+t*float2(-1,-1)).rgb);
   float ne=luma(tex2D(_MainTex,i.uv+t*float2(1,-1)).rgb);
   float sw=luma(tex2D(_MainTex,i.uv+t*float2(-1,1)).rgb);
   float se=luma(tex2D(_MainTex,i.uv+t*float2(1,1)).rgb);
   float y=luma(c.rgb),lo=min(y,min(min(nw,ne),min(sw,se))),hi=max(y,max(max(nw,ne),max(sw,se)));
   if(hi-lo<max(.0312,hi*.125)) return c;
   float2 d=float2(sw+se-nw-ne,nw+sw-ne-se);
   d=clamp(d/(min(abs(d.x),abs(d.y))+max((nw+ne+sw+se)*.03125,.0078125)),-8,8)*t;
   float3 a=(tex2D(_MainTex,i.uv-d/6).rgb+tex2D(_MainTex,i.uv+d/6).rgb)*.5;
   float3 b=a*.5+(tex2D(_MainTex,i.uv-d*.5).rgb+tex2D(_MainTex,i.uv+d*.5).rgb)*.25;
   float by=luma(b);return float4(by<lo||by>hi?a:b,c.a);
 }
 float4 thresholdPass(v2f_img i):SV_Target {
   float3 c=tex2D(_MainTex,i.uv).rgb;float y=max(c.r,max(c.g,c.b));
   return float4(c*max(0,y-_Glow.x)/max(y,.0001),1);
 }
 float4 blurPass(v2f_img i):SV_Target {
   float2 t=_Direction*_MainTex_TexelSize.xy;
   return tex2D(_MainTex,i.uv)*.375+(tex2D(_MainTex,i.uv+t)+tex2D(_MainTex,i.uv-t))*.25+(tex2D(_MainTex,i.uv+t*2)+tex2D(_MainTex,i.uv-t*2))*.0625;
 }
 float4 glowPass(v2f_img i):SV_Target {
   float4 c=tex2D(_MainTex,i.uv);float3 added=tex2D(_GlowTex,i.uv).rgb*_Glow.y;
   // Optional headroom composition for the strong candidate. The old path
   // remains byte-identical when _Glow.z=0; this is not reconstruction of HDR.
   if(_Glow.z>.5) added*=1-saturate(c.rgb);
   return float4(c.rgb+added,c.a);
 }
 float4 lightPass(v2f_img i):SV_Target {
   float4 c=tex2D(_MainTex,i.uv);float3 light=tex2D(_GlowTex,i.uv).rgb;
   return float4(c.rgb+light*_Glow.y*(1-saturate(luma(c.rgb))),c.a);
 }
 // Original radial integration of bright image samples, not a depth-based
 // volumetric renderer. No ReShade/GPU Gems shader source copied.
 float4 raysPass(v2f_img i):SV_Target {
   float2 uv=i.uv,delta=(uv-_Rays.xy)*(_Rays.z/24);
   float3 sum=0;float weight=1,total=0;
   [unroll] for(int k=0;k<24;k++) {
     sum+=tex2D(_MainTex,saturate(uv)).rgb*weight;total+=weight;
     uv-=delta;weight*=_Rays.w;
   }
   return float4(sum/max(total,.0001),1);
 }
 float4 streakPass(v2f_img i):SV_Target {
   float3 c=0;float sum=0;
   [unroll] for(int k=-6;k<=6;k++) {
     float w=7-abs(k);c+=tex2D(_MainTex,saturate(i.uv+float2(k*_Direction.x,0))).rgb*w;sum+=w;
   }
   return float4(c/sum,1);
 }
 float4 flarePass(v2f_img i):SV_Target {
   float2 axis=.5-i.uv;float3 c=0;
   [unroll] for(int k=1;k<=4;k++) {
     float2 uv=i.uv+axis*(.4+k*.3);
     float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
     c+=tex2D(_MainTex,saturate(uv)).rgb*inside/(k+1);
   }
   return float4(c,1);
 }
 float noise(float2 p) {return frac(sin(dot(p,float2(31.73,79.21)))*12743.19);}
 float4 stylePass(v2f_img i):SV_Target {
   float2 uv=i.uv,p=uv*2-1;
   if(_StyleA.z>0) uv=.5+.5*p*(1+.045*_StyleA.z*dot(p,p));
   if(_StyleA.w>1) uv=(floor(uv/(_MainTex_TexelSize.xy*_StyleA.w))+.5)*(_MainTex_TexelSize.xy*_StyleA.w);
   float4 src=tex2D(_MainTex,saturate(uv));float3 c=src.rgb;
   if(_StyleA.x>0) {
     float2 shift=p*_StyleA.x*_MainTex_TexelSize.xy;
     c.r=tex2D(_MainTex,saturate(uv+shift)).r;c.b=tex2D(_MainTex,saturate(uv-shift)).b;
   }
   if(_StyleB.x>=2) c=floor(saturate(c)*(_StyleB.x-1)+.5)/(_StyleB.x-1);
   if(_StyleA.y>0) c+=(noise(floor(uv/_MainTex_TexelSize.xy)+floor(_StyleB.w*24))-.5)*_StyleA.y;
   if(_StyleA.z>0) {
     float scanline=.5+.5*cos(uv.y/_MainTex_TexelSize.y*3.14159265);
     c*=1-_StyleA.z*.2*scanline;
     c*=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
   }
   if(_StyleB.y>.5) {
     float cap=clamp(_StyleB.z,.5,1),bright=max(c.r,max(c.g,c.b)),knee=cap*.72;
     if(bright>knee) {float mapped=knee+(cap-knee)*(1-exp(-(bright-knee)/(cap-knee)));c*=mapped/max(bright,.0001);}
   }
   return float4(saturate(c),src.a);
 }
 float4 sceneBlurPass(v2f_img i):SV_Target {
   float4 c=tex2D(_MainTex,i.uv);return float4(tex2D(_GlowTex,i.uv).rgb,c.a);
 }
 ENDCG
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment colorPass
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment sharpenPass
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment aaPass
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment thresholdPass
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment blurPass
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment glowPass
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment lightPass
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment raysPass
 #pragma target 3.0
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment streakPass
 #pragma target 3.0
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment flarePass
 #pragma target 3.0
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment stylePass
 #pragma target 3.0
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment sceneBlurPass
 #pragma target 3.0
 ENDCG }
 }
 Fallback Off
}
