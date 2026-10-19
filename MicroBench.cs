#if DEV || AUTOTEST
using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Jobs;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StutterFix
{
    // Only AutoTest calls Step; no patches, game decorations, or playback hooks.
    internal static class MicroBench
    {
        private static Runner active;
        private static bool finished;
        private static int finishedFrame;
        private const int Warmup = 60;
        private static readonly string[] MaterialCases = {
            "SetFloat(string)/same", "SetFloat(id)/same", "SetFloat(string)/vary", "SetFloat(id)/vary",
            "SetColor(string)/same", "SetColor(id)/same", "SetColor(string)/vary", "SetColor(id)/vary",
            "SetFloat(string)/missing/vary"
        };
        private static void Log(string s) { Main.Entry.Logger.Log("[마이크로벤치] " + s); }
        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

        internal static bool Step(string command, string arg)
        {
            if (finished)
            {
                // Destroy is deferred: don't advance AutoTest until the following frame.
                if (Time.frameCount <= finishedFrame) return false;
                finished = false;
                return true;
            }
            if (active != null) return false;
            if (Hitch.Playing) { Log(command + ": 거부 — 곡 재생 중"); return true; }
            var args = arg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int n = command == "animbench" ? 2000 : 20000, frames = 300;
            string mode = args.Length > 2 ? args[2].ToLowerInvariant() : "main";
            int maxN = command == "animbench" ? 50000 : 1000000;
            if (args.Length > (command == "animbench" ? 3 : 2) ||
                (args.Length > 0 && (!int.TryParse(args[0], out n) || n < 1 || n > maxN)) ||
                (args.Length > 1 && (!int.TryParse(args[1], out frames) || frames < 1 || frames > 10000)) ||
                (command == "animbench" && mode != "main" && mode != "mainsep" && mode != "job" && mode != "jobrot" && mode != "anim"))
            {
                Log(command + ": 인수 오류. animbench [N=2000] [프레임=300] [main|mainsep|job|jobrot|anim], matbench [N=20000] [프레임=300]; N<= " + maxN + ", 프레임<=10000");
                return true;
            }
            var go = new GameObject("StutterFix MicroBench");
            try
            {
                active = go.AddComponent<Runner>();
                active.Init(command, n, frames, mode);
            }
            catch (Exception ex)
            {
                Log(command + ": 시작 실패/지원 불가 " + ex);
                if (active != null) active.Stop(); else Object.Destroy(go);
            }
            return false;
        }

        internal static void Cancel()
        {
            if (active != null) { Log("진행 중 시험 취소"); active.Stop(); }
            finished = false;
        }

        private struct TransformJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<Vector3> Positions, Scales;
            [ReadOnly] public NativeArray<Quaternion> Rotations;
            [ReadOnly] public NativeArray<float> Angles;
            public bool CalculateRotation;
            public void Execute(int i, TransformAccess t)
            {
                t.localPosition = Positions[i];
                t.localRotation = CalculateRotation ? Quaternion.Euler(0, 0, Angles[i]) : Rotations[i];
                t.localScale = Scales[i];
            }
        }

        private struct StreamJob : IAnimationJob
        {
            [ReadOnly] public NativeArray<TransformStreamHandle> Handles;
            [ReadOnly] public NativeArray<Vector3> Positions, Scales;
            [ReadOnly] public NativeArray<Quaternion> Rotations;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                for (int i = 0; i < Handles.Length; i++)
                {
                    var h = Handles[i];
                    h.SetLocalPosition(stream, Positions[i]);
                    h.SetLocalRotation(stream, Rotations[i]);
                    h.SetLocalScale(stream, Scales[i]);
                }
            }
        }

        // A temporary scene component supplies Update/LateUpdate without a Harmony patch.
        public sealed class Runner : MonoBehaviour
        {
            private string command, mode, floatName, colorName;
            private int n, frames, frame, variant, sceneHandle, floatId, colorId, missingId;
            private bool initialized, stopped, outstanding, samplePending, probed, animVerified;
            private Transform[] targets;
            private Texture2D texture;
            private Sprite sprite;
            private Material material;
            private NativeArray<Vector3> positions, scales;
            private NativeArray<Quaternion> rotations;
            private NativeArray<float> angles;
            private NativeArray<TransformStreamHandle> handles;
            private TransformAccessArray access;
            private JobHandle pending;
            private PlayableGraph graph;
            private AnimationScriptPlayable playable;
            private long start, prepTicks, writeTicks, completeTicks;
            private double[] prepSamples, writeSamples, completeSamples, totalSamples, frameSamples;

            internal void Init(string cmd, int count, int samples, string method)
            {
                command = cmd; n = count; frames = samples; mode = method;
                sceneHandle = gameObject.scene.handle;
                prepSamples = new double[frames]; writeSamples = new double[frames];
                completeSamples = new double[frames]; totalSamples = new double[frames]; frameSamples = new double[frames];
                if (cmd == "animbench") InitTransforms(); else InitMaterial();
                initialized = true;
                Log(cmd + " 시작 N=" + n + " 측정=" + frames + "프레임/방식, 워밍업=" + Warmup +
                    (cmd == "animbench" ? " 방식=" + mode + " 카메라=" + Camera.allCamerasCount : " 셰이더=" + material.shader.name + " float=" + floatName + " color=" + colorName));
            }

            private void InitTransforms()
            {
                if (Camera.allCamerasCount == 0) throw new InvalidOperationException("활성 카메라가 있는 장면 필요");
                texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white); texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
                targets = new Transform[n];
                for (int i = 0; i < n; i++)
                {
                    var child = new GameObject("microbench-" + i);
                    child.transform.SetParent(transform, false);
                    targets[i] = child.transform;
                    child.AddComponent<SpriteRenderer>().sprite = sprite;
                }
                positions = new NativeArray<Vector3>(n, Allocator.Persistent);
                scales = new NativeArray<Vector3>(n, Allocator.Persistent);
                rotations = new NativeArray<Quaternion>(n, Allocator.Persistent);
                angles = new NativeArray<float>(n, Allocator.Persistent);
                if (mode == "job" || mode == "jobrot") access = new TransformAccessArray(targets);
                if (mode == "anim")
                {
                    var animator = gameObject.AddComponent<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.applyRootMotion = false;
                    handles = new NativeArray<TransformStreamHandle>(n, Allocator.Persistent);
                    for (int i = 0; i < n; i++) handles[i] = animator.BindStreamTransform(targets[i]);
                    graph = PlayableGraph.Create("MicroBench");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    playable = AnimationScriptPlayable.Create(graph, new StreamJob {
                        Handles = handles, Positions = positions, Scales = scales, Rotations = rotations
                    });
                    playable.SetProcessInputs(false);
                    var output = AnimationPlayableOutput.Create(graph, "MicroBench", animator);
                    output.SetSourcePlayable(playable); output.SetWeight(1f);
                    graph.Play();
                }
            }

            private void InitMaterial()
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/Internal-Colored");
                if (shader == null) throw new InvalidOperationException("벤치 셰이더 없음");
                material = new Material(shader);
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    var type = shader.GetPropertyType(i);
                    if (floatName == null && (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range)) floatName = shader.GetPropertyName(i);
                    if (colorName == null && type == ShaderPropertyType.Color) colorName = shader.GetPropertyName(i);
                }
                if (floatName == null || colorName == null) throw new InvalidOperationException("float/color 프로퍼티 부재: " + shader.name);
                floatId = Shader.PropertyToID(floatName); colorId = Shader.PropertyToID(colorName);
                missingId = Shader.PropertyToID("_StutterFixMicroBenchMissing");
                if (material.HasProperty(missingId)) throw new InvalidOperationException("없는 프로퍼티 시험 이름 충돌");
            }

            private void Update()
            {
                if (!initialized || stopped) return;
                try
                {
                    if (Hitch.Playing || SceneManager.GetActiveScene().handle != sceneHandle ||
                        (command == "animbench" && Camera.allCamerasCount == 0))
                    { Log(command + ": 중단 — 재생 시작/장면 변경/카메라 없음"); Stop(); return; }
                    long now = Stopwatch.GetTimestamp();
                    if (samplePending)
                    {
                        if (outstanding) throw new InvalidOperationException("LateUpdate에서 job 완료되지 않음");
                        if (frame >= Warmup)
                        {
                            int i = frame - Warmup;
                            prepSamples[i] = Ms(prepTicks); writeSamples[i] = Ms(writeTicks);
                            completeSamples[i] = Ms(completeTicks); totalSamples[i] = Ms(prepTicks + writeTicks + completeTicks);
                            frameSamples[i] = Ms(now - start);
                        }
                        samplePending = false;
                        if (++frame == Warmup + frames)
                        {
                            Report();
                            if (command == "animbench" || ++variant == MaterialCases.Length) { Stop(); return; }
                            frame = 0;
                            // Avoid counting report formatting/log IO in the next case's frame time.
                        }
                    }
                    start = Stopwatch.GetTimestamp(); prepTicks = writeTicks = completeTicks = 0;
                    if (command == "animbench") WriteTransforms(); else WriteMaterial();
                    samplePending = true;
                }
                catch (Exception ex) { Log(command + " 실패/지원 불가: " + ex); Stop(); }
            }

            private void WriteTransforms()
            {
                long t = Stopwatch.GetTimestamp();
                for (int i = 0; i < n; i++)
                {
                    float phase = ((frame * 13 + i * 7) % 360) * .01f;
                    positions[i] = new Vector3(i % 80 - 40 + phase, i / 80 - 13 + phase * .5f, 0);
                    scales[i] = new Vector3(.2f + phase * .01f, .2f + phase * .02f, 1);
                    angles[i] = phase * 100;
                    if (mode != "jobrot") rotations[i] = Quaternion.Euler(0, 0, angles[i]);
                }
                prepTicks = Stopwatch.GetTimestamp() - t;
                t = Stopwatch.GetTimestamp();
                if (mode == "job" || mode == "jobrot")
                {
                    pending = new TransformJob { Positions = positions, Scales = scales, Rotations = rotations,
                        Angles = angles, CalculateRotation = mode == "jobrot" }.Schedule(access);
                    outstanding = true;
                    JobHandle.ScheduleBatchedJobs();
                }
                else if (mode != "anim")
                    for (int i = 0; i < n; i++)
                    {
                        var tr = targets[i];
                        if (mode == "mainsep") { tr.position = positions[i]; tr.rotation = rotations[i]; }
                        else { tr.localPosition = positions[i]; tr.localRotation = rotations[i]; }
                        tr.localScale = scales[i];
                    }
                writeTicks = Stopwatch.GetTimestamp() - t;
                // One untimed warmup probe, kept outside recorded samples. Do not infer forced
                // synchronization solely from duration: record IsCompleted and actual readback.
                if (outstanding && !probed)
                {
                    probed = true;
                    bool before = pending.IsCompleted;
                    long readStart = Stopwatch.GetTimestamp();
                    string result;
                    try { result = targets[0].localPosition.ToString("F4"); }
                    catch (Exception ex) { result = ex.GetType().Name + ": " + ex.Message; }
                    double readMs = Ms(Stopwatch.GetTimestamp() - readStart);
                    bool after = pending.IsCompleted;
                    long waitStart = Stopwatch.GetTimestamp(); pending.Complete(); outstanding = false;
                    double waitMs = Ms(Stopwatch.GetTimestamp() - waitStart);
                    Log("animbench " + mode + " Complete 전 Transform 읽기: before=" + before + " after=" + after +
                        " 읽기=" + readMs.ToString("F6") + "ms 이후 Complete=" + waitMs.ToString("F6") +
                        "ms 값/예외=" + result + " 목표=" + positions[0].ToString("F4") + " 완료 후=" + targets[0].localPosition.ToString("F4") +
                        "; 1회 워밍업 진단, 강제 동기화 여부는 이 표본만으로 단정 불가");
                }
            }

            private void LateUpdate()
            {
                if (!initialized || stopped || !samplePending) return;
                try
                {
                    long t = Stopwatch.GetTimestamp();
                    if (outstanding) { pending.Complete(); outstanding = false; }
                    if (command == "animbench" && mode == "anim") graph.Evaluate(1f / 60f);
                    completeTicks = Stopwatch.GetTimestamp() - t;
                    if (command == "animbench" && mode == "anim" && !animVerified)
                    {
                        for (int i = 0; i < n; i++)
                            if ((targets[i].localPosition - positions[i]).sqrMagnitude > 1e-8f ||
                                (targets[i].localScale - scales[i]).sqrMagnitude > 1e-8f ||
                                Quaternion.Angle(targets[i].localRotation, rotations[i]) > .1f)
                                throw new InvalidOperationException("AnimationStream 쓰기 검증 실패 index=" + i + " (바인딩/Animator 지원 확인 필요)");
                        animVerified = true;
                        Log("animbench anim: 첫 워밍업 프레임 위치/회전/크기 검증 통과");
                    }
                }
                catch (Exception ex) { Log(command + " LateUpdate 실패/지원 불가: " + ex); Stop(); }
            }

            private void WriteMaterial()
            {
                // No delegate calls, property lookup, or per-call allocations in measured loops.
                long t = Stopwatch.GetTimestamp();
                switch (variant)
                {
                    case 0: for (int i = 0; i < n; i++) material.SetFloat(floatName, .5f); break;
                    case 1: for (int i = 0; i < n; i++) material.SetFloat(floatId, .5f); break;
                    case 2: for (int i = 0; i < n; i++) material.SetFloat(floatName, (i & 1023) * (1f / 1024)); break;
                    case 3: for (int i = 0; i < n; i++) material.SetFloat(floatId, (i & 1023) * (1f / 1024)); break;
                    case 4: for (int i = 0; i < n; i++) material.SetColor(colorName, Color.white); break;
                    case 5: for (int i = 0; i < n; i++) material.SetColor(colorId, Color.white); break;
                    case 6: for (int i = 0; i < n; i++) material.SetColor(colorName, new Color((i & 1023) * (1f / 1024), .5f, .25f, 1)); break;
                    case 7: for (int i = 0; i < n; i++) material.SetColor(colorId, new Color((i & 1023) * (1f / 1024), .5f, .25f, 1)); break;
                    case 8: for (int i = 0; i < n; i++) material.SetFloat("_StutterFixMicroBenchMissing", (i & 1023) * (1f / 1024)); break;
                }
                writeTicks = Stopwatch.GetTimestamp() - t;
            }

            private static string Stats(double[] values, double factor = 1)
            {
                double sum = 0; for (int i = 0; i < values.Length; i++) sum += values[i];
                var sorted = (double[])values.Clone(); Array.Sort(sorted);
                return (sum / values.Length * factor).ToString("F6") + "/" +
                    (sorted[(int)Math.Ceiling(sorted.Length * .99) - 1] * factor).ToString("F6");
            }

            private void Report()
            {
                if (command == "animbench")
                    Log("animbench " + mode + " N=" + n + ": 쓰기 " + Stats(writeSamples) +
                        "ms/프레임 (전체 프레임 " + Stats(frameSamples) + "ms) 준비=" + Stats(prepSamples) +
                        "ms Complete/Evaluate=" + Stats(completeSamples) + "ms 메인 합계=" + Stats(totalSamples) +
                        "ms; 모두 평균/p99, 표본=" + frames);
                else
                    Log("matbench " + MaterialCases[variant] + " N=" + n + ": " + Stats(writeSamples, 1000.0 / n) +
                        "µs/호출 (배치시간/N 평균/p99), 전체 프레임=" + Stats(frameSamples) + "ms, 표본=" + frames);
            }

            internal void Stop()
            {
                if (stopped) return;
                stopped = true; initialized = false;
                try { if (outstanding) { pending.Complete(); outstanding = false; } }
                finally
                {
                    // Destroy the graph before disposing the NativeArrays its job references.
                    if (graph.IsValid()) graph.Destroy();
                    if (access.isCreated) access.Dispose();
                    if (handles.IsCreated) handles.Dispose();
                    if (positions.IsCreated) positions.Dispose();
                    if (scales.IsCreated) scales.Dispose();
                    if (rotations.IsCreated) rotations.Dispose();
                    if (angles.IsCreated) angles.Dispose();
                    if (material != null) Object.Destroy(material);
                    if (sprite != null) Object.Destroy(sprite);
                    if (texture != null) Object.Destroy(texture);
                    active = null; finished = true; finishedFrame = Time.frameCount;
                    Object.Destroy(gameObject); // Also destroys all child renderers/Transforms.
                }
            }
            private void OnDisable() { if (!stopped) Stop(); }
            private void OnDestroy() { if (!stopped) Stop(); }
        }
    }
}
#endif
