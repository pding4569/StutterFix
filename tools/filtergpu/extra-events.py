from pathlib import Path
import json
d=json.loads(Path('D:/얼불춤 맵 파일/Windflower/Windflower.adofai').read_text(encoding='utf-8-sig'),strict=False)
for name in ['AAA_SuperComputer','TV_WideScreenHorizontal']:
 print(name,[(e['floor'],e.get('filterProperties')) for e in d['actions'] if e.get('filter')=='CameraFilterPack_'+name and e.get('enabled')][:3])
