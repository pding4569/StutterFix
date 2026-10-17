using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace StutterFix
{
    internal static class AssetDiskCache
    {
        internal static PersistentBlobCache Store;
        internal static string Codec="";
        internal static long DxtHits,DxtWrites,JsonHits,JsonWrites;
        internal static void Configure(bool enabled,int megabytes,string directory)
        {
            Store=null;if(!enabled)return;
            try
            {
                if(Codec.Length==0)using(var sha=SHA256.Create())using(var stream=typeof(AssetDiskCache).Assembly.GetManifestResourceStream("StutterFix.sfnative.dll"))
                    Codec=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
                // 기본 위치는 내장 드라이브(LocalApplicationData). 모드 폴더는 USB SSD(D:)일 수 있어 읽기·쓰기가 느리다.
                if(string.IsNullOrWhiteSpace(directory))directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StutterFix","asset-cache");
                long quota=Math.Max(64,Math.Min(4096,megabytes))*1048576L;
                // 남은 공간이 상한 + 4GB 보다 적으면 쓰지 않는다(드라이브가 꽉 차는 것을 막음).
                try
                {
                    var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory)));
                    if(drive.IsReady&&drive.AvailableFreeSpace<quota+(4L<<30)){Main.Entry.Logger.Log("[캐시] 남은 공간 부족("+(drive.AvailableFreeSpace>>20)+"MB): 디스크 캐시 쓰지 않음");return;}
                }catch{}
                Store=new PersistentBlobCache(directory,quota);
            }
            catch(Exception ex){try{Main.Entry.Logger.Log("[캐시] 사용할 수 없어 기존 방식: "+ex.Message);}catch{}}
        }
        internal static string ImageKey(string path,int side)=>PersistentBlobCache.Key(path,"dxt-v1/"+side+"/"+Codec);
        internal static byte[] PackDxt(int w,int h,int format,float factor,bool dxt5,byte[] data)
        {
            using(var m=new MemoryStream())using(var b=new BinaryWriter(m))
            {b.Write(w);b.Write(h);b.Write(format);b.Write(factor);b.Write(dxt5);b.Write(data.Length);b.Write(data);return m.ToArray();}
        }
        internal static bool UnpackDxt(byte[] data,out int w,out int h,out int format,out float factor,out bool dxt5,out byte[] blocks,out int offset)
        {
            w=h=format=0;factor=1;dxt5=false;blocks=null;offset=0;
            try
            {
                using(var m=new MemoryStream(data,false))using(var b=new BinaryReader(m))
                {
                    w=b.ReadInt32();h=b.ReadInt32();format=b.ReadInt32();factor=b.ReadSingle();dxt5=b.ReadBoolean();int count=b.ReadInt32();
                    if(w<=0||h<=0||w>32768||h>32768||w%4!=0||h%4!=0||!(factor>0&&factor<=1)||count<=0||count!=m.Length-m.Position)return false;
                    if(format!=PngDecoder.FormatRGBA32&&format!=PngDecoder.FormatARGB32&&format!=PngDecoder.FormatRGB24)return false;
                    if(dxt5!=(format!=PngDecoder.FormatRGB24)||count!=(long)(w/4)*(h/4)*(dxt5?16:8))return false;
                    blocks=data;offset=(int)m.Position;return data.Length-offset==count;   // 블록은 복사하지 않고 읽은 덩어리 안의 위치로 가리킨다
                }
            }
            catch{return false;}
        }
    }
}
