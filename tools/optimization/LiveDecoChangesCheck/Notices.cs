using UnityEngine;

namespace StutterFix;

static partial class NoticeAdapter
{
    sealed class Image { internal bool Owned; internal scrDecoration D; internal Renderer R; }
    sealed class Source { internal Image Image; internal bool Invalidate, Queued; }
    static readonly Dictionary<UnityEngine.Object, HashSet<Source>> watchers = new();
    static readonly List<Source> pending = new();
    static bool Active = true, restoring;
    static long writeCallbacks, watcherVisits;
    static int releases, registrations;
    static void Release(Image image, string reason) { image.Owned = false; releases++; }
    static void RegisterRenderer(Renderer renderer) { registrations++; }
    static void WriteFailed(Exception ex) => throw new Exception("production notification failed", ex);

    internal static void Check()
    {
        int checks = 0;
        void Expect(bool valid, string label) { checks++; if (!valid) throw new Exception(label); }
        var pivot = new Transform(); var child = new Transform();
        var d = new scrDecoration { pivotTrans = pivot };
        var r = new Renderer { transform = child, gameObject = new GameObject() };
        var image = new Image { Owned = true, D = d, R = r };
        var source = new Source { Image = image };
        watchers[d] = new() { source }; watchers[pivot] = new() { source }; watchers[r] = new() { source };
        BeforeWrite(d, LiveDecoChanges.Script); AfterWrite(d, LiveDecoChanges.Script);
        Expect(pending.Count == 0 && !source.Queued && !source.Invalidate, "bound-self ignores both sides of notification");
        Expect(releases == 0 && image.Owned, "bound-self preserves owner");
        Expect(HasOwnedWatcher(pivot), "owned pivot guard is derived from actual watcher graph");
        var parent = new Transform();
        watchers[parent] = new() { new Source(), source };
        Expect(HasOwnedWatcher(parent), "owned ancestor guard sees shared watcher sets");
        Expect(!HasOwnedWatcher(new Transform()), "unindexed transform has no owner");
        image.Owned = false;
        Expect(!HasOwnedWatcher(pivot), "released owner no longer shadows native pose");
        image.Owned = true;
        BeforeWrite(pivot, LiveDecoChanges.Pose); AfterWrite(pivot, LiveDecoChanges.Pose);
        Expect(!image.Owned && releases == 1 && source.Queued, "foreign pivot write releases and queues");
        Expect(!source.Invalidate, "pivot pose preserves static shape cache");
        Expect(pending.Count == 1, "before/after notification deduplicates queue");
        pending.Clear(); source.Queued = false; image.Owned = true;
        BeforeWrite(r, LiveDecoChanges.Shape); AfterWrite(r, LiveDecoChanges.Shape);
        Expect(!image.Owned && source.Invalidate && pending.Count == 1, "real shape write invalidates cached admission");
        pending.Clear(); source.Queued = source.Invalidate = false; image.Owned = true;
        var script = new Behaviour { transform = child }; watchers[child] = new() { source };
        BeforeWrite(script, LiveDecoChanges.Script); AfterWrite(script, LiveDecoChanges.Script);
        Expect(!image.Owned && source.Invalidate && pending.Count == 1, "foreign script activation still falls back to transform and releases");
        pending.Clear(); source.Queued = source.Invalidate = false;
        restoring = true;
        long callbacks = writeCallbacks;
        BeforeWrite(r, LiveDecoChanges.Shape); AfterWrite(r, LiveDecoChanges.Shape);
        Expect(!ObservingWrites && pending.Count == 0 && writeCallbacks == callbacks, "restoration suppresses observations");
        restoring = false; Active = false;
        Expect(!ObservingWrites, "inactive observer is disabled");
        Active = true;
        AfterWrite(new Renderer(), LiveDecoChanges.State);
        Expect(registrations == 1, "new renderer discovery remains event based");
        watchers.Clear(); pending.Clear();
        Console.WriteLine($"Production observation/queue guards: {checks} checks; managed adapter, no engine execution");
    }
}
