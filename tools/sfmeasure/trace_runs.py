"""Owned research jobs and compact evidence; standard library only.

Used through sf_run(steps=[], trace={...}) so existing MCP tool allowlists work.
Plans are local data, never Python/shell commands. Raw charts/logs stay local.
"""
import csv
import hashlib
import io
import json
import math
import os
import re
import shutil
import subprocess
import sys
import time
import uuid
from pathlib import Path

DEFAULT_WORKSPACE = r"E:\StutterFixWork\wt-gpu"
NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,79}\Z")


def digest(data):
    return hashlib.sha256(data).hexdigest() if data is not None else None


def write_json(path, data):
    tmp = path.with_suffix(path.suffix + "." + uuid.uuid4().hex + ".tmp")
    try:
        tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        os.replace(tmp, path)
    finally:
        tmp.unlink(missing_ok=True)


def root_path(workspace):
    root = Path(workspace or os.environ.get("SF_RESEARCH_WORKSPACE", DEFAULT_WORKSPACE)).resolve()
    if not (root / "StutterFix.csproj").is_file():
        raise ValueError("StutterFix workspace not found")
    return root


def inside(root, relative):
    path = (root / relative).resolve()
    if path != root and root not in path.parents:
        raise ValueError("Path leaves the selected workspace")
    return path


def alive(pid):
    if not isinstance(pid, int) or pid <= 0:
        return False
    if os.name == "nt":
        import ctypes
        from ctypes import wintypes
        k = ctypes.WinDLL("kernel32", use_last_error=True)
        k.OpenProcess.restype = wintypes.HANDLE
        k.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
        h = k.OpenProcess(0x1000, False, pid)
        if not h:
            return ctypes.get_last_error() == 5  # access denied is not proof of exit
        try:
            code = wintypes.DWORD()
            if not k.GetExitCodeProcess(wintypes.HANDLE(h), ctypes.byref(code)):
                return True
            return code.value == 259
        finally:
            k.CloseHandle(wintypes.HANDLE(h))
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def process_lease():
    import server as api
    return Path(api.MOD_DIR) / ".sfmeasure-trace.lock"


def active_job():
    lease = process_lease()
    try:
        data = json.loads(lease.read_text(encoding="utf-8"))
        if not alive(data.get("pid")):
            data["state"] = "STALE_REQUIRES_RESTORATION_REVIEW"
        return data
    except FileNotFoundError:
        return None
    except (OSError, ValueError):
        return {"state": "UNREADABLE_LEASE_REQUIRES_REVIEW"}


def numbers(line):
    return dict(re.findall(r"([A-Za-z_][A-Za-z_0-9]*)=([^\s]+)", line))


