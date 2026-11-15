"""Generate M4e plans, offline only. No game/installation calls."""
import json, runpy, sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
sys.dont_write_bytecode=True
sys.argv[1:1]=['--milestone','m4e','--output',str(root/'out/direct-renderer/m4e/plans')]
runpy.run_path(str(Path(__file__).with_name('plan-m4c.py')),run_name='__main__')
out=root/'out/direct-renderer/m4e/plans'
control=json.loads((out/'m4e-hl50-pixel.json').read_text(encoding='utf-8'))
control['tag']='m4e-hl50-unityblend-pixel'
control['steps']=['drfull grab unity' if s=='drfull grab native' else s for s in control['steps']]
control['measurement']['scope']='diagnostic: original Unity DrawRenderer for blend shaders; not ABBA evidence'
(out/'m4e-hl50-unityblend-pixel.json').write_text(json.dumps(control,ensure_ascii=False,indent=2),encoding='utf-8')
print('PLAN ONLY additional HELLO Unity-blend diagnostic; no launch/install')
