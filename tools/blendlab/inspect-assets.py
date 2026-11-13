"""Read-only inventory of game blend states. Reuses the installed offline reader."""
import json
import sys
from pathlib import Path
root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root / 'out/asset-reader-deps'))
import UnityPy
game = Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data')
rows = []
for filename in ('sharedassets4.assets', 'resources.assets', 'Resources/unity_builtin_extra'):
    if not (game / filename).exists():
        continue
    env = UnityPy.load(str(game / filename))
    for obj in env.objects:
        if obj.type.name != 'Shader':
            continue
        tree = obj.read_typetree()
        parsed = tree.get('m_ParsedForm', {})
        name = parsed.get('m_Name') or tree.get('m_Name', '')
        if name not in ('Sprites/Tile', 'Sprites/Default', 'Legacy Shaders/Particles/Additive',
                        'Hidden/BlendModes/SpritesDefault/Grab'):
            continue
        passes = []
        for sub in parsed.get('m_SubShaders', []):
            for p in sub.get('m_Passes', []):
                state = p.get('m_State', {})
                passes.append(dict(type=p.get('m_Type'), state=state))
        rows.append(dict(name=name, asset=filename, path_id=obj.path_id, passes=passes))
    del env
out = Path(__file__).with_name('asset-states.json')
out.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
for row in rows:
    print(row['name'], row['asset'], [(p['state'].get('rtBlend0')) for p in row['passes']])
