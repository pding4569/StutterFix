// Original API/lifecycle fixture; no frame loop, graphics hooks or game assets.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
public static class SpriteCallbackFixture
{
    [Serializable] sealed class Result {public string editor,error,scope="native SpriteRenderer change notification and self-unregister; not game performance or module pixel equivalence";public int callbacks;public bool updatedReference,selfUnregister,explicitUnregister,passed;}
    public static void Run()
    {
        var result=new Result{editor=Application.unityVersion};GameObject go=null;Texture2D texture=null;Sprite a=null,b=null;SpriteRenderer sr=null;UnityAction<SpriteRenderer> callback=null;int exit=0;
        try
        {
            go=new GameObject("SF callback fixture"){hideFlags=HideFlags.HideAndDontSave};sr=go.AddComponent<SpriteRenderer>();sr.enabled=true;
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);a=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.zero);b=Sprite.Create(texture,new Rect(0,0,1,1),Vector2.zero);
            callback=r=>{result.callbacks++;result.updatedReference=r.sprite==a;r.UnregisterSpriteChangeCallback(callback);};
            sr.RegisterSpriteChangeCallback(callback);sr.sprite=a;sr.sprite=b;result.selfUnregister=result.callbacks==1&&result.updatedReference;
            sr.RegisterSpriteChangeCallback(callback);sr.UnregisterSpriteChangeCallback(callback);sr.sprite=a;result.explicitUnregister=result.callbacks==1;
            if(!result.selfUnregister||!result.explicitUnregister)throw new InvalidOperationException("callback lifecycle mismatch");result.passed=true;
        }
        catch(Exception e){result.error=e.ToString();exit=1;Debug.LogException(e);}
        finally{if(sr&&callback!=null)sr.UnregisterSpriteChangeCallback(callback);if(go)UnityEngine.Object.DestroyImmediate(go);if(a)UnityEngine.Object.DestroyImmediate(a);if(b)UnityEngine.Object.DestroyImmediate(b);if(texture)UnityEngine.Object.DestroyImmediate(texture);}
        File.WriteAllText(Path.Combine(Application.dataPath,"../sprite-callback-result.json"),JsonUtility.ToJson(result,true));EditorApplication.Exit(exit);
    }
}
