"""Extract raw cold-open fixtures without dropping slow/failed runs."""
from pathlib import Path
import json
import re
import shutil

ROOT = Path('E:/StutterFixWork')
DEST = ROOT/'wt-partial/docs/deco-spec/results/evidence/cold-open'
DEST.mkdir(parents=True, exist_ok=True)
rows = []
for file in sorted((ROOT/'night-results').glob('cold*-*.log')):
    text = file.read_text(encoding='utf-8', errors='replace')
    def value(pattern, default='NA'):
        found = re.findall(pattern, text)
        return found[-1] if found else default
    selected = [l for l in text.splitlines() if any(s in l for s in (
        '[coldopen]', '[이미지] 미리 푼 것', '[이미지] 맵 연 뒤', '게임 화면 맵 열림',
        '[FPS 구간]', 'phase_window end', 'frame:first-camera', 'render_counter=', '단계 실패'))]
    (DEST/(file.stem+'.txt')).write_text('\n'.join(selected)+'\n', encoding='utf-8')
    source = file.with_suffix('.json')
    if source.exists():
        shutil.copy2(source, DEST/source.name)
    rows.append(dict(run=file.stem,
        open_s=value(r'게임 화면 맵 열림 \(([\d.]+)초'),
        image_s=value(r'미리 푼 것 .*?전체 ([\d.]+)초'),
        fps=value(r'\[FPS 구간\] .*? : ([\d.]+) FPS'),
        gpu_ms=value(r'frame:first-camera.*?mean_frame_ms=([\d.]+)'),
        late=value(r'압축이 늦어 원래대로 (\d+)장', '0' if '[이미지] 미리 푼 것' in text else 'NA'),
        order_miss=value(r'순서 어긋남 (\d+)'),
        vram_MB=value(r'맵 연 뒤:.*?게임 ([\d.]+)MB'),
        wait_ms=value(r'WaitForLastPresentationAndUpdateTime ([\d.]+)ms'),
        loop_ms=value(r'phase_window end.*?합계 ([\d.]+)ms'),
        complete='[FPS 구간]' in text))
(DEST/'summary.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(rows, ensure_ascii=False))
