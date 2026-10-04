#!/usr/bin/env python3
"""StutterFix 측정 MCP 서버 (PC 전용, 파이썬 표준 라이브러리만).

Claude 가 게임을 직접 켜서 같은 구간을 여러 번 돌리고(AutoTest), 결과를 짧게 받아 보게 한다.
사람이 "됐어"라고 하기를 기다리지 않고 A/B 를 여러 번 돌릴 수 있어 측정 횟수가 늘고, 로그 전체 대신 요약만 받아 토큰도 준다.

도구
  sf_status     게임이 켜져 있는지, 설치된 DLL 이 자동 시험 빌드인지, boot.config 상태
  sf_run        autotest.txt 를 써 두고 게임을 켜서 끝날 때까지 기다린 뒤 결과 요약 (설정 덮어쓰기는 끝나면 되돌림)
  sf_ab         설정 하나를 A/B 로 바꿔 가며 sf_run 을 ABBA 순서로 되풀이하고 비교표
  sf_live       켜 둔 게임에 명령 묶음을 넣고(없으면 켬) 끝날 때까지 기다린 뒤 그 묶음의 로그만 요약 (게임은 계속 켜 둠)
  sf_ab_live    같은 게임 안에서 A/B: 첫 판 버림, ABBA, 실패하면 게임을 새로 켜서 그 판만 다시
  sf_quit       켜 둔 게임 끄기
  sf_log        Player.log(또는 저장한 판 로그)의 최근 판 요약 (.claude/skills/log 와 같은 내용)
  sf_presentmon 켜져 있는 게임을 PresentMon 으로 N초 재서 요약 (관리자 또는 Performance Log Users 필요)

경로는 환경 변수로 바꿀 수 있다: SF_GAME_DIR, SF_PLAYER_LOG, SF_PRESENTMON, SF_RUNS_DIR.
자동 시험 명령은 AutoTest.cs 머리 주석을 본다. 자동 시험은 개발자용 빌드나 -p:AutoTestBuild=1 빌드에만 들어 있다.
"""
import csv
import json
import os
import re
import shutil
import statistics
import subprocess
import sys
import threading
import time

EXE = "A Dance of Fire and Ice.exe"
STEAM_URL = "steam://rungameid/977950"
GAME_DIR = os.environ.get("SF_GAME_DIR", r"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice")
MOD_DIR = os.path.join(GAME_DIR, "Mods", "StutterFix")
PLAYER_LOG = os.environ.get("SF_PLAYER_LOG", os.path.join(
    os.environ.get("USERPROFILE", os.path.expanduser("~")), "AppData", "LocalLow", "7th Beat Games", "A Dance of Fire and Ice", "Player.log"))
PRESENTMON = os.environ.get("SF_PRESENTMON", "")
RUNS_DIR = os.environ.get("SF_RUNS_DIR", os.path.join(os.environ.get("LOCALAPPDATA", os.path.expanduser("~")), "StutterFix", "runs"))
IS_WIN = os.name == "nt"
OUT_LOCK = threading.Lock()   # 도구 호출은 따로 도는 스레드에서 답한다
RUN_LOCK = threading.Lock()   # 게임은 한 번에 하나만


# ── 게임 프로세스 ──
def game_running():
    if not IS_WIN:
        return False
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq " + EXE, "/NH", "/FO", "CSV"], capture_output=True, text=True, errors="replace").stdout
    return EXE.lower() in out.lower()


def kill_game():
    if IS_WIN:
        subprocess.run(["taskkill", "/IM", EXE, "/F"], capture_output=True)


def launch_game():
    os.startfile(STEAM_URL)  # Steam 실행 옵션도 사용자가 평소 켜는 것과 같게 적용된다


def dll_has_autotest(path):
    try:
        with open(path, "rb") as f:
            return "autotest.txt".encode("utf-16-le") in f.read()   # .NET 문자열 상수는 UTF-16 으로 들어 있다
    except OSError:
        return False


# ── 로그 읽기 ──
def read_text(path):
    with open(path, "rb") as f:
        return f.read().decode("utf-8", errors="replace")


RE_RUN = re.compile(r"판 #(\d+) \(([^)]*)\): 평균 ([\d.]+) FPS, 화면 대기 ([\d.]+)ms")
RE_SONG = re.compile(r"\[곡\] 평균 ([\d.]+) FPS.*?곡 시작 연출 뒤 가장 긴 프레임 (\d+)ms, 끊김 (\d+)번")   # 플레이어용에도 있음
RE_HITCH = re.compile(r"\[끊김\] ([\d.]+)초 중 (\d+)회 끊김, 합계 ([\d.]+)ms, 최악 ([\d.]+)ms")
RE_FRAME = re.compile(r"프레임 ([\d.]+)")
RE_ERR = re.compile(r"\[StutterFix\].*(Error|\[Error\]|설치 실패|patch failed|Exception)|단계 실패|시간 초과")


