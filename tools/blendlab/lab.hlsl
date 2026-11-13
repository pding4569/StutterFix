cbuffer Params : register(b0) { uint mode, special, width, repeatTest; float alpha; uint candidate; uint pad0,pad1; }
struct V { float4 p:SV_POSITION; };
V VS(uint i:SV_VertexID) { V v; v.p=float4(i==1?3:-1,i==2?3:-1,0,1); return v; }
uint index(float4 p) { return (uint)p.x+(uint)p.y*width; }
float4 Init(V v):SV_TARGET { float d=(index(v.p)&255)/255.0; return d.xxxx; }
float4 PS(V v):SV_TARGET {
 uint n=index(v.p); float c=((n>>8)&255)/255.0;
 float a=special!=0?alpha:((n>>16)&255)/255.0;
 return float4((mode==0||mode==3?c*a:c).xxx,mode==3?0:a);
}
Texture2D<float4> actual:register(t0);
RWStructuredBuffer<uint> stats:register(u0);
uint q(float x) { return (uint)round(saturate(x)*255); }
[numthreads(16,16,1)] void CS(uint3 tid:SV_DispatchThreadID) {
 uint n=tid.x+tid.y*width; if(n>=width*width)return;
 float d=(n&255)/255.0,c=((n>>8)&255)/255.0;
 float a=special!=0?alpha:((n>>16)&255)/255.0;
 precise float s=(mode==0||mode==3)?c*a:c;
 float sa=mode==3?0:a;
 float sf=mode==1||mode==2?a:1;
 float df=mode==2||mode==4?1:1-sa;
 uint4 got=(uint4)round(actual[uint2(n%width,n/width)]*255);
 [unroll] for(uint ch=0;ch<2;ch++) {
  float src=ch==0?s:sa;
  precise float x=src*sf+d*df;
  uint expected;
  if(candidate==0)expected=q(x);
  else if(candidate==1)expected=(uint)floor(saturate(x)*255+0.5);
  else if(candidate<6) {
   float scale=candidate==2?255:candidate==3?256:candidate==4?512:1024;
   float ss=round(saturate(src)*scale)/scale;
   float fs=round(sf*scale)/scale,fd=round(df*scale)/scale;
   expected=q(ss*fs+d*fd);
  } else {
   uint si=(uint)round(saturate(src)*255), ai=(uint)round(sa*255), di=n&255;
   uint fi=(uint)round(sf*255), gi=(uint)round(df*255);
   if(candidate==6)expected=min(255u,(si*fi+di*gi+127)/255);
   else if(candidate==7)expected=min(255u,(si*(fi+(fi>>7))+di*(gi+(gi>>7))+128)>>8);
   else if(candidate==8)expected=min(255u,(uint)round(saturate(src*sf)*255)+(uint)round(d*df*255));
   else expected=min(255u,(si*fi+127)/255+(di*gi+127)/255);
  }
  if(mode==2 && ch==1)expected=n&255; // Actual legacy additive asset is ColorMask RGB.
  uint observed=ch==0?got.r:got.a;
  if(expected!=observed) {
   InterlockedAdd(stats[ch*3],1);
   uint old;InterlockedMin(stats[ch*3+1],n,old);
   InterlockedMax(stats[ch*3+2],(uint)abs((int)expected-(int)observed));
  }
 }
}

