"""Test actual ScreenPrefs logic with in-memory registry/PlayerPrefs substitutes."""
from pathlib import Path
import re
import subprocess
root=Path(__file__).resolve().parents[2]
source=(root/'PresentPath.cs').read_text(encoding='utf-8-sig')
start=source.index('        sealed class ScreenPrefs')
end=source.index('        static void Log',start)
prefs=re.sub(r'^\s*\[DllImport[^\n]+\n','\n',source[start:end],flags=re.M)
fake_api=r'''
            static int RegOpenKeyEx(IntPtr r,string p,int o,int a,out IntPtr k){k=(IntPtr)1;return 0;}
            static int RegCloseKey(IntPtr k){return 0;}
            static int RegEnumValue(IntPtr k,uint i,StringBuilder name,ref uint length,IntPtr reserved,out uint type,byte[] data,ref uint size){
                type=4;if(i>=raw.Count)return 259;string n=raw.Keys.ElementAt((int)i);
                name.Append(n);length=(uint)n.Length;type=n==badType?1u:4u;size=4;Array.Copy(raw[n],data,4);return 0;
            }
            static int RegSetValueEx(IntPtr k,string n,int r,uint type,byte[] data,int size){raw[n]=(byte[])data.Clone();return 0;}
            static int RegDeleteValue(IntPtr k,string n){raw.Remove(n);return 0;}
'''
prefs=prefs.replace('readonly string path=',fake_api+'            readonly string path=')
prefix=r'''
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
class Test {
    static Dictionary<string,byte[]> raw;
    static Dictionary<string,int> cache;
    static string badType;
    class Application {internal static string companyName="fake",productName="fake";}
    class PlayerPrefs {
        static HashSet<string> deleted=new HashSet<string>();
        internal static bool HasKey(string n)=>cache.ContainsKey(n);
        internal static int GetInt(string n)=>cache[n];
        internal static void SetInt(string n,int v){cache[n]=v;deleted.Remove(n);}
        internal static void DeleteKey(string n){cache.Remove(n);deleted.Add(n);}
        internal static void Save(){foreach(var n in deleted)raw.Remove(n+"_h123");deleted.Clear();foreach(var p in cache)raw[p.Key+"_h123"]=BitConverter.GetBytes(p.Value);}
    }
'''
suffix=r'''
    static void Expect(bool b,string s){if(!b)throw new Exception(s);}
    static void Main(){
        raw=new Dictionary<string,byte[]>{{"Screenmanager Width_h123",BitConverter.GetBytes(1280)},{"unrelated_h123",BitConverter.GetBytes(7)}};
        cache=new Dictionary<string,int>{{"Screenmanager Width",1280},{"unrelated",7}};
        var snapshot=new ScreenPrefs();Expect(snapshot.Count==1,"snapshot filtering");
        PlayerPrefs.SetInt("Screenmanager Width",1279);
        PlayerPrefs.SetInt("Screenmanager NEW",99); // cache-only value not yet in registry
        snapshot.Restore();
        Expect(raw.Count==2 && BitConverter.ToInt32(raw["Screenmanager Width_h123"],0)==1280,"exact registry restore");
        Expect(!cache.ContainsKey("Screenmanager NEW"),"new cache key removed");
        PlayerPrefs.Save();Expect(raw.Count==2 && !raw.ContainsKey("Screenmanager NEW_h123"),"later Save must not resurrect new keys");
        Expect(BitConverter.ToInt32(raw["unrelated_h123"],0)==7,"unrelated preferences preserved");
        snapshot.Restore();Expect(raw.Count==2,"idempotent restore");
        badType="Screenmanager Width_h123";bool rejected=false;
        try {new ScreenPrefs();}catch(Exception){rejected=true;}
        Expect(rejected,"unknown registry type rejected before resolution request");
        Console.WriteLine("PASS actual ScreenPrefs: raw value restore; cache-only new key deletion; later Save stability; unrelated key preservation; idempotence; non-DWORD preflight refusal");
    }
}
'''
out=root/'out/present-buffers/prefs-fixture'
out.mkdir(parents=True,exist_ok=True)
(out/'Prefs.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Program.cs" /></ItemGroup></Project>')
(out/'Program.cs').write_text(prefix+prefs+suffix,encoding='utf-8')
r=subprocess.run(['dotnet','run','--project',str(out/'Prefs.csproj'),'-c','Release'],cwd=root,capture_output=True,text=True)
(out.parent/'prefs-fixture.txt').write_text(r.stdout+r.stderr,encoding='utf-8')
print(r.stdout+r.stderr)
raise SystemExit(r.returncode)
