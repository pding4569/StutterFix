using System;
using System.Collections.Generic;
using StutterFix;
int n=0;
void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
var shared=new List<int>{3,4};var source=new Dictionary<string,List<int>>(StringComparer.OrdinalIgnoreCase){{"a",new List<int>{1,2}},{"b",shared},{"null",null}};
var t=new CompiledTagTable<int>(source);var tags=new List<string>{"A","b","a","absent","null"};var ids=t.Compile(tags);
Check(ids[0]==ids[2],"source comparer retained");Check(ids[3]==-1&&ids[4]>=0&&t.Rows[ids[4]]==null,"absent vs present null");
Check(ReferenceEquals(t.Rows[ids[1]],shared),"live list retained");shared.Add(5);Check(t.Rows[ids[1]].Count==3,"list content mutations visible");
var original=new List<int>();var compiled=new List<int>();
foreach(var tag in new[]{"a","b","a"})original.AddRange(source[tag]);foreach(var id in t.Compile(new List<string>{"a","b","a"}))compiled.AddRange(t.Rows[id]);
Check(string.Join(",",original)==string.Join(",",compiled),"order and duplicate targets preserved");
bool threw=false;try{t.Compile(new List<string>{null});}catch(ArgumentException){threw=true;}Check(threw,"null tags use original path");
using(var store=new PrimitiveIdStore(16384))
{
    Check(t.CompileNative(tags,store,out var native),"native plan compiled");
    for(int i=0;i<ids.Length;i++)Check(native.TryGet(i,out int id)&&id==ids[i],"native lookup same order/value "+i);
    Check(!native.TryGet(-1,out _)&&!native.TryGet(ids.Length,out _),"slice bounds rejected");
    int used=store.Used;
    Check(!t.CompileNative(new List<string>{null},store,out _)&&store.Used==used,"null tag leaves allocation unchanged");
    Check(store.TryAllocate(0,out var empty)&&empty.Count==0&&!empty.TryGet(0,out _),"empty plan safe");
    Check(!store.TryAllocate(int.MaxValue,out _)&&!store.TryAllocate(-1,out _),"overflow and negative allocation rejected");
    for(int k=0;k<2000;k++)
    {
        var order=new List<string>{"b","absent","A",k%2==0?"a":"null"};
        int[] expected=t.Compile(order);Check(t.CompileNative(order,store,out var slice),"disjoint slice allocated");
        for(int i=0;i<order.Count;i++)Check(slice.TryGet(i,out int id)&&id==expected[i],"disjoint slice value");
    }
    for(int i=0;i<ids.Length;i++)Check(native.TryGet(i,out int id)&&id==ids[i],"old slice not overwritten");
    bool foreignRead=true,foreignDispose=false;
    var thread=new System.Threading.Thread(()=>
    {
        foreignRead=native.TryGet(0,out _);
        try{store.Dispose();}catch(InvalidOperationException){foreignDispose=true;}
    });thread.Start();thread.Join();
    Check(!foreignRead&&foreignDispose&&store.Active,"foreign thread cannot read/free owned buffer");
    store.Dispose();Check(!native.TryGet(0,out _)&&!store.Active,"old slice cannot read freed memory");
    Check(!store.TryAllocate(1,out _),"disposed allocation rejected");
}
using(var small=new PrimitiveIdStore(1))
{
    Check(small.TryAllocate(1,out var one)&&one.TrySet(0,-1),"one value fits exact capacity");
    Check(!small.TryAllocate(1,out _),"capacity exhaustion safe");
    Check(one.TryGet(0,out int id)&&id==-1,"failed allocation preserves live slice");
}
bool capacityRejected=false;try{new PrimitiveIdStore(0);}catch(ArgumentOutOfRangeException){capacityRejected=true;}
Check(capacityRejected,"invalid store capacity rejected");
Console.WriteLine($"Tag table/native primitive lifetime checks {n}, errors 0; runtime version capability and game performance separately required");