def summarize_log(text):
    """No raw error lines or chart paths escape this parser."""
    result = {"native_errors": 0, "native_executed": 0, "gpu_owned_peak": 0,
              "portal_song_s": None, "command_errors": 0, "early_recoveries": 0,
              "resume_compared": 0, "resume_mismatch": 0, "resource_checks": 0,
              "resource_alive_max": 0, "own_transpilers_max": 0,
              "waitend_timeouts": text.count("[자동 시험] 곡이 끝나지 않음 ("),
              "observed_song_s": None,
              "autotest_started": "[자동 시험] 시작:" in text,
              "quit_observed": "[자동 시험] 끝 - 게임을 끕니다" in text}
    costs = []
    windows = []
    current_ablation = {}
    last_micro_valid = False
    for line in text.splitlines():
        if "[화면효과시각] song_s=" in line:
            song = float(numbers(line)["song_s"])
            if song == song and 0 <= song < 86400:
                result["observed_song_s"] = max(result["observed_song_s"] or 0, song)
        if "[자동 시험]" in line and any(x in line for x in ("단계 실패", "오류로 그만둠", "시간 초과")):
            result["command_errors"] += 1
        if "[곡] 평균 " in line:
            m = re.search(r"평균 ([\d.]+) FPS.*?곡 시작 연출 뒤 가장 긴 프레임 (\d+)ms, 끊김 (\d+)번", line)
            if m:
                result.update(real_fps=float(m[1]), worst_frame_ms=int(m[2]), hitches=int(m[3]))
        if "260s " in line:
            hot = re.search(r"\b260s (\d+)\(", line)
            if hot:
                result["real_fps_260_270s"] = int(hot[1])
        if "[gpudeco]" not in line and "[TileCull reload]" not in line:
            continue
        row = numbers(line)
        if "[gpudeco] on=True " in line:
            current_ablation = {}
            last_micro_valid = False
        if "[gpudeco] ablation " in line:
            current_ablation = {k: row[k] == "True" for k in
                                ("destination_copy_skipped", "replacement_root_inactive") if k in row}
            if "draw_hidden" in row:
                try:
                    current_ablation["draw_hidden"] = int(row["draw_hidden"])
                except ValueError:
                    pass
        if "[gpudeco] micro_end " in line:
            last_micro_valid = False
            try:
                w = {k: float(row[k]) for k in ("song_begin", "song_end", "elapsed_s", "real_fps")}
                w["frames"] = int(row["frames"])
                if (all(math.isfinite(w[k]) for k in ("song_begin", "song_end", "elapsed_s", "real_fps"))
                        and w["elapsed_s"] > 0 and w["frames"] > 0 and w["real_fps"] > 0
                        and w["song_end"] >= w["song_begin"]):
                    w["active"] = row.get("active") == "True"
                    if current_ablation:
                        w["ablation"] = dict(current_ablation)
                    windows.append(w)
                    windows = windows[-8:]
                    last_micro_valid = True
            except (KeyError, ValueError, OverflowError):
                pass
        if ("[gpudeco] threadcpu_row " in line and windows and last_micro_valid
                and row.get("window") == "-1" and row.get("role") in ("main", "render")):
            try:
                value = float(row["cpu_per_frame_ms"])
                if math.isfinite(value) and value >= 0:
                    windows[-1].setdefault("thread_cpu_boundary_ms", {})[row["role"]] = value
            except (KeyError, ValueError, OverflowError):
                pass
        for key in ("native_errors", "native_executed"):
            if key in row:
                result[key] = max(result[key], int(row[key]))
        if "owned" in row:
            result["gpu_owned_peak"] = max(result["gpu_owned_peak"], int(row["owned"]))
        if "on=False" in line and "reason" in row:
            reason = row["reason"]
            if reason == "GameCallback:OnLandOnPortal":
                result["portal_song_s"] = float(row["song"])
            elif ((result["gpu_owned_peak"] > 0 and result["portal_song_s"] is None)
                  or reason.startswith(("effect:", "LateUpdate", "FastMoveOriginal", "target_", "clock_"))):
                result["early_recoveries"] += 1
        if "resume_audit frames=" in line:
            result["resume_compared"] = max(result["resume_compared"], int(row.get("compared", 0)))
            result["resume_mismatch"] = max(result["resume_mismatch"], int(row.get("mismatch", 0)))
        if "resource_probe checked=" in line:
            result["resource_checks"] += 1
            result["resource_alive_max"] = max(result["resource_alive_max"], int(row.get("unity_alive", 0)))
        if "[TileCull reload]" in line:
            result["own_transpilers_max"] = max(result["own_transpilers_max"], int(row.get("own_transpilers", 0)))
        if "cost_probe enabled=" in line and "source_removals" in row:
            costs.append({key: row[key] for key in ("frames", "advance_ms", "dirty_alpha_ms", "source_upload_ms", "source_removals", "source_shifted_ints", "source_max_list")})
    if costs:
        result["cost_reports"] = costs
    if windows:
        result["micro_windows"] = windows
        result["real_fps_scope"] = "song_summary_not_micro_window"
    result["physical_display_fps"] = "UNMEASURED"
    return result


def compact(folder):
    path = folder / "manifest.json"
    if not path.is_file():
        return {"run": folder.name, "state": "NOT_RUN"}
    m = json.loads(path.read_text(encoding="utf-8-sig"))
    state = m.get("state", "ENDED" if m.get("ended") else "RUNNING")
    result = {"run": folder.name, "state": state}
    if m.get("ended") and (folder / "Player.log").is_file():
        result.update(summarize_log((folder / "Player.log").read_text(encoding="utf-8-sig", errors="replace")))
    result.update(restored=sum(m.get("restored", {}).values()), restore_total=len(m.get("restored", {})),
                  game_running=m.get("game_running"), errors=m.get("errors", []),
                  checks=m.get("checks", {}), contract="declared_checks_and_owned_exit_restoration_only")
    if (folder / "annotations.json").is_file():
        result["annotations"] = json.loads((folder / "annotations.json").read_text(encoding="utf-8"))
    return result


