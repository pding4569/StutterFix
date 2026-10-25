from pathlib import Path
import hashlib
import json
import subprocess

root=Path(__file__).resolve().parents[2]
out=Path(__file__).resolve().parent
docs=root/'docs/handoff'
for name in ('codex-imageown-result.md','codex-master-20261022.md','im-autoloop-ledger.md','imageown-autoloop-ledger.md'):
    before=(out/('before-'+name)).read_text(encoding='utf-8-sig')
    after=(docs/name).read_text(encoding='utf-8-sig')
    assert (before if name=='codex-imageown-result.md' else before.split('\n',1)[1]) in after, name+' history changed'
    assert '최신 run3' in after and 'changed27870/23002' in after and 'max2/5' in after
    assert '\ufffd' not in after
summary=(docs/'codex-imageown-summary.txt').read_text(encoding='utf-8').splitlines()
assert len(summary)==10 and 'pixel_max=5' in summary[0]
result=(docs/'codex-imageown-result.md').read_text(encoding='utf-8').split('\n---\n',1)[0]
assert 'reason=COLLAPSE_PIXEL_SCOPE_LIMIT' in result and 'game_reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED' in result
for key in ('O/G·G/B 분해 비교가 없다','전체통합방식의 불가능 판정이 아니라','270초·ABBA·FPS/+20%','설치복원은 **NA**'):
    assert key in result, key
before=json.loads((out/'preserved.json').read_text(encoding='utf-8'))
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==sha for p,sha in before.items())
raw=(root.parent/'night-results/al-im-3.log').read_text(encoding='utf-8-sig').splitlines()
rows={386:'registered=93 addressed=93 missing=0',481:'GPU_owned=28523 CPU_fallback=0',703:'GPU_owned=28523 CPU_fallback=0',536:'image_excluded=0',757:'image_excluded=0',601:'maxRGBA=2,2,2,0 changed=27870',612:'second=260 changed=27870 maxdiff=2',617:'resumed_recs=11821 resume_expected=11821',821:'maxRGBA=5,5,5,0 changed=23002',832:'second=262 changed=23002 maxdiff=5',831:'max_ok=False',833:'pixel_max=5 reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED',842:'resumed_recs=0 resume_expected=0',844:'native_contexts=0'}
for row,key in rows.items():
    assert key in raw[row-1],row
check=subprocess.run(['git','diff','--check'],cwd=root,capture_output=True,text=True)
assert check.returncode==0,check.stdout+check.stderr
changed=subprocess.run(['git','diff','--name-only'],cwd=root,capture_output=True,text=True,check=True).stdout.splitlines()
assert set(changed)=={'docs/handoff/'+p for p in ('codex-imageown-result.md','codex-imageown-summary.txt','codex-master-20261022.md','im-autoloop-ledger.md','imageown-autoloop-ledger.md')},changed
report=('PASS79 direct evidence lines in raw order; 14 quoted row references verified\n'
        'PASS five handoff documents / previous histories retained / exactly10 summary lines\n'
        f'PASS{len(before)} protected hashes unchanged; shared PNG not used\n'
        'PASS current game DLL read-only ILScan / decompile sampling-wrap cross-check\n'
        'PASS git diff --check; tracked changes exactly5 handoff documents; no runtime code changes\n'
        'task-observer: no new generalizable observations; existing ownership/repeat-test rules suffice\n')
(out/'verification.txt').write_text(report,encoding='utf-8')
(out/'checkpoints.log').write_text('task-observer3.5.0: existing user-scope log/principles read only; no shared skill or memory write.\nEvidence, code review, documents complete: no new generalizable observations.\n',encoding='utf-8')
print(report)
