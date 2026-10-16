#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StutterFix
{
    // Explicit commands only. Never called from Update or an installed rendering patch.
    internal static class DecorationAudit
    {
        internal static void Run()
        {
            var scene=SceneManager.GetActiveScene();var seen=new HashSet<int>();
            var roles=new SortedDictionary<string,int[]>();var mats=new HashSet<int>();
            var crc=new HashSet<string>();var shaders=new SortedDictionary<string,Material>();
            int decorations=0,duplicates=0,active=0,enabled=0,visible=0,propertyBlocks=0;
            foreach(var d in Resources.FindObjectsOfTypeAll<scrDecoration>())
            {
                if(d==null||d.gameObject.scene!=scene)continue;decorations++;
                var v=d as scrVisualDecoration;
                foreach(var r in d.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if(!seen.Add(r.GetInstanceID())){duplicates++;continue;}
                    string role=v!=null&&r==v.spriteRenderer?"main":
                        v!=null&&r==v.bordersRenderer?"selection":v!=null&&r==v.hitboxRenderer?"hitbox":"other";
                    bool a=r.gameObject.activeInHierarchy,e=r.enabled;
                    int[] row;if(!roles.TryGetValue(role,out row))roles[role]=row=new int[5];
                    row[0]++;if(a){row[1]++;active++;}if(e){row[2]++;enabled++;}
                    if(a&&e)row[3]++;if(r.isVisible){row[4]++;visible++;}
                    if(r.HasPropertyBlock())propertyBlocks++;
                    var m=DecorationMaterialPool.Enabled?DecorationMaterialPool.Peek(r):r.sharedMaterial;if(m==null)continue;mats.Add(m.GetInstanceID());
                    string shader=m.shader!=null?m.shader.name:"null";shaders[shader]=m;
                    // CRC is a candidate bucket, never proof of material equality.
                    crc.Add(shader+"/"+m.ComputeCRC());
                }
            }
            Log("[장식 구조 감사] decorations="+decorations+" unique_renderers="+seen.Count+" duplicate_visits="+duplicates+
                " active_objects="+active+" enabled_components="+enabled+" is_visible="+visible+
                " property_blocks="+propertyBlocks+" materials="+mats.Count+" crc_candidate_groups="+crc.Count+" changed=0");
            foreach(var p in roles)Log("[장식 렌더러 역할] "+p.Key+" total/active/enabled/active_enabled/visible="+string.Join("/",p.Value));
            foreach(var p in shaders)
            {
                var m=p.Value;var s=m.shader;var names=new List<string>();
                for(int i=0;s!=null&&i<s.GetPropertyCount();i++)names.Add(s.GetPropertyName(i)+":"+s.GetPropertyType(i));
                Log("[장식 셰이더 속성] "+p.Key+" queue="+m.renderQueue+" passes="+m.passCount+" properties="+string.Join(",",names));
            }
        }
        internal static void Input()
        {
            var type=typeof(AsyncInputManager);
            Log("[입력 시각 감사] game_async_active="+AsyncInputManager.isActive+" qpc_frequency="+System.Diagnostics.Stopwatch.Frequency+" changed=0");
            foreach(var f in type.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
                if(f.Name.IndexOf("Tick",StringComparison.OrdinalIgnoreCase)>=0||f.Name=="keyQueue")
                    Log("[입력 시각 필드] "+f.Name+" type="+f.FieldType+" value="+f.GetValue(null));
        }
        internal static void Window()
        {
            var overlay=PerfOverlay.Instance;if(overlay==null){Log("[원본 프레임 창] unavailable");return;}
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var ms=(double[])typeof(PerfOverlay).GetField("bucketMs",flags).GetValue(overlay);
            var frames=(int[])typeof(PerfOverlay).GetField("bucketFrames",flags).GetValue(overlay);
            double total=0;long count=0;
            for(int i=1;i<=4;i++){total+=ms[i];count+=frames[i];}
            Log("[원본 프레임 창] monitor_elapsed=10..50 seconds accumulated_ms="+total.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" frames="+count+" fps="+(count*1000.0/total).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" pixels_capture=0 excludes_frames_ge_1500ms=1");
        }
        private static void Log(string value){Main.Entry.Logger.Log(value);}
    }
}
#endif
