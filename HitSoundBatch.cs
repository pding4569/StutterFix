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
        private const int MaxPool = 16, MaxGroups = 2, MaxUploads = 2, MaxVoices = 200000;
        private const long MaxUploadBytes = 768000; // Two 0.5s stereo buffers at 96kHz.
        private const long PcmLimit = 64L * 1024 * 1024;
        private static Harmony harmony;
        private static bool enabled, installed;
        private static Session current, retiring;
        private static string reason = "disabled";
        private static int captures, fallbacks, lastCount, lastUploads, peakUploads;
        private static int uploadFrame = -1, uploadsThisFrame;
        private static long uploadBytesThisFrame, lastUploadBytes, peakUploadBytes;
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

        // 곡 시작 때 히트음 수천 개를 훑는 반복문이 reflection GetValue 3번 + 문자열 이어붙이기로 HELLO 에서 약 45ms 걸렸다(2026-11-22 프로파일). 같은 필드를 읽는 작은 동적 메서드로 바꾼다.
        private static readonly Func<object, double> FastTime = MakeGetter<double>(VoiceTime);
        private static readonly Func<object, float> FastVolume = MakeGetter<float>(VoiceVolume);
        private static readonly Func<object, int> FastType = MakeGetter<int>(VoiceType);
        // 목록 전체를 한 번에 배열로: IList 인덱서가 항목마다 구조체를 상자에 담고(할당) 대리자 3번을 불러 Lost 13,189개에 약 8ms 걸렸다(2026-11-24).
        private static readonly Type VoiceListType = VoiceTime == null ? null : typeof(List<>).MakeGenericType(VoiceTime.DeclaringType);
        private static readonly Action<object, double[], float[], int[]> FastExtract = MakeExtract();
        private static Action<object, double[], float[], int[]> MakeExtract()
        {
            try
            {
                if (VoiceTime == null || VoiceVolume == null || VoiceType == null || VoiceListType == null) return null;
                if (VoiceTime.FieldType != typeof(double) || VoiceVolume.FieldType != typeof(float)) return null;
                var vt = VoiceType.FieldType.IsEnum ? Enum.GetUnderlyingType(VoiceType.FieldType) : VoiceType.FieldType;
                if (vt != typeof(int)) return null;
                var dm = new System.Reflection.Emit.DynamicMethod("sfhit_extract", null, new[] { typeof(object), typeof(double[]), typeof(float[]), typeof(int[]) }, typeof(HitSoundBatch).Module, true);
                var il = dm.GetILGenerator();
                var lst = il.DeclareLocal(VoiceListType); var i = il.DeclareLocal(typeof(int)); var n = il.DeclareLocal(typeof(int)); var tmp = il.DeclareLocal(VoiceTime.DeclaringType);
                var loop = il.DefineLabel(); var cond = il.DefineLabel();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Castclass, VoiceListType); il.Emit(System.Reflection.Emit.OpCodes.Stloc, lst);
                il.Emit(System.Reflection.Emit.OpCodes.Ldloc, lst); il.Emit(System.Reflection.Emit.OpCodes.Callvirt, VoiceListType.GetProperty("Count").GetGetMethod()); il.Emit(System.Reflection.Emit.OpCodes.Stloc, n);
                il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0); il.Emit(System.Reflection.Emit.OpCodes.Stloc, i);
                il.Emit(System.Reflection.Emit.OpCodes.Br, cond);
                il.MarkLabel(loop);
                il.Emit(System.Reflection.Emit.OpCodes.Ldloc, lst); il.Emit(System.Reflection.Emit.OpCodes.Ldloc, i); il.Emit(System.Reflection.Emit.OpCodes.Callvirt, VoiceListType.GetMethod("get_Item")); il.Emit(System.Reflection.Emit.OpCodes.Stloc, tmp);
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1); il.Emit(System.Reflection.Emit.OpCodes.Ldloc, i); il.Emit(System.Reflection.Emit.OpCodes.Ldloca, tmp); il.Emit(System.Reflection.Emit.OpCodes.Ldfld, VoiceTime); il.Emit(System.Reflection.Emit.OpCodes.Stelem_R8);
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_2); il.Emit(System.Reflection.Emit.OpCodes.Ldloc, i); il.Emit(System.Reflection.Emit.OpCodes.Ldloca, tmp); il.Emit(System.Reflection.Emit.OpCodes.Ldfld, VoiceVolume); il.Emit(System.Reflection.Emit.OpCodes.Stelem_R4);
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_3); il.Emit(System.Reflection.Emit.OpCodes.Ldloc, i); il.Emit(System.Reflection.Emit.OpCodes.Ldloca, tmp); il.Emit(System.Reflection.Emit.OpCodes.Ldfld, VoiceType); il.Emit(System.Reflection.Emit.OpCodes.Stelem_I4);
                il.Emit(System.Reflection.Emit.OpCodes.Ldloc, i); il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1); il.Emit(System.Reflection.Emit.OpCodes.Add); il.Emit(System.Reflection.Emit.OpCodes.Stloc, i);
                il.MarkLabel(cond);
                il.Emit(System.Reflection.Emit.OpCodes.Ldloc, i); il.Emit(System.Reflection.Emit.OpCodes.Ldloc, n); il.Emit(System.Reflection.Emit.OpCodes.Blt, loop);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                return (Action<object, double[], float[], int[]>)dm.CreateDelegate(typeof(Action<object, double[], float[], int[]>));
            }
            catch { return null; }
        }
        private static Func<object, T> MakeGetter<T>(FieldInfo f)
        {
            try
            {
                if (f == null) return null;
                var ft = f.FieldType.IsEnum ? Enum.GetUnderlyingType(f.FieldType) : f.FieldType;
                if (ft != typeof(T)) return null;
                var dm = new System.Reflection.Emit.DynamicMethod("sfhit_" + f.Name, typeof(T), new[] { typeof(object) }, typeof(HitSoundBatch).Module, true);
                var il = dm.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(f.DeclaringType.IsValueType ? System.Reflection.Emit.OpCodes.Unbox : System.Reflection.Emit.OpCodes.Castclass, f.DeclaringType);
                il.Emit(System.Reflection.Emit.OpCodes.Ldfld, f);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                return (Func<object, T>)dm.CreateDelegate(typeof(Func<object, T>));
            }
            catch { return null; }
        }

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
            internal int Pool;   // 형식당 소스 수: 예약 거리/구간 길이 + 2 (재생 중 1 + 꼬리 겹침 1). 켜 둔 소스도 엔진 오디오 갱신 비용이 든다
            internal IList Original;
            internal readonly List<FormatGroup> Formats = new List<FormatGroup>();
            internal AudioClip[] Inputs;
            internal double[] Times;
            internal float[] Volumes;
            internal double Origin, SongClock;
            internal float Pitch, HitVolume;
            internal AudioMixerGroup Group;
            internal int Rate, Frames, Segments, Requested = -1;
            internal long PcmBytes;
            internal GameObject Root;
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
#endif
            internal bool Taken;
            internal int OutputRate;
        }
        private sealed class FormatGroup
        {
            internal int Channels, Next;
            internal HitSoundMix.Voice[] Voices;
            internal readonly List<int> Indices = new List<int>();
            internal AudioSource[] Sources;
            internal AudioClip[] Clips;
            internal double[] Until;
            internal readonly ConcurrentQueue<float[]> Free = new ConcurrentQueue<float[]>();
            internal readonly ConcurrentQueue<Rendered> Ready = new ConcurrentQueue<Rendered>();
#if DEV || AUTOTEST
            internal long[] ObservedHashes, ExpectedHashes;
#endif
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
                PatchStop(typeof(AudioManager), "StopAllSounds", nameof(StopPrefix));
                PatchStop(typeof(scrConductor), "Rewind", nameof(StopPrefix));
                foreach (var name in new[] { "Restart", "OnPlayerDied", "FailAction", "Fail2Action", "QuitToMainMenu" })
                    PatchStop(typeof(scrController), name, nameof(StopPrefix));
                // 끝까지 깬 곡은 마지막 소리가 이어서 나야 하므로 남은 예약을 게임에 돌려준다.
                PatchStop(typeof(scrController), "OnLandOnPortal", nameof(PortalPrefix));
                PatchStop(typeof(scnEditor), "SwitchToEditMode", nameof(StopPrefix));
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

        private static void PatchStop(Type type, string name, string prefix)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (method.Name == name)
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(HitSoundBatch), prefix));
        }
        // 죽음/다시 하기/나가기: 이 판은 끝났으므로 아직 안 들려준 히트음을 게임에 돌려주면 안 된다.
        // (돌려주면 게임이 5초 앞 예약 창을 다시 채우고, 상태가 Fail 로 바뀌기 전 한 프레임 사이에 예약돼 죽은 뒤에도 소리가 난다. 2.8.1 수정)
        public static void StopPrefix() { Release("game stop/rebuild", true, false, false); }
        public static void PortalPrefix() { Release("level end", true, false); }
        private static void SceneChanged(Scene a, Scene b) { Release("scene changed", true, false); }
        private static void AudioChanged(bool changed) { Release("audio configuration changed", true, false); }
        // 곡 시작 준비 시간(시험용 기록): 게임 PlayHitTimes 전체, 그중 우리 Capture·첫 PCM 기다림·Pump
        private static long prepStart; internal static double PrepGameMs, PrepCaptureMs, PrepWaitMs, PrepPumpMs;
        public static void BeforePrepare(out bool __state)
        {
            prepStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Release("preparing", true, false);
            __state = enabled && Compat.EnsureHitSoundGuard();
        }

        public static void AfterPrepare(scrConductor __instance, bool __state)
        {
            if (!enabled) return;
            // A newly removed late-loaded Quartz postfix may still exist in THIS
            // invocation's old Harmony wrapper. Take over only on the next preparation.
            if (__state) { reason = "late Quartz detected: takeover deferred until next preparation"; return; }
            PrepGameMs = (System.Diagnostics.Stopwatch.GetTimestamp() - prepStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (DeferCapture && TryDefer(__instance)) return;
            TakeOver(__instance);
        }

        // 곡 시작 준비를 다음 프레임으로: 게임 PlayHitTimes(약 12ms)와 우리 준비(약 14ms)가 카운트다운 한 프레임에 겹쳐 Lost 29ms 였다(2026-11-24).
        // 이번 프레임에는 게임 예약 커서만 맡아 두고(게임이 예약하지 않게), 다음 scrConductor.Update 앞에서 준비한다.
        // 준비가 실패하면 커서를 0 으로 돌려 그 Update 에서 게임이 원래대로 예약한다(그 사이 지나간 히트음 없음: 첫 히트음은 카운트다운 뒤).
        internal static bool DeferCapture = true;
        internal static long Deferred, DeferredLate, DeferredCanceled;
        private static scrConductor pendingC; private static IList pendingList; private static int pendingFrame;
        private static bool TryDefer(scrConductor c)
        {
            try
            {
                var list = Data(c);
                if (!Compat.HitSoundGuardReady || Volatile.Read(ref retiring) != null || !GCS.d_hitsounds || !c.isGameWorld || ADOBase.lm == null || c.song == null ||
                    ADOBase.audioManager == null || list == null || list.Count == 0 || list.Count > MaxVoices || Cursor(c) != 0) return false;
                // 첫 히트음이 곧이면(편집기 중간 시작 등) 미루지 않는다: 미룬 동안 지나간 항목이 생기면 준비가 실패하고 게임이 늦게 예약한다
                double first = FastTime != null ? FastTime(list[0]) : (double)VoiceTime.GetValue(list[0]);
                if (!(first > AudioSettings.dspTime + 0.25)) return false;
                pendingC = c; pendingList = list; pendingFrame = Time.frameCount;
                Cursor(c) = list.Count;
                Deferred++; reason = "capture deferred to next frame";
                return true;
            }
            catch { pendingC = null; pendingList = null; return false; }
        }
        // 맡아 둔 것을 지금 준비한다(c 가 null 이면 어느 지휘자든: OnUpdate 안전망)
        private static void ResolvePending(scrConductor c)
        {
            var pc = pendingC;
            // 맡은 다음다음 프레임에: 게임은 StartMusicCo 끝과 DesyncFix(다음 프레임)에서 PlayHitTimes 를 두 번 부른다. 다음 프레임에 준비하면 두 번째 호출과 겹치고 버려진다.
            if ((object)pc == null || ((object)c != null && !ReferenceEquals(c, pc)) || Time.frameCount <= pendingFrame + 1) return;
            var list = pendingList; pendingC = null; pendingList = null;
            if (pc == null || Data(pc) != list) return;   // 게임이 목록을 새로 만들었으면 게임 커서도 새것
            Cursor(pc) = 0;                               // 준비는 새 목록(커서 0)을 요구한다. 실패하면 0 그대로 = 게임이 원래대로 예약
            TakeOver(pc);
        }
        internal static void LateResolve() { if ((object)pendingC != null && Time.frameCount > pendingFrame + 2) { DeferredLate++; ResolvePending(null); } }
        // 맡아 둔 채로 놓을 때(Release): 살아 있는 판이면 남은 히트음을 게임에 돌려주고, 죽음 등(giveBack=false)이면 돌려주지 않는다
        private static void CancelPending(bool restore, bool giveBack)
        {
            var pc = pendingC; var list = pendingList; pendingC = null; pendingList = null;
            if ((object)pc == null) return;
            DeferredCanceled++;
            try
            {
                if (!restore || pc == null || Data(pc) != list) return;
                int first = list.Count;
                if (giveBack) { double now = AudioSettings.dspTime; for (int i = 0; i < list.Count; i++) if ((FastTime != null ? FastTime(list[i]) : (double)VoiceTime.GetValue(list[i])) >= now) { first = i; break; } }
                Cursor(pc) = first;
            }
            catch { }
        }

        private static void TakeOver(scrConductor __instance)
        {
            Session s = null;
            try
            {
                if (!Compat.HitSoundGuardReady) throw new NotSupportedException("Quartz guard unavailable");
                if (Volatile.Read(ref retiring) != null) throw new NotSupportedException("previous worker cleanup pending");
                long c0 = System.Diagnostics.Stopwatch.GetTimestamp();
                s = Capture(__instance);
                long c1 = System.Diagnostics.Stopwatch.GetTimestamp(); PrepCaptureMs = (c1 - c0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                current = s;
                s.Worker = new Thread(() => Work(s)) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "StutterFix hit PCM" };
                s.Requested = Math.Min(s.Segments - 1, 3);
                s.Worker.Start();
                s.Wake.Set();
                // Preparation only. Never wait for PCM in an Update.
                var deadline = Environment.TickCount + 250;
                while (!InitialReady(s) && s.Error == null && unchecked(Environment.TickCount - deadline) < 0)
                    Thread.Sleep(1);
                if (s.Error != null || !InitialReady(s))
                    throw new NotSupportedException(s.Error ?? "initial PCM timeout");
                long c2 = System.Diagnostics.Stopwatch.GetTimestamp(); PrepWaitMs = (c2 - c1) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (s.Origin < AudioSettings.dspTime + 0.05) throw new NotSupportedException("insufficient initial DSP lead");
                Pump(s);
                PrepPumpMs = (System.Diagnostics.Stopwatch.GetTimestamp() - c2) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                foreach (var g in s.Formats)
                    if (g.Next == 0) throw new NotSupportedException("initial group schedule unavailable");
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
        // 클립 PCM 을 맵 준비(SoundWarm) 때 미리 풀어 두고 다시 하기에도 쓴다. 곡 시작 카운트다운 중 처음 쓰는 클립마다 약 0.7ms(Lost 8개 5.7ms) 걸렸다(2026-11-24).
        // PCM 배열은 읽기만 한다(HitSoundMix). 같은 클립의 GetData 결과는 같다.
        private sealed class ClipRef : IEqualityComparer<AudioClip>
        {
            public bool Equals(AudioClip a, AudioClip b) => ReferenceEquals(a, b);
            public int GetHashCode(AudioClip o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }
        private static readonly Dictionary<AudioClip, float[]> pcmWarm = new Dictionary<AudioClip, float[]>(new ClipRef());
        internal static long WarmHits, WarmMade;
        internal static void WarmPcm(AudioClip clip)
        {
            try
            {
                if (!enabled || clip == null || pcmWarm.ContainsKey(clip) || clip.loadType == AudioClipLoadType.Streaming) return;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                if (clip.loadState == AudioDataLoadState.Loading)
                {
                    int deadline = Environment.TickCount + 200;
                    while (clip.loadState == AudioDataLoadState.Loading && unchecked(Environment.TickCount - deadline) < 0) Thread.Sleep(1);
                }
                if (clip.loadState != AudioDataLoadState.Loaded || clip.samples <= 0 || clip.channels < 1 || clip.channels > 2 || (double)clip.samples / Math.Max(1, clip.frequency) > 8) return;
                long count = (long)clip.samples * clip.channels;
                if (count * 4 > PcmLimit) return;
                var pcm = new float[(int)count];
                if (!clip.GetData(pcm, 0)) return;
                foreach (float x in pcm) if (!Finite(x)) return;
                if (pcmWarm.Count >= 128) pcmWarm.Clear();
                pcmWarm[clip] = pcm; WarmMade++;
            }
            catch { }
        }
        internal static void ClearWarm() { pcmWarm.Clear(); }
        internal static bool CaptureFastSame = true;
        internal static long PrepSwitches, PrepSwitchTicks, PrepNewClips, PrepNewTicks;   // 시험용 끄기(sfield): 원래 조회 방식과 결과 대조
        private static long capStart; internal static double PrepHeadMs, PrepLoopMs, PrepVoiceMs;
        private static Session Capture(scrConductor c)
        {
            capStart = System.Diagnostics.Stopwatch.GetTimestamp();
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
            // 유니티 AudioSource 기본값(playOnAwake=true, reverbZoneMix=1)은 클립 없이 만들어지고 장면에 AudioReverbZone 이 없으면 소리에 영향이 없다(2026-10-20 실게임 확인: 게임 프리팹이 기본값 그대로).
            if (prefab == null || prefab.loop || prefab.spatialBlend != 0 || prefab.spatialize || prefab.panStereo != 0 ||
                prefab.ignoreListenerPause || prefab.ignoreListenerVolume || UnityEngine.Object.FindObjectsOfType<AudioReverbZone>().Length != 0 ||
                prefab.gameObject.GetComponents<Component>().Length != 2 || prefab.transform.childCount != 0)
                throw new NotSupportedException("source has 3D/loop/pan/reverb/filter or custom components [" + (prefab == null ? "null" :
                    "playOnAwake=" + prefab.playOnAwake + " loop=" + prefab.loop + " spatialBlend=" + prefab.spatialBlend + " spatialize=" + prefab.spatialize +
                    " pan=" + prefab.panStereo + " ignorePause=" + prefab.ignoreListenerPause + " ignoreVolume=" + prefab.ignoreListenerVolume +
                    " reverb=" + prefab.reverbZoneMix + " comps=" + string.Join("+", System.Array.ConvertAll(prefab.gameObject.GetComponents<Component>(), x => x == null ? "null" : x.GetType().Name)) +
                    " children=" + prefab.transform.childCount) + "]");
            var s = new Session { Conductor = c, Original = list, Origin = double.PositiveInfinity,
                SongClock = c.dspTimeSong, Pitch = c.song.pitch, HitVolume = c.hitSoundVolume,
                Group = c.hitSoundGroup != null ? c.hitSoundGroup : am.fallbackMixerGroup,
                Inputs = new AudioClip[list.Count],
                Times = new double[list.Count], Volumes = new float[list.Count] };
            // Return partially created objects to the common exception/cleanup path.
            current = s;
            if (!Finite(s.Pitch) || s.Pitch <= 0 || !Finite(s.SongClock)) throw new NotSupportedException("invalid clock/pitch");
            var cache = new Dictionary<AudioClip, float[]>();
            double end = double.NegativeInfinity;
            var clipByType = new Dictionary<int, AudioClip>();
            var knownClip = new Dictionary<AudioClip, KeyValuePair<int, double>>(); // 이미 검사한 클립: (형식 묶음 번호, 길이 초)
            double dspNow = AudioSettings.dspTime;
            // 연속된 히트음은 대부분 같은 종류다: 직전 종류의 (클립, 묶음, 길이, PCM)을 기억해 사전 조회와 유니티 객체 비교를 건너뛴다.
            // 기억은 검사를 통과한(null 아닌) 클립만이고 종류→클립, 클립→묶음은 한 번 정해지면 바뀌지 않아 결과가 같다.
            // (Lost 13,189개: 목록 훑기 7.7ms, 2026-11-24)
            int lastType = int.MinValue; AudioClip lastClip = null; KeyValuePair<int, double> lastKnown = default; float[] lastPcm = null;
            var pcmOf = new float[list.Count][];
            double[] xTime = null; float[] xVol = null; int[] xType = null;
            if (CaptureFastSame && FastExtract != null && list.GetType() == VoiceListType)
            {
                xTime = new double[list.Count]; xVol = new float[list.Count]; xType = new int[list.Count];
                FastExtract(list, xTime, xVol, xType);
            }
            long q0 = System.Diagnostics.Stopwatch.GetTimestamp(); PrepHeadMs = (q0 - capStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            for (int i = 0; i < list.Count; i++)
            {
                object item = xTime != null ? null : list[i];
                double time = xTime != null ? xTime[i] : FastTime != null ? FastTime(item) : (double)VoiceTime.GetValue(item);
                float volume = xTime != null ? xVol[i] : FastVolume != null ? FastVolume(item) : (float)VoiceVolume.GetValue(item);
                if (!Finite(time) || !Finite(volume) || time < dspNow || (i > 0 && time < s.Times[i - 1]))
                    throw new NotSupportedException("invalid/past/unsorted event");
                int typeId = xTime != null ? xType[i] : FastType != null ? FastType(item) : (int)(HitSound)VoiceType.GetValue(item);
                if (CaptureFastSame && typeId == lastType)
                {
                    s.Formats[lastKnown.Key].Indices.Add(i);
                    s.Inputs[i] = lastClip; pcmOf[i] = lastPcm; s.Times[i] = time; s.Volumes[i] = volume;
                    if (time < s.Origin) s.Origin = time;
                    double sameEnd = time + lastKnown.Value;
                    if (sameEnd > end) end = sameEnd;
                    continue;
                }
                long sl0 = System.Diagnostics.Stopwatch.GetTimestamp(); PrepSwitches++;
                AudioClip clip;
                if (!clipByType.TryGetValue(typeId, out clip))
                {
                    clip = am.FindOrLoadAudioClip("snd" + (HitSound)typeId);
                    clipByType[typeId] = clip;
                }
                KeyValuePair<int, double> known;
                if (clip != null && knownClip.TryGetValue(clip, out known))
                {
                    s.Formats[known.Key].Indices.Add(i);
                    s.Inputs[i] = clip; s.Times[i] = time; s.Volumes[i] = volume;
                    if (time < s.Origin) s.Origin = time;
                    double fastEnd = time + known.Value;
                    if (fastEnd > end) end = fastEnd;
                    lastType = typeId; lastClip = clip; lastKnown = known; lastPcm = pcmOf[i] = cache[clip];
                    PrepSwitchTicks += System.Diagnostics.Stopwatch.GetTimestamp() - sl0;
                    continue;
                }
                // 게임 히트음은 CompressedInMemory 이고 처음 쓰기 전에는 Unloaded 다(2026-10-20 실게임 확인). 스트리밍만 막고, 안 불러온 클립은 짧게 불러온다.
                if (clip != null && clip.loadType != AudioClipLoadType.Streaming && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                if (clip != null && clip.loadState == AudioDataLoadState.Loading)
                {
                    int loadDeadline = Environment.TickCount + 200;
                    while (clip.loadState == AudioDataLoadState.Loading && unchecked(Environment.TickCount - loadDeadline) < 0) Thread.Sleep(1);
                }
                if (clip == null || clip.loadType == AudioClipLoadType.Streaming || clip.loadState != AudioDataLoadState.Loaded ||
                    clip.samples <= 0 || clip.frequency < 8000 || clip.frequency > 96000 || clip.channels < 1 || clip.channels > 2 ||
                    (double)clip.samples / clip.frequency > 8)
                    throw new NotSupportedException("unreadable/streaming/compressed/long clip [" + (clip == null ? "null" : clip.name + " loadType=" + clip.loadType + " loadState=" + clip.loadState + " samples=" + clip.samples + " freq=" + clip.frequency + " ch=" + clip.channels + " sec=" + ((double)clip.samples / System.Math.Max(1, clip.frequency)).ToString("F2")) + "]");
                if (s.Rate == 0) s.Rate = clip.frequency;
                HitSoundMix.RequireFormat(s.Rate, clip.frequency, clip.channels);
                int groupIndex = -1;
                for (int gi = 0; gi < s.Formats.Count; gi++)
                    if (s.Formats[gi].Channels == clip.channels) { groupIndex = gi; break; }
                if (groupIndex < 0)
                {
                    if (s.Formats.Count >= MaxGroups) throw new NotSupportedException("format group limit");
                    groupIndex = s.Formats.Count;
                    s.Formats.Add(new FormatGroup { Channels = clip.channels });
                }
                s.Formats[groupIndex].Indices.Add(i);
                if (!cache.TryGetValue(clip, out var pcm))
                {
                    long count = (long)clip.samples * clip.channels;
                    if (cache.Count >= 64 || s.PcmBytes + count * 4 > PcmLimit) throw new NotSupportedException("PCM cache limit");
                    if (CaptureFastSame && pcmWarm.TryGetValue(clip, out pcm) && pcm.Length == count) WarmHits++;
                    else
                    {
                        pcm = new float[(int)count];
                        if (!clip.GetData(pcm, 0)) throw new NotSupportedException("GetData failed");
                        foreach (float sample in pcm) if (!Finite(sample)) throw new NotSupportedException("nonfinite PCM");
                        if (CaptureFastSame) { if (pcmWarm.Count >= 128) pcmWarm.Clear(); pcmWarm[clip] = pcm; }
                    }
                    cache.Add(clip, pcm); s.PcmBytes += count * 4;
                }
                s.Inputs[i] = clip; s.Times[i] = time; s.Volumes[i] = volume;
                s.Origin = Math.Min(s.Origin, time);
                end = Math.Max(end, time + (double)clip.samples / clip.frequency);
                knownClip[clip] = new KeyValuePair<int, double>(groupIndex, (double)clip.samples / clip.frequency);
                pcmOf[i] = pcm;
                lastType = typeId; lastClip = clip; lastKnown = knownClip[clip]; lastPcm = pcm;
                PrepNewClips++; PrepNewTicks += System.Diagnostics.Stopwatch.GetTimestamp() - sl0;
            }
            long q1 = System.Diagnostics.Stopwatch.GetTimestamp(); PrepLoopMs = (q1 - q0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            s.Frames = s.Rate / 2;
            s.Origin -= (double)HitSoundMix.LeadFrames / s.Rate;
            if (end - s.Origin > 7200) throw new NotSupportedException("duration limit (2 hours)");
            long total = 0;
            foreach (var g in s.Formats)
            {
                g.Voices = new HitSoundMix.Voice[g.Indices.Count];
                for (int j = 0; j < g.Voices.Length; j++)
                {
                    int i = g.Indices[j];
                    var v = new HitSoundMix.Voice { Start = HitSoundMix.Sample(s.Times[i] - s.Origin, s.Rate),
                        Pcm = CaptureFastSame ? pcmOf[i] : cache[s.Inputs[i]], Gain = Mathf.Clamp01(s.Volumes[i]) };
                    g.Voices[j] = v;
                    total = Math.Max(total, v.Start + v.Pcm.Length / g.Channels);
                }
            }
            s.Segments = checked((int)((total + s.Frames - 1) / s.Frames));
            s.OutputRate = AudioSettings.outputSampleRate;
            int clipFrames = s.Frames + HitSoundMix.PadFrames;
            s.Pool = Math.Min(MaxPool, (int)Math.Ceiling(Lookahead * s.Rate / s.Frames) + 2);
            // Count managed reusable buffers AND native AudioClip PCM conservatively.
            long poolBytes = 0;
            foreach (var g in s.Formats) poolBytes += (long)s.Pool * clipFrames * g.Channels * 4 * 2;
            if (s.PcmBytes + poolBytes > PcmLimit) throw new NotSupportedException("PCM plus pool limit");
            s.PcmBytes += poolBytes;
            long q2 = System.Diagnostics.Stopwatch.GetTimestamp(); PrepVoiceMs = (q2 - q1) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            s.Root = new GameObject("StutterFix hit sound batch");
            s.Root.transform.SetParent(c.transform, false);
            foreach (var g in s.Formats)
            {
#if DEV || AUTOTEST
                g.ObservedHashes = new long[s.Segments]; g.ExpectedHashes = new long[s.Segments];
#endif
                g.Sources = new AudioSource[s.Pool]; g.Clips = new AudioClip[s.Pool]; g.Until = new double[s.Pool];
                for (int i = 0; i < s.Pool; i++)
                {
                    g.Sources[i] = Object.Instantiate(prefab, s.Root.transform);
                    g.Sources[i].Stop(); g.Sources[i].playOnAwake = false;
                    g.Sources[i].pitch = 1; g.Sources[i].volume = 1; g.Sources[i].priority = 128;
                    g.Sources[i].outputAudioMixerGroup = s.Group;
                    g.Clips[i] = AudioClip.Create("StutterFix PCM " + g.Channels + "ch " + i, clipFrames, g.Channels, s.Rate, false);
                    g.Sources[i].clip = g.Clips[i];
                    g.Free.Enqueue(new float[clipFrames * g.Channels]);
                }
            }
            return s;
        }

        private static bool InitialReady(Session s)
        {
            foreach (var g in s.Formats) if (g.Ready.Count < Math.Min(2, s.Segments)) return false;
            return true;
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
                    bool available = true;
                    foreach (var g in s.Formats) if (g.Free.IsEmpty) available = false;
                    if (index > Volatile.Read(ref s.Requested) || !available) { s.Wake.WaitOne(); continue; }
                    foreach (var g in s.Formats)
                    {
                        if (!g.Free.TryDequeue(out var buffer)) throw new InvalidOperationException("group buffer unavailable");
                        HitSoundMix.RenderSegment(g.Voices, g.Channels, index, s.Segments, s.Frames, buffer, cancelled);
                        if (s.Cancel) return;
                        foreach (float value in buffer) if (!Finite(value)) throw new InvalidOperationException("nonfinite mix");
#if DEV || AUTOTEST
                        Volatile.Write(ref g.ObservedHashes[index], Fingerprint(buffer));
#endif
                        g.Ready.Enqueue(new Rendered { Index = index, Pcm = buffer });
                        Interlocked.Increment(ref s.Rendered);
                    }
                    index++;
                }
            }
            catch (Exception ex) { s.Error = ex.GetType().Name + ": " + ex.Message; }
        }

        public static void BeforeUpdate(scrConductor __instance)
        {
            if ((object)pendingC != null) ResolvePending(__instance);
            var s = current;
            if (s == null || s.Conductor != __instance || !s.Taken) return;
            try
            {
                // 죽음·실패·완주 상태이거나 게임 월드가 아니면 남은 예약을 놓고 게임에 돌려주지 않는다(FailAction 이 Update 안 UpdateInput 에서 불리는 틈 보호).
                var runCtrl = ADOBase.controller;
                if (!__instance.isGameWorld || (runCtrl != null && (runCtrl.state == States.Fail || runCtrl.state == States.Fail2)))
                { Release("run ended", true, false, false); return; }
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
        // 몇 초 앞까지 예약할지. 예약된 소스도 엔진 오디오 갱신(AudioManager::Update)에서 하나씩 비용이 든다(소스 하나 약 0.13%).
        private static double Lookahead => Main.Config == null ? 2 : Mathf.Clamp(Main.Config.HitSoundLookaheadSec, 1f, 6f);
        private static void Pump(Session s)
        {
            if (uploadFrame != Time.frameCount) { uploadFrame = Time.frameCount; uploadsThisFrame = 0; uploadBytesThisFrame = 0; }
            lastUploads = uploadsThisFrame;
            lastUploadBytes = uploadBytesThisFrame;
            double now = AudioSettings.dspTime;
            double ahead = Math.Max(now, s.Origin) + Lookahead;   // 곡 시작 전(카운트다운)에도 첫 구간부터 예약되게 곡 시작 기준
            int request = Math.Min(s.Segments - 1, Math.Max(3, (int)Math.Floor((ahead - s.Origin) * s.Rate / s.Frames)));
            if (request > s.Requested) { Volatile.Write(ref s.Requested, request); s.Wake.Set(); }
            while (uploadsThisFrame < MaxUploads)
            {
                // Earliest segment first, then stable group order. Both formats use
                // the exact same integer sample boundary and DSP start expression.
                FormatGroup g = null;
                foreach (var candidate in s.Formats)
                    if (candidate.Next < s.Segments && (g == null || candidate.Next < g.Next)) g = candidate;
                if (g == null) break;
                double start = s.Origin + (double)g.Next * s.Frames / s.Rate;
                if (start > ahead) break;
                // Fail before the deadline instead of silently omitting a late segment.
                if (start < now + 0.02) throw new InvalidOperationException("segment deadline missed");
                long bytes = (long)(s.Frames + HitSoundMix.PadFrames) * g.Channels * 4;
                if (uploadBytesThisFrame + bytes > MaxUploadBytes) break;
                int slot = g.Next % s.Pool;
                if (g.Until[slot] > now - 0.05) break;
                if (!g.Ready.TryDequeue(out var result)) break;
                if (result.Index != g.Next) throw new InvalidOperationException("segment order");
                g.Sources[slot].Stop();
                if (!g.Clips[slot].SetData(result.Pcm, 0)) throw new InvalidOperationException("SetData failed");
                g.Sources[slot].PlayScheduled(start);
                g.Until[slot] = start + (double)(s.Frames + HitSoundMix.PadFrames) / s.Rate;
                g.Free.Enqueue(result.Pcm); s.Wake.Set();
                g.Next++; s.Scheduled++; uploadsThisFrame++; lastUploads = uploadsThisFrame;
                uploadBytesThisFrame += bytes; lastUploadBytes = uploadBytesThisFrame;
            }
            peakUploads = Math.Max(peakUploads, lastUploads);
            peakUploadBytes = Math.Max(peakUploadBytes, lastUploadBytes);
        }

        private static void Release(string why, bool restore, bool replayTails = true, bool giveBack = true)
        {
            if ((object)pendingC != null) CancelPending(restore, giveBack);
            reason = why;
            var s = current; current = null;
            if (s == null) return;
#if DEV || AUTOTEST
            lastVerification = VerificationState(s) + " (snapshot at release: " + why + ")";
#endif
            s.Cancel = true; s.Wake.Set();
            try
            {
                foreach (var g in s.Formats)
                    if (g.Sources != null) foreach (var source in g.Sources) if (source != null) { source.Stop(); source.clip = null; source.enabled = false; } // Destroy 는 프레임 끝이라 예약 채널을 바로 끊는다
                if (s.Root != null) s.Root.SetActive(false);
                if (restore && s.Taken && s.Conductor != null && Data(s.Conductor) == s.Original)
                {
                double now = AudioSettings.dspTime;
                int first = s.Original.Count;
                if (giveBack)
                    for (int i = 0; i < s.Original.Count; i++)
                        // 곡 끝에는 모두 지나 전체를 훑는다: 리플렉션 GetValue 로 Lost 1만 3천 개 곡 끝 프레임 28ms(2026-11-24 dropscan6).
                        // 준비 때 같은 순서로 복사한 시각(s.Times, 목록 길이 같음)을 쓴다. 목록 시각은 그 뒤 바뀌지 않는다(게임 예약 커서는 끝에 있음).
                        if ((s.Times.Length == s.Original.Count ? s.Times[i] : FastTime != null ? FastTime(s.Original[i]) : (double)VoiceTime.GetValue(s.Original[i])) >= now) { first = i; break; }
                Cursor(s.Conductor) = first;
                // Rebuild only tails which started before handback. Future entries remain
                // the original game's responsibility; no game sources are stopped here.
                if (replayTails && s.Original.Count == s.Times.Length && GCS.d_hitsounds && ADOBase.audioManager != null)
                    for (int i = 0; i < first; i++)
                    {
                        long offset = HitSoundMix.Sample(now - s.Times[i], s.Inputs[i].frequency);
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
                foreach (var g in s.Formats)
                    if (g.Clips != null) foreach (var clip in g.Clips) if (clip != null) Object.Destroy(clip);
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
            if (arg == "voicehash")
            {
                if (s == null || !s.Taken) return "voicehash unavailable: " + reason;
                var sb = new System.Text.StringBuilder("voicehash");
                var pcmHash = new Dictionary<float[], long>();
                foreach (var g in s.Formats)
                {
                    long h = 1469598103934665603L;
                    foreach (var v in g.Voices)
                    {
                        long ph;
                        if (!pcmHash.TryGetValue(v.Pcm, out ph)) { ph = 1469598103934665603L; foreach (float x in v.Pcm) ph = (ph ^ BitConverter.ToInt32(BitConverter.GetBytes(x), 0)) * 1099511628211L; pcmHash[v.Pcm] = ph; }
                        h = (h ^ v.Start) * 1099511628211L; h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(v.Gain), 0)) * 1099511628211L; h = (h ^ ph) * 1099511628211L;
                    }
                    sb.Append(" ch").Append(g.Channels).Append(":n=").Append(g.Voices.Length).Append(":h=").Append(h.ToString("x16"));
                }
                return sb.Append(" segments=").Append(s.Segments).Append(" pcm_arrays=").Append(pcmHash.Count).ToString();
            }
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
            int used = 0, pool = 0;
            string formats = "";
            if (s != null) foreach (var g in s.Formats)
            {
                if (g.Until != null) foreach (var t in g.Until) if (t > AudioSettings.dspTime) used++;
                pool += g.Sources?.Length ?? 0;
                formats += (formats.Length == 0 ? "" : ",") + s.Rate + "Hz/" + g.Channels + "ch:next=" + g.Next;
            }
            return "deferred=" + Deferred + "/late " + DeferredLate + "/canceled " + DeferredCanceled + " hold_none=" + FfxReuse.HitTimesNoHoldRuns + " checked=" + FfxReuse.HitTimesChecked + " wrong=" + FfxReuse.HitTimesWrong + " prep_ms game=" + PrepGameMs.ToString("F1") + " capture=" + PrepCaptureMs.ToString("F1") + " wait=" + PrepWaitMs.ToString("F1") + " pump=" + PrepPumpMs.ToString("F1") + " (head=" + PrepHeadMs.ToString("F1") + " loop=" + PrepLoopMs.ToString("F1") + " voices=" + PrepVoiceMs.ToString("F1") + " switches=" + PrepSwitches + "/" + (PrepSwitchTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("F1") + "ms warm=" + WarmHits + "/" + WarmMade + " new=" + PrepNewClips + "/" + (PrepNewTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("F1") + "ms) " +
                "enabled=" + enabled + " reason=" + reason + " list=" + lastCount + " captures=" + captures +
                " groups=" + (s?.Formats.Count ?? 0) + " formats=" + formats +
                " segments=" + (s?.Segments ?? 0) + " rendered=" + (s?.Rendered ?? 0) + " scheduled=" + (s?.Scheduled ?? 0) +
                " pool=" + used + "/" + pool + " SetData_last=" + lastUploads + " SetData_peak=" + peakUploads +
                " SetData_bytes_last=" + lastUploadBytes + " SetData_bytes_peak=" + peakUploadBytes +
                " fallbacks=" + fallbacks + " PCM_bytes=" + (s?.PcmBytes ?? 0) + " quartz_guard=" + Compat.HitSoundGuardState;
        }

        private static string VerificationState(Session s)
        {
            int checkedHashes = 0, mismatches = 0;
            foreach (var g in s.Formats)
            if (g.ObservedHashes != null) for (int i = 0; i < s.Segments; i++)
            {
                long observed = Volatile.Read(ref g.ObservedHashes[i]), expected = Volatile.Read(ref g.ExpectedHashes[i]);
                if (observed == 0 || expected == 0) continue;
                checkedHashes++; if (observed != expected) mismatches++;
            }
            return s.Verification + " group_segments=" + s.Verified + "/" + (s.Segments * s.Formats.Count) + " groups=" + s.Formats.Count + " max_error=" + s.MaxError + " differing_samples=" + s.Differences +
                " actual_PCM_hashes=" + checkedHashes + " hash_mismatches=" + mismatches + " pending_actual=" + (s.Segments * s.Formats.Count - checkedHashes) +
                " group_alignment_samples=0 (shared rate/origin/boundaries)" +
                " max_time_error_samples=" + s.MaxTimeErrorSamples + " (PCM quantization); audio-output timing unmeasured";
        }

        private static void VerifyAll(Session s)
        {
            try
            {
                foreach (var g in s.Formats)
                {
                var actual = new float[s.Frames * g.Channels];
                var expected = new float[actual.Length];
                var segmented = new float[(s.Frames + HitSoundMix.PadFrames) * g.Channels];
                // Reconstruct starts independently from captured original absolute times.
                var voices = new HitSoundMix.Voice[g.Voices.Length];
                for (int i = 0; i < voices.Length; i++)
                {
                    int original = g.Indices[i];
                    voices[i] = new HitSoundMix.Voice {
                        Start = checked((long)Math.Round((s.Times[original] - s.Origin) * s.Rate, MidpointRounding.AwayFromZero)),
                        Pcm = g.Voices[i].Pcm, Gain = Math.Max(0, Math.Min(1, s.Volumes[original])) };
                }
                for (int i = 0; i < voices.Length; i++)
                    s.MaxTimeErrorSamples = Math.Max(s.MaxTimeErrorSamples, Math.Abs(voices[i].Start - (s.Times[g.Indices[i]] - s.Origin) * s.Rate));
                Func<bool> cancelled = () => s.Cancel;
                for (int i = 0; i < s.Segments && !s.Cancel; i++)
                {
                    HitSoundMix.Render(g.Voices, g.Channels, (long)i * s.Frames, actual, cancelled);
                    double error = HitSoundMix.Verify(voices, g.Channels, (long)i * s.Frames, actual, expected, cancelled, out var diff);
                    HitSoundMix.RenderSegment(voices, g.Channels, i, s.Segments, s.Frames, segmented, cancelled);
                    Volatile.Write(ref g.ExpectedHashes[i], Fingerprint(segmented));
                    s.MaxError = Math.Max(s.MaxError, error); s.Differences += diff;
                    Interlocked.Increment(ref s.Verified);
                }
                }
                s.Verification = s.Cancel ? "cancelled" : "complete (pure PCM only)";
            }
            catch (Exception ex) { s.Verification = "failed: " + ex.Message; }
        }
#endif
    }
}