def summarize(text, runs=1, max_frames=15):
    lines = text.splitlines()
    out = []
    errs = [l for l in lines if RE_ERR.search(l)]
    out.append("=== 설치 상태 ===")
    out += (errs[-10:] or ["오류 없음"])
    auto = [l.split("[자동 시험] ", 1)[1] for l in lines if "[자동 시험] " in l]
    if auto:
        out.append("\n=== 자동 시험 ===")
        out += auto[-40:]
    starts = [i for i, l in enumerate(lines) if "[끊김] 기록 시작" in l]
    if not starts:
        out.append("\n플레이 기록 없음")
        return "\n".join(out)
    sec = lines[starts[-runs] if len(starts) >= runs else starts[0]:]
    out.append("\n=== 요약 (최근 %d판) ===" % min(runs, len(starts)))
    out += [l for l in sec if RE_HITCH.search(l) or "심한 순서" in l or "GC 재개" in l or "모드별 할당" in l][-8:]
    out += [l for l in sec if "[곡]" in l][-4:]
    out.append("\n=== 30ms 넘은 프레임 (최대 %d건) ===" % max_frames)
    n, show = 0, False
    for l in sec:
        if "[끊김] 타일" in l:
            m = RE_FRAME.search(l.split("[끊김] 타일", 1)[1])
            show = bool(m) and float(m.group(1)) >= 30
            if show:
                n += 1
                if n > max_frames:
                    break
                out.append("")
                out.append(l)
        elif show and "[끊김]    " in l:
            out.append(l)
    return "\n".join(out)


def metrics(text):
    """판 하나의 숫자: 판별 FPS/화면 대기, 끊김 횟수·최악."""
    runs = [{"no": int(m.group(1)), "kind": m.group(2), "fps": float(m.group(3)), "wait": float(m.group(4))} for m in RE_RUN.finditer(text)]
    hitches = [{"sec": float(m.group(1)), "count": int(m.group(2)), "total": float(m.group(3)), "worst": float(m.group(4))} for m in RE_HITCH.finditer(text)]
    songs = [{"fps": float(m.group(1)), "worst": float(m.group(2)), "count": int(m.group(3))} for m in RE_SONG.finditer(text)]
    return {"runs": runs, "hitches": hitches, "songs": songs, "errors": len([l for l in text.splitlines() if RE_ERR.search(l)])}


# ── 설정 덮어쓰기 (UMM 의 Settings.xml, 필드 이름 = 요소 이름) ──
def apply_settings(overrides):
    path = os.path.join(MOD_DIR, "Settings.xml")
    if not overrides:
        return None, []
    if not os.path.exists(path):
        raise RuntimeError("Settings.xml 없음: 게임을 한 번 켜서 모드 설정을 저장해야 한다")
    backup = path + ".sfmeasure.bak"
    shutil.copy2(path, backup)
    xml = read_text(path)
    changed = []
    for k, v in overrides.items():
        v = str(v).lower() if isinstance(v, bool) else str(v)
        pat = re.compile(r"<%s>[^<]*</%s>" % (re.escape(k), re.escape(k)))
        if not pat.search(xml):
            # 새로 넣은 설정은 게임이 아직 저장하지 않아 없다: 끝에 넣는다 (XmlSerializer 는 순서를 보지 않는다)
            if "</Settings>" not in xml:
                restore_settings(backup)
                raise RuntimeError("설정 이름 없음: %s (StutterFix.cs 의 Settings 필드 이름을 쓴다)" % k)
            xml = xml.replace("</Settings>", "  <%s>%s</%s>\n</Settings>" % (k, v, k), 1)
            changed.append("%s=%s(새로 넣음)" % (k, v))
            continue
        xml = pat.sub("<%s>%s</%s>" % (k, v, k), xml, count=1)
        changed.append("%s=%s" % (k, v))
    with open(path, "wb") as f:
        f.write(xml.encode("utf-8"))
    return backup, changed


def restore_settings(backup):
    if backup and os.path.exists(backup):
        shutil.copy2(backup, backup[: -len(".sfmeasure.bak")])
        os.remove(backup)


