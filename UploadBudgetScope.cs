using System;

namespace StutterFix
{
    internal interface IUploadSettings
    {
        int TimeSlice {get;set;}
        int BufferSize {get;set;}
    }
    internal sealed class UploadBudgetScope
    {
        private readonly IUploadSettings settings;
        private int beforeTime,beforeBuffer;
        private bool ownsTime,ownsBuffer;
        internal bool Active=>ownsTime||ownsBuffer;
        internal UploadBudgetScope(IUploadSettings settings){this.settings=settings;}
        internal bool Begin()
        {
            // A failed restore can leave only one setting owned. Never report
            // a complete budget until both settings have been applied again.
            if(ownsTime&&ownsBuffer)return true;
            if(Active&&!Restore())return false;
            try
            {
                beforeTime=settings.TimeSlice;beforeBuffer=settings.BufferSize;
                ownsTime=ownsBuffer=true;
                settings.TimeSlice=16;settings.BufferSize=64;
                return true;
            }
            catch{Restore();return false;}
        }
        internal bool Restore()
        {
            if(ownsTime)try
            {
                if(settings.TimeSlice==16)settings.TimeSlice=beforeTime;
                ownsTime=false;
            }catch{}
            if(ownsBuffer)try
            {
                if(settings.BufferSize==64)settings.BufferSize=beforeBuffer;
                ownsBuffer=false;
            }catch{}
            return !Active;
        }
    }
}
