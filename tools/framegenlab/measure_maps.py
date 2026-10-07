"""Stage-0 runner: fresh game per run, restore installed DLL and temporary settings even on failure."""
import argparse
import ctypes
import importlib.util
import json
import os
import re
import statistics
import subprocess
import threading
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("sfmeasure", ROOT/"tools"/"sfmeasure"/"server.py")
sf = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sf)


def classify(name):
    if name.endswith("/outside"):
        return "outside"
    if any(v in name for v in ("ScriptRunBehaviourUpdate", "ScriptRunBehaviourLateUpdate", "ScriptRunBehaviourFixedUpdate")):
        return "script_stages"
    if "Wait" in name or "Present" in name or "FrameRate" in name:
        return "wait"
    if any(v in name for v in ("Render", "Canvas", "RectTransform", "SkinnedMesh")):
        return "render_stages"
    return "other_engine"


def parse_log(text):
    heads = re.findall(r"\[프레임생성 단계\] frames=(\d+) mean_ms=([\d.]+) excluded=(\d+) excluded_ms=([\d.]+)", text)
    if not heads:
        raise RuntimeError("No measurement-only stage summary; check installed build and completed song")
    count, mean, excluded, excluded_ms = heads[-1]
    tail = text[text.rfind("[프레임생성 단계] frames="):]
    stages = {k: float(v) for k, v in re.findall(r"\[프레임생성 단계\] ([^=\r\n]+)=([\d.]+)", tail) if "/" in k}
    groups = dict.fromkeys(("script_stages", "render_stages", "wait", "other_engine", "outside"), 0.0)
    for name, value in stages.items():
        groups[classify(name)] += value
    mean = float(mean)
    coverage = sum(groups.values())/mean
    if abs(coverage-1)>0.01:
        raise RuntimeError(f"Stage coverage {coverage:.4f}; do not infer bottleneck from missing phases")
    return {"frames": int(count), "mean_ms": mean, "fps": 1000/mean,
        "excluded_boundary_frames": int(excluded), "excluded_boundary_ms": float(excluded_ms),
        "coverage": coverage, "groups_ms": groups, "groups_percent": {k: v/mean*100 for k,v in groups.items()}, "stages_ms": stages}


def cpu_load(mask, duty, priority, thread_priority, lifetime):
    k = ctypes.WinDLL("kernel32", use_last_error=True)
    k.GetCurrentProcess.restype = ctypes.c_void_p
    k.SetProcessAffinityMask.argtypes = [ctypes.c_void_p, ctypes.c_size_t]
    k.SetPriorityClass.argtypes = [ctypes.c_void_p, ctypes.c_uint32]
    k.GetCurrentThread.restype = ctypes.c_void_p
    k.SetThreadPriority.argtypes = [ctypes.c_void_p,ctypes.c_int]
    if not k.SetProcessAffinityMask(k.GetCurrentProcess(),mask):
        raise ctypes.WinError(ctypes.get_last_error())
    if not k.SetPriorityClass(k.GetCurrentProcess(),priority):
        raise ctypes.WinError(ctypes.get_last_error())
    if not k.SetThreadPriority(k.GetCurrentThread(),thread_priority):
        raise ctypes.WinError(ctypes.get_last_error())
    deadline = time.monotonic()+lifetime
    while time.monotonic()<deadline:
        start = time.perf_counter()
        while time.perf_counter()-start < duty*.01:
            pass
        time.sleep(max(0, .01-(time.perf_counter()-start)))


