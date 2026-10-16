#if DEV || AUTOTEST
using System;
using UnityEngine;
namespace StutterFix
{
    internal static class MaterialPoolFixture
    {
        internal static string Run()
        {
            if(!DecorationMaterialPool.Enabled)return "applied=0 unsupported-or-disabled; "+DecorationMaterialPool.State;
            if(!Hitch.Playing)return "applied=0 requires-playing-scene";
            GameObject ca=null,ga=null,gb=null;RenderTexture rt=null;Texture2D tex=null,read=null;Sprite sprite=null;Material a=null,b=null,templateA=null,templateB=null,aliasSource=null,aliasBaseline=null,aliasActual=null;
            var previous=RenderTexture.active;int checks=0;
            void Check(bool value,string what){checks++;if(!value)throw new InvalidOperationException("Material COW fixture: "+what);}
            byte[] Pixels(Camera camera)
            {
                camera.Render();RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,64,64),0,0,false);read.Apply(false,false);
                return read.GetRawTextureData<byte>().ToArray();
            }
            try
            {
                tex=new Texture2D(4,4,TextureFormat.RGBA32,false);var colors=new Color32[16];
                for(int i=0;i<16;i++)colors[i]=new Color32((byte)(i*13),140,220,255);tex.SetPixels32(colors);tex.Apply(false,false);
                sprite=Sprite.Create(tex,new Rect(0,0,4,4),new Vector2(.5f,.5f),4);
                a=new Material(scrDecorationManager.tileShader);b=new Material(scrDecorationManager.tileShader);
                ca=new GameObject("SF-material-fixture-camera");var camera=ca.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;
                camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                rt=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;read=new Texture2D(64,64,TextureFormat.RGBA32,false);
                ga=new GameObject("SF-material-fixture-a");gb=new GameObject("SF-material-fixture-b");ga.layer=gb.layer=30;
                var ra=ga.AddComponent<SpriteRenderer>();var rb=gb.AddComponent<SpriteRenderer>();ra.sprite=rb.sprite=sprite;ra.sharedMaterial=a;rb.sharedMaterial=b;
                templateA=a;templateB=b;a=ra.material;b=rb.material; // Cache actual pre-pool private instances.
                ga.transform.position=new Vector3(-.45f,0,0);gb.transform.position=new Vector3(.45f,0,0);ra.color=new Color(1,.5f,.7f,1);rb.color=new Color(.6f,1,.4f,1);ra.sortingOrder=2;rb.sortingOrder=7;
                var baseline=Pixels(camera);bool lit=false;for(int i=0;i<baseline.Length;i+=4)if(baseline[i]!=0||baseline[i+1]!=0||baseline[i+2]!=0){lit=true;break;}Check(lit,"nonempty visible fixture");
                DecorationMaterialPool.BindFixture(ra);DecorationMaterialPool.BindFixture(rb);
                var pooled=Pixels(camera);Check(Equal(baseline,pooled),"baseline pixels");
                Check(DecorationMaterialPool.Peek(ra)==DecorationMaterialPool.Peek(rb),"canonical shared");
                Check(ra.material==a,"instance getter preserves cached private identity");Pixels(camera);
                Check(ra.sharedMaterial==a,"shared getter returns original identity");Check(DecorationMaterialPool.Peek(rb)!=b,"other renderer remains shared");
                pooled=Pixels(camera);Check(Equal(baseline,pooled),"getter repool pixels");
                a.SetColor("_Color",new Color(.4f,.8f,.2f,1));Check(DecorationMaterialPool.Peek(ra)==a,"cached-source write detaches");Check(DecorationMaterialPool.Peek(rb)!=b,"cached write isolated");
                Check(a.GetColor("_Color")!=b.GetColor("_Color"),"source values remain independent");
                var changed=Pixels(camera);Check(DecorationMaterialPool.Peek(ra)!=DecorationMaterialPool.Peek(rb),"changed state separate");
                DecorationMaterialPool.UnbindFixture(ra);DecorationMaterialPool.UnbindFixture(rb);
                Check(Equal(changed,Pixels(camera)),"changed pixels vs originals");
                DecorationMaterialPool.BindFixture(ra);DecorationMaterialPool.BindFixture(rb);Pixels(camera);
                a.SetOverrideTag("SFUnknownTag","tainted");Pixels(camera);Check(DecorationMaterialPool.Peek(ra)==a,"unknown tag excluded");
                Check(ra.sortingOrder==2&&rb.sortingOrder==7,"sorting unchanged");
                Check(ga.GetComponent("Collider2D")==null&&gb.GetComponent("Collider2D")==null,"no collider added");
                DecorationMaterialPool.UnbindFixture(ra);DecorationMaterialPool.UnbindFixture(rb);
                aliasSource=new Material(scrDecorationManager.tileShader);var startColor=aliasSource.GetColor("_Color");
                ra.sharedMaterial=rb.sharedMaterial=aliasSource;aliasBaseline=ra.material;
                bool originallyCloned=aliasBaseline!=aliasSource;var tint=new Color(.26f,.31f,.82f,.73f);
                aliasBaseline.SetColor("_Color",tint);var expectedB=rb.sharedMaterial.GetColor("_Color");
                ra.sharedMaterial=rb.sharedMaterial=aliasSource;aliasSource.SetColor("_Color",startColor);
                DecorationMaterialPool.BindFixture(ra);DecorationMaterialPool.BindFixture(rb);Pixels(camera);
                aliasActual=ra.material;Check((aliasActual!=aliasSource)==originallyCloned,"originally shared instance-clone semantics");
                aliasActual.SetColor("_Color",tint);Check(rb.sharedMaterial.GetColor("_Color")==expectedB,"instance write affects same original aliases");
                aliasSource.SetColor("_Color",Color.cyan);Check(aliasActual.GetColor("_Color")== (originallyCloned?tint:Color.cyan),"cached old shared source cannot retarget a private clone");
                var aliasPixels=Pixels(camera);DecorationMaterialPool.UnbindFixture(ra);DecorationMaterialPool.UnbindFixture(rb);
                Check(Equal(aliasPixels,Pixels(camera)),"shared-source COW pixel equivalence");
                return "conditions="+checks+" errors=0 pixels=0 cached_write_isolation=1 renderer_sorting_unchanged=1";
            }
            finally
            {
                if(ga!=null)DecorationMaterialPool.UnbindFixture(ga.GetComponent<SpriteRenderer>());
                if(gb!=null)DecorationMaterialPool.UnbindFixture(gb.GetComponent<SpriteRenderer>());
                RenderTexture.active=previous;
                foreach(var o in new UnityEngine.Object[]{ga,gb,ca,sprite,a,b,templateA,templateB,aliasSource,aliasBaseline,aliasActual,read,tex})if(o!=null)UnityEngine.Object.DestroyImmediate(o);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
        private static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        internal static string Scene()
        {
            if(!DecorationMaterialPool.Enabled||!Hitch.Playing)return "applied=0";
            var sc=scrCamera.instance;if(sc==null)return "applied=0 no-camera";
            var original=HarmonyLib.AccessTools.FieldRefAccess<scrCamera,Camera>("camobj")(sc);
            if(original==null)return "applied=0 no-world-camera";
            int width=original.targetTexture!=null?original.targetTexture.width:original.pixelWidth;
            int height=original.targetTexture!=null?original.targetTexture.height:original.pixelHeight;
            if(width<1||height<1||width*height>16000000)return "applied=0 invalid-size";
            GameObject go=null;RenderTexture rt=null;Texture2D read=null;var previous=RenderTexture.active;
            try
            {
                go=new GameObject("SF-material-scene-fixture");var camera=go.AddComponent<Camera>();camera.CopyFrom(original);camera.enabled=false;
                go.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);
                rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;
                read=new Texture2D(width,height,TextureFormat.RGBA32,false);
                byte[] Draw()
                {
                    RenderTexture.active=rt;GL.Clear(true,true,Color.clear);camera.Render();RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,width,height),0,0,false);read.Apply(false,false);return read.GetRawTextureData<byte>().ToArray();
                }
                DecorationMaterialPool.SuspendFixture(true);var baseline=Draw();
                DecorationMaterialPool.SuspendFixture(false);var actual=Draw();
                int changed=0,nonblack=0;for(int i=0;i<baseline.Length;i+=4)
                {
                    if(baseline[i]!=0||baseline[i+1]!=0||baseline[i+2]!=0)nonblack++;
                    if(baseline[i]!=actual[i]||baseline[i+1]!=actual[i+1]||baseline[i+2]!=actual[i+2]||baseline[i+3]!=actual[i+3])changed++;
                }
                return "song_s="+(scrConductor.instance!=null?scrConductor.instance.songposition_minusi:double.NaN).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" size="+width+"x"+height+" pixels="+(width*height)+" different="+changed+" nonblack="+nonblack+" same_unity_frame=1 scope=world-camera-without-image-effects; "+DecorationMaterialPool.State;
            }
            finally
            {
                DecorationMaterialPool.SuspendFixture(false);RenderTexture.active=previous;
                if(go!=null)UnityEngine.Object.DestroyImmediate(go);if(read!=null)UnityEngine.Object.DestroyImmediate(read);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
        internal static string Sorting()
        {
            if(!DecorationMaterialPool.Enabled||!Hitch.Playing)return "applied=0";
            GameObject go=null;RenderTexture rt=null;Texture2D tex=null,read=null;Sprite sprite=null;var previous=RenderTexture.active;
            int cases=0,mismatches=0,sharedPairs=0;
            var objects=new System.Collections.Generic.List<UnityEngine.Object>();
            var renderers=new System.Collections.Generic.List<SpriteRenderer>();
            int[][] permutations={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
            try
            {
                tex=new Texture2D(4,4,TextureFormat.RGBA32,false);var white=new Color32[16];for(int i=0;i<16;i++)white[i]=new Color32(255,255,255,255);tex.SetPixels32(white);tex.Apply(false,false);
                sprite=Sprite.Create(tex,new Rect(0,0,4,4),new Vector2(.5f,.5f),4);
                go=new GameObject("SF-sorting-fixture");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;camera.transform.position=new Vector3(0,0,-10);
                camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                rt=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;read=new Texture2D(64,64,TextureFormat.RGBA32,false);
                byte[] Draw(){camera.Render();RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,64,64),0,0,false);read.Apply(false,false);return read.GetRawTextureData<byte>().ToArray();}
                foreach(var materialOrder in permutations)foreach(var rendererOrder in permutations)
                {
                    var materials=new Material[3];var rs=new SpriteRenderer[3];
                    foreach(int i in materialOrder){materials[i]=new Material(scrDecorationManager.tileShader);objects.Add(materials[i]);if(i==2)materials[i].SetFloat("PixelSnap",1);}
                    foreach(int i in rendererOrder)
                    {
                        var item=new GameObject("SF-sort-"+i);objects.Add(item);item.layer=30;var r=item.AddComponent<SpriteRenderer>();rs[i]=r;renderers.Add(r);
                        r.sprite=sprite;r.sharedMaterial=materials[i];r.sortingOrder=0;r.color=i==0?new Color(1,0,0,.55f):i==1?new Color(0,1,0,.55f):new Color(0,0,1,.55f);
                    }
                    if(cases==0)
                    {
                        for(int i=0;i<3;i++)rs[i].sortingOrder=i;var forward=Draw();
                        for(int i=0;i<3;i++)rs[i].sortingOrder=2-i;var backward=Draw();
                        if(Equal(forward,backward))throw new InvalidOperationException("Sorting fixture cannot detect reordered translucent sprites");
                        foreach(var r in rs)r.sortingOrder=0;
                    }
                    var baseline=Draw();foreach(var r in rs)DecorationMaterialPool.BindFixture(r);var pooled=Draw();cases++;
                    if(DecorationMaterialPool.Peek(rs[0])==DecorationMaterialPool.Peek(rs[1])&&DecorationMaterialPool.Peek(rs[0])!=materials[0])sharedPairs++;
                    if(!Equal(baseline,pooled))mismatches++;
                    foreach(var r in rs)DecorationMaterialPool.UnbindFixture(r);
                    foreach(var item in objects)if(item!=null)UnityEngine.Object.DestroyImmediate(item);objects.Clear();renderers.Clear();
                }
                return "overlapping_translucent_cases="+cases+" pooled_pairs="+sharedPairs+" mismatch="+mismatches+" positive_control=1 equal_layer_order_distance=1 material_and_renderer_creation_permutations=6x6";
            }
            finally
            {
                foreach(var r in renderers)if(r!=null)DecorationMaterialPool.UnbindFixture(r);
                foreach(var item in objects)if(item!=null)UnityEngine.Object.DestroyImmediate(item);
                RenderTexture.active=previous;foreach(var item in new UnityEngine.Object[]{go,read,sprite,tex})if(item!=null)UnityEngine.Object.DestroyImmediate(item);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
    }
}
#endif
