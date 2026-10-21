"""Compile the unmodified production index/repair/arena methods with managed adapters."""
from pathlib import Path
root = Path(__file__).resolve().parents[3]
source = (root / 'LiveDecoRenderer.cs').read_text(encoding='utf-8-sig')
parts = []
for start, end in [('        static int Locate(', '        static void DrainChanges('),
                   ('        static bool Together(', '        static Run CreateRun(')]:
    parts.append(source[source.index(start):source.index(end)])
target = Path(__file__).parent / 'obj' / 'ProductionDelta.cs'
target.parent.mkdir(exist_ok=True)
target.write_text('using System; using System.Diagnostics; using System.Collections.Generic;\n'
                  'namespace StutterFix { partial class DeltaAdapter {\n' + '\n'.join(parts) + '\n}}\n', encoding='utf-8')
