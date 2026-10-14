from pathlib import Path
p=Path('FreeFpsProbe.cs');s=p.read_text();s=s.replace('static readonly Dictionary<Camera,bool> enabled = new Dictionary<Camera,bool>();\n','');s=s.replace('foreach(var x in enabled) if(x.Key!=null)x.Key.enabled=x.Value; enabled.Clear(); ','');s=s.replace('rt.Create();','rt.Create();\n   Main.Entry.Logger.Log($"[무료FPS 실제RT] requestedDepth={depth} requestedFormat={format} depth={rt.depth} format={rt.format} graphicsFormat={rt.graphicsFormat} stencil={rt.descriptor.depthStencilFormat}");');s=s.replace('if(arg=="verify")', 'if(arg=="counters") { Counters();return; }\n  if(arg=="verify")')
s=s.replace(' static void Batches() {',''' static Unity.Profiling.ProfilerRecorder draw,passes,batches;
 static bool counters;
 static void Counters() {
  if(!counters) {draw=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render,"Draw Calls Count",1);passes=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render,"SetPass Calls Count",1);batches=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render,"Batches Count",1);counters=true;Main.Entry.Logger.Log("[무료FPS 카운터] 시작");}
  else {Main.Entry.Logger.Log($"[무료FPS 카운터] draw={draw.LastValue} valid={draw.Valid} setpass={passes.LastValue} valid={passes.Valid} batches={batches.LastValue} valid={batches.Valid}");draw.Dispose();passes.Dispose();batches.Dispose();counters=false;}
 }
 static void Batches() {''');s=s.replace('masks.Clear(); }','masks.Clear();if(counters){draw.Dispose();passes.Dispose();batches.Dispose();counters=false;} }');p.write_text(s)
