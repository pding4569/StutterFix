#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Research only. Zero-duration opacity has no tween/proxy registration and no
    // Transform mutation. Other channels/durations deliberately retain FloorAnim.
    internal static class FloorRange
    {
        private static bool enabled, ready, busy, shadow;
        private static Harmony patches;
        private static List<scrFloor> floors;
        private static FloorRangeLedger ledger;
        private static int[] visibleCounts;
        private static bool[] culled;
        private static readonly Dictionary<scrFloor, int> indices = new Dictionary<scrFloor, int>();
        private static readonly Dictionary<string, long> reasons = new Dictionary<string, long>();
        private static readonly HashSet<MethodBase> patched = new HashSet<MethodBase>();
        private static Action<int, float> apply = Apply;
        private static long accepted, chunks, eager, materialized, comparisons, mismatch, patchFailures;
        private static long large, largeZero, largeDuration, largeOpa, largePos, largeRot, largeScale;
        private static long startTicks, enqueueTicks, eagerTicks, pullTicks, tickTicks, verifyTicks;
        private static int sweep;
        private static bool mismatchLatched;
        private static int ownerThread;
        private static bool listening;
        private static bool lifecycleReady;
        private static volatile bool coverageChanged;
        private static readonly FieldInfo opacity = AccessTools.Field(typeof(scrFloor), "opacity");
        private static readonly FieldInfo sprite = AccessTools.Field(typeof(scrFloor), "dontChangeMySprite");
        private static readonly FieldInfo tweens = AccessTools.Field(typeof(scrFloor), "moveTweens");
        private static readonly FieldInfo renderer = AccessTools.Field(typeof(scrFloor), "floorRenderer");

        private static void Log(string s) { Main.Entry.Logger.Log("[타일구간] " + s); }
        private static void Reason(string s) { long n; reasons.TryGetValue(s, out n); reasons[s] = n + 1; }
        internal static void Configure(bool on) { enabled = on; }
        internal static void Install(Harmony harmony)
        {
            if (!listening) { AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded; listening = true; }
            lifecycleReady = false;
            try
            {
                var method = AccessTools.Method(typeof(scnGame), "ApplyEventsToFloors", new[] { typeof(List<scrFloor>), typeof(ADOFAI.LevelData), typeof(scrLevelMaker), typeof(List<ADOFAI.LevelEvent>) });
                if (method == null) { Reason("prepare-hook-missing"); return; }
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(FloorRange), nameof(PreparedHook)));
                var constructors = typeof(FreeroamArea).GetConstructors(AccessTools.all);
                if (constructors.Length == 0) { Reason("freeroam-hook-missing"); return; }
                foreach (var ctor in constructors)
                    harmony.Patch(ctor, prefix: new HarmonyMethod(typeof(FloorRange), nameof(FreeroamCreated)));
                lifecycleReady = true;
            }
            catch (Exception ex) { Reason("lifecycle-install:" + ex.GetType().Name); Log(Status()); }
        }
        public static void FreeroamCreated() { if (ready) { Reason("freeroam-created-reprepare"); ResetMap(); } }
        public static void PreparedHook()
        {
            if (!enabled) return;
            try { Prepare(); Log(Status()); }
            catch (Exception ex) { ready = false; Reason("prepare-exception:" + ex.GetType().Name); Log(Status()); }
        }
        internal static void Command(string arg)
        {
            switch (arg.Trim())
            {
                case "on": enabled = true; shadow = false; Prepare(); break;
                case "shadow": enabled = true; shadow = true; Prepare(); break;
                case "off": Shutdown(); break;
                case "flush": Flush("command"); break;
                case "status": break;
                default: Log("tilerange on/shadow/off/flush/status; on/shadow는 맵 준비 뒤 재생 전에 실행"); return;
            }
            Log(Status());
        }

        // Explicit one-time preparation: never enumerate tiles in StartEffect.
        // Patching reads/writes is also done here, before accepting any range.
        private static void Prepare()
        {
            Flush("prepare"); ClearMap();
            if (!lifecycleReady || opacity == null || sprite == null || tweens == null || renderer == null)
            { Reason("lifecycle-or-fields-unavailable"); return; }
            ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            coverageChanged = false;
            if (!listening) { AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded; listening = true; }
            var list = ADOBase.lm == null ? null : ADOBase.lm.listFloors;
            if (list == null) { Reason("prepare-no-map"); return; }
            if (patches == null) patches = new Harmony("StutterFix.FloorRange.Research");
            bool ok = InstallReaders();
            if (!ok) { Reason("reader-coverage-failed"); return; }
            floors = list; ledger = new FloorRangeLedger(list.Count);
            visibleCounts = new int[ledger.Chunks.Length]; culled = new bool[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f == null || indices.ContainsKey(f)) { ledger.Chunks[i / 64].Blocked = true; Reason("null-or-alias-floor"); continue; }
                indices.Add(f, i);
                culled[i] = TileCull.IsCulled(f);
                if (!culled[i]) visibleCounts[i / 64]++;
                if (f.freeroamArea != null || f.floorRenderer == null || f.moveTweens == null)
                { ledger.Chunks[i / 64].Blocked = true; Reason("freeroam-or-missing-state"); continue; }
                // Real DOTween callbacks cannot be delayed. A later dictionary
                // exposure also fences its chunk through ReadTweens below.
                foreach (var t in f.moveTweens.Values)
                    if ((object)t != null && t.active && !(t.id is FloorAnim.Rec))
                    { ledger.Chunks[i / 64].Blocked = true; Reason("real-tween-at-prepare"); break; }
            }
            ready = true;
            Log("prepared floors=" + list.Count + " chunks=" + ledger.Chunks.Length + " patched=" + patched.Count + " shadow=" + shadow);
        }

        internal static bool TryStart(List<scrFloor> list, int start, int end, int step, ref FloorAnim.Ctx c)
        {
            if (step > 0 && (long)end - start >= 0 && ((long)end - start) / step + 1 >= 1000)
            {
                large++; if (c.Zero) largeZero++; else largeDuration++;
                if (c.OpaOnly) largeOpa++;
                if (c.PosUsed && !(float.IsNaN(c.Pos.x) && float.IsNaN(c.Pos.y))) largePos++;
                if (c.RotUsed) largeRot++;
                if (c.ScaleUsed && !(float.IsNaN(c.Scale.x) && float.IsNaN(c.Scale.y))) largeScale++;
            }
            if (!enabled) return false;
            if (coverageChanged) { Flush("assembly-load"); Reason("assembly-set-changed-reprepare"); return false; }
            if (!ready || !ReferenceEquals(list, floors) || list.Count != ledger.Count)
            { Reason("not-prepared-or-map-changed"); return false; }
            if (mismatchLatched) { Reason("shadow-mismatch-latched"); return false; }
            if (FloorAnim.Split) { Reason("split-enabled"); return false; }
            if (TileSim.Capturing) { Reason("tilesim-capture"); return false; }
            if (!c.Zero) { Reason("duration-not-supported"); return false; }
            if (!c.OpaOnly) { Reason("non-opacity-channel"); return false; }
            if (step != 1 || start < 0 || end >= list.Count || end < start)
            { Reason("gap-or-invalid-range"); return false; }
            if (end - start + 1 < 1000) { Reason("below-1000"); return false; }
            long begin = Stopwatch.GetTimestamp();
            accepted++;
            var entry = new FloorRangeLedger.Entry { Start = start, End = end, Opacity = c.Opacity };
            for (int ch = start / 64; ch <= end / 64; ch++)
            {
                long t = Stopwatch.GetTimestamp();
                bool added = ledger.Append(ch, entry);
                enqueueTicks += Stopwatch.GetTimestamp() - t;
                if (added) { chunks++; continue; }
                Reason(ledger.Chunks[ch].Blocked ? "chunk-fenced" : "chunk-depth-32");
                t = Stopwatch.GetTimestamp();
                // Fallback is local to the chunk, and preserves ordering of every
                // older zero effect (overwrite is not a global Stop).
                int lo = Math.Max(start, ch * 64), hi = Math.Min(end, ch * 64 + 63);
                for (int i = lo; i <= hi; i++)
                {
                    Materialize(list[i], "chunk-fallback");
                    FloorAnim.RangeLegacy(list[i], ref c); eager++;
                }
                eagerTicks += Stopwatch.GetTimestamp() - t;
            }
            startTicks += Stopwatch.GetTimestamp() - begin;
            Log("start tiles=" + (end - start + 1) + " ms=" + Ms(Stopwatch.GetTimestamp() - begin).ToString("F3") + " deferred-chunks-total=" + chunks + " eager-tiles-total=" + eager);
            return true;
        }

        private static void Apply(int i, float target)
        {
            var f = floors[i];
            // Complete a known FloorAnim record before capturing the candidate
            // baseline. Real tweens were excluded before acceptance.
            FloorAnim.RangeCompleteOpacity(f);
            bool verify = shadow && ((i & 15) == 0 || Visible(f));
            if (!verify) { FloorAnim.RangeWriteOpacity(f, target); materialized++; return; }
            long stamp = Stopwatch.GetTimestamp();
            var t = f.transform; var p = t.position; var r = t.eulerAngles; var s = t.localScale;
            float before = f.opacity; bool beforeSprite = f.dontChangeMySprite;
            FloorAnim.RangeWriteOpacity(f, target);
            float expected = f.opacity; bool expectedSprite = f.dontChangeMySprite;
            var candidateP = t.position; var candidateR = t.eulerAngles; var candidateS = t.localScale;
            f.opacity = before; f.dontChangeMySprite = beforeSprite;
            // Independent original FloorAnim path is the oracle, with its normal
            // dontChangeMySprite side effect and completed predecessor.
            FloorAnim.RangeReferenceOpacity(f, target);
            comparisons++;
            if (!Bits(expected, f.opacity) || expectedSprite != f.dontChangeMySprite || !Bits(p, candidateP) || !Bits(r, candidateR) || !Bits(s, candidateS) || !Bits(candidateP, t.position) || !Bits(candidateR, t.eulerAngles) || !Bits(candidateS, t.localScale))
            {
                mismatch++; mismatchLatched = true;
                if (mismatch <= 8) Log("mismatch tile=" + i + " opacity expected=" + expected.ToString("R") + " actual=" + f.opacity.ToString("R")
                    + " sprite=" + expectedSprite + "/" + f.dontChangeMySprite
                    + " position-bits=" + Hex(candidateP) + "/" + Hex(t.position)
                    + " euler-bits=" + Hex(candidateR) + "/" + Hex(t.eulerAngles)
                    + " scale-bits=" + Hex(candidateS) + "/" + Hex(t.localScale));
            }
            verifyTicks += Stopwatch.GetTimestamp() - stamp; materialized++;
        }
        private static bool Bits(float a, float b) { return BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b); }
        private static bool Bits(Vector3 a, Vector3 b) { return Bits(a.x,b.x) && Bits(a.y,b.y) && Bits(a.z,b.z); }
        private static string Hex(Vector3 v) { return BitConverter.SingleToInt32Bits(v.x).ToString("X8") + "," + BitConverter.SingleToInt32Bits(v.y).ToString("X8") + "," + BitConverter.SingleToInt32Bits(v.z).ToString("X8"); }
        private static bool Visible(scrFloor f) { return !TileCull.IsCulled(f); }
        internal static void SetCulled(scrFloor f, bool value)
        {
            int i;
            if (!ready || !indices.TryGetValue(f, out i) || culled[i] == value) return;
            culled[i] = value; visibleCounts[i / 64] += value ? -1 : 1;
        }

        internal static void Materialize(scrFloor f, string reason)
        {
            if (!ready || busy || ReferenceEquals(f, null)) return;
            int i; if (!indices.TryGetValue(f, out i)) return;
            var ch = ledger.Chunks[i / 64];
            if (ch.Entries.Count == 0 || ch.Cursor[i % 64] == ch.Entries.Count) return;
            long t = Stopwatch.GetTimestamp(); busy = true;
            try { ledger.Drain(i, apply); Reason("materialize:" + reason); }
            catch (Exception ex) { ch.Blocked = true; Reason("materialize-error:" + ex.GetType().Name); throw; }
            finally { busy = false; pullTicks += Stopwatch.GetTimestamp() - t; }
        }
        private static void Fence(scrFloor f, string reason)
        {
            Materialize(f, reason);
            int i;
            if (ready && !ReferenceEquals(f, null) && indices.TryGetValue(f, out i))
            { ledger.Chunks[i / 64].Blocked = true; Reason("fence:" + reason); }
        }
        internal static void BeforeLegacy(scrFloor f) { Materialize(f, "legacy-floor"); }
        internal static void Tick()
        {
            if (!ready) return;
            if (coverageChanged) { Flush("assembly-load"); ClearMap(); Reason("assembly-set-changed-reprepare"); return; }
            if (ADOBase.lm == null || !ReferenceEquals(floors, ADOBase.lm.listFloors) || floors.Count != ledger.Count)
            { ClearMap(); Reason("map-invalidated"); return; }
            long t = Stopwatch.GetTimestamp();
            int current = ADOBase.controller == null || ADOBase.controller.currFloor == null ? -10000 : ADOBase.controller.currFloor.seqID;
            // Chunk visibility is maintained by TileCull events. Unknown/cull-off
            // chunks are conservatively visible; no native query or full tile scan.
            int swept = ledger.Chunks.Length == 0 ? -1 : sweep++ % ledger.Chunks.Length;
            for (int ch = 0; ch < ledger.Chunks.Length; ch++)
            {
                if (ledger.Chunks[ch].Entries.Count == 0) continue;
                bool full = ch == swept || visibleCounts[ch] > 0 || (current >= ch * 64 - 128 && current < ch * 64 + 64 + 128);
                if (!full && !shadow) continue;
                int hi = Math.Min(floors.Count, ch * 64 + 64);
                for (int i = ch * 64; i < hi; i++)
                {
                    if (full || (shadow && (i & 15) == 0))
                        Materialize(floors[i], ch == swept ? "sweep" : "visible-player-sample");
                }
            }
            tickTicks += Stopwatch.GetTimestamp() - t;
        }
        internal static void Flush(string reason)
        {
            if (!ready || busy) return;
            for (int ch = 0; ch < ledger.Chunks.Length; ch++)
                if (ledger.Chunks[ch].Entries.Count != 0)
                    for (int i = ch * 64; i < Math.Min(floors.Count, ch * 64 + 64); i++) Materialize(floors[i], reason);
        }
        internal static void ResetMap() { Flush("map-reset"); ClearMap(); }
        private static void ClearMap() { ready = false; floors = null; ledger = null; visibleCounts = null; culled = null; indices.Clear(); sweep = 0; }
        internal static void Shutdown()
        {
            Flush("unload"); ClearMap(); enabled = shadow = false;
            if (patches != null) patches.UnpatchAll(patches.Id);
            patches = null; patched.Clear();
            if (listening) { AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded; listening = false; }
        }
        private static void AssemblyLoaded(object sender, AssemblyLoadEventArgs args)
        {
            if (!ready) return;
            var loaded = args.LoadedAssembly;
            if (loaded.IsDynamic) return;
            bool relevant = false;
            try { foreach (var reference in loaded.GetReferencedAssemblies()) if (reference.Name == typeof(scrFloor).Assembly.GetName().Name) relevant = true; }
            catch { relevant = true; }
            if (!relevant) return;
            coverageChanged = true;
            // Loading on the main thread flushes before the new assembly's code
            // can observe an old field. Background loads fence new acceptance;
            // the next main-thread tick drains pending work (see documented limit).
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == ownerThread) Flush("assembly-load");
        }
        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static string Status()
        {
            long pending = 0; if (ledger != null) foreach (var c in ledger.Chunks) pending += c.Entries.Count;
            string s = "enabled=" + enabled + " ready=" + ready + " shadow=" + shadow + " effects=" + accepted + " chunks=" + chunks + " eager=" + eager + " pending=" + pending + " apply=" + materialized + " verify=" + comparisons + " mismatch=" + mismatch + " patch-fail=" + patchFailures;
            s += " large=" + large + " zero=" + largeZero + " duration=" + largeDuration + " opa-only=" + largeOpa + " pos=" + largePos + " rot=" + largeRot + " scale=" + largeScale;
            s += string.Format(" start-ms={0:F3} enqueue-ms={1:F3} eager-ms={2:F3} unaccounted-ms={3:F3} pull-ms={4:F3} tick-ms={5:F3} verify-ms={6:F3}", Ms(startTicks), Ms(enqueueTicks), Ms(eagerTicks), Ms(startTicks-enqueueTicks-eagerTicks), Ms(pullTicks), Ms(tickTicks), Ms(verifyTicks));
            foreach (var pair in reasons) s += " | " + pair.Key + "=" + pair.Value;
            return s;
        }

        public static float ReadOpacity(scrFloor f) { Materialize(f, "opacity-read"); return f.opacity; }
        public static void WriteOpacity(scrFloor f, float v) { Materialize(f, "opacity-write"); f.opacity = v; }
        public static bool ReadSprite(scrFloor f) { Materialize(f, "sprite-read"); return f.dontChangeMySprite; }
        public static void WriteSprite(scrFloor f, bool v) { Materialize(f, "sprite-write"); f.dontChangeMySprite = v; }
        public static Dictionary<global::TweenType, DG.Tweening.Tween> ReadTweens(scrFloor f) { Fence(f, "dictionary-alias"); return f.moveTweens; }
        public static void WriteTweens(scrFloor f, Dictionary<global::TweenType, DG.Tweening.Tween> v) { Fence(f, "dictionary-write"); f.moveTweens = v; }
        public static FloorRenderer ReadRenderer(scrFloor f) { Materialize(f, "renderer-read"); return f.floorRenderer; }
        public static void WriteRenderer(scrFloor f, FloorRenderer v) { Fence(f, "renderer-write"); f.floorRenderer = v; }
        public static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source)
        {
            foreach (var ins in source)
            {
                var fi = ins.operand as FieldInfo;
                if (fi != null && (fi == opacity || fi == sprite || fi == tweens || fi == renderer))
                {
                    string name = fi == opacity ? "Opacity" : fi == sprite ? "Sprite" : fi == tweens ? "Tweens" : "Renderer";
                    if (ins.opcode == OpCodes.Ldfld || ins.opcode == OpCodes.Stfld)
                    {
                        name = (ins.opcode == OpCodes.Ldfld ? "Read" : "Write") + name;
                        ins.opcode = OpCodes.Call; ins.operand = AccessTools.Method(typeof(FloorRange), name);
                    }
                    else if (ins.opcode == OpCodes.Ldflda) throw new InvalidOperationException("floor field address escape");
                }
                yield return ins;
            }
        }
        private static bool InstallReaders()
        {
            bool ok = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (assembly.IsDynamic || name.StartsWith("System") || name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor") || name == "mscorlib" || name == "netstandard" || name.StartsWith("Mono.") || name.Contains("Harmony")) continue;
                bool referencesFloor = assembly == typeof(scrFloor).Assembly || assembly == typeof(FloorRange).Assembly;
                foreach (var reference in assembly.GetReferencedAssemblies()) if (reference.Name == typeof(scrFloor).Assembly.GetName().Name) referencesFloor = true;
                if (!referencesFloor) continue;
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                catch { Reason("scan-types-failed:" + name); ok = false; continue; }
                foreach (var type in types)
                {
                    if (type == null) { ok = false; continue; }
                    if (type == typeof(FloorRange) || type == typeof(FloorRangeLedger) || type.DeclaringType == typeof(FloorRangeLedger) || type == typeof(FloorAnim) || type.DeclaringType == typeof(FloorAnim)) continue;
                    var methods = new List<MethodBase>(type.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly));
                    methods.AddRange(type.GetConstructors(AccessTools.all | BindingFlags.DeclaredOnly));
                    foreach (var method in methods)
                    {
                        if (patched.Contains(method) || method.IsAbstract) continue;
                        try
                        {
                            if (method.GetMethodBody() == null) continue;
                            bool hit = false;
                            // Harmony's optional ILGenerator parameter refers to
                            // framework mscorlib; reflect across that ABI boundary.
                            var read = AccessTools.Method(typeof(PatchProcessor), "GetOriginalInstructions");
                            var args = new object[read.GetParameters().Length]; args[0] = method;
                            foreach (var ins in (IEnumerable<CodeInstruction>)read.Invoke(null, args))
                                if (ins.operand is FieldInfo fi && (fi == opacity || fi == sprite || fi == tweens || fi == renderer)) { hit = true; break; }
                            if (!hit) continue;
                            if (method.ContainsGenericParameters) throw new InvalidOperationException("generic reader");
                            patches.Patch(method, transpiler: new HarmonyMethod(typeof(FloorRange), nameof(Rewrite)));
                            patched.Add(method);
                        }
                        catch (Exception ex) { patchFailures++; ok = false; Reason("patch:" + type.FullName + "." + method.Name + ":" + ex.GetType().Name); }
                    }
                }
            }
            return ok;
        }
    }
}
#endif
