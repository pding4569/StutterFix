"""Known-transform offline validation of the measurement, not the interpolator.

Input snapshots remain unchanged. Synthetic photometric/grain/affine stresses
do not establish that the same conditions occurred in the recorded game.
"""
import argparse,csv,hashlib,json,math,sys
from pathlib import Path
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/framegenlab'))
from analyze_image_motion import phase

CASES=[
    ('stationary','uniform',0.,0.),
    ('integer','uniform',2.,1.),
    ('subpixel','uniform',.75,.5),
    ('known_reversal','uniform',2.,1.),
    ('positive_contrast','photometric',2.,1.),
    ('gamma','photometric',2.,1.),
    ('invert_middle','photometric',2.,1.),
    ('independent_grain32','photometric',2.,1.),
    ('independent_grain96','photometric',2.,1.),
    ('zoom2pct','affine',0.,0.),
    ('zoom_rebound4pct','affine',0.,0.),
    ('rotate_half_degree','affine',0.,0.),
    ('affine_and_translation','affine',2.,1.),
    ('fixed_grain70pct','multilayer',2.,1.),
    ('fixed_grain90pct','multilayer',2.,1.),
]

def pose(name,i,dx,dy):
    step=(0,1,0)[i] if name=='known_reversal' else i
    zoom=1+.02*i if name in ('zoom2pct','affine_and_translation') else 1.
    if name=='zoom_rebound4pct':zoom=[1.,1.04,1.][i]
    angle=math.radians(.5*i) if name in ('rotate_half_degree','affine_and_translation') else 0.
    return dict(tx=dx*step,ty=dy*step,scale=zoom,angle=angle)

def warp(image,p,center):
    """Forward truth: q=center+scale*R*(p-center)+translation.
    Explicit inverse coordinates and bilinear sampling; no phase-based truth.
    """
    h,w=image.shape;yy,xx=np.mgrid[:h,:w];cx,cy=center
    x=(xx-cx-p['tx'])/p['scale'];y=(yy-cy-p['ty'])/p['scale']
    c,s=math.cos(p['angle']),math.sin(p['angle'])
    u=np.clip(c*x+s*y+cx,0,w-1);v=np.clip(-s*x+c*y+cy,0,h-1)
    x0=np.floor(u).astype(int);y0=np.floor(v).astype(int)
    x1=np.minimum(x0+1,w-1);y1=np.minimum(y0+1,h-1)
    ax=u-x0;ay=v-y0
    return ((image[y0,x0]*(1-ax)+image[y0,x1]*ax)*(1-ay)+(image[y1,x0]*(1-ax)+image[y1,x1]*ax)*ay).astype(np.float32)

def apply_effect(image,name,i,rng,pattern):
    if name=='positive_contrast':image=(image-128)*[.25,1.5,.6][i]+128+[20,-20,0][i]
    elif name=='gamma':image=255*np.power(np.clip(image/255,0,1),[.7,1.8,.5][i])
    elif name=='invert_middle' and i==1:image=255-image
    elif name.startswith('independent_grain'):image=image+rng.normal(0,int(name.split('grain')[1]),image.shape)
    elif name.startswith('fixed_grain'):
        alpha=.7 if name=='fixed_grain70pct' else .9
        image=(1-alpha)*image+alpha*pattern
    return np.clip(image,0,255).astype(np.float32)

def reversal(a,b):
    return bool(a['accepted'] and b['accepted'] and a['length']>.25 and b['length']>.25 and a['dx']*b['dx']+a['dy']*b['dy']<0)

def load_bases(a):
    if a.inputs:
        with np.load(a.inputs,allow_pickle=False) as pack:
            manifests=json.loads(str(pack['bases_json']))
            images=pack['images'].copy()
        assert len(images)==len(manifests)==8 and images.shape[1:]==(240,573)
        assert np.isfinite(images).all() and images.dtype==np.float32
        return list(zip(manifests,images))
    result=[]
    for label,folder in [('off',a.raw_off),('glow',a.raw_glow)]:
        rows=list(csv.DictReader((folder/'frames.csv').open(newline='')))
        for target in [86.2,86.5,87.,88.]:
            row=min(rows,key=lambda r:abs(float(r['song_s'])-target))
            path=folder/f'raw-{int(row["index"]):03d}.ppm'
            image=np.asarray(Image.open(path).convert('RGB'),np.float32)@np.array([.299,.587,.114],np.float32)
            h,w=image.shape;box=(w//5,h//5,4*w//5,4*h//5)
            meta=dict(base=len(result),run=label,file=path.name,song_s=float(row['song_s']),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),size=[w,h],roi=list(box))
            result.append((meta,image))
    return result

