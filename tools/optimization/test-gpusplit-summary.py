import importlib.util
from pathlib import Path
import sys
import unittest

spec = importlib.util.spec_from_file_location('gpusplit_summary', Path(__file__).with_name('gpusplit-summary.py'))
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)


def fixture():
    lines = []
    for i, (group, mean) in enumerate(zip(m.ORDER, [4, 3, 3.5, 3.8, 3.9, 3.7, 1, 4.2])):
        lines += [f'[gpusplit] hide={group} selected=4', f'[archegpu] stamp song={262+i*5}',
                  '[gpuprobe] reset queued (existing hooks and queries retained)',
                  f'[archegpu] stamp song={266+i*5}',
                  '[gpuprobe] ready=1 completed=10 errors=0 overflow=0',
                  f'[gpuprobe] camera:Camera#123 active_frames=10 calls=10 mean_frame_ms={mean:.4f} mean_active_ms={mean:.4f}',
                  '[gpuprobe] camera:UICamera#456 active_frames=10 calls=10 mean_frame_ms=99.0000',
                  '[gpuprobe] residual_frame_minus_cameras_ms=0.1', '[gpusplit] hide=none restored=1']
    return '\n'.join(lines)


class SummaryTest(unittest.TestCase):
    def test_means_and_nonadditivity(self):
        text = m.summarize(m.parse(fixture()))
        self.assertIn('기준 평균 4.1000', text)
        self.assertIn('±0.2000', text)
        self.assertIn('그룹 몫 합: +2.6000', text)
        self.assertIn('불일치(합−전체): -0.5000', text)

    def test_invalid_runs(self):
        for old, new in [('completed=10', 'completed=0'), ('errors=0', 'errors=1'),
                         ('camera:Camera#123', 'camera:Other#123'), ('stamp song=266', 'stamp song=269'),
                         ('active_frames=10', 'active_frames=9')]:
            with self.subTest(old=old), self.assertRaises(ValueError):
                m.summarize(m.parse(fixture().replace(old, new, 1)))

    def test_missing_and_repeated_runs(self):
        for text in [fixture().split('[gpusplit] hide=all')[0], fixture() + '\n' + fixture()]:
            with self.assertRaises(ValueError):
                m.summarize(m.parse(text))

    def test_midwindow_mutation(self):
        text = fixture().replace('[gpuprobe] ready=1', '[gpusplit] hide=tiles selected=1\n[gpuprobe] ready=1', 1)
        with self.assertRaises(ValueError):
            m.parse(text)


if __name__ == '__main__':
    unittest.main(testRunner=unittest.TextTestRunner(stream=sys.stdout))
