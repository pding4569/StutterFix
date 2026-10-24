using System;
using StutterFix;
var values=new[]{0f,-0f,1f,float.Epsilon,-float.Epsilon,.5f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity};
int n=0;
foreach(float a in values)foreach(float b in values)foreach(int sa in new[]{-1,0,2047})foreach(int sb in new[]{-1,0,2047})
{
    bool hidden=GpuDecoAlphaPolicy.Hidden(a,sa,b,sb);
    if(hidden && (sa>=0 || sb>=0 || !float.IsFinite(a) || !float.IsFinite(b) || a*b!=0))throw new Exception("unsafe hidden alpha");
    if((a==float.Epsilon || b==float.Epsilon) && a!=0 && b!=0 && hidden)throw new Exception("epsilon cutoff");
    n++;
}
if(!GpuDecoAlphaPolicy.Hidden(0,-1,1,-1) || GpuDecoAlphaPolicy.Hidden(0,0,1,-1) || GpuDecoAlphaPolicy.Hidden(0,-1,1,0))throw new Exception("constant/animated distinction");
Console.WriteLine("GPUDECO_ALPHA_POLICY=PASS cases="+n+" active_sources_hidden=0 epsilon_cutoff=False pixel_game=UNMEASURED");
var mask=new GpuDecoCopyMask(65536);var active=new bool[65536];var rng=new Random(2741);
for(int pass=0;pass<10000;pass++) {
    int item=pass<128?pass*512:rng.Next(active.Length);bool state=pass<128||rng.Next(2)==1;
    active[item]=state;mask.Set(item,state);mask.Set(item,state);
    ulong lo=0,hi=0;
    for(int i=0;i<active.Length;i++)if(active[i]) {int c=i/512;if(c<64)lo|=1UL<<c;else hi|=1UL<<(c-64);}
    if(mask.Low!=lo || mask.High!=hi)throw new Exception("copy mask differs from renderer visibility");
}
for(int i=0;i<active.Length;i++)mask.Set(i,false);
if(mask.Low!=0 || mask.High!=0)throw new Exception("disabled chunk retained");
Console.WriteLine("GPUDECO_COPY_MASK=PASS changes=10000 duplicate_updates=10000 chunks=128 final_mask=0");
int size=2567;var order=new int[size];var inverse=new int[size];var flags=new bool[size];
for(int i=0;i<size;i++)order[i]=i;
for(int i=size-1;i>0;i--){int j=rng.Next(i+1);int v=order[i];order[i]=order[j];order[j]=v;}
for(int i=0;i<size;i++)inverse[order[i]]=i;
var shuffled=new GpuDecoCopyMask(size,order);
for(int pass=0;pass<10000;pass++) {
    int item=rng.Next(size);flags[item]=rng.Next(2)==1;shuffled.Set(item,flags[item]);ulong wanted=0;
    for(int i=0;i<size;i++)if(flags[i])wanted|=1UL<<(inverse[i]/512);
    if(shuffled.Low!=wanted || shuffled.High!=0)throw new Exception("permuted copy mask changed destination identity");
}
try {new GpuDecoCopyMask(3,new[]{0,0,2});throw new Exception("duplicate copy index accepted");}catch(ArgumentException){}
Console.WriteLine("GPUDECO_COPY_PERMUTATION=PASS changes=10000 duplicate_index=rejected renderer_order=UNCHANGED");
