"""Extract actual renderer observation/queue guards; never load game assemblies."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[3]
code = (Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'LiveDecoRenderer.cs').read_text(encoding='utf-8-sig')
start = code.index('        static void Queue(Source s, bool invalidate = false)')
end = code.index('        static void WriteFailed(Exception ex)', start)
target = Path(__file__).parent / 'obj' / 'ProductionNotices.cs'
target.parent.mkdir(exist_ok=True)
target.write_text('using System; using UnityEngine;\nnamespace StutterFix { static partial class NoticeAdapter {\n'
                  + code[start:end] + '\n}}\n', encoding='utf-8')
print('Extracted actual Queue/ObservingWrites/HasOwnedWatcher/BeforeWrite/AfterWrite without rewriting their bodies')
