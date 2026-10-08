"""Pool complete fresh-process repetitions, preserving all trials and observed conditions."""
import argparse
import json
from pathlib import Path


def summarize(root):
    data = json.loads((root/'repeat.json').read_text(encoding='utf8'))
    if data['visual_only'] or len(data['runs']) != len(data['modes']):
        raise RuntimeError('A complete performance batch is required')
    modes = data['modes']
    if len(modes) < 4 or modes[0] != 0 or modes[-1] != 0 or any(m != 0 and not 2 <= m <= 8 for m in modes):
        raise RuntimeError('Complete bounded multipliers with OFF controls at both ends required')
    pooled = []
    for mode in sorted(set(modes)):
        runs = [r for r in data['runs'] if r['mode'] == mode]
        rows = [r['native'] for r in runs]
        for r in runs:
            s = r['safety']
            n = r['native']
            if int(s['worker_error']) or s['frame_begins'] != s['frame_ends'] or n['seconds'] < 39 or n['gpu_coverage'] != n['generated_frames']:
                raise RuntimeError('Incomplete/failed trial; do not silently exclude it')
        source_seconds = sum(r['source_seconds'] for r in rows)
        output_seconds = sum(r['seconds'] for r in rows)
        source = sum(r['source_frames']-1 for r in rows)/source_seconds
        missing = sum(r['missing_camera_callbacks'] for r in rows)
        lower = source-missing/source_seconds
        output = sum(r['output_frames']-1 for r in rows)/output_seconds
        samples = sum(r['gpu_coverage'] for r in rows)
        gpu = sum(r['generated_gpu_ms']['mean']*r['gpu_coverage'] for r in rows)/samples if samples else None
        pooled.append(dict(mode=mode, trials=len(rows), real_fps=source, scene_fps_bounds=[lower, source],
                           output_fps=output, output_over_source=output/source, missing_camera_callbacks=missing,
                           generated_gpu_mean_ms=gpu, generated_gpu_samples=samples,
                           worst_output_ms=max(r['output_worst_ms'] for r in rows),
                           worst_source_ms=max(r['source_worst_ms'] for r in rows),
                           continuous_miss_lock=sum(r['scheduler']['continuous_miss_lock'] for r in rows),
                           continuous_miss_other=sum(r['scheduler']['continuous_miss_timer'] for r in rows),
                           source_fps_trials=[r['real_fps'] for r in rows],
                           output_ratio_trials=[r['output_fps']/r['real_fps'] for r in rows]))
    off = pooled[0]
    for r in pooled:
        r['output_over_off'] = r['output_fps']/off['output_fps']
        r['scene_fps_decrease_bounds_percent'] = [100*(1-r['scene_fps_bounds'][1]/off['scene_fps_bounds'][0]),
                                                 100*(1-r['scene_fps_bounds'][0]/off['scene_fps_bounds'][1])]
    return dict(label=data['label'], conditions=data['conditions'], order=data['modes'],
                research_binary_sha256=data['research_binary_sha256'], pooled=pooled, runs=data['runs'])


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('directory', type=Path)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    result = summarize(a.directory)
    a.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print(json.dumps(result['pooled'], ensure_ascii=False, indent=2))
