#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Vertex = StutterFix.GpuDecoGpu.Vertex;

namespace StutterFix
{
    // G1 diagnostic: evaluate a fresh paused snapshot, then draw GPU-written
    // buffers with borrowed game materials. Playback always restores originals.
    // No live animation ownership or per-frame scan is installed by this gate.
    internal sealed class GpuDecoSnapshot : MonoBehaviour
    {
        internal sealed class Row
        {
            internal Renderer R;
            internal Material M;
            internal int Item = -1;
            internal SceneRenderOrder.Key Key;
            internal string Signature;
            internal int RunIndex = -1, SubmeshIndex = -1;
        }
        static GpuDecoSnapshot instance;
        readonly List<SpriteRenderer> originals = new List<SpriteRenderer>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<GraphicsBuffer> buffers = new List<GraphicsBuffer>();
        readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        readonly Dictionary<int, Row> rowByItemId = new Dictionary<int, Row>();
        readonly Dictionary<int, Vertex[]> actualVerticesByRun = new Dictionary<int, Vertex[]>();
        readonly Dictionary<int, int> runFirstItemByRun = new Dictionary<int, int>();
        AssetBundle bundle; GpuDecoGpu gpu; CommandBuffer commands;
        GpuDecoNativeBatch nativeBatch;
        GpuDecoCopy nativeCopy;
        GpuDecoData activeData;
        Vertex[] evaluatedVertices;
        int generation; double song; bool g0;
        internal static bool Active => instance;
        internal static int Owned => instance ? instance.originals.Count : 0;
        internal static string DiagnosticMode { get; private set; } = "off";
        // Default keeps SpriteRenderer bounds.center and half-up UNorm8 draw
        // color. Float32 is the logical/G0 format, not the native draw color.
        internal static bool UseQuantizedVertexColor = true;
        internal static void SetDiagnostic(string mode)
        {
            Require(!instance, "stop drawing before selecting diagnostic");
            Require(mode == "off" || mode == "world" || mode == "single" || mode == "floatcolor"
                || mode == "unorm8" || mode == "localz" || mode == "float_single_localz",
                "diagnostic off|world|single|floatcolor|unorm8|localz|float_single_localz required");
            DiagnosticMode = mode;
        }
        static void Log(string s) => Main.Entry.Logger.Log("[gpudeco] " + s);
        static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException("gpudeco G1: " + reason); }

        internal static string MaterialSignature(Material m)
        {
            var props = MatShare.Properties(m);
            if (props.TryGetValue("_MainTex", out var val))
            {
                int colon = val.IndexOf(':');
                if (colon >= 0) props["_MainTex"] = "tex" + val.Substring(colon);
            }
            return string.Join(";", props.Select(p => p.Key + "=" + p.Value));
        }

        internal static bool Start(GpuDecoData data, scrDecoration[] baseline, string bundlePath, HashSet<int> excludedItemIds = null)
        {
            Require(!instance, "already drawing");
            Require(FrameCompare.Paused && ADOBase.controller && ADOBase.controller.paused && Time.timeScale == 0, "framecmp pause required");
            Require(data != null && data.Schema == 2 && data.Items.Length > 0, "fresh export required");
            Require(Math.Abs(scrConductor.instance.songposition_minusi - data.RawTime) < 1e-6, "export song time changed");
            Require(excludedItemIds != null || !GpuDecoCoverage.OwnedLow(data.Awake, data.Items.Length), "OWNED_LOW");
            var root = new GameObject("GpuDeco.G1") { hideFlags = HideFlags.HideAndDontSave };
            instance = root.AddComponent<GpuDecoSnapshot>();
            try { instance.Build(data, baseline, bundlePath, excludedItemIds); return instance.g0; }
            catch { Stop(); throw; }
        }

