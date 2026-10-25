"""이미지 소유 진단/ABBA 계획 생성 전용. 게임 실행·설치 기능 없음.

기본 diag2는 8장 상한 안에서 진단한다. diag1은 과거 실패 계획 보존용이다.
abba는 실제 픽셀 원인 수정 및 maxdiff<=3 확인 후 외부에서만 실행한다.
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BUNDLE = ROOT / 'tools/gpudeco-lab/evidence/drawcollapse5/bundle/stutterfix_gpudeco'


def prepare(chart, bundle, images=True, collapse=False, pose=False):
    steps = ['gpudeco off', 'decobake off', 'gpudeco drawcollapse reset', 'gpudeco drawcollapse unsafe off', 'decobake on',
             'gpudeco costprobe off', 'gpudeco effectcache off',
             'gpudeco submitsingle off', 'gpudeco latecache off', 'gpudeco nombp off',
             'gpudeco nativebatch on', 'gpudeco nativecopy on8masked', 'gpudeco zeroalpha on',
             'gpudeco bundle ' + str(bundle).replace('\\', '/'),
             'gpudeco images playback ' + ('on' if images else 'off'), 'open ' + str(chart),
             'gpuprobe off', 'gpusplit off', 'matshare off', 'livedeco off',
             'select 0', 'play', 'gpudeco scheduled', 'pause', 'decobake ready']
    if images:
        steps += ['gpudeco images compile', 'gpudeco images own on', 'gpudeco images matrix on',
                  'gpudeco images refreshpose ' + ('on' if pose else 'off')]
    steps += ['gpudeco live scope song']
    if collapse:
        steps += ['gpudeco drawcollapse prepare']
    return steps + ['gpudeco live prepare', 'resume', 'stop']


def boundary(second):
    return ['seek 260 12', 'decobake ready', f'waitsongpos {second} 40', 'pause',
            'framecmp clear', 'framecmp mask none', 'framecmp filters on', 'framecmp ui exclude',
            'framecmp noise O O2', 'framecmp assertzero O O2']


def build(chart, mode='diag', bundle=DEFAULT_BUNDLE, refreshpose=False):
    if mode not in ('diag', 'diag1', 'abba'):
        raise ValueError('unknown mode: ' + mode)
    steps = ['timeout 600', 'auto on']
    if mode in ('diag', 'diag1'):
        steps += prepare(chart, bundle)
        for second in (260, 262, 270):
            steps += boundary(second)
            if mode == 'diag':
                # Persist the CPU repeat before O2 is reused below. Save uses
                # the existing name and does not consume another shot slot.
                steps += ['framecmp save O', 'framecmp save O2']
            steps += ['gpudeco images refreshpose off', 'gpudeco live on',
                'framecmp noise G N', 'framecmp assertzero G N',
                *(['framecmp diff O G', 'framecmp save G', 'framecmp save N'] if mode == 'diag' else []),
                'gpudeco images locate cpuvertices on', 'framecmp noise D DN',
                'framecmp assertzero D DN',
                *(['framecmp diff O D', 'framecmp save D', 'framecmp save DN'] if mode == 'diag' else []),
                'gpudeco images locate cpuvertices off',
                'framecmp grab G2', 'framecmp assertzero G G2',
                *(['framecmp save G2'] if mode == 'diag' else []), 'gpudeco live off',
                'gpudeco images refreshpose on', 'gpudeco live on',
                'framecmp noise U ' + ('O2' if mode == 'diag' else 'UN'),
                'framecmp assertzero U ' + ('O2' if mode == 'diag' else 'UN'),
                *(['framecmp diff O U'] if mode == 'diag' else []),
                'gpudeco images diag pixel' + (' bounded' if mode == 'diag' else ''),
                'gpudeco images playback state']
            steps += [f'framecmp save {n}' for n in (('U', 'O2') if mode == 'diag' else ('O', 'O2', 'G', 'N', 'D', 'DN', 'G2', 'U', 'UN'))]
            steps += ['framecmp clear', 'gpudeco live off', 'resume', 'stop']
    else:
        steps += ['gpudeco off', 'gpudeco images gate reset',
                  'gpudeco images gate refreshpose ' + ('on' if refreshpose else 'off')]
        for index, images in enumerate((False, True, True, False)):
            # A/B 소유 집합을 재준비한다. 28523 준비의 own off는 CPU fallback 4976개다.
            steps += prepare(chart, bundle, images, True, images and refreshpose)
            steps += ['seek 260 12', 'decobake ready', 'waitsongpos 256 40',
                'gpudeco packets compiled', 'gpudeco live on', 'waitsongpos 258.5 40',
                'gpudeco drawcollapse ready', 'gpudeco drawcollapse on', 'waitsongpos 260 40',
                f'gpudeco images begin {index}', 'waitsongpos 270 30', 'gpudeco images end',
                'gpudeco drawcollapse off', 'gpudeco live off', f'gpudeco images restore {index}', 'stop']
        steps += prepare(chart, bundle, True, True, refreshpose)
        for index, second in enumerate((260, 262, 270)):
            steps += boundary(second) + ['gpudeco live on', 'framecmp noise G N', 'framecmp assertzero G N',
                'gpudeco drawcollapse ready', 'gpudeco drawcollapse on', 'framecmp grab B',
                'gpudeco drawcollapse off', 'framecmp grab G2', f'gpudeco images pixel {index}']
            steps += [f'framecmp save {n}' for n in ('O', 'O2', 'G', 'N', 'B', 'G2')]
            steps += ['framecmp clear', 'gpudeco live off', 'resume', 'stop']
        steps += ['gpudeco images result']
    steps += ['quit']
    return dict(steps=steps, settings={'FrameGenOutside': 0, 'FrameGenRefresh': False}, timeout_min=30,
        tag='imageown-260-270-' + ('diag2-bounded' if mode == 'diag' else 'diag1' if mode == 'diag1' else 'abba1'),
        notes={'status': '생성만; 이번 게임·설치·자동 장치 호출 없음', 'mode': 'diag' if mode == 'diag1' else mode,
            'bundle': str(bundle).replace('\\', '/'),
            'prerequisites': ('새 managed 빌드, EvaluateImageLive 포함 번들 확인; 정지 진단만'
                if mode in ('diag', 'diag1') else '픽셀 원인 수정·max<=3 확인 및 Claude의 PrepareDrawCollapse 두 훅 연결 필요; 아직 실행 불가'),
            'A': '이미지 candidate off + GPU23547 + draw 통합',
            'B': '이미지 candidate/own on + GPU28523 + draw 통합',
            'refreshpose': refreshpose if mode == 'abba' else 'off/on 두 가설 비교',
            'diagnostic': 'O=CPU, G=GPU 수식, D=CPU 정점 주입, U=UpdatePosition 선택; 잡음·G 복원 필수',
            'acceptance': '3440x1440@165; B/A>=1.20 (A=202라면 B>=242.4FPS); 260/262/270 maxdiff<=3; CPU/후보텍스처/런타임 fallback=0; >=30ms=0',
            'timing': 'ABBA 창에 pause/캡처/readback 없음; 준비·재열기 제외 260..270초',
            'lifetime': 'live off 뒤 resume/stop; 설치 복원은 외부 실행기 책임; 전곡 수명 미검증'})


def self_test():
    import unittest

    class Plans(unittest.TestCase):
        def test_lifetime_and_preconditions(self):
            for mode in ('diag', 'abba'):
                owned = paused = compiled = prepared = False
                for step in build('Arche.adofai', mode)['steps']:
                    if step == 'decobake off':
                        self.assertFalse(owned)
                        compiled = prepared = False
                    elif step == 'pause':
                        paused = True
                    elif step == 'gpudeco images compile':
                        self.assertTrue(paused and not owned)
                        compiled = True
                    elif step in ('gpudeco images own on', 'gpudeco images matrix on'):
                        self.assertTrue(paused and compiled and not owned)
                    elif step == 'gpudeco live prepare':
                        self.assertTrue(paused and not owned)
                        prepared = True
                    elif step == 'gpudeco live on':
                        self.assertTrue(prepared and not owned)
                        owned = True
                    elif step == 'gpudeco live off':
                        self.assertTrue(owned)
                        owned = False
                    elif step in ('resume', 'stop', 'quit') or step.startswith('seek '):
                        self.assertFalse(owned, step)
                        paused = False
                self.assertFalse(owned)

        def test_diagnostic_controls(self):
            plan = build('Arche.adofai')
            steps = plan['steps']
            self.assertEqual(plan['tag'], 'imageown-260-270-diag2-bounded')
            self.assertEqual(steps.count('gpudeco images diag pixel bounded'), 3)
            for pair in ('O O2', 'G N', 'D DN', 'U O2'):
                self.assertEqual(steps.count('framecmp assertzero ' + pair), 3)
            for state in ('on', 'off'):
                self.assertEqual(steps.count('gpudeco images locate cpuvertices ' + state), 3)
            self.assertNotIn('gpudeco drawcollapse prepare', steps)

        def test_capture_capacity_and_dependencies(self):
            import re
            # Model the real FrameCompare allocation/replacement semantics.
            maximum = int(re.search(r'const int MaxShots\s*=\s*(\d+)',
                (ROOT / 'FrameCompare.cs').read_text(encoding='utf-8')).group(1))
            for mode in ('diag', 'abba'):
                shots, asserted, saved = set(), set(), set()
                peak = 0
                for step in build('Arche.adofai', mode)['steps']:
                    args = step.split()
                    if step == 'framecmp clear':
                        shots, asserted, saved = set(), set(), set()
                    elif args[:2] in (['framecmp', 'grab'], ['framecmp', 'noise']):
                        if step == 'framecmp noise U O2':
                            self.assertIn(('O', 'O2'), asserted)
                            self.assertTrue({'O', 'O2', 'G', 'D', 'G2'} <= saved)
                        shots.update(args[2:])
                        peak = max(peak, len(shots))
                        self.assertLessEqual(len(shots), maximum, step)
                    elif args[:2] in (['framecmp', 'diff'], ['framecmp', 'assertzero']):
                        self.assertTrue(set(args[2:4]) <= shots, step)
                        if args[1] == 'assertzero':
                            asserted.add(tuple(args[2:4]))
                    elif args[:2] == ['framecmp', 'save']:
                        self.assertIn(args[2], shots)
                        saved.add(args[2])
                    elif step == 'gpudeco images diag pixel bounded':
                        self.assertTrue({'O', 'G', 'D', 'U', 'G2', 'N', 'DN', 'O2'} <= shots)
                        self.assertTrue({('O', 'O2'), ('G', 'N'), ('D', 'DN'), ('G', 'G2'), ('U', 'O2')} <= asserted)
                self.assertGreater(peak, 0)

        def test_partial_results_precede_pose(self):
            steps = build('Arche.adofai')['steps']
            for part in ('G', 'D', 'U'):
                self.assertEqual(steps.count('framecmp diff O ' + part), 3)
            first_pose = steps.index('framecmp noise U O2')
            self.assertLess(steps.index('framecmp diff O G'), first_pose)
            self.assertLess(steps.index('framecmp diff O D'), first_pose)
            legacy = build('Arche.adofai', 'diag1')
            self.assertEqual(legacy['tag'], 'imageown-260-270-diag1')
            self.assertEqual(legacy['steps'].count('framecmp noise U UN'), 3)

        def test_abba_ownership_and_uninterrupted_windows(self):
            steps = build('Arche.adofai', 'abba')['steps']
            candidate = None
            for step in steps:
                if step.startswith('gpudeco images playback '):
                    candidate = step.endswith(' on')
                if step.startswith('gpudeco images begin '):
                    self.assertEqual(candidate, int(step.split()[-1]) in (1, 2))
            for index in range(4):
                start = steps.index(f'gpudeco images begin {index}')
                self.assertEqual(steps[start+1:start+3], ['waitsongpos 270 30', 'gpudeco images end'])
                self.assertEqual(steps.count(f'gpudeco images restore {index}'), 1)
            self.assertEqual(steps[-2:], ['gpudeco images result', 'quit'])
            self.assertNotIn('gpudeco drawcollapse begin 0', steps)

        def test_targets_and_no_install(self):
            for mode in ('diag', 'abba'):
                steps = build('Arche.adofai', mode)['steps']
                for second in (260, 262, 270):
                    self.assertIn(f'waitsongpos {second} 40', steps)
                self.assertFalse(any(s.startswith(('install ', 'shell ', 'exec ')) for s in steps))
                self.assertEqual(steps.count('gpudeco drawcollapse unsafe off'), steps.count('gpudeco live prepare'))
                self.assertEqual(build('Arche.adofai', mode)['settings'], {'FrameGenOutside': 0, 'FrameGenRefresh': False})

        def test_pose_and_explicit_bundle(self):
            plan = build('Arche.adofai', 'abba', 'X:/validated/bundle', True)
            self.assertTrue(plan['notes']['refreshpose'])
            self.assertIn('gpudeco images gate refreshpose on', plan['steps'])
            self.assertEqual(plan['steps'].count('gpudeco images refreshpose on'), 3)
            self.assertEqual(plan['steps'].count('gpudeco bundle X:/validated/bundle'), 5)
            with self.assertRaises(ValueError):
                build('Arche.adofai', 'unknown')

    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Plans))
    return 0 if result.wasSuccessful() else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--chart')
    parser.add_argument('--output')
    parser.add_argument('--bundle', default=str(DEFAULT_BUNDLE))
    parser.add_argument('--mode', choices=('diag', 'diag1', 'abba'), default='diag')
    parser.add_argument('--refreshpose', action='store_true')
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    if args.self_test:
        raise SystemExit(self_test())
    if not args.chart or not args.output:
        parser.error('--chart and --output are required unless --self-test')
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(build(args.chart, args.mode, args.bundle, args.refreshpose), ensure_ascii=False, indent=1), encoding='utf-8')


if __name__ == '__main__':
    main()
