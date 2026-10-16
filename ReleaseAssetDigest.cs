using System;
using System.Collections;
using System.Collections.Generic;

namespace StutterFix
{
    internal static class ReleaseAssetDigest
    {
        internal static bool TryFind(string json,string fileName,string urlPrefix,out string url,out string sha256)
        {
            url=sha256="";
            try
            {
                if(json==null||json.Length>2*1024*1024)return false;
                var root=FastJsonParser.Parse(json) as Dictionary<string,object>;object raw;
                if(root==null||!root.TryGetValue("assets",out raw)||!(raw is IList assets))return false;
                bool found=false;
                foreach(var item in assets)
                {
                    var a=item as Dictionary<string,object>;object name,download,digest;
                    if(a==null||!a.TryGetValue("name",out name)||!string.Equals(name as string,fileName,StringComparison.Ordinal))continue;
                    if(found){url=sha256="";return false;}found=true;
                    if(!a.TryGetValue("browser_download_url",out download)||!a.TryGetValue("digest",out digest))return false;
                    string u=download as string,h=digest as string;
                    if(u==null||!u.StartsWith(urlPrefix,StringComparison.Ordinal)||!u.EndsWith("/"+fileName,StringComparison.Ordinal)
                        ||h==null||!h.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)||h.Length!=71)return false;
                    h=h.Substring(7).ToLowerInvariant();
                    foreach(char c in h)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;
                    url=u;sha256=h;
                }
                return found&&url.Length>0;
            }
            catch{url=sha256="";return false;}
        }
    }
}
