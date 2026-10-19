"""Link actual engine and deferred-Late code; no Unity/game process launched."""
from pathlib import Path
import subprocess

here = Path(__file__).resolve().parent
source = (here.parents[1] / 'TightLate.cs').read_text(encoding='utf-8-sig')

def block(marker, text=None):
    text = source if text is None else text
    start = text.index(marker)
    end = text.index('{', start) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]

field = next(line.strip() for line in source.splitlines() if 'readonly List<Entry> engineDeferred =' in line)
parts = [field, block('internal static void EngineDefer('), block('public static void EngineLatePostfix('), block('internal static int EngineReason(')]
(here / 'obj').mkdir(exist_ok=True)
(here / 'obj/LateFixture.cs').write_text('using System; using System.Collections.Generic; using UnityEngine;\nnamespace StutterFix { static partial class TightLate {\n' + '\n'.join(parts) + '\n} }', encoding='utf-8')
anim = (here.parents[1] / 'DecoAnim.cs').read_text(encoding='utf-8-sig')
markers = ['private static void Startup(', 'private static void ApplyE(',
           'private static void OnUpdate(', 'private static void OnComplete(',
           'private static void Step(', 'private static void PositionUpdate(',
           'internal static bool EngineReplayStep(', 'internal static bool EngineReplayPair(', 'private static void StepPair(']
(here / 'obj/ReplayFixture.cs').write_text(
    'using System; using UnityEngine;\nnamespace StutterFix { static partial class DecoAnim {\n'
    + '\n'.join(block(m, anim) for m in markers) + '\n} }', encoding='utf-8')
subprocess.run(['dotnet', 'run', '--project', str(here / 'DecoEngineCheck.csproj'), '-c', 'Release'], check=True)