# ── PresentMon ──
COLS = {
    "frame": ["MsBetweenPresents", "FrameTime", "MsBetweenAppStart"],
    "gpu": ["MsGPUBusy", "GPUBusy"],
    "disp": ["MsUntilDisplayed", "DisplayLatency"],
    "mode": ["PresentMode"],
    "tear": ["AllowsTearing"],
    "sync": ["SyncInterval"],
}


def pick(row, names):
    for n in names:
        if n in row and row[n] not in ("", "NA"):
            return row[n]
    return None


def summarize_presentmon(csv_path):
    with open(csv_path, newline="", encoding="utf-8", errors="replace") as f:
        rows = [r for r in csv.DictReader(f) if (r.get("Application") or r.get("ProcessName") or EXE).lower() == EXE.lower()]
    ft = [float(v) for v in (pick(r, COLS["frame"]) for r in rows) if v]
    if not ft:
        return "PresentMon 결과에 게임 프레임이 없음 (게임이 앞에 있었는지, 권한이 있는지 확인)"
    srt = sorted(ft)
    p99 = srt[int(len(srt) * 0.99) - 1] if len(srt) > 100 else srt[-1]
    low1 = statistics.mean(srt[int(len(srt) * 0.99):]) if len(srt) > 100 else srt[-1]
    out = ["프레임 %d개, 평균 %.1f FPS, 1%% 최저 %.1f FPS, 99%% %.2fms, 최악 %.1fms, 16.7ms 넘음 %d, 33ms 넘음 %d" % (
        len(ft), 1000 / statistics.mean(ft), 1000 / low1, p99, srt[-1], sum(x > 16.7 for x in ft), sum(x > 33.3 for x in ft))]
    gpu = [float(v) for v in (pick(r, COLS["gpu"]) for r in rows) if v]
    disp = [float(v) for v in (pick(r, COLS["disp"]) for r in rows) if v]
    if gpu:
        out.append("GPU 바쁨 평균 %.2fms" % statistics.mean(gpu))
    if disp:
        out.append("화면에 나오기까지 평균 %.2fms" % statistics.mean(disp))
    for key, label in (("mode", "출력 방식"), ("tear", "찢어짐 허용"), ("sync", "수직동기 간격")):
        cnt = {}
        for r in rows:
            v = pick(r, COLS[key])
            if v is not None:
                cnt[v] = cnt.get(v, 0) + 1
        if cnt:
            out.append(label + ": " + ", ".join("%s %d" % kv for kv in sorted(cnt.items(), key=lambda kv: -kv[1])))
    return "\n".join(out)


def run_presentmon(seconds, csv_path):
    if not PRESENTMON or not os.path.exists(PRESENTMON):
        raise RuntimeError("SF_PRESENTMON 환경 변수에 PresentMon.exe 경로를 넣어야 한다")
    args = [PRESENTMON, "--process_name", EXE, "--output_file", csv_path, "--timed", str(int(seconds)),
            "--terminate_after_timed", "--stop_existing_session"]
    p = subprocess.run(args, capture_output=True, text=True, errors="replace", timeout=seconds + 60)
    if not os.path.exists(csv_path):
        raise RuntimeError("PresentMon 실패 (관리자 권한?): " + (p.stderr or p.stdout)[-400:])
    return summarize_presentmon(csv_path)


# ── 한 판 돌리기 ──
def sf_run(steps, settings=None, timeout_min=15, tag="", presentmon=None):
    if not RUN_LOCK.acquire(blocking=False):
        raise RuntimeError("다른 측정이 돌고 있다")
    try:
        return _run(steps, settings, timeout_min, tag, presentmon)
    finally:
        RUN_LOCK.release()


