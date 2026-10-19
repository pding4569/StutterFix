using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StutterFix
{
    // Independent, opt-in replacement of ONLY scrConductor.hitSoundsData scheduling.
    // All Unity API calls stay on the main thread; workers receive immutable PCM only.
    internal static class HitSoundBatch
    {
        internal const string Owner = "StutterFix.hitsoundbatch";
        private const int PoolSize = 16, MaxUploads = 2, MaxVoices = 200000;
        private const long PcmLimit = 64L * 1024 * 1024;
        private static Harmony harmony;
        private static bool enabled, installed;
        private static Session current, retiring;
        private static string reason = "disabled";
        private static int captures, fallbacks, lastCount, lastUploads, peakUploads;
        private static int uploadFrame = -1, uploadsThisFrame;
#if DEV || AUTOTEST
        private static string lastVerification = "no captured run";
#endif
        private static readonly AccessTools.FieldRef<scrConductor, IList> Data =
            AccessTools.FieldRefAccess<scrConductor, IList>("hitSoundsData");
        private static readonly AccessTools.FieldRef<scrConductor, int> Cursor =
            AccessTools.FieldRefAccess<scrConductor, int>("nextHitSoundToSchedule");
        private static readonly FieldInfo VoiceType = AccessTools.Field(
            AccessTools.Inner(typeof(scrConductor), "HitSoundsData"), "hitSound");
        private static readonly FieldInfo VoiceTime = AccessTools.Field(
            AccessTools.Inner(typeof(scrConductor), "HitSoundsData"), "time");
        private static readonly FieldInfo VoiceVolume = AccessTools.Field(
            AccessTools.Inner(typeof(scrConductor), "HitSoundsData"), "volume");

        internal static bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                if (!value) { Release("disabled", true); Compat.ReleaseHitSoundGuard(); }
                else if (installed) Compat.EnsureHitSoundGuard();
            }
        }

        private sealed class Session
        {
            internal scrConductor Conductor;
            internal IList Original;
            internal HitSoundMix.Voice[] Voices;
            internal AudioClip[] Inputs;
            internal double[] Times;
            internal float[] Volumes;
            internal double Origin, SongClock;
            internal float Pitch, HitVolume;
            internal AudioMixerGroup Group;
            internal int Rate, Channels, Frames, Segments, Next, Requested = -1;
            internal long PcmBytes;
            internal GameObject Root;
            internal AudioSource[] Sources;
            internal AudioClip[] Clips;
            internal double[] Until;
            internal readonly ConcurrentQueue<float[]> Free = new ConcurrentQueue<float[]>();
            internal readonly ConcurrentQueue<Rendered> Ready = new ConcurrentQueue<Rendered>();
            internal readonly AutoResetEvent Wake = new AutoResetEvent(false);
            internal Thread Worker, Verifier;
            internal volatile bool Cancel;
            internal volatile string Error;
            internal int Rendered, Scheduled;
#if DEV || AUTOTEST
            internal volatile string Verification = "not requested";
            internal int Verified;
            internal double MaxError;
            internal double MaxTimeErrorSamples;
            internal long Differences;
            internal long[] ObservedHashes, ExpectedHashes;
#endif
            internal bool Taken;
            internal int OutputRate;
        }
        private struct Rendered { internal int Index; internal float[] Pcm; }

        internal static void Install()
        {
            if (installed) return;
            harmony = new Harmony(Owner);
            try
            {
                harmony.Patch(AccessTools.Method(typeof(scrConductor), "PlayHitTimes"),
                    prefix: new HarmonyMethod(typeof(HitSoundBatch), nameof(BeforePrepare)) { priority = Priority.First },
                    postfix: new HarmonyMethod(typeof(HitSoundBatch), nameof(AfterPrepare)) { priority = Priority.First });
                harmony.Patch(AccessTools.Method(typeof(scrConductor), "Update"),
                    prefix: new HarmonyMethod(typeof(HitSoundBatch), nameof(BeforeUpdate)) { priority = Priority.First });
                PatchStop(typeof(AudioManager), "StopAllSounds");
                PatchStop(typeof(scrConductor), "Rewind");
                foreach (var name in new[] { "Restart", "FailAction", "OnLandOnPortal", "QuitToMainMenu" })
                    PatchStop(typeof(scrController), name);
                PatchStop(typeof(scnEditor), "SwitchToEditMode");
                SceneManager.activeSceneChanged += SceneChanged;
                AudioSettings.OnAudioConfigurationChanged += AudioChanged;
                installed = true;
                if (enabled) Compat.EnsureHitSoundGuard();
            }
            catch
            {
                harmony.UnpatchAll(Owner);
                SceneManager.activeSceneChanged -= SceneChanged;
                AudioSettings.OnAudioConfigurationChanged -= AudioChanged;
                throw;
            }
        }

        private static void PatchStop(Type type, string name)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (method.Name == name)
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(HitSoundBatch), nameof(StopPrefix)));
        }
        public static void StopPrefix() { Release("game stop/rebuild", true, false); }
        private static void SceneChanged(Scene a, Scene b) { Release("scene changed", true, false); }
        private static void AudioChanged(bool changed) { Release("audio configuration changed", true, false); }
        public static void BeforePrepare(out bool __state)
        {
            Release("preparing", true, false);
            __state = enabled && Compat.EnsureHitSoundGuard();
        }

        public static void AfterPrepare(scrConductor __instance, bool __state)
        {
            if (!enabled) return;
            // A newly removed late-loaded Quartz postfix may still exist in THIS
            // invocation's old Harmony wrapper. Take over only on the next preparation.
            if (__state) { reason = "late Quartz detected: takeover deferred until next preparation"; return; }
            Session s = null;
            try
            {
                if (!Compat.HitSoundGuardReady) throw new NotSupportedException("Quartz guard unavailable");
                if (Volatile.Read(ref retiring) != null) throw new NotSupportedException("previous worker cleanup pending");
                s = Capture(__instance);
                current = s;
                s.Worker = new Thread(() => Work(s)) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "StutterFix hit PCM" };
                s.Requested = Math.Min(s.Segments - 1, 3);
                s.Worker.Start();
                s.Wake.Set();
                // Preparation only. Never wait for PCM in an Update.
                var deadline = Environment.TickCount + 250;
                while (s.Ready.Count < Math.Min(2, s.Segments) && s.Error == null && unchecked(Environment.TickCount - deadline) < 0)
                    Thread.Sleep(1);
                if (s.Error != null || s.Ready.Count < Math.Min(2, s.Segments))
                    throw new NotSupportedException(s.Error ?? "initial PCM timeout");
                if (s.Origin < AudioSettings.dspTime + 0.05) throw new NotSupportedException("insufficient initial DSP lead");
                Pump(s);
                if (s.Next == 0) throw new NotSupportedException("initial schedule unavailable");
                Cursor(__instance) = s.Original.Count;
                s.Taken = true;
                captures++;
                reason = "captured";
            }
            catch (Exception ex)
            {
                if (s != null && current == null) current = s;
                Release("capture fallback: " + ex.Message, true);
                fallbacks++;
            }
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        private static Session Capture(scrConductor c)
        {
            var am = ADOBase.audioManager;
            var list = Data(c);
            lastCount = list?.Count ?? 0;
            if (!GCS.d_hitsounds || !c.isGameWorld || ADOBase.lm == null || c.song == null || am == null ||
                list == null || list.Count == 0 || list.Count > MaxVoices || Cursor(c) != 0)
                throw new NotSupportedException("no fresh supported hit list");
            foreach (var name in new[] { "Play", "_Play", "MakeSource" })
            {
                var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(AudioManager), name));
                if (info != null && (info.Prefixes.Count != 0 || info.Postfixes.Count != 0 || info.Transpilers.Count != 0 || info.Finalizers.Count != 0))
                    throw new NotSupportedException("custom AudioManager path: " + name);
            }
            var prefab = am.audioSourcePrefab;
            if (prefab == null || prefab.playOnAwake || prefab.loop || prefab.spatialBlend != 0 || prefab.spatialize || prefab.panStereo != 0 ||
                prefab.ignoreListenerPause || prefab.ignoreListenerVolume || prefab.reverbZoneMix != 0 ||
                prefab.gameObject.GetComponents<Component>().Length != 2 || prefab.transform.childCount != 0)
                throw new NotSupportedException("source has 3D/loop/pan/reverb/filter or custom components");
            var s = new Session { Conductor = c, Original = list, Origin = double.PositiveInfinity,
                SongClock = c.dspTimeSong, Pitch = c.song.pitch, HitVolume = c.hitSoundVolume,
                Group = c.hitSoundGroup != null ? c.hitSoundGroup : am.fallbackMixerGroup,
                Voices = new HitSoundMix.Voice[list.Count], Inputs = new AudioClip[list.Count],
                Times = new double[list.Count], Volumes = new float[list.Count] };
            // Return partially created objects to the common exception/cleanup path.
            current = s;
            if (!Finite(s.Pitch) || s.Pitch <= 0 || !Finite(s.SongClock)) throw new NotSupportedException("invalid clock/pitch");
            var cache = new Dictionary<AudioClip, float[]>();
            double end = double.NegativeInfinity;
            for (int i = 0; i < list.Count; i++)
            {
                object item = list[i];
                double time = (double)VoiceTime.GetValue(item);
                float volume = (float)VoiceVolume.GetValue(item);
                if (!Finite(time) || !Finite(volume) || time < AudioSettings.dspTime || (i > 0 && time < s.Times[i - 1]))
                    throw new NotSupportedException("invalid/past/unsorted event");
                var clip = am.FindOrLoadAudioClip("snd" + (HitSound)VoiceType.GetValue(item));
                if (clip == null || clip.loadType != AudioClipLoadType.DecompressOnLoad || clip.loadState != AudioDataLoadState.Loaded ||
                    clip.samples <= 0 || clip.frequency < 8000 || clip.frequency > 96000 || clip.channels < 1 || clip.channels > 2 ||
                    (double)clip.samples / clip.frequency > 8)
                    throw new NotSupportedException("unreadable/streaming/compressed/long clip");
                if (s.Rate == 0) { s.Rate = clip.frequency; s.Channels = clip.channels; }
                if (s.Rate != clip.frequency || s.Channels != clip.channels)
                    throw new NotSupportedException("mixed sample rate/channel layout (no guessed resampling)");
                if (!cache.TryGetValue(clip, out var pcm))
                {
                    long count = (long)clip.samples * clip.channels;
                    if (cache.Count >= 64 || s.PcmBytes + count * 4 > PcmLimit) throw new NotSupportedException("PCM cache limit");
                    pcm = new float[(int)count];
                    if (!clip.GetData(pcm, 0)) throw new NotSupportedException("GetData failed");
                    foreach (float sample in pcm) if (!Finite(sample)) throw new NotSupportedException("nonfinite PCM");
                    cache.Add(clip, pcm); s.PcmBytes += count * 4;
                }
                s.Inputs[i] = clip; s.Times[i] = time; s.Volumes[i] = volume;
                s.Voices[i] = new HitSoundMix.Voice { Pcm = pcm, Gain = Mathf.Clamp01(volume) };
                s.Origin = Math.Min(s.Origin, time);
                end = Math.Max(end, time + (double)clip.samples / clip.frequency);
            }
            if (end - s.Origin > 7200) throw new NotSupportedException("duration limit (2 hours)");
            for (int i = 0; i < list.Count; i++) s.Voices[i].Start = HitSoundMix.Sample(s.Times[i] - s.Origin, s.Rate);
            s.Frames = s.Rate / 2;
            long total = 0;
            foreach (var v in s.Voices) total = Math.Max(total, v.Start + v.Pcm.Length / s.Channels);
            s.Segments = checked((int)((total + s.Frames - 1) / s.Frames));
            s.OutputRate = AudioSettings.outputSampleRate;
