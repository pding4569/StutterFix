using System;
using System.Collections.Generic;
using System.Linq;
using StutterFix;
using Row = StutterFix.DirectProxyPolicy.Row;

static class Program
{
 static void Check(bool ok,string text) {if(!ok)throw new Exception(text);}
 static Row R(int slot,float distance,int tex=0,uint flags=0,int mat=1,int layer=0,int order=0,int queue=3000,int goLayer=0)
  =>new Row(new SceneRenderOrder.Key(layer,order,queue,distance,mat,slot+100,0,1),slot,tex,flags,goLayer);
 static (List<DirectProxyPolicy.Span>,List<int>) Plan(params Row[] rows) {
  var list=rows.ToList();list.Sort((a,b)=>a.Key.CompareTo(b.Key));
  var spans=new List<DirectProxyPolicy.Span>();var returned=new List<int>();DirectProxyPolicy.Plan(list,spans,returned);return(spans,returned);
 }
 static void Main() {
  var (a,b)=Plan(R(0,10),R(-1,9),R(1,8));Check(a.Count==2&&b.Count==0,"interleaved Unity renderer must split only locally");
  (a,b)=Plan(R(0,10,mat:1),R(1,10,mat:2));Check(a.Count==1&&a[0].Count==2&&b.Count==0,"homogeneous complete tie bucket with known material order");
  (a,b)=Plan(R(0,10),R(1,10));Check(a.Count==0&&b.Count==2,"Unity same-material tie is unestablished");
  (a,b)=Plan(R(0,10),R(-1,10));Check(a.Count==0&&b.SequenceEqual(new[]{0}),"foreign tie must return source");
  (a,b)=Plan(R(0,10),R(1,10,tex:1));Check(a.Count==0&&b.Count==2,"heterogeneous tie must return entire bucket");
  (a,b)=Plan(R(0,10,flags:256),R(1,9,flags:256));Check(a.Count==2,"each Grab requires its own capture");
  (a,b)=Plan(R(0,10,flags:16),R(1,9,flags:32));Check(a.Count==2,"inside/outside stencil state split");
  (a,b)=Plan(R(0,10),R(1,9,goLayer:2));Check(a.Count==2,"camera culling layers split");
  (a,b)=Plan(R(0,10,order:1),R(1,9,order:2));Check(a.Count==2,"sorting order split");
  var random=new Random(6314);long entries=0,spansSeen=0,returnsSeen=0;
  for(int run=0;run<200;run++) {
   var rows=new List<Row>();
   for(int i=0;i<1000;i++) rows.Add(R(random.Next(4)==0?-1:i,random.Next(30),random.Next(4),(uint)(random.Next(5)==0?256:random.Next(3)*16),random.Next(8),random.Next(3),random.Next(12),3000,random.Next(2)));
   rows.Sort((x,y)=>x.Key.CompareTo(y.Key));var spans=new List<DirectProxyPolicy.Span>();var returned=new List<int>();DirectProxyPolicy.Plan(rows,spans,returned);
   var actual=new HashSet<int>();
   foreach(var span in spans) {
    Check(span.Count>0,"empty span");for(int k=0;k<span.Count;k++) {
     var row=rows[span.Start+k];Check(row.Slot>=0&&actual.Add(row.Slot),"barrier or duplicate owned slot");
     if(k>0)Check(rows[span.Start+k-1].Fits(row),"incompatible rows merged");
     int lo=span.Start+k;while(lo>0&&rows[lo-1].Key.SameBucket(row.Key))lo--;
     int hi=span.Start+k;while(hi+1<rows.Count&&rows[hi+1].Key.SameBucket(row.Key))hi++;
     if(hi>lo)for(int p=lo;p<=hi;p++)Check(row.Fits(rows[p]),"ambiguous tie admitted");
    }
   }
   foreach(int slot in returned)Check(actual.Add(slot),"returned twice");
   Check(actual.Count==rows.Count(x=>x.Slot>=0),"owned slot lost");
   entries+=rows.Count;spansSeen+=spans.Count;returnsSeen+=returned.Count;
  }
  Console.WriteLine($"M3 policy PASS: deterministic cases=9 randomized rows={entries} spans={spansSeen} returned={returnsSeen}; no lost slots, crossing barriers or ambiguous ties");
 }
}