def _run(steps, settings, timeout_min, tag, presentmon):
    if not IS_WIN:
        raise RuntimeError("PC(윈도우)에서만 돈다")
    if game_running():
        raise RuntimeError("게임이 켜져 있다. 끄고 다시 (자동 시험은 켤 때 autotest.txt 를 읽는다)")
    dll = os.path.join(MOD_DIR, "StutterFix.dll")
    if not dll_has_autotest(dll):
        raise RuntimeError("설치된 DLL 에 자동 시험이 없다 (개발자용 빌드나 -p:AutoTestBuild=1 로 빌드해 설치)")
    steps = [s.strip() for s in steps if s.strip()]
    if not steps or steps[-1].lower() != "quit":
        steps.append("quit")
    backup, changed = apply_settings(settings or {})
    os.makedirs(RUNS_DIR, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S") + ("-" + re.sub(r"[^\w.-]", "_", tag) if tag else "")
    pm_result = {}
    try:
        with open(os.path.join(MOD_DIR, "autotest.txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(steps) + "\n")
        t0 = time.time()
        launch_game()
        while not game_running():
            if time.time() - t0 > 90:
                raise RuntimeError("90초 안에 게임이 켜지지 않음 (Steam 확인)")
            time.sleep(1)
        if presentmon:
            def pm():
                time.sleep(float(presentmon.get("start_after_s", 20)))
                try:
                    pm_result["text"] = run_presentmon(float(presentmon.get("seconds", 20)), os.path.join(RUNS_DIR, stamp + ".csv"))
                except Exception as e:
                    pm_result["text"] = "PresentMon 실패: %s" % e
            th = threading.Thread(target=pm, daemon=True)
            th.start()
        timed_out = False
        while game_running():
            if time.time() - t0 > timeout_min * 60:
                kill_game()
                timed_out = True
                break
            time.sleep(2)
        if presentmon:
            th.join(timeout=120)
        time.sleep(2)  # 로그가 닫히기를 잠깐 기다림
    finally:
        restore_settings(backup)
        leftover = os.path.join(MOD_DIR, "autotest.txt")
        if os.path.exists(leftover):
            os.remove(leftover)   # 게임이 읽지 못했으면 다음 실행에 남지 않게
    saved = os.path.join(RUNS_DIR, stamp + ".log")
    shutil.copy2(PLAYER_LOG, saved)
    text = read_text(saved)
    head = ["걸린 시간 %.0f초%s, 로그 %s" % (time.time() - t0, " (시간 초과로 끔)" if timed_out else "", saved)]
    if changed:
        head.append("설정 덮어씀(끝나고 되돌림): " + ", ".join(changed))
    if "[자동 시험] 시작" not in text:
        head.append("경고: 로그에 자동 시험 시작 줄이 없다")
    if pm_result:
        head += ["\n=== PresentMon ===", pm_result["text"]]
    return "\n".join(head) + "\n\n" + summarize(text), metrics(text)


def sf_ab(steps, setting, a, b, repeats=2, timeout_min=15, presentmon=None):
    n = 2 * max(1, int(repeats))
    order = ([a, b, b, a] * n)[:n]   # ABBA: 판이 갈수록 달라지는 것(온도, 캐시)을 상쇄
    res = {str(a): [], str(b): []}
    lines = []
    for i, v in enumerate(order):
        try:
            _, m = sf_run(list(steps), {setting: v}, timeout_min, "ab-%s-%s" % (setting, v), presentmon)
        except Exception as e:
            lines.append("%2d. %s=%s: 실패, 여기서 멈춤 - %s" % (i + 1, setting, v, e))
            break
        res[str(v)].append(m)
        fps = [r["fps"] for r in m["runs"]] or [x["fps"] for x in m["songs"]]   # 판별 FPS 는 개발자용에만 있다
        h = m["hitches"]
        sg = m["songs"]
        lines.append("%2d. %s=%s: FPS %s | 끊김 %s | 최악 %s ms | 곡 중(연출 뒤) 최악 %s ms, 끊김 %s | 오류 %d" % (
            i + 1, setting, v, ", ".join("%.0f" % x for x in fps) or "-",
            ", ".join(str(x["count"]) for x in h) or "-", ", ".join("%.0f" % x["worst"] for x in h) or "-",
            ", ".join("%.0f" % x["worst"] for x in sg) or "-", ", ".join(str(x["count"]) for x in sg) or "-", m["errors"]))
    lines.append("")
    for v, ms in res.items():
        fps = [r["fps"] for m in ms for r in m["runs"]] or [x["fps"] for m in ms for x in m["songs"]]
        sworst = [x["worst"] for m in ms for x in m["songs"]]
        cnt = [x["count"] for m in ms for x in m["hitches"]]
        worst = [x["worst"] for m in ms for x in m["hitches"]]
        if not ms:
            continue
        lines.append("%s=%s (%d번): 평균 FPS %s, 끊김 평균 %s, 최악 최댓값 %s, 곡 중(연출 뒤) 최악 최댓값 %s" % (
            setting, v, len(ms),
            "%.1f (편차 %.1f)" % (statistics.mean(fps), statistics.pstdev(fps)) if fps else "-",
            "%.1f" % statistics.mean(cnt) if cnt else "-", "%.0fms" % max(worst) if worst else "-",
            "%.0fms" % max(sworst) if sworst else "-"))
    return "\n".join(lines)


# ── 켜 둔 게임 다시 쓰기 (AutoTest 의 keep 묶음: 1초마다 autotest.txt 를 찾고, 끝나면 autotest.end 를 쓴다) ──
END_FILE = os.path.join(MOD_DIR, "autotest.end")
TXT_FILE = os.path.join(MOD_DIR, "autotest.txt")


def log_size():
    try:
        return os.path.getsize(PLAYER_LOG)
    except OSError:
        return 0


def read_from(off):
    with open(PLAYER_LOG, "rb") as f:
        size = os.fstat(f.fileno()).st_size
        f.seek(off if off <= size else 0)   # 게임을 새로 켜면 로그가 처음부터 다시 쓰인다
        return f.read().decode("utf-8", errors="replace")


def _batch(lines, timeout_min, launch_ok=True):
    """묶음 하나를 넣고 autotest.end 를 기다린다. (끝 이유, 그 묶음 로그, 걸린 초)"""
    if os.path.exists(END_FILE):
        os.remove(END_FILE)
    running = game_running()
    off = log_size() if running else 0
    with open(TXT_FILE, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(["keep"] + lines) + "\n")
    t0 = time.time()
    if not running:
        if not launch_ok:
            raise RuntimeError("게임이 꺼져 있다")
        launch_game()
        while not game_running():
            if time.time() - t0 > 90:
                raise RuntimeError("90초 안에 게임이 켜지지 않음 (Steam 확인)")
            time.sleep(1)
    try:
        while not os.path.exists(END_FILE):
            if not game_running():
                raise RuntimeError("묶음 도중 게임이 꺼짐")
            if time.time() - t0 > timeout_min * 60:
                raise RuntimeError("%.0f분 안에 묶음이 끝나지 않음" % timeout_min)
            if time.time() - t0 > (15 if running else 150) and os.path.exists(TXT_FILE):
                raise RuntimeError("게임이 autotest.txt 를 읽지 않음 (자동 시험 빌드가 아니거나 이전 버전 DLL)")
            time.sleep(0.5)
    finally:
        if os.path.exists(TXT_FILE):
            os.remove(TXT_FILE)
    time.sleep(0.5)   # 로그가 마저 쓰이기를 잠깐
    why = read_text(END_FILE).strip() if os.path.exists(END_FILE) else "?"
    return why, read_from(off), time.time() - t0


def live_run(steps, settings=None, timeout_min=15, tag="", reload=False, fresh=False):
    if not IS_WIN:
        raise RuntimeError("PC(윈도우)에서만 돈다")
    dll = os.path.join(MOD_DIR, "StutterFix.dll")
    if not dll_has_autotest(dll):
        raise RuntimeError("설치된 DLL 에 자동 시험이 없다 (개발자용 빌드나 -p:AutoTestBuild=1 로 빌드해 설치)")
    if fresh and game_running():
        kill_game()
        time.sleep(3)
    head = []
    if reload and game_running():
        why, _, _ = _batch(["reload"], 2)
        time.sleep(3)   # 새 DLL 이 올라오기를
        head.append("다시 불러옴 (새 DLL)" if why == "다시 불러오기" else "DLL 이 그대로라 다시 불러오지 않음")
    lines = [s.strip() for s in steps if s.strip() and s.strip().lower() not in ("quit", "keep")]
    sets = ["set %s %s" % (k, str(v).lower() if isinstance(v, bool) else v) for k, v in (settings or {}).items()]
    why, text, sec = _batch(sets + lines, timeout_min)
    os.makedirs(RUNS_DIR, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S") + ("-" + re.sub(r"[^\w.-]", "_", tag) if tag else "")
    saved = os.path.join(RUNS_DIR, stamp + ".log")
    with open(saved, "w", encoding="utf-8") as f:
        f.write(text)
    head.append("걸린 시간 %.0f초, 묶음 끝: %s, 로그 %s" % (sec, why, saved))
    if sets:
        head.append("설정(묶음 끝에 게임이 되돌림): " + ", ".join(s[4:] for s in sets))
    m = metrics(text)
    if why != "끝":
        m["errors"] += 1
    return "\n".join(head) + "\n\n" + summarize(text), m, why


def sf_live(steps, settings=None, timeout_min=15, tag="", reload=False, fresh=False):
    if not RUN_LOCK.acquire(blocking=False):
        raise RuntimeError("다른 측정이 돌고 있다")
    try:
        text, _, _ = live_run(steps, settings, timeout_min, tag, reload, fresh)
        return text
    finally:
        RUN_LOCK.release()


def ab_line(i, setting, v, m):
    fps = [r["fps"] for r in m["runs"]] or [x["fps"] for x in m["songs"]]
    h, sg = m["hitches"], m["songs"]
    return "%2d. %s=%s: FPS %s | 끊김 %s | 최악 %s ms | 곡 중(연출 뒤) 최악 %s ms, 끊김 %s | 오류 %d" % (
        i, setting, v, ", ".join("%.0f" % x for x in fps) or "-",
        ", ".join(str(x["count"]) for x in h) or "-", ", ".join("%.0f" % x["worst"] for x in h) or "-",
        ", ".join("%.0f" % x["worst"] for x in sg) or "-", ", ".join(str(x["count"]) for x in sg) or "-", m["errors"])


def ab_table(setting, res):
    lines = []
    for v, ms in res.items():
        if not ms:
            continue
        fps = [r["fps"] for m in ms for r in m["runs"]] or [x["fps"] for m in ms for x in m["songs"]]
        sworst = [x["worst"] for m in ms for x in m["songs"]]
        cnt = [x["count"] for m in ms for x in m["hitches"]]
        worst = [x["worst"] for m in ms for x in m["hitches"]]
        lines.append("%s=%s (%d번): 평균 FPS %s, 끊김 평균 %s, 최악 최댓값 %s, 곡 중(연출 뒤) 최악 최댓값 %s" % (
            setting, v, len(ms),
            "%.1f (편차 %.1f)" % (statistics.mean(fps), statistics.pstdev(fps)) if fps else "-",
            "%.1f" % statistics.mean(cnt) if cnt else "-", "%.0fms" % max(worst) if worst else "-",
            "%.0fms" % max(sworst) if sworst else "-"))
    return lines


def sf_ab_live(steps, setting, a, b, repeats=2, timeout_min=15, warmup=True, reload=False):
    if not RUN_LOCK.acquire(blocking=False):
        raise RuntimeError("다른 측정이 돌고 있다")
    try:
        n = 2 * max(1, int(repeats))
        order = ([a, b, b, a] * n)[:n]
        res = {str(a): [], str(b): []}
        lines = []
        if warmup:
            try:
                _, m, why = live_run(list(steps), {setting: a}, timeout_min, "ab-warm", reload)
                lines.append(" 0. (버림, 첫 판) " + ab_line(0, setting, a, m)[4:] + ("" if why == "끝" else " [" + why + "]"))
            except Exception as e:
                lines.append(" 0. 첫 판 실패: %s" % e)
            reload = False
        for i, v in enumerate(order):
            m = None
            for attempt in range(2):
                try:
                    _, m, why = live_run(list(steps), {setting: v}, timeout_min, "ab-%s-%s" % (setting, v), reload and i == 0, fresh=attempt > 0)
                    if why != "끝":
                        raise RuntimeError("묶음 끝: " + why)
                    break
                except Exception as e:
                    lines.append("%2d. %s=%s: %s번째 실패 - %s%s" % (i + 1, setting, v, attempt + 1, e, ", 게임을 새로 켜서 다시" if attempt == 0 else ""))
                    m = None
                    if attempt == 0:
                        kill_game()
                        time.sleep(3)
                        try:   # 새로 켠 게임의 첫 판은 버린다
                            live_run(list(steps), {setting: v}, timeout_min, "ab-rewarm")
                        except Exception:
                            pass
            if m is None:
                lines.append("여기서 멈춤")
                break
            res[str(v)].append(m)
            lines.append(ab_line(i + 1, setting, v, m))
        lines.append("")
        lines += ab_table(setting, res)
        return "\n".join(lines)
    finally:
        RUN_LOCK.release()


def sf_quit():
    if not game_running():
        return "이미 꺼져 있다"
    try:
        _batch(["quit"], 1, launch_ok=False)
    except Exception:
        pass
    t0 = time.time()
    while game_running() and time.time() - t0 < 20:
        time.sleep(1)
    if game_running():
        kill_game()
        return "안 꺼져서 강제로 끔"
    return "껐다"


def sf_status():
    dll = os.path.join(MOD_DIR, "StutterFix.dll")
    out = ["게임 켜짐: %s" % ("예" if game_running() else "아니오")]
    if os.path.exists(dll):
        st = os.stat(dll)
        out.append("DLL: %s (%s, %d KB), 자동 시험 %s" % (dll, time.strftime("%Y-%m-%d %H:%M", time.localtime(st.st_mtime)), st.st_size // 1024,
                                                    "있음" if dll_has_autotest(dll) else "없음"))
    else:
        out.append("DLL 없음: " + dll)
    boot = os.path.join(GAME_DIR, "A Dance of Fire and Ice_Data", "boot.config")
    if os.path.exists(boot):
        keep = [l for l in read_text(boot).splitlines() if re.match(r"(force-|gfx-)", l)]
        out.append("boot.config: " + (", ".join(keep) or "관련 줄 없음 (Flip 방식, 그래픽 작업 기본값)"))
    out.append("Player.log: " + (time.strftime("%Y-%m-%d %H:%M", time.localtime(os.path.getmtime(PLAYER_LOG))) if os.path.exists(PLAYER_LOG) else "없음"))
    out.append("PresentMon: " + (PRESENTMON if PRESENTMON and os.path.exists(PRESENTMON) else "설정 안 됨 (SF_PRESENTMON)"))
    return "\n".join(out)


# ── MCP (stdio, 줄 단위 JSON-RPC) ──
STEPS_DESC = "자동 시험 명령 줄들. 예: [\"open D:/maps/x.adofai\", \"auto on\", \"play\", \"wait 60\", \"stop\"]. 마지막 quit 은 자동으로 붙는다."
PM_SCHEMA = {"type": "object", "description": "곡 중 PresentMon 기록 (선택)", "properties": {
    "start_after_s": {"type": "number", "description": "게임 켜진 뒤 몇 초부터"}, "seconds": {"type": "number"}}}
TOOLS = [
    {"name": "sf_status", "description": "게임 실행 여부, 설치된 StutterFix DLL(자동 시험 포함 여부), boot.config, PresentMon 설정 확인",
     "inputSchema": {"type": "object", "properties": {}}},
    {"name": "sf_run", "description": "게임을 켜서 자동 시험 명령을 돌리고 끝나면 로그 요약을 돌려준다. settings 로 모드 설정을 이번 판만 바꿀 수 있다(끝나면 되돌림).",
     "inputSchema": {"type": "object", "required": ["steps"], "properties": {
         "steps": {"type": "array", "items": {"type": "string"}, "description": STEPS_DESC},
         "settings": {"type": "object", "description": "Settings 필드 이름 -> 값 (예: {\"LegacyGfxJobs\": false})"},
         "timeout_min": {"type": "number"}, "tag": {"type": "string"}, "presentmon": PM_SCHEMA}}},
    {"name": "sf_ab", "description": "설정 하나를 a/b 로 바꿔 ABBA 순서로 되풀이해 돌리고 FPS·끊김 비교표를 돌려준다 (판마다 게임을 새로 켬).",
     "inputSchema": {"type": "object", "required": ["steps", "setting", "a", "b"], "properties": {
         "steps": {"type": "array", "items": {"type": "string"}, "description": STEPS_DESC},
         "setting": {"type": "string"}, "a": {}, "b": {}, "repeats": {"type": "integer", "description": "A/B 쌍 수 (기본 2 -> 4판)"},
         "timeout_min": {"type": "number"}, "presentmon": PM_SCHEMA}}},
    {"name": "sf_live", "description": "켜 둔 게임에 명령 묶음을 넣고(게임이 꺼져 있으면 켬) 끝나면 그 묶음 로그만 요약한다. 게임은 계속 켜 둔다. settings 는 게임 안에서 set 으로 바꾸고 묶음 끝에 되돌린다(재시작이 필요한 설정은 sf_run/sf_ab). reload 는 먼저 Ctrl+F5(새 DLL), fresh 는 게임을 새로 켬.",
     "inputSchema": {"type": "object", "required": ["steps"], "properties": {
         "steps": {"type": "array", "items": {"type": "string"}, "description": "자동 시험 명령 줄들 (quit 없이)"},
         "settings": {"type": "object"}, "timeout_min": {"type": "number"}, "tag": {"type": "string"},
         "reload": {"type": "boolean"}, "fresh": {"type": "boolean"}}}},
    {"name": "sf_ab_live", "description": "같은 게임 안에서 설정 하나를 a/b 로 바꿔 ABBA 로 되풀이 (첫 판 버림, 실패한 판은 게임을 새로 켜서 다시). 재시작이 필요한 설정과 최종 확인은 sf_ab.",
     "inputSchema": {"type": "object", "required": ["steps", "setting", "a", "b"], "properties": {
         "steps": {"type": "array", "items": {"type": "string"}}, "setting": {"type": "string"}, "a": {}, "b": {},
         "repeats": {"type": "integer"}, "timeout_min": {"type": "number"}, "warmup": {"type": "boolean"}, "reload": {"type": "boolean"}}}},
    {"name": "sf_quit", "description": "켜 둔 게임을 끈다 (자동 시험 quit, 안 되면 강제)", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "sf_log", "description": "Player.log(또는 file 로 준 저장 로그)의 최근 판 요약: 설치 오류, 자동 시험 결과, 끊김 요약, 30ms 넘은 프레임과 원인",
     "inputSchema": {"type": "object", "properties": {"runs": {"type": "integer"}, "file": {"type": "string"}, "max_frames": {"type": "integer"}}}},
    {"name": "sf_presentmon", "description": "지금 켜져 있는 게임을 PresentMon 으로 N초 재서 FPS, 1% 최저, GPU 바쁨, 화면 지연, 출력 방식(Flip/합성), 찢어짐 허용을 요약",
     "inputSchema": {"type": "object", "properties": {"seconds": {"type": "number"}}}},
]


def call(name, a):
    if name == "sf_status":
        return sf_status()
    if name == "sf_run":
        text, _ = sf_run(a["steps"], a.get("settings"), float(a.get("timeout_min", 15)), a.get("tag", ""), a.get("presentmon"))
        return text
    if name == "sf_ab":
        return sf_ab(a["steps"], a["setting"], a["a"], a["b"], int(a.get("repeats", 2)), float(a.get("timeout_min", 15)), a.get("presentmon"))
    if name == "sf_live":
        return sf_live(a["steps"], a.get("settings"), float(a.get("timeout_min", 15)), a.get("tag", ""), bool(a.get("reload")), bool(a.get("fresh")))
    if name == "sf_ab_live":
        return sf_ab_live(a["steps"], a["setting"], a["a"], a["b"], int(a.get("repeats", 2)), float(a.get("timeout_min", 15)),
                          a.get("warmup", True) is not False, bool(a.get("reload")))
    if name == "sf_quit":
        return sf_quit()
    if name == "sf_log":
        return summarize(read_text(a.get("file") or PLAYER_LOG), int(a.get("runs", 1)), int(a.get("max_frames", 15)))
    if name == "sf_presentmon":
        if not game_running():
            raise RuntimeError("게임이 꺼져 있다")
        os.makedirs(RUNS_DIR, exist_ok=True)
        return run_presentmon(float(a.get("seconds", 20)), os.path.join(RUNS_DIR, time.strftime("%Y%m%d-%H%M%S") + "-pm.csv"))
    raise RuntimeError("모르는 도구: " + name)


def send(obj):
    data = json.dumps(obj, ensure_ascii=False).encode("utf-8") + b"\n"
    with OUT_LOCK:
        sys.stdout.buffer.write(data)
        sys.stdout.buffer.flush()


def handle(msg):
    mid, method = msg.get("id"), msg.get("method")
    if mid is None:
        return  # 알림
    if method == "initialize":
        ver = (msg.get("params") or {}).get("protocolVersion", "2025-06-18")
        send({"jsonrpc": "2.0", "id": mid, "result": {"protocolVersion": ver, "capabilities": {"tools": {}},
                                                       "serverInfo": {"name": "sfmeasure", "version": "1.0.0"}}})
    elif method == "tools/list":
        send({"jsonrpc": "2.0", "id": mid, "result": {"tools": TOOLS}})
    elif method == "tools/call":
        p = msg.get("params") or {}
        try:
            text, err = call(p.get("name"), p.get("arguments") or {}), False
        except Exception as e:
            text, err = "실패: %s" % e, True
        send({"jsonrpc": "2.0", "id": mid, "result": {"content": [{"type": "text", "text": text}], "isError": err}})
    elif method == "ping":
        send({"jsonrpc": "2.0", "id": mid, "result": {}})
    else:
        send({"jsonrpc": "2.0", "id": mid, "error": {"code": -32601, "message": "없는 메서드: %s" % method}})


def main():
    for raw in sys.stdin.buffer:
        raw = raw.strip()
        if not raw:
            continue
        try:
            msg = json.loads(raw.decode("utf-8"))
        except ValueError:
            continue
        if msg.get("method") == "tools/call":
            threading.Thread(target=handle, args=(msg,), daemon=True).start()   # 게임 한 판은 몇 분 걸린다
        else:
            handle(msg)


if __name__ == "__main__":
    main()
