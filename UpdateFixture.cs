#if DEV || AUTOTEST
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace StutterFix
{
    internal static class UpdateFixture
    {
        internal static string Run(string root)
        {
            string directory=Path.Combine(Path.GetFullPath(root),"update-fixture-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            byte[] original=File.ReadAllBytes(Path.Combine(Main.Entry.Path,"StutterFix.dll"));
            byte[] candidate=new byte[original.Length+1];Buffer.BlockCopy(original,0,candidate,0,original.Length);candidate[candidate.Length-1]=0x36;
            byte[] info=Encoding.UTF8.GetBytes("{\"Id\":\"StutterFix\",\"Version\":\"9.9.9\"}");
            byte[] oldInfo=Encoding.UTF8.GetBytes("{\"Id\":\"StutterFix\",\"Version\":\"0.0.0\"}");
            byte[] zip;
            using(var memory=new MemoryStream())
            {
                using(var archive=new ZipArchive(memory,ZipArchiveMode.Create,true))
                {
                    using(var output=archive.CreateEntry("StutterFix/StutterFix.dll").Open())output.Write(candidate,0,candidate.Length);
                    using(var output=archive.CreateEntry("StutterFix/Info.json").Open())output.Write(info,0,info.Length);
                }
                zip=memory.ToArray();
            }
            string dllPath=Path.Combine(directory,"StutterFix.dll"),infoPath=Path.Combine(directory,"Info.json");
            File.WriteAllBytes(dllPath,original);File.WriteAllBytes(infoPath,oldInfo);
            int checks=0;bool refused=false;
            try{Updater.TestInstall(zip,directory,new string('0',64));}catch(InvalidDataException){refused=true;}
            if(!refused||!File.ReadAllBytes(dllPath).SequenceEqual(original))throw new Exception("Checksum rejection changed original");checks++;
            Updater.TestInstall(zip,directory,UpdateTransaction.Hash(zip));
            if(!File.ReadAllBytes(dllPath).SequenceEqual(candidate)||!File.ReadAllBytes(infoPath).SequenceEqual(info)
                ||!File.ReadAllBytes(dllPath+".bak").SequenceEqual(original)||!File.ReadAllBytes(infoPath+".bak").SequenceEqual(oldInfo))throw new Exception("Verified install or backup mismatch");checks++;
            File.WriteAllBytes(dllPath,original);File.WriteAllBytes(infoPath,oldInfo);
            bool rolledBack=false;
            try{UpdateTransaction.Apply(directory,candidate,info,new FailAfterInfoSwap(infoPath));}catch(IOException){rolledBack=true;}
            if(!rolledBack||!File.ReadAllBytes(dllPath).SequenceEqual(original)||!File.ReadAllBytes(infoPath).SequenceEqual(oldInfo))throw new Exception("Physical rollback mismatch");checks++;
            if(Directory.GetFiles(directory,"*.new-*").Length!=0)throw new Exception("Leftover transaction files");checks++;
            return "checks="+checks+" mismatch=0 target=isolated-filesystem memory_zip_only=True";
        }
        private sealed class FailAfterInfoSwap:UpdateTransaction.IFiles
        {
            private readonly UpdateTransaction.Disk disk=new UpdateTransaction.Disk();private readonly string info;
            internal FailAfterInfoSwap(string info){this.info=info;}
            public bool Exists(string p)=>disk.Exists(p);
            public byte[] Read(string p)=>disk.Read(p);
            public void Write(string p,byte[] b)=>disk.Write(p,b);
            public void Move(string a,string b)=>disk.Move(a,b);
            public void Delete(string p)=>disk.Delete(p);
            public void Replace(string a,string b,string backup){disk.Replace(a,b,backup);if(b==info&&!a.EndsWith("-restore"))throw new IOException("Injected post-info-swap failure");}
        }
    }
}
#endif
