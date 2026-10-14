#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;
namespace StutterFix {
internal static class FreeFpsProbe {
 static readonly Dictionary<Camera,int> masks = new Dictionary<Camera,int>();
  internal static void Restore() { foreach(var x in masks)if(x.Key!=null)x.Key.cullingMask=x.Value;masks.Clear();if(counters){draw.Dispose();passes.Dispose();batches.Dispose();counters=false;} }
 internal static void Command(string arg) {
  if(arg=="counters") { Counters();return; }
  if(arg=="verify") { Verify(); return; }
  if(arg=="batches") { Batches(); return; }
  if(arg=="restore") {Restore();return;}
  var cameras=UnityEngine.Object.FindObjectsOfType<Camera>();
  foreach(var c in cameras) {
   Main.Entry.Logger.Log($"[무료FPS] camera={c.name} enabled={c.enabled} mask={c.cullingMask} depth={c.depth} clear={c.clearFlags} HDR={c.allowHDR} MSAA={c.allowMSAA} dynamic={c.allowDynamicResolution} occlusion={c.useOcclusionCulling} forceRT={c.forceIntoRenderTexture} RT={(c.targetTexture!=null?c.targetTexture.descriptor.ToString():"screen")}");
   if(arg.StartsWith("off ") && c.name.Equals(arg.Substring(4),StringComparison.OrdinalIgnoreCase)) {if(!masks.ContainsKey(c))masks[c]=c.cullingMask;c.cullingMask=0;}
  }
  Main.Entry.Logger.Log($"[무료FPS] quality softParticles={QualitySettings.softParticles} particleRaycast={QualitySettings.particleRaycastBudget} anisotropic={QualitySettings.anisotropicFiltering} textureLimit={QualitySettings.masterTextureLimit} display={Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRate} graphics={SystemInfo.graphicsDeviceType}");
 }
 static byte[] Shot(Camera[] cams,int depth,RenderTextureFormat format) {
  var rt=new RenderTexture(Screen.width,Screen.height,depth,format); var oldActive=RenderTexture.active; Texture2D tex=null;
  try {
   rt.Create();
   Main.Entry.Logger.Log($"[무료FPS 실제RT] requestedDepth={depth} requestedFormat={format} depth={rt.depth} format={rt.format} graphicsFormat={rt.graphicsFormat} stencil={rt.descriptor.depthStencilFormat}");
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
 static Unity.Profiling.ProfilerRecorder draw,passes,batches;
 static bool counters;
 static void Counters() {
  if(!counters) {draw=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render,"Draw Calls Count",1);passes=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render,"SetPass Calls Count",1);batches=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render,"Batches Count",1);counters=true;Main.Entry.Logger.Log("[무료FPS 카운터] 시작");}
  else {Main.Entry.Logger.Log($"[무료FPS 카운터] draw={draw.LastValue} valid={draw.Valid} setpass={passes.LastValue} valid={passes.Valid} batches={batches.LastValue} valid={batches.Valid}");draw.Dispose();passes.Dispose();batches.Dispose();counters=false;}
 }
 static void Batches() {
  var renderers=UnityEngine.Object.FindObjectsOfType<Renderer>();var mats=new HashSet<int>();int active=0,mpb=0;
  foreach(var r in renderers) {if(!r.enabled)continue;active++;if(r.HasPropertyBlock())mpb++;foreach(var m in r.sharedMaterials)if(m!=null)mats.Add(m.GetInstanceID());}
  Main.Entry.Logger.Log($"[무료FPS 배치] activeRenderers={active} sharedMaterialIds={mats.Count} propertyBlocks={mpb}");
  foreach(var name in new[]{"Draw Calls Count","SetPass Calls Count","Batches Count"}){var r=UnityEngine.Profiling.Recorder.Get(name);Main.Entry.Logger.Log($"[무료FPS 배치] recorder={name} valid={r.isValid} samples={r.sampleBlockCount}");}
 }

}
}
#endif
