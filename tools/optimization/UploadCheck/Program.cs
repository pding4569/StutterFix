using System;
using StutterFix;
int n=0;
void Check(bool b,string name){n++;if(!b)throw new Exception(name);}
var settings=new Fake();var scope=new UploadBudgetScope(settings);
Check(scope.Begin()&&settings.TimeSlice==16&&settings.BufferSize==64,"Apply loading budget");
Check(scope.Begin()&&scope.Restore()&&settings.TimeSlice==3&&settings.BufferSize==8,"Repeated begin preserves original");
Check(scope.Restore(),"Repeated restore harmless");
settings.FailBuffer=true;Check(!scope.Begin()&&!scope.Active&&settings.TimeSlice==3&&settings.BufferSize==8,"Partial apply failure rolled back");
settings.FailBuffer=false;scope.Begin();settings.TimeSlice=9;settings.BufferSize=128;Check(scope.Restore()&&settings.TimeSlice==9&&settings.BufferSize==128,"Other mod settings retained");
settings.TimeSlice=2;settings.BufferSize=4;scope.Begin();settings.FailTimeRestore=true;Check(!scope.Restore()&&scope.Active&&settings.BufferSize==4,"Failed restoration stays owned");settings.FailTimeRestore=false;Check(scope.Restore()&&settings.TimeSlice==2,"Retry pending restoration");
scope.Begin();settings.FailTimeRestore=true;scope.Restore();Check(!scope.Begin()&&settings.BufferSize==4,"Pending partial restore does not report active budget");settings.FailTimeRestore=false;Check(scope.Begin()&&settings.BufferSize==64&&scope.Restore()&&settings.TimeSlice==2&&settings.BufferSize==4,"Begin retries restoration before capturing originals");
Console.WriteLine($"Upload scope checks {n}, errors 0");
sealed class Fake:IUploadSettings
{
    private int time=3,buffer=8;
    internal bool FailBuffer,FailTimeRestore;
    public int TimeSlice{get=>time;set{if(FailTimeRestore&&value==2)throw new Exception();time=value;}}
    public int BufferSize{get=>buffer;set{if(FailBuffer&&value==64)throw new Exception();buffer=value;}}
}
