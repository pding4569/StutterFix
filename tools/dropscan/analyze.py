"""DropScan offline analysis. Python 3.12+, standard library only.

--map NAME FRAMES SAMPLES PLAYER_LOG (repeat); '-' means no Player.log.
Only full timestamped samples can be aligned; legacy files are diagnosed.
"""
from __future__ import annotations

import argparse
from bisect import bisect_right
from collections import Counter, defaultdict
import csv
from dataclasses import dataclass
import importlib.util
import json
import math
from pathlib import Path
import re
import statistics


@dataclass(slots=True)
class Frame:
    frame: int
    song: float
    end_song: float
    start: float
    end: float
    ms: float
    playing: bool
    record_us: float


def finite(value, default=None):
    try:
        n = float(value)
        return n if math.isfinite(n) else default
    except (ValueError, TypeError):
        return default


def load_frames(path):
    result = []
    with Path(path).open(encoding="utf-8-sig", newline="") as stream:
        for number, r in enumerate(csv.DictReader(stream), 2):
            try:
                f = Frame(int(r["frame"]), float(r["song_s"]), float(r["song_end_s"]),
                          float(r["wall_start_s"]), float(r["wall_end_s"]), float(r["frame_ms"]),
                          r["playing"] == "1", float(r["record_us"]))
                if not all(math.isfinite(x) for x in (f.start, f.end, f.ms, f.record_us)) or f.end <= f.start or f.ms <= 0:
                    raise ValueError("유효하지 않은 프레임 시간")
                if abs((f.end - f.start) * 1000 - f.ms) > max(.02, f.ms * .001):
                    raise ValueError("벽시계 구간/프레임 시간 불일치")
                if result and (f.start < result[-1].end - 1e-7 or f.frame <= result[-1].frame):
                    raise ValueError("벽시계 또는 프레임 번호 역행/중복")
                result.append(f)
            except (ValueError, KeyError) as error:
                raise ValueError(f"{path}:{number}: {error}") from error
    if not result:
        raise ValueError(f"프레임 없음: {path}")
    return result


class Symbols:
    def __init__(self, path=None):
        self.module = None
        self.cache = {}
        self.unresolved = set()
        if path:
            spec = importlib.util.spec_from_file_location("dropscan_symb", Path(path).resolve())
            self.module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(self.module)
            if not callable(getattr(self.module, "name", None)):
                raise ValueError("symb_lib에 name(offset: int) 함수가 필요합니다")

    def resolve(self, token):
        if not token.startswith("U:"):
            return token
        if token not in self.cache:
            offset = int(token[2:], 16)
            name = self.module.name(offset) if self.module else None
            if not name or str(name).startswith("U?"):
                self.unresolved.add(token)
                self.cache[token] = f"U:0x{offset:X}"
            else:
                self.cache[token] = "U:" + str(name)
        return self.cache[token]


def category(function):
    if function.startswith("U:"):
        return "엔진 네이티브"
    if function.startswith("X:"):
        return "기타 네이티브"
    if "StutterFix" in function:
        return "우리 모드"
    if function.startswith(("M:UnityEngine", "M:Unity.", "M:System.", "M:Mono.", "M:(wrapper")):
        return "엔진·런타임 관리"
    return "게임·외부 모드 관리"


def load_samples(path, frames, symbols):
    starts = [f.start for f in frames]
    by_id = {f.frame: i for i, f in enumerate(frames)}
    # Counters are per-frame inclusive counts; recursion counts once per sample.
    counts = defaultdict(Counter)
    totals = Counter()
    stats = Counter()
    suspend = []
    suspend_by_frame = Counter()
    with Path(path).open(encoding="utf-8-sig") as stream:
        for number, line in enumerate(stream, 1):
            line = line.strip()
            if not line or line.startswith(("#", "@")):
                continue
            stats["lines"] += 1
            tokens = line.split(";")
            meta = {}
            stack = []
            try:
                for token in tokens:
                    if token.startswith(("T:", "F:", "W:", "S:")):
                        meta[token[0]] = token[2:]
                    elif token.startswith(("M:", "U:", "X:")):
                        stack.append(symbols.resolve(token))
                if "T" not in meta or "F" not in meta:
                    stats["legacy_unaligned"] += 1
                    continue
                if not stack:
                    stats["empty_stack"] += 1
                    continue
                frame_id = int(meta["F"])
                if "W" in meta:
                    stamp = finite(meta["W"])
                    if stamp is None:
                        raise ValueError("invalid W")
                    index = bisect_right(starts, stamp) - 1
                    if index < 0 or stamp >= frames[index].end:
                        stats["outside_frames"] += 1
                        continue
                    if frames[index].frame != frame_id:
                        stats["frame_clock_mismatch"] += 1
                else:
                    index = by_id.get(frame_id)
                    stats["frame_only"] += 1
                    if index is None:
                        stats["outside_frames"] += 1
                        continue
                totals[index] += 1
                counts[index].update(set(stack))
                stats["aligned"] += 1
                cost = finite(meta.get("S"))
                if cost is not None:
                    suspend.append(cost)
                    suspend_by_frame[index] += cost
            except (ValueError, OverflowError) as error:
                raise ValueError(f"{path}:{number}: 표본 형식 오류: {error}") from error
    return counts, totals, dict(stats), suspend, suspend_by_frame


