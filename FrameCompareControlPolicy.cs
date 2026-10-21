#if DEV || AUTOTEST
using System.Globalization;

namespace StutterFix
{
    internal static class FrameCompareControlPolicy
    {
        // A diagnostic freeze entered from playback is distinct from an editor
        // pause. A stale/replaced controller or a broken freeze cannot grant it.
        internal static bool FrozenPlayback(bool paused, bool pausedBefore, bool playingBefore, bool sameController, bool intactPause)
            => paused && !pausedBefore && playingBefore && sameController && intactPause;

        // SortProbe's rule uses signed Unity instance IDs, not allocation order.
        internal static bool FirstBehind(int first, int second) => first < second;
        internal static string Verdict(string count, bool positive, bool valid)
        {
            return valid && long.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out long n)
                && (positive ? n > 0 : n == 0) ? "PASS" : "FAIL";
        }
    }
}
#endif
