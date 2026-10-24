import copy
import unittest
from harness_gate import assess


class EvidenceGate(unittest.TestCase):
    def setUp(self):
        self.manifest = {"ended": 1, "game_running": False, "restored": {str(i): True for i in range(8)},
                         "source_dll": "a" * 64, "source_native": "b" * 64, "bundle": "c" * 64}
        self.log = "\n".join(
            f"[gpudeco] FPS mode={'on' if i in (1, 2) else 'off'} index={i} fps={130 if i in (1, 2) else 100} valid=True spikes_30ms=0" for i in range(4))
        self.log += "\n" + "\n".join(f"[gpudeco] cost window={i} total_ms=0.49 native_errors=0 native_callbacks=100" for i in (1, 2))
        self.log += "\n" + "\n".join(f"[gpudeco] stability checkpoint={k} pass=True" for k in ("pause_resume", "song_end", "retry_restart", "reopen_toggle"))
        self.log += "\n[gpudeco] GPUDECO_G2=PASS"

    def check_issue(self, issue, log=None, manifest=None, paired=None):
        r = assess(manifest or self.manifest, log or self.log, paired or self.manifest)
        self.assertFalse(r["admissible_G2_evidence"])
        self.assertIn(issue, r["issues"])

    def test_complete_evidence(self):
        self.assertTrue(assess(self.manifest, self.log, self.manifest)["admissible_G2_evidence"])

    def test_resume_diagnostics_are_not_clean_performance(self):
        for diagnostic in ("resume_key_fixture", "resume_roundtrip records=384", "resume_handoff unobserved_order_excluded=6"):
            self.check_issue("instrumented_not_clean_G2", self.log + "\n[gpudeco] " + diagnostic)

    def test_failed_command_invalidates_trial(self):
        self.check_issue("runtime_or_test_error", self.log + "\n[자동 시험] 단계 실패 'stop'")

    def test_fault_injection_and_census_are_not_clean_performance(self):
        for diagnostic in ("recovery_audit stage=render_before", "recovery_fixture stage=preflight",
                           "recovery_force continued=False", "CPU_census running_recs=100", "activation_values channel=4",
                           "activation_fixture stage=activation_after_record", "transition_fixture kind=generation", "resume_frame_fixture kind=supported", "original_guard_trace=True", "resource_probe marked=47098", "resource_probe checked=47098", "[TileCull reload] own_transpilers=0"):
            self.check_issue("instrumented_not_clean_G2", self.log + "\n[gpudeco] " + diagnostic)

    def test_cpu_boundary(self):
        self.assertTrue(assess(self.manifest, self.log.replace("0.49", "0.5"), self.manifest)["admissible_G2_evidence"])
        self.check_issue("cpu_above_0.5ms", self.log.replace("0.49", "0.500001"))

    def test_fps_boundary(self):
        self.assertTrue(assess(self.manifest, self.log.replace("fps=130", "fps=125"), self.manifest)["admissible_G2_evidence"])
        self.check_issue("gain_below_25pct", self.log.replace("fps=130", "fps=124.999"))

    def test_fuse_not_clean(self):
        self.check_issue("instrumented_not_clean_G2", self.log + "\n[임무단계] fuse begin")

    def test_fuse_off_does_not_instrument(self):
        self.assertTrue(assess(self.manifest, self.log + "\n[임무단계] fuse off", self.manifest)["admissible_G2_evidence"])

    def test_counter_probe_not_clean(self):
        self.check_issue("instrumented_not_clean_G2", self.log + "\ncost_probe_enabled=True")

    def test_artifact_mismatch(self):
        paired = copy.deepcopy(self.manifest); paired["source_dll"] = "d" * 64
        self.check_issue("paired_artifacts_mismatch", paired=paired)

    def test_no_paired_provenance(self):
        self.assertIn("paired_pixel_artifacts_unverified", assess(self.manifest, self.log)["issues"])

    def test_failed_restore(self):
        m = copy.deepcopy(self.manifest); m["restored"]["0"] = False
        self.check_issue("incomplete_quit_or_byte_restoration", manifest=m)

    def test_raw_failure_remains_failure(self):
        self.check_issue("raw_G2_not_PASS", self.log.replace("GPUDECO_G2=PASS", "GPUDECO_G2=FPS_FAIL"))

    def test_missing_window(self):
        self.check_issue("four_windows_or_two_costs_missing", "\n".join(x for x in self.log.splitlines() if "index=3" not in x))

    def test_duplicate_window(self):
        self.check_issue("duplicate_window", self.log + "\n[gpudeco] FPS mode=off index=0 fps=100 valid=True spikes_30ms=0")

    def test_errors_are_not_success(self):
        self.check_issue("runtime_or_test_error", self.log + "\nresume_error=failed")


if __name__ == "__main__":
    unittest.main()