#if DEV || AUTOTEST
            s.ObservedHashes = new long[s.Segments]; s.ExpectedHashes = new long[s.Segments];
#endif
            s.Root = new GameObject("StutterFix hit sound batch");
            s.Root.transform.SetParent(c.transform, false);
            s.Sources = new AudioSource[PoolSize]; s.Clips = new AudioClip[PoolSize]; s.Until = new double[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                s.Sources[i] = Object.Instantiate(prefab, s.Root.transform);
                s.Sources[i].Stop(); s.Sources[i].playOnAwake = false;
                s.Sources[i].pitch = 1; s.Sources[i].volume = 1; s.Sources[i].priority = 128;
                s.Sources[i].outputAudioMixerGroup = s.Group;
                s.Clips[i] = AudioClip.Create("StutterFix PCM " + i, s.Frames, s.Channels, s.Rate, false);
                s.Sources[i].clip = s.Clips[i];
                s.Free.Enqueue(new float[s.Frames * s.Channels]);
            }
            return s;
        }

        private static void Work(Session s)
        {
            try
            {
                int index = 0;
                Func<bool> cancelled = () => s.Cancel;
                while (!s.Cancel)
                {
                    if (index >= s.Segments) return;
                    if (index > Volatile.Read(ref s.Requested) || !s.Free.TryDequeue(out var buffer)) { s.Wake.WaitOne(); continue; }
                    HitSoundMix.Render(s.Voices, s.Channels, (long)index * s.Frames, buffer, cancelled);
                    if (s.Cancel) return;
                    foreach (float value in buffer) if (!Finite(value)) throw new InvalidOperationException("nonfinite mix");
#if DEV || AUTOTEST
                    Volatile.Write(ref s.ObservedHashes[index], Fingerprint(buffer));
#endif
                    s.Ready.Enqueue(new Rendered { Index = index++, Pcm = buffer });
                    Interlocked.Increment(ref s.Rendered);
                }
            }
            catch (Exception ex) { s.Error = ex.GetType().Name + ": " + ex.Message; }
        }

        public static void BeforeUpdate(scrConductor __instance)
        {
            var s = current;
            if (s == null || s.Conductor != __instance || !s.Taken) return;
            try
            {
                if (!enabled || Data(__instance) != s.Original || s.Original.Count != s.Times.Length || __instance.song == null || __instance.song.pitch != s.Pitch ||
                    __instance.dspTimeSong != s.SongClock || __instance.hitSoundVolume != s.HitVolume || !GCS.d_hitsounds ||
                    (__instance.hitSoundGroup != null ? __instance.hitSoundGroup : ADOBase.audioManager.fallbackMixerGroup) != s.Group ||
                    AudioSettings.outputSampleRate != s.OutputRate || s.Error != null)
                { fallbacks++; Release("runtime condition changed: " + (s.Error ?? "clock/volume/pitch/list/group"), true); return; }
                // Listener pause freezes DSP scheduling, including partially played segments.
                if (!AudioListener.pause) Pump(s);
            }
            catch (Exception ex) { fallbacks++; Release("pump fallback: " + ex.Message, true); }
        }
        private static void Pump(Session s)
        {
            if (uploadFrame != Time.frameCount) { uploadFrame = Time.frameCount; uploadsThisFrame = 0; }
            lastUploads = uploadsThisFrame;
            double now = AudioSettings.dspTime;
            int request = Math.Min(s.Segments - 1, Math.Max(3, (int)Math.Floor((now + 6 - s.Origin) * s.Rate / s.Frames)));
            if (request > s.Requested) { Volatile.Write(ref s.Requested, request); s.Wake.Set(); }
            while (uploadsThisFrame < MaxUploads && s.Next < s.Segments)
            {
                double start = s.Origin + (double)s.Next * s.Frames / s.Rate;
                if (start > now + 6) break;
                // Fail before the deadline instead of silently omitting a late segment.
                if (start < now + 0.02) throw new InvalidOperationException("segment deadline missed");
                int slot = s.Next % PoolSize;
                if (s.Until[slot] > now - 0.05) break;
                if (!s.Ready.TryDequeue(out var result)) break;
                if (result.Index != s.Next) throw new InvalidOperationException("segment order");
                if (!s.Clips[slot].SetData(result.Pcm, 0)) throw new InvalidOperationException("SetData failed");
                s.Sources[slot].PlayScheduled(start);
                s.Until[slot] = start + (double)s.Frames / s.Rate;
                s.Free.Enqueue(result.Pcm); s.Wake.Set();
                s.Next++; s.Scheduled++; uploadsThisFrame++; lastUploads = uploadsThisFrame;
            }
            peakUploads = Math.Max(peakUploads, lastUploads);
        }

        private static void Release(string why, bool restore, bool replayTails = true)
        {
            reason = why;
            var s = current; current = null;
            if (s == null) return;
#if DEV || AUTOTEST
            lastVerification = VerificationState(s) + " (snapshot at release: " + why + ")";
#endif
            s.Cancel = true; s.Wake.Set();
            try
            {
                if (s.Sources != null) foreach (var source in s.Sources) if (source != null) source.Stop();
                if (restore && s.Taken && s.Conductor != null && Data(s.Conductor) == s.Original)
                {
                double now = AudioSettings.dspTime;
                int first = s.Original.Count;
                for (int i = 0; i < s.Original.Count; i++)
                    if ((double)VoiceTime.GetValue(s.Original[i]) >= now) { first = i; break; }
                Cursor(s.Conductor) = first;
                // Rebuild only tails which started before handback. Future entries remain
                // the original game's responsibility; no game sources are stopped here.
                if (replayTails && s.Original.Count == s.Times.Length && GCS.d_hitsounds && ADOBase.audioManager != null)
                    for (int i = 0; i < first; i++)
                    {
                        long offset = HitSoundMix.Sample(now - s.Times[i], s.Rate);
                        if (offset < 0 || offset >= s.Inputs[i].samples) continue;
                        object item = s.Original[i];
                        var source = AudioManager.Play("snd" + (HitSound)VoiceType.GetValue(item), now, s.Group, s.Volumes[i]);
                        if (source != null) source.timeSamples = (int)offset;
                    }
                }
            }
            catch (Exception ex) { reason += "; handback: " + ex.Message; }
            finally
            {
                if (s.Root != null) Object.Destroy(s.Root);
                if (s.Clips != null) foreach (var clip in s.Clips) if (clip != null) Object.Destroy(clip);
                // Cleanup waits outside the game thread, then drops every captured reference.
                retiring = s;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        if (s.Worker != null && (s.Worker.ThreadState & ThreadState.Unstarted) == 0) s.Worker.Join();
                        if (s.Verifier != null && (s.Verifier.ThreadState & ThreadState.Unstarted) == 0) s.Verifier.Join();
                    }
                    finally { s.Wake.Dispose(); Interlocked.CompareExchange(ref retiring, null, s); }
                });
            }
        }

