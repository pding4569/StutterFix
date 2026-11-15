using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
var path=args.Length>0?args[0]:"D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data/Managed/UnityEngine.CoreModule.dll";
var expected=new Dictionary<string,string[]>{
 ["UnityEngine.Rendering.BlendMode"]=new[]{"Zero","One","DstColor","SrcColor","OneMinusDstColor","SrcAlpha","OneMinusSrcColor","DstAlpha","OneMinusDstAlpha","SrcAlphaSaturate","OneMinusSrcAlpha"},
 ["UnityEngine.Rendering.BlendOp"]=new[]{"Add","Subtract","ReverseSubtract","Min","Max"},
 ["UnityEngine.Rendering.CompareFunction"]=new[]{"Disabled","Never","Less","Equal","LessEqual","Greater","NotEqual","GreaterEqual","Always"},
 ["UnityEngine.Rendering.StencilOp"]=new[]{"Keep","Zero","Replace","IncrementSaturate","DecrementSaturate","Invert","IncrementWrap","DecrementWrap"},
 ["UnityEngine.Rendering.CullMode"]=new[]{"Off","Front","Back"},
 ["UnityEngine.Rendering.ColorWriteMask"]=new[]{"Alpha=1","Blue=2","Green=4","Red=8","All=15"},
 ["UnityEngine.FilterMode"]=new[]{"Point","Bilinear","Trilinear"},
 ["UnityEngine.TextureWrapMode"]=new[]{"Repeat","Clamp","Mirror","MirrorOnce"},
 ["UnityEngine.Rendering.VertexAttributeFormat"]=new[]{"Float32","Float16","UNorm8","SNorm8","UNorm16","SNorm16","UInt8","SInt8","UInt16","SInt16","UInt32","SInt32"},
 ["UnityEngine.Rendering.IndexFormat"]=new[]{"UInt16","UInt32"}};
using var file=File.OpenRead(path);using var pe=new PEReader(file);var reader=pe.GetMetadataReader();
var result=new Dictionary<string,Dictionary<string,int>>();
foreach(var handle in reader.TypeDefinitions){var type=reader.GetTypeDefinition(handle);string name=reader.GetString(type.Namespace)+"."+reader.GetString(type.Name);if(!expected.ContainsKey(name))continue;
 var values=new Dictionary<string,int>();foreach(var fieldHandle in type.GetFields()){var field=reader.GetFieldDefinition(fieldHandle);var value=field.GetDefaultValue();if(value.IsNil)continue;var constant=reader.GetConstant(value);if(constant.TypeCode!=ConstantTypeCode.Int32)throw new Exception("Unexpected enum storage "+name);values[reader.GetString(field.Name)]=reader.GetBlobReader(constant.Value).ReadInt32();}result[name]=values;
 foreach(var (entry,index) in expected[name].Select((v,i)=>(v,i))){var parts=entry.Split('=');int n=parts.Length==2?int.Parse(parts[1]):index;if(!values.TryGetValue(parts[0],out int actual)||actual!=n)throw new Exception($"Enum drift {name}.{parts[0]} expected={n} actual={actual}");}}
if(result.Count!=expected.Count)throw new Exception("Missing Unity enum metadata");
Console.WriteLine(JsonSerializer.Serialize(new{assembly=path,types=result.Count,verified=true,values=result},new JsonSerializerOptions{WriteIndented=true}));

