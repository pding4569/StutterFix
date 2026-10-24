#if AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Explicit attribution experiment, never a visual-equivalence mode.
        // Only replacement renderers are touched, not original SpriteRenderers.
        static readonly Dictionary<Renderer,bool> gpuAblationDrawUndo=new Dictionary<Renderer,bool>();
        static long gpuAblationRestored,gpuAblationRestoreErrors;
        static GameObject gpuAblationRoot;
        static bool gpuAblationRootWasActive;
        static bool GpuAblationCommand(string command)
        {
            if(!command.StartsWith("ablate ",StringComparison.Ordinal))return false;
            string mode=command.Substring(7);
            if(mode=="state") {LogGpuAblation();return true;}
            RequireLive(g2Window<0,"ablation cannot be used in G2 acceptance windows");
            RequireLive(mode=="off"||mode=="copy"||mode=="draw"||mode=="both"||mode=="root"||mode=="rootcopy","ablate off|copy|draw|both|root|rootcopy|state");
            RequireLive(mode=="off"||gpuLiveActive,"ablation requires active GPU ownership");
            if(mode=="copy"||mode=="both"||mode=="rootcopy")
            {
                RequireLive(GpuDecoCopy.Enabled&&GpuDecoCopy.MaskedEnabled,"copy ablation requires masked native copies");
                foreach(var s in gpuLiveSlices)RequireLive(s.NativeCopy!=null&&s.NativeCopy.Ready,"missing native copy context");
            }
            ResetGpuAblation();
            RequireLive(gpuAblationRestoreErrors==0,"ablation renderer restore failed");
            try
            {
                if(mode=="draw"||mode=="both")foreach(var s in gpuLiveSlices)foreach(var r in s.Renderers)
                    if(r&&!gpuAblationDrawUndo.ContainsKey(r)) {gpuAblationDrawUndo.Add(r,r.forceRenderingOff);r.forceRenderingOff=true;}
                if(mode=="root"||mode=="rootcopy")
                {
                    RequireLive(gpuLiveRoot,"missing replacement renderer root");
                    gpuAblationRoot=gpuLiveRoot;gpuAblationRootWasActive=gpuLiveRoot.activeSelf;
                    gpuAblationRoot.SetActive(false);
                }
                GpuDecoCopy.AblateDestinations=mode=="copy"||mode=="both"||mode=="rootcopy";
            }
            catch {ResetGpuAblation();throw;}
            LogGpuAblation();return true;
        }
        static void ResetGpuAblation()
        {
            GpuDecoCopy.AblateDestinations=false;
            try
            {
                if(gpuAblationRoot)
                {
                    gpuAblationRoot.SetActive(gpuAblationRootWasActive);
                    if(gpuAblationRoot.activeSelf!=gpuAblationRootWasActive)gpuAblationRestoreErrors++;
                }
            }
            catch {gpuAblationRestoreErrors++;}
            gpuAblationRoot=null;
            foreach(var pair in gpuAblationDrawUndo)try
            {
                if(!pair.Key)continue;
                pair.Key.forceRenderingOff=pair.Value;
                if(pair.Key.forceRenderingOff!=pair.Value)gpuAblationRestoreErrors++;else gpuAblationRestored++;
            }
            catch {gpuAblationRestoreErrors++;}
            gpuAblationDrawUndo.Clear();
        }
        static void LogGpuAblation()
        {
            int hidden=0;foreach(var p in gpuAblationDrawUndo)if(p.Key&&p.Key.forceRenderingOff)hidden++;
            var native=GpuDecoCopy.ReadTotals();
            GpuLog("ablation destination_copy_skipped="+GpuDecoCopy.AblateDestinations+" draw_hidden="+hidden
                +" saved_renderers="+gpuAblationDrawUndo.Count+" "+GpuDecoRenderMetrics.ScreenState()
                +" replacement_root_inactive="+(gpuAblationRoot&&!gpuAblationRoot.activeSelf)
                +" native_submitted="+native.Submitted+" native_executed="+native.Executed+" native_errors="+native.Errors
                +" native_cpu_total_ms="+(native.Ticks*1000.0/System.Diagnostics.Stopwatch.Frequency).ToString("F6",Inv)
                +" restored_flags="+gpuAblationRestored+" restore_errors="+gpuAblationRestoreErrors
                +" attribution_only=True visual_acceptance=False normalization_and_evaluate_preserved=True");
        }
    }
}
#endif
