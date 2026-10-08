"""Export the first genuinely changed mid-phase sample; diagnostic pictures only."""
import csv
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image
from measure_outside import cost_scenes

HERE=Path(__file__).resolve().parent
ROOT=HERE/'out/validation27/final-visual'
DEST=HERE/'validation-27'


def main():
    scenes=cost_scenes(ROOT/'capture',end=23)
    rows=list(csv.DictReader((ROOT/'capture/clip.csv').open()))
    for row in rows:
        if row['real']!='0' or not .25<=float(row['block_phase'])<=.75: continue
        i=int(row['index'])
        names=[f'clip-4x-{i:03d}-generated.ppm',f'reference-4x-{i:03d}.ppm',f'next-reference-4x-{i:03d}.ppm']
        arrays=[np.asarray(Image.open(ROOT/'capture'/name),dtype=np.int16) for name in names]
        differences=[float((np.max(np.abs(arrays[0]-a),axis=2)>3).mean()) for a in arrays[1:]]
        if min(differences)<4/256: continue
        outputs=['arche4-generated-song20.png','arche4-reference-song20.png','arche4-next-reference-song20.png']
        for name,array in zip(outputs,arrays): Image.fromarray(array.astype(np.uint8)).save(DEST/name)
        result=dict(scope='Separate final-build28s visual run; GPU captures invalidate performance/pacing. Original runner39s cost-window check failed; saved short data postprocessed with corrected23s end. Not a whole-song motion validation.',
                    selection_rule='First generated sample with phase0.25..0.75 differing from both references in at least4/256 pixels at threshold3/255',
                    sample=row,size=list(Image.open(ROOT/'capture'/names[0]).size),difference_fraction=differences,triplets=len(rows),camera_window=scenes,
                    build=json.loads((HERE/'out/outside/build-stamp.json').read_text(encoding='utf-8-sig')),files={})
        for name in names+['clip.csv','safety.txt','ownership.txt','native-init.txt']:
            p=ROOT/'capture'/name
            result['files'][name]=dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
        (DEST/'final-visual.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print(dict(sample=row,difference_fraction=differences))
        return
    raise RuntimeError('No genuinely new generated sample')


if __name__=='__main__': main()
