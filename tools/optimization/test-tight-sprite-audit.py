"""Independent edge support and outer-polygon admission checks."""
import importlib.util
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from PIL import Image

spec = importlib.util.spec_from_file_location('tight', Path(__file__).with_name('tight-sprite-audit.py'))
tight = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tight)


class SupportCheck(unittest.TestCase):
    def test_repeat_opposite_edge_and_alpha_one(self):
        with TemporaryDirectory() as tmp:
            path = Path(tmp)/'support.png'
            im = Image.new('RGBA', (16, 16), (210, 10, 90, 0))
            im.putpixel((0, 8), (0, 0, 0, 1))
            im.save(path)
            r = tight.geometry(path, 1)
            points = list(zip(r['xy'][::2], r['xy'][1::2]))
            self.assertEqual(min(x for x, y in points), 0)
            self.assertEqual(max(x for x, y in points), 16)
            self.assertEqual(r['colored_alpha0'], 255)
            self.assertGreater(r['padded_ratio'], 0)

    def test_outer_caps_contain_all_original_points(self):
        p = tight.audit.hull([(0, 3), (1, 1), (3, 0), (7, 1), (9, 3), (8, 8), (5, 10), (1, 8)])
        for cap in (4, 6, 8):
            q = tight.capped_hull(p, cap, 12, 12)
            self.assertLessEqual(len(q), cap)
            for a, b in zip(q[-1:]+q[:-1], q):
                for x, y in p:
                    self.assertGreaterEqual((b[0]-a[0])*(y-a[1])-(b[1]-a[1])*(x-a[0]), -1e-6)


if __name__ == '__main__':
    unittest.main()
