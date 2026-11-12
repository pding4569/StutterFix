"""Reproduce summary tables from the lab CSV/PNG evidence (never runs the game)."""
from pathlib import Path
import csv
import json
import statistics
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'evidence' / 'composite-20261112a'


def main():
    rows = list(csv.DictReader((OUT / 'results.csv').open(encoding='utf-8')))
    assert len(rows) == 180, f'Incomplete full benchmark: {len(rows)}/180'
    raw = list(csv.DictReader((OUT / 'samples.csv').open(encoding='utf-8')))
    assert len(raw) == 3600
    assert all(float(r['total_ms']) > 0 for r in raw)
    entries = []
    markdown = [
        '| 장면 | N | 방법 | K24 ms | A1/방법 | RGBA 최대 차이 | RGBA 평균 차이 |',
        '|---|---:|---|---:|---:|---|---|',
    ]
    slopes = ['| 장면 | N | 방법 | K4 ms | K12 ms | K24 ms | 기울기 ms | K12→24 변화 |',
              '|---|---:|---|---:|---:|---:|---:|---:|']
    diagnostics = []
    for scene in (1, 2, 3):
        for n in (8, 16, 32, 64):
            images = {m: np.array(Image.open(OUT / f'S{scene}-N{n}-{m}.png')).astype(np.int16) for m in ('A1','A2','B1','B2','B3')}
            base_image, b1, b2, b3 = (images[m] for m in ('A1','B1','B2','B3'))
            assert np.array_equal(b2, b3), (scene, n, 'tile-list parity')
            for method in ('A1', 'A2', 'B1', 'B2', 'B3'):
                matches = [r for r in rows if int(r['scene']) == scene and int(r['N']) == n and r['method'] == method]
                matches.sort(key=lambda r: int(r['K']))
                assert [int(r['K']) for r in matches] == [4, 12, 24]
                for r in matches:
                    samples = [float(s['per_pass_ms']) for s in raw if all(s[k] == r[k] for k in ('scene', 'N', 'method', 'K'))]
                    assert len(samples) == 20
                    assert abs(statistics.median(samples) - float(r['median_ms'])) < 1e-9
                r = matches[-1]
                x = np.array([4, 12, 24])
                y = np.array([float(q['median_ms']) for q in matches])
                slope, intercept = np.polyfit(x, x*y, 1)
                delta = (y[-1]/y[1]-1)*100
                rgba_max = '/'.join(r['max'+c] for c in 'RGBA')
                rgba_mean = '/'.join(f"{float(r['mean'+c]):.4f}" for c in 'RGBA')
                markdown.append(f"| S{scene} | {n} | {method} | {float(r['median_ms']):.4f} | {float(r['speedup_A1']):.3f}× | {rgba_max} | {rgba_mean} |")
                slopes.append(f'| S{scene} | {n} | {method} | {y[0]:.4f} | {y[1]:.4f} | {y[2]:.4f} | {slope:.4f} | {delta:+.1f}% |')
                actual = images[method]
                diff = np.abs(actual-base_image)
                assert diff.max(axis=(0, 1)).tolist() == [int(r['max'+c]) for c in 'RGBA']
                assert np.allclose(diff.mean(axis=(0, 1)), [float(r['mean'+c]) for c in 'RGBA'])
                if method == 'A2':
                    assert not diff.any()
                e = dict(scene=scene, N=n, method=method, ms24=float(r['median_ms']), slope_ms=float(slope), intercept_ms=float(intercept), K12_to_24_pct=float(delta),
                         max_rgba=diff.max(axis=(0, 1)).tolist(), mean_rgba=diff.mean(axis=(0, 1)).tolist(),
                         changed_pixels=int(np.any(diff, axis=2).sum()), pixels_gt4=int(np.any(diff>4, axis=2).sum()),
                         early_skipped=float(r['earlySkipped']), samples=float(r['samples']), mean_tile_entries=float(r['meanTileEntries']), coverage=float(r['coverage']))
                entries.append(e)
            early_diff = np.abs(b2-b1)
            diagnostics.append(dict(scene=scene,N=n,B2_B1_max=early_diff.max(axis=(0,1)).tolist(),B2_B1_mean=early_diff.mean(axis=(0,1)).tolist(),B2_B1_changed=int(np.any(early_diff,axis=2).sum())))
    (OUT / 'summary-table.md').write_text('\n'.join(markdown)+'\n', encoding='utf-8')
    (OUT / 'K-table.md').write_text('\n'.join(slopes)+'\n', encoding='utf-8')
    (OUT / 'analysis.json').write_text(json.dumps(dict(entries=entries,early_diffs=diagnostics),indent=2),encoding='utf-8')
    layer_file = OUT / 'layers.csv'
    if layer_file.exists():
        layer_rows = list(csv.DictReader(layer_file.open(encoding='utf-8')))
        edge_summary = []
        x = (np.arange(2560, dtype=np.float64)[None, :]+.5)/2560
        # Unity's ReadPixels/EncodeToPNG stores texture row zero at screen bottom.
        y = (np.arange(1440, dtype=np.float64)[:, None]+.5)/1440
        for n in (8,16,32,64):
            near = np.zeros((1440,2560), dtype=bool)
            for l in layer_rows:
                if int(l['scene']) != 2 or int(l['N']) != n:
                    continue
                ux,uy,uo,vx,vy,vo = (float(l[k]) for k in ('uX','uY','uOffset','vX','vY','vOffset'))
                u=ux*x+uy*y+uo;v=vx*x+vy*y+vo
                du=abs(ux)/2560+abs(uy)/1440;dv=abs(vx)/2560+abs(vy)/1440
                near |= (((np.abs(u)<du)|(np.abs(u-1)<du))&(v>=-dv)&(v<=1+dv))|(((np.abs(v)<dv)|(np.abs(v-1)<dv))&(u>=-du)&(u<=1+du))
            a=np.array(Image.open(OUT/f'S2-N{n}-A1.png')).astype(np.int16)
            b=np.array(Image.open(OUT/f'S2-N{n}-B1.png')).astype(np.int16)
            diff=np.abs(a-b)
            large=np.any(diff>4,axis=2)
            interior=diff[~near]
            edge_summary.append(dict(N=n,edge_band_pixels=int(near.sum()),pixels_gt4=int(large.sum()),pixels_gt4_outside_edge=int((large&~near).sum()),pixels_gt5_outside_edge=int((np.any(diff>5,axis=2)&~near).sum()),interior_max=interior.max(axis=0).tolist(),interior_mean=interior.mean(axis=0).tolist()))
        (OUT/'edge-analysis.json').write_text(json.dumps(edge_summary,indent=2),encoding='utf-8')
        print('Edge-band diagnostics:',json.dumps(edge_summary))
    print(f'Validated {len(rows)} aggregate rows, {len(raw)} samples, 60 PNGs, A2=A1 and B3=B2 in all 12 cases.')
    for scene in (1,2,3):
        for n in (8,16,32,64):
            es=[e for e in entries if e['scene']==scene and e['N']==n]
            print(f"S{scene} N{n}: " + ' '.join(f"{e['method']}={e['ms24']:.4f}ms" for e in es))


if __name__ == '__main__':
    main()
