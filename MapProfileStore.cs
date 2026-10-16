using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace StutterFix
{
    internal sealed class MapProfileStore
    {
        private const int Maximum=128*1024;
        private readonly string directory;
        internal MapProfileStore(string directory){this.directory=Path.GetFullPath(directory);}
        private static bool Key(string hash)
        {
            if(hash==null||hash.Length!=64)return false;
            foreach(char c in hash)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;
            return true;
        }
        internal Dictionary<string,object> Read(string hash)
        {
            if(!Key(hash))return null;
            return ReadFile(Path.Combine(directory,hash+".sfp"),hash)??ReadFile(Path.Combine(directory,hash+".sfp.bak"),hash);
        }
        private static Dictionary<string,object> ReadFile(string path,string hash)
        {
            try
            {
                byte[] raw;
                using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(file.Length<72||file.Length>Maximum+72)return null;
                    raw=new byte[(int)file.Length];int at=0;
                    while(at<raw.Length){int n=file.Read(raw,at,raw.Length-at);if(n==0)return null;at+=n;}
                    if(file.Length!=raw.Length)return null;
                }
                if(raw[0]!=0x53||raw[1]!=0x46||raw[2]!=0x50||raw[3]!=0x31||BitConverter.ToInt32(raw,68)!=raw.Length-72)return null;
                byte[] payload=new byte[raw.Length-72];Buffer.BlockCopy(raw,72,payload,0,payload.Length);
                if(UpdateTransaction.Hash(payload)!=Encoding.ASCII.GetString(raw,4,64))return null;
                var root=JsonSnapshot.Decode(payload) as Dictionary<string,object>;object map,version,values;
                if(root==null||!root.TryGetValue("map",out map)||!Equals(map,hash)||!root.TryGetValue("version",out version)||!Equals(version,1)||!root.TryGetValue("values",out values))return null;
                var result=values as Dictionary<string,object>;return result!=null&&result.Count<=256?result:null;
            }
            catch{return null;}
        }
        internal bool Write(string hash,Dictionary<string,object> values)
        {
            if(!Key(hash)||values==null||values.Count>256)return false;
            string temp=null;
            try
            {
                byte[] payload=JsonSnapshot.Encode(new Dictionary<string,object>{{"version",1},{"map",hash},{"values",values}});
                if(payload.Length>Maximum)return false;
                byte[] raw=new byte[payload.Length+72];raw[0]=0x53;raw[1]=0x46;raw[2]=0x50;raw[3]=0x31;
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(UpdateTransaction.Hash(payload)),0,raw,4,64);
                Buffer.BlockCopy(BitConverter.GetBytes(payload.Length),0,raw,68,4);Buffer.BlockCopy(payload,0,raw,72,payload.Length);
                Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,hash+".sfp");temp=path+".new-"+Guid.NewGuid().ToString("N");
                using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(raw,0,raw.Length);file.Flush(true);}
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
                return true;
            }
            catch{return false;}
            finally{try{if(temp!=null&&File.Exists(temp))File.Delete(temp);}catch{}}
        }
    }
}
