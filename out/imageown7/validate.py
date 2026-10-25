import hashlib
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
before = json.loads((out/'preserved.json').read_text(encoding='utf-8'))
after = {p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in before}
changed = {p for p in before if before[p] != after[p]}
assert changed == {'GpuDecoImagePlayback.cs','GpuDecoDrawCollapse.cs'}, changed
(out/'preserved-after.json').write_text(json.dumps(after,indent=2)+'\n',encoding='utf-8')
print(f'PASS protected {len(before)}: intended image/helper2 changed, remaining{len(before)-2} SHA256 unchanged')

def read(path):
    data = path.read_bytes()
    return data.decode('utf-16' if data.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')

for edition,warnings,fields in [('dev',48,279),('player',36,229),('playerauto',40,281)]:
    build = read(out/f'build-{edition}.txt')
    load = read(out/f'loadcheck-{edition}.txt')
    assert f'경고 {warnings}개' in build and '오류 0개' in build
    assert f'{fields}곳 확인, 틀림 0' in load
    print(f'PASS {edition}: build errors0 warnings{warnings}; LoadCheck fields{fields} bad0')
assert 'checks=330 failures=0 actual_methods=3' in read(out/'sampling-check.txt')
assert 'Ran 8 tests' in read(out/'generator-tests.txt') and '\nOK' in read(out/'generator-tests.txt')
print('PASS actual methods sampling330; generator8')
playback = read(root/'GpuDecoImagePlayback.cs')
draw = read(root/'GpuDecoDrawCollapse.cs')
geometry = read(root/'GpuDecoImageGeometryFixture.cs')
gpu = read(root/'GpuDecoGpu.cs')
refresh = playback[playback.index('static void RefreshGpuImageGeometry('):playback.index('static void CaptureGpuImageActivation(')]
assert refresh.index('UpdateImageGeometry(item,next)') < refresh.index('UpdateDrawCollapseTextureSlot(item,source.sprite.texture,source.sharedMaterial)')
assert 'item.UV[n*2]=v.UV[n].x;item.UV[n*2+1]=v.UV[n].y;' in geometry
assert 'data.Items[index]=next;' in gpu
old = subprocess.run(['git','show','HEAD:GpuDecoDrawCollapse.cs'],cwd=root,capture_output=True,check=True).stdout.decode('utf-8-sig')
expected = old.replace('RequireLive(texture.mipmapCount==1&&texture.wrapModeU==TextureWrapMode.Clamp&&texture.wrapModeV==TextureWrapMode.Clamp,\n                "image collapse changed mip/wrap");','RequireGpuImageCollapseSampling(item,texture);')
assert expected.replace('\r\n','\n') == draw.replace('\r\n','\n'), 'shared file changed beyond authorized statement'
print('PASS production UV copy/store/update order; shared file only authorized statement changed')
for typename,tokens in {
    'scrVisualDecoration':['SetSprite','RDUtils::SetPointSamplingVariant','UnityEngine.Texture::set_wrapMode'],
    'scrDecorationManager':['UpdateDecorationTilingLateUpdate','UnityEngine.Texture::set_wrapModeU','UnityEngine.Texture::set_wrapModeV'],
    'TextureManager':['UnityEngine.ImageConversion::LoadImage','UnityEngine.Texture::set_wrapMode']}.items():
    il = read(out/f'game-{typename}-il.txt')
    assert all(token in il for token in tokens)
print('PASS installed game ILScan three types: wrapping/sprite call paths present')

result = read(root/'docs/handoff/codex-imageown-result.md')
summary = read(root/'docs/handoff/codex-imageown-summary.txt').splitlines()
assert len(summary) == 10 and summary[0] == result.splitlines()[0]
assert all(s in summary[0] for s in ['IMAGEOWN=FAIL','direct_required=34','direct_limit=48','missing=0','reason=SAMPLING_FIX_NOT_RUN'])
docs = ['codex-imageown-result.md','codex-imageown-summary.txt','codex-master-20261022.md','im-autoloop-ledger.md','imageown-autoloop-ledger.md']
for name in docs:
    text = read(root/'docs/handoff'/name)
    assert '\ufffd' not in text
    head = '\n'.join(text.splitlines()[:17])
    assert all(s in head for s in ['17:05','SAMPLING_FIX_NOT_RUN'])
assert '## 자동 반복 6회차' in result
plan = json.loads(read(root/'tools/optimization/out/imageown-abba3-window-steps.json'))
assert len(plan['steps']) == 333
print('PASS five documents consistent; summary10; previous history retained; existing plan333 unchanged')
diff = subprocess.run(['git','diff','--check'],cwd=root,capture_output=True)
assert diff.returncode == 0, diff.stdout + diff.stderr
changes = subprocess.run(['git','diff','--name-only'],cwd=root,capture_output=True,check=True).stdout.decode().splitlines()
expected_changes = {'GpuDecoImagePlayback.cs','GpuDecoDrawCollapse.cs','docs/handoff/codex-al-im-7.md'} | {'docs/handoff/'+name for name in docs}
assert set(changes) == expected_changes, changes
print('PASS diff check; changes limited to image/helper2 + handoff5 + pre-existing instruction edit')
print('GAME/INSTALL/COMMIT/PUSH/DEVICE/EDITOR/SETTINGS/CONTROL WRITES=0; runtime acceptance NA')
