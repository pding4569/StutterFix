import hashlib
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
raw = (root.parent / 'night-results/al-im-3.log').read_text(encoding='utf-8-sig').splitlines()
evidence = (root / 'docs/handoff/im-autoloop-run3-evidence.txt').read_text(encoding='utf-8-sig').splitlines()
selected = [s for s in evidence if s.startswith('[StutterFix]')]
cursor = 0
for line in selected:
    found = next((i for i in range(cursor, len(raw)) if raw[i] == line or (len(line) == 1500 and raw[i].startswith(line))), None)
    assert found is not None, line[:100]
    cursor = found + 1
print(f'run3 ordered direct evidence matched: {len(selected)}')
for key in ('DRAWCOLLAPSE_ARRAY_TOTAL', 'DRAWCOLLAPSE_PREPARED', 'activation_ownership allocated=', 'activation_rollback reason=', "단계 실패 'gpudeco live on'", 'shutdown_disposed='):
    matches = [(i+1, s) for i, s in enumerate(raw) if key in s and not '자동 시험] 시작:' in s]
    assert matches, key
    for line, value in matches:
        print(f'raw line {line}: {value}')
rollback = next(s for s in raw if 'activation_rollback reason=' in s)
fields = dict(re.findall(r'(\w+)=([^ ]+)', rollback))
assert fields['restored_recs'] == fields['saved_recs'] == '11821'
assert fields['continuity'] == 'RESTORED'
assert sum("단계 실패 'gpudeco live on'" in s for s in raw) == 2
assert not any(('IMAGEOWN_WINDOW' in s or 'IMAGEOWN_PIXEL' in s or 'GPUIMG_DIAG mode=collapsecheck' in s) and not '자동 시험] 시작:' in s for s in raw)
print('integrated pixels/ABBA absent; missing texture identity/bank class NOT recorded in old log')
before = json.loads((out / 'preserved-before.json').read_text(encoding='utf-8-sig'))
after = []
for row in before:
    digest = hashlib.sha256(Path(row['Path']).read_bytes()).hexdigest().upper()
    assert digest == row['Hash'], row['Path']
    after.append(dict(Path=row['Path'], Hash=digest))
(out / 'preserved-after.json').write_text(json.dumps(after, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'protected owner sources/shader/bundle/generator/old JSON/config unchanged: {len(after)} SHA256 matches')
old = (out / 'GpuDecoDrawCollapse-before.cs').read_text(encoding='utf-8-sig')
new = (root / 'GpuDecoDrawCollapse.cs').read_text(encoding='utf-8-sig')
start = new.index('            if(texture&&!gpuImageCollapseAddresses.ContainsKey(')
end = new.index('            RequireLive(texture&&material', start)
assert new[:start] + new[end:] == old
print('shared GpuDecoDrawCollapse.cs: only allowed helper failure logging changed')
