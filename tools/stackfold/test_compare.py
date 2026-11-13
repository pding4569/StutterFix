"""Small independent checks for sampling, storage, and alpha semantics."""
import unittest

import numpy as np

from compare import bilinear, metrics, store


class CompareTests(unittest.TestCase):
    def test_unorm_storage_after_each_blend_changes_answer(self):
        # Hand calculation: 0 -> 128/255 -> 192/255, versus .75 -> 191/255.
        sequential = store(store(np.array([.5], np.float32), "ARGB32") * .5 + .5, "ARGB32")
        folded = store(np.array([.75], np.float32), "ARGB32")
        self.assertEqual(round(float(sequential[0]) * 255), 192)
        self.assertEqual(round(float(folded[0]) * 255), 191)

    def test_half_storage_is_not_float32(self):
        x = np.array([.33333334], np.float32)
        self.assertNotEqual(float(store(x, "ARGBHalf")[0]), float(x[0]))

    def test_bilinear_texel_centres_and_midpoint(self):
        t = np.zeros((2, 2, 4), np.uint8)
        t[0, 0] = 255
        u = np.array([[.25, .5]], np.float32)
        v = np.array([[.25, .5]], np.float32)
        sampled = bilinear(t, u, v)
        np.testing.assert_array_equal(sampled[0, 0], np.ones(4))
        np.testing.assert_array_equal(sampled[0, 1], np.full(4, .25))

    def test_alpha_blend_semantics_differ(self):
        # For zero destination alpha, straight blending stores a^2, premult stores a.
        a = np.float32(.02)
        self.assertLess(float(a * a), float(a))
        # RGB follows the same recurrence for the two equivalent source conventions.
        trans = (1 - a) ** 150
        rgb, alpha_straight = 0., 0.
        for _ in range(150):
            rgb = rgb * (1 - a) + a
            alpha_straight = alpha_straight * (1 - a) + a * a
        self.assertAlmostEqual(float(rgb), float(1 - trans), places=5)
        self.assertAlmostEqual(float(alpha_straight), float(a * (1 - trans)), places=5)

    def test_pixel_thresholds_use_largest_channel(self):
        a = np.zeros((1, 2, 4), np.float32)
        b = a.copy()
        b[0, 0, 0] = 2 / 255
        b[0, 1, 3] = 3 / 255
        m = metrics(a, b)
        self.assertEqual(m["pixels_gt1_percent"], 100)
        self.assertEqual(m["pixels_gt2_percent"], 50)


if __name__ == "__main__":
    unittest.main()
