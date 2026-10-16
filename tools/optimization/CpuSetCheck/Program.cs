using System;
using StutterFix;
int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
byte[] Row(int index,int efficiency,int flags=0,int group=0){var b=new byte[32];BitConverter.GetBytes(32).CopyTo(b,0);BitConverter.GetBytes((ushort)group).CopyTo(b,12);b[14]=(byte)index;b[18]=(byte)efficiency;b[19]=(byte)flags;return b;}
byte[] Join(params byte[][] rows){var b=new byte[rows.Length*32];for(int i=0;i<rows.Length;i++)rows[i].CopyTo(b,i*32);return b;}
bool hybrid;
Check(CpuSetPolicy.PerformanceMask(Join(Row(0,0),Row(1,0)),3,out hybrid)==0&&!hybrid,"homogeneous keeps OS placement");
var data=Join(Row(0,0),Row(1,2),Row(2,2));
Check(CpuSetPolicy.PerformanceMask(data,7,out hybrid)==6&&hybrid,"higher class selected");
Check(CpuSetPolicy.PerformanceMask(data,3,out hybrid)==2,"existing affinity preserved");
Check(CpuSetPolicy.PerformanceMask(data,1,out hybrid)==0,"no eligible P core keeps original");
Check(CpuSetPolicy.PerformanceMask(Join(Row(0,0),Row(1,2,2),Row(2,2,6)),7,out hybrid)==4,"foreign allocation excluded");
Check(CpuSetPolicy.PerformanceMask(Join(Row(0,0),Row(1,2,1)),3,out hybrid)==0,"parked excluded");
Check(CpuSetPolicy.PerformanceMask(Join(Row(0,0),Row(1,2,0,1)),3,out hybrid)==0,"multi group rejected");
Check(CpuSetPolicy.PerformanceMask(Join(Row(0,0),Row(64,2)),3,out hybrid)==0,"oversized core index rejected");
Check(CpuSetPolicy.PerformanceMask(new byte[32],3,out hybrid)==0,"zero sized record rejected");
Check(CpuSetPolicy.PerformanceMask(new byte[7],3,out hybrid)==0,"short record rejected");
var bad=Row(0,0);bad[0]=64;Check(CpuSetPolicy.PerformanceMask(bad,1,out hybrid)==0,"truncated record rejected");
Check(CpuSetPolicy.SchedulingMask(null,63,false,false,out hybrid)==0,"default off does not request affinity");
Check(CpuSetPolicy.SchedulingMask(null,63,false,true,out hybrid)==62&&!hybrid,"logical zero excluded on homogeneous CPU");
Check(CpuSetPolicy.SchedulingMask(null,1,false,true,out hybrid)==0,"single eligible CPU keeps placement");
Check(CpuSetPolicy.SchedulingMask(null,0,false,true,out hybrid)==0,"empty affinity keeps placement");
Check(CpuSetPolicy.SchedulingMask(null,0x8000000000000001UL,false,true,out hybrid)==0x8000000000000000UL,"logical 63 preserved");
Check(CpuSetPolicy.SchedulingMask(data,7,true,true,out hybrid)==6&&hybrid,"hybrid intersection preserved");
Check(CpuSetPolicy.SchedulingMask(Join(Row(0,2),Row(1,0)),3,true,true,out hybrid)==0,"no P core after exclusion keeps placement");
Check(CpuSetPolicy.SchedulingMask(Join(Row(0,2),Row(1,2),Row(2,0)),7,true,true,out hybrid)==2,"P core zero removed");
Check(CpuSetPolicy.SchedulingMask(Join(Row(0,0),Row(1,0)),3,true,true,out hybrid)==0&&!hybrid,"unknown P core class not invented");
Check(CpuSetPolicy.SchedulingMask(new byte[7],7,true,true,out hybrid)==0,"malformed topology does not invent P cores");
for(int bits=1;bits<=64;bits++)
{
    ulong allowed=bits==64?ulong.MaxValue:(1UL<<bits)-1;
    ulong mask=CpuSetPolicy.SchedulingMask(null,allowed,false,true,out hybrid);
    Check((mask&1)==0&&(mask&~allowed)==0,"exclusion remains subset "+bits);
}
Console.WriteLine($"CPU set conditions {n}, errors 0; actual hybrid hardware and rendering thread unverified");
