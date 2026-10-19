"""Extract small raw IL evidence from existing temporary ilspycmd output."""
import os
from pathlib import Path

OUT = Path(__file__).resolve().parent / 'out'
def read(path):
    b = path.read_bytes()
    s = b.decode('utf-16' if b[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig')
    return '\n'.join(line for line in s.splitlines() if line.strip()) + '\n'

def extract(which, names):
    text = read(Path(os.environ['TEMP']) / ('decotrack-' + which + '-il.txt'))
    blocks = []
    for name in names:
        end = text.find('// end of method ' + name + '\r')
        if end < 0: end = text.find('// end of method ' + name + '\n')
        if end < 0:
            print('NOT FOUND', name)
            continue
        start = text.rfind('\n\t.method ', 0, end)
        if start < 0: start = text.rfind('\n\t\t.method ', 0, end)
        blocks.append(text[start:end + len('// end of method ' + name)].strip())
    (OUT / (which + '-raw-selected.txt')).write_text('\n\n'.join(blocks), encoding='utf-8')
    print(which, len(blocks), 'methods', sum(map(len, blocks)), 'characters')

extract('game', ['LevelEvent::Decode', 'LevelEvent::Encode',
    'LevelEventButton::Init', 'RDUtils::ParseEnum', 'ffxMoveDecorationsPlus::Decode',
    'ffxMoveDecorationsPlus::.ctor', 'LevelData::Decode', 'LevelEvent::.ctor',
    'RDEditorUtils::CheckModsDependency','ffxPlusBase::SetStartTime','ffxPlusBase::StartEffectWithOffset'])

for which, classes in [('game',['DecPlacementType'])]:
    s = read(Path(os.environ['TEMP']) / ('decotrack-' + which + '-il.txt'))
    result = []
    for name in classes:
        end = s.find('// end of class ' + name)
        start = s.rfind('\n.class ',0,end)
        if end >= 0: result.append(s[start:end+len('// end of class '+name)])
    if which == 'game':
        marker = '// end of method LevelEvent::.ctor'
        end = s.find(marker)
        while end >= 0:
            start = s.rfind('\n\t.method ',0,end)
            result.append(s[start:end+len(marker)])
            end = s.find(marker,end+len(marker))
        for name in ['<Start>g__LoadLevelEventSprites|450_0', '<Start>g__LoadLevelCategorySprites|450_1']:
            end = s.find("// end of method scnEditor::'"+name+"'")
            if end>=0:
                start=s.rfind('\n\t.method ',0,end)
                result.append(s[start:end+len("// end of method scnEditor::'"+name+"'")])
    (OUT/(which+'-raw-extra.txt')).write_text('\n\n'.join(result),encoding='utf-8')
