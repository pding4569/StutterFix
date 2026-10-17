using System;
using System.Reflection;

namespace StutterFix
{
    // 곡 중 유니티 물리 시뮬레이션(FixedUpdate 단계의 Physics/Physics2D 시뮬레이션)을 Script 모드로 돌려 멈춘다. 기본 꺼짐.
    // 게임은 Rigidbody 가 없고(감사 명령 physicsaudit) 히트박스는 Collider2D.Overlap 질의만 쓴다. 질의는 시뮬레이션 없이도 되며
    // Physics2D.autoSyncTransforms 가 켜져 있으면 질의 전에 변환을 동기화한다. 그래서 autoSyncTransforms 가 꺼져 있으면 아무것도 바꾸지 않는다.
    // 한계: 트리거/충돌 콜백(OnTrigger*, OnCollision*)은 시뮬레이션 단계에서 오므로 이 옵션을 켜면 그것에 기대는 다른 모드는 멈춘다.
    // 우리가 바꾼 값만 기억해 그 값으로 되돌린다(곡이 끝날 때, 끌 때, Unload).
    internal static class PhysicsPause
    {
        internal static bool Enabled;
        private static readonly Type P2D=Type.GetType("UnityEngine.Physics2D, UnityEngine.Physics2DModule");
        private static readonly Type P3D=Type.GetType("UnityEngine.Physics, UnityEngine.PhysicsModule");
        private static object before2D,before3D;
        private static bool changed2D,changed3D;
        internal static bool Active=>changed2D||changed3D;
        internal static string State=>"enabled="+Enabled+" active2d="+changed2D+" active3d="+changed3D+" mode2d="+Read(P2D)+" mode3d="+Read(P3D);

        private static object Read(Type t)
        {
            try{var p=t==null?null:t.GetProperty("simulationMode",BindingFlags.Public|BindingFlags.Static);return p==null?"n/a":p.GetValue(null,null);}
            catch{return "n/a";}
        }
        private static bool Set(Type t,ref object saved,int mode)
        {
            try
            {
                var p=t==null?null:t.GetProperty("simulationMode",BindingFlags.Public|BindingFlags.Static);
                if(p==null||!p.CanWrite)return false;
                var cur=p.GetValue(null,null);
                if(Convert.ToInt32(cur)==mode)return false;   // 이미 Script 이면 우리 것이 아니니 복구 대상도 아니다
                saved=cur;p.SetValue(null,Enum.ToObject(p.PropertyType,mode),null);return true;
            }
            catch(Exception ex){Main.Entry.Logger.Log("[물리] 변경 실패: "+ex.Message);return false;}
        }
        private static void Restore(Type t,ref object saved,ref bool changed)
        {
            if(!changed)return;
            try
            {
                var p=t.GetProperty("simulationMode",BindingFlags.Public|BindingFlags.Static);
                // 다른 코드가 그 사이 바꿨으면 덮지 않는다(우리가 둔 Script 일 때만 되돌림).
                if(Convert.ToInt32(p.GetValue(null,null))==2)p.SetValue(null,saved,null);
                changed=false;saved=null;
            }
            catch(Exception ex){Main.Entry.Logger.Log("[물리] 복구 실패(소유 유지, 다시 시도): "+ex.Message);}
        }
        private static bool AutoSync2D()
        {
            try{var p=P2D==null?null:P2D.GetProperty("autoSyncTransforms",BindingFlags.Public|BindingFlags.Static);return p!=null&&(bool)p.GetValue(null,null);}
            catch{return false;}
        }
        internal static void SongStarted()
        {
            if(!Enabled||Active)return;
            if(!AutoSync2D()){Main.Entry.Logger.Log("[물리] autoSyncTransforms 꺼짐: 시뮬레이션 중지 건너뜀");return;}
            changed2D=Set(P2D,ref before2D,2);changed3D=Set(P3D,ref before3D,2);
            Main.Entry.Logger.Log("[물리] 곡 중 시뮬레이션 중지: 2D="+changed2D+" 3D="+changed3D);
        }
        internal static void SongEnded(){Restore(P2D,ref before2D,ref changed2D);Restore(P3D,ref before3D,ref changed3D);}
        internal static void Shutdown(){Enabled=false;SongEnded();}
    }
}
