using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

var names=new HashSet<string>("AutoTest AllocProbe TimeProbe FrameScan HeapPath SceneScan FloorProf FilterGpuProbe FilterHalfProbe FilterHalfPerf FreeFpsProbe ScriptProbe FilterTrace MergeProbe RenderVerify UiProf RenderCallbackScan FrameRateScreenWatch ParticleTextWatch BlendProbe StartProbe UpdateFixture".Split(' '));
var directory=Path.GetFullPath(args[0]);bool apply=args.Contains("--apply");int total=0;
bool Guarded(string text,int position)
{
    var stack=new List<bool>();
    foreach(string raw in text.Substring(0,position).Split('\n'))
    {
        string line=raw.Trim();
        if(line.StartsWith("#if ")){string c=line.Substring(4).Replace(" ","");stack.Add(c=="DEV"||c=="AUTOTEST"||c=="DEV||AUTOTEST"||c=="AUTOTEST||DEV");}
        else if(line.StartsWith("#else")||line.StartsWith("#elif")){if(stack.Count>0)stack[stack.Count-1]=false;}
        else if(line.StartsWith("#endif")){if(stack.Count>0)stack.RemoveAt(stack.Count-1);}
    }
    return stack.Any(v=>v);
}
foreach(var path in Directory.EnumerateFiles(directory,"*.cs"))
{
    if(names.Contains(Path.GetFileNameWithoutExtension(path)))continue;
    string source=File.ReadAllText(path);var tree=CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.Latest,preprocessorSymbols:new[]{"DEV","AUTOTEST"}));
    var candidates=new Dictionary<TextSpan,StatementSyntax>();
    foreach(var id in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
    {
        if(!names.Contains(id.Identifier.ValueText))continue;
        if(id.Parent is MemberAccessExpressionSyntax member && member.Name==id)continue;
        if(Guarded(source,id.SpanStart))continue;
        var statement=id.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if(statement==null)throw new Exception("Manual guard required: "+path+":"+id.GetLocation().GetLineSpan().StartLinePosition);
        if(statement is ReturnStatementSyntax)throw new Exception("Manual return guard required: "+path+":"+id.GetLocation().GetLineSpan().StartLinePosition);
        if(statement is IfStatementSyntax branch && branch.Else!=null)throw new Exception("Preserve else branch manually: "+path);
        candidates[statement.Span]=statement;
    }
    var spans=candidates.Where(p=>!candidates.Keys.Any(s=>s!=p.Key&&s.Contains(p.Key))).OrderByDescending(p=>p.Key.Start).ToArray();
    if(spans.Length==0)continue;
    Console.WriteLine(Path.GetFileName(path)+": "+spans.Length+" diagnostic statements");total+=spans.Length;
    foreach(var p in spans)
    {
        bool needsBlock=!(p.Value.Parent is BlockSyntax || p.Value.Parent is SwitchSectionSyntax);
        string original=source.Substring(p.Key.Start,p.Key.Length);
        string replacement=(needsBlock?"{\n":"\n")+"#if DEV || AUTOTEST\n"+original+"\n#endif\n"+(needsBlock?"}":"");
        source=source.Remove(p.Key.Start,p.Key.Length).Insert(p.Key.Start,replacement);
    }
    if(apply)File.WriteAllText(path,string.Join("\n",source.Split('\n').Select(line=>line.TrimEnd())).TrimEnd()+"\n",new UTF8Encoding(false,true));
}
Console.WriteLine($"{total} guards; apply={apply}");
