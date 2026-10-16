using System;
using System.Collections.Generic;
using System.Reflection;

namespace StutterFix
{
    internal static class RenderProfileValues
    {
        private static bool Owned(string name)
        {
            return name.StartsWith("Fx",StringComparison.Ordinal)||name=="LowRenderScale"||name=="LowFsr"
                ||name=="LowHalfRender"||name=="LowAutoRes"||name=="LowAutoFps"||name=="LowAutoMin"
                ||name=="FrameGenOutside"||name=="FrameGenMultiplier"||name=="FrameGenRefresh";
        }
        private static bool Valid(object value,Type type)
        {
            if(type==typeof(bool))return value is bool;
            if(type==typeof(int))return value is int;
            if(type==typeof(float))return value is float f&&!float.IsNaN(f)&&!float.IsInfinity(f);
            return false;
        }
        internal static Dictionary<string,object> Capture(object settings)
        {
            var result=new Dictionary<string,object>(StringComparer.Ordinal);
            foreach(var field in settings.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))
            {
                if(!Owned(field.Name)||field.IsInitOnly)continue;
                object value=field.GetValue(settings);
                if(Valid(value,field.FieldType))result.Add(field.Name,value);
            }
            return result;
        }
        internal static void Apply(object settings,Dictionary<string,object> values)
        {
            if(values==null)return;
            foreach(var field in settings.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))
            {
                object value;
                if(Owned(field.Name)&&!field.IsInitOnly&&values.TryGetValue(field.Name,out value)&&Valid(value,field.FieldType))field.SetValue(settings,value);
            }
        }
        internal static void WithBaseline(object settings,Dictionary<string,object> baseline,Action save)
        {
            var current=Capture(settings);
            try{Apply(settings,baseline);save();}
            finally{Apply(settings,current);}
        }
    }
}
