"""Compose real native samples into a labelled visual-only preview; never synthesize frames."""
import argparse
import csv
import json
from pathlib import Path
from PIL import Image, ImageDraw


def main():
    p = argparse.ArgumentParser()
    p.add_argument('directory', type=Path)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    result = json.loads((a.directory/'repeat.json').read_text(encoding='utf8'))
    if not result['visual_only'] or result['modes'] != [0, 2, 4]:
        p.error('A complete 0/2/4 visual-only batch is required')
    series, metadata = {}, []
    for run in result['runs']:
        mode = run['mode']
        root = a.directory/f"{run['index']:02d}-{mode}x/capture"
        with (root/'clip.csv').open(newline='') as f:
            rows = list(csv.DictReader(f))
        frames = [(float(r['song_s']), Image.open(root/f"clip-{mode}x-{int(r['index']):03d}-{'real' if r['real']=='1' else 'generated'}.ppm").convert('RGB')) for r in rows]
        if len(frames) < 30 or min(t for t, _ in frames) > 20.1 or max(t for t, _ in frames) < 22.8:
            raise RuntimeError('Incomplete actual GPU sample window')
        generated = sum(r['real'] == '0' for r in rows)
        if mode and not generated:
            raise RuntimeError('No generated image in visual samples')
        series[mode] = frames
        metadata.append(dict(mode=mode, sample_count=len(frames), sampled_generated=generated,
                             first_song_s=min(t for t, _ in frames), last_song_s=max(t for t, _ in frames)))
    if set(series) != {0, 2, 4}:
        raise RuntimeError('Three completed visual runs required')
    w, h = series[0][0][1].size
    animation = []
    for tick in range(90):
        song = 20+tick/30
        canvas = Image.new('RGB', (w*3, h+45), (17, 17, 20))
        draw = ImageDraw.Draw(canvas)
        for col, mode in enumerate([0, 2, 4]):
            t, im = min(series[mode], key=lambda v: abs(v[0]-song))
            if im.size != (w, h):
                raise RuntimeError('Capture dimensions changed')
            canvas.paste(im, (col*w, 45))
            draw.text((col*w+8, 6), f"{'OFF' if mode == 0 else str(mode)+'x'}  song {t:.3f}s", fill=(235, 235, 235))
            draw.text((col*w+8, 24), 'Native GPU samples / 30 FPS preview', fill=(160, 165, 175))
        animation.append(canvas)
    a.output.parent.mkdir(parents=True, exist_ok=True)
    animation[0].save(a.output, save_all=True, append_images=animation[1:],
                      duration=[33, 33, 34]*30, loop=0, quality=75, method=4)
    animation[0].save(a.output.with_name(a.output.stem+'-poster.png'))
    a.output.with_suffix('.json').write_text(json.dumps(dict(
        label=result['label'], conditions=result['conditions'], samples=metadata,
        scope='Actual decimated GPU captures in separate fresh runs; nearest song-time alignment; compressed 30 FPS preview, not a performance or physical pacing measurement'),
        ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print('Actual GPU preview saved:', a.output, a.output.stat().st_size, flush=True)


if __name__ == '__main__':
    main()
