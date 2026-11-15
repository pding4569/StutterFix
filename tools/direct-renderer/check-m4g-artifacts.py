"""M4g offline evidence gates. No game, installation or git writes."""
import hashlib,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=root/'out/direct-renderer/m4g'
def read(path):
    b=path.read_bytes()
    return b.decode('utf-16') if b.startswith((b'\xff\xfe',b'\xfe\xff')) else b.decode('utf-8-sig')
validation=out/'validation'
assert '74/74 including sf_z_drfull_v1' in read(validation/'exports-m4d-result.txt')
for edition in ('dev','player','playerauto'):
    assert re.search(r'(오류\s*0개|0 Error\(s\))',read(validation/f'build-{edition}.log')),edition
assert 'PASS production resource loader' in read(validation/'contract.log')
assert 'PASS preparation completion/failure' in read(validation/'lifecycle.log')
counts={}
for path,n in [(validation/'warp.log',48),(out/'default-warp.log',8)]:
    log=read(path)
    batches=re.findall(r'PASS M4f batch (\S+) sources=3 draws=1 changed=(\d+) max=(\d+)',log)
    assert len(batches)==n and all(a[1:] == ('0','0') for a in batches)
    assert len(re.findall(r'PASS M4g delta .*reorder_geometry=0 reorder_dispatches=0 reorder_indices=72 sparse_pose_bytes=80 stable_cb_color=0 pixels=identical',log))==n
    assert len(re.findall(r'PASS M4g thin .*transforms=24 differing_channels=0',log))==n
    assert len(re.findall(r'PASS M4g batch-cull .*offscreen_draws=0',log))==n
    counts[path.name]=n
warp=read(validation/'warp.log')
for required in ('PASS SPSC packets=128','PASS packed-poses dense-count=1','PASS world-only poses dense-count=1',
                 'PASS M4g sparse sources=7000 changed=131 frames=256 pose_bytes_per_frame=10480',
                 'PASS M4g reorder sources=7000 frames=64 geometry_bytes=0 pose_bytes=0 compute_dispatches=0'):
    assert required in warp,required
for name,named in [('grab',False),('named-grab',True)]:
    log=read(out/f'{name}.log')
    assert 'PASS cases=8 maxChannel=0 differingPixels=0' in log
    rows=re.findall(r'PASS M4g Grab (\S+) copies=(\d+) draws=(\d+) bytes=(\d+)',log)
    assert len(rows)==8
    for label,copies,draws,size in rows:
        assert int(copies)==(1 if named else int(draws)) and int(size)==int(copies)*192*128*4
policy=read(validation/'policy-final.log')
assert 'PASS M4g order identities common-drift=skip crossing=replan removed=replan stable_checks=10000 allocated=0' in policy
assert 'checks=20115 stable_layer_allocated=0' in policy
manifest=json.loads(read(out/'plans/manifest.json'))
assert manifest['launch'] is False and len(manifest['plans'])==24
for key,at in [('hl50',50),('wf',258),('dd',50),('sh',88)]:
    for label in ('plan','pixel','A1','B1','B2','A2'):
        path=out/f'plans/m4g-{key}-{label}.json';p=json.loads(read(path));steps=p['steps']
        assert str(path) in manifest['plans'] and f'seek {at} 8' in steps and f'waitsongpos {at} 400' in steps
        candidate=label=='pixel' or label.startswith('B')
        assert ('drfull warm' in steps)==candidate and ('drfull wait' in steps)==candidate
        assert steps[-2:]==['drfull cold','quit']
        if label=='pixel':assert 'framecmp assertzero d0 d00' in steps and 'drfull assert clean' in steps
        elif label!='plan':
            begin=steps.index('fpsmark start');end=steps.index(f'fpsmark end m4g-{key}-{label}')
            assert steps[begin+1:end]==['wait 12','drfull assert clean']
            assert steps[begin-1]=='drfull assert clean'
source=read(root/'native/sfnative/drfull.cpp')
assert 'geometryDirty=true;' not in source[source.index('void apply('):source.index('void patch(')]
assert 'markBake(draw)' in source and 'CopySubresourceRegion' in source and 'D3D11_MAP_WRITE_DISCARD' in source
assert 'precise float3 position=p.world._m01_m11_m21*local.y' in source
assert 'grab.frame!=stats.frames' in source and 'timing=profile?beginGpu():nullptr' in source
managed=read(root/'DirectFull.Render.cs')
assert 'orderTree.Matches(ordered)' in managed and 'key-distance' in managed and 'commit_peak_growths=' in managed
census=read(root/'DirectFull.Census.cs')
assert 'AuditConstants(constants' in census and 'FindPropertyIndex' in census and 'packetOffset' in census
paths=['DirectFull.cs','DirectFull.Render.cs','DirectFull.Canvas.cs','DirectFull.Census.cs','DirectFull.ObjectLayers.cs','DirectFull.Native.cs','DirectFull.Shaders.cs',
       'native/sfnative/drfull.cpp','native/sfnative/drfull.h','effects/direct-full/original-shaders.bin','native/sfnative.dll',
       'bin/Release/StutterFix.dll','bin/Player/StutterFix.dll','bin/PlayerAuto/StutterFix.dll']
hashes={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in paths}
(out/'final-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
result=dict(builds=4,exports=74,exact_batch_cases=56,thin_transform_cases=56*24,thin_differing_channels=0,
            sparse_pose_bytes_per_frame=10480,reorder_geometry_bytes=0,reorder_compute_dispatches=0,
            unnamed_grab_cases=8,named_grab_cases=8,plans=24,game_run=False,installation_write=False,
            live_exit_faster_than_335fps='UNMEASURED',live_dd_gpu_cause='UNMEASURED',live_dd_pixel_zero='UNMEASURED')
(out/'m4g-audit.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print('PASS M4g builds=4 exports=74 batch=56 thin_transforms=1344 exact delta uploads + stable geometry/order + Grab=16 plans=24')
print('HOLD real Exit >335 FPS / DD GPU bottleneck / DD thin-line pixels require game measurements; not run')
