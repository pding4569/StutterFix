"""Read existing logs and audit the r7 source contract. Never run a game or install files."""
from decimal import Decimal
from hashlib import sha256
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
out = root / 'docs' / 'handoff'


def read(path):
    raw = path.read_bytes()
    return raw.decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')


renderer = read(root / 'LiveDecoRenderer.cs')
changes = read(root / 'LiveDecoChanges.cs')
gate = read(root / 'LiveDecoGate2.cs')
dormancy = read(root / 'Dormancy.cs')
checks = 0


def check(value, label):
    global checks
    if not value:
        raise AssertionError(label)
    checks += 1


hot = renderer[renderer.index('        static void RenderFrame()'):renderer.index('        static void ReportCost()')]
repair = renderer[renderer.index('        static void Reconcile()'):renderer.index('        static void Retire(')]
check('Scan(' not in renderer, 'old full renderer Scan removed')
check('Register(' not in hot and 'sources' not in hot and 'images.Values' not in hot, 'hot frame has no full source/image traversal')
check('ordered.Count' in repair and 'for (int a = 0;' not in repair, 'repair starts at changed rows, not ordered-list origin')
check('foreach (var run in runs)' not in repair, 'reconcile does not enumerate all runs')
check('pending.Count == 0' in renderer and 's.Queued' in renderer, 'deduplicated event queue with empty fast path')
check('if (i.Cached)' in renderer and 'cacheHits++' in renderer, 'static admission cache')
check('OwnershipVisible' in renderer and 'LogicalVisible' in renderer, 'logical visibility separated from owned hide bit')
check('LiveDecoRenderer.Owns(inst)' in read(root / 'InvisibleSkip.cs'), 'InvisibleSkip preserves live owner rendering bit')
check('WithinPixelError' in renderer and 'threshold_px=0.01' in renderer, 'screen-space error admission')
check('histogram_px_' in renderer and 'released reason=' in renderer, 'error histogram and release reasons')
check('owned_ts t=' in renderer and 'nextReport = Time.realtimeSinceStartup + 1f' in renderer, 'one-second timeline')
check('scan_ms=' in renderer and 'delta_ms=' in renderer and 'audit_ms=' in renderer, 'separate poll, event, and audit costs')
check('reconcileTicks + deltaTicks' in renderer, 'event work included in reconciliation cost, not hidden')
check('write_callbacks=' in renderer and 'watcher_visits=' in renderer, 'callback workload counters')
check('ex.ToString()' in renderer and 'e.ToString()' in renderer, 'full exception stacks retained')
check('minimum_on_warmup_s=3' in gate and 'EnabledSeconds < 3' in gate, 'ABBA refuses insufficient on warmup')
check('GATE2=OWNED_LOW' in gate and 'AutoTest.StopLiveDecoTest' in gate, 'early low ownership stops test')
check('EnabledSeconds >= 3 && EnabledSeconds <= 5' in hot and '!FrameCompare.Paused' in hot, 'low guard runs before 5s on natural playback')
check('OnLandOnPortal' in renderer and 'FailAction' in renderer and 'QuitToMainMenu' in renderer, 'explicit lifetime exits')
check('DormancyRebuilt' in dormancy and 'DormancyReset' in dormancy and 'DormancyChanged(d)' in dormancy, 'pre-existing Dormancy callbacks preserved')
check('if (LiveDecoRenderer.Active) auditDue = now >= nextAudit' in dormancy, 'existing dormant audit uses 1s while owner active')
check('typeof(scrDecoration).Assembly' in changes and 'typeof(LiveDecoChanges).Assembly' in changes, 'managed write observers cover game and mod')
check('CreateDelegate' in changes and 'DynamicInvoke' not in changes and 'Module.ResolveSignature' not in changes, 'typed allocation-free writer delegates, no forbidden signature resolution')
check('Spawn4' in changes and 'Destroy2' in changes, 'spawn/destruction notification wrappers')
check('Watched.Clear()' in renderer and 'watchers.Clear()' in renderer, 'old watcher edges and scene references released')
check('WatchDependents(i)' in renderer and 'Watch(script, i.Source)' in renderer, 'dependent renderer and script changes invalidate owner classification')
check('sort-context-changed' in renderer, 'global sort context change restores owners without a global rescan')

log_path = Path('E:/StutterFixWork/night-results/al-ga-6.log')
log = read(log_path)
lines = log.splitlines()
pixels = [line for line in lines if '[livedeco2] PIXEL t=' in line]
fps_lines = [line for line in lines if '[livedeco2] FPS mode=' in line]
fps = [Decimal(re.search(r' fps=([\d.]+)', line).group(1)) for line in fps_lines]
check(len(pixels) == 3 and len(fps) == 4, 'latest existing full log has three pixels and four ABBA samples')
off = (fps[0] + fps[3]) / 2
on = (fps[1] + fps[2]) / 2
gain = (on / off - 1) * 100
prior = ['Existing game evidence only; this session did not launch/install a game.',
         f'path={log_path.as_posix()} sha256={sha256(log_path.read_bytes()).hexdigest()}',
         *pixels, *fps_lines, f'off_mean={off} on_mean={on} gain_pct={gain}',
         *[line for line in lines if '[livedeco2] PROFILE samples=' in line or '[livedeco2] GATE2=' in line]]
(out / 'gate2-A-r7-prior-evidence.txt').write_text('\n'.join(prior) + '\n', encoding='utf-8')

for name in ('dev', 'player', 'auto'):
    check('오류 0개' in read(out / f'gate2-A-r7-build-{name}.log'), f'{name} build has zero errors')
summary = read(out / 'codex-gate2-A-o.txt').splitlines()
check(len(summary) == 10, 'handoff summary exactly 10 lines')
check('미측정' in '\n'.join(summary), 'handoff summary preserves runtime measurement limit')
hashes = [f'{name} sha256={sha256((root / name).read_bytes()).hexdigest()}' for name in
          ('LiveDecoRenderer.cs', 'LiveDecoChanges.cs', 'Dormancy.cs', 'InvisibleSkip.cs', 'LiveDecoPolicy.cs', 'LiveDecoGate2.cs', 'AutoTest.cs', 'StutterFix.csproj')]
(out / 'gate2-A-r7-validation.txt').write_text(
    f'Offline r7 contract audit: {checks} checks passed. No game, installation or commit.\n'
    'Static source checks are not measured scan/reconcile/FPS/coverage/pixel/lifetime results.\n' + '\n'.join(hashes) + '\n', encoding='utf-8')
print(f'Offline r7 contract audit: {checks} checks passed; existing game gain={gain}%; no new runtime evidence')
