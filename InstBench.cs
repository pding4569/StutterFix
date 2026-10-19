#if DEV || AUTOTEST
using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StutterFix
{
    // Explicit AutoTest command only. No game patches or decoration state are touched.
    internal static class InstBench
    {
        const int Warmup = 60;
        static Runner active;
        static bool finished;
        static int finishedFrame;
        static Baseline baseline;
        sealed class Baseline
        {
            internal string Key;
            internal double Frame, Tail, Main;
        }
        static void Log(string s) { Main.Entry.Logger.Log("[마이크로벤치] instbench " + s); }
        static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        static string F(double value) { return value.ToString("F6", CultureInfo.InvariantCulture); }

        // Shared synthetic workload: meshbench must use the exact renderer reference values.
        internal struct Layout
        {
            internal Vector3 Origin, Right, Up;
            internal Quaternion Rotation;
            internal float Width, Height, Cell;
            internal int Columns, Rows;
            internal void Value(int frame, int i, out Vector3 p, out Quaternion r, out Vector3 s, out Color c)
            {
                float phase = ((frame * 13 + i * 7) % 360) / 360f;
                float x = ((i % Columns + .5f) / Columns - .5f) * Width;
                float y = ((i / Columns + .5f) / Rows - .5f) * Height;
                p = Origin + Right * (x + (phase - .5f) * Cell * .2f) + Up * (y + (phase - .5f) * Cell * .1f);
                r = Rotation * Quaternion.Euler(0, 0, phase * 360);
                s = new Vector3(Cell * (.8f + phase * .2f), Cell * (.8f + phase * .1f), 1);
                c = new Color(.5f + phase * .5f, 1 - phase * .5f, .75f, .6f + phase * .4f);
            }
        }

        internal static bool Step(string arg)
        {
            if (finished)
            {
                // Destroy is deferred; let the final submitted frame and cleanup finish first.
                if (Time.frameCount <= finishedFrame) return false;
                finished = false;
                return true;
            }
            if (active != null) return false;
            if (Hitch.Playing) { Log("거부 — 곡 재생 중"); return true; }
            var args = arg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int n = 2000, frames = 300, k = 1;
            string mode = args.Length > 2 ? args[2].ToLowerInvariant() : "renderers";
            if (args.Length > 4 ||
                (args.Length > 0 && (!int.TryParse(args[0], out n) || n < 1 || n > 50000)) ||
                (args.Length > 1 && (!int.TryParse(args[1], out frames) || frames < 1 || frames > 10000)) ||
                (args.Length > 3 && (!int.TryParse(args[3], out k) || k < 1 || k > 64)) ||
                (mode != "renderers" && mode != "drawinst" && mode != "renderparams" &&
                 mode != "drawindirect" && mode != "texturearray"))
            {
                Log("인수 오류: instbench [N=2000, 1..50000] [프레임=300, 1..10000] " +
                    "[renderers|drawinst|renderparams|drawindirect|texturearray] [K=1, 1..64]");
                return true;
            }
            if (mode == "drawindirect" || mode == "texturearray")
            {
                Log(mode + " 건너뜀: RenderMeshIndirect/ComputeBuffer/Texture2DArray API는 참조 DLL에 존재; " +
                    (mode == "drawindirect" ?
                    "기존 DecoGpu 셰이더는 StructuredBuffer/간접 인스턴스 인덱스 경로가 없어 업로드+그리기 동등 시험 불가" :
                    "기존 DecoGpu 셰이더는 sampler2D 전용이며 배열 샘플링/인스턴스별 슬라이스 경로 없음") +
                    "; API=" + SystemInfo.graphicsDeviceType + " instancing=" + SystemInfo.supportsInstancing +
                    " compute=" + SystemInfo.supportsComputeShaders + " textureArrays=" + SystemInfo.supports2DArrayTextures +
                    "; API/장치 기능만으로 셰이더 동작을 보장할 수 없음");
                return true;
            }
            var go = new GameObject("StutterFix InstBench");
            try
            {
                active = go.AddComponent<Runner>();
                active.Init(n, frames, mode, k);
            }
            catch (Exception ex)
            {
                Log(mode + " 시작 실패/지원 불가: " + ex);
                if (active != null) active.Stop(); else Object.Destroy(go);
            }
            return false;
        }

        internal static void Cancel()
        {
            if (active != null) { Log("취소"); active.Stop(); }
            finished = false;
        }

        sealed class Batch
        {
            internal int Texture, Offset, Count;
            internal Matrix4x4[] Matrices;
            internal Vector4[] Colors;
            internal MaterialPropertyBlock Props;
            internal RenderParams Parameters;
        }

        public sealed class Runner : MonoBehaviour
        {
            int n, frames, k, frame, scene, cameraCount, screenWidth, screenHeight, vsync, targetFps, quality;
            string mode, key;
            bool initialized, stopped, pending;
            Camera camera;
            Matrix4x4 cameraMatrix, projection;
            Vector3 origin, right, up;
            Quaternion rotation;
            float width, height, cell;
            int columns, rows;
            Layout layout;
            Texture2D[] textures;
            Sprite[] sprites;
            SpriteRenderer[] renderers;
            Transform[] targets;
            Material[] materials;
            Mesh mesh;
            Shader shader;
            AssetBundle bundle;
            Batch[] batches;
            double[] mainSamples, frameSamples;
            long start, mainTicks;
            static readonly int Tint = Shader.PropertyToID("_InstanceTint");

            internal void Init(int count, int samples, string method, int kinds)
            {
                n = count; frames = samples; mode = method; k = kinds;
                scene = SceneManager.GetActiveScene().handle;
                cameraCount = Camera.allCamerasCount;
                if (cameraCount == 0) throw new InvalidOperationException("활성 카메라가 있는 장면 필요");
                camera = Camera.main;
                if (!camera || !camera.isActiveAndEnabled) camera = Camera.allCameras[0];
                if ((camera.cullingMask & (1 << gameObject.layer)) == 0)
                    throw new InvalidOperationException("카메라가 벤치 레이어(Default)를 렌더링하지 않음");
                if (GraphicsSettings.currentRenderPipeline != null)
                    throw new InvalidOperationException("이 벤치는 Built-in RP 전용");
                cameraMatrix = camera.transform.localToWorldMatrix; projection = camera.projectionMatrix;
                float depth = Mathf.Max(camera.nearClipPlane + 10, 10);
                if (depth >= camera.farClipPlane) throw new InvalidOperationException("카메라 clip 범위가 너무 좁음");
                origin = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth));
                width = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.05f, .5f, depth)),
                    camera.ViewportToWorldPoint(new Vector3(.95f, .5f, depth)));
                height = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.5f, .05f, depth)),
                    camera.ViewportToWorldPoint(new Vector3(.5f, .95f, depth)));
                right = camera.transform.right; up = camera.transform.up; rotation = camera.transform.rotation;
                columns = Mathf.CeilToInt(Mathf.Sqrt(n * width / height)); rows = (n + columns - 1) / columns;
                cell = Mathf.Min(width / columns, height / rows) * .6f;
                layout = new Layout { Origin = origin, Right = right, Up = up, Rotation = rotation,
                    Width = width, Height = height, Cell = cell, Columns = columns, Rows = rows };
                mainSamples = new double[frames]; frameSamples = new double[frames];
                screenWidth = Screen.width; screenHeight = Screen.height;
                vsync = QualitySettings.vSyncCount; targetFps = Application.targetFrameRate;
                quality = QualitySettings.GetQualityLevel();
                key = n + "/" + frames + "/" + k + "/" + scene + "/" + camera.GetInstanceID() + "/" +
                    screenWidth + "/" + screenHeight + "/" + vsync + "/" + targetFps + "/" + quality + "/" + cameraCount + "/" +
                    cameraMatrix.ToString("F6") + "/" + projection.ToString("F6");
                // Starting a new reference invalidates an older reference even if this run fails.
                if (mode == "renderers") baseline = null;
                textures = new Texture2D[k]; sprites = new Sprite[k];
                for (int j = 0; j < k; j++)
                {
                    textures[j] = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    textures[j].SetPixel(0, 0, j == 0 ? Color.white : Color.HSVToRGB(j / (float)k, .5f, 1));
                    textures[j].Apply();
                    if (mode == "renderers")
                        sprites[j] = Sprite.Create(textures[j], new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
                }
                if (mode == "renderers")
                {
                    targets = new Transform[n]; renderers = new SpriteRenderer[n];
                    for (int i = 0; i < n; i++)
                    {
                        var go = new GameObject("instbench-" + i); // Deliberately parentless.
                        targets[i] = go.transform;
                        renderers[i] = go.AddComponent<SpriteRenderer>();
                        renderers[i].sprite = sprites[i % k];
                        renderers[i].sortingOrder = 0;
                        renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                        renderers[i].receiveShadows = false;
                        renderers[i].lightProbeUsage = LightProbeUsage.Off;
                    }
                }
                else InitInstancing();
                initialized = true;
                Log(mode + " 시작 N=" + n + " K=" + k + " 워밍업=" + Warmup + " 측정=" + frames +
                    " 배치좌표 기준 카메라=" + camera.name + " 활성 카메라 수=" + cameraCount +
                    " Unity=" + Application.unityVersion + " API=" + SystemInfo.graphicsDeviceType +
                    " 해상도=" + Screen.width + "x" + Screen.height + " vSync=" + QualitySettings.vSyncCount +
                    " targetFPS=" + Application.targetFrameRate + " 제출호출/프레임=" + (batches == null ? "SpriteRenderer 내부" : batches.Length.ToString()) +
                    "; 모든 방식에서 레이어를 보는 전체 카메라에 렌더링; 정렬 순서 미반영, GPU 직접 타이머 없음");
            }

            void InitInstancing()
            {
                if (!SystemInfo.supportsInstancing) throw new InvalidOperationException("SystemInfo.supportsInstancing=false");
                using (var stream = typeof(InstBench).Assembly.GetManifestResourceStream("StutterFix.decogpu"))
                {
                    if (stream == null) throw new InvalidOperationException("effects/stutterfix_decogpu 번들 없음");
                    var bytes = new byte[stream.Length]; int read = 0;
                    while (read < bytes.Length)
                    {
                        int got = stream.Read(bytes, read, bytes.Length - read);
                        if (got == 0) throw new System.IO.EndOfStreamException();
                        read += got;
                    }
                    bundle = AssetBundle.LoadFromMemory(bytes);
                }
                if (!bundle) throw new InvalidOperationException("DecoGpu 번들 로드 실패 (다른 실험이 이미 로드했는지 확인)");
                shader = bundle.LoadAsset<Shader>("Assets/DecoGpu/DecoGpu.shader");
                if (!shader || !shader.isSupported) throw new InvalidOperationException("DecoGpu 셰이더 지원 불가");
                materials = new Material[k];
                for (int j = 0; j < k; j++)
                {
                    materials[j] = new Material(shader) { enableInstancing = true, mainTexture = textures[j] };
                }
                // Known source: multi_compile_instancing and _InstanceTint. A generic
                // Shader.Find success/enableInstancing flag alone cannot prove per-instance tint.
                Log(mode + " 셰이더=" + shader.name + " (_InstanceTint, multi_compile_instancing 소스 확인; 실제 변형/시각 동등성은 런타임 확인 필요)");
                mesh = new Mesh { name = "InstBench quad" };
                mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
                    new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
                mesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds();
                // RenderMeshInstanced may also store worldToObject for nonuniform scales.
                // Use conservative 511 there; legacy DrawMeshInstanced batches at 1023.
                int limit = mode == "drawinst" ? 1023 : 511;
                int batchCount = 0;
                for (int j = 0; j < k; j++) batchCount += ((n / k + (j < n % k ? 1 : 0)) + limit - 1) / limit;
                batches = new Batch[batchCount]; int b = 0;
                var bounds = new Bounds(origin, Vector3.one * (width + height + 10));
                for (int j = 0; j < k; j++)
                {
                    int count = n / k + (j < n % k ? 1 : 0);
                    for (int offset = 0; offset < count; offset += limit)
                    {
                        int length = Math.Min(limit, count - offset);
                        var batch = new Batch { Texture = j, Offset = offset, Count = length,
                            Matrices = new Matrix4x4[length], Colors = new Vector4[length], Props = new MaterialPropertyBlock() };
                        batch.Parameters = new RenderParams(materials[j]) { camera = null, layer = gameObject.layer,
                            worldBounds = bounds, matProps = batch.Props, shadowCastingMode = ShadowCastingMode.Off,
                            receiveShadows = false, lightProbeUsage = LightProbeUsage.Off };
                        batches[b++] = batch;
                    }
                }
            }

            void Value(int i, out Vector3 p, out Quaternion r, out Vector3 s, out Color c)
            {
                layout.Value(frame, i, out p, out r, out s, out c);
            }

            void Update()
            {
                if (!initialized || stopped) return;
                try
                {
                    if (Hitch.Playing || SceneManager.GetActiveScene().handle != scene || !camera ||
                        !camera.isActiveAndEnabled || Camera.allCamerasCount != cameraCount ||
                        camera.transform.localToWorldMatrix != cameraMatrix || camera.projectionMatrix != projection ||
                        Screen.width != screenWidth || Screen.height != screenHeight ||
                        QualitySettings.vSyncCount != vsync || Application.targetFrameRate != targetFps ||
                        QualitySettings.GetQualityLevel() != quality)
                    { Log(mode + " 중단 — 재생/장면/카메라/표시 설정 변경"); Stop(); return; }
                    long now = Stopwatch.GetTimestamp();
                    if (pending)
                    {
                        if (frame >= Warmup)
                        {
                            int sample = frame - Warmup;
                            mainSamples[sample] = Ms(mainTicks);
                            frameSamples[sample] = Ms(now - start);
                        }
                        pending = false;
                        if (++frame == Warmup + frames) { Report(); Stop(); return; }
                    }
                    // Includes deterministic value calculation, matrix/color fill, property
                    // upload and draw submission. Next Update captures the whole frame interval.
                    start = Stopwatch.GetTimestamp();
                    if (mode == "renderers")
                    {
                        for (int i = 0; i < n; i++)
                        {
                            Value(i, out var p, out var r, out var s, out var c);
                            targets[i].position = p; targets[i].rotation = r; targets[i].localScale = s;
                            renderers[i].color = c;
                        }
                    }
                    else
                    {
                        foreach (var batch in batches)
                        {
                            for (int j = 0; j < batch.Count; j++)
                            {
                                int i = batch.Texture + (batch.Offset + j) * k;
                                Value(i, out var p, out var r, out var s, out var c);
                                batch.Matrices[j] = Matrix4x4.TRS(p, r, s); batch.Colors[j] = c;
                            }
                            batch.Props.SetVectorArray(Tint, batch.Colors);
                            if (mode == "drawinst")
                                Graphics.DrawMeshInstanced(mesh, 0, materials[batch.Texture], batch.Matrices, batch.Count,
                                    batch.Props, ShadowCastingMode.Off, false, gameObject.layer, null, LightProbeUsage.Off);
                            else
                                Graphics.RenderMeshInstanced(in batch.Parameters, mesh, 0, batch.Matrices, batch.Count);
                        }
                    }
                    mainTicks = Stopwatch.GetTimestamp() - start;
                    pending = true;
                }
                catch (Exception ex) { Log(mode + " 실패/지원 불가: " + ex); Stop(); }
            }

            static double Mean(double[] samples)
            {
                double sum = 0; foreach (double v in samples) sum += v; return sum / samples.Length;
            }
            static double P99(double[] samples)
            {
                var sorted = (double[])samples.Clone(); Array.Sort(sorted);
                return sorted[(int)Math.Ceiling(sorted.Length * .99) - 1];
            }
            void Report()
            {
                double main = Mean(mainSamples), whole = Mean(frameSamples), tail = P99(frameSamples);
                Log(mode + " N=" + n + " K=" + k + " 메인=" + F(main) + "/" + F(P99(mainSamples)) +
                    "ms 전체 프레임=" + F(whole) + "/" + F(tail) + "ms (평균/p99, 표본=" + frames + ")");
                if (mode == "renderers")
                {
                    baseline = new Baseline { Key = key, Main = main, Frame = whole, Tail = tail };
                    Log("renderers 기준선 저장; 같은 N/K/프레임/카메라/화질 설정의 drawinst 또는 renderparams와 비교 가능");
                }
                else if (baseline != null && baseline.Key == key)
                    Log(mode + " renderers 대비 Δ메인=" + F(main - baseline.Main) + "ms Δ전체평균=" + F(whole - baseline.Frame) +
                        "ms Δ전체p99=" + F(tail - baseline.Tail) + "ms; GPU 병목 여부는 이 전체 프레임 차이를 참고하되 " +
                        "GPU 직접 시간 미측정, vSync/FPS 제한/렌더 스레드/장면 부하 영향으로 단정 불가");
                else Log(mode + " GPU 병목 판단 보류: 일치하는 renderers 기준선 없음; 같은 조건에서 renderers를 먼저 실행. GPU 직접 시간 미측정");
            }

            internal void Stop()
            {
                if (stopped) return;
                stopped = true; initialized = false;
                // Only resources created by this runner; includes parentless renderers.
                if (targets != null) foreach (var t in targets) if (t) Object.Destroy(t.gameObject);
                if (materials != null) foreach (var m in materials) if (m) Object.Destroy(m);
                if (sprites != null) foreach (var s in sprites) if (s) Object.Destroy(s);
                if (textures != null) foreach (var t in textures) if (t) Object.Destroy(t);
                if (mesh) Object.Destroy(mesh);
                // Unload(false) releases the container without immediately invalidating the
                // shader of draws submitted in this frame. Explicitly destroy our shader later.
                if (bundle) bundle.Unload(false);
                if (shader) Object.Destroy(shader);
                active = null; finished = true; finishedFrame = Time.frameCount;
                Object.Destroy(gameObject);
            }
            void OnDisable() { if (!stopped) Stop(); }
            void OnDestroy() { if (!stopped) Stop(); }
        }
    }
}
#endif
