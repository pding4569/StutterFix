using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

// Read the actual tester artifact, without starting Unity or installing anything.
if(args.Length!=3) throw new ArgumentException("TesterCheck <zip> <repo> <game Managed>");
string zipPath=Path.GetFullPath(args[0]),repo=Path.GetFullPath(args[1]),managed=Path.GetFullPath(args[2]);
AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{
    string name=new AssemblyName(e.Name).Name+".dll";
    foreach(string dir in new[]{managed,Path.Combine(managed,"UnityModManager")}) {
        string path=Path.Combine(dir,name);if(File.Exists(path))return Assembly.LoadFrom(path);
    }
    return null;
};
void Check(bool valid,string reason){if(!valid)throw new Exception(reason);}
string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
byte[] Read(ZipArchiveEntry entry){using var m=new MemoryStream();using var s=entry.Open();s.CopyTo(m);return m.ToArray();}
using var zip=ZipFile.OpenRead(zipPath);
string[] entries=zip.Entries.Select(e=>e.FullName).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
Check(entries.SequenceEqual(new[]{"StutterFix/Info.json","StutterFix/StutterFix.dll","StutterFix/effects-LICENSE.txt"}),"Unexpected package entries");
Check(Read(zip.GetEntry("StutterFix/effects-LICENSE.txt")).SequenceEqual(File.ReadAllBytes(Path.Combine(repo,"effects/license.txt"))),"Shader license mismatch");
byte[] dll=Read(zip.GetEntry("StutterFix/StutterFix.dll"));
Check(dll.SequenceEqual(File.ReadAllBytes(Path.Combine(repo,"bin/Player/StutterFix.dll"))),"ZIP DLL differs from Player build");
using var info=JsonDocument.Parse(Read(zip.GetEntry("StutterFix/Info.json")));
Check(info.RootElement.GetProperty("DisplayName").GetString().Contains("테스터"),"Missing tester label");
// Edition.cs intentionally compiles diagnostic types into both editions.
// Presence of old command strings does not prove that AutoTest is enabled.
var asm=Assembly.Load(dll);
var main=asm.GetType("StutterFix.Main",true);
Check(!(bool)main.GetField("MeasureBuild",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue(),"Measurement build");
Check(!(bool)main.GetField("AutoTestBuild",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue(),"AutoTest build");
var edition=asm.GetType("StutterFix.Edition",true);
Check(!(bool)edition.GetField("AutoTest",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue(),"AutoTest enabled");
Check(!(bool)edition.GetField("Dev",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue(),"Developer edition");
Check(asm.GetType("StutterFix.SettingsWindow",true).GetMethod("InputFxForTest",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Instance)==null,"Test input hook included");
Check(asm.GetType("StutterFix.PerfOverlay",true).GetMethod("TestFreezeOutputSample",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Instance)==null,"Output counter test hook included");
var type=asm.GetType("StutterFix.Settings",true);
object settings=Activator.CreateInstance(type);
object Value(object o,string field)=>type.GetField(field).GetValue(o);
Check((int)Value(settings,"FrameGenOutside")==0,"FrameGen enabled by default");
Check((bool)Value(settings,"FrameGenRefresh"),"Refresh is not the initial choice");
Check((bool)Value(settings,"FrameGenRefreshRest"),"Rest initial value false");
Check((int)Value(settings,"OverlayFpsSource")==-1,"Monitor default changed");
string[] fx={"FxColor","FxSharp","FxAA","FxGlow","FxVignette","FxLut","FxLight","FxGlowStack","FxToneMap","FxRays","FxStreak","FxFlare","FxChromatic","FxGrain","FxCrt","FxPixel","FxPosterize","FxBlur"};
Check((int)Value(settings,"FxPreset")==0 && fx.All(n=>!(bool)Value(settings,n)),"Effects enabled by default");
var serializer=new XmlSerializer(type);
object Xml(string value){using var r=new StringReader("<Settings>"+value+"</Settings>");return serializer.Deserialize(r);}
var old=Xml("<FrameGenOutside>4</FrameGenOutside><FrameGenMultiplier>4</FrameGenMultiplier><FrameGenRefresh>false</FrameGenRefresh><FrameGenRefreshRest>false</FrameGenRefreshRest>");
Check(!(bool)Value(old,"FrameGenRefresh")&&(int)Value(old,"FrameGenOutside")==4&&(int)Value(old,"FrameGenMultiplier")==4,"Saved fixed multiplier changed");
var minimal=Xml("");Check((bool)Value(minimal,"FrameGenRefresh")&&(int)Value(minimal,"FrameGenOutside")==0,"Missing XML fields lost new safe defaults");
var resources=new Dictionary<string,string>();
foreach(var pair in new[]{("StutterFix.sfnative.dll","native/sfnative.dll"),("StutterFix.effects","effects/stutterfix_effects"),("StutterFix.fsr","fsr/stutterfix_fsr")}) {
    using var stream=asm.GetManifestResourceStream(pair.Item1)??throw new Exception("Missing embedded "+pair.Item1);
    using var memory=new MemoryStream();stream.CopyTo(memory);byte[] bytes=memory.ToArray();
    Check(bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(repo,pair.Item2))),"Embedded resource mismatch "+pair.Item1);
    resources[pair.Item1]=Sha(bytes);
}
var runtime=asm.GetType("StutterFix.FrameGenRuntime",true);
Check((int)runtime.GetField("FixedCostStage",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue()==1,"Unapproved fixed-cost stage");
Check((int)runtime.GetField("BlockVariant",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue()==3,"Unexpected block variant");
Console.WriteLine(JsonSerializer.Serialize(new{zip_sha256=Sha(File.ReadAllBytes(zipPath)),managed_sha256=Sha(dll),version=info.RootElement.GetProperty("Version").GetString(),entries,defaults=new{framegen=false,refresh=true,rest=true,effects=false,fx_off_count=fx.Length,monitor=-1},saved_fixed_multiplier_preserved=true,missing_xml_defaults=true,measurement=false,autotest=false,resources,stage=1,variant=3,errors=0},new JsonSerializerOptions{WriteIndented=true}));
