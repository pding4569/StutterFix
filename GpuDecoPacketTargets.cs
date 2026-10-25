#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        sealed class PacketTargetGuard
        {
            internal List<string> Tags;internal int TagsVersion,CacheVersion;
            internal Dictionary<string,List<scrDecoration>> Dictionary;
            internal List<scrDecoration>[] Rows;
            internal int[] Versions;
            internal bool[] Exists;
            internal List<scrDecoration> Cache;
            internal bool AllGpu;
        }
        static AccessTools.FieldRef<List<string>,int> packetTagVersion;
        static AccessTools.FieldRef<List<scrDecoration>,int> packetRowVersion;
        static bool packetVersionsTried;
        static GpuEventPacket gpuPacketTargetExpected;
        static long gpuPacketTargetEligible,gpuPacketTargetFast,gpuPacketTargetInvalid;
        static long gpuPacketTargetOracle;
        static bool PacketVersions()
        {
            if(packetVersionsTried)return packetTagVersion!=null&&packetRowVersion!=null;
            packetVersionsTried=true;
            try
            {
                var tt=typeof(List<string>);var rt=typeof(List<scrDecoration>);
                packetTagVersion=AccessTools.FieldRefAccess<List<string>,int>(AccessTools.Field(tt,"_version")!=null?"_version":"version");
                packetRowVersion=AccessTools.FieldRefAccess<List<scrDecoration>,int>(AccessTools.Field(rt,"_version")!=null?"_version":"version");
                var a=new List<string>{"x"};var b=new List<scrDecoration>{null};
                int av=packetTagVersion(a),bv=packetRowVersion(b);a[0]="y";b[0]=null;
                if(av==packetTagVersion(a)||bv==packetRowVersion(b))throw new NotSupportedException("list replacement not versioned");
                av=packetTagVersion(a);bv=packetRowVersion(b);a.Clear();b.Clear();
                if(av==packetTagVersion(a)||bv==packetRowVersion(b))throw new NotSupportedException("list clear not versioned");
                return true;
            }
            catch{packetTagVersion=null;packetRowVersion=null;return false;}
        }
        static PacketTargetGuard CapturePacketTargetGuard(ffxMoveDecorationsPlus fx,GpuEventPacket p)
        {
            if(gpuPacketMode<6||!PacketVersions()||fx==null)return null;
            try
            {
                var mgr=FastMove.ManagerForIndex(fx);var tags=FastMove.TagsForIndex(fx);
                if(mgr!=manager||tags==null||mgr.taggedDecorations==null)return null;
                var guard=new PacketTargetGuard{Tags=tags,TagsVersion=packetTagVersion(tags),Dictionary=mgr.taggedDecorations,
                    Rows=new List<scrDecoration>[tags.Count],Versions=new int[tags.Count],Exists=new bool[tags.Count],
                    Cache=new List<scrDecoration>(p.Targets)};
                for(int i=0;i<tags.Count;i++)
                {
                    if(tags[i]==null)return null;
                    guard.Exists[i]=guard.Dictionary.TryGetValue(tags[i],out var row);guard.Rows[i]=row;
                    if(guard.Exists[i]&&row==null)return null;
                    if(row!=null)guard.Versions[i]=packetRowVersion(row);
                }
                int at=0;
                foreach(var d in mgr.GetTaggedDecorations((IEnumerable<string>)tags))
                {if(at>=p.Targets.Length||!ReferenceEquals(d,p.Targets[at++]))return null;}
                if(at!=p.Targets.Length)return null;
                guard.CacheVersion=packetRowVersion(guard.Cache);
                return MatchPacketTargetCollections(guard,guard.Dictionary,tags)?guard:null;
            }
            catch{return null;}
        }
        // Called once after activation classifies native/CPU leftovers. Bypass
        // only entirely GPU-owned effects: no interleaved CPU setter can mutate
        // an original single-tag list halfway through that event.
        static void ClassifyPacketTargetGuards()
        {
            gpuPacketTargetExpected=null;
            if(gpuPacketMode<6)return;
            int prepared=0,all=0;
            foreach(var p in gpuPackets.Values)
            {
                var g=p.TargetGuard;if(g==null)continue;
                prepared++;
                g.AllGpu=p.Runs.Length==1&&p.Runs[0].First==0&&p.Runs[0].Masters.Length==p.Targets.Length;
                if(g.AllGpu)foreach(uint m in p.Runs[0].Masters)if(GpuCpuFallback((int)m)){g.AllGpu=false;break;}
                if(g.AllGpu)all++;
            }
            GpuLog("packet_target_guards prepared="+prepared+" all_gpu="+all+" available="+PacketVersions());
        }
        static bool MatchPacketTargetCollections(PacketTargetGuard g,Dictionary<string,List<scrDecoration>> dict,List<string> tags)
        {
            if(g==null||!ReferenceEquals(dict,g.Dictionary)||!ReferenceEquals(tags,g.Tags)
                ||packetTagVersion(tags)!=g.TagsVersion||packetRowVersion(g.Cache)!=g.CacheVersion)return false;
            for(int i=0;i<tags.Count;i++)
            {
                bool exists=dict.TryGetValue(tags[i],out var row);
                if(exists!=g.Exists[i]||!ReferenceEquals(row,g.Rows[i])||row!=null&&packetRowVersion(row)!=g.Versions[i])return false;
            }
            return true;
        }
        internal static bool TryPacketTargets(ffxMoveDecorationsPlus fx,Dictionary<string,List<scrDecoration>> dict,
            List<string> tags,out List<scrDecoration> targets)
        {
            targets=null;gpuPacketTargetExpected=null;
            if(gpuPacketMode<6||!gpuLiveActive||gpuPacketGeneration!=generation||dict==null||tags==null)return false;
            if(!gpuPackets.TryGetValue(fx.GetInstanceID(),out var p))return false;
            try
            {
                if(p.TargetGuard==null||!p.TargetGuard.AllGpu||!ReferenceEquals(FastMove.ManagerForIndex(fx),manager)
                    ||!MatchPacketTargetCollections(p.TargetGuard,dict,tags)){gpuPacketTargetInvalid++;return false;}
                gpuPacketTargetExpected=p;gpuPacketTargetEligible++;
                if(gpuPacketMode==7)return false; // Independent old Take oracle.
                targets=p.TargetGuard.Cache;gpuPacketTargetFast++;return true;
            }
            catch{gpuPacketTargetInvalid++;return false;}
        }
        static string PacketGuardFixture()
        {
            if(!PacketVersions())throw new NotSupportedException("packet list versions unavailable");
            var row=new List<scrDecoration>{null};var tags=new List<string>{"a","missing"};
            var dict=new Dictionary<string,List<scrDecoration>>{{"a",row}};
            Func<PacketTargetGuard> snapshot=()=>
            {
                var cache=new List<scrDecoration>{null};
                return new PacketTargetGuard{Tags=tags,TagsVersion=packetTagVersion(tags),Dictionary=dict,Rows=new[]{row,(List<scrDecoration>)null},
                    Exists=new[]{true,false},Versions=new[]{packetRowVersion(row),0},Cache=cache,CacheVersion=packetRowVersion(cache)};
            };
            int rejects=0;var g=snapshot();
            RequireLive(MatchPacketTargetCollections(g,dict,tags),"guard baseline");
            Action test=()=>{RequireLive(!MatchPacketTargetCollections(g,dict,tags),"mutation was accepted");rejects++;};
            dict["a"]=new List<scrDecoration>{null};test();dict["a"]=row;
            g=snapshot();row[0]=null;test();
            g=snapshot();row.Add(null);test();row.RemoveAt(row.Count-1);
            g=snapshot();tags[0]="b";test();tags[0]="a";
            g=snapshot();dict.Remove("a");test();dict["a"]=row;
            g=snapshot();dict["missing"]=null;test();dict.Remove("missing");
            g=snapshot();g.Cache[0]=null;test();
            g=snapshot();RequireLive(!MatchPacketTargetCollections(g,new Dictionary<string,List<scrDecoration>>(dict),tags),"dictionary identity");rejects++;
            RequireLive(!MatchPacketTargetCollections(g,dict,new List<string>(tags)),"tag list identity");rejects++;
            return "packet_guard_fixture baseline=1 rejected="+rejects+" mismatch=0 game_objects_modified=0";
        }
    }
}
#endif
