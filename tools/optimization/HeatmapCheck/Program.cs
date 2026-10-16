using System;
using StutterFix;
int n=0;void Check(bool ok,string name){n++;if(!ok)throw new Exception(name);}
var h=new HitchHistogram();h.Add(7,40);h.Add(7,80);h.Add(2,80);h.Add(-1,50);h.Add(1,double.NaN);h.Add(1,-4);
var t=h.Top();Check(t.Length==2&&t[0].Floor==2&&t[1].Floor==7,"deterministic ties");
Check(t[1].Count==2&&t[1].Total==120&&t[1].Worst==80,"aggregate timing");Check(h.Missing==1,"unknown tile retained");
for(int i=10;i<1035;i++)h.Add(i,10+i);
Check(h.Overflow==3&&h.Top().Length==5,"bounded records and top5");
h.Add(7,1500);Check(h.Top()[0].Floor==7,"existing entries still update when full");
h.Clear();Check(h.Top().Length==0&&h.Missing==0&&h.Overflow==0,"new attempt clears counters");
Console.WriteLine($"Heatmap conditions {n}, errors 0; game classification and completion require separate validation");
