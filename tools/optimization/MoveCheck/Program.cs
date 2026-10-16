using System;
using StutterFix;
int n=0;
void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
Check(MoveDeltaPolicy.Within(0,0,0,0.5e-6f,-0.5e-6f,0),"tiny finite position");
Check(!MoveDeltaPolicy.Within(0,0,0,1e-6f,0,0),"strict threshold");
Check(!MoveDeltaPolicy.Within(0,0,0,0,0,1e-5f),"all axes");
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
    Check(!MoveDeltaPolicy.Within(bad,0,0,0,0,0),"invalid requests follow original setter");
float written=0;
for(int i=1;i<=100000;i++)
{
    float desired=i*0.1e-6f;
    if(!MoveDeltaPolicy.Within(written,0,0,desired,0,0))written=desired;
    Check(Math.Abs(written-desired)<MoveDeltaPolicy.Epsilon,"bounded drift");
}
Console.WriteLine($"Movement policy checks {n}, errors 0; game visual/performance parity not measured");
