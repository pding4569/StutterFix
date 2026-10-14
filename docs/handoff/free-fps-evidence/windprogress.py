from pathlib import Path
import re
s=(Path.home()/'AppData/LocalLow/7th Beat Games/A Dance of Fire and Ice/Player.log').read_text(encoding='utf-8',errors='replace')
print(re.findall(r'FREEFPS MASK WindflowerStatic [AB]',s)[-4:])
print([l for l in s.splitlines() if '[무료FPS 대조]' in l][-5:])
print([l for l in s.splitlines() if '[임무단계] open' in l][-1:])
