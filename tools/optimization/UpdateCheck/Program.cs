using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StutterFix;
int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
byte[] oldDll={1,2,3},oldInfo={4,5,6},newDll={7,8,9},newInfo={10,11,12};
string prefix="https://github.com/pding4569/StutterFix/releases/download/",name="StutterFix-9.0.0-player.zip",sha=new string('a',64);
string Asset(string url,string digest)=>"{\"assets\":[{\"name\":\""+name+"\",\"browser_download_url\":\""+url+"\",\"digest\":\""+digest+"\"}]}";
Check(ReleaseAssetDigest.TryFind(Asset(prefix+"v9/"+name,"sha256:"+sha),name,prefix,out var url,out var found)&&found==sha,"associated digest");
Check(!ReleaseAssetDigest.TryFind(Asset("https://elsewhere/"+name,"sha256:"+sha),name,prefix,out _,out _),"foreign host rejected");
Check(!ReleaseAssetDigest.TryFind(Asset(prefix+"v9/"+name,"sha256:bad"),name,prefix,out _,out _),"invalid hash rejected");
Check(!ReleaseAssetDigest.TryFind("{}",name,prefix,out _,out _),"missing digest not trusted");
bool failed=false;try{UpdateTransaction.VerifyPackage(newDll,sha);}catch(InvalidDataException){failed=true;}Check(failed,"incorrect package hash rejected");
UpdateTransaction.VerifyPackage(newDll,UpdateTransaction.Hash(newDll));Check(true,"correct package hash");
Fake Files(){var f=new Fake();f.Data["StutterFix.dll"]=oldDll;f.Data["Info.json"]=oldInfo;return f;}
var good=Files();UpdateTransaction.Apply("",newDll,newInfo,good);Check(good.Data["StutterFix.dll"].SequenceEqual(newDll)&&good.Data["Info.json"].SequenceEqual(newInfo),"successful install");Check(good.Data["StutterFix.dll.bak"].SequenceEqual(oldDll)&&good.Data["Info.json.bak"].SequenceEqual(oldInfo),"exact backups");
foreach(var fault in new[]{"stage","checksum","validate","dll-before","dll-after","info-before","info-after","cancel-after-dll"})
{
    var f=Files();f.Fault=fault;bool error=false;
    try{UpdateTransaction.Apply("",newDll,newInfo,f,fault=="validate"?_=>throw new BadImageFormatException():null,()=>fault=="cancel-after-dll"&&f.DllSwapped);}catch{error=true;}
    Check(error&&f.Data["StutterFix.dll"].SequenceEqual(oldDll)&&f.Data["Info.json"].SequenceEqual(oldInfo),"rollback "+fault);
    Check(!f.Data.Keys.Any(k=>k.Contains(".new-")),"own stage cleanup "+fault);
}
var both=Files();both.Fault="rollback-dll";failed=false;try{UpdateTransaction.Apply("",newDll,newInfo,both);}catch(AggregateException){failed=true;}
Check(failed&&both.Data["StutterFix.dll.bak"].SequenceEqual(oldDll)&&both.Data["Info.json"].SequenceEqual(oldInfo),"rollback failure retains backup and attempts other restore");
Console.WriteLine($"Update transaction/digest checks {checks}, errors 0; fake storage faults, physical filesystem checked separately");

sealed class Fake:UpdateTransaction.IFiles
{
    internal Dictionary<string,byte[]> Data=new Dictionary<string,byte[]>(StringComparer.Ordinal);
    internal string Fault="";internal bool DllSwapped;
    public bool Exists(string p)=>Data.ContainsKey(p);
    public byte[] Read(string p)=>Data[p].ToArray();
    public void Write(string p,byte[] b){if(Fault=="stage"&&p.StartsWith("Info.json.new-"))throw new IOException();if(Data.ContainsKey(p))throw new IOException("exists");Data[p]=b.ToArray();if(Fault=="checksum"&&p.StartsWith("StutterFix.dll.new-"))Data[p][0]^=1;}
    public void Replace(string stage,string dest,string backup)
    {
        bool restore=stage.EndsWith("-restore");
        if(!restore&&dest=="StutterFix.dll"&&Fault=="dll-before")throw new IOException();
        if(!restore&&dest=="Info.json"&&(Fault=="info-before"||Fault=="rollback-dll"))throw new IOException();
        if(restore&&dest=="StutterFix.dll"&&Fault=="rollback-dll")throw new IOException();
        if(backup!=null)Data[backup]=Data[dest].ToArray();Data[dest]=Data[stage];Data.Remove(stage);
        if(dest=="StutterFix.dll"&&!restore)DllSwapped=true;
        if(!restore&&dest=="StutterFix.dll"&&Fault=="dll-after")throw new IOException();
        if(!restore&&dest=="Info.json"&&Fault=="info-after")throw new IOException();
    }
    public void Move(string stage,string dest){if(Data.ContainsKey(dest))throw new IOException();Data[dest]=Data[stage];Data.Remove(stage);}
    public void Delete(string p){Data.Remove(p);}
}
