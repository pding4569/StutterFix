using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Explicit visual diagnostic, not per-frame performance instrumentation.
    // Sample five pairs of consecutive real cameras; retain up to128 central sprites.
    internal static class FrameGenLayerProbe
    {
        private static readonly AccessTools.FieldRef<scrCamera,Camera> Cam=AccessTools.FieldRefAccess<scrCamera,Camera>("camobj");
        private static readonly AccessTools.FieldRef<scrDecoration,scrParallax> Parallax=AccessTools.FieldRefAccess<scrDecoration,scrParallax>("parallax");
        private static readonly AccessTools.FieldRef<scrDecoration,DecPlacementType> Placement=AccessTools.FieldRefAccess<scrDecoration,DecPlacementType>("placementType");
        private static readonly double[] Times={20.125,20.54,21.2,22.236,22.876};
        private static readonly List<string> rows=new List<string>();
        private static int target,previousFrame;
        private static bool paired;
        private static double lastSong;
        private struct Entry { internal float area; internal string line; }
        internal static void Install() {
            rows.Clear(); target=0; previousFrame=-1; paired=false; lastSong=double.NaN;
            Camera.onPostRender+=Sample;
        }
        private static string Csv(object value) { return "\""+Convert.ToString(value,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\""; }
        private static void Sample(Camera camera) {
            if(!Hitch.Playing || scrCamera.instance==null || camera!=Cam(scrCamera.instance) || scrConductor.instance==null) return;
            double song=scrConductor.instance.songposition_minusi;
            if(song<lastSong-1) { target=0; previousFrame=-1; paired=false; rows.Clear(); }
            lastSong=song;
            if(target>=Times.Length || song<0) return;
            if(paired) { if(Time.frameCount<=previousFrame) return; paired=false; target++; }
            else { if(song<Times[target]) return; paired=true; previousFrame=Time.frameCount; }
            try { Capture(camera,song,paired?0:1,paired?target:target-1); }
            catch(Exception ex) { rows.Add("ERROR,"+Csv(ex.ToString())); target=Times.Length; }
        }
        private static void Capture(Camera camera,double song,int second,int pair) {
            var manager=scrDecorationManager.instance; if(manager==null) throw new InvalidOperationException("No decoration manager");
            var candidates=new List<Entry>(); int visible=0,central=0;
            foreach(var decoration in manager.allDecorations) {
                var visual=decoration as scrVisualDecoration;
                if(visual==null || !visual.gameObject.activeInHierarchy || visual.spriteRenderer==null) continue;
                var renderer=visual.spriteRenderer;
                if(!renderer.enabled || renderer.forceRenderingOff || renderer.color.a<.02f) continue;
                var bounds=renderer.bounds;
                float left=float.PositiveInfinity,right=float.NegativeInfinity,top=float.NegativeInfinity,bottom=float.PositiveInfinity;
                for(int i=0;i<8;i++) {
                    var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var uv=camera.WorldToViewportPoint(point); left=Mathf.Min(left,uv.x); right=Mathf.Max(right,uv.x); bottom=Mathf.Min(bottom,uv.y); top=Mathf.Max(top,uv.y);
                }
                if(right<=0 || left>=1 || top<=0 || bottom>=1) continue; visible++;
                if(right<=.2f || left>=.8f || top<=.2f || bottom>=.8f) continue; central++;
                var parallax=Parallax(decoration); var multiplier=parallax!=null?parallax.multiplier:new Vector2(float.NaN,float.NaN);
                var source=decoration.sourceLevelEvent;
                object relative=null,initialParallax=null,image=null,tag=null;
                if(source!=null) { relative=source["relativeTo"]; initialParallax=source["parallax"]; image=source["decorationImage"]; tag=source["tag"]; }
                var position=decoration.transform.position; var center=camera.WorldToViewportPoint(bounds.center); var pose=camera.transform.position;
                string line=FormattableString.Invariant($"{pair},{second},{Time.frameCount},{song:R},{camera.GetInstanceID()},{pose.x:R},{pose.y:R},{camera.orthographicSize:R},{camera.transform.eulerAngles.z:R},{decoration.GetInstanceID()},{position.x:R},{position.y:R},{center.x:R},{center.y:R},{left:R},{right:R},{bottom:R},{top:R},{multiplier.x:R},{multiplier.y:R},")+
                    Csv(Placement(decoration))+","+Csv(relative)+","+Csv(initialParallax)+","+Csv(image)+","+Csv(tag)+","+renderer.sortingOrder;
                candidates.Add(new Entry {area=(Mathf.Min(right,.8f)-Mathf.Max(left,.2f))*(Mathf.Min(top,.8f)-Mathf.Max(bottom,.2f)),line=line});
            }
            candidates.Sort((a,b)=>b.area.CompareTo(a.area));
            rows.Add(FormattableString.Invariant($"# pair={pair} second={second} frame={Time.frameCount} song={song:R} visible_sprites={visible} central_sprites={central}"));
            for(int i=0;i<Math.Min(128,candidates.Count);i++) rows.Add(candidates[i].line);
        }
        internal static void Finish() {
            Uninstall();
            File.WriteAllLines(Path.Combine(Main.Entry.Path,"framegen-outside","deco-state.csv"),
                new[]{"pair,second,frame,song_s,camera_id,camera_x,camera_y,camera_size,camera_angle_deg,deco_id,world_x,world_y,center_u,center_v,left,right,bottom,top,parallax_x,parallax_y,placement,initial_relative,initial_parallax,image,tag,sorting_order"}.ConcatRows(rows));
        }
        private static IEnumerable<string> ConcatRows(this string[] header,IEnumerable<string> body) { foreach(var line in header) yield return line; foreach(var line in body) yield return line; }
        internal static void Uninstall() { Camera.onPostRender-=Sample; }
    }
}
