using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StutterFix;
int tests=0;
void Check(bool b,string what){tests++;if(!b)throw new Exception(what);}
bool Equal(object a,object b)
{
    if(a==null||b==null)return a==b;
    if(a.GetType()!=b.GetType())return false;
    if(a is Dictionary<string,object> d){var e=(Dictionary<string,object>)b;return d.Keys.SequenceEqual(e.Keys)&&d.All(k=>Equal(k.Value,e[k.Key]));}
    if(a is IList x){var y=(IList)b;return x.Count==y.Count&&Enumerable.Range(0,x.Count).All(i=>Equal(x[i],y[i]));}
    if(a is float f)return BitConverter.SingleToInt32Bits(f)==BitConverter.SingleToInt32Bits((float)b);
    return a.Equals(b);
}
object Parse(string text,bool pooled,out Type error)
{
    FastJsonParser.PoolStrings=pooled;
    try{error=null;return FastJsonParser.Parse(text);}catch(Exception e){error=e.GetType();return null;}
}
FastJsonParser.ArrayLimit=10000;
string[] cases={"{}","[]","{\"tag\":\"hello\",\"eventType\":\"MoveDecorations\"}","[\"tag\",\"t\\u0061g\",\"tag\"]","{\"a\":1,\"a\":2}","[1,1.2,-0.0,true,false,null]","{\"한글\":\"한글\"}","\"unterminated","{", "[", "[ : ]", "[\"\\uQxxx\"]", "[\"\\u000", "{\"a\":\"\\s\"}"};
foreach(string text in cases){var a=Parse(text,false,out Type e1);var b=Parse(text,true,out Type e2);Check(e1==e2&&Equal(a,b),"Type/value/order/exception parity");}
var builder=new StringBuilder("[");for(int i=0;i<8000;i++){if(i>0)builder.Append(',');builder.Append("{\"floor\":1,\"eventType\":\"MoveDecorations\",\"tag\":\"target\",\"duration\":0,\"ease\":\"Linear\"}");}builder.Append(']');string bulk=builder.ToString();
var plain=Parse(bulk,false,out var ep);var pooled=Parse(bulk,true,out var eq);Check(ep==eq&&Equal(plain,pooled),"Repeated event tree parity");
var strings=(List<object>)Parse("[\"target\",\"target\",\"t\\u0061rget\"]",true,out _);Check(ReferenceEquals(strings[0],strings[1])&&ReferenceEquals(strings[0],strings[2]),"Same strings reused across raw/escaped paths");
using(var pool=new JsonStringPool()){for(int i=0;i<20000;i++){string k="tag-"+i;Check(pool.Get(k,0,k.Length)==k,"Capacity/collision preserves data");}}
long Allocation(bool mode){FastJsonParser.PoolStrings=mode;long start=GC.GetAllocatedBytesForCurrentThread();GC.KeepAlive(FastJsonParser.Parse(bulk));return GC.GetAllocatedBytesForCurrentThread()-start;}
Allocation(false);Allocation(true);long off=Allocation(false),on=Allocation(true);Check(on<off,"Repeated string allocations reduced");
Console.WriteLine($"JSON pool checks {tests}, errors 0, allocated off {off}, on {on}");
