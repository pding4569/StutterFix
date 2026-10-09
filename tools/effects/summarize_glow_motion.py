"""Export measured image diagnostics, including failures, without changing rendering."""
import argparse,csv,hashlib,json,math
from pathlib import Path
from PIL import Image,ImageDraw

def main():
    p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    summary=json.loads((a.root/'summary.json').read_text(encoding='utf8'));trials=[]
    for trial in summary['trials']:
        folder=a.root/trial['trial'];paired=json.loads((folder/'paired.json').read_text());regions=json.loads((folder/'regions.json').read_text());identity=json.loads((folder/'effect-identity.json').read_text())
        safety=dict(part.split('=',1) for part in (folder/'capture/safety.txt').read_text().split());run=json.loads((folder/'summary.json').read_text(encoding='utf8'))
        source=list(csv.DictReader((folder/'capture/sources.csv').open(newline='')))
        scene=[r for r in source if 86<=float(r['song_s'])<=89]
        angles=[float(scene[0]['camera_angle'])]
        for r in scene[1:]:angles.append(angles[-1]+(float(r['camera_angle'])-angles[-1]+math.pi)%(2*math.pi)-math.pi)
        trials.append({**trial,'new_picture_ratio_percent':100*trial['visual']['captured_new_picture_generated']/trial['visual']['captured_generated'],
            'extra_reversal_rows':[r for r in paired['reversals'] if r['actual'] and not r['reference']],
            'overshoot_rows':[r for r in paired['rows'] if r['accepted'] and not r['real'] and r['added']['length']>r['source']['length']+.25],
            'region_summary':regions['summary'],'region_flagged_rows':[r for r in regions['rows'] if r['static_added'] or r['opposite'] or r['overshoot']],
            'safety':safety,'identity_scope':identity['scope'],'build':run['build'],
            'source_camera_angle_range': [min(float(r['camera_angle']) for r in scene),max(float(r['camera_angle']) for r in scene)],
            'source_camera_angle_unwrapped_range_rad':[min(angles),max(angles)],
            'source_camera_angle_unwrapped_span_degrees':(max(angles)-min(angles))*180/math.pi,
            'source_camera_size_range':[min(float(r['camera_size']) for r in scene),max(float(r['camera_size']) for r in scene)]})
    glow=a.root/'hello-glow';paired=json.loads((glow/'paired.json').read_text());flag=next(r for r in paired['reversals'] if r['actual'] and not r['reference'])
    selected=paired['rows'][max(0,flag['index']-2):flag['index']+1];capture=glow/'capture';columns=[('reference','Same selected real source'),('clip','Actual output'),('next-reference','Next real source')]
    sample=Image.open(capture/'reference-4x-000.ppm');w,h=sample.size;image=Image.new('RGB',(3*w,len(selected)*(h+26)),(16,16,18));draw=ImageDraw.Draw(image);hashes=[]
    for y,row in enumerate(selected):
        i=row['index']
        for x,(prefix,title) in enumerate(columns):
            name=f'{prefix}-4x-{i:03d}'+(('-real' if row['real'] else '-generated') if prefix=='clip' else '')+'.ppm';path=capture/name
            image.paste(Image.open(path).convert('RGB'),(x*w,y*(h+26)+26));draw.text((x*w+4,y*(h+26)+5),f'{title} | {row["song_s"]:.6f}s | #{i}',fill=(238,239,241))
            hashes.append(dict(file=name,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    figure=a.out.with_suffix('.png');image.save(figure)
    exported=dict(scope=summary['scope']+' This is 4x with optional glow OFF/ON, not FrameGen OFF. Paired references are same-source counterfactuals, not independent OFF runs. Registration flags are retained; no quality pass or causal attribution to glow. Captures excluded from FPS/latency.',
        trials=trials,figure=dict(file=figure.name,selected_rows=[dict(index=r['index'],song_s=r['song_s'],real=r['real'],new_picture=r['new_picture']) for r in selected],flagged_reversal=flag,source_file_sha256=hashes))
    a.out.write_text(json.dumps(exported,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps([dict(trial=t['trial'],new_percent=t['new_picture_ratio_percent'],extra_reversals=len(t['extra_reversal_rows']),camera_angle=t['source_camera_angle_range'],camera_size=t['source_camera_size_range']) for t in trials]))

if __name__=='__main__':main()
