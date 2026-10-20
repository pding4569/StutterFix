"""Synthetic file-format/tolerance tests. No game execution or installation."""
import contextlib
import gzip
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
import unittest
import uuid

spec = importlib.util.spec_from_file_location("compare", Path(__file__).with_name("adoptsig-compare.py"))
compare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compare)


class Tests(unittest.TestCase):
    def setUp(self):
        base = Path(__file__).resolve().parent
        token = uuid.uuid4().hex
        self.off = base / ("adoptsig-test-" + token + "-off.gz")
        self.on = base / ("adoptsig-test-" + token + "-on.gz")
        self.addCleanup(lambda: self.off.unlink(missing_ok=True))
        self.addCleanup(lambda: self.on.unlink(missing_ok=True))
        self.values = [0., 0., 0., 0., 0., 1., 1., 1., 1., 1., 1., 1., 1., 1., 0., 0., 100., 100., 100., 100.]
        self.values += [100., 100., 200., 100., 100., 200., 1., 1., 1., 1., 1., 0., 1., 1., 1.]

    def write(self, path, times=(1., 1.01), change=None, complete=True, fingerprint="same", indices=None):
        header = dict(schema=8, channels=35, indices=indices or [7], hz=120, fingerprint=fingerprint, adopt=path == self.on)
        with gzip.open(path, "wb") as f:
            f.write((json.dumps(header)+"\n").encode())
            for serial, time in enumerate(times):
                row = self.values.copy()
                if change:
                    change(row, serial)
                f.write(struct.pack("<Bddi", 1, time, time, serial))
                f.write(struct.pack("<35f", *row))
            f.write(struct.pack("<B?i", 0, complete, len(times)))

    def compare(self, start=-float("inf"), end=float("inf")):
        with contextlib.redirect_stdout(io.StringIO()):
            a = compare.compare_pass(self.off, self.on, "off->on", [], start, end)
            return a + compare.compare_pass(self.on, self.off, "on->off", [], start, end)

    def test_exact_and_neighbor_alignment(self):
        self.write(self.off)
        self.write(self.on, (1.004, 1.014))
        self.assertEqual(self.compare(), 0)

    def test_pixel_tolerance(self):
        self.write(self.off)
        self.write(self.on, change=lambda row, _: row.__setitem__(0, .00009))
        self.assertEqual(self.compare(), 0)  # 0.009 px
        self.write(self.on, change=lambda row, _: row.__setitem__(0, .00011))
        self.assertGreater(self.compare(), 0)  # 0.011 px

    def test_relative_scale_and_color(self):
        self.write(self.off)
        self.write(self.on, change=lambda row, _: (row.__setitem__(5, 1.00009), row.__setitem__(7, 1.003)))
        self.assertEqual(self.compare(), 0)
        self.write(self.on, change=lambda row, _: row.__setitem__(5, 1.00011))
        self.assertGreater(self.compare(), 0)
        self.write(self.on, change=lambda row, _: row.__setitem__(11, 1.004))
        self.assertGreater(self.compare(), 0)

    def test_all_channels_share_one_neighbor(self):
        self.write(self.off, (1.,))
        self.write(self.on, (.996, 1.004), change=lambda row, serial: row.__setitem__(serial, 10.))
        self.assertGreater(self.compare(), 0)

    def test_reverse_pass_detects_extra_bad_frame(self):
        self.write(self.off, (1.,))
        self.write(self.on, (1., 1.004), change=lambda row, serial: row.__setitem__(0, 100. if serial else 0.))
        self.assertGreater(self.compare(), 0)

    def test_no_time_coverage_is_not_pass(self):
        self.write(self.off)
        self.write(self.on, (2., 2.01))
        with self.assertRaises(ValueError):
            self.compare()

    def test_rendered_output_and_visibility_must_match(self):
        self.write(self.off)
        for channel, value in ((22, 200.02), (29, 0.5), (30, 0.)):
            self.write(self.on, change=lambda row, _, c=channel, v=value: row.__setitem__(c, v))
            self.assertGreater(self.compare(), 0)

    def test_incomplete_empty_and_foreign_timeline_rejected(self):
        self.write(self.off)
        for options in (dict(complete=False), dict(times=()), dict(fingerprint="different"), dict(indices=[8])):
            self.write(self.on, **options)
            with self.assertRaises(ValueError):
                self.compare()

    def test_truncation_and_nonfinite_rejected(self):
        self.write(self.off)
        self.write(self.on, change=lambda row, _: row.__setitem__(3, float("nan")))
        with self.assertRaises(ValueError):
            self.compare()

    def test_explicit_window_still_validates_entire_file(self):
        self.write(self.off, (.5, 1., 1.01, 2.))
        self.write(self.on, (.6, 1.004, 1.014, 3.))
        self.assertEqual(self.compare(.9, 1.1), 0)
        self.write(self.on, (.6, 1.004, 1.014, 3.), complete=False)
        with self.assertRaises(ValueError):
            self.compare(.9, 1.1)
        self.write(self.on)
        self.on.write_bytes(self.on.read_bytes()[:-10])
        with self.assertRaises((EOFError, OSError, ValueError)):
            self.compare()


if __name__ == "__main__":
    unittest.main(testRunner=unittest.TextTestRunner(stream=sys.stdout))