def windows(frames, active):
    """Fixed 0.5s wall-clock bins, fractionally apportion crossing frames.

    A single long frame contributes 1/frame_duration FPS to each crossed bin.
    This keeps a frozen main thread visible even with no frame completion.
    """
    origin = frames[min(active)].start
    bins = defaultdict(lambda: [0., 0., []])
    for i in sorted(active):
        f = frames[i]
        first = math.floor((f.start - origin) / .5)
        last = math.ceil((f.end - origin) / .5) - 1
        for k in range(first, last + 1):
            duration = max(0., min(f.end, origin + (k + 1) * .5) - max(f.start, origin + k * .5))
            if duration:
                bins[k][0] += duration / (f.end - f.start)
                bins[k][1] += duration
                bins[k][2].append(i)
    complete = {k: {"fps": v[0] / .5, "indices": v[2], "start": origin + k * .5,
                    "end": origin + (k + 1) * .5} for k, v in bins.items() if v[1] >= .5 - 1e-6}
    return complete


def ranges(indices):
    result = []
    for i in sorted(indices):
        if result and i == result[-1][-1] + 1:
            result[-1].append(i)
        else:
            result.append([i])
    return result


def read_log(path):
    if path == "-":
        return []
    # Monitor song time is elapsed playing time, not necessarily conductor time.
    pattern = re.compile(r"\[모니터\]\s*([\d.,]+)ms\s*(?:->|→)\s*(.*?)\s*/.*?곡\s*([\d.,-]+)초")
    found = []
    with Path(path).open(encoding="utf-8-sig", errors="replace") as stream:
        for number, line in enumerate(stream, 1):
            m = pattern.search(line)
            if m:
                found.append({"ms": float(m[1].replace(",", ".")), "classification": m[2],
                              "monitor_song_s": float(m[3].replace(",", ".")), "line": number})
    return found


def sidecar(path, name, warnings):
    p = Path(path).parent / name
    if not p.exists():
        warnings.append(f"{name} 없음: 상한·누락 여부 확인 불가")
        return None
    return json.loads(p.read_text(encoding="utf-8-sig"))


