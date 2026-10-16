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
Console.WriteLine($"Tag table checks {n}, errors 0; runtime version capability separately required");