def plan_data(root, filename):
    path = inside(root, filename)
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data.get("steps"), list) or not data["steps"] or len(data["steps"]) > 512:
        raise ValueError("Expected 1..512 autotest steps")
    if any(not isinstance(s, str) or "\n" in s or "\r" in s for s in data["steps"]):
        raise ValueError("Each step must be one command line")
    if not 0 < data.get("timeout_min", 20) <= 30:
        raise ValueError("timeout_min must be <=30")
    dll = inside(root, data.get("source_dll", "bin/PlayerAuto/StutterFix.dll"))
    native = inside(root, data.get("native_dll", "native/sfnative.dll"))
    bundle = inside(root, "tools/gpudeco-lab/evidence/bundle/stutterfix_gpudeco")
    stamps = {"source_dll": digest(dll.read_bytes()), "source_native": digest(native.read_bytes()), "bundle": digest(bundle.read_bytes())}
    semantic = {k: data.get(k) for k in ("steps", "settings", "checks")}
    fingerprint = digest(json.dumps([semantic, stamps], sort_keys=True).encode("utf-8"))
    return data, dll, native, stamps, fingerprint


def start(root, plan, name, repeat_reason=""):
    import server as api
    if not NAME.fullmatch(name):
        raise ValueError("run must be a short file name")
    if api.game_running() or active_job():
        raise RuntimeError("Existing game or owned trial: not launching or installing")
    data, _, _, stamps, fingerprint = plan_data(root, plan)
    out = root / "tools/optimization/out"
    out.mkdir(parents=True, exist_ok=True)
    for old in out.glob("*/manifest.json"):
        try:
            m = json.loads(old.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            continue
        if m.get("fingerprint") == fingerprint and not repeat_reason.strip():
            return {"state": "DUPLICATE", "existing_run": old.parent.name, "result": compact(old.parent), "requires": "repeat_reason and a new run name"}
    folder = out / name
    folder.mkdir(exist_ok=False)
    write_json(folder / "plan.json", data)
    m = dict(stamps, started=time.time(), fingerprint=fingerprint, repeat_reason=repeat_reason,
             checks=data.get("checks", {}), state="QUEUED", nonce=uuid.uuid4().hex)
    write_json(folder / "manifest.json", m)
    # Reserve before spawning: legacy calls cannot race a queued worker's install.
    lease = process_lease()
    try:
        with lease.open("x", encoding="utf-8") as f:
            json.dump({"pid": os.getpid(), "workspace": str(root), "run": name, "nonce": m["nonce"]}, f)
    except OSError:
        m.update(state="FAILED", errors=["lease_reservation_failed"], ended=time.time())
        write_json(folder / "manifest.json", m)
        raise
    try:
        with (folder / "worker.log").open("wb") as log:
            child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "--worker", str(root), name],
                                     stdout=log, stderr=subprocess.STDOUT,
                                     creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    except Exception:
        lease.unlink()
        m.update(state="FAILED", errors=["worker_spawn_failed"], ended=time.time())
        write_json(folder / "manifest.json", m)
        raise
    return {"run": name, "state": "QUEUED", "worker_pid": child.pid, "status": "sf_run steps=[] trace={op:status,run:" + name + "}"}


def dispatch(args):
    root = root_path(args.get("workspace", ""))
    out = root / "tools/optimization/out"
    op = args.get("op", "status")
    if op == "start":
        result = start(root, args["plan"], args["run"], args.get("repeat_reason", ""))
    elif op == "status":
        if args.get("run"):
            if not NAME.fullmatch(args["run"]):
                raise ValueError("Invalid run name")
            result = compact(out / args["run"])
        else:
            paths = sorted(out.glob("*/manifest.json"), key=lambda p: p.stat().st_mtime, reverse=True)[:5]
            result = {"runs": [compact(p.parent) for p in paths]}
    elif op == "catalog":
        paths = sorted(out.glob("*-steps.json"), key=lambda p: p.stat().st_mtime, reverse=True)[:12]
        result = {"plans": [str(p.relative_to(root)) for p in paths], "execution": "start requires explicit plan and a fresh run name"}
    elif op == "note":
        if not NAME.fullmatch(args.get("run", "")) or not 0 <= float(args["song_s"]) < 86400:
            raise ValueError("Invalid run/song time")
        path = out / args["run"] / "annotations.json"
        if not path.parent.is_dir():
            raise ValueError("Run not found")
        rows = json.loads(path.read_text(encoding="utf-8")) if path.is_file() else []
        rows.append({"song_s": float(args["song_s"]), "note": str(args["note"])[:160], "raw_log_modified": False})
        write_json(path, rows)
        result = {"run": args["run"], "annotations": len(rows), "raw_log_modified": False}
    else:
        raise ValueError("trace op: catalog|start|status|note")
    return json.dumps(result, ensure_ascii=False, separators=(",", ":"))


