using System;
using System.Collections.Generic;
using System.IO;
using StutterFix;
int n=0;
void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
var settings=new FakeSettings();var baseline=RenderProfileValues.Capture(settings);settings.FxContrast=1.75f;settings.LowRenderScale=75;
var current=RenderProfileValues.Capture(settings);RenderProfileValues.WithBaseline(settings,baseline,()=>Check(settings.FxContrast==1&&settings.LowRenderScale==100,"global serialized baseline"));
Check(settings.FxContrast==1.75f&&settings.LowRenderScale==75,"runtime restored after serialization");
bool error=false;try{RenderProfileValues.WithBaseline(settings,baseline,()=>throw new IOException());}catch(IOException){error=true;}
Check(error&&settings.FxContrast==1.75f,"runtime restored on save failure");
RenderProfileValues.Apply(settings,new Dictionary<string,object>{{"GlobalInput",false},{"FxContrast",float.NaN},{"LowRenderScale","wrong"},{"FxFile","foreign"}});
Check(settings.GlobalInput&&settings.FxContrast==1.75f&&settings.LowRenderScale==75&&settings.FxFile=="global-file","unowned or invalid data ignored");
string root=Path.GetFullPath(args[0]);if(Directory.Exists(root))throw new Exception("Use a new fixture directory");Directory.CreateDirectory(root);
var store=new MapProfileStore(root);string key=new string('a',64),other=new string('b',64);
Check(store.Write(key,current),"atomic profile write");var read=store.Read(key);Check((float)read["FxContrast"]==1.75f&&(int)read["LowRenderScale"]==75,"typed roundtrip");
Check(store.Read(other)==null,"other map has no profile");Check(!store.Write("../escape",current)&&store.Read("../escape")==null,"key cannot escape");
Check(store.Write(key,baseline),"profile replacement and backup");string file=Path.Combine(root,key+".sfp");var raw=File.ReadAllBytes(file);raw[raw.Length-1]^=1;File.WriteAllBytes(file,raw);
Check((float)store.Read(key)["FxContrast"]==1.75f,"corrupt primary uses verified backup");
File.Copy(Path.Combine(root,key+".sfp.bak"),Path.Combine(root,other+".sfp"));Check(store.Read(other)==null,"payload map binding enforced");
string blocked=Path.Combine(root,"file-not-directory");File.WriteAllText(blocked,"keep");Check(!new MapProfileStore(blocked).Write(key,current)&&File.ReadAllText(blocked)=="keep","unavailable storage preserves user file");
Check(Directory.GetFiles(root,"*.new-*").Length==0,"own temporary files cleaned");
Console.WriteLine($"Rendering profile checks {n}, errors 0; game load/switch/save lifecycle separately required");
sealed class FakeSettings
{
    public bool FxColor=true,GlobalInput=true;
    public float FxContrast=1;
    public int LowRenderScale=100,FrameGenOutside=0;
    public string FxFile="global-file";
}
