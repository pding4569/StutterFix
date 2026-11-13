"""Read-only native sample decomposition. Samples are leaf-to-root, not calls."""
import argparse
import collections
import hashlib
import json
from pathlib import Path

TARGETS = ['PlayerRender', 'RenderManager::RenderOffscreenCameras', 'Camera::CustomRender',
           'ujob_wait_for', 'ujob_execute_job', 'CullResults::GetOrCreateSharedRendererScene',
           'ExtractSceneRenderNodeQueue', 'RendererUpdateManager::UpdateAll',
           'RenderManager::UpdateAllRenderers', 'TransformChangeDispatch::ScanAndHandleChangesJob',
           'Camera::CustomCull', 'MeshRenderer::FinalizeUpdate', 'SyncFence']


def analyze(path):
    raw = path.read_bytes()
    stacks = [[f.strip() for f in s.split(';')] for s in raw.decode('utf-8-sig').splitlines() if s.strip()]
    result = {'path': str(path), 'sha256': hashlib.sha256(raw).hexdigest(), 'samples': len(stacks), 'targets': {}}
    for target in TARGETS:
        matches = [(s, next(i for i, f in enumerate(s) if f == '[Unity] ' + target))
                   for s in stacks if '[Unity] ' + target in s]
        counters = {k: collections.Counter() for k in ('child', 'caller', 'leaf', 'path', 'execute_child')}
        for s, i in matches:
            counters['child'][s[i-1] if i else '(self)'] += 1
            counters['caller'][s[i+1] if i+1 < len(s) else '(root)'] += 1
            counters['leaf'][s[0]] += 1
            counters['path'][' <- '.join(s[max(0, i-4):i+5])] += 1
            if target == 'ujob_wait_for' and '[Unity] ujob_execute_job' in s[:i]:
                j = s.index('[Unity] ujob_execute_job')
                counters['execute_child'][s[j-1] if j else '(self)'] += 1
        result['targets'][target] = dict(count=len(matches), percent=100*len(matches)/len(stacks),
                                        outside_count=sum(not any('runtime-invoke' in f for f in s) for s, _ in matches),
                                        **{k: c.most_common(20) for k, c in counters.items()})
    return result


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[3])
    p.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[2] / 'out/engine-hot')
    a = p.parse_args()
    a.output.mkdir(parents=True, exist_ok=True)
    data = {k: analyze(a.root / 'night-results' / f'nat-{k}.resolved.txt') for k in ('hl', 'sh')}
    (a.output / 'samples.json').write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    lines = ['# Native sample decomposition', '', 'Counts are sampled stacks, never invocation counts. Inclusive targets overlap.', '']
    for key, d in data.items():
        lines += [f'## {key}: {d["samples"]} samples', f'SHA256 {d["sha256"]}', '']
        for name, t in d['targets'].items():
            lines += [f'### {name}: {t["count"]} ({t["percent"]:.3f}%)']
            lines += [f'Outside runtime-invoke: {t["outside_count"]} ({100*t["outside_count"]/d["samples"]:.3f}% of all samples). Distributions below include script callbacks.']
            for kind in ('child', 'caller', 'leaf', 'execute_child'):
                if t[kind]:
                    lines += [kind + ':'] + [f'- {n} ({100*n/max(t["count"],1):.2f}% of target): {f}' for f, n in t[kind][:12]]
            lines += ['']
    (a.output / 'samples.md').write_text('\n'.join(lines), encoding='utf-8')
    print('\n'.join(f'{k}: {v["samples"]} samples' for k, v in data.items()))


if __name__ == '__main__':
    main()