#if DEV || AUTOTEST
        private static long Fingerprint(float[] pcm)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (float sample in pcm) hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(sample)) * 1099511628211UL;
                return hash == 0 ? 1 : (long)hash;
            }
        }
#endif

        internal static void Shutdown()
        {
            enabled = false;
            Release("unloaded", true);
            Compat.ReleaseHitSoundGuard();
            SceneManager.activeSceneChanged -= SceneChanged;
            AudioSettings.OnAudioConfigurationChanged -= AudioChanged;
            harmony?.UnpatchAll(Owner);
            installed = false;
        }

#if DEV || AUTOTEST
        internal static string Command(string arg)
        {
            var s = current;
            if (arg == "compare") return "PCM comparison only: listener output mixes song/holds/UI/mixer; isolated two-pass recording is not implemented. Use verify; audible A/B remains required.";
            if (arg == "verify")
            {
                if (s == null || !s.Taken) return "verify unavailable: " + reason + "; last=" + lastVerification;
                if (s.Verifier == null)
                {
                    s.Verification = "running";
                    s.Verifier = new Thread(() => VerifyAll(s)) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "StutterFix PCM verify" };
                    s.Verifier.Start();
                }
                return VerificationState(s);
            }
            int used = 0;
            if (s?.Until != null) foreach (var t in s.Until) if (t > AudioSettings.dspTime) used++;
            return "enabled=" + enabled + " reason=" + reason + " list=" + lastCount + " captures=" + captures +
                " segments=" + (s?.Segments ?? 0) + " rendered=" + (s?.Rendered ?? 0) + " scheduled=" + (s?.Scheduled ?? 0) +
                " pool=" + used + "/" + (s?.Sources?.Length ?? 0) + " SetData_last=" + lastUploads + " SetData_peak=" + peakUploads +
                " fallbacks=" + fallbacks + " PCM_bytes=" + (s?.PcmBytes ?? 0) + " quartz_guard=" + Compat.HitSoundGuardState;
        }

        private static string VerificationState(Session s)
        {
            int checkedHashes = 0, mismatches = 0;
            if (s.ObservedHashes != null) for (int i = 0; i < s.Segments; i++)
            {
                long observed = Volatile.Read(ref s.ObservedHashes[i]), expected = Volatile.Read(ref s.ExpectedHashes[i]);
                if (observed == 0 || expected == 0) continue;
                checkedHashes++; if (observed != expected) mismatches++;
            }
            return s.Verification + " segments=" + s.Verified + "/" + s.Segments + " max_error=" + s.MaxError + " differing_samples=" + s.Differences +
                " actual_PCM_hashes=" + checkedHashes + " hash_mismatches=" + mismatches + " pending_actual=" + (s.Segments - checkedHashes) +
                " max_time_error_samples=" + s.MaxTimeErrorSamples + " (PCM quantization); audio-output timing unmeasured";
        }

        private static void VerifyAll(Session s)
        {
            try
            {
                var actual = new float[s.Frames * s.Channels];
                var expected = new float[actual.Length];
                // Reconstruct starts independently from captured original absolute times.
                var voices = new HitSoundMix.Voice[s.Voices.Length];
                for (int i = 0; i < voices.Length; i++) voices[i] = new HitSoundMix.Voice {
                    Start = checked((long)Math.Round((s.Times[i] - s.Origin) * s.Rate, MidpointRounding.AwayFromZero)),
                    Pcm = s.Voices[i].Pcm, Gain = Math.Max(0, Math.Min(1, s.Volumes[i])) };
                for (int i = 0; i < voices.Length; i++)
                    s.MaxTimeErrorSamples = Math.Max(s.MaxTimeErrorSamples, Math.Abs(voices[i].Start - (s.Times[i] - s.Origin) * s.Rate));
                Func<bool> cancelled = () => s.Cancel;
                for (int i = 0; i < s.Segments && !s.Cancel; i++)
                {
                    HitSoundMix.Render(s.Voices, s.Channels, (long)i * s.Frames, actual, cancelled);
                    double error = HitSoundMix.Verify(voices, s.Channels, (long)i * s.Frames, actual, expected, cancelled, out var diff);
                    Volatile.Write(ref s.ExpectedHashes[i], Fingerprint(expected));
                    s.MaxError = Math.Max(s.MaxError, error); s.Differences += diff;
                    Volatile.Write(ref s.Verified, i + 1);
                }
                s.Verification = s.Cancel ? "cancelled" : "complete (pure PCM only)";
            }
            catch (Exception ex) { s.Verification = "failed: " + ex.Message; }
        }
#endif
    }
}
