using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Buffers;
using System.Security.Cryptography;

namespace StutterFix
{
    // Portable parser DOM only: no Unity objects, LevelEvent instances or pointers.
    internal static class JsonSnapshot
    {
        private const int Maximum=64*1024*1024;
        internal static byte[] Encode(object value)
        {
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream))
            {Write(writer,value,0);return stream.ToArray();}
        }
        private static void Text(BinaryWriter w,string s)
        {if(w.BaseStream.Position+4+2L*s.Length>Maximum)throw new InvalidDataException();w.Write(s.Length);foreach(char c in s)w.Write((ushort)c);}
        internal static string Fingerprint(string s)
        {
            byte[] buffer=ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                using(var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    for(int start=0;start<s.Length;start+=4096)
                    {
                        int n=Math.Min(4096,s.Length-start);
                        for(int i=0;i<n;i++){char c=s[start+i];buffer[i*2]=(byte)c;buffer[i*2+1]=(byte)(c>>8);}
                        hash.AppendData(buffer,0,n*2);
                    }
                    return BitConverter.ToString(hash.GetHashAndReset()).Replace("-","");
                }
            }
            finally{ArrayPool<byte>.Shared.Return(buffer,true);}
        }
        private static void Write(BinaryWriter w,object v,int depth)
        {
            if(depth>128||w.BaseStream.Position>Maximum)throw new InvalidDataException();
            if(v==null){w.Write((byte)0);return;}
            if(v is string s){w.Write((byte)1);Text(w,s);return;}
            if(v is int i){w.Write((byte)2);w.Write(i);return;}
            if(v is float f){w.Write((byte)3);w.Write(f);return;}
            if(v is bool b){w.Write((byte)(b?4:5));return;}
            if(v is Dictionary<string,object> d){w.Write((byte)6);w.Write(d.Count);foreach(var kv in d){Text(w,kv.Key);Write(w,kv.Value,depth+1);}return;}
            if(v is IList list){w.Write((byte)7);w.Write(list.Count);foreach(var item in list)Write(w,item,depth+1);return;}
            throw new InvalidDataException("Unsupported snapshot object");
        }
        internal static object Decode(byte[] data)
        {
            if(data==null||data.Length>Maximum)throw new InvalidDataException();
            using(var stream=new MemoryStream(data,false))
            using(var reader=new BinaryReader(stream))
            {int nodes=0;var v=Read(reader,0,ref nodes);if(stream.Position!=stream.Length)throw new InvalidDataException();return v;}
        }
        private static int Count(BinaryReader r)
        {int n=r.ReadInt32();if(n<0||n>1000000||n>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException();return n;}
        private static string Text(BinaryReader r)
        {
            int n=r.ReadInt32();if(n<0||n>Maximum/2||n>(r.BaseStream.Length-r.BaseStream.Position)/2)throw new InvalidDataException();
            var chars=new char[n];for(int i=0;i<n;i++)chars[i]=(char)r.ReadUInt16();return new string(chars);
        }
        private static object Read(BinaryReader r,int depth,ref int nodes)
        {
            if(depth>128||++nodes>2000000)throw new InvalidDataException();
            switch(r.ReadByte())
            {
                case 0:return null;case 1:return Text(r);case 2:return r.ReadInt32();case 3:return r.ReadSingle();case 4:return true;case 5:return false;
                case 6:{int n=Count(r);var d=new Dictionary<string,object>();for(int i=0;i<n;i++){string key=Text(r);d.Add(key,Read(r,depth+1,ref nodes));}return d;}
                case 7:{int n=Count(r);var a=new List<object>();for(int i=0;i<n;i++)a.Add(Read(r,depth+1,ref nodes));return a;}
                default:throw new InvalidDataException();
            }
        }
    }
}
