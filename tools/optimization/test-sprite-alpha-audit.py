"""Independent small masks check bbox/hull geometry and alpha=1 retention."""
import importlib.util
from pathlib import Path
import unittest
from PIL import Image

spec = importlib.util.spec_from_file_location('sprite_alpha', Path(__file__).with_name('sprite-alpha-audit.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class AlphaAuditCheck(unittest.TestCase):
    def test_masks(self):
        folder = Path(__file__).parent / 'out' / 'gpusplit2'
        folder.mkdir(parents=True, exist_ok=True)
        p = folder / 'test-mask.png'
        try:
            im = Image.new('RGBA', (8, 8))
            im.save(p)
            r = audit.measure(p)
            self.assertEqual((r['bbox_area'], r['hull_area'], r['hull_vertices']), (0, 0, 0))
            for x in range(2, 6):
                for y in range(1, 7):
                    im.putpixel((x, y), (240, 30, 0, 1))
            im.save(p)
            r = audit.measure(p)
            self.assertEqual(r['bbox'], (2, 1, 6, 7))
            self.assertEqual((r['bbox_area'], r['hull_area'], r['hull_vertices'], r['hull_triangles']), (24, 24, 4, 2))
            im.putpixel((2, 1), (0, 0, 0, 0))  # One missing corner: bbox unchanged, hull loses half a pixel.
            im.save(p)
            r = audit.measure(p)
            self.assertEqual((r['bbox_area'], r['hull_area'], r['hull_vertices']), (24, 23.5, 5))
            self.assertEqual(r['nonzero'], 23)
        finally:
            p.unlink(missing_ok=True)


if __name__ == '__main__':
    unittest.main()
