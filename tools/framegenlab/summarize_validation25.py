"""Offline chapter-25 evidence; retain controls, failed candidates and exact event rows."""
import csv
import hashlib
import json
import math
from pathlib import Path
from measure_outside import read

HERE = Path(__file__).resolve().parent
RESULTS = HERE / 'results'


def load(path):
    return json.loads(path.read_text(encoding='utf-8'))


def latency(label, mode):
    cap = RESULTS / label / mode / 'capture'
    rows = [r for r in read(cap / 'presents.csv') if 5 <= float(r['song_s']) < 45
            and math.isfinite(float(r['timeline_to_submit_ms']))]
    worst = max(rows, key=lambda r: float(r['timeline_to_submit_ms']))
    generated = max((r for r in rows if r['real'] == '0'), key=lambda r: float(r['timeline_to_submit_ms']))
    frame = int(worst['unity_frame'])
    return dict(worst=worst, generated_worst=generated,
                sources=[r for r in read(cap / 'sources.csv') if abs(int(r['unity_frame'])-frame) <= 2],
                schedule=[r for r in read(cap / 'schedule.csv') if abs(int(r['unity_frame'])-frame) <= 1],
                root_cause='확인 안 됨', physical_display_latency='확인 안 됨')


def reversals(label, mode):
    root = RESULTS / label / mode
    paired = load(root / 'paired.json')
    sources = read(root / 'capture/sources.csv')
    result = []
    for rev in paired['reversals']:
        if not (rev['actual'] and not rev['reference']):
            continue
        i = min(range(len(sources)), key=lambda i: abs(float(sources[i]['song_s'])-rev['song_s']))
        result.append(dict(reversal=rev, sources=sources[max(0, i-2):i+3],
                           pairs=[r for r in paired['pairs'] if abs(r['index']-rev['index']) <= 1]))
    return result


def full_song(label):
    root = RESULTS / label
    if not (root / 'summary.json').exists():
        return '확인 안 됨'
    summary = load(root / 'summary.json')
    sources = [r for r in read(root / 'capture/sources.csv') if float(r['song_s']) >= 5]
    output = [r for r in read(root / 'capture/presents.csv')
              if int(sources[0]['unity_frame']) <= int(r['unity_frame']) <= int(sources[-1]['unity_frame'])]
    def gaps(rows, clock):
        g = [(1000*(float(b[clock])-float(a[clock])), float(b['song_s'])) for a,b in zip(rows, rows[1:])]
        maximum, song = max(g)
        return dict(worst_ms=maximum, worst_song_s=song, over_33ms=sum(x > 1000/30 for x,_ in g))
    return dict(completed='곡 끝남' in (root / 'run.txt').read_text(encoding='utf-8'),
                last_song_s=float(sources[-1]['song_s']), source_rows=len(sources), output_rows=len(output),
                source_frame_gaps=sum(int(b['unity_frame'])-int(a['unity_frame']) != 1 for a,b in zip(sources,sources[1:])),
                missing_camera_callbacks=sum(r['scene_rendered']=='0' for r in sources),
                failed_present=sum(int(r['hr']) != 0 for r in output),
                source_intervals=gaps(sources,'source_s'), output_intervals=gaps(output,'present_s'),
                safety=summary['safety'], game_metrics=summary['game_metrics'], build=summary['build'],
                physical_display_latency='확인 안 됨', whole_song_pixel_quality='확인 안 됨')


