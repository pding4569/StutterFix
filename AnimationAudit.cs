#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StutterFix
{
    // One explicit preparation/paused command. Never a per-frame scan or patch.
    internal static class AnimationAudit
    {
        internal static void Run()
        {
            if(Hitch.Playing)throw new InvalidOperationException("Pause/stop before the dependency audit; not a performance sample");
            var manager=scrDecorationManager.instance;
            if(manager==null)throw new InvalidOperationException("No current decoration manager");
            var current=HarmonyLib.AccessTools.FieldRefAccess<scrDecorationManager,List<scrDecoration>>("allDecorations")(manager);
            if(current==null)throw new InvalidOperationException("No current decoration list");
            int decos=0,parentFloor=0,parentDeco=0,maxDepth=0,depthLimit=0;
            foreach(var d in current)
            {
                if(d==null)continue;decos++;
                var parent=d.transform.parent;int depth=0;bool floor=false,deco=false;
                while(parent!=null&&depth<64)
                {
                    if(parent.GetComponent<scrFloor>()!=null)floor=true;
                    if(parent.GetComponent<scrDecoration>()!=null)deco=true;
                    parent=parent.parent;depth++;
                }
                if(parent!=null)depthLimit++;if(floor)parentFloor++;if(deco)parentDeco++;maxDepth=Math.Max(maxDepth,depth);
            }
            Log("[장식 계층 감사] decorations="+decos+" current_manager=1 floor_ancestor="+parentFloor+" decoration_ancestor="+parentDeco+" max_depth="+maxDepth+" truncated="+depthLimit+" reparented=0 jobs_applied=0");
            var info=new Dictionary<Type,int[]>();int empty=0,others=0,unknown=0;
            foreach(var b in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if(b==null||!b.gameObject.scene.IsValid()||!b.gameObject.scene.isLoaded||!b.isActiveAndEnabled)continue;
                var t=b.GetType();int[] row;
                if(!info.TryGetValue(t,out row))
                {
                    row=new int[3];info.Add(t,row);var update=Message(t,"Update");
                    if(update!=null)
                    {
                        try
                        {
                            var bytes=update.GetMethodBody()?.GetILAsByteArray();
                            if(bytes==null)row[1]=2;
                            else
                            {
                                int at=0;while(at<bytes.Length&&bytes[at]==0)at++;
                                row[1]=at+1==bytes.Length&&bytes[at]==0x2a?1:0;
                            }
                        }
                        catch{row[1]=2;}
                    }
                    else row[1]=-1;
                    foreach(var name in new[]{"Awake","Start","OnEnable","OnDisable","OnDestroy","FixedUpdate","LateUpdate","OnGUI","OnBecameVisible","OnBecameInvisible"})
                        if(Message(t,name)!=null)row[2]++;
                }
                row[0]++;
                if(row[1]==1){empty++;if(row[2]>0)others++;}else if(row[1]==2)unknown++;
            }
            Log("[휴면 갱신 감사] active_types="+info.Count+" exact_empty_update="+empty+" empty_with_other_lifecycle="+others+" unknown_update_body="+unknown+" components_disabled=0");
            foreach(var item in info)if(item.Value[1]==1)Log("[빈 갱신 형식] "+item.Key.FullName+" active="+item.Value[0]+" other_callbacks="+item.Value[2]);
            int hitTexts=0,hitActive=0,maxCharacters=0;var fonts=new HashSet<int>();var materials=new HashSet<int>();var texts=new HashSet<string>();
            foreach(var h in Resources.FindObjectsOfTypeAll<scrHitTextMesh>())
            {
                if(h==null||!h.gameObject.scene.IsValid()||!h.gameObject.scene.isLoaded||h.text==null)continue;hitTexts++;
                if(h.text.isActiveAndEnabled)hitActive++;
                if(h.text.font!=null)fonts.Add(h.text.font.GetInstanceID());
                if(h.text.fontSharedMaterial!=null)materials.Add(h.text.fontSharedMaterial.GetInstanceID());
                texts.Add(h.text.text??"");maxCharacters=Math.Max(maxCharacters,h.text.textInfo?.characterCount??0);
            }
            Log("[판정 글자 구조 감사] pooled="+hitTexts+" active_sample="+hitActive+" fonts="+fonts.Count+" materials="+materials.Count+" unique_strings="+texts.Count+" max_characters="+maxCharacters+" ring_replaced=0 canvas_reparented=0");
        }
        private static MethodInfo Message(Type type,string name)
        {
            for(var t=type;t!=null&&t!=typeof(MonoBehaviour);t=t.BaseType)
            {
                var method=t.GetMethod(name,BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
                if(method!=null)return method;
            }
            return null;
        }
        private static void Log(string value){Main.Entry.Logger.Log(value);}
    }
}
#endif
