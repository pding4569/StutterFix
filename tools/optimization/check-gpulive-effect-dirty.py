"""Compile actual MarkDirty/MarkEffectDirty source in a Unity-free fixture.

Only dirty bookkeeping is tested; no claim about game rendering or Mono cost.
Run: py -3 tools/optimization/check-gpulive-effect-dirty.py
"""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
source = (root / 'GpuDecoExport.cs').read_text(encoding='utf-8-sig')
methods = source[source.index('            internal void MarkDirty(int slot)'):source.index('            // Native packet writers expose dirty blocks')]
dest = root / 'out' / 'gpulive-effect-dirty-fixture'
dest.mkdir(parents=True, exist_ok=True)
(dest / 'Fixture.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>', encoding='utf-8')
prefix = '''using System;
using System.Linq;
using System.Collections.Generic;
class Fixture {
 static bool gpuZeroAlpha,gpuRenderMergePrepared,gpuCollapsePrepare;
 bool[] alphaDirty=new bool[32],DirtyBlocks=new bool[2];
 int[] dirtyFirst=new int[2],dirtyEnd=new int[2];
 int DirtyBlockMin=int.MaxValue,DirtyBlockMax=-1;
 List<int> pendingAlpha=new List<int>();
'''
suffix = '''
 static Fixture Seed(int state,int item) {
  var a=new Fixture();
  if(state!=0){a.MarkDirty(item*16+3);a.MarkDirty((31-item)*16+8);}
  if(state==2)a.MarkDirty(item*16+10);
  return a;
 }
 static void Main(){long cases=0;
  for(int flags=0;flags<8;flags++) {
   gpuZeroAlpha=(flags&1)!=0;gpuRenderMergePrepared=(flags&2)!=0;gpuCollapsePrepare=(flags&4)!=0;
   foreach(int item in new[]{0,15,16,31})for(int state=0;state<3;state++)for(int mask=1;mask<65536;mask++){
    var a=Seed(state,item);var b=Seed(state,item);
    for(int c=0;c<16;c++)if((mask&(1<<c))!=0)a.MarkDirty(item*16+c);
    b.MarkEffectDirty(item,mask);
    if(!a.alphaDirty.SequenceEqual(b.alphaDirty)||!a.DirtyBlocks.SequenceEqual(b.DirtyBlocks)
     ||!a.dirtyFirst.SequenceEqual(b.dirtyFirst)||!a.dirtyEnd.SequenceEqual(b.dirtyEnd)
     ||!a.pendingAlpha.SequenceEqual(b.pendingAlpha)||a.DirtyBlockMin!=b.DirtyBlockMin||a.DirtyBlockMax!=b.DirtyBlockMax)
     throw new Exception($"mismatch flags={flags} item={item} state={state} mask={mask}");
    cases++;
   }
  }
  Console.WriteLine($"actual_source_dirty_fixture cases={cases} mismatch=0 runtime=net8 performance=False");
 }
}
'''
(dest / 'Program.cs').write_text(prefix + methods + suffix, encoding='utf-8')
subprocess.run(['dotnet', 'run', '--project', str(dest / 'Fixture.csproj'), '-c', 'Release', '--nologo'], cwd=root, check=True)
