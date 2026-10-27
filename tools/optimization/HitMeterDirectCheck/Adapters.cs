using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
namespace UnityEngine.UI
{
    public class Image : Object { public Color color { get; set; } public CanvasRenderer canvasRenderer = new CanvasRenderer(); }
}
namespace DG.Tweening
{
    // The game's UI module is in Assembly-CSharp; reproduce only its two shortcut bodies.
    public static class DOTweenModuleUI
    {
        public static TweenerCore<Color,Color,ColorOptions> DOColor(this UnityEngine.UI.Image t,Color v,float d) => DOTween.To(()=>t.color,x=>t.color=x,v,d).SetTarget(t);
        public static TweenerCore<Vector2,Vector2,VectorOptions> DOAnchorPos(this RectTransform t,Vector2 v,float d,bool snapping=false)
        { var tween=DOTween.To(()=>t.anchoredPosition,x=>t.anchoredPosition=x,v,d); tween.SetOptions(snapping); return tween.SetTarget(t); }
    }
}
namespace StutterFix
{
    internal static class Main
    {
        internal static EntryType Entry = new EntryType();
        internal class EntryType { public LoggerType Logger = new LoggerType(); }
        internal class LoggerType { public void Log(string s) => System.Console.WriteLine(s); }
    }
}
