"""Extract actual baseline methods, then test the linked production GroupStep.
No Unity/game process is started. The fixture uses managed setter stubs.
"""
from pathlib import Path
import subprocess

here = Path(__file__).resolve().parent
source = (here.parent.parent / 'DecoAnim.cs').read_text(encoding='utf-8-sig')

def block(marker):
    start = source.index(marker)
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

parts = [block('internal sealed class Rec')]
parts += [block('private static void ' + name + '(') for name in
          ('Startup', 'ApplyE', 'OnUpdate', 'OnComplete', 'Step', 'StepPair', 'Compact', 'CompactRecords', 'Complete')]
parts += [block('public static void UpdatePrefix(')]
fixture = 'using System; using System.Collections.Generic; using DG.Tweening; using UnityEngine;\nnamespace StutterFix { internal static partial class DecoAnim {\n' + '\n'.join(parts) + '\n} }\n'
(here / 'obj').mkdir(exist_ok=True)
(here / 'obj/ProductionFixture.cs').write_text(fixture, encoding='utf-8')
subprocess.run(['dotnet', 'run', '--project', str(here / 'GroupStepCheck.csproj'), '-c', 'Release'], check=True)
