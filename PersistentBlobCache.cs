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
        private long known=-1;   // 폴더 안 .sfc 총 크기. 처음 쓸 때 한 번만 센다(쓸 때마다 폴더를 훑고 정렬하던 것을 없앰)
        private long Scan(object _){long t=0;try{foreach(var f in new DirectoryInfo(root).GetFiles("*.sfc"))if(Valid(Path.GetFileNameWithoutExtension(f.Name)))t+=f.Length;}catch{}return t;}
        private const int Maximum=64*1024*1024, Header=104, Magic=0x32434653;   // v2: 64비트 빠른 검사합(관리 SHA-256 은 읽기마다 수백 MB 를 해시해 느렸다)
        internal PersistentBlobCache(string directory,long capacityBytes)
        {root=Path.GetFullPath(directory);quota=Math.Max(1024,capacityBytes);}
        internal static string Key(string file,string variant)
        {
            var info=new FileInfo(file);if(!info.Exists)throw new FileNotFoundException();
            string value=info.FullName.ToUpperInvariant()+"\n"+info.Length+"\n"+info.LastWriteTimeUtc.Ticks+"\n"+variant;
            using(var sha=SHA256.Create())return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        private static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
        // 8바이트씩 섞는 64비트 검사합. 손상·잘림을 잡는 용도이지 보안용이 아니다(키는 SHA-256, 파일은 우리 폴더에만 있다).
        private static unsafe ulong Sum(byte[] data)
        {
            ulong h=0x9E3779B97F4A7C15UL^(ulong)data.Length;int n=data.Length,i=0;
            fixed(byte* p=data)
            {
                for(;i+8<=n;i+=8){ulong v=*(ulong*)(p+i);h=(h^v)*0xFF51AFD7ED558CCDUL;h^=h>>29;}
                for(;i<n;i++){h=(h^p[i])*0x100000001B3UL;}
            }
            h^=h>>32;h*=0xC4CEB9FE1A85EC53UL;h^=h>>29;return h;
        }
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
                    ulong expected=reader.ReadUInt64();reader.ReadBytes(24);
                    var data=reader.ReadBytes(size);
                    if(data.Length!=size||Sum(data)!=expected)return false;
                    bytes=data;
                }
                return true;
            }
            catch{return false;}
        }
        internal bool Write(string key,byte[] bytes)
        {
            if(!Valid(key)||bytes==null||bytes.Length>Maximum||bytes.LongLength+Header>quota)return false;
            string temporary=null;
            lock(writer)
            try
            {
                Directory.CreateDirectory(root);
                string path=FilePath(key);
                if(known<0)known=Scan(null);
                long total=known;
                if(File.Exists(path))total-=new FileInfo(path).Length;
                if(total+bytes.LongLength+Header>quota)
                {
                    var files=new DirectoryInfo(root).GetFiles("*.sfc");
                    Array.Sort(files,(a,b)=>a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                    total=0;foreach(var f in files)if(Valid(Path.GetFileNameWithoutExtension(f.Name)))total+=f.Length;
                    if(File.Exists(path))total-=new FileInfo(path).Length;
                    foreach(var f in files)
                    {
                        if(total+bytes.LongLength+Header<=quota)break;
                        if(!Valid(Path.GetFileNameWithoutExtension(f.Name)))continue;
                        if(string.Equals(f.FullName,path,StringComparison.OrdinalIgnoreCase))continue;
                        long length=f.Length;f.Delete();total-=length;
                    }
                    known=total+(File.Exists(path)?new FileInfo(path).Length:0);
                }
                if(total+bytes.LongLength+Header>quota)return false;
                temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var w=new BinaryWriter(stream))
                {w.Write(Magic);w.Write(bytes.Length);w.Write(Encoding.ASCII.GetBytes(key));w.Write(Sum(bytes));w.Write(new byte[24]);w.Write(bytes);}
                long old=File.Exists(path)?new FileInfo(path).Length:0;
                if(old>0)File.Replace(temporary,path,null);else File.Move(temporary,path);
                known+=Header+bytes.LongLength-old;temporary=null;return true;
            }
            catch{return false;}
            finally{if(temporary!=null)try{File.Delete(temporary);}catch{}}
        }
    }
}