class Constraint:
    def __init__(self, mask, duty, workers=1, thread_priority=0, seconds=60):
        self.mask, self.duty, self.workers = mask, duty, workers
        self.thread_priority, self.seconds = thread_priority, seconds
        self.stop = threading.Event()
        self.hogs = []; self.thread = None
        self.error = None
        self.applied = False

    def start(self):
        started_at = time.time()
        def monitor():
            k = ctypes.WinDLL("kernel32", use_last_error=True)
            k.OpenProcess.restype = ctypes.c_void_p
            k.OpenProcess.argtypes = [ctypes.c_uint32, ctypes.c_int, ctypes.c_uint32]
            k.SetProcessAffinityMask.argtypes = [ctypes.c_void_p, ctypes.c_size_t]
            k.GetProcessAffinityMask.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_size_t), ctypes.POINTER(ctypes.c_size_t)]
            k.GetPriorityClass.argtypes = [ctypes.c_void_p]
            k.GetPriorityClass.restype = ctypes.c_uint32
            k.CloseHandle.argtypes = [ctypes.c_void_p]
            try:
                while not self.stop.wait(.5):
                    # Loading is excluded: constrain only after this run has actually pressed Play.
                    try:
                        if Path(sf.PLAYER_LOG).stat().st_mtime < started_at or "[자동 시험] 키 누름" not in sf.read_text(sf.PLAYER_LOG):
                            continue
                    except OSError:
                        continue
                    raw = subprocess.run(["tasklist", "/FI", "IMAGENAME eq "+sf.EXE, "/NH", "/FO", "CSV"], capture_output=True, text=True).stdout
                    match = re.search(r'"'+re.escape(sf.EXE)+r'","(\d+)"',raw,re.I)
                    if not match:
                        continue
                    handle = k.OpenProcess(0x0200|0x0400,False,int(match[1]))
                    if not handle:
                        raise ctypes.WinError(ctypes.get_last_error())
                    old = ctypes.c_size_t(); system = ctypes.c_size_t()
                    try:
                        if not k.GetProcessAffinityMask(handle,ctypes.byref(old),ctypes.byref(system)):
                            raise ctypes.WinError(ctypes.get_last_error())
                        if not k.SetProcessAffinityMask(handle,self.mask):
                            raise ctypes.WinError(ctypes.get_last_error())
                        priority = k.GetPriorityClass(handle)
                        if not priority:
                            raise ctypes.WinError(ctypes.get_last_error())
                        # Match this game's existing priority; do not change it or other apps.
                        self.applied = True
                        print(f"Constraint: pid={match[1]} affinity={self.mask:#x} busy duty={self.duty:.2f} workers={self.workers} game/worker process priority={priority:#x} worker thread priority={self.thread_priority}",flush=True)
                        if self.duty:
                            masks = [1<<i for i in range(self.mask.bit_length()) if self.mask & (1<<i)]
                            for mask in masks[:self.workers]:
                                self.hogs.append(subprocess.Popen([os.sys.executable,str(Path(__file__)),"--cpu-worker",str(mask),str(self.duty),str(priority),str(self.thread_priority),str(self.seconds+30)], creationflags=subprocess.CREATE_NO_WINDOW))
                        while not self.stop.wait(.5):
                            if any(hog.poll() is not None for hog in self.hogs):
                                raise RuntimeError("CPU worker exited early; reject constrained run")
                    finally:
                        # The normal runner quits this game. Restore affinity if it is still alive.
                        if old.value:
                            k.SetProcessAffinityMask(handle,old.value)
                        k.CloseHandle(handle)
                    break
            except Exception as e:
                self.error = str(e)
        self.thread = threading.Thread(target=monitor,daemon=True)
        self.thread.start()

    def close(self):
        self.stop.set()
        if self.thread:
            self.thread.join(timeout=5)
        for hog in self.hogs:
            hog.terminate(); hog.wait(timeout=5)
        if self.error:
            raise RuntimeError(self.error)
        if not self.applied:
            raise RuntimeError("Requested CPU constraint never applied; reject run")


