"""Extract real pause/resume and renderer lifetime guard; never load game code."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[3]
source = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'FrameCompare.cs'
code = source.read_text(encoding='utf-8-sig')
fields = code[code.index('        internal static bool Paused'):code.index('        static readonly Harmony pausePatch')]
methods = code[code.index('        internal static void Pause()'):code.index('        internal static bool Step(')]
renderer_source = Path(sys.argv[2]) if len(sys.argv) > 2 else root / 'LiveDecoRenderer.cs'
renderer = renderer_source.read_text(encoding='utf-8-sig')
guard_start = renderer.index('            if (!manager || reset != SceneReset.Count')
guard_end = renderer.index('            if (!SortSupported()', guard_start)
guard = renderer[guard_start:guard_end]
target = Path(__file__).parent / 'obj' / 'ProductionPause.cs'
target.parent.mkdir(exist_ok=True)
target.write_text('using System; using UnityEngine; using DG.Tweening;\nnamespace StutterFix {\n'
                  'internal sealed partial class FrameCompare : UnityEngine.Object {\n'
                  + fields + methods + '\n}\n'
                  'internal static class RendererLifetime {\n'
                  'internal static void Check(bool manager, int reset) {\n' + guard + '\n}}}\n', encoding='utf-8')
print('Extracted production FrameCompare fields/Pause/Resume and renderer lifetime guard')
