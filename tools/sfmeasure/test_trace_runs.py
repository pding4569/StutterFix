import json
import os
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

import trace_runs as T


class TraceRuns(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / "StutterFix.csproj").write_text("fixture")
        self.mod = self.root / "game/Mods/StutterFix"
        self.mod.mkdir(parents=True)
        self.log = self.root / "Player.log"
        self.api = types.SimpleNamespace(MOD_DIR=str(self.mod), GAME_DIR=str(self.root / "game"), PLAYER_LOG=str(self.log),
                                         game_running=Mock(return_value=False), launch_game=Mock(), kill_game=Mock(),
                                         sf_run=Mock(side_effect=RuntimeError("fixture after install failure")))
        self.patch = patch.dict(sys.modules, {"server": self.api})
        self.patch.start()
        self.addCleanup(self.patch.stop)
        for path, data in (("bin/PlayerAuto/StutterFix.dll", b"new DLL"), ("native/sfnative.dll", b"new native"),
                           ("tools/gpudeco-lab/evidence/bundle/stutterfix_gpudeco", b"bundle")):
            p = self.root / path
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(data)
        self.plan = {"steps": ["auto on", "quit"], "checks": {"gpu_until_portal": True}, "timeout_min": 2}
        (self.root / "plan.json").write_text(json.dumps(self.plan))

    def setup_worker(self, name):
        data, _, _, stamps, fp = T.plan_data(self.root, "plan.json")
        folder = self.root / "tools/optimization/out" / name
        folder.mkdir(parents=True)
        T.write_json(folder / "plan.json", data)
        T.write_json(folder / "manifest.json", dict(stamps, fingerprint=fp, nonce="fixture", checks=data["checks"], state="QUEUED"))
        T.write_json(T.process_lease(), {"pid": os.getpid(), "run": name, "nonce": "fixture"})
        return folder

    def test_failed_execution_restores_bytes_and_original_missing_files(self):
        folder = self.setup_worker("fault")
        originals = {"StutterFix.dll": b"original DLL", "sfnative.dll": b"original native",
                     "Settings.xml": b"original XML", "gpudeco-resource-probe.bin": b"original probe"}
        for name, data in originals.items():
            (self.mod / name).write_bytes(data)
        old_launch, old_kill = self.api.launch_game, self.api.kill_game
        T.run_worker(self.root, "fault", self.api)
        m = json.loads((folder / "manifest.json").read_text())
        self.assertEqual(m["state"], "FAILED")
        self.assertEqual(len(m["restored"]), 9)
        self.assertTrue(all(m["restored"].values()))
        for name, data in originals.items():
            self.assertEqual((self.mod / name).read_bytes(), data)
        self.assertFalse((self.mod / "autotest.txt").exists())
        self.assertFalse(T.process_lease().exists())
        self.assertIs(self.api.launch_game, old_launch)
        self.assertIs(self.api.kill_game, old_kill)
        old_kill.assert_not_called()

    def test_unowned_game_is_never_installed_or_closed(self):
        folder = self.setup_worker("unowned")
        self.api.game_running.return_value = True
        (self.mod / "StutterFix.dll").write_bytes(b"user DLL")
        T.run_worker(self.root, "unowned", self.api)
        self.assertEqual((self.mod / "StutterFix.dll").read_bytes(), b"user DLL")
        self.api.sf_run.assert_not_called()
        self.api.kill_game.assert_not_called()
        self.assertFalse(T.process_lease().exists())
        self.assertNotEqual(json.loads((folder / "manifest.json").read_text())["state"], "PASSED")

    def test_duplicate_never_starts_another_process(self):
        _, _, _, stamps, fp = T.plan_data(self.root, "plan.json")
        folder = self.root / "tools/optimization/out/old"
        folder.mkdir(parents=True)
        T.write_json(folder / "manifest.json", dict(stamps, fingerprint=fp, state="PASSED", ended=1))
        with patch.object(T.subprocess, "Popen") as spawn:
            result = T.start(self.root, "plan.json", "new")
        self.assertEqual(result["state"], "DUPLICATE")
        spawn.assert_not_called()

    def test_stale_lease_blocks_install_until_restoration_review(self):
        T.write_json(T.process_lease(), {"pid": 123, "run": "interrupted"})
        with patch.object(T, "alive", return_value=False), patch.object(T.subprocess, "Popen") as spawn:
            self.assertEqual(T.active_job()["state"], "STALE_REQUIRES_RESTORATION_REVIEW")
            with self.assertRaises(RuntimeError):
                T.start(self.root, "plan.json", "unsafe")
        spawn.assert_not_called()

    def test_worker_spawn_failure_releases_its_reservation(self):
        with patch.object(T.subprocess, "Popen", side_effect=OSError("fixture spawn failure")):
            with self.assertRaises(OSError):
                T.start(self.root, "plan.json", "spawn-fault")
        self.assertFalse(T.process_lease().exists())
        m = json.loads((self.root / "tools/optimization/out/spawn-fault/manifest.json").read_text())
        self.assertEqual(m["state"], "FAILED")

    def test_unknown_fallback_before_portal_is_not_full_gpu_success(self):
        result = T.summarize_log("[gpudeco] on=True owned=21\n[gpudeco] on=False reason=new_unknown_guard song=10\n[gpudeco] on=False reason=GameCallback:OnLandOnPortal song=180")
        self.assertEqual(result["early_recoveries"], 1)
        self.assertEqual(result["portal_song_s"], 180)

    def test_cpu_full_song_requires_clock_and_waitend_timeout_is_not_completion(self):
        result = T.summarize_log("[StutterFix] [자동 시험] [화면효과시각] song_s=308.004 unity_frame=99\n[StutterFix] [자동 시험] 곡이 끝나지 않음 (420초)")
        self.assertEqual(result["observed_song_s"], 308.004)
        self.assertEqual(result["waitend_timeouts"], 1)
        self.assertIsNone(result["portal_song_s"])

    def test_changed_artifact_changes_fingerprint(self):
        a = T.plan_data(self.root, "plan.json")[-1]
        (self.root / "bin/PlayerAuto/StutterFix.dll").write_bytes(b"new candidate")
        self.assertNotEqual(a, T.plan_data(self.root, "plan.json")[-1])

    def test_paths_and_multiline_commands_are_rejected(self):
        with self.assertRaises(ValueError):
            T.inside(self.root, "../escape")
        self.plan["steps"] = ["quit\nopen private"]
        (self.root / "plan.json").write_text(json.dumps(self.plan))
        with self.assertRaises(ValueError):
            T.plan_data(self.root, "plan.json")

    def test_log_output_omits_private_paths_and_distinguishes_real_fps(self):
        text = """[StutterFix] [자동 시험] 시작: open C:/PRIVATE_CHART.adofai
[StutterFix] [gpudeco] on=True owned=23547
[StutterFix] [gpudeco] live_state native_executed=120 native_errors=0
[StutterFix] [gpudeco] on=False reason=GameCallback:OnLandOnPortal song=308.4
[StutterFix] [곡] 평균 384 FPS (2.6ms), 곡 시작 연출 뒤 가장 긴 프레임 36ms, 끊김 1번
[StutterFix] [자동 시험] 끝 - 게임을 끕니다
"""
        result = T.summarize_log(text)
        self.assertEqual(result["real_fps"], 384)
        self.assertEqual(result["physical_display_fps"], "UNMEASURED")
        self.assertEqual(result["portal_song_s"], 308.4)
        self.assertEqual(result["native_executed"], 120)
        self.assertNotIn("PRIVATE_CHART", json.dumps(result))

    def test_failure_and_empty_audit_are_not_continuity_success(self):
        result = T.summarize_log("[StutterFix] [자동 시험] 단계 실패 'private'\n[gpudeco] on=False reason=FastMoveOriginal song=258.0\n[gpudeco] resume_audit frames=12 compared=0 mismatch=0")
        self.assertEqual(result["command_errors"], 1)
        self.assertEqual(result["early_recoveries"], 1)
        self.assertEqual(result["resume_compared"], 0)

    def test_micro_windows_keep_scope_ablation_and_parallel_cpu_separate(self):
        text = """[gpudeco] ablation destination_copy_skipped=False draw_hidden=0 replacement_root_inactive=False private=C:/SECRET
[gpudeco] micro_end song_begin=260 song_end=270 elapsed_s=10 frames=1700 real_fps=170 active=True
[gpudeco] threadcpu_row window=-1 role=main cpu_per_frame_ms=5.5
[gpudeco] threadcpu_row window=-1 role=render cpu_per_frame_ms=3.4
[gpudeco] ablation destination_copy_skipped=True draw_hidden=23547 replacement_root_inactive=False
[gpudeco] micro_end song_begin=260 song_end=270 elapsed_s=10 frames=2470 real_fps=247 active=True
"""
        r = T.summarize_log(text)
        self.assertEqual([w["real_fps"] for w in r["micro_windows"]], [170, 247])
        self.assertFalse(r["micro_windows"][0]["ablation"]["destination_copy_skipped"])
        self.assertEqual(r["micro_windows"][0]["thread_cpu_boundary_ms"], {"main": 5.5, "render": 3.4})
        self.assertNotIn("thread_cpu_boundary_ms", r["micro_windows"][1])
        self.assertNotIn("SECRET", json.dumps(r))
        self.assertEqual(r["physical_display_fps"], "UNMEASURED")

    def test_micro_windows_reject_nonfinite_incomplete_and_rewound_samples(self):
        text = "\n".join("[gpudeco] micro_end " + s for s in [
            "song_begin=260 song_end=270 elapsed_s=10 frames=1700 real_fps=NaN",
            "song_begin=270 song_end=260 elapsed_s=10 frames=1700 real_fps=170",
            "song_begin=260 song_end=270 elapsed_s=0 frames=0 real_fps=170",
            "song_begin=260 song_end=270 elapsed_s=10 frames=1700"])
        self.assertNotIn("micro_windows", T.summarize_log(text))

    def test_micro_output_is_bounded_and_does_not_replace_song_summary(self):
        text = "\n".join("[gpudeco] micro_end song_begin=260 song_end=270 elapsed_s=10 frames=1700 real_fps=170 active=True" for _ in range(20))
        r = T.summarize_log(text)
        self.assertEqual(len(r["micro_windows"]), 8)
        self.assertNotIn("real_fps", r)
        self.assertEqual(r["real_fps_scope"], "song_summary_not_micro_window")

    def test_other_window_cpu_and_invalid_micro_cannot_contaminate_last_result(self):
        text = """[gpudeco] micro_end song_begin=260 song_end=270 elapsed_s=10 frames=1700 real_fps=170 active=True
[gpudeco] threadcpu_row window=0 role=main cpu_per_frame_ms=99
[gpudeco] micro_end song_begin=260 song_end=270 elapsed_s=10 frames=1700 real_fps=NaN
[gpudeco] threadcpu_row window=-1 role=main cpu_per_frame_ms=88
"""
        self.assertNotIn("thread_cpu_boundary_ms", T.summarize_log(text)["micro_windows"][0])


if __name__ == "__main__":
    unittest.main()
