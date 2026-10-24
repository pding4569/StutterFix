using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Exact instruction addresses only: nearest-start matching crosses JIT bodies
// and can attribute unrelated methods to a hot sample.
internal sealed class MonoSymbols
{
    readonly Dictionary<ulong,string> names=new Dictionary<ulong,string>();
    internal int Count=>names.Count;
    internal static bool Address(string text,out ulong address)
    {
        text=text.Trim();
        if(text.StartsWith("!",StringComparison.Ordinal))text=text.Substring(1);
        if(text.StartsWith("0x",StringComparison.OrdinalIgnoreCase))text=text.Substring(2);
        return ulong.TryParse(text,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out address) && address!=0;
    }
    internal static MonoSymbols Read(string path)
    {
        var result=new MonoSymbols();if(string.IsNullOrWhiteSpace(path))return result;
        int line=0;
        foreach(string raw in File.ReadLines(path))
        {
            line++;string text=raw.Trim();if(text.Length==0 || text.StartsWith("#"))continue;
            int space=text.IndexOfAny(new[]{' ','\t'});
            if(space<0 || !Address(text.Substring(0,space),out ulong address) || text.Substring(space).Trim().Length==0)
                throw new InvalidDataException("Mono symbol row "+line+" must be ADDRESS NAME");
            string name=text.Substring(space).Trim();
            if(result.names.TryGetValue(address,out string old) && old!=name)
                throw new InvalidDataException("conflicting Mono symbol at "+address.ToString("x"));
            result.names[address]=name;
        }
        return result;
    }
    internal string Resolve(ulong address,string fallback)=>names.TryGetValue(address,out string name)?"mono-jit!"+name:fallback;
}