def main():
    p=argparse.ArgumentParser();p.add_argument('--raw-off',type=Path);p.add_argument('--raw-glow',type=Path);p.add_argument('--inputs',type=Path);p.add_argument('--write-inputs',type=Path);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    if a.inputs and (a.raw_off or a.raw_glow) or not a.inputs and not(a.raw_off and a.raw_glow):p.error('Use --inputs OR both --raw-off/--raw-glow')
    # Independent sign/inverse-coordinate checks with exact integer texture roll.
    rng=np.random.default_rng(912);control=rng.normal(128,30,(80,100)).astype(np.float32)
    check=phase(control,np.roll(control,(1,2),(0,1)))
    assert abs(check['dx']-2)<.1 and abs(check['dy']-1)<.1,check
    shifted=warp(control,dict(tx=2.,ty=1.,scale=1.,angle=0.),(49.5,39.5))
    assert np.array_equal(shifted[1:,2:],control[:-1,:-2])
    assert np.array_equal(warp(control,dict(tx=0.,ty=0.,scale=1.,angle=0.),(49.5,39.5)),control)
    inputs=load_bases(a);manifests=[m for m,_ in inputs];trials=[]
    if a.write_inputs:np.savez_compressed(a.write_inputs,images=np.stack([im for _,im in inputs]),bases_json=np.asarray(json.dumps(manifests,ensure_ascii=False)))
    for manifest,image in inputs:
        base_id=manifest['base'];x0,y0,x1,y1=manifest['roi']
        center=((x0+x1-1)/2,(y0+y1-1)/2)
        for name,model,dx,dy in CASES:
            # Seeds change only grain cases. Repeating deterministic inputs
            # is not additional evidence, so count those once per base.
            for seed in ([41,42,43] if 'grain' in name else [41]):
                rng=np.random.default_rng(seed+base_id*1000)
                pattern=rng.uniform(0,255,image.shape).astype(np.float32)
                poses=[pose(name,i,dx,dy) for i in range(3)]
                synthetic=[apply_effect(warp(image,po,center),name,i,rng,pattern)[y0:y1,x0:x1] for i,po in enumerate(poses)]
                pairs=[]
                for i in range(2):
                    r=phase(synthetic[i],synthetic[i+1]);truth=[poses[i+1]['tx']-poses[i]['tx'],poses[i+1]['ty']-poses[i]['ty']]
                    r.update(accepted=bool(r['psr']>=8 and r['texture_std']>=3),truth_center_material_displacement=truth,center_material_error=math.hypot(r['dx']-truth[0],r['dy']-truth[1]))
                    # Uniform/photometric: all latent map points have the same motion.
                    # Affine/multilayer: center/map target is not a global composite truth.
                    pairs.append(r)
                truth_vectors=[r['truth_center_material_displacement'] for r in pairs]
                truth_reversal=all(math.hypot(*v)>.25 for v in truth_vectors) and np.dot(*truth_vectors)<0
                measured=reversal(*pairs)
                trials.append(dict(base=base_id,case=name,model=model,seed=seed,poses=poses,pairs=pairs,measured_reversal=measured,truth_map_center_reversal=bool(truth_reversal),accepted_both=all(r['accepted'] for r in pairs)))
    groups={}
    for name,model,_,_ in CASES:
        ts=[r for r in trials if r['case']==name];accepted=[p for t in ts for p in t['pairs'] if p['accepted']]
        errors=[r['center_material_error'] for r in accepted]
        groups[name]=dict(model=model,trials=len(ts),pairs=2*len(ts),accepted_pairs=len(accepted),accepted_center_target_errors_over_quarter_pixel=sum(e>.25 for e in errors),accepted_center_target_error=dict(median=float(np.median(errors)),p95=float(np.percentile(errors,95)),maximum=max(errors)) if errors else None,accepted_both=sum(t['accepted_both'] for t in ts),measured_reversals=sum(t['measured_reversal'] for t in ts),known_map_center_reversals=sum(t['truth_map_center_reversal'] for t in ts),accepted_false_reversals=sum(t['measured_reversal'] and not t['truth_map_center_reversal'] for t in ts),accepted_missed_reversals=sum(t['accepted_both'] and t['truth_map_center_reversal'] and not t['measured_reversal'] for t in ts))
    # The unchanged rigid integer control must agree with the independent ground truth.
    assert groups['integer']['accepted_pairs']==16 and groups['integer']['accepted_center_target_errors_over_quarter_pixel']==0,groups['integer']
    assert groups['known_reversal']['accepted_both']==8 and groups['known_reversal']['measured_reversals']==8,groups['known_reversal']
    pack_path=a.inputs or a.write_inputs
    result=dict(scope='Offline measurement fixture only. Eight unchanged stored HELLO snapshots, fifteen explicitly synthetic cases, three deterministic seeds only for grain cases (deterministic inputs counted once), three frames per trial. Central60% and unchanged phase()/PSR>=8/std>=3; reversal lengths>0.25 stored px. Uniform/photometric cases have known latent map translation. Affine center-material and multilayer map displacement are specified targets, not a unique global composite translation; errors in those groups are model limitations, not automatically estimator failures. Stress levels are not fitted to observed game filters or grain. No interpolation algorithm/rendering changes, new game runs, physical latency or quality pass. Existing game extra reversals14/16 retained.',controls=dict(integer_sign=check,exact_inverse_identity=True,exact_inverse_integer_interior=True),input_pack=dict(file=pack_path.name,sha256=hashlib.sha256(pack_path.read_bytes()).hexdigest()) if pack_path else None,bases=manifests,groups=groups,trials=trials)
    a.out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(groups,indent=2),flush=True)

if __name__=='__main__':main()