def analyze_map(name, frame_path, sample_path, log_path, symbols):
    warnings = []
    frames = load_frames(frame_path)
    active = {i for i, f in enumerate(frames) if f.playing and math.isfinite(f.song) and f.song >= 0}
    if not active:
        raise ValueError(f"{name}: 곡 재생 프레임 없음")
    if any(frames[b].song < frames[a].song - .05 for a, b in zip(sorted(active), sorted(active)[1:])):
        raise ValueError(f"{name}: 곡 시각 역행: seek/재시작 판을 분리하세요")
    tail = max(frames[i].end_song if math.isfinite(frames[i].end_song) else frames[i].song for i in active)
    core = {i for i in active if frames[i].song >= 5 and frames[i].end_song < tail - 5}
    if not core:
        core = active
        warnings.append("중앙 재생 구간 부족: 전체 재생 프레임을 중앙값 기준으로 사용")
    normal_ms = statistics.median(frames[i].ms for i in core)
    threshold = max(20., normal_ms * 2.5)
    bins = windows(frames, active)
    core_bins = [v["fps"] for v in bins.values() if all(i in core for i in v["indices"])]
    median_fps = statistics.median(core_bins or [v["fps"] for v in bins.values()]) if bins else 1000 / normal_ms
    low = sorted(k for k, v in bins.items() if v["fps"] < median_fps * .8)
    drop_indices = []
    for run in ranges(low):
        if len(run) >= 2:
            drop_indices.append((set(i for k in run for i in bins[k]["indices"]), bins[run[0]]["start"], bins[run[-1]]["end"]))
    hitch_indices = sorted(i for i, f in enumerate(frames) if math.isfinite(f.song) and f.ms > threshold)
    bad = set(hitch_indices)
    for ids, _, _ in drop_indices:
        bad.update(ids)
    counts, totals, stats, suspend, suspend_by_frame = load_samples(sample_path, frames, symbols)
    normal_ids = core - bad
    normal_counts = Counter()
    for i in normal_ids:
        normal_counts.update(counts[i])
    normal_total = sum(totals[i] for i in normal_ids)
    if not normal_total:
        warnings.append("정상 구간 표본 없음: 원인 비율 차이/절감 추정 계산 안 함")
    if stats.get("legacy_unaligned"):
        warnings.append("기존 frames 형식은 시각 없음: 해당 표본은 시간 분석에서 제외")
    if stats.get("frame_clock_mismatch"):
        warnings.append("공개 프레임 번호와 벽시계 불일치: Update 경계 표본을 W 기준으로 연결")
    if stats.get("outside_frames"):
        warnings.append("프레임 기록 밖 표본 존재: begin/end 순서 및 기록 상한 확인")
    if stats.get("aligned", 0) == 0:
        warnings.append("연결된 표본 0: 원인 분석 불가")

    def describe(ids, kind, start=None, end=None):
        ids = sorted(ids)
        selected = Counter()
        for i in ids:
            selected.update(counts[i])
        n = sum(totals[i] for i in ids)
        excess = sum(frames[i].ms - normal_ms for i in ids)
        candidates = []
        if normal_total and n:
            for function, hits in selected.items():
                # X stacks are retained in inputs; cause ranking is M/U only.
                if not function.startswith(("M:", "U:")):
                    continue
                bad_ratio = hits / n
                good_ratio = normal_counts[function] / normal_total
                delta = bad_ratio - good_ratio
                if delta > 0:
                    candidates.append({"function": function, "category": category(function), "bad_ratio": bad_ratio,
                                       "normal_ratio": good_ratio, "delta": delta, "estimated_excess_ms": excess * delta})
            candidates.sort(key=lambda c: (-c["delta"], c["function"]))
        first, last = frames[ids[0]], frames[ids[-1]]
        tags = []
        if any(not frames[i].playing or frames[i].song < 0 for i in ids):
            tags.append("재생 전·재생 밖")
        if first.song < 5:
            tags.append("시작 5초·재생 전 경계")
        if last.end_song >= tail - 5:
            tags.append("기록 끝 5초·곡 종료 여부 확인 필요")
        begin, finish = start if start is not None else first.start, end if end is not None else last.end
        matching_logs = [l for l in logs if first.song - 1 <= l["monitor_song_s"] <= last.end_song + 1]
        return {"kind": kind, "song_start_s": first.song, "song_end_s": last.end_song,
                "wall_start_s": begin, "wall_end_s": finish, "duration_s": finish - begin,
                "frames": len(ids), "first_frame": first.frame, "last_frame": last.frame,
                "worst_ms": max(frames[i].ms for i in ids), "excess_ms": excess, "samples": n,
                "suspend_upper_estimate_ms": sum(suspend_by_frame[i] for i in ids) / 1000,
                "sampled_frames": sum(totals[i] > 0 for i in ids), "boundary": tags,
                "candidates": candidates[:10], "monitor_nearby_unverified": matching_logs,
                "representative_stack_group": candidates[0]["function"] if candidates else None}

    logs = read_log(log_path)
    if log_path == "-":
        warnings.append("Player.log 미입력: 모니터 분류 없음")
    elif not logs:
        warnings.append("Player.log에서 모니터 끊김 분류를 찾지 못함")
    if logs:
        warnings.append("모니터 곡 시각은 conductor와 다를 수 있음: ±1초 근접 분류는 미확정 참고")
    hitches = [describe([i], "hitch") for i in hitch_indices]
    drops = [describe(ids, "drop", start, end) for ids, start, end in drop_indices]
    # One union episode owns each bad frame; hitches inside drops are not added twice.
    episodes = [describe(ids, "union") for ids in ranges(bad)]
    frame_meta = sidecar(frame_path, "dropscan-meta.json", warnings)
    sample_meta = sidecar(sample_path, "profsamp-meta.json", warnings)
    if frame_meta and frame_meta.get("truncated"):
        warnings.append("프레임 600000 상한 초과: 전곡 결과 아님")
    if sample_meta and sample_meta.get("capacity_reached"):
        warnings.append("표본 상한 도달: 이후 구간 원인 누락 가능")
    if sample_meta and (sample_meta.get("sampling_error") or sample_meta.get("failed_contexts") or sample_meta.get("failed_suspends")):
        warnings.append("표본 수집 오류/컨텍스트 실패 발생: meta 확인")
    record = sorted(frames[i].record_us for i in active)
    costs = sorted(suspend)
    return {"name": name, "inputs": {"frames": str(frame_path), "samples": str(sample_path), "log": str(log_path)},
            "frames": len(frames), "playing_frames": len(active), "median_frame_ms": normal_ms,
            "median_window_fps": median_fps, "hitch_threshold_ms": threshold,
            "recorded_song_end_s": tail, "normal_samples": normal_total, "sample_stats": stats,
            "overhead": {"record_body_median_us": statistics.median(record), "record_body_max_us": max(record),
                         "suspend_median_us": statistics.median(costs) if costs else None,
                         "suspend_p99_us": costs[min(len(costs) - 1, math.ceil(len(costs) * .99) - 1)] if costs else None,
                         "suspend_max_us": max(costs) if costs else None},
            "frame_meta": frame_meta, "sample_meta": sample_meta, "warnings": warnings,
            "hitches": hitches, "drops": drops, "episodes": episodes, "monitor_lines": logs}


