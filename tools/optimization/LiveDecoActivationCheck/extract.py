"""Extract production activation/sort guards; never load game assemblies."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[3]
source = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'LiveDecoRenderer.cs'
code = source.read_text(encoding='utf-8-sig')
parts = []
for start, end in [('        static void StartRendering()', '        static void Register()'),
                   ('        static bool SortSupported()', '        static bool Live('),
                   ('        static void PreCull(', '        static int Locate(')]:
    parts.append(code[code.index(start):code.index(end)])
parts.append(next(line for line in code.splitlines() if 'internal static float EnabledSeconds =>' in line))

begin = code[code.index('        internal static void Begin()'):code.index('        static void StartRendering()')]
assert 'RenderFrame();' not in begin, 'Begin must not admit/validate/render in Update'
assert begin.index('starting = true;') < begin.index('Register();') < begin.index('Camera.onPreCull += PreCull;')
assert 'nextReport = float.PositiveInfinity;' in begin
assert '!starting && !FrameCompare.Paused && EnabledSeconds >= 3' in code
assert 'starting = false;' in code[code.index('        internal static void Stop()'):]
target = Path(__file__).parent / 'obj' / 'ProductionActivation.cs'
target.parent.mkdir(exist_ok=True)
target.write_text('using System; using System.Diagnostics; using System.Globalization;\n'
                  'namespace StutterFix { internal static partial class ActivationAdapter {\n'
                  + '\n'.join(parts) + '\n}}\n', encoding='utf-8')
print('Production Begin/deferred-render/warmup/cleanup contracts: 5 checks passed')
