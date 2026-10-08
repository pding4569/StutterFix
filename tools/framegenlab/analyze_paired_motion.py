"""Actual output versus its exact source image, from the research async GPU clip.
The reference is an identity composition of the selected source, not another OFF run.
It removes sampling/scheduling differences when checking added motion/reversals.
"""
import argparse,csv,json,math
from pathlib import Path
import numpy as np
from PIL import Image
from analyze_image_motion import phase

def analyze(folder,mode):
    rows=list(csv.DictReader((folder/'clip.csv').open(newline='')))
    records=[]; pictures=[]; references=[]
    def read(name):
        rgb=np.asarray(Image.open(folder/name).convert('RGB'),np.float32)
        h,w=rgb.shape[:2]; roi=np.s_[h//5:4*h//5,w//5:4*w//5]
        return rgb,rgb[roi]@np.array([.299,.587,.114],np.float32),roi
    def accepted(p):return p['psr']>=8 and p['texture_std']>=3
    for r in rows:
        i=int(r['index']); actual,gray,roi=read(f"clip-{mode}x-{i:03d}-{'real' if r['real']=='1' else 'generated'}.ppm")
        original,reference,_=read(f'reference-{mode}x-{i:03d}.ppm')
        next_original,next_reference,_=read(f'next-reference-{mode}x-{i:03d}.ppm')
        added=phase(reference,gray); source=phase(reference,next_reference)
        error=np.max(np.abs(actual-original),axis=2)
        next_error=np.max(np.abs(actual-next_original),axis=2)
        records.append(dict(index=i,song_s=float(r['song_s']),real=r['real']=='1',gate_reprojected=int(r['gate_reprojected']) if 'gate_reprojected' in r else None,
                            block_phase=float(r.get('block_phase',-1)),
                            new_picture_fraction_threshold=4/256,
                            new_picture=bool((error>3).mean()>=4/256 and (next_error>3).mean()>=4/256),
                            next_fraction_rgb_error_above3=float((next_error>3).mean()),
                            added=added,source=source,accepted=accepted(added) and accepted(source),
                            maximum_rgb_error=float(error.max()),mean_rgb_error=float(error.mean()),full_fraction_rgb_error_above3=float((error>3).mean()),
                            roi_fraction_rgb_error_above3=float((error[roi]>3).mean())))
        pictures.append(gray);references.append(reference)
    pairs=[]
    for i in range(1,len(rows)):
        a=phase(pictures[i-1],pictures[i]); b=phase(references[i-1],references[i])
        pairs.append(dict(index=i,song_s=float(rows[i]['song_s']),actual=a,reference=b,
                          accepted=accepted(a) and accepted(b)))
    def reversal(a,b,key):
        a=a[key]; b=b[key]
        return accepted(a) and accepted(b) and a['length']>.25 and b['length']>.25 and a['dx']*b['dx']+a['dy']*b['dy']<0
    reversal_rows=[]
    for a,b in zip(pairs,pairs[1:]):
        ar,br=reversal(a,b,'actual'),reversal(a,b,'reference')
        if ar or br: reversal_rows.append(dict(index=b['index'],song_s=b['song_s'],actual=ar,reference=br))
    valid=[r for r in records if r['accepted']]
    generated=[r for r in valid if not r['real']]
    valid_pairs=[r for r in pairs if r['accepted']]
    def lengths(key):
        values=[r[key]['length'] for r in valid_pairs]
        return dict(median=float(np.median(values)),p95=float(np.percentile(values,95)),maximum=max(values)) if values else None
    metadata_complete=all(r['gate_reprojected'] is not None for r in records)
    static_bad=[r for r in generated if r['source']['length']<.1 and r['added']['length']>.25]
    backwards=[r for r in generated if r['source']['length']>.25 and r['added']['length']>.25 and
               r['source']['dx']*r['added']['dx']+r['source']['dy']*r['added']['dy']<0]
    overshoot=[r for r in generated if r['added']['length']>r['source']['length']+.25]
    summary=dict(frames=len(rows),accepted_triplets=len(valid),accepted_generated=len(generated),
                 captured_generated=sum(not r['real'] for r in records),
                 captured_new_picture_generated=sum(not r['real'] and r['new_picture'] for r in records),
                 accepted_adjacent_pairs=len(valid_pairs),adjacent_actual_length=lengths('actual'),adjacent_reference_length=lengths('reference'),
                 actual_reversals=sum(r['actual'] for r in reversal_rows),reference_reversals=sum(r['reference'] for r in reversal_rows),
                 extra_reversals=sum(r['actual'] and not r['reference'] for r in reversal_rows),
                 static_source_added_motion=len(static_bad),generated_opposite_source_direction=len(backwards),
                 generated_overshoot_quarter_pixel=len(overshoot),reprojection_metadata_complete=metadata_complete,
                 gate_reprojected_frames=sum(r['gate_reprojected']==1 for r in records) if metadata_complete else None,
                 accepted_gate_reprojected=sum(r['gate_reprojected']==1 for r in valid) if metadata_complete else None,
                 added_length_median=float(np.median([r['added']['length'] for r in generated])) if generated else None,
                 added_length_max=max((r['added']['length'] for r in generated),default=None),
                 maximum_roi_fraction_rgb_error_above3=max((r['roi_fraction_rgb_error_above3'] for r in records),default=None),maximum_full_rgb_error=max((r['maximum_rgb_error'] for r in records),default=None))
    return dict(scope='Uncompressed step6 GPU triplets; center60% grayscale phase correlation; PSR>=8, std>=3. Identity reference is the SAME selected source/UI/final-mask composition, not independently sampled OFF gameplay. Next reference is the next real source. Additional displacement and reversal comparison are in stored pixels, not physical scanout. Image registration may fail on animation/flash/rotation; rejected triplets retained.',
                mode=mode,summary=summary,rows=records,pairs=pairs,reversals=reversal_rows)

if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('capture',type=Path); p.add_argument('--mode',type=int,required=True); p.add_argument('--out',type=Path,required=True); a=p.parse_args()
    result=analyze(a.capture,a.mode);a.out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result['summary']))
