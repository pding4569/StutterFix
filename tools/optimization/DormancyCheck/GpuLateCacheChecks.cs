using StutterFix;

static class GpuLateCacheChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var a = new scrVisualDecoration { Visible = true };
        var b = new scrVisualDecoration { Visible = true };
        var asleep = new scrVisualDecoration();
        var manager = new scrDecorationManager();
        manager.allDecorations.AddRange(new[] { a, b, asleep });
        Dormancy.ForgetLevel(); Hitch.Playing = true;
        TightLate.GpuListCaching = DecoBake.GpuLiveActive = true;
        void Frame() { UnityEngine.Time.frameCount++; Dormancy.NewFrame(); Dormancy.List(manager); }
        int Version()
        {
            check(Dormancy.TryGpuListVersion(Dormancy.AwakeList, out int v), "real list version available");
            return v;
        }
        Frame(); Dormancy.Sleep(asleep); Frame();
        int version = Version();
        var cache = new GpuLateListCache<scrDecoration>();
        var owned = new HashSet<scrDecoration> { a };
        int queries = 0, skipped = 0, epoch = 1;
        void Prepare() => cache.Prepare(Dormancy.AwakeList, Version(), epoch,
            d => { queries++; return owned.Contains(d); }, d => skipped++);
        Prepare();
        check(cache.Count == 1 && ReferenceEquals(cache.Items[0], b) && cache.Skipped == 1, "GPU excluded, CPU fallback retained");
        int queriesBefore = queries;
        for (int f = 0; f < 20; f++) { Frame(); Prepare(); }
        check(Version() == version && queries == queriesBefore && cache.Rebuilds == 1, "unchanged frames do not rescan or rewrite source");
        asleep.Visible = true; Dormancy.WakePost(asleep); Frame(); Prepare();
        check(cache.Items.Take(cache.Count).SequenceEqual(new[] { b, asleep }), "wake delta keeps fallback order");
        asleep.Visible = false; Dormancy.Sleep(asleep); Frame(); Prepare();
        check(cache.Count == 1 && cache.Items.Skip(cache.Count).All(d => d == null), "sleep delta releases old array tail");
        owned.Clear(); owned.Add(b); epoch++; Prepare();
        check(cache.Count == 1 && ReferenceEquals(cache.Items[0], a), "same-sized ownership replacement invalidates");
        cache.Clear(); check(cache.Count == 0 && cache.Items.All(d => d == null), "off/unload clears retained objects");
        owned.Clear(); epoch++; Prepare();
        check(cache.Items.Take(cache.Count).SequenceEqual(Dormancy.AwakeList), "CPU restoration returns every member");

        // 개수가 같은 교체·순서 변경도 List._version으로 감지한다.
        var replacement = new scrVisualDecoration { Visible = true };
        manager.allDecorations[0] = replacement; Frame(); Prepare();
        check(cache.Items.Take(cache.Count).SequenceEqual(manager.allDecorations), "same-count source replacement invalidates");
        manager.allDecorations.Reverse(); Frame(); Prepare();
        check(cache.Items.Take(cache.Count).SequenceEqual(manager.allDecorations), "reorder preserves manager order");
        Dormancy.Sleep(asleep); Frame();
        TightLate.GpuListCaching = false; version = Version(); Frame();
        check(Version() != version, "A path retains original per-frame compaction writes");
        TightLate.GpuListCaching = true;

        var list = new List<scrDecoration> { a, null, b, a };
        cache.Clear(); owned.Add(a);
        cache.Prepare(list, 1, 10, owned.Contains, _ => { });
        check(cache.Count == 2 && cache.Items[0] == null && ReferenceEquals(cache.Items[1], b) && cache.Skipped == 2,
            "null retained, owned duplicates excluded individually");
        cache.Prepare(new List<scrDecoration>(list), 1, 10, _ => false, _ => { });
        check(cache.Count == 4, "new list identity invalidates equal version/count");
        try { cache.Prepare(list, 2, 10, _ => throw new InvalidOperationException(), _ => { }); }
        catch (InvalidOperationException) { }
        cache.Prepare(list, 2, 10, _ => false, _ => { });
        check(cache.Count == 4, "exception does not publish partial cache");
        list.RemoveAt(3); cache.Prepare(list, 2, 10, _ => false, _ => { });
        check(cache.Count == 3 && cache.Items[3] == null, "count mismatch invalidates even with equal version");
        cache.Clear(); cache.Clear();
        DecoBake.GpuLiveActive = TightLate.GpuListCaching = false; Dormancy.ForgetLevel();
        check(!Dormancy.TryGpuListVersion(list, out _), "untracked source refuses caching");
    }
}
