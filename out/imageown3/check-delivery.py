import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
summary = (root / 'docs/handoff/codex-imageown-summary.txt').read_text(encoding='utf-8').splitlines()
result = (root / 'docs/handoff/codex-imageown-result.md').read_text(encoding='utf-8')
assert len(summary) == 10
assert result.splitlines()[0] == summary[0]
assert 'reason=INTEGRATED_ABBA_NOT_RUN' in summary[0]
plan_path = root / 'tools/optimization/out/imageown-abba2-steps.json'
plan = json.loads(plan_path.read_text(encoding='utf-8'))
assert len(plan['steps']) == 333
assert hashlib.sha256(plan_path.read_bytes()).hexdigest() in result
before = json.loads((root / 'out/imageown3/preserved-before.json').read_text(encoding='utf-8-sig'))
for entry in before:
    assert hashlib.sha256(Path(entry['Path']).read_bytes()).hexdigest().upper() == entry['Hash']
assert len(before) == 12
for name in ('im-autoloop-ledger.md', 'imageown-autoloop-ledger.md', 'codex-master-20261022.md'):
    text = (root / 'docs/handoff' / name).read_text(encoding='utf-8')
    assert 'INTEGRATED_ABBA_NOT_RUN' in text[:1800]
for name in ('dev', 'player', 'playerauto'):
    build = (root / f'out/imageown3/build-{name}.txt').read_text(encoding='utf-16')
    assert '오류 0개' in build
    load = (root / f'out/imageown3/loadcheck-{name}.txt').read_text(encoding='utf-16')
    assert '틀림 0' in load
print('summary=10 lines; status/plan hash/documents consistent; 12 preserved hashes unchanged')
print('3 build logs error=0; 3 LoadCheck logs mismatch=0; game execution not performed')
