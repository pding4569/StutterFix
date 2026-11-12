"""Analyze four saved ablation images after the owned game has exited."""
import json
import re
import shutil
import sys
from pathlib import Path
from PIL import Image, ImageChops

folder = Path(sys.argv[1]).resolve()
plan = json.loads((folder / 'plan.json').read_text(encoding='utf-8'))
mod = Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix')
for label in ('base', 'noise', 'hidden', 'restore'):
    target = folder / (label + '.png')
    if not target.exists():
        shutil.copy2(mod / 'framecmp' / (plan['capture_prefix'] + '_' + label + '.png'), target)
with Image.open(folder / 'base.png') as raw:
    base = raw.convert('RGBA')
result = dict(scope='paused ablation; not playback quality or physical display FPS', dimensions=list(base.size), pixels={})
for label in ('noise', 'hidden', 'restore'):
    with Image.open(folder / (label + '.png')) as raw:
        current = raw.convert('RGBA')
    assert current.size == base.size
    delta = ImageChops.difference(base, current)
    mask = delta.getchannel('R')
    for channel in ('G', 'B', 'A'):
        mask = ImageChops.lighter(mask, delta.getchannel(channel))
    result['pixels'][label] = dict(changed_pixels=base.width * base.height - mask.histogram()[0], max_rgba=[v[1] for v in delta.getextrema()])
text = (folder / 'Player.log').read_text(encoding='utf-8', errors='replace')
result['raw_windows'] = [line for line in text.splitlines() if '[FPS 구간]' in line]
result['normal_autotest_quit'] = '[자동 시험] 끝 - 게임을 끕니다' in text
result['restored_files'] = json.loads((folder / 'restore.json').read_text())['equal']
result['restored_preferences'] = json.loads((folder / 'screen-preferences-restore.json').read_text())['equal']
result['noise_and_restore_exact'] = all(result['pixels'][k]['changed_pixels'] == 0 for k in ('noise', 'restore'))
result['ablation_visible'] = result['pixels']['hidden']['changed_pixels'] > 0
(folder / 'ablation-analysis.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False))
