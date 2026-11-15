"""Audit fresh offline M4n artifacts; installation drift remains a separate HOLD."""
import hashlib
import json
import re
import sys
from pathlib import Path

root=Path(__file__).resolve().parents[2]
out=root/'out/direct-renderer/m4n'
def read(path):
    b=path.read_bytes()
    return b.decode('utf-16') if b.startswith((b'\xff\xfe',b'\xfe\xff')) else b.decode('utf-8-sig')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def rows(path):return json.loads(read(path))

validation=root/rows(out/'validation-latest.json')['directory']
assert validation.is_relative_to(out)
assert '74/74 including sf_z_drfull_v1' in read(validation/'exports-m4d-result.txt')
assert 'OK ' in read(validation/'build-native.log')
native=(root/'native/sfnative.dll').read_bytes()
artifacts={'native/sfnative.dll':sha(root/'native/sfnative.dll')}
builds={}
for edition,directory in [('dev','Release'),('player','Player'),('playerauto','PlayerAuto')]:
    log=read(validation/f'build-{edition}.log')
    assert re.search(r'(오류\s*0개|0 Error\(s\))',log),edition
    binary=root/f'bin/{directory}/StutterFix.dll'
    assert native in binary.read_bytes(),f'{edition}: stale embedded native'
    artifacts[str(binary.relative_to(root))]=sha(binary)
    builds[edition]={'errors':0,'warnings':int(next(n for n in re.findall(r'(?:경고\s*(\d+)개|(\d+) Warning\(s\))',log)[-1] if n))}
for file,marker in [('lifecycle.log','PASS 25 lifecycle/refusal cases'),('contract.log','PASS production resource loader shaders=21 variants=1012 stages=2024'),('policy-final.log','PASS M4n order rank_oracle=7000000 crossings=1000'),('pixel-metrics.log','FrameCompare M4l quality: 9 checks passed')]:
    assert marker in read(validation/file),file
markers={
 'drfull-m4m-warp.log':'PASS M4m moving playback fixtures game=not_launched',
 'drfull-m4l-warp.log':'cpp_heap_calls=0',
 'drfull-m4l-lifecycle.log':'failure_state=restored recovery=PASS stale_event=inert legacy_fallback=PASS',
 'drfull-m4j-warp.log':'PASS M4j',
 'drfull-m4k-warp.log':'PASS M4k resource_controls late=rejected ABI=append_only game=not_launched',
 'composite.log':'shared-stencil=9',
}
for file,marker in markers.items():assert marker in read(out/file),file
local=read(out/'drfull-m4n-warp.log')
assert 'PASS M4n locality GPU comparison game=not_launched' in local
local_rows=[]
for mode in (1,2):
    line=next(s for s in local.splitlines() if f'PASS M4n local mode={mode} ' in s)
    fields=dict(re.findall(r'([a-z_]+)=([^\s]+)',line))
    assert fields['full_delta']=='0' and fields['visited_sources']=='1536' and fields['index_sources']=='24' and fields['record_delta']=='12'
    assert f'PASS M4n replacement mode={mode} stable_capacity=True same_indices=True full_delta=0 record_delta=1 baked_vertices=64 pixels=exact' in local
    assert f'PASS M4n GPU_stage_sum mode={mode} samples=1 ' in local
    local_rows.append({'mode':mode,**fields})
for mode in ('cached','immediate','deferred','legacy'):
    for label,count,max_channel in [('all',48,1),('grab',8,0),('named-grab',8,0)]:
        assert f'PASS cases={count} maxChannel={max_channel}' in read(out/f'{mode}-{label}.log'),(mode,label)
manifest=rows(out/'plans/manifest.json')
assert manifest['launch'] is False and len(manifest['plans'])==29 and len(manifest['diagnostics'])==24
for filename in manifest['plans']+manifest['diagnostics']:
    path=Path(filename);assert path.is_relative_to(out/'plans') and path.name.startswith('m4n-')
    plan=rows(path);assert plan['measurement']['local_replan_target_us']==50
    assert 'fps_goal' in plan['measurement']
    if path.stem.endswith('-pixel'):
        assert 'framecmp assertzero d0 d00' in plan['steps'] and 'framecmp assertzero d1 d11' in plan['steps']
before=rows(out/'source-before.json');after=rows(out/'source-after.json')
assert {s['Path']:s['Hash'] for s in before}=={s['Path']:s['Hash'] for s in after}
assert all(sha(Path(row['Path']))==row['Hash'] for row in after),'compiled source drift'
hardware=out/'drfull-m4n-warp-hardware.log'
hardware_current=False
if hardware.exists() and (out/'hardware-source-after.json').exists():
    hardware_current=all(sha(Path(row['Path']))==row['Hash'] for row in rows(out/'hardware-source-after.json'))
    if hardware_current:
        assert len(re.findall(r'MEASURE M4n adapter=hardware',read(hardware)))==6
installed=rows(out/'installed-before.json')
current=[{'Path':row['Path'],'Hash':sha(Path(row['Path']))} for row in installed]
(out/'installed-after.json').write_text(json.dumps(current,indent=2),encoding='utf-8')
changed=[row['Path'] for row in current if not any(old['Path']==row['Path'] and old['Hash']==row['Hash'] for old in installed)]
result={'builds':{'native':{'errors':0},**builds},'exports_preserved':74,'embedded_native_matches':3,'locality':local_rows,
        'direct_plan_50us_fixture':float(local_rows[0]['local_plan_max_us'])<50,'hardware_comparison_current':hardware_current,
        'plans':29,'diagnostics':24,'artifacts':artifacts,'installed_session_changed':changed,
        'game_launched':False,'installed_by_checker':False,'commit_created':False,
        'Exit_faster_than_original':'UNMEASURED','HELLO_M4n_quality':'UNMEASURED'}
(out/'m4n-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS M4n four builds errors=0 exports=74/74 native embedded=3/3; locality/pixel/lifecycle/order/stage-sum fixtures; plans=29+24')
print('M4n direct plan <50us fixture:',result['direct_plan_50us_fixture'],'hardware current:',hardware_current)
if changed:
    print('HOLD session installed hash changed externally; snapshots retained; no restoration: '+', '.join(changed));sys.exit(2)
print('PASS M4n installed session hashes preserved 2/2')
