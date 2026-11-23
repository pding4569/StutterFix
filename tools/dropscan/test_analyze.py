"""Synthetic ground-truth regression fixtures; no game or external modules."""
import csv
import json
import sys
from pathlib import Path
import unittest

import analyze as a


class AnalysisTests(unittest.TestCase):
    def setUp(self):
        # Under the checkout so every generated file obeys workspace permissions.
        root = Path(__file__).resolve().parents[2] / "out" / "dropscan-selftest"
        root.mkdir(parents=True, exist_ok=True)
        # TemporaryDirectory's Windows private ACL blocks this restricted worker.
        # Retain reproducible inputs/output in a normal inherited workspace dir.
        self.root = root / self._testMethodName
        self.root.mkdir(parents=True, exist_ok=True)

    def fixture(self, name, mode="bad", legacy=False):
        d = self.root / name
        d.mkdir(exist_ok=True)
        frames = d / "dropscan-frames.csv"
        samples = d / "profsamp-frames.txt"
        log = d / "Player.log"
        with frames.open("w", newline="", encoding="utf-8") as f, samples.open("w", encoding="utf-8") as s:
            writer = csv.writer(f)
            writer.writerow("frame,song_s,song_end_s,wall_start_s,wall_end_s,frame_ms,cpu_delayed_ms,gpu_delayed_ms,playing,record_us".split(","))
            t = -1.
            frame = 0
            while t < 35 - 1e-6:
                ms = 10.
                kind = "normal"
                if mode == "bad":
                    if 8 <= t < 10:
                        ms, kind = 40., "managed"
                    elif 14.999 < t < 15.011:
                        ms, kind = 2000., "native"
                    elif 1 <= t < 1.011 or 33 <= t < 33.011:
                        ms, kind = 100., "managed"
                end = t + ms / 1000
                writer.writerow([frame, t, end, 1000 + t, 1000 + end, ms, "NaN", "NaN", int(t >= 0), .1])
                token = "M:StutterFix.Work:Apply()" if kind == "managed" else "U:000ABC" if kind == "native" else "M:Game:Normal()"
                # Recursive function appears twice; inclusive ratios must stay <= 1.
                for k in range(max(1, int(ms // 4))):
                    stamp = 1000 + t + (k + .5) * ms / 1000 / max(1, int(ms // 4))
                    prefix = "" if legacy else f"T:{t};F:{frame};W:{stamp};S:8;"
                    s.write(prefix + token + ";" + token + ";M:Game:Root();X:[ntdll.dll+0x1]\n")
                frame += 1
                t = end
        log.write_text("[StutterFix] [모니터] 2000ms -> 게임 바깥 / gpu 0 cpu 0 | 곡 15.0초\n", encoding="utf-8")
        (d / "dropscan-meta.json").write_text('{"truncated":false}', encoding="utf-8")
        (d / "profsamp-meta.json").write_text('{"capacity_reached":false}', encoding="utf-8")
        return name, str(frames), str(samples), str(log)

    def test_hitches_sustained_freeze_boundaries_and_no_double_count(self):
        symbols = a.Symbols()
        m = a.analyze_map(*self.fixture("map"), symbols)
        self.assertAlmostEqual(m["median_frame_ms"], 10)
        self.assertEqual(m["hitch_threshold_ms"], 25)
        self.assertTrue(any(e["worst_ms"] == 2000 and e["samples"] == 500 for e in m["hitches"]))
        self.assertTrue(any(e["worst_ms"] == 2000 for e in m["drops"]))
        self.assertTrue(any(e["duration_s"] >= 1.5 and e["worst_ms"] == 40 for e in m["drops"]))
        self.assertTrue(any("시작" in "".join(e["boundary"]) for e in m["hitches"]))
        self.assertTrue(any("끝" in "".join(e["boundary"]) for e in m["hitches"]))
        for e in m["hitches"] + m["drops"]:
            self.assertTrue(all(0 <= c["bad_ratio"] <= 1 for c in e["candidates"]))
        report = a.build_report([m], symbols)
        grouped = sum(c["estimated_excess_ms"] for c in report["causes"])
        union_excess = sum(e["excess_ms"] for e in m["episodes"])
        self.assertLessEqual(grouped, union_excess + 1e-6)
        self.assertGreater(sum(e["excess_ms"] for e in m["hitches"] + m["drops"]), union_excess)
        self.assertTrue(any(c["function"] == "U:0xABC" for c in report["causes"]))
        self.assertEqual(len(m["monitor_lines"]), 1)

    def test_normal_song_and_legacy_zero_are_explicit(self):
        m = a.analyze_map(*self.fixture("normal", "normal"), a.Symbols())
        self.assertEqual(m["hitches"], [])
        self.assertEqual(m["drops"], [])
        legacy = a.analyze_map(*self.fixture("legacy", legacy=True), a.Symbols())
        self.assertGreater(legacy["sample_stats"]["legacy_unaligned"], 0)
        self.assertEqual(legacy["normal_samples"], 0)
        self.assertTrue(any("연결된 표본 0" in w for w in legacy["warnings"]))

    def test_cross_map_native_symbols_and_cli_output(self):
        first, second = self.fixture("A"), self.fixture("B")
        sym = self.root / "symb_lib.py"
        sym.write_text('def name(off):\n    assert off == 0xABC\n    return "Renderer::Wait"\n', encoding="utf-8")
        out = self.root / "report"
        args = ["--map", *first, "--map", *second, "--symb-lib", str(sym), "--out", str(out)]
        report = a.main(args)
        native = next(c for c in report["causes"] if c["function"] == "U:Renderer::Wait")
        self.assertEqual({i["map"] for i in native["intervals"]}, {"A", "B"})
        self.assertEqual(native["category"], "엔진 네이티브")
        parsed = json.loads((out / "dropscan-report.json").read_text(encoding="utf-8"))
        self.assertEqual(parsed["schema_version"], 1)
        self.assertIn("원인별 우선순위", (out / "dropscan-report.md").read_text(encoding="utf-8"))

    def test_truncation_and_missing_sidecars(self):
        paths = self.fixture("cap")
        directory = Path(paths[1]).parent
        (directory / "dropscan-meta.json").write_text('{"truncated":true}', encoding="utf-8")
        (directory / "profsamp-meta.json").write_text('{"capacity_reached":true,"failed_contexts":1}', encoding="utf-8")
        m = a.analyze_map(*paths, a.Symbols())
        self.assertTrue(any("전곡 결과 아님" in w for w in m["warnings"]))
        self.assertTrue(any("표본 상한" in w for w in m["warnings"]))
        (directory / "profsamp-meta.json").unlink()
        m = a.analyze_map(*paths, a.Symbols())
        self.assertTrue(any("profsamp-meta.json 없음" in w for w in m["warnings"]))

    def test_bad_frame_time_and_seek_rejected(self):
        paths = self.fixture("invalid", "normal")
        path = Path(paths[1])
        text = path.read_text(encoding="utf-8")
        lines = text.splitlines()
        fields = lines[1000].split(",")
        fields[1] = "0"
        lines[1000] = ",".join(fields)
        path.write_text("\n".join(lines), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "역행"):
            a.analyze_map(*paths, a.Symbols())
        fields[5] = "-10"
        lines[1000] = ",".join(fields)
        path.write_text("\n".join(lines), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "프레임 시간"):
            a.load_frames(path)


if __name__ == "__main__":
    unittest.main(testRunner=unittest.TextTestRunner(stream=sys.stdout, verbosity=2))
