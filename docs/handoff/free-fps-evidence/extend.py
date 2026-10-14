from pathlib import Path
p=Path('FreeFpsProbe.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('if(arg=="restore")', 'if(arg=="verify") { Verify(); return; }\n  if(arg=="batches") { Batches(); return; }\n  if(arg=="restore")')
at='''
 static byte[] Shot(Camera[] cams,int depth,RenderTextureFormat format) {
  var rt=new RenderTexture(Screen.width,Screen.height,depth,format); var oldActive=RenderTexture.active; Texture2D tex=null;
  try {
   rt.Create();
   foreach(var c in cams) {var old=c.targetTexture;try {c.targetTexture=rt;c.Render();} finally {c.targetTexture=old;}}
   RenderTexture.active=rt;tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();return tex.GetRawTextureData();
  } finally {RenderTexture.active=oldActive;if(tex!=null)UnityEngine.Object.Destroy(tex);rt.Release();UnityEngine.Object.Destroy(rt);}
 }
 static long Diff(byte[] a,byte[] b) {long n=0;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])n++;return n;}
 static void Verify() {
  var all=UnityEngine.Object.FindObjectsOfType<Camera>();var cams=new List<Camera>();foreach(var name in new[]{"BGStaticCam","BGMovingCam","Camera"})foreach(var c in all)if(c.name==name)cams.Add(c);
  if(cams.Count!=3)throw new Exception("Expected three scene cameras");
  var a=Shot(cams.ToArray(),24,RenderTextureFormat.ARGB32);var b=Shot(cams.ToArray(),24,RenderTextureFormat.ARGB32);
  Main.Entry.Logger.Log("[무료FPS 대조] control depth24 repeat changedBytes="+Diff(a,b));
  foreach(int depth in new[]{0,16}) {var x=Shot(cams.ToArray(),depth,RenderTextureFormat.ARGB32);Main.Entry.Logger.Log("[무료FPS 대조] depth="+depth+" changedBytes="+Diff(a,x));}
  foreach(var format in new[]{RenderTextureFormat.RGB565,RenderTextureFormat.ARGB4444})if(SystemInfo.SupportsRenderTextureFormat(format)){var x=Shot(cams.ToArray(),24,format);Main.Entry.Logger.Log("[무료FPS 대조] format="+format+" changedBytes="+Diff(a,x));}
 }
 static void Batches() {
  var renderers=UnityEngine.Object.FindObjectsOfType<Renderer>();var mats=new HashSet<int>();int active=0,mpb=0;
  foreach(var r in renderers) {if(!r.enabled)continue;active++;if(r.HasPropertyBlock())mpb++;foreach(var m in r.sharedMaterials)if(m!=null)mats.Add(m.GetInstanceID());}
  Main.Entry.Logger.Log($"[무료FPS 배치] activeRenderers={active} sharedMaterialIds={mats.Count} propertyBlocks={mpb}");
  foreach(var name in new[]{"Draw Calls Count","SetPass Calls Count","Batches Count"}){var r=UnityEngine.Profiling.Recorder.Get(name);Main.Entry.Logger.Log($"[무료FPS 배치] recorder={name} valid={r.isValid} samples={r.sampleBlockCount}");}
 }
'''
s=s.replace('\n}\n}\n#endif',at+'\n}\n}\n#endif');p.write_text(s,encoding='utf-8-sig')