def build_report(maps, symbols):
    groups = {}
    for m in maps:
        for e in m["episodes"]:
            if not e["candidates"]:
                continue
            c = e["candidates"][0]
            g = groups.setdefault(c["function"], {"function": c["function"], "category": c["category"],
                                                   "estimated_excess_ms": 0., "core_estimated_excess_ms": 0.,
                                                   "boundary_estimated_excess_ms": 0., "intervals": []})
            g["estimated_excess_ms"] += c["estimated_excess_ms"]
            g["boundary_estimated_excess_ms" if e["boundary"] else "core_estimated_excess_ms"] += c["estimated_excess_ms"]
            g["intervals"].append({"map": m["name"], "song_start_s": e["song_start_s"], "song_end_s": e["song_end_s"],
                                   "excess_ms": e["excess_ms"], "delta": c["delta"],
                                   "estimated_excess_ms": c["estimated_excess_ms"], "samples": e["samples"], "boundary": e["boundary"]})
    return {"schema_version": 1, "method": {"window_s": .5, "fps_fraction": .8, "drop_min_s": 1.,
                "hitch_ms": "max(20, median_frame_ms * 2.5)", "boundary_s": 5,
                "estimate": "Σ (frame_ms - normal_ms) × (bad_inclusive_ratio - normal_inclusive_ratio)",
                "grouping": "대표 함수; 겹친 끊김/드랍 프레임 합집합 단위로 한 번만 배정",
                "causality": "표본 상관 기반 우선순위 추정, 실제 제거 가능 시간/인과관계 아님"},
            "symbolization": {"enabled": symbols.module is not None, "unresolved_unity_rvas": sorted(symbols.unresolved)},
            "causes": sorted(groups.values(), key=lambda g: -g["estimated_excess_ms"]), "maps": maps}


def escaped(value):
    return str(value).replace("|", "\\|").replace("\n", " ")


