"""A/B/B/A 계약 및 기존 기준선 계획의 호환성을 오프라인 확인한다."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('modcpu_steps', Path(__file__).with_name('modcpu-steps.py'))
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


class ModCpuStepsTests(unittest.TestCase):
    def test_baseline_contract(self):
        plan = mod.build('Arche.adofai', 'baseline')
        steps = plan['steps']
        self.assertEqual(len(steps), 66)
        self.assertEqual(steps.count('gpudeco micro begin'), 2)
        self.assertFalse(any('latecache' in s for s in steps))
        self.assertEqual(plan['tag'], 'modcpu-260-270')

    def test_abba_order_and_window(self):
        steps = mod.build('Arche.adofai')['steps']
        toggles = [s for s in steps if s in ('gpudeco latecache off', 'gpudeco latecache on')]
        self.assertEqual(toggles, ['gpudeco latecache off', 'gpudeco latecache on', 'gpudeco latecache on', 'gpudeco latecache off'])
        self.assertEqual(steps.count('gpudeco latecache validate'), 8)
        self.assertEqual(steps.count('gpudeco micro begin'), 4)
        for start in [i for i, s in enumerate(steps) if s in ('gpudeco latecache on', 'gpudeco latecache off')]:
            window = steps[start:start + 22]
            self.assertEqual(window[1], 'seek 260 12')
            self.assertLess(window.index('gpudeco live on'), window.index('waitsongpos 260 40'))
            self.assertLess(window.index('gpudeco latecache validate'), window.index('fuse begin'))
            self.assertLess(window.index('gpudeco micro begin'), window.index('waitsongpos 270 25'))
            self.assertLess(window.index('waitsongpos 270 25'), window.index('gpudeco micro end'))
            self.assertEqual(window[-2:], ['gpudeco live off', 'stop'])

    def test_scope_and_settings(self):
        plan = mod.build('Arche.adofai')
        steps = plan['steps']
        self.assertEqual(plan['settings'], {'FrameGenOutside': 0, 'FrameGenRefresh': False})
        self.assertEqual(steps.count('gpudeco live prepare'), 1)
        self.assertLess(steps.index('gpudeco live scope song'), steps.index('gpudeco live prepare'))
        self.assertLess(steps.index('gpudeco live prepare'), steps.index('seek 260 12'))
        self.assertFalse(any('rendermerge' in s for s in steps))
        self.assertEqual(steps[-1], 'quit')

    def test_unknown_mode(self):
        with self.assertRaises(ValueError):
            mod.build('Arche.adofai', 'unknown')


if __name__ == '__main__':
    unittest.main()
