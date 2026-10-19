#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StutterFix
{
    // Explicit AutoTest command only. All objects belong to this benchmark.
    internal static class MeshBench
    {
        const int Warmup = 60;
        const MeshUpdateFlags Flags = MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
        static Runner active;
        static bool running, aborted;
        static int stage, afterFrame, n, frames, orders, textures;
        static string baselineKey;
        static double baselineMain, baselineMainTail, baselineFrame, baselineTail;
        static readonly string[] Modes = { "renderers", "a-buffer", "b-lists", "c-job" };
        static void Log(string s) { Main.Entry.Logger.Log("[마이크로벤치] meshbench " + s); }
        static string F(double v) { return v.ToString("F6", CultureInfo.InvariantCulture); }
        static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

        internal static bool Step(string arg)
        {
            if (!running)
            {
                if (Hitch.Playing) { Log("거부 — 곡 재생 중"); return true; }
                var a = arg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                n = 2000; frames = 300; orders = 42; textures = 10;
                if (a.Length > 4 || (a.Length > 0 && (!int.TryParse(a[0], out n) || n < 1 || n > 50000)) ||
                    (a.Length > 1 && (!int.TryParse(a[1], out frames) || frames < 1 || frames > 10000)) ||
                    (a.Length > 2 && (!int.TryParse(a[2], out orders) || orders < 1 || orders > 32768)) ||
                    (a.Length > 3 && (!int.TryParse(a[3], out textures) || textures < 1 || textures > 64)))
                { Log("인수 오류: meshbench [N=2000,1..50000] [프레임=300,1..10000] [G=42,1..32768] [T=10,1..64]"); return true; }
                running = true; aborted = false; stage = 0; afterFrame = -1; baselineKey = null;
            }
            if (active != null || Time.frameCount <= afterFrame) return false;
            if (aborted || stage == Modes.Length)
            { running = false; Log(aborted ? "중단 완료" : "전체 방식 완료"); return true; }
            var go = new GameObject("StutterFix MeshBench");
            try { active = go.AddComponent<Runner>(); active.Init(Modes[stage]); }
            catch (Exception ex)
            {
                Log(Modes[stage] + " 시작 실패/지원 불가: " + ex);
                if (active != null) active.Stop(false); else { Object.Destroy(go); aborted = true; afterFrame = Time.frameCount; }
            }
            return false;
        }
        internal static void Cancel()
        {
            if (active != null) { Log("취소"); active.Stop(false); }
            running = false; baselineKey = null;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Vertex { public Vector3 Position; public Color Color; public Vector2 UV; }
        struct Item { public int Index, Offset; }
        struct FillJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Item> Items;
            // Each decoration owns four disjoint vertices at its precomputed offset.
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<Vertex> Vertices;
            public InstBench.Layout Layout;
            public int Frame;
            public void Execute(int i) { Fill(Layout, Frame, Items[i], Vertices); }
        }
        static void Fill(InstBench.Layout layout, int frame, Item item, NativeArray<Vertex> vertices)
        {
            layout.Value(frame, item.Index, out var p, out var r, out var s, out var c);
            var trs = Matrix4x4.TRS(p, r, s);
            for (int q = 0; q < 4; q++)
            {
                float x = q == 0 || q == 3 ? 0 : 1, y = q < 2 ? 0 : 1;
                vertices[item.Offset + q] = new Vertex { Position = trs.MultiplyPoint3x4(new Vector3(x - .5f, y - .5f, 0)),
                    Color = c, UV = new Vector2(x, y) };
            }
        }
        sealed class Group
        {
            internal GameObject Object;
            internal Mesh Mesh;
            internal int Ordinal, Offset, Count;
            internal List<Vector3> Positions;
            internal List<Color> Colors;
        }

        public sealed class Runner : MonoBehaviour
        {
            string mode, key;
            bool initialized, stopped, pending;
            int frame, scene, cameraCount, screenWidth, screenHeight, vsync, targetFps, quality, cullingMask;
            Camera camera;
            Matrix4x4 cameraMatrix, projection;
            InstBench.Layout layout;
            Texture2D[] texture;
            Sprite[] sprites;
            Material[] materials;
            Transform[] targets;
            SpriteRenderer[] renderers;
            Group[] groups;
            NativeArray<Item> items;
            NativeArray<Vertex> vertices;
            double[] mainSamples, frameSamples;
            long start, mainTicks;

            internal void Init(string method)
            {
                mode = method;
                if (Hitch.Playing) throw new InvalidOperationException("곡 재생 중");
                scene = SceneManager.GetActiveScene().handle;
                cameraCount = Camera.allCamerasCount;
                if (cameraCount == 0) throw new InvalidOperationException("활성 카메라 필요");
                camera = Camera.main;
                if (!camera || !camera.isActiveAndEnabled) camera = Camera.allCameras[0];
                cullingMask = camera.cullingMask;
                if ((cullingMask & (1 << gameObject.layer)) == 0) throw new InvalidOperationException("Default 레이어가 카메라에서 제외됨");
                if (GraphicsSettings.currentRenderPipeline != null) throw new InvalidOperationException("Built-in RP 전용");
                cameraMatrix = camera.transform.localToWorldMatrix; projection = camera.projectionMatrix;
                float depth = Mathf.Max(camera.nearClipPlane + 10, 10);
                if (depth >= camera.farClipPlane) throw new InvalidOperationException("clip 범위 부족");
                layout = new InstBench.Layout {
                    Origin = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth)),
                    Width = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.05f,.5f,depth)), camera.ViewportToWorldPoint(new Vector3(.95f,.5f,depth))),
                    Height = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.5f,.05f,depth)), camera.ViewportToWorldPoint(new Vector3(.5f,.95f,depth))),
                    Right = camera.transform.right, Up = camera.transform.up, Rotation = camera.transform.rotation };
                layout.Columns = Mathf.CeilToInt(Mathf.Sqrt(n * layout.Width / layout.Height));
                layout.Rows = (n + layout.Columns - 1) / layout.Columns;
                layout.Cell = Mathf.Min(layout.Width / layout.Columns, layout.Height / layout.Rows) * .6f;
                mainSamples = new double[frames]; frameSamples = new double[frames];
                screenWidth = Screen.width; screenHeight = Screen.height; vsync = QualitySettings.vSyncCount;
                targetFps = Application.targetFrameRate; quality = QualitySettings.GetQualityLevel();
                key = n + "/" + frames + "/" + orders + "/" + textures + "/" + scene + "/" + camera.GetInstanceID() + "/" +
                    screenWidth + "/" + screenHeight + "/" + vsync + "/" + targetFps + "/" + quality + "/" + cameraCount + "/" +
                    cullingMask + "/" + cameraMatrix.ToString("F6") + "/" + projection.ToString("F6");
                if (mode != "renderers" && key != baselineKey) throw new InvalidOperationException("기준 조건 변경: renderers부터 재실행 필요");
                var shader = Shader.Find("Sprites/Default");
                if (!shader || !shader.isSupported) throw new InvalidOperationException("Sprites/Default 지원 불가");
                int usedTextures = Math.Min(textures, n);
                texture = new Texture2D[usedTextures]; materials = new Material[usedTextures];
                if (mode == "renderers") sprites = new Sprite[usedTextures];
                for (int t = 0; t < usedTextures; t++)
                {
                    texture[t] = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture[t].SetPixel(0, 0, t == 0 ? Color.white : Color.HSVToRGB(t / (float)textures, .5f, 1)); texture[t].Apply();
                    materials[t] = new Material(shader) { mainTexture = texture[t] };
                    if (sprites != null) sprites[t] = Sprite.Create(texture[t], new Rect(0,0,1,1), new Vector2(.5f,.5f), 1);
                }
                if (mode == "renderers")
                {
                    targets = new Transform[n]; renderers = new SpriteRenderer[n];
                    for (int i = 0; i < n; i++)
                    {
                        var go = new GameObject("meshbench-renderer"); targets[i] = go.transform;
                        var sr = go.AddComponent<SpriteRenderer>(); renderers[i] = sr;
                        sr.sprite = sprites[i % textures]; sr.sharedMaterial = materials[i % textures];
                        Configure(sr, (i / textures) % orders);
                    }
                }
                else InitMeshes();
                initialized = true;
                Log(mode + " 시작 N=" + n + " G=" + orders + " T=" + textures + " 그룹=" + Math.Min(n, orders * textures) +
                    " 총정점=" + (4 * n) + " 워밍업=" + Warmup + " 측정=" + frames + " 카메라=" + camera.name +
                    " 카메라수=" + cameraCount + " Unity=" + Application.unityVersion + " API=" + SystemInfo.graphicsDeviceType +
                    " 해상도=" + screenWidth + "x" + screenHeight + " vSync=" + vsync + " targetFPS=" + targetFps +
                    "; sortingLayerID=0, sortingOrder 적용; 마스크/동일순서 내부정렬 미검증; GPU 직접 시간 없음");
            }
            static void Configure(Renderer renderer, int order)
            {
                renderer.sortingLayerID = 0; renderer.sortingOrder = order;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
            void InitMeshes()
            {
                int count = Math.Min(n, orders * textures);
                groups = new Group[count];
                items = new NativeArray<Item>(n, Allocator.Persistent);
                vertices = new NativeArray<Vertex>(4 * n, Allocator.Persistent);
                int offset = 0;
                for (int g = 0; g < count; g++)
                {
                    var group = new Group { Ordinal = g, Offset = offset, Count = (n - 1 - g) / count + 1 };
                    groups[g] = group;
                    group.Object = new GameObject("meshbench-group-" + g);
                    var filter = group.Object.AddComponent<MeshFilter>();
                    var renderer = group.Object.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[g % textures]; Configure(renderer, g / textures);
                    var mesh = new Mesh { name = "MeshBench dynamic", indexFormat = IndexFormat.UInt32 };
                    group.Mesh = mesh; filter.sharedMesh = mesh; mesh.MarkDynamic();
                    int size = group.Count * 4;
                    var uv = new List<Vector2>(size);
                    var indices = new int[group.Count * 6];
                    for (int j = 0; j < group.Count; j++)
                    {
                        items[g + j * count] = new Item { Index = g + j * count, Offset = offset + j * 4 };
                        for (int q = 0; q < 4; q++) uv.Add(new Vector2(q == 0 || q == 3 ? 0 : 1, q < 2 ? 0 : 1));
                        int v = j * 4, ix = j * 6;
                        indices[ix] = v; indices[ix+1] = v+1; indices[ix+2] = v+2;
                        indices[ix+3] = v; indices[ix+4] = v+2; indices[ix+5] = v+3;
                    }
                    if (mode == "b-lists")
                    {
                        group.Positions = new List<Vector3>(new Vector3[size]); group.Colors = new List<Color>(new Color[size]);
                        mesh.SetVertices(group.Positions); mesh.SetColors(group.Colors); mesh.SetUVs(0, uv);
                    }
                    else mesh.SetVertexBufferParams(size,
                        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
                    mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
                    mesh.bounds = new Bounds(layout.Origin, Vector3.one * (layout.Width + layout.Height + 100));
                    offset += size;
                }
                if (offset != n * 4 || Marshal.SizeOf(typeof(Vertex)) != 36) throw new InvalidOperationException("정점 배치 오류");
            }
            void Update()
            {
                if (!initialized || stopped) return;
                try
                {
                    if (Hitch.Playing || SceneManager.GetActiveScene().handle != scene || !camera || !camera.isActiveAndEnabled ||
                        Camera.allCamerasCount != cameraCount || camera.cullingMask != cullingMask ||
                        camera.transform.localToWorldMatrix != cameraMatrix || camera.projectionMatrix != projection ||
                        Screen.width != screenWidth || Screen.height != screenHeight || QualitySettings.vSyncCount != vsync ||
                        Application.targetFrameRate != targetFps || QualitySettings.GetQualityLevel() != quality)
                    { Log(mode + " 중단 — 재생/장면/카메라/표시 설정 변경"); Stop(false); return; }
                    long now = Stopwatch.GetTimestamp();
                    if (pending)
                    {
                        if (frame >= Warmup) { mainSamples[frame-Warmup] = Ms(mainTicks); frameSamples[frame-Warmup] = Ms(now-start); }
                        pending = false;
                        if (++frame == Warmup + frames) { Report(); Stop(true); return; }
                    }
                    start = Stopwatch.GetTimestamp();
                    if (mode == "renderers")
                    {
                        for (int i = 0; i < n; i++)
                        {
                            layout.Value(frame, i, out var p, out var r, out var s, out var c);
                            targets[i].position = p; targets[i].rotation = r; targets[i].localScale = s; renderers[i].color = c;
                        }
                    }
                    else
                    {
                        if (mode == "c-job")
                            new FillJob { Layout = layout, Frame = frame, Items = items, Vertices = vertices }.Schedule(n, 64).Complete();
                        else if (mode == "a-buffer") for (int i = 0; i < n; i++) Fill(layout, frame, items[i], vertices);
                        foreach (var group in groups)
                        {
                            int size = group.Count * 4;
                            if (mode == "b-lists")
                            {
                                for (int j = 0; j < group.Count; j++)
                                {
                                    // Item indices are interleaved by group, while vertices are packed.
                                    int i = group.Ordinal + j * groups.Length;
                                    layout.Value(frame, i, out var p, out var r, out var s, out var c);
                                    var trs = Matrix4x4.TRS(p, r, s);
                                    for (int q = 0; q < 4; q++)
                                    {
                                        float x = q == 0 || q == 3 ? 0 : 1, y = q < 2 ? 0 : 1;
                                        group.Positions[j*4+q] = trs.MultiplyPoint3x4(new Vector3(x-.5f, y-.5f, 0));
                                        group.Colors[j*4+q] = c;
                                    }
                                }
                                group.Mesh.SetVertices(group.Positions, 0, size, Flags); group.Mesh.SetColors(group.Colors, 0, size, Flags);
                            }
                            else group.Mesh.SetVertexBufferData(vertices, group.Offset, 0, size, 0, Flags);
                        }
                    }
                    mainTicks = Stopwatch.GetTimestamp() - start; pending = true;
                }
                catch (Exception ex) { Log(mode + " 실패/지원 불가: " + ex); Stop(false); }
            }
            static double Mean(double[] a) { double sum = 0; foreach (var v in a) sum += v; return sum / a.Length; }
            static double P99(double[] a) { var sorted = (double[])a.Clone(); Array.Sort(sorted); return sorted[(int)Math.Ceiling(a.Length * .99)-1]; }
            void Report()
            {
                double main = Mean(mainSamples), mainTail = P99(mainSamples), whole = Mean(frameSamples), tail = P99(frameSamples);
                Log(mode + " N=" + n + " G=" + orders + " T=" + textures + " 그룹=" + Math.Min(n, orders * textures) + " 총정점=" + (n*4) +
                    " 메인=" + F(main) + "/" + F(mainTail) + "ms 전체 프레임=" + F(whole) + "/" + F(tail) + "ms (평균/p99, 표본=" + frames + ")");
                if (mode == "renderers")
                { baselineKey = key; baselineMain = main; baselineMainTail = mainTail; baselineFrame = whole; baselineTail = tail; }
                else if (key == baselineKey)
                    Log(mode + " renderers 대비 Δ메인=" + F(main-baselineMain) + "/" + F(mainTail-baselineMainTail) +
                        "ms Δ전체평균=" + F(whole-baselineFrame) + "ms Δ전체p99=" + F(tail-baselineTail) +
                        "ms; vSync/FPS 제한/장면/렌더 스레드 영향 포함, GPU 직접 시간 미측정");
            }
            internal void Stop(bool success)
            {
                if (stopped) return;
                stopped = true; initialized = false;
                if (targets != null) foreach (var t in targets) if (t) Object.Destroy(t.gameObject);
                if (groups != null) foreach (var g in groups) if (g != null)
                { if (g.Object) Object.Destroy(g.Object); if (g.Mesh) Object.Destroy(g.Mesh); }
                if (materials != null) foreach (var m in materials) if (m) Object.Destroy(m);
                if (sprites != null) foreach (var s in sprites) if (s) Object.Destroy(s);
                if (texture != null) foreach (var t in texture) if (t) Object.Destroy(t);
                if (items.IsCreated) items.Dispose(); if (vertices.IsCreated) vertices.Dispose();
                active = null; afterFrame = Time.frameCount + 1;
                if (success) stage++; else aborted = true;
                Object.Destroy(gameObject);
            }
            void OnDisable() { if (!stopped) Stop(false); }
            void OnDestroy() { if (!stopped) Stop(false); }
        }
    }
}
#endif
