from pathlib import Path
import json
d=json.loads(Path('D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai').read_text(encoding='utf-8-sig'))
for name in ['Blur_Radial','Blur_Movie','Color_Noise','Color_BrightContrastSaturation','TV_WideScreenHorizontal']:
 events=[e for e in d['actions'] if e.get('filter')=='CameraFilterPack_'+name and e.get('enabled')]
 print(name,[(e['floor'],e.get('filterProperties')) for e in events[:2]])
