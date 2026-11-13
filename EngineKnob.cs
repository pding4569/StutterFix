#if AUTOTEST
using System;
using System.Collections.Generic;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace StutterFix
{
    // Explicit measurement only; single lease, no persistent configuration.
    internal static class EngineKnob
    {
        sealed class SavedCamera
        {
            internal Camera Camera;
            internal int Mask;
            internal bool Occlusion;
            internal long Calls;
        }
        static readonly List<SavedCamera> cameras = new List<SavedCamera>();
        static string active;
        static int workerBefore, workerTarget, frameStart;
        static bool syncBefore;
        static int restoreErrors;

        internal static bool Step(string arg)
        {
            var a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 1 && a[0] == "state") { State(); return true; }
            if (a.Length == 1 && a[0] == "census") { Census(); return true; }
            if (a.Length == 1 && a[0] == "off")
            {
                Stop();
                if (restoreErrors != 0) throw new Exception("engknob INVALID: restoration failed");
                return true;
            }
            if (a.Length == 2 && a[0] == "assert")
            {
                if (active != a[1] || Time.frameCount <= frameStart) throw new Exception("engknob INVALID: inactive/no elapsed frame");
                if (active.StartsWith("jobs") && JobsUtility.JobWorkerCount != workerTarget) throw new Exception("engknob INVALID: worker count changed");
                if (active == "sync2d" && Physics2D.autoSyncTransforms) throw new Exception("engknob INVALID: sync changed");
                foreach (var c in cameras)
                {
                    if (!c.Camera || c.Calls == 0 || (active == "occ" && c.Camera.useOcclusionCulling) || (active == "bgmask" && c.Camera.cullingMask != 0))
                        throw new Exception("engknob INVALID: camera destroyed/not rendered/overwritten");
                }
                State(); Log("accepted " + active); return true;
            }
            if (a.Length != 2 || (a[1] != "on" && a[1] != "off"))
                throw new Exception("engknob observe|jobs1|jobs2|jobs4|jobs8|occ|bgmask|sync2d on|off; census; state; assert <name>; off");
            if (a[1] == "off")
            {
                if (active != null && active != a[0]) throw new Exception("engknob: different active lease");
                return Step("off");
            }
            if (active != null) throw new Exception("engknob: stop existing lease first");
            string name = a[0];
            if (name != "jobs1" && name != "jobs2" && name != "jobs4" && name != "jobs8" && name != "occ" && name != "bgmask" && name != "sync2d" && name != "observe")
                throw new Exception("engknob: unknown target");
            restoreErrors = 0;
            active = name; frameStart = Time.frameCount;
            try
            {
                if (name.StartsWith("jobs"))
                {
                    workerBefore = JobsUtility.JobWorkerCount;
                    workerTarget = int.Parse(name.Substring(4));
                    if (workerTarget > JobsUtility.JobWorkerMaximumCount || workerBefore == workerTarget)
                        throw new Exception("engknob INVALID: unsupported/no-op worker target");
                    JobsUtility.JobWorkerCount = workerTarget;
                }
                else if (name == "sync2d")
                {
                    syncBefore = Physics2D.autoSyncTransforms;
                    if (!syncBefore) throw new Exception("engknob INVALID: autoSyncTransforms already false");
                    Physics2D.autoSyncTransforms = false;
                }
                else
                {
                    foreach (var cam in Camera.allCameras)
                    {
                        if (!cam || !cam.gameObject.scene.IsValid()) continue;
                        if (name == "occ" && !cam.useOcclusionCulling) continue;
                        if (name == "bgmask" && ((cam.name != "BGStaticCam" && cam.name != "BGMovingCam") || cam.cullingMask == 0)) continue;
                        cameras.Add(new SavedCamera { Camera = cam, Mask = cam.cullingMask, Occlusion = cam.useOcclusionCulling });
                        if (name == "occ") cam.useOcclusionCulling = false;
                        if (name == "bgmask") cam.cullingMask = 0;
                    }
                    if (cameras.Count == 0) throw new Exception("engknob INVALID: no eligible camera");
                    Camera.onPreCull += OnPreCull;
                }
                State();
            }
            catch { Stop(); throw; }
            return true;
        }

        static void OnPreCull(Camera cam)
        {
            foreach (var c in cameras) if (c.Camera == cam) c.Calls++;
        }

        internal static void Stop()
        {
            Camera.onPreCull -= OnPreCull;
            if (active == null) return;
            string old = active;
            // Try every restoration even if one API rejects it. Report failure.
            try
            {
                if (old.StartsWith("jobs"))
                {
                    JobsUtility.JobWorkerCount = workerBefore;
                    if (JobsUtility.JobWorkerCount != workerBefore) throw new Exception("worker restore mismatch");
                }
                if (old == "sync2d")
                {
                    Physics2D.autoSyncTransforms = syncBefore;
                    if (Physics2D.autoSyncTransforms != syncBefore) throw new Exception("sync restore mismatch");
                }
            }
            catch (Exception e) { restoreErrors++; Log("restore error " + e.Message); }
            foreach (var c in cameras)
            {
                try
                {
                    if (!c.Camera) continue;
                    if (old == "occ") { c.Camera.useOcclusionCulling = c.Occlusion; if (c.Camera.useOcclusionCulling != c.Occlusion) throw new Exception("occlusion restore mismatch"); }
                    else if (old == "bgmask") { c.Camera.cullingMask = c.Mask; if (c.Camera.cullingMask != c.Mask) throw new Exception("mask restore mismatch"); }
                }
                catch (Exception e) { restoreErrors++; Log("restore error " + e.Message); }
            }
            cameras.Clear(); active = null;
            Log("restored " + old + " errors=" + restoreErrors);
        }

        static void State()
        {
            Log("state active=" + (active ?? "off") + " workers=" + JobsUtility.JobWorkerCount + " max=" + JobsUtility.JobWorkerMaximumCount + " sync2d=" + Physics2D.autoSyncTransforms + " frames=" + (Time.frameCount - frameStart));
            foreach (var c in cameras) Log("camera=" + (c.Camera ? c.Camera.name : "destroyed") + " preCull=" + c.Calls);
        }

        static void Census()
        {
            State();
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            foreach (var cam in Camera.allCameras)
            {
                int count = 0;
                foreach (var r in renderers)
                    if (r && r.enabled && r.gameObject.activeInHierarchy && r.gameObject.scene.IsValid() && (cam.cullingMask & (1 << r.gameObject.layer)) != 0) count++;
                Log("census camera=" + cam.name + " id=" + cam.GetInstanceID() + " mask=" + cam.cullingMask + " depth=" + cam.depth + " rt=" + (cam.targetTexture ? cam.targetTexture.name : "screen") + " occlusion=" + cam.useOcclusionCulling + " rendererCandidates=" + count);
            }
        }

        static void Log(string s) { Main.Entry.Logger.Log("[engknob] " + s); }
    }
}
#endif
