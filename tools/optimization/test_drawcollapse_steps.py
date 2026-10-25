"""게임을 실행하지 않고 1관문 계획 순서와 이전 명시적 모드의 호환성을 검증한다."""
import importlib.util
import unittest
from pathlib import Path


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


mod = load('drawcollapse-steps')
prior = load('modcpu-steps')


class DrawCollapseStepsTests(unittest.TestCase):
    def test_diagnostic_prepares_at_start_before_seek_and_ownership(self):
        plan = mod.build('Arche.adofai')
        steps = plan['steps']
        self.assertEqual(plan['tag'], 'drawcollapse-gate1-260-270-diag2-isolated')
        self.assertEqual(steps.count('gpudeco live prepare'), 1)
        self.assertLess(steps.index('gpudeco live prepare'), steps.index('seek 260 12'))
        self.assertLess(steps.index('seek 260 12'), steps.index('gpudeco live on'))
        self.assertLess(steps.index('gpudeco live on'), steps.index('waitsongpos 260 40'))
        self.assertEqual(steps[-4:], ['gpudeco live off', 'resume', 'stop', 'quit'])
        self.assertEqual(plan['settings'], {'FrameGenOutside': 0, 'FrameGenRefresh': False})

    def test_readbacks_are_paused_separate_from_performance(self):
        steps = mod.build('Arche.adofai')['steps']
        self.assertEqual(steps.count('gpudeco drawcollapse diag'), 6)
        for second in (260, 262, 264, 266, 268, 270):
            start = steps.index(f'waitsongpos {second} 40')
            self.assertEqual(steps[start:start + 7], [f'waitsongpos {second} 40', 'pause',
                             'gpudeco drawcollapse validate', 'gpudeco drawcollapse diag',
                             'gpudeco live off', 'resume', 'stop'])
        self.assertFalse(any('rendermerge' in s or 'micro begin' in s or 'fuse begin' in s for s in steps))
        for option in ('submitsingle', 'latecache'):
            self.assertIn('gpudeco ' + option + ' off', steps)
            self.assertNotIn('gpudeco ' + option + ' on', steps)

    def test_no_seek_resume_or_stop_while_gpu_owned(self):
        # run1은 첫 진단 뒤 GPU 소유 중 resume하여 다음 validate 전에
        # FastMoveOriginal 복귀가 발생했다. 출력 전체의 소유 수명을 검사한다.
        steps = mod.build('Arche.adofai')['steps']
        owned = False
        acquired = released = 0
        for step in steps:
            if step == 'gpudeco live on':
                self.assertFalse(owned)
                owned = True
                acquired += 1
            elif step == 'gpudeco live off':
                self.assertTrue(owned)
                owned = False
                released += 1
            elif step == 'gpudeco drawcollapse diag':
                self.assertTrue(owned)
            elif step in ('resume', 'stop', 'quit') or step.startswith('seek '):
                self.assertFalse(owned, step)
        self.assertEqual((acquired, released), (6, 6))

    def test_each_sample_replays_the_verified_lead_in(self):
        steps = mod.build('Arche.adofai')['steps']
        starts = [i for i, step in enumerate(steps) if step.startswith('seek ')]
        self.assertEqual(len(starts), 6)
        for start, end in zip(starts, starts[1:] + [len(steps)]):
            sample = steps[start:end]
            self.assertEqual(sample[0], 'seek 260 12')
            self.assertEqual(sample.count('gpudeco live on'), 1)
            self.assertEqual(sample.count('gpudeco drawcollapse diag'), 1)
            self.assertEqual(sample.count('gpudeco live off'), 1)
            # 재생을 새로 시작할 때의 보호 구간은 CPU로 지나간 뒤 소유한다.
            self.assertLess(sample.index('decobake ready'), sample.index('waitsongpos 256 40'))
            self.assertLess(sample.index('waitsongpos 256 40'), sample.index('gpudeco packets compiled'))
            self.assertLess(sample.index('gpudeco packets compiled'), sample.index('gpudeco live on'))
            self.assertLess(sample.index('gpudeco live on'), sample.index('gpudeco drawcollapse diag'))
            self.assertLess(sample.index('gpudeco drawcollapse diag'), sample.index('gpudeco live off'))

    def test_explicit_legacy_modes_preserved(self):
        for mode in ('baseline', 'abba', 'single'):
            with self.subTest(mode=mode):
                actual = mod.build('Arche.adofai', mode)
                expected = prior.build('Arche.adofai', mode)
                # 기존 drawcollapse 복사본은 두 tag만 달랐다.
                expected['tag'] = {'baseline': 'drawcollapse-260-270',
                                   'abba': 'modcpu-260-270-abba-latecache1',
                                   'single': 'drawcollapse-260-270-abba-submitsingle2'}[mode]
                self.assertEqual(actual, expected)

    def test_unknown_mode(self):
        with self.assertRaises(ValueError):
            mod.build('Arche.adofai', 'unknown')


if __name__ == '__main__':
    unittest.main()