        void Build(GpuDecoData data, scrDecoration[] baseline, string bundlePath, HashSet<int> excludedItemIds)
        {
            var timer = Stopwatch.StartNew(); generation = DecoBake.Generation; song = data.RawTime;
            Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 && SystemInfo.supportsComputeShaders, "D3D11 compute required");
            var camera = GpuSplit.FindMain(); Require(camera && camera.orthographic, "orthographic camera required");
            var mode = camera.transparencySortMode;
            if (mode == TransparencySortMode.Default) mode = GraphicsSettings.transparencySortMode;
            var axis = mode == TransparencySortMode.CustomAxis
                ? (camera.transparencySortMode == TransparencySortMode.CustomAxis ? camera.transparencySortAxis : GraphicsSettings.transparencySortAxis)
                : camera.transform.forward;
            Require(Mathf.Abs(axis.x) < 1e-6f && Mathf.Abs(axis.y) < 1e-6f && Mathf.Abs(axis.z) > 0, "Z sorting axis required");
            var candidates = new Dictionary<int, int>();
            for (int i = 0; i < data.Items.Length; i++)
            {
                var item = data.Items[i];
                if (excludedItemIds != null && excludedItemIds.Contains(item.Id)) continue;
                var d = baseline[item.Index] as scrVisualDecoration;
                Require(d && d.GetInstanceID() == item.Id && d.spriteRenderer, "export object changed");
                var r = d.spriteRenderer;
                Require(r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff && r.sharedMaterial
                    && r.sharedMaterial.GetInstanceID() == item.MaterialId && r.sprite && r.sprite.texture.GetInstanceID() == item.TextureId, "export renderer changed");
                candidates.Add(r.GetInstanceID(), i);
            }
            var sigCache = new Dictionary<int, string>();
            var rows = new List<Row>();
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff
                    || (camera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                Require(!r.GetComponentInParent<SortingGroup>(), "live SortingGroup unsupported");
                var materials = r.sharedMaterials; Require(materials.Length > 0, "missing material slot");
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var m = materials[slot]; Require(m && m.renderQueue > 2500, "opaque/missing material in transparent census renderer=" + r.GetInstanceID() + " slot=" + slot);
                    int index; bool owned = candidates.TryGetValue(r.GetInstanceID(), out index);
                    Require(!owned || materials.Length == 1, "owned material slots changed");
                    string sig = "";
                    if (owned)
                    {
                        int matId = m.GetInstanceID();
                        if (!sigCache.TryGetValue(matId, out sig)) { sig = MaterialSignature(m); sigCache.Add(matId, sig); }
                    }
                    rows.Add(new Row { R = r, M = m, Item = owned ? index : -1, Key = MatShare.SortKey(r, m, camera, slot), Signature = sig });
                }
            }
            rows.Sort((a, b) => a.Key.CompareTo(b.Key));
            Require(rows.Count(r => r.Item >= 0) == candidates.Count, "owned census changed");
            int tiedBarrierOwned=0,tiedBarrierBuckets=0;
            for(int a=0;a<rows.Count;)
            {
                int b=a+1;while(b<rows.Count && rows[a].Key.SameBucket(rows[b].Key))b++;
                int owned=0,barriers=0;
                for(int n=a;n<b;n++) {if(rows[n].Item>=0)owned++;else barriers++;}
                if(owned>0 && barriers>0) {tiedBarrierOwned+=owned;tiedBarrierBuckets++;}
                a=b;
            }
            Log("sort_audit owned="+candidates.Count+" barrier_tied_owned="+tiedBarrierOwned+" barrier_tied_buckets="+tiedBarrierBuckets);
            for (int i = 1; i < rows.Count; i++) if (rows[i - 1].Key.SameMaterialTie(rows[i].Key))
                Require(rows[i - 1].Item < 0 && rows[i].Item < 0, "ambiguous owned material tie material=" + rows[i].Key.Material + " renderers=" + rows[i - 1].R.GetInstanceID() + "," + rows[i].R.GetInstanceID());
            var originalItems = data.Items;
            var sortedOwnedItems = rows.Where(r => r.Item >= 0).Select(r => originalItems[r.Item]).ToArray();
            if (excludedItemIds == null)
            {
                data.Items = sortedOwnedItems;
            }
            else
            {
                var clone = new GpuDecoData
                {
                    Schema = data.Schema, RawTime = data.RawTime, BakeTime = data.BakeTime,
                    Pitch = data.Pitch, TweenScale = data.TweenScale, PixelsPerWorld = data.PixelsPerWorld,
                    Frame = data.Frame, Awake = data.Awake, Supported = data.Supported,
                    Fingerprint = data.Fingerprint, Camera = data.Camera, Sources = data.Sources,
                    MeanFrameSeconds = data.MeanFrameSeconds, FrameSamples = data.FrameSamples, MeanProgressStep = data.MeanProgressStep,
                    Items = sortedOwnedItems
                };
                data = clone;
            }
            activeData = data;
            var map = new Dictionary<int, int>(); for (int i = 0; i < data.Items.Length; i++) map.Add(data.Items[i].Id, i);
            rowByItemId.Clear();
            foreach (var r in rows)
            {
                if (r.Item >= 0)
                {
                    int itemId = originalItems[r.Item].Id;
                    r.Item = map[itemId];
                    rowByItemId[itemId] = r;
                }
            }
            bundle = AssetBundle.LoadFromFile(Path.GetFullPath(bundlePath)); Require(bundle, "bundle load failed");
            var shader = bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute"); Require(shader, "compute asset missing");
            gpu = new GpuDecoGpu(shader, data); var evaluated = gpu.Dispatch(data.BakeTime, true);
            evaluatedVertices = evaluated.vertices;
            double maximum = 0, sum = 0, colorMax = 0, colorSum = 0; int over = 0, colorOver = 0;
            for (int i = 0; i < data.Items.Length; i++) for (int k = 0; k < 4; k++)
            {
                var d = data.Items[i]; var v = evaluated.vertices[i * 4 + k];
                var expected = new Vector3(d.World[k * 3], d.World[k * 3 + 1], d.World[k * 3 + 2]);
                double error = Vector3.Distance(v.position, expected) * data.PixelsPerWorld;
                Require(DecoBakeCore.Finite(error), "nonfinite GPU vertex"); maximum = Math.Max(maximum, error); sum += error; if (error > .01) over++;
                for (int c = 0; c < 4; c++)
                {
                    error = Math.Abs((double)v.color[c] - d.Color[c]); Require(DecoBakeCore.Finite(error), "nonfinite GPU color");
                    colorMax = Math.Max(colorMax, error); colorSum += error; if (error > 1.0 / 255) colorOver++;
                }
            }
            for (int ch = 0; ch < 16; ch++)
            {
                double max = 0, total = 0;
                for (int i = 0; i < data.Items.Length; i++) { double error = Math.Abs((double)evaluated.values[i * 16 + ch] - data.Items[i].Actual[ch]); max = Math.Max(max, error); total += error; }
                Log("G0 t=" + song.ToString("R", CultureInfo.InvariantCulture) + " slot=" + ch + " max=" + max.ToString("G12", CultureInfo.InvariantCulture) + " mean=" + (total / Math.Max(1, data.Items.Length)).ToString("G12", CultureInfo.InvariantCulture) + " source=paused_gpu_snapshot");
            }
            Log("G0_snapshot vertices=" + (data.Items.Length * 4) + " max_px=" + maximum.ToString("G12", CultureInfo.InvariantCulture)
                + " mean_px=" + (sum / Math.Max(1, data.Items.Length * 4)).ToString("G12", CultureInfo.InvariantCulture) + " over_tol=" + over
                + " rgba_max=" + colorMax.ToString("G12", CultureInfo.InvariantCulture) + " rgba_mean=" + (colorSum / Math.Max(1, data.Items.Length * 16)).ToString("G12", CultureInfo.InvariantCulture) + " rgba_over_tol=" + colorOver);
            g0 = over == 0 && colorOver == 0;
            Require(g0 || GpuDecoCoverage.Auxiliary(data.Awake), "G0_FAIL before renderer ownership");

            bool quantizeColor = DiagnosticMode != "floatcolor" && DiagnosticMode != "float_single_localz" && (DiagnosticMode == "unorm8" || UseQuantizedVertexColor);
            bool singleRun = DiagnosticMode == "single" || DiagnosticMode == "float_single_localz" || GpuDecoNativeBatch.Enabled;
            bool localZ = DiagnosticMode == "localz" || DiagnosticMode == "float_single_localz";

            if(GpuDecoNativeBatch.Enabled && GpuDecoNativeBatch.StaticCombine)
            {
                Require(DiagnosticMode=="off","native batch requires default draw mode");
                var nativeOriginals=rows.Where(r=>r.Item>=0).Select(r=>(SpriteRenderer)r.R).ToArray();
                nativeBatch=GpuDecoNativeBatch.Build(transform,data,evaluated.vertices,nativeOriginals,shader,gpu.VertexBuffer);
                originals.AddRange(nativeOriginals);
                foreach(var original in originals)original.forceRenderingOff=true;
                nativeBatch.SetEnabled(true);
                Log("on=True scope=paused_G1_only owned="+originals.Count+" awake="+data.Awake+" native_sort=True renderers="+nativeBatch.RendererCount
                    +" buffers="+nativeBatch.BufferCount+" G0="+(g0?"PASS":"FAIL_AUXILIARY"));
                return;
            }

            commands = new CommandBuffer { name = "GpuDeco.G1 copy runs" }; int copy = shader.FindKernel("CopyRun");
            commands.SetComputeBufferParam(shader, copy, "_CopySource", gpu.VertexBuffer);
            commands.SetComputeIntParam(shader, "_CopyQuantizeColor", quantizeColor ? 1 : 0);
            int runCount = 0, submeshCount = 0;
            for (int a = 0; a < rows.Count;)
            {
                if (rows[a].Item < 0) { a++; continue; }
                int b = a + 1;
                while (!singleRun && b < rows.Count && rows[b].Item >= 0 && rows[a].Key.SameBucket(rows[b].Key)
                    && rows[a].R.gameObject.layer == rows[b].R.gameObject.layer) b++;
                submeshCount += BuildRun(data, rows, a, b, shader, copy, evaluated.vertices[rows[a].Item * 4].position.z, runCount, singleRun, localZ);
                runCount++; a = b;
            }
            if(GpuDecoCopy.Enabled)
            {
                Require(GpuDecoNativeBatch.Enabled && !GpuDecoNativeBatch.StaticCombine && DiagnosticMode=="off", "native copy requires independent default renderers");
                nativeCopy=GpuDecoCopy.TryCreate(shader,gpu.VertexBuffer,buffers,data.Items.Length*4);
                Require(nativeCopy!=null && nativeCopy.Execute(), "native copy unavailable: "+GpuDecoCopy.Status);
            }
            else Graphics.ExecuteCommandBuffer(commands);

            if (DiagnosticMode == "world" || localZ)
            {
                int offset = 0;
                for (int n = 0; n < buffers.Count; n++)
                {
                    var buffer = buffers[n]; var points = new Vertex[meshes[n].vertexCount]; buffer.GetData(points);
                    float runZ = renderers[n].transform.localPosition.z;
                    for (int j = 0; j < points.Length; j++)
                    {
                        var item = data.Items[(offset + j) / 4]; int k = (offset + j) % 4;
                        if (DiagnosticMode == "world")
                            points[j].position = new Vector3(item.World[k * 3], item.World[k * 3 + 1], item.World[k * 3 + 2] - runZ);
                        else if (localZ)
                            points[j].position = new Vector3(points[j].position.x, points[j].position.y, points[j].position.z - runZ);
                    }
                    buffer.SetData(points); offset += points.Length;
                }
            }

            actualVerticesByRun.Clear();
            int first = 0;
            for (int n = 0; n < buffers.Count; n++)
            {
                var actual = new Vertex[meshes[n].vertexCount]; buffers[n].GetData(actual);
                actualVerticesByRun[n] = actual;
                runFirstItemByRun[n] = first / 4;
                float runZ = renderers[n].transform.localPosition.z;
                for (int i = 0; i < actual.Length; i++)
                {
                    var expected = evaluated.vertices[first + i];
                    if (DiagnosticMode == "world")
                    {
                        var d = data.Items[(first + i) / 4]; int k = (first + i) % 4;
                        expected.position = new Vector3(d.World[k * 3], d.World[k * 3 + 1], d.World[k * 3 + 2] - runZ);
                    }
                    else if (localZ)
                    {
                        expected.position = new Vector3(expected.position.x, expected.position.y, expected.position.z - runZ);
                    }
                    Require(actual[i].position.Equals(expected.position)
                        && DrawColorMatches(actual[i].color, expected.color, quantizeColor) && actual[i].uv.Equals(expected.uv), "GPU run copy mismatch");
                }
                first += actual.Length;
            }
            foreach (var r in rows) if (r.Item >= 0) originals.Add((SpriteRenderer)r.R);
            foreach (var r in originals) r.forceRenderingOff = true;
            foreach (var r in renderers) r.enabled = true;
            int executedDispatches=1+(nativeCopy!=null?1+(GpuDecoCopy.EightUavEnabled?(buffers.Count+7)/8:0):runCount);
            Log("on=True scope=paused_G1_only owned=" + originals.Count + " awake=" + data.Awake + " runs=" + runCount + " submeshes=" + submeshCount
                + " dispatches=" + executedDispatches + " legacy_copy_commands="+runCount+" prepare_ms=" + timer.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture)
                + " draw_color=" + (quantizeColor ? "UNorm8_equivalent" : "Float32_exact")
                + " logical_color=Float32 prepare_scope=paused_diagnostic steady_cost=UNMEASURED G0=" + (g0 ? "PASS" : "FAIL_AUXILIARY"));
            Log("draw_diagnostic=" + DiagnosticMode + " truth_vertex_upload=" + (DiagnosticMode == "world") + " acceptance_eligible=" + (DiagnosticMode == "off" && excludedItemIds == null));
            Log("native_sort="+GpuDecoNativeBatch.Enabled+" static_batch=False renderers="+renderers.Count
                +" legacy_copy_commands="+runCount+" native_copy="+GpuDecoCopy.Enabled+" eight_uav="+GpuDecoCopy.EightUavEnabled
                +" copy_dispatches="+(GpuDecoCopy.Enabled?(GpuDecoCopy.EightUavEnabled?(buffers.Count+7)/8:0):runCount)
                +" copy_regions="+(GpuDecoCopy.Enabled && !GpuDecoCopy.EightUavEnabled?buffers.Count:0)
                +" normalize_dispatches="+(GpuDecoCopy.Enabled?1:0));
        }

        static bool DrawColorMatches(Color actual, Color logical, bool quantized)
        {
            Color expected = quantized ? GpuDecoGpu.DrawColor(logical) : logical;
            for (int c = 0; c < 4; c++) if (!DecoBakeCore.Finite(actual[c]) || Mathf.Abs(actual[c] - expected[c]) > 1e-7f) return false;
            return true;
        }

        int BuildRun(GpuDecoData data, List<Row> rows, int a, int b, ComputeShader shader, int copy, float z, int runIndex, bool singleRun, bool localZ)
        {
            var first = rows[a]; int count = b - a;
            var groups = new List<KeyValuePair<int, int>>();
            for (int n = 0; n < count;)
            {
                int start = n;
                int texId = data.Items[rows[a + start].Item].TextureId;
                string sig = rows[a + start].Signature;
                rows[a + start].RunIndex = runIndex;
                rows[a + start].SubmeshIndex = groups.Count;
                n++;
                while (DiagnosticMode == "merged" && n < count
                    && data.Items[rows[a + n].Item].TextureId == texId
                    && rows[a + n].Signature == sig)
                {
                    rows[a + n].RunIndex = runIndex;
                    rows[a + n].SubmeshIndex = groups.Count;
                    n++;
                }
                groups.Add(new KeyValuePair<int, int>(start, n - start));
            }
            var mesh = new Mesh { name = "GpuDeco.G1 run", indexFormat = IndexFormat.UInt32, vertexBufferTarget = GraphicsBuffer.Target.Raw };
            meshes.Add(mesh);
            mesh.SetVertexBufferParams(count * 4,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            mesh.SetVertexBufferData(new Vertex[count * 4], 0, 0, count * 4);
            mesh.subMeshCount = groups.Count;
            for (int g = 0; g < groups.Count; g++)
            {
                int start = groups[g].Key, len = groups[g].Value;
                var indices = new int[len * 6];
                for (int k = 0; k < len; k++)
                {
                    var tri = data.Items[rows[a + start + k].Item].Triangles;
                    int baseVertex = (start + k) * 4;
                    for (int t = 0; t < 6; t++) indices[k * 6 + t] = tri[t] + baseVertex;
                }
                mesh.SetIndices(indices, MeshTopology.Triangles, g, false);
            }
            // Match the exact sort-reference point used by MatShare.SortKey so
            // camera distance sorting against non-owned barriers is bit-identical.
            Vector3 sortCenter = first.R.bounds.center;
            var sr = first.R as SpriteRenderer;
            if (sr && sr.spriteSortPoint == SpriteSortPoint.Pivot) sortCenter = first.R.transform.position;
            float goZ = localZ ? z : 0f;
            mesh.bounds = new Bounds(sortCenter-new Vector3(0f,0f,goZ), new Vector3(2000000, 2000000, 0));
            mesh.UploadMeshData(true);
            var buffer = mesh.GetVertexBuffer(0);
            buffers.Add(buffer);
            var go = new GameObject("GpuDeco.G1 run") { hideFlags = HideFlags.HideAndDontSave, layer = first.R.gameObject.layer };
            go.transform.SetParent(transform, false);
            if (localZ) go.transform.localPosition = new Vector3(0f, 0f, goZ);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            renderers.Add(renderer);
            renderer.sharedMaterials = groups.Select(g => rows[a + g.Key].M).ToArray();
            renderer.sortingLayerID = first.R.sortingLayerID;
            renderer.sortingOrder = first.R.sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var originalKey=MatShare.SortKey(first.R,first.M,GpuSplit.FindMain());
            var replacementKey=MatShare.SortKey(renderer,first.M,GpuSplit.FindMain());
            Require(originalKey.SameBucket(replacementKey),"replacement sort-reference bucket changed");
            for (int g = 0; g < groups.Count; g++)
            {
                var props = new MaterialPropertyBlock();
                var sprite = (SpriteRenderer)rows[a + groups[g].Key].R;
                props.SetTexture("_MainTex", sprite.sprite.texture);
                props.SetColor("_RendererColor", Color.white);
                props.SetVector("_Flip", Vector4.one);
                renderer.SetPropertyBlock(props, g);
            }
            commands.SetComputeIntParam(shader, "_CopyFirstVertex", first.Item * 4);
            commands.SetComputeIntParam(shader, "_CopyVertexCount", count * 4);
            commands.SetComputeBufferParam(shader, copy, "_Vertices", buffer);
            commands.DispatchCompute(shader, copy, (count * 4 + 63) / 64, 1, 1);
            return groups.Count;
        }

        // Request A.2: CPU oracle comparing SpriteRenderer vs GPU mesh world vertices, clip/screen coords, and quantized/premultiplied RGBA
        internal static void CompareSpriteVsGpuOracle(scrDecoration[] baseline, int targetDecoId = 0, int maxLogs = 16)
        {
            Require(instance && instance.activeData != null, "start GpuDecoSnapshot before running oracle");
            var cam = GpuSplit.FindMain();
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            int logged = 0;
            for (int i = 0; i < instance.activeData.Items.Length; i++)
            {
                var item = instance.activeData.Items[i];
                if (targetDecoId != 0 && item.Id != targetDecoId) continue;
                var d = baseline[item.Index] as scrVisualDecoration;
                if (!d || !d.spriteRenderer) continue;
                var sr = d.spriteRenderer;
                var mat = sr.sharedMaterial;
                Color matColor = mat && mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Color srLogical = sr.color;
                Color srUnorm8 = GpuDecoGpu.DrawColor(srLogical);
                Color srFinalFloat = new Color(srLogical.r * matColor.r, srLogical.g * matColor.g, srLogical.b * matColor.b, srLogical.a * matColor.a);
                Color srFinalUnorm8 = new Color(srUnorm8.r * matColor.r, srUnorm8.g * matColor.g, srUnorm8.b * matColor.b, srUnorm8.a * matColor.a);

                instance.rowByItemId.TryGetValue(item.Id, out var row);
                int runIdx = row != null ? row.RunIndex : -1;
                int subIdx = row != null ? row.SubmeshIndex : -1;
                float runZ = (runIdx >= 0 && runIdx < instance.renderers.Count) ? instance.renderers[runIdx].transform.localPosition.z : 0f;

                var corners = sr.sprite.vertices;
                float maxWorldPx = 0f, maxClipDiff = 0f;
                string cornerReport = "";
                for (int k = 0; k < 4; k++)
                {
                    Vector2 local2 = corners[k];
                    if (sr.flipX) local2.x = -local2.x;
                    if (sr.flipY) local2.y = -local2.y;
                    Vector3 cpuWorld = sr.transform.TransformPoint(local2);
                    Vector3 gpuWorld = instance.evaluatedVertices[i * 4 + k].position;
                    float distPx = Vector3.Distance(cpuWorld, gpuWorld) * instance.activeData.PixelsPerWorld;
                    if (distPx > maxWorldPx) maxWorldPx = distPx;

                    // Local->World->Clip (SpriteRenderer non-batched) vs World->Clip (MeshRenderer)
                    Matrix4x4 srMvp = vp * sr.transform.localToWorldMatrix;
                    Vector4 clipLocal = srMvp * new Vector4(local2.x, local2.y, 0f, 1f);
                    Vector4 clipWorld = vp * new Vector4(gpuWorld.x, gpuWorld.y, gpuWorld.z, 1f);
                    float clipDiff = Mathf.Max(Mathf.Abs(clipLocal.x - clipWorld.x), Mathf.Abs(clipLocal.y - clipWorld.y));
                    if (clipDiff > maxClipDiff) maxClipDiff = clipDiff;
                    Vector3 scrCpu = cam.WorldToScreenPoint(cpuWorld);
                    Vector3 scrGpu = cam.WorldToScreenPoint(gpuWorld);
                    cornerReport += string.Format(CultureInfo.InvariantCulture, "c{0}:cpu=({1:F3},{2:F3})_gpu=({3:F3},{4:F3})_dpx={5:E3};",
                        k, scrCpu.x, scrCpu.y, scrGpu.x, scrGpu.y, distPx);
                }
                Color gpuVertCol = instance.evaluatedVertices[i * 4].color;
                Color gpuQuantCol = GpuDecoGpu.DrawColor(gpuVertCol);
                int dr = Mathf.Abs(Mathf.RoundToInt(srFinalFloat.r * srFinalFloat.a * 255f) - Mathf.RoundToInt(gpuQuantCol.r * gpuQuantCol.a * 255f));
                int dg = Mathf.Abs(Mathf.RoundToInt(srFinalFloat.g * srFinalFloat.a * 255f) - Mathf.RoundToInt(gpuQuantCol.g * gpuQuantCol.a * 255f));
                int db = Mathf.Abs(Mathf.RoundToInt(srFinalFloat.b * srFinalFloat.a * 255f) - Mathf.RoundToInt(gpuQuantCol.b * gpuQuantCol.a * 255f));
                int da = Mathf.Abs(Mathf.RoundToInt(srFinalFloat.a * 255f) - Mathf.RoundToInt(gpuQuantCol.a * 255f));

                if (targetDecoId != 0 || maxWorldPx > 1e-4f || maxClipDiff > 1e-6f || dr + dg + db + da > 0)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        "G1_oracle id={0} idx={1} run={2} submesh={3} tex={4} mat={5} order={6} z={7:R} boundsZ={8:R} runZ={9:R} maxWorldPx={10:E4} maxClipDiff={11:E4} srRGBA=({12:R},{13:R},{14:R},{15:R}) gpuFloatRGBA=({16:R},{17:R},{18:R},{19:R}) gpuUNorm8RGBA=({20:R},{21:R},{22:R},{23:R}) premulByteDiff=({24},{25},{26},{27}) corners={28}",
                        item.Id, item.Index, runIdx, subIdx, item.TextureId, item.MaterialId, item.Order,
                        gpuWorldZ(instance.evaluatedVertices[i * 4]), sr.bounds.center.z, runZ,
                        maxWorldPx, maxClipDiff,
                        srLogical.r, srLogical.g, srLogical.b, srLogical.a,
                        gpuVertCol.r, gpuVertCol.g, gpuVertCol.b, gpuVertCol.a,
                        gpuQuantCol.r, gpuQuantCol.g, gpuQuantCol.b, gpuQuantCol.a,
                        dr, dg, db, da, cornerReport));
                    if (++logged >= maxLogs) break;
                }
            }
        }
        static float gpuWorldZ(Vertex v) => v.position.z;

        internal static bool TryGetRunSubmesh(int itemId, out int runIdx, out int submeshIdx)
        {
            runIdx = -1; submeshIdx = -1;
            if (!instance || !instance.rowByItemId.TryGetValue(itemId, out var row)) return false;
            if(row.RunIndex<0 || row.SubmeshIndex<0)return false;
            runIdx = row.RunIndex; submeshIdx = row.SubmeshIndex;
            return true;
        }

        void Update()
        {
            if (!FrameCompare.Paused || !ADOBase.controller || !ADOBase.controller.paused || Time.timeScale != 0
                || generation != DecoBake.Generation || !scrConductor.instance || scrConductor.instance.songposition_minusi != song) Stop();
        }
        internal static void Stop()
        {
            var p = instance; instance = null; if (ReferenceEquals(p, null)) return;
            Release(()=>p.nativeCopy?.Dispose());p.nativeCopy=null;
            Release(()=>p.nativeBatch?.Dispose());p.nativeBatch=null;
            foreach (var r in p.renderers) if (r) r.enabled = false; p.renderers.Clear();
            bool restored = true;
            foreach (var r in p.originals) try { if (r) r.forceRenderingOff = false; } catch (Exception e) { restored = false; Log("restore_error=" + e.Message); }
            p.originals.Clear(); Release(() => p.commands?.Dispose()); p.commands = null;
            foreach (var b in p.buffers) Release(b.Dispose); p.buffers.Clear(); Release(() => p.gpu?.Dispose()); p.gpu = null;
            foreach (var m in p.meshes) if (m) Destroy(m); p.meshes.Clear(); if (p.bundle) Release(() => p.bundle.Unload(true)); p.bundle = null;
            if (p) Destroy(p.gameObject); Log("draw_off=True originals_restored=" + restored);
        }
        static void Release(Action action) { try { action(); } catch (Exception e) { Log("release_error=" + e.Message); } }
        void OnDestroy() { if (instance == this) Stop(); }
    }
}
#endif
