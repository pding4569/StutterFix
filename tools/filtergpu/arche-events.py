from pathlib import Path
import re
from collections import Counter
p=next(p for p in Path('D:/얼불춤 맵 파일/Arche').glob('*FULL.adofai') if 'NONTILE' not in p.name)
text=p.read_text(encoding='utf-8-sig')
print(Counter(re.findall(r'"eventType"\s*:\s*"([^"]*Filter[^"]*)"',text)))
print(Counter(re.findall(r'"filter"\s*:\s*"(CameraFilterPack_[^"]*)"',text)))
