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
 float4 _Hdr;  // local contrast (clarity), shadow lift, highlight roll-off, saturation
 float4 _HdrB; // filmic (0/1), exposure, unused, unused
 float4 _GradeA; // sepia, duotone, teal-orange, invert
 float4 _GradeB; // mono, night vision, thermal, hue shift (radians)
 float4 _GradeC; // duotone hue (0..1), clock, unused, unused
 float4 _DetailA; // outline, emboss, halftone cell (pixels, 0 off), tilt-shift
 float4 _DetailB; // soft focus, halftone amount, unused, unused
 float4 _LensA; // barrel (-1..1), ripple, glitch, zoom blur
 float4 _LensB; // mirror (0/1), kaleidoscope segments (0 off), letterbox aspect (0 off), clock
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
 // ACES filmic curve (Narkowicz fit), normalized so white stays white.
 float3 aces(float3 x) {return (x*(2.51*x+.03))/(x*(2.43*x+.59)+.14);}
 // Pseudo-HDR look on an 8-bit picture: local contrast from a blurred copy, a shadow lift that keeps true black,
 // a highlight roll-off that keeps white, and a little saturation. Optional filmic curve. No new highlight detail is created.
 float4 hdrPass(v2f_img i):SV_Target {
   float4 src=tex2D(_MainTex,i.uv);float3 c=src.rgb;
   if(_Hdr.x>0) c+=(c-tex2D(_GlowTex,i.uv).rgb)*_Hdr.x;
   c=saturate(c);
   float L=luma(c);
   c+=_Hdr.y*c*(1-c)*(1-L);
   c=c*(1+_Hdr.z)/(1+_Hdr.z*c);
   c=lerp(luma(c).xxx,c,1+_Hdr.w);
   if(_HdrB.x>.5) {float e=max(.2,_HdrB.y);c=aces(c*e)/aces(float3(e,e,e));}
   return float4(saturate(c),src.a);
 }
 // ---- extra looks (original code, no third-party shader source) ----
 float3 hsv2rgb(float3 c) {
   float3 p=abs(frac(c.xxx+float3(0,2./3,1./3))*6-3);
   return c.z*lerp(float3(1,1,1),saturate(p-1),c.y);
 }
 // Hue rotation about the gray axis (the usual Rec.601-based color matrix).
 float3 hueRotate(float3 c,float a) {
   float s=sin(a),k=cos(a);
   return float3(
     dot(c,float3(.213+k*.787-s*.213,.715-k*.715-s*.715,.072-k*.072+s*.928)),
     dot(c,float3(.213-k*.213+s*.143,.715+k*.285+s*.140,.072-k*.072-s*.283)),
     dot(c,float3(.213-k*.213-s*.787,.715-k*.715+s*.715,.072+k*.928+s*.072)));
 }
 float3 thermalMap(float t) {
   float3 c=0;
   c=lerp(c,float3(0,0,.55),smoothstep(0,.2,t));
   c=lerp(c,float3(.6,0,.65),smoothstep(.2,.4,t));
   c=lerp(c,float3(1,.1,0),smoothstep(.4,.6,t));
   c=lerp(c,float3(1,.7,0),smoothstep(.6,.8,t));
   c=lerp(c,float3(1,1,.8),smoothstep(.8,1,t));
   return c;
 }
 float4 gradePass(v2f_img i):SV_Target {
   float4 src=tex2D(_MainTex,i.uv);float3 c=src.rgb;
   if(_GradeB.w!=0) c=saturate(hueRotate(c,_GradeB.w));
   if(_GradeA.x>0) {
     float3 s=float3(dot(c,float3(.393,.769,.189)),dot(c,float3(.349,.686,.168)),dot(c,float3(.272,.534,.131)));
     c=lerp(c,saturate(s),_GradeA.x);
   }
   if(_GradeB.x>0) c=lerp(c,luma(c).xxx,_GradeB.x);
   if(_GradeA.y>0) {
     float3 lo=hsv2rgb(float3(_GradeC.x,.85,.16)),hi=hsv2rgb(float3(frac(_GradeC.x+.08),.3,1));
     c=lerp(c,lerp(lo,hi,saturate(luma(c)*1.1)),_GradeA.y);
   }
   if(_GradeA.z>0) {
     float l=saturate(luma(c));
     float3 tint=lerp(float3(-.09,.025,.1),float3(.11,.04,-.11),l);
     c=saturate(c+tint*_GradeA.z+(c-.5)*.12*_GradeA.z);
   }
   if(_GradeB.z>0) c=lerp(c,thermalMap(saturate(luma(c)*1.05)),_GradeB.z);
   if(_GradeB.y>0) {
     float l=saturate(luma(c)*1.5+.03);
     float n=noise(floor(i.uv/_MainTex_TexelSize.xy)+floor(_GradeC.y*30));
     float3 g=float3(.08,1,.25)*pow(l,.8)*(.85+.3*n);
     g*=.9+.1*cos(i.uv.y/_MainTex_TexelSize.y*3.14159265);
     c=lerp(c,g,_GradeB.y);
   }
   if(_GradeA.w>0) c=lerp(c,1-c,_GradeA.w);
   return float4(saturate(c),src.a);
 }
 float4 detailPass(v2f_img i):SV_Target {
   float2 t=_MainTex_TexelSize.xy,uv=i.uv;
   float4 src=tex2D(_MainTex,uv);float3 c=src.rgb;
   if(_DetailA.z>1) {
     float2 cell=t*_DetailA.z;float2 centre=(floor(uv/cell)+.5)*cell;
     float3 s=tex2D(_MainTex,centre).rgb;float l=saturate(luma(s));
     float r=length((uv-centre)/cell);float radius=pow(saturate(l),.45)*.75;
     float m=smoothstep(radius,radius-.18,r);
     float3 dotColor=saturate(s/max(l*1.2,.25));
     c=lerp(c,dotColor*m+(1-m)*.015,_DetailB.y);
   }
   if(_DetailA.x>0) {
     float a=luma(tex2D(_MainTex,uv+t*float2(-1,-1)).rgb),b=luma(tex2D(_MainTex,uv+t*float2(0,-1)).rgb),d=luma(tex2D(_MainTex,uv+t*float2(1,-1)).rgb);
     float e=luma(tex2D(_MainTex,uv+t*float2(-1,0)).rgb),g=luma(tex2D(_MainTex,uv+t*float2(1,0)).rgb);
     float h=luma(tex2D(_MainTex,uv+t*float2(-1,1)).rgb),k=luma(tex2D(_MainTex,uv+t*float2(0,1)).rgb),m=luma(tex2D(_MainTex,uv+t*float2(1,1)).rgb);
     float gx=(d+2*g+m)-(a+2*e+h),gy=(h+2*k+m)-(a+2*b+d);
     float edge=saturate(sqrt(gx*gx+gy*gy)*3.5);
     float3 lineColor=luma(c)>.4?float3(0,0,0):float3(.92,.96,1);
     c=lerp(c,lineColor,edge*_DetailA.x);
   }
   if(_DetailA.y>0) {
     float d=luma(tex2D(_MainTex,uv+t).rgb)-luma(tex2D(_MainTex,uv-t).rgb);
     c=lerp(c,saturate(c+d*3.0),_DetailA.y);
   }
   if(_DetailA.w>0) {
     float w=smoothstep(.1,.42,abs(uv.y-.5))*_DetailA.w;
     c=lerp(c,tex2D(_GlowTex,uv).rgb,w);
   }
   if(_DetailB.x>0) {
     float3 b=tex2D(_GlowTex,uv).rgb;
     c=1-(1-c)*(1-b*_DetailB.x*.8);
   }
   return float4(saturate(c),src.a);
 }
 float4 lensPass(v2f_img i):SV_Target {
   float2 uv=i.uv;float aspect=_MainTex_TexelSize.z/_MainTex_TexelSize.w;
   if(_LensB.x>.5 && uv.x>.5) uv.x=1-uv.x;
   if(_LensB.y>=2) {
     float2 q=(uv-.5)*float2(aspect,1);float r=length(q),a=atan2(q.y,q.x);
     float seg=6.28318531/_LensB.y;a=abs(fmod(a+6.28318531,seg)-seg*.5);
     q=float2(cos(a),sin(a))*r;uv=q/float2(aspect,1)+.5;
   }
   if(_LensA.x!=0) {
     float2 q=uv*2-1;float r2=dot(q,q);
     q=q*(1+_LensA.x*r2)/(1+max(_LensA.x,0)*.9);
     uv=q*.5+.5;
   }
   if(_LensA.y>0) uv+=float2(sin(uv.y*38+_LensB.w*3.1),cos(uv.x*30+_LensB.w*2.3))*.0045*_LensA.y;
   float2 split=0;
   if(_LensA.z>0) {
     float band=floor(uv.y*34),tk=floor(_LensB.w*11);
     if(noise(float2(band,tk))>1-.2*_LensA.z) {
       uv.x+=(noise(float2(band,tk+7))-.5)*.14*_LensA.z;
       split=float2(.006*_LensA.z,0);
     }
   }
   uv=saturate(uv);float3 c;
   if(_LensA.w>0) {
     float3 sum=0;
     [unroll] for(int k=0;k<8;k++) {
       float s=k/8.;float2 p=lerp(uv,float2(.5,.5),s*_LensA.w*.14);
       sum+=tex2D(_MainTex,p).rgb;
     }
     c=sum/8;
   } else c=tex2D(_MainTex,uv).rgb;
   if(split.x>0) {c.r=tex2D(_MainTex,saturate(uv+split)).r;c.b=tex2D(_MainTex,saturate(uv-split)).b;}
   if(_LensB.z>0) {
     float h=aspect/_LensB.z;
     if(h<1) {float m=(1-h)*.5;if(i.uv.y<m || i.uv.y>1-m) c=0;}
   }
   return float4(c,1);
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
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment hdrPass
 #pragma target 3.0
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment gradePass
 #pragma target 3.0
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment detailPass
 #pragma target 3.0
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment lensPass
 #pragma target 3.0
 ENDCG }
 }
 Fallback Off
}
