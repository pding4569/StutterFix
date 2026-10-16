using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StutterFix
{
    // Only this cache's 64-hex .sfc files are managed. All failures are misses.
    internal sealed class PersistentBlobCache
    {
        private readonly string root;
        private readonly long quota;
        private static readonly object writer=new object();
        private const int Maximum=64*1024*1024, Header=104, Magic=0x31434653;
        internal PersistentBlobCache(string directory,long capacityBytes)
        {root=Path.GetFullPath(directory);quota=Math.Max(1024,capacityBytes);}
        internal static string Key(string file,string variant)
        {
            var info=new FileInfo(file);if(!info.Exists)throw new FileNotFoundException();
            string value=info.FullName.ToUpperInvariant()+"\n"+info.Length+"\n"+info.LastWriteTimeUtc.Ticks+"\n"+variant;
            using(var sha=SHA256.Create())return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        private static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
        private static bool Valid(string key)
        {if(key==null||key.Length!=64)return false;foreach(char c in key)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;return true;}
        private string FilePath(string key)=>Path.Combine(root,key+".sfc");
        internal bool TryRead(string key,out byte[] bytes)
        {
            bytes=null;if(!Valid(key))return false;
            try
            {
                string path=FilePath(key);
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
                using(var reader=new BinaryReader(stream))
                {
                    if(stream.Length<Header||reader.ReadInt32()!=Magic)return false;
                    int size=reader.ReadInt32();if(size<0||size>Maximum||stream.Length!=Header+(long)size)return false;
                    if(Encoding.ASCII.GetString(reader.ReadBytes(64))!=key)return false;
                    byte[] expected=reader.ReadBytes(32),data=reader.ReadBytes(size);
                    if(data.Length!=size)return false;
                    using(var sha=SHA256.Create())if(!Equal(expected,sha.ComputeHash(data)))return false;
                    bytes=data;
                }
                try{File.SetLastWriteTimeUtc(path,DateTime.UtcNow);}catch{}
                return true;
            }
            catch{return false;}
        }
        private static bool Equal(byte[] a,byte[] b)
        {if(a.Length!=b.Length)return false;int diff=0;for(int i=0;i<a.Length;i++)diff|=a[i]^b[i];return diff==0;}
        internal bool Write(string key,byte[] bytes)
        {
            if(!Valid(key)||bytes==null||bytes.Length>Maximum||bytes.LongLength+Header>quota)return false;
            string temporary=null;
            lock(writer)
            try
            {
                Directory.CreateDirectory(root);
                string path=FilePath(key);
                var files=new DirectoryInfo(root).GetFiles("*.sfc");
                Array.Sort(files,(a,b)=>a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                long total=0;foreach(var f in files)if(Valid(Path.GetFileNameWithoutExtension(f.Name)))total+=f.Length;
                if(File.Exists(path))total-=new FileInfo(path).Length;
                foreach(var f in files)
                {
                    if(total+bytes.LongLength+Header<=quota)break;
                    if(!Valid(Path.GetFileNameWithoutExtension(f.Name)))continue;
                    if(string.Equals(f.FullName,path,StringComparison.OrdinalIgnoreCase))continue;
                    long length=f.Length;f.Delete();total-=length;
                }
                if(total+bytes.LongLength+Header>quota)return false;
                temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var w=new BinaryWriter(stream))
                using(var sha=SHA256.Create())
                {w.Write(Magic);w.Write(bytes.Length);w.Write(Encoding.ASCII.GetBytes(key));w.Write(sha.ComputeHash(bytes));w.Write(bytes);}
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
                temporary=null;return true;
            }
            catch{return false;}
            finally{if(temporary!=null)try{File.Delete(temporary);}catch{}}
        }
    }
}