def main():
    if len(os.sys.argv)>1 and os.sys.argv[1] == "--cpu-worker":
        cpu_load(int(os.sys.argv[2]),float(os.sys.argv[3]),int(os.sys.argv[4]),int(os.sys.argv[5]),float(os.sys.argv[6])); return
    ap = argparse.ArgumentParser()
    ap.add_argument("--map", type=Path, required=True)
    ap.add_argument("--label", required=True, help="Public label only; private map paths never enter committed summaries")
    ap.add_argument("--seconds", type=int, default=60)
    ap.add_argument("--seek", type=float, default=0)
    ap.add_argument("--affinity", type=lambda s: int(s,0), default=0)
    ap.add_argument("--cpu-duty", type=float, default=0)
    ap.add_argument("--cpu-workers", type=int, default=1)
    ap.add_argument("--cpu-thread-priority", type=int, choices=[0,1,2], default=0, help="Worker thread priority: normal/above normal/highest within the game's existing process class")
    ap.add_argument("--plain", action="store_true", help="Two frame-boundary markers instead of all stages, for overhead comparison")
    ap.add_argument("--out", type=Path, required=True)
    args = ap.parse_args()
    if not args.map.is_file() or not 5<args.seconds<=600 or args.seek<0 or (args.seek and args.affinity) or not 0<=args.cpu_duty<1 or (args.cpu_duty and not args.affinity) or args.cpu_workers<1 or (args.cpu_duty and args.cpu_workers>args.affinity.bit_count()):
        ap.error("Existing map and valid affinity/duty required")
    if sf.game_running():
        raise RuntimeError("Game already running; close normally before measurement")
    mod_dll = Path(sf.MOD_DIR)/"StutterFix.dll"
    original = mod_dll.read_bytes()
    measured = ROOT/"tools"/"framegenlab"/"out"/"measure"/"StutterFix.dll"
    baseline_flag = Path(sf.MOD_DIR)/"framegen-probe-baseline.txt"
    old_flag = baseline_flag.read_bytes() if baseline_flag.exists() else None
    constraint = Constraint(args.affinity,args.cpu_duty,args.cpu_workers,args.cpu_thread_priority,args.seconds) if args.affinity else None
    args.out.mkdir(parents=True,exist_ok=False)
    (args.out/"installed-original.dll").write_bytes(original)
    (args.out/"Settings.xml.original").write_bytes((Path(sf.MOD_DIR)/"Settings.xml").read_bytes())
    try:
        mod_dll.write_bytes(measured.read_bytes())
        if args.plain:
            baseline_flag.write_text("two-boundary reference",encoding="utf-8")
        elif baseline_flag.exists():
            baseline_flag.unlink()
        if constraint:
            constraint.start()
        steps = [("open " if args.seek else "game ")+str(args.map),"auto on"]
        if args.seek:
            steps += ["seek "+str(args.seek)]
        else:
            steps += ["press"]
        steps += ["wait "+str(args.seconds), "quit"]
        summary, metrics = sf.sf_run(steps, settings={"FrameStats": False}, tag="framegen-"+args.label, timeout_min=10)
        if constraint:
            constraint.close(); constraint = None
        log = sf.read_text(sf.PLAYER_LOG)
        (args.out/"game.log").write_text(log,encoding="utf-8")
        (args.out/"run.txt").write_text(summary,encoding="utf-8")
        if "시간 초과로 끔" in summary or "단계 실패" in log or "[안정성] 안전 모드로 켬" in log:
            raise RuntimeError("Timeout, failed step, or safety mode; reject this run")
        metrics["errors"] = sum(bool(sf.RE_ERR.search(l)) for l in log.splitlines() if "[프레임생성 단계]" not in l)
        result = {"label": args.label, "seconds": args.seconds, "seek": args.seek,
            "affinity": args.affinity, "cpu_duty": args.cpu_duty, "cpu_workers": args.cpu_workers, "cpu_thread_priority": args.cpu_thread_priority, "plain": args.plain, "metrics": metrics,
            "screen_log": re.findall(r"화면: 수직동기[^\r\n]+",log)[-1:],
            "stage": parse_log(log)}
        if not result["stage"]:
            raise RuntimeError("No complete playable frame summary; reject run")
        (args.out/"summary.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
        print(json.dumps({k:v for k,v in result.items() if k!="stage"},ensure_ascii=False),flush=True)
        if result["stage"]:
            s = result["stage"]
            print(f"Stage FPS={s['fps']:.1f}, mean={s['mean_ms']:.3f}ms, groups={s['groups_percent']}, coverage={s['coverage']:.6f}",flush=True)
    finally:
        try:
            if constraint:
                constraint.close()
            if sf.game_running():
                sf.sf_quit()
        finally:
            mod_dll.write_bytes(original)
            if old_flag is not None:
                baseline_flag.write_bytes(old_flag)
            elif baseline_flag.exists():
                baseline_flag.unlink()
            print("Restored installed DLL",flush=True)


if __name__ == "__main__":
    main()
