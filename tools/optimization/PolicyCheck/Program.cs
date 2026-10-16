using System;
using StutterFix;
int checks=0;
void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
foreach(long total in new long[]{8192,16384,24576,65536})
{
    int previous=0;
    foreach(long free in new long[]{0,256,1024,total/2})
    {
        int budget=MemoryBudgetPolicy.Calculate(6000,total,free,4096,2048);
        Check(budget>=256,"Positive bound");
        Check(budget<=Math.Max(256,total*7/10-2048),"Native resident budget");
        Check(budget>=previous,"Available RAM monotonicity");previous=budget;
    }
    Check(MemoryBudgetPolicy.Calculate(6000,total,1024,8000,2048)<=MemoryBudgetPolicy.Calculate(6000,total,1024,4096,2048),"Native pressure lowers budget");
}
Check(MemoryBudgetPolicy.Calculate(6000,0,0,0,0)==6000,"Unknown RAM fallback");
Check(MemoryBudgetPolicy.Calculate(6000,8192,-1,0,0)==3276,"Invalid snapshot legacy fallback");
Check(MemoryBudgetPolicy.Calculate(6000,long.MaxValue,0,0,0)==6000,"Overflow-safe fallback");
Console.WriteLine($"Memory policy checks {checks}, errors 0");
