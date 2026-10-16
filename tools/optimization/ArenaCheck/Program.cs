using System;
using System.Runtime.InteropServices;
using StutterFix;
int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
using(var a=new AlignedArena(1024))
{
 Check((a.Pointer.ToInt64()&63)==0,"base alignment");
 var first=a.Allocate(3);var second=a.Allocate(10);
 Check(second.ToInt64()-first.ToInt64()==64&&(second.ToInt64()&63)==0,"slice alignment and gap");
 Marshal.WriteByte(first,123);Marshal.WriteByte(second,45);Check(Marshal.ReadByte(first)==123&&Marshal.ReadByte(second)==45,"independent slice data");
 Check(a.Allocate(1024)==IntPtr.Zero&&a.Used==74,"failed allocation preserves cursor");
 a.Reset();Check(a.Used==0&&a.Allocate(1024)==first,"constant time reset and full reuse");
 Check(a.Allocate(1)==IntPtr.Zero,"full capacity boundary");
 a.Dispose();a.Dispose();bool closed=false;try{a.Allocate(1);}catch(ObjectDisposedException){closed=true;}Check(closed,"double free prevented and use after dispose rejected");
}
using(var a=new AlignedArena(65536*5*4))for(int i=0;i<5;i++)Check(a.Allocate(65536*4)!=IntPtr.Zero,"five motion columns fit");
bool bad=false;try{using var a=new AlignedArena(int.MaxValue);}catch(ArgumentOutOfRangeException){bad=true;}Check(bad,"oversized allocation rejected");
IntPtr nativeBase=IntPtr.Zero,passed=IntPtr.Zero;int frees=0;
using(var a=new AlignedArena(256,bytes=>{nativeBase=Marshal.AllocHGlobal(bytes+63);return new IntPtr((nativeBase.ToInt64()+63)&~63L);},p=>{passed=p;frees++;Marshal.FreeHGlobal(nativeBase);}))
{Check(a.NativeAllocated&&(a.Pointer.ToInt64()&63)==0,"native allocator selected");a.Dispose();a.Dispose();}
Check(frees==1&&passed!=IntPtr.Zero,"native allocation uses matching free once");
using(var a=new AlignedArena(128,bytes=>throw new EntryPointNotFoundException(),p=>throw new Exception("must not free fallback with native")))
 Check(!a.NativeAllocated&&a.Allocate(128)!=IntPtr.Zero,"missing allocator safely falls back");
Console.WriteLine($"Aligned arena conditions {n}, errors 0; game native arithmetic needs actual runtime comparison");
