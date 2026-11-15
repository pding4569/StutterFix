"""Offline M4f contract/evidence audit. Never launch/install."""
import hashlib,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=root/'out/direct-renderer/m4f'
def read(path):
    data=path.read_bytes()
    return data.decode('utf-16') if data.startswith((b'\xff\xfe',b'\xfe\xff')) else data.decode('utf-8-sig')
validation=out/'validation'
assert '74/74 including sf_z_drfull_v1' in read(validation/'exports-m4d-result.txt')
for edition in ('dev','player','playerauto'):
    log=read(validation/f'build-{edition}.log')
    assert re.search(r'(\uc624\ub958 0\uac1c|0 Error\(s\))',log),edition
assert 'PASS production resource loader' in read(validation/'contract.log')
assert 'PASS preparation completion/failure' in read(validation/'lifecycle.log')
warp=read(validation/'warp.log')
batch=re.findall(r'PASS M4f batch (\S+) sources=3 draws=1 changed=(\d+) max=(\d+) cb_creates=(\d+)',warp)
assert len(batch)==48 and all(int(row[1])==0 and int(row[2])==0 for row in batch)
assert 'PASS world-only poses dense-count=1 sparse-native-id=5 pixels=identical' in warp
assert 'gpu_pretransform=True' in warp
assert 'PASS SPSC packets=128' in warp and 'PASS packed-poses dense-count=1' in warp
submission=re.search(r'PASS M4f submission sources=7000 frames=256 draws_per_frame=1 cpp_heap_calls=0 render_us=([\d.]+) batch_build_us=([\d.]+) execute_us=([\d.]+)',warp)
assert submission
moving=re.search(r'PASS M4f moving sources=7000 frames=256 draws_per_frame=1 cpp_heap_calls=0 render_us=([\d.]+) commit_us=([\d.]+) batch_build_us=([\d.]+)',warp)
assert moving
default=read(out/'default-warp.log')
default_batches=re.findall(r'PASS M4f batch (\S+) sources=3 draws=1 changed=(\d+) max=(\d+) cb_creates=(\d+)',default)
assert len(default_batches)==8 and all(int(row[1])==0 and int(row[2])==0 for row in default_batches)
assert 'PASS cases=8 maxChannel=0 differingPixels=0' in default
policy=read(validation/'policy-final.log')
assert 'PASS M4f incremental order sources=7000 oracle_edits=7000 allocated=0' in policy
assert 'checks=20115 stable_layer_allocated=0' in policy
old=json.loads(read(validation/'warp-m4d/summary.json'))
assert old['composition_cases']==24 and old['shared_stencil_cases']==9
grab=json.loads(read(out/'grab/warp-summary.json'))
assert grab['passed']==8 and grab['differing_pixels']==0 and grab['max_channel']==0
source=read(root/'DirectFull.Render.cs')
assert 'ordered.Sort(' not in source
fixed=source[source.index('static void BuildPoseTracking()'):source.index('static void Index(')]
assert '.Dispose()' not in fixed and 'haveNativeFrame = false' not in fixed
assert 'RemoveAtSwapBack' in fixed and 'new NativeQueue' not in source
for method in ('Update','LateUpdate','OnGUI'):
    overlay=read(root/'PerfOverlay.cs').split('private void '+method+'()',1)[1].split('#endif',1)[0]
    assert 'if (FrameCompare.Paused) return;' in overlay
manifest=json.loads(read(out/'plans/manifest.json'))
assert manifest['launch'] is False and len(manifest['plans'])==24
for key,at in [('hl50',50),('wf',258),('dd',50),('sh',88)]:
    for label in ('plan','pixel','A1','B1','B2','A2'):
        path=out/f'plans/m4f-{key}-{label}.json';plan=json.loads(read(path));steps=plan['steps']
        assert str(path) in manifest['plans'] and f'seek {at} 8' in steps and f'waitsongpos {at} 400' in steps
        candidate=label=='pixel' or label.startswith('B')
        assert ('drfull warm' in steps)==candidate
        assert ('drfull wait' in steps)==candidate
        assert steps[-2:]==['drfull cold','quit']
        if candidate:assert steps.index('drfull warm')<steps.index('pause')<steps.index('drfull on')<steps.index('drfull wait')
        if label=='pixel':
            assert 'framecmp assertzero d0 d00' in steps and 'framecmp assertlimit d0 d1 0.1 3 png' in steps
        elif label!='plan':
            begin=steps.index('fpsmark start');end=steps.index(f'fpsmark end m4f-{key}-{label}')
            assert steps[begin+1:end]==['wait 12'] and steps.index('resume')<begin
paths=['DirectFull.cs','DirectFull.Render.cs','DirectFull.Canvas.cs','DirectFull.ObjectLayers.cs','DirectFull.Native.cs','DirectSequenceChanges.cs','FrameCompare.cs','PerfOverlay.cs','native/sfnative/drfull.cpp','native/sfnative/drfull.h','native/sfnative.dll','bin/Release/StutterFix.dll','bin/Player/StutterFix.dll','bin/PlayerAuto/StutterFix.dll']
hashes={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in paths}
(out/'final-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
(out/'m4f-audit.json').write_text(json.dumps(dict(builds=4,exports=74,batch_cases=56,batch_changed_pixels=0,gpu_pretransform=True,world_only_sparse_pose=5,default_color_flip_cases=8,draw_reduction='3->1',submission=dict(sources=7000,draws_per_frame=1,cpp_heap_calls=0,render_us=float(submission[1]),batch_build_us=float(submission[2]),execute_us=float(submission[3]),scope='tiny geometry WARP submission; not game/GPU evidence'),moving_submission=dict(sources=7000,render_us=float(moving[1]),commit_us=float(moving[2]),batch_build_us=float(moving[3]),cpp_heap_calls=0,scope='all poses changed; tiny geometry WARP'),order_sources=7000,order_oracle_edits=7000,order_allocated=0,plans=24,grab=grab,game_run=False,installation_write=False,live_budget_950us='UNMEASURED',live_target_300us='UNMEASURED',live_setup_1s='UNMEASURED',live_main_max_2ms='UNMEASURED',live_off_off_zero='UNMEASURED'),indent=2),encoding='utf-8')
print('PASS M4f builds=4 exports=74 batch=56/56 exact pixels (Tile/Floor 48 + Default 8) order=7000 zero allocations live-bit + MPB boundaries plans=24')
print('HOLD live <0.95ms / target 0.3ms / setup <1s / main_max <2ms / off-off 0 require authorized game measurements')
