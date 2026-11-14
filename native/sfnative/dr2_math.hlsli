// Shared arithmetic ported from GpuDeco.compute (CPU float quotient / Unity image quaternion).
float CpuQuotient(float numerator,float denominator)
{
    // SM5 division uses an approximate reciprocal. Correct the residual and
    // round once, matching the CPU float quotient for polynomial easings too.
    precise float estimate=numerator/denominator;
    precise double residual=(double)numerator-(double)estimate*(double)denominator;
    precise double corrected=(double)estimate+residual*(double)(1.0/denominator);
    return (float)corrected;
}
float2 RotationSinCos(float x)
{
    int quadrant=(int)round(x*0.63661977236758134);
    precise double r=(double)x-(double)quadrant*1.57079632679489661923L;
    precise double z=r*r;
    precise double sn=r+r*z*(-0.16666666666666666667L+z*(0.00833333333333333333L+z*(-0.00019841269841269841L+z*(0.00000275573192239858907L+z*(-0.00000002505210838544172L+z*0.00000000016059043836821615L)))));
    precise double cs=1.0L+z*(-0.5L+z*(0.04166666666666666667L+z*(-0.00138888888888888889L+z*(0.00002480158730158730L+z*(-0.00000027557319223985891L+z*0.00000000208767569878681L)))));
    int q=(int)((uint)quadrant & 3u);
    return q==0?float2((float)sn,(float)cs):q==1?float2((float)cs,(float)-sn):q==2?float2((float)-sn,(float)-cs):float2((float)-cs,(float)sn);
}
float4 ImageRotation(float angle)
{
    precise float halfAngle=angle*0.01745329238474369*.5;
    precise float2 sc=RotationSinCos(halfAngle);
    // Transform's native normalization rounds both products before the dot
    // sum. Managed Quaternion.Normalize keeps a wider intermediate instead.
    precise float norm2=sc.x*sc.x+sc.y*sc.y;
    precise float norm=sqrt(norm2);
    if(asuint(norm2)==0x3f7fffff)norm=norm2;
    if(asuint(norm2)==0x3f800001)norm=1;
    precise float qz=CpuQuotient(sc.x,norm),qw=CpuQuotient(sc.y,norm);
    precise float twiceZ=qz*2;
    return float4(qz,qw,qw*twiceZ,1-qz*twiceZ);
}
