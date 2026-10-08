"""Compare actual GPU samples from OFF, extrapolation and delayed camera trials."""
import argparse
import csv
import json
from pathlib import Path
from PIL import Image, ImageDraw


def main():
    p = argparse.ArgumentParser()
    p.add_argument('directory', type=Path)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--prediction-run', default='predict4', help='Explicit completed prediction trial directory; failed trials remain preserved')
    a = p.parse_args()
    samples, metadata = [], []
    for label, mode, blend in [('off', 0, False), (a.prediction_run, 4, False), ('blend4', 4, True)]:
        root = a.directory/label
        result = json.loads((root/'summary.json').read_text(encoding='utf8'))
        if result['mode'] != mode or result['camera_blend'] != blend or not result['visual_smoke']:
            raise RuntimeError('Unexpected visual trial: '+label)
        with (root/'capture/clip.csv').open(newline='') as f:
            rows = list(csv.DictReader(f))
        frames = [(float(r['song_s']), Image.open(root/'capture'/f"clip-{mode}x-{int(r['index']):03d}-{'real' if r['real']=='1' else 'generated'}.ppm").convert('RGB')) for r in rows]
        if len(frames)<30 or min(t for t,_ in frames)>20.1 or max(t for t,_ in frames)<22.8:
            raise RuntimeError('Incomplete actual image window: '+label)
        samples.append(frames)
        metadata.append(dict(label=label, count=len(rows), generated=sum(r['real']=='0' for r in rows)))
    w,h = samples[0][0][1].size
    animation=[]
    labels=['OFF', '4x predicted camera', '4x known-camera blend (delayed)']
    for tick in range(90):
        song=20+tick/30
        canvas=Image.new('RGB',(3*w,h+45),(17,17,20))
        draw=ImageDraw.Draw(canvas)
        for col,frames in enumerate(samples):
            t,im=min(frames,key=lambda v:abs(v[0]-song))
            if im.size!=(w,h): raise RuntimeError('Image sizes differ')
            canvas.paste(im,(col*w,45))
            draw.text((col*w+8,6),labels[col],fill=(235,235,235))
            draw.text((col*w+8,24),f'actual GPU sample / song {t:.3f}s',fill=(160,165,175))
        animation.append(canvas)
    a.output.parent.mkdir(parents=True,exist_ok=True)
    animation[0].save(a.output,save_all=True,append_images=animation[1:],duration=[33,33,34]*30,loop=0,quality=75,method=4)
    animation[0].save(a.output.with_name(a.output.stem+'-poster.png'))
    a.output.with_suffix('.json').write_text(json.dumps(dict(samples=metadata,scope='Separate fresh runs, approximate song-time alignment, decimated actual GPU samples;30FPS compressed preview;readbacks affect pacing;not physical display/motion proof'),indent=2)+'\n',encoding='utf8')


if __name__=='__main__': main()
