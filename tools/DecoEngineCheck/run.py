"""Link actual engine and deferred-Late code; no Unity/game process launched."""
from pathlib import Path
import subprocess

here = Path(__file__).resolve().parent
source = (here.parents[1] / 'TightLate.cs').read_text(encoding='utf-8-sig')

def block(marker):
    start = source.index(marker)
    end = source.index('{', start) + 1
    depth = 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

field = next(line.strip() for line in source.splitlines() if 'readonly List<Entry> engineDeferred =' in line)
parts = [field, block('internal static void EngineDefer('), block('public static void EngineLatePostfix(')]
(here / 'obj').mkdir(exist_ok=True)
(here / 'obj/LateFixture.cs').write_text('using System; using System.Collections.Generic; using UnityEngine;\nnamespace StutterFix { static partial class TightLate {\n' + '\n'.join(parts) + '\n} }', encoding='utf-8')
subprocess.run(['dotnet', 'run', '--project', str(here / 'DecoEngineCheck.csproj'), '-c', 'Release'], check=True)
