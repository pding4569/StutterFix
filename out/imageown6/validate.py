import hashlib
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
result = (root / 'docs/handoff/codex-imageown-result.md').read_text(encoding='utf-8')
summary = (root / 'docs/handoff/codex-imageown-summary.txt').read_text(encoding='utf-8').splitlines()
assert len(summary) == 10
assert summary[0] == result.splitlines()[0]
assert result.startswith('IMAGEOWN=FAIL')
assert all(token in result.splitlines()[0] for token in ('registered=93', 'addressed=89', 'missing=4', 'direct_required=36', 'direct_limit=32', 'arrays_used=4', 'required_slots=40'))
for name in ('codex-master-20261022.md', 'im-autoloop-ledger.md', 'imageown-autoloop-ledger.md'):
    head = '\n'.join((root / 'docs/handoff' / name).read_text(encoding='utf-8').splitlines()[:9])
    assert '6회차' in head or '회차 6' in head
    assert 'WINDOW_TEXTURE_CAPACITY_EXCEEDED' in head
    assert '직접필요36/32' in head and '배열4/4' in head
groups = json.loads((out / 'groups.json').read_text(encoding='utf-8'))
assert len(groups) == 44
for g in groups:
    row = f"| {g['size']} | {g['format']} | {g['tier']} | {g['textures']} | {g['addressed']} | {g['textures']-g['addressed']} |"
    assert row in result
assert '## 자동 반복 5회차' in result and '## 자동 반복 4회차' in result
scan = (out / 'game-visual-il.txt').read_text(encoding='utf-8-sig')
sprite_blocks = [re.split(r'\n  \S', s, maxsplit=1)[0] for s in scan.split('\n  SetSprite\n')[1:]]
assert len(sprite_blocks) == 2
for block in sprite_blocks:
    assert all(s in block for s in ('SpriteRenderer::set_sprite', 'scrVisualDecoration::ResizeCollider', 'RDUtils::SetPointSamplingVariant', 'UnityEngine.Texture::set_wrapMode'))
snapshot = json.loads((out / 'preserved.json').read_text(encoding='utf-8'))
assert len(snapshot) == 15
for p, digest in snapshot.items():
    assert hashlib.sha256((root / p).read_bytes()).hexdigest() == digest, p
print('PASS: 5 documents consistent; summary10; group44/texture93/address89/missing4; both SetSprite IL paths; source/helper/generator/plans15 SHA256 unchanged')