def run_worker(root, name, api):
    """Same owned/normal-quit/byte-restore policy as the proven round runner."""
    folder = root / "tools/optimization/out" / name
    meta = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    lease = process_lease()
    lease_owned = False
    before = {}
    owned_pid = None
    handle = None
    old_launch, old_kill = api.launch_game, api.kill_game
    errors = []

    def game_pid():
        raw = subprocess.run(["tasklist", "/FI", "IMAGENAME eq " + api.EXE, "/NH", "/FO", "CSV"], capture_output=True, text=True).stdout
        return next((int(row[1]) for row in csv.reader(io.StringIO(raw)) if row and row[0].lower() == api.EXE.lower()), None)

    def normal_quit():
        if owned_pid is None or game_pid() != owned_pid:
            return
        # WM_CLOSE requests the normal Unity exit path, never TerminateProcess.
        import ctypes
        from ctypes import wintypes
        u = ctypes.WinDLL("user32", use_last_error=True)
        cbtype = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        u.GetWindowThreadProcessId.argtypes = (wintypes.HWND, ctypes.POINTER(wintypes.DWORD))
        u.PostMessageW.argtypes = (wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM)
        def close(hwnd, _):
            pid = wintypes.DWORD()
            u.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            if pid.value == owned_pid:
                u.PostMessageW(hwnd, 0x10, 0, 0)
            return True
        u.EnumWindows(cbtype(close), 0)
        until = time.monotonic() + 20
        while game_pid() == owned_pid and time.monotonic() < until:
            time.sleep(.5)

    def launch():
        nonlocal owned_pid, handle
        if api.game_running():
            raise RuntimeError("Game appeared before launch; ownership unknown")
        steam = Path(os.environ.get("SF_STEAM_EXE", r"C:\Program Files (x86)\Steam\steam.exe"))
        subprocess.Popen([str(steam), "-applaunch", "977950"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                         creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        until = time.monotonic() + 150
        while time.monotonic() < until:
            owned_pid = game_pid()
            if owned_pid:
                import ctypes
                from ctypes import wintypes
                k = ctypes.WinDLL("kernel32", use_last_error=True)
                k.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
                k.OpenProcess.restype = wintypes.HANDLE
                handle = k.OpenProcess(0x1000, False, owned_pid)
                meta["owned_pid"] = owned_pid
                write_json(folder / "manifest.json", meta)
                return
            time.sleep(1)
        raise RuntimeError("Single Steam launch timed out; no retry")

    try:
        # Claim exactly the parent's reservation; never delete another job's lease.
        reserved = json.loads(lease.read_text(encoding="utf-8"))
        if reserved.get("nonce") != meta["nonce"] or reserved.get("run") != name:
            raise RuntimeError("Lease reservation mismatch")
        write_json(lease, {"pid": os.getpid(), "workspace": str(root), "run": name, "nonce": meta["nonce"]})
        lease_owned = True
        if api.game_running():
            raise RuntimeError("Existing unowned game")
        data, dll, native, stamps, fingerprint = plan_data(root, "tools/optimization/out/" + name + "/plan.json")
        if fingerprint != meta["fingerprint"]:
            raise RuntimeError("Plan or binary changed after queuing")
        mod = Path(api.MOD_DIR)
        game = Path(api.GAME_DIR)
        files = [mod / "StutterFix.dll", mod / "sfnative.dll", mod / "Settings.xml",
                 game / "A Dance of Fire and Ice_Data/boot.config", game / "A Dance of Fire and Ice_Data/boot.config.stutterfix.bak",
                 mod / "autotest.txt", mod / "autotest.end", mod / "autotest.done", mod / "gpudeco-resource-probe.bin"]
        before = {p: p.read_bytes() if p.is_file() else None for p in files}
        for i, p in enumerate(files):
            if before[p] is not None:
                (folder / ("original-" + str(i) + ".bin")).write_bytes(before[p])
        meta.update(before={str(p): digest(b) for p, b in before.items()}, state="RUNNING")
        write_json(folder / "manifest.json", meta)
        shutil.copy2(dll, mod / "StutterFix.dll")
        shutil.copy2(native, mod / "sfnative.dll")
        if digest((mod / "StutterFix.dll").read_bytes()) != stamps["source_dll"] or digest((mod / "sfnative.dll").read_bytes()) != stamps["source_native"]:
            raise RuntimeError("Installed SHA mismatch")
        api.launch_game, api.kill_game = launch, normal_quit
        api.sf_run(steps=data["steps"], settings=data.get("settings", {}), timeout_min=data.get("timeout_min", 20), tag=name)
    except Exception as ex:
        errors.append(type(ex).__name__)  # no raw private paths in compact output
        meta["error_detail"] = str(ex)
    finally:
        try:
            if owned_pid and api.game_running():
                normal_quit()
        except Exception as ex:
            errors.append("normal_quit_" + type(ex).__name__)
        api.launch_game, api.kill_game = old_launch, old_kill
        try:
            meta["game_running"] = api.game_running()
        except Exception:
            meta["game_running"] = True  # Unknown process state must not allow DLL replacement.
            errors.append("game_exit_check_failed")
        try:
            if Path(api.PLAYER_LOG).is_file() and owned_pid:
                shutil.copy2(api.PLAYER_LOG, folder / "Player.log")
        except OSError:
            errors.append("log_copy_failed")  # Evidence failure must not bypass original-byte restoration.
        if not meta["game_running"]:
            for p, b in before.items():
                try:
                    if b is None:
                        p.unlink(missing_ok=True)
                    else:
                        p.write_bytes(b)
                except OSError:
                    errors.append("restore_write_failed")
            meta["restored"] = {}
            for p, b in before.items():
                try:
                    meta["restored"][str(p)] = (p.read_bytes() if p.is_file() else None) == b
                except OSError:
                    meta["restored"][str(p)] = False
        if handle:
            import ctypes
            from ctypes import wintypes
            k = ctypes.WinDLL("kernel32", use_last_error=True)
            code = wintypes.DWORD()
            if k.GetExitCodeProcess(wintypes.HANDLE(handle), ctypes.byref(code)):
                meta["exit_code"] = code.value
            k.CloseHandle(wintypes.HANDLE(handle))
        try:
            text = (folder / "Player.log").read_text(encoding="utf-8", errors="replace") if (folder / "Player.log").is_file() else ""
            result = summarize_log(text)
        except (OSError, ValueError, KeyError):
            result = summarize_log("")
            errors.append("log_summary_failed")
        checks = meta.get("checks", {})
        if not result["autotest_started"] or not result["quit_observed"]:
            errors.append("autotest_start_or_normal_quit_not_observed")
        if result["command_errors"]:
            errors.append("autotest_command_error")
        if result["waitend_timeouts"]:
            errors.append("song_end_timeout")
        if result["native_errors"]:
            errors.append("native_error")
        if meta.get("exit_code") != 0:
            errors.append("abnormal_or_unmeasured_exit")
        if len(meta.get("restored", {})) != 9 or not all(meta["restored"].values()):
            errors.append("restoration_incomplete")
        if checks.get("portal_min_s") is not None and (result["portal_song_s"] is None or result["portal_song_s"] < checks["portal_min_s"]):
            errors.append("portal_missing_or_early")
        if checks.get("song_min_s") is not None and (result["observed_song_s"] is None or result["observed_song_s"] < checks["song_min_s"]):
            errors.append("song_clock_missing_or_early")
        if checks.get("gpu_until_portal") and (result["early_recoveries"] or result["native_executed"] == 0 or result["gpu_owned_peak"] == 0):
            errors.append("GPU_continuity_not_proved")
        if lease_owned and (not before or (not meta["game_running"] and len(meta.get("restored", {})) == 9 and all(meta["restored"].values()))):
            try:
                lease.unlink()
            except OSError:
                errors.append("lease_release_failed")
        meta.update(errors=sorted(set(errors)), ended=time.time(), state="RESTORE_DEFERRED" if meta["game_running"] else "FAILED" if errors else "PASSED")
        write_json(folder / "manifest.json", meta)


if __name__ == "__main__":
    if len(sys.argv) != 4 or sys.argv[1] != "--worker":
        raise SystemExit("Worker entry only")
    import server as api
    run_worker(root_path(sys.argv[2]), sys.argv[3], api)
