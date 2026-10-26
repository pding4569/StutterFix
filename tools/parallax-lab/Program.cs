using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;

// No game/Unity references, process launches, writes to maps, or recursive map discovery.
static class Program
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly (string Name, string Path)[] Maps = {
        ("Arche", @"D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai"),
        ("HELLO 2026", @"D:\얼불춤 맵 파일\HELLO (BPM) 2026\level.adofai"),
        ("Windflower", @"D:\얼불춤 맵 파일\Windflower\Windflower.adofai")
    };
    static readonly string[] Related = { "parallax", "parallaxOffset", "relativeTo", "lockScale", "lockRotation", "scaleMultiplier", "stickToFloor" };
    static int Main(string[] args)
    {
        try {
            object result = args.FirstOrDefault() switch {
                "stats" => Stats(),
                "bench" => Bench(args.Skip(1).Select(int.Parse).ToArray()),
                "selftest" => SelfTest(),
                _ => throw new ArgumentException("stats | bench [N K frames rounds] | selftest")
            };
            Console.WriteLine(JsonSerializer.Serialize(result, Json));
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static string S(JsonElement e, string key, string fallback="") => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
    static bool B(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
    static bool Enabled(JsonElement e) => !e.TryGetProperty("active", out var v) || v.ValueKind != JsonValueKind.False;
    static bool Has(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null;
    static string Raw(JsonElement e, string k) => e.TryGetProperty(k, out var v) ? v.GetRawText() : "<missing>";
    static string Canon(JsonElement v) => v.ValueKind switch {
        JsonValueKind.Number=>"f:"+Bits(v.GetSingle()),
        JsonValueKind.Array=>"["+string.Join(",",v.EnumerateArray().Select(Canon))+"]",
        _=>v.GetRawText()
    };
    static string Key(JsonElement e,string k)=>e.TryGetProperty(k,out var v)?Canon(v):"<missing>";
    static float F(JsonElement v) => v.ValueKind == JsonValueKind.Number ? v.GetSingle() : float.NaN;
    static (float X,float Y)? Pair(JsonElement e, string k)
    {
        if (!e.TryGetProperty(k, out var v)) return null;
        if (v.ValueKind==JsonValueKind.Number) return (v.GetSingle(),v.GetSingle());
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength()!=2) return null;
        return (F(v[0]),F(v[1]));
    }
    static string Bits(float f) => BitConverter.SingleToUInt32Bits(f).ToString("x8");
    static string PairKey((float X,float Y) p) => Bits(p.X)+":"+Bits(p.Y);
    static IEnumerable<JsonElement> Array(JsonElement e,string k) => e.TryGetProperty(k,out var a) && a.ValueKind==JsonValueKind.Array ? a.EnumerateArray() : Enumerable.Empty<JsonElement>();
    static string[] Tags(JsonElement e)
    {
        var tags=S(e,"tag").Split(' ',StringSplitOptions.RemoveEmptyEntries);
        // Both scrDecoration.Setup and move Decode use NO TAG when empty.
        return tags.Length==0 ? new[]{"NO TAG"} : tags.Distinct(StringComparer.Ordinal).ToArray();
    }
    static object Distribution(IEnumerable<string> keys)
    {
        var groups=keys.GroupBy(x=>x).Select(g=>new { Key=g.Key, Size=g.Count() }).OrderByDescending(g=>g.Size).ThenBy(g=>g.Key).ToArray();
        return new { Items=groups.Sum(g=>g.Size), Unique=groups.Length, Largest=groups.Take(12),
            SizeHistogram=groups.GroupBy(g=>g.Size).OrderByDescending(g=>g.Key).Select(g=>new { Size=g.Key, Groups=g.Count(), Decorations=g.Sum(x=>x.Size) }) };
    }
    static object Stats()
    {
        var reports=new List<object>();
        foreach(var map in Maps) {
            var source=EscapeStringControls(File.ReadAllText(map.Path),out int controls);
            var options=new JsonDocumentOptions { AllowTrailingCommas=true, CommentHandling=JsonCommentHandling.Skip };
            bool repaired=false;
            JsonDocument doc;
            try { doc=JsonDocument.Parse(source,options); }
            catch(JsonException) {
                // Arche omits this single root-property comma. Narrow repair in memory only;
                // require exactly one match, then let the strict parser validate everything else.
                var pattern="(?m)^([ \\t]*\\])([ \\t]*\\r?\\n)([ \\t]*\"decorations\"[ \\t]*:)";
                if(Regex.Matches(source,pattern).Count!=1) throw;
                source=Regex.Replace(source,pattern,"$1,$2$3");repaired=true;
                doc=JsonDocument.Parse(source,options);
            }
            using(doc) reports.Add(new { MissingCommaBeforeDecorationsNormalized=repaired,RawStringControlCharactersEscaped=controls,
                Data=JsonSerializer.SerializeToElement(Analyze(doc.RootElement,map.Name,map.Path,new FileInfo(map.Path).Length,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(map.Path))))) });
        }
        return new { Schema=1, Meaning="Authored JSON census, not runtime timeline or visible/GPU-eligible counts", Maps=reports,
            PeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64 };
    }
    static string EscapeStringControls(string text,out int count)
    {
        bool quoted=false,escaped=false;count=0;var b=new StringBuilder(text.Length);
        foreach(char c in text) {
            if(quoted && c<32) { b.Append("\\u").Append(((int)c).ToString("x4"));count++;escaped=false;continue; }
            b.Append(c);
            if(escaped) {escaped=false;continue;}
            if(quoted&&c=='\\') escaped=true;
            else if(c=='\"') quoted=!quoted;
        }
        return b.ToString();
    }
    static object Analyze(JsonElement root,string name,string path,long bytes,string sha)
    {
        var all=Array(root,"decorations").Concat(Array(root,"actions")).ToArray();
        var adds=all.Where(e=>S(e,"eventType") is "AddDecoration" or "AddText" or "AddObject").ToArray();
        var moves=all.Where(e=>S(e,"eventType")=="MoveDecorations").ToArray();
        var valid=adds.Where(e=>Pair(e,"parallax") is {} p && float.IsFinite(p.X) && float.IsFinite(p.Y)).ToArray();
        var tagIndex=new Dictionary<string,HashSet<int>>(StringComparer.Ordinal);
        for(int i=0;i<adds.Length;i++) foreach(var tag in Tags(adds[i])) {
            if(!tagIndex.TryGetValue(tag,out var ids)) tagIndex[tag]=ids=new(); ids.Add(i);
        }
        var touched=new HashSet<int>(); var offsetTouched=new HashSet<int>(); var jointTouched=new HashSet<int>();var differentTarget=new HashSet<int>();
        int unmatched=0, inactiveMoves=0, parMoves=0, offMoves=0, offPresent=0, relatedMoves=0;
        var examples=new List<object>();
        foreach(var move in moves) {
            if(!Enabled(move)) { inactiveMoves++; continue; }
            bool par=Has(move,"parallax"),off=Pair(move,"parallaxOffset") is {} op && (float.IsFinite(op.X)||float.IsFinite(op.Y));
            if(Has(move,"parallaxOffset")) offPresent++;
            bool related=par||off||(Has(move,"relativeTo") && S(move,"relativeTo")!="LastPosition");
            if(par) parMoves++; if(off) offMoves++; if(related) relatedMoves++;
            var targets=new HashSet<int>();
            foreach(var tag in Tags(move)) if(tagIndex.TryGetValue(tag,out var ids)) targets.UnionWith(ids);
            if(related && targets.Count==0) unmatched++;
            if(par) touched.UnionWith(targets); if(off) offsetTouched.UnionWith(targets); if(related) jointTouched.UnionWith(targets);
            if(par && Pair(move,"parallax") is {} target) foreach(int id in targets)
                if(Pair(adds[id],"parallax") is {} initial && PairKey(target)!=PairKey(initial)) differentTarget.Add(id);
            if(par && examples.Count<8) examples.Add(new { Floor=Raw(move,"floor"), Tag=S(move,"tag"), Target=Raw(move,"parallax"), Targets=targets.Count, Duration=Raw(move,"duration") });
        }
        string Composite(JsonElement e) => string.Join("|",Related.Select(k=>k+"="+Key(e,k)));
        string Operand(JsonElement e) => Composite(e)+"|position="+Key(e,"position")+"|pivotOffset="+Key(e,"pivotOffset")+"|scale="+Key(e,"scale")+"|rotation="+Key(e,"rotation")+"|image="+Key(e,"decorationImage")+"|originalSize="+Key(e,"originalSize");
        var movePairs=moves.Where(e=>Enabled(e)&&Pair(e,"parallax") is {} p&&float.IsFinite(p.X)&&float.IsFinite(p.Y)).Select(e=>PairKey(Pair(e,"parallax")!.Value));
        return new {
            Name=name, Path=path, Bytes=bytes, Sha256=sha,
            LevelVersion=root.TryGetProperty("settings",out var settings)?Raw(settings,"version"):"<missing>",
            NonemptyComponents=adds.Count(e=>Has(e,"components") && Key(e,"components") is not "\"\"" and not "{}"),
            Arrays=new { Decorations=Array(root,"decorations").Count(), Actions=Array(root,"actions").Count() },
            AddEvents=adds.Length, AddTypes=adds.GroupBy(e=>S(e,"eventType")).ToDictionary(g=>g.Key,g=>g.Count()),
            ActiveAdds=adds.Count(Enabled), MissingOrInvalidParallax=adds.Length-valid.Length,
            InitialParallax=Distribution(valid.Select(e=>PairKey(Pair(e,"parallax")!.Value))),
            InitialParallaxValues=valid.GroupBy(e=>PairKey(Pair(e,"parallax")!.Value)).OrderByDescending(g=>g.Count()).Select(g=>new { X=Pair(g.First(),"parallax")!.Value.X, Y=Pair(g.First(),"parallax")!.Value.Y, Count=g.Count() }),
            RawParallaxTimesOnePercentUnique=valid.Select(e=>PairKey((Pair(e,"parallax")!.Value.X*0.01f,Pair(e,"parallax")!.Value.Y*0.01f))).Distinct().Count(),
            InitialAuthoredRelatedTuples=Distribution(adds.Select(Composite)),
            InitialAuthoredOperandProxy=Distribution(adds.Select(Operand)),
            // Grouped exact arithmetic needs runtime float operands, not only these authored fields.
            NonzeroParallax=valid.Count(e=>Pair(e,"parallax") is {} p && (p.X!=0 || p.Y!=0)),
            NonzeroParallaxPercent=100.0*valid.Count(e=>Pair(e,"parallax") is {} p && (p.X!=0 || p.Y!=0))/Math.Max(1,adds.Length),
            Fields=Related.ToDictionary(k=>k,k=>new { Present=adds.Count(e=>e.TryGetProperty(k,out _)), Missing=adds.Count(e=>!e.TryGetProperty(k,out _)), Values=adds.GroupBy(e=>Key(e,k)).OrderByDescending(g=>g.Count()).Take(15).Select(g=>new{ Value=Raw(g.First(),k),Count=g.Count() }) }),
            MoveEvents=moves.Length, InactiveMoves=inactiveMoves, ParallaxMoveEvents=parMoves, ParallaxOffsetFieldPresentEvents=offPresent, ParallaxOffsetMoveEvents=offMoves, RelatedMoveEvents=relatedMoves,
            MoveFields=Related.ToDictionary(k=>k,k=>new { Present=moves.Count(e=>Enabled(e)&&e.TryGetProperty(k,out _)), Values=moves.Where(Enabled).GroupBy(e=>Key(e,k)).OrderByDescending(g=>g.Count()).Take(10).Select(g=>new{Value=Raw(g.First(),k),Count=g.Count()}) }),
            MoveParallaxTargets=Distribution(movePairs), InitialAndMoveParallaxUnion=valid.Select(e=>PairKey(Pair(e,"parallax")!.Value)).Concat(movePairs).Distinct().Count(),
            PotentiallyParallaxWrittenDecorations=touched.Count, PotentiallyParallaxWrittenPercent=100.0*touched.Count/Math.Max(1,adds.Length),
            AuthoredParallaxTargetDifferentFromInitialDecorations=differentTarget.Count,
            PotentiallyOffsetWrittenDecorations=offsetTouched.Count, PotentiallyRelatedWrittenDecorations=jointTouched.Count,
            UntouchedParallaxDecorations=adds.Length-touched.Count,
            UnmatchedRelatedMoveEvents=unmatched, Examples=examples,
            ControlEvents=all.Where(e=>S(e,"eventType") is "RepeatEvents" or "RepeatEventsPlus" or "MoveDecorations" or "SetConditionalEvents").GroupBy(e=>S(e,"eventType")).ToDictionary(g=>g.Key,g=>g.Count()),
            Limit="Touched means at least one active authored move targets the decoration by exact tag union. Offset moves require at least one finite axis; [null,null] has no setter. Related moves mean parallax/usable offset/non-LastPosition placement. It is an upper bound on actual change, not tween replay; inactive adds included in denominator. Repeats/easing/conditions/NaN interpolation are not expanded. Missing defaults are kept missing; no implied default value. Operand proxy is NOT a safe runtime cache key (tile positions/textures/parents absent)."
        };
    }

    readonly record struct Input(float X,float Y,float Mx,float My,float Ox,float Oy,float Sx,float Sy,float Tx,float Ty);
    readonly record struct Output(float X,float Y);
    // Separate scalar methods force the same subtract, multiply, add rounding sites in the parity probe.
    [MethodImpl(MethodImplOptions.NoInlining)] static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Sub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Mul(float a,float b)=>a*b;
    static Output Exact(Input i,float cx,float cy) => new(
        Add(Add(Mul(Sub(cx,i.X),i.Mx),i.X),i.Ox*i.Sx/i.Tx),
        Add(Add(Mul(Sub(cy,i.Y),i.My),i.Y),i.Oy*i.Sy/i.Ty));
    static Output Regroup(Input i,float cmx,float cmy) => new(
        Add(Add(cmx,Mul(i.X,Sub(1,i.Mx))),i.Ox*i.Sx/i.Tx),
        Add(Add(cmy,Mul(i.Y,Sub(1,i.My))),i.Oy*i.Sy/i.Ty));
    static Output FastExact(Input i,float cx,float cy) => new(
        ((cx-i.X)*i.Mx+i.X)+i.Ox*i.Sx/i.Tx,
        ((cy-i.Y)*i.My+i.Y)+i.Oy*i.Sy/i.Ty);
    static Output FastRegroup(Input i,Output c) => new(
        (c.X+i.X*(1-i.Mx))+i.Ox*i.Sx/i.Tx,
        (c.Y+i.Y*(1-i.My))+i.Oy*i.Sy/i.Ty);
    static bool Same(Output a,Output b)=>Bits(a.X)==Bits(b.X)&&Bits(a.Y)==Bits(b.Y);
    static float sink;
    static object Bench(int[] a)
    {
        int n=a.Length>0?a[0]:29000,k=a.Length>1?a[1]:4,frames=a.Length>2?a[2]:300,rounds=a.Length>3?a[3]:9;
        if(n<1||n>1000000||k<1||k>n||frames<1||rounds<3) throw new ArgumentException("1 <= K <= N <= 1000000, frames >= 1, rounds >= 3");
        var rng=new Random(726);
        float Next()=>((float)rng.NextDouble()*2-1)*10000;
        var unique=Enumerable.Range(0,k).Select(j=>new Input(Next(),Next(),j/(float)Math.Max(1,k-1),(k-1-j)/(float)Math.Max(1,k-1),Next()/100,Next()/100,1.5f,2,1,1)).ToArray();
        var repeated=Enumerable.Range(0,n).Select(j=>unique[j%k]).ToArray();
        var varied=repeated.Select(i=>i with {X=Next(),Y=Next()}).ToArray();
        var outv=new Output[n];var cache=new Output[k];
        var timing=new Dictionary<string,List<double>>();
        void Run(string mode) {
            for(int f=0;f<frames;f++) {
                float cx=f*0.03125f,cy=f*-0.015625f;
                if(mode is "exact_full_operand_groups" or "unsafe_multiplier_groups")
                    for(int j=0;j<k;j++) cache[j]=mode=="exact_full_operand_groups"?FastExact(unique[j],cx,cy):new Output(cx*unique[j].Mx,cy*unique[j].My);
                if(mode=="trig_groups_hypothetical") for(int j=0;j<k;j++) cache[j]=new Output(MathF.Sin(cx+unique[j].Mx),MathF.Cos(cy+unique[j].My));
                // Select a kernel outside the decoration loop: no per-item string dispatch overhead.
                switch(mode) {
                    case "exact_full_operand_groups":
                    case "trig_groups_hypothetical":
                        for(int j=0;j<n;j++) outv[j]=cache[j%k];break;
                    case "unsafe_multiplier_groups":
                        for(int j=0;j<n;j++) outv[j]=FastRegroup(varied[j],cache[j%k]);break;
                    case "trig_per_decoration_hypothetical":
                        for(int j=0;j<n;j++) outv[j]=new Output(MathF.Sin(cx+repeated[j].Mx),MathF.Cos(cy+repeated[j].My));break;
                    case "copy_floor":
                        for(int j=0;j<n;j++) outv[j]=new Output(repeated[j].X,repeated[j].Y);break;
                    case "per_decoration_varied":
                        for(int j=0;j<n;j++) outv[j]=FastExact(varied[j],cx,cy);break;
                    default:
                        for(int j=0;j<n;j++) outv[j]=FastExact(repeated[j],cx,cy);break;
                }
                sink=outv[f%n].X; // observable output, every frame
            }
        }
        string[] modes={"copy_floor","per_decoration_repeated","exact_full_operand_groups","per_decoration_varied","unsafe_multiplier_groups","trig_per_decoration_hypothetical","trig_groups_hypothetical"};
        foreach(var mode in modes){ Run(mode); timing[mode]=new(); }
        for(int r=0;r<rounds;r++) foreach(var mode in modes.Skip(r%modes.Length).Concat(modes.Take(r%modes.Length))) {
            long t=Stopwatch.GetTimestamp();Run(mode);timing[mode].Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds/frames);
        }
        int regroupMismatch=0,exactMismatch=0,inlinedMismatch=0;object? first=null;
        for(int f=0;f<17;f++) {
            float cx=f*0.03125f,cy=f*-0.015625f;
            for(int g=0;g<k;g++)cache[g]=FastExact(unique[g],cx,cy);
            for(int j=0;j<n;j++) {
            var i=varied[j];var x=Exact(i,cx,cy);var y=Regroup(i,Mul(cx,i.Mx),Mul(cy,i.My));
            if(!Same(x,y)) { regroupMismatch++; first??=new { Position=new[]{i.X,i.Y},Multiplier=new[]{i.Mx,i.My},Camera=new[]{cx,cy},Original=new[]{x.X,x.Y},Regrouped=new[]{y.X,y.Y},OriginalBits=new[]{Bits(x.X),Bits(x.Y)},RegroupedBits=new[]{Bits(y.X),Bits(y.Y)} }; }
            if(!Same(Exact(repeated[j],cx,cy),cache[j%k])) exactMismatch++;
            if(!Same(x,FastExact(i,cx,cy)))inlinedMismatch++;
            }
        }
        return new { Schema=1,N=n,K=k,Frames=frames,Rounds=rounds,Seed=726,Runtime=RuntimeInformation.FrameworkDescription,OS=RuntimeInformation.OSDescription,Architecture=RuntimeInformation.ProcessArchitecture.ToString(),StopwatchFrequency=Stopwatch.Frequency,
            Processor=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),LogicalProcessors=Environment.ProcessorCount,
            TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),HardwareIntrinsics=Environment.GetEnvironmentVariable("DOTNET_EnableHWIntrinsic"),
            Samples=timing.ToDictionary(x=>x.Key,x=>new { MsPerFrame=x.Value,MedianMsPerFrame=x.Value.Order().ElementAt(rounds/2),MinMsPerFrame=x.Value.Min(),MaxMsPerFrame=x.Value.Max() }),
            Parity=new { Comparisons=17L*n,RegroupMismatch=regroupMismatch,ExactFullOperandMismatch=exactMismatch,InlinedVsScalarMismatch=inlinedMismatch,First=first },Sink=sink,
            Limit="Synthetic .NET 8 CPU benchmark; repeated = all operands shared; varied = only multipliers shared. Group IDs prebuilt (modulo cost included), rebuild/hash/tween/Unity Transform/native/GPU/visibility costs excluded. Trig modes are deliberately hypothetical: the game's ordinary parallax position path has NO trig. No FPS/Mono/GPU conclusion. NoInlining scalar parity probe is distinct from timed inlined arithmetic." };
    }
    static object SelfTest()
    {
        using var doc=JsonDocument.Parse("""
        {"decorations":[
          {"eventType":"AddDecoration","parallax":[0,0],"tag":"a b"},
          {"eventType":"AddText","parallax":[100,100],"tag":"b"},
          {"eventType":"AddObject","tag":""}],
        "actions":[
          {"eventType":"MoveDecorations","tag":"a b","parallax":[50,50]},
          {"eventType":"MoveDecorations","tag":"b","parallax":[1,1],"active":false},
          {"eventType":"MoveDecorations","tag":"","parallax":[1,1]},
          {"eventType":"MoveDecorations","tag":"b","parallaxOffset":[null,0]}]}
        """);
        var report=JsonSerializer.SerializeToElement(Analyze(doc.RootElement,"fixture","inline",0,"fixture"));
        void Assert(bool b,string m){if(!b)throw new Exception(m);}
        Assert(report.GetProperty("AddEvents").GetInt32()==3,"all add kinds");
        Assert(report.GetProperty("InitialParallax").GetProperty("Unique").GetInt32()==2,"missing not zero");
        Assert(report.GetProperty("PotentiallyParallaxWrittenDecorations").GetInt32()==3,"tag union, NO TAG and inactive");
        Assert(report.GetProperty("UnmatchedRelatedMoveEvents").GetInt32()==0,"NO TAG indexed by Setup");
        Assert(report.GetProperty("PotentiallyOffsetWrittenDecorations").GetInt32()==2,"partial offset");
        Assert(PairKey((0,0))!=PairKey((-0.0f,0)),"signed zero bit key");
        var x=new Input(16777216,0,0.5f,0,0,0,1,1,1,1);
        Assert(!Same(Exact(x,-1,0),Regroup(x,-0.5f,0)),"reassociation cancellation");
        Assert(EscapeStringControls("{\"x\":\"a\nb\"}",out int controls)=="{\"x\":\"a\\u000ab\"}"&&controls==1,"string control escape");
        Assert(Pair(JsonDocument.Parse("{\"v\":12}").RootElement,"v")== (12f,12f),"legacy scalar vector");
        return new { Passed=true,Checks=9 };
    }
}
