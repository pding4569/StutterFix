using UnityEngine;

namespace StutterFix
{
    internal static class UploadBudget
    {
        private sealed class UnityUploadSettings:IUploadSettings
        {
            public int TimeSlice{get=>QualitySettings.asyncUploadTimeSlice;set=>QualitySettings.asyncUploadTimeSlice=value;}
            public int BufferSize{get=>QualitySettings.asyncUploadBufferSize;set=>QualitySettings.asyncUploadBufferSize=value;}
        }
        private static readonly UploadBudgetScope scope=new UploadBudgetScope(new UnityUploadSettings());
        internal static bool Enabled;
        internal static void Configure(bool value){Enabled=value;if(!value)Restore();}
        internal static void Begin()
        {
            if(!Enabled)return;
            bool ok=scope.Begin();
            try{Main.Entry.Logger.Log("[업로드 예산] 로딩 시작 적용="+ok+" time="+QualitySettings.asyncUploadTimeSlice+" buffer="+QualitySettings.asyncUploadBufferSize);}catch{}
        }
        internal static void Restore()
        {
            if(!scope.Active)return;
            bool ok=scope.Restore();
            try{Main.Entry.Logger.Log("[업로드 예산] 원래 설정 복원="+ok+" time="+QualitySettings.asyncUploadTimeSlice+" buffer="+QualitySettings.asyncUploadBufferSize);}catch{}
        }
    }
}