def main():
    data = dict(conditions='2560x1440 144Hz D3D11 sync0', actual_3440='확인 안 됨', optimizations={})
    for v, name in [(1, 'smaller_search'), (2, 'larger_blocks'), (3, 'metadata_reuse_excluded')]:
        perf = load(RESULTS / f'ch25-arche-v{v}-perf55/summary.json')
        visual_root = RESULTS / f'ch25-arche-v{v}-visual'
        visual = load(visual_root / 'paired.json')['summary']
        data['optimizations'][name] = dict(performance=perf['pooled'], conditions=perf['conditions'],
                                           visual=visual, visual_native=load(visual_root / 'summary.json')['native'],
                                           visual_build=load(visual_root / 'summary.json')['build'],
                                           eligible=v != 3,
                                           caveat='metadata equality does not establish image equality; stale flow' if v == 3 else '',
                                           reduction_sample_offset_actual_px=32 if v == 1 else 16)
    for variant, name in [('lazy','lazy_pair_search'), ('batch','batched_pair_search')]:
        perf_path = RESULTS / f'ch25-arche-{variant}-perf55/summary.json'
        if perf_path.exists():
            perf = load(perf_path)
            root = RESULTS / f'ch25-arche-{variant}-visual'
            data['optimizations'][name] = dict(performance=perf['pooled'], conditions=perf['conditions'],
                visual=load(root/'paired.json')['summary'], visual_native=load(root/'summary.json')['native'], eligible=True,
                visual_build=load(root/'summary.json')['build'],
                reduction_sample_offset_actual_px=2)
    data['latency_events'] = {f'{label}/{mode}': latency(label,mode)
        for label in ['ch24-hello-perf','ch24-arche-perf'] for mode in ['01-2x','02-4x']}
    data['new_latency_events'] = {f'{label}/{mode}': latency(label,mode)
        for label in ['ch25-arche-lazy-perf55','ch25-arche-batch-perf55'] for mode in ['01-4x','02-4x']
        if (RESULTS/label/mode/'summary.json').exists()}
    data['reversal_events'] = {f'{label}/{mode}': reversals(label,mode)
        for label in ['ch24-hello-visual','ch24-arche-visual'] for mode in ['01-2x','02-4x']}
    data['full_songs'] = {label: full_song(label) for label in ['ch25-hello-2-full-level','ch25-hello-4-full-level',
                                                              'ch25-arche-2-full','ch25-arche-4-full']}
    data['excluded'] = dict(short_performance='ch25-arche-v1-perf: 35s, required39s; preserved',
                            wrong_song='ch25-hello-2-full: main.adofai, short map; preserved')
    target = HERE / 'validation-25/summary.json'
    target.parent.mkdir(exist_ok=True)
    restoration = target.parent / 'restoration.json'
    if restoration.exists():
        data['recorded_restoration'] = load(restoration)
    fixture = target.parent / 'batch-fixture.txt'
    if fixture.exists():
        rows = [dict(part.split('=',1) for part in line.split()) for line in fixture.read_text().splitlines()]
        data['batch_fixture'] = dict(cases=len(rows),
            pixel_errors=sum(int(r['errors_above3']) for r in rows),
            ui_errors=sum(int(r['ui_errors']) for r in rows), state_errors=sum(int(r['state_errors']) for r in rows))
    cap = RESULTS / 'ch25-arche-batch-visual/capture'
    if (cap / 'clip.csv').exists():
        from PIL import Image
        with (cap/'clip.csv').open(encoding='utf-8') as rows:
            row = next(r for r in csv.DictReader(rows) if r['real'] == '0')
        source = cap / f"clip-4x-{int(row['index']):03d}-generated.ppm"
        picture = target.parent / 'arche-batch4-song20.png'
        with Image.open(source) as pixels:
            pixels.save(picture)
        data['sample_image'] = dict(capture=row, source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
            png_sha256=hashlib.sha256(picture.read_bytes()).hexdigest(), file=picture.name,
            scope='New chapter25 visual sample, exact reduced GPU pixels; format conversion only, not a performance run')
        data['new_visual_extra_reversals'] = {
            variant: [r for r in load(RESULTS/f'ch25-arche-{variant}-visual/paired.json')['reversals']
                      if r['actual'] and not r['reference']] for variant in ['lazy','batch']}
    target.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n',encoding='utf-8')
    print(target)


if __name__ == '__main__':
    main()