def markdown(report):
    lines = ["# DropScan 전곡 분석", "", "표본 포함 비율 차이로 뽑은 원인 후보입니다. 실제 최적화 이득이나 인과관계는 검증하지 않았습니다.",
             "정상 기준은 시작·기록 끝 5초를 제외한 재생 구간 중앙값입니다. FPS는 벽시계 0.5초 고정 창에서 경계 프레임을 비례 배분합니다.",
             "원인 합계는 끊김·드랍의 프레임 합집합에서 대표 함수 하나에 배정하므로 구간 중복 합산은 없습니다. 각 후보 함수의 포함 비율은 서로 겹칠 수 있습니다.",
             "", "## 원인별 우선순위", "", "| 대표 함수 | 구분 | 추정 합(ms) | 본 구간(ms) | 시작·끝·재생 밖(ms) | 맵·곡 구간 |", "|---|---|---:|---:|---:|---|"]
    for c in report["causes"]:
        intervals = ", ".join(f'{i["map"]} {i["song_start_s"]:.2f}–{i["song_end_s"]:.2f}s' for i in c["intervals"])
        lines.append(f'| {escaped(c["function"])} | {c["category"]} | {c["estimated_excess_ms"]:.2f} | {c["core_estimated_excess_ms"]:.2f} | {c["boundary_estimated_excess_ms"]:.2f} | {escaped(intervals)} |')
    if not report["causes"]:
        lines.append("| 계산 가능한 후보 없음 | 표본·정상 구간 확인 | — | — | — | — |")
    for m in report["maps"]:
        lines += ["", f'## {escaped(m["name"])}', "", f'재생 {m["playing_frames"]}프레임, 정상 중앙값 {m["median_frame_ms"]:.3f}ms, '
                  f'0.5초 창 중앙값 {m["median_window_fps"]:.1f}FPS, 끊김 기준 >{m["hitch_threshold_ms"]:.2f}ms. 정상 표본 {m["normal_samples"]}개.', ""]
        lines += ["- " + w for w in m["warnings"]]
        lines += ["", "| 종류 | 곡 구간(s) | 프레임 | 최악(ms) | 초과(ms) | 표본 | 경계·대표 후보 |", "|---|---:|---:|---:|---:|---:|---|"]
        for e in sorted(m["hitches"] + m["drops"], key=lambda e: e["wall_start_s"]):
            label = "끊김" if e["kind"] == "hitch" else "FPS 드랍"
            candidate = e["candidates"][0] if e["candidates"] else None
            detail = ", ".join(e["boundary"]) + ("; " if e["boundary"] else "")
            detail += f'{candidate["function"]} (포함 차이 {candidate["delta"]:.1%})' if candidate else "표본/비율 차이 부족"
            lines.append(f'| {label} | {e["song_start_s"]:.3f}–{e["song_end_s"]:.3f} | {e["frames"]} | {e["worst_ms"]:.2f} | {e["excess_ms"]:.2f} | {e["samples"]} | {escaped(detail)} |')
        if not m["hitches"] and not m["drops"]:
            lines.append("| 검출 없음 | — | — | — | — | — | — |")
        lines += ["", "세부 후보 상위 10개·포함 비율·근접 모니터 분류·오버헤드·상한 정보는 JSON에 있습니다."]
    lines += ["", "## 한계", "", "- 250Hz는 약 4ms 간격입니다. 짧은 구간은 표본이 0개일 수 있고, 스택 깊이 32에서 잘립니다.",
              "- U: 기호 미해결 주소는 RVA별 후보로 유지합니다. PDB와 UnityPlayer 버전이 일치해야 함수 단위로 묶입니다.",
              "- M: 이름만으로 다른 모드와 게임을 완벽히 구분할 수 없어 게임·외부 모드는 함께 표시합니다.",
              "- GPU/대기/외부 선점 구분은 이 스택 표본만으로 확정할 수 없습니다. CPU·GPU 지연 값은 합산하지 않습니다.",
              "- 마지막 5초는 기록 끝 기준입니다. 완주 여부는 Claude의 waitend 결과와 대조해야 합니다.",
              "- 정지 비용은 SuspendThread 직전부터 ResumeThread 복귀까지의 상한 추정이며 OS 선점도 포함합니다.", ""]
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map", action="append", nargs=4, required=True, metavar=("NAME", "FRAMES", "SAMPLES", "PLAYER_LOG"))
    parser.add_argument("--symb-lib", help="name(offset) 함수를 제공하는 로컬 symb_lib.py 경로")
    parser.add_argument("--out", default=".", type=Path)
    args = parser.parse_args(argv)
    if len({m[0] for m in args.map}) != len(args.map):
        parser.error("맵 이름은 중복될 수 없습니다")
    symbols = Symbols(args.symb_lib)
    maps = [analyze_map(*m, symbols) for m in args.map]
    report = build_report(maps, symbols)
    args.out.mkdir(parents=True, exist_ok=True)
    (args.out / "dropscan-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False), encoding="utf-8")
    (args.out / "dropscan-report.md").write_text(markdown(report), encoding="utf-8")
    print(f"분석 완료: {len(maps)}맵, 원인 후보 {len(report['causes'])}개 → {args.out}")
    return report


if __name__ == "__main__":
    main()
