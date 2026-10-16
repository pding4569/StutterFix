using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace StutterFix
{
    internal static class UpdateTransaction
    {
        internal interface IFiles
        {
            bool Exists(string path);
            byte[] Read(string path);
            void Write(string path,byte[] bytes);
            void Replace(string stage,string destination,string backup);
            void Move(string stage,string destination);
            void Delete(string path);
        }
        internal sealed class Disk:IFiles
        {
            public bool Exists(string path){return File.Exists(path);}
            public byte[] Read(string path){return File.ReadAllBytes(path);}
            public void Write(string path,byte[] bytes)
            {
                using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {f.Write(bytes,0,bytes.Length);f.Flush(true);}
            }
            public void Replace(string stage,string destination,string backup){File.Replace(stage,destination,backup);}
            public void Move(string stage,string destination){File.Move(stage,destination);}
            public void Delete(string path){File.Delete(path);}
        }
        internal static string Hash(byte[] bytes)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        }
        internal static void VerifyPackage(byte[] bytes,string expected)
        {
            if(bytes==null||bytes.Length>128*1024*1024||expected==null||expected.Length!=64
                ||!string.Equals(Hash(bytes),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Release package SHA-256 mismatch");
        }
        private static void CheckStage(IFiles files,string path,byte[] expected)
        {
            byte[] actual=files.Read(path);
            if(actual.Length!=expected.Length||Hash(actual)!=Hash(expected))throw new IOException("Staged update checksum mismatch");
        }
        internal static void Apply(string directory,byte[] dll,byte[] info,IFiles files=null,Action<string> validateAssembly=null,Func<bool> cancelled=null)
        {
            if(dll==null||info==null||dll.Length>32*1024*1024||info.Length>65536)throw new InvalidDataException("Update size limit");
            files=files??new Disk();
            string dllPath=Path.Combine(directory,"StutterFix.dll"),infoPath=Path.Combine(directory,"Info.json");
            string token=".new-"+Guid.NewGuid().ToString("N"),dllStage=dllPath+token,infoStage=infoPath+token,restoreStage=dllPath+token+"-restore";
            bool hadDll=files.Exists(dllPath),hadInfo=files.Exists(infoPath),dllAttempted=false,infoAttempted=false,keepRecovery=false;
            byte[] before=hadDll?files.Read(dllPath):null,infoBefore=hadInfo?files.Read(infoPath):null;
            string infoRestore=infoPath+token+"-restore";
            void CheckCancelled(){if(cancelled!=null&&cancelled())throw new OperationCanceledException("Update cancelled");}
            void Restore(string destination,string stage,byte[] original,byte[] replacement,bool existed)
            {
                if(existed)
                {
                    if(files.Exists(destination)&&Hash(files.Read(destination))==Hash(original))return;
                    files.Write(stage,original);CheckStage(files,stage,original);
                    if(files.Exists(destination))files.Replace(stage,destination,null);else files.Move(stage,destination);
                    CheckStage(files,destination,original);
                }
                else if(files.Exists(destination))
                {
                    if(Hash(files.Read(destination))!=Hash(replacement))throw new IOException("Concurrent file change; preserve current file");
                    files.Delete(destination);
                }
            }
            try
            {
                CheckCancelled();files.Write(dllStage,dll);CheckStage(files,dllStage,dll);
                validateAssembly?.Invoke(dllStage);
                files.Write(infoStage,info);CheckStage(files,infoStage,info);
                CheckCancelled();
                if(hadDll)CheckStage(files,dllPath,before);
                if(hadInfo)CheckStage(files,infoPath,infoBefore);
                dllAttempted=true;
                if(hadDll)files.Replace(dllStage,dllPath,dllPath+".bak");else files.Move(dllStage,dllPath);
                CheckCancelled();
                infoAttempted=true;
                if(hadInfo)files.Replace(infoStage,infoPath,infoPath+".bak");else files.Move(infoStage,infoPath);
            }
            catch(Exception installFailure)
            {
                if(dllAttempted)
                {
                    // Attempt both restores even if one fails. Backups remain.
                    var failures=new List<Exception>{installFailure};
                    try{Restore(dllPath,restoreStage,before,dll,hadDll);}catch(Exception e){failures.Add(e);}
                    if(infoAttempted)try{Restore(infoPath,infoRestore,infoBefore,info,hadInfo);}catch(Exception e){failures.Add(e);}
                    if(failures.Count>1){keepRecovery=true;throw new AggregateException("Update failed and rollback failed; preserve backup/recovery files",failures);}
                }
                throw;
            }
            finally
            {
                foreach(var path in new[]{dllStage,infoStage,restoreStage,infoRestore})
                    try{if(keepRecovery&&(path==restoreStage||path==infoRestore))continue;if(files.Exists(path))files.Delete(path);}catch{}
            }
        }
    }
}
