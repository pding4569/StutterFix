"""Convert literal material property calls in root mod source; no game code."""
from pathlib import Path
import re

root=Path(__file__).resolve().parents[2]
pattern=re.compile(r'(\.(?:Set(?:Float|Int|Integer|Color|Vector|Texture|Matrix|Buffer|FloatArray|VectorArray|ColorArray|MatrixArray)|Get(?:Float|Int|Integer|Color|Vector|Texture|Matrix)|HasProperty)\(\s*)"(_[A-Za-z0-9_]+)"')
sources={p:p.read_text(encoding='utf-8-sig') for p in root.glob('*.cs')}
names=sorted({m.group(2) for text in sources.values() for m in pattern.finditer(text)})
assert names,'No literal properties found; do not regenerate an empty table'
lines=['using UnityEngine;','','namespace StutterFix','{','    internal static class MaterialPropertyIds','    {']
lines += [f'        internal static readonly int P{name}=Shader.PropertyToID("{name}");' for name in names]
lines += ['        internal static void Warm(){}','    }','}']
(root/'MaterialPropertyIds.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
count=0
for p,text in sources.items():
    changed,n=pattern.subn(lambda m:m.group(1)+'MaterialPropertyIds.P'+m.group(2),text)
    if n:p.write_text(changed,encoding='utf-8');count+=n
print(f'Converted {count} material call sites to {len(names)} runtime property IDs')
