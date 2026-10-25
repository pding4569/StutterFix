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
        self.assertEqual(plan['tag'], 'drawcollapse-gate1-260-270-diag1')
        self.assertEqual(steps.count('gpudeco live prepare'), 1)
        self.assertLess(steps.index('gpudeco live prepare'), steps.index('seek 260 12'))
        self.assertLess(steps.index('seek 260 12'), steps.index('gpudeco live on'))
        self.assertLess(steps.index('gpudeco live on'), steps.index('waitsongpos 260 40'))
        self.assertEqual(steps[-3:], ['gpudeco live off', 'stop', 'quit'])
        self.assertEqual(plan['settings'], {'FrameGenOutside': 0, 'FrameGenRefresh': False})

    def test_readbacks_are_paused_separate_from_performance(self):
        steps = mod.build('Arche.adofai')['steps']
        self.assertEqual(steps.count('gpudeco drawcollapse diag'), 6)
        for second in (260, 262, 264, 266, 268, 270):
            start = steps.index(f'waitsongpos {second} 40')
            self.assertEqual(steps[start:start + 5], [f'waitsongpos {second} 40', 'pause',
                             'gpudeco drawcollapse validate', 'gpudeco drawcollapse diag', 'resume'])
        self.assertFalse(any('rendermerge' in s or 'micro begin' in s or 'fuse begin' in s for s in steps))
        for option in ('submitsingle', 'latecache'):
            self.assertIn('gpudeco ' + option + ' off', steps)
            self.assertNotIn('gpudeco ' + option + ' on', steps)

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
