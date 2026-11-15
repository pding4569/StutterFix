"""Read existing M4m game evidence only; do not run or alter the game."""
import json
import re
from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=root/'out/direct-renderer/m4n'
source=Path('E:/StutterFixWork/night-results')
rows=[]
for path in sorted(source.glob('m4m-*.log')):
    text=path.read_text(encoding='utf-8-sig',errors='replace')
    lines=text.splitlines()
    state=[s for s in lines if 'mode=M4h-original active=True' in s]
    fps=re.findall(r'\[FPS [^]]*\]\s+([^:]+)\s*:\s*([\d.]+) FPS',text)
    fields={}
    if state:
        fields=dict(re.findall(r'([A-Za-z0-9_]+)=([^\s]+)',state[-1]))
    keys=('frames','main_us','render_us','collect_us','pose_us','sort_us','gpu_camera_us','gpu_native_frame_us','cache_plan_builds','rt_cache_plan_us','cache_indirect_draws','cache_direct_draws','cache_cull_items','cache_requested','cache_indirect_batches')
    row=dict(path=str(path),fps=fps,metrics={k:fields[k] for k in keys if k in fields},
             pixel_lines=[line for line in lines if '[framecmp]' in line and any(k in line for k in ('RESULT','VERDICT','PSNR','assertquality','assertzero')) and '[자동 시험] 시작' not in line])
    rows.append(row)
(out/'prior-game-evidence.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
for row in rows:
    print(Path(row['path']).name, row['fps'], json.dumps(row['metrics'],ensure_ascii=False))
