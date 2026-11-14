"""Offline chart census. Never imports a launcher or changes installed files.

Times are source-derived schedule estimates in songposition_minusi coordinates,
not observed execution frames. See the companion handoff for scope/limitations.
"""
import argparse
import collections
import csv
import hashlib
import json
import math
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = Path(r"D:\얼불춤 맵 파일\[노가다 크루] Silentroom - 1KXYZO\level.adofai")


def f(x):
    return struct.unpack("f", struct.pack("f", x))[0]


def analyze(path, out):
    raw = path.read_bytes()
    d = json.loads(raw.decode("utf-8-sig"))
    actions, settings, angles = d["actions"], d["settings"], d["angleData"]
    n = len(angles) + 1
    by_floor = collections.defaultdict(list)
    for i, e in enumerate(actions):
        if e.get("active", True):
            by_floor[e["floor"]].append((i, e))
    # This map has no multiplanet/free-roam/hold or intra-floor speed changes.
    # Refuse unsupported timing instead of silently reporting incomplete times.
    assert not any(e["eventType"] in ("MultiPlanet", "FreeRoam", "Hold") for e in actions)
    assert all(e.get("angleOffset", 0) == 0 for e in actions if e["eventType"] == "SetSpeed")
    pi, tau = 3.1415927410125732, 6.2831854820251465
    rad = f(pi / 180)
    entry, exit_ = [4.71238899230957], []
    mid = []
    for a in angles:
        ex = float(f(entry[-1])) if a == 999 else float(f(f(90 - a) * rad))
        exit_.append(ex)
        mid.append(a == 999)
        entry.append(math.fmod(ex + pi, tau))
    exit_.append(entry[-1] + pi)
    mid.append(False)
    speed, ccw, extra = [], [], []
    mult, twirl = 1.0, False
    bpm = f(settings["bpm"])
    for floor in range(n):
        pause = 0.0
        for _, e in by_floor[floor]:
            if e["eventType"] == "Twirl":
                twirl = not twirl
            elif e["eventType"] == "SetSpeed":
                mult = f(f(e["beatsPerMinute"]) / bpm) if e["speedType"] == "Bpm" else f(mult * f(e["bpmMultiplier"]))
            elif e["eventType"] == "Pause" and not mid[floor]:
                pause = f(pause + f(e["duration"]))
        speed.append(mult)
        ccw.append(twirl)
        extra.append(pause)
    crotchet = 60.0 / bpm

    def moved(i):
        return ((exit_[i] - entry[i]) * (-1 if ccw[i] else 1)) % tau

    times, beats_at = [0.0] * n, [-1.0] + [0.0] * (n - 1)
    times[1] = crotchet * (settings["countdownTicks"] - 1) + moved(0) / pi * crotchet / speed[0]
    for i in range(1, n - 1):
        angle = moved(i)
        beats = (0 if mid[i] else 2) if angle <= 1e-6 or angle >= 6.283184482025146 else angle / pi
        times[i + 1] = times[i] + (beats + extra[i]) * crotchet / speed[i]
        beats_at[i + 1] = beats_at[i] + beats + extra[i]

    def target(pair, floor):
        v, rel = pair
        return min(n - 1, max(0, v + (floor if rel == "ThisTile" else n - 1 if rel == "End" else 0)))

    # RepeatEvents lookup is per source floor and tag; last declaration wins.
    repeat = {}
    for _, e in enumerate(actions):
        if e["eventType"] == "RepeatEvents" and e.get("active", True):
            for tag in e.get("tag", "").split(" "):
                repeat[e["floor"], tag] = e
    rows = []
    keys = ("positionOffset", "rotationOffset", "scale", "opacity", "trackStyle", "trackColorType", "trackColor", "secondaryTrackColor", "trackTexture", "trackTextureScale", "trackGlowIntensity", "ease")
    for i, e in enumerate(actions):
        if not e.get("active", True) or e["eventType"] not in ("MoveTrack", "RecolorTrack"):
            continue
        floor = e["floor"]
        rep = next((repeat[floor, t] for t in e.get("eventTag", "").split(" ") if (floor, t) in repeat), None)
        count = (rep.get("repetitions", 0) if rep["repeatType"] == "Beat" else rep["floorCount"]) if rep else 0
        for k in range(count + 1):
            repeated_floor = floor + (k * rep.get("gapLength", 1) if rep and rep["repeatType"] == "Floor" else 0)
            if repeated_floor >= n:
                break
            on_current = rep and rep.get("executeOnCurrentFloor", False)
            effective_floor = repeated_floor if on_current else floor
            if rep and rep["repeatType"] == "Beat" and rep["interval"] > 0:
                repeat_offset = f(f(f(rep["interval"]) * k) * 180)
            else:
                repeat_offset = f(0 if on_current else (beats_at[repeated_floor] - beats_at[floor]) * 180)
            offset = f(f(e.get("angleOffset", 0)) + repeat_offset)
            # ffxPlusBase.SetStartTime uses float degree-to-radian arithmetic.
            delta = f(f(f(offset * rad) / f(pi)) * f(60 / f(bpm * speed[effective_floor])))
            start = times[effective_floor] + delta
            if start > 15:
                continue
            s, t = sorted((target(e["startTile"], effective_floor), target(e["endTile"], effective_floor)))
            rows.append(dict(kind=e["eventType"], source_index=i, repeat=k, floor=effective_floor,
                             start_s=start, apply_at_s=max(0, start), phase="scrub0" if start <= 0 else "play",
                             start_tile=s, end_tile=t, step=1 + e.get("gapLength", 0),
                             duration_s=f(f(e.get("duration", 0)) * f(60 / f(bpm * speed[effective_floor]))),
                             manual=bool(e.get("eventTag", "")), values={key: e[key] for key in keys if key in e}))

    # AnimateTrack is preparation state, not a StartEffect. It creates per-tile
    # appear/disappear effects; FloorSetup executes for every appear at scrub0.
    anim, disappear = settings["trackAnimation"], settings["trackDisappearAnimation"]
    ahead, behind = settings["beatsAhead"], settings["beatsBehind"]
    anim_speed, old_anim_speed, anim_used = 1.0, 1.0, True
    position_scale, position_opacity = 1.0, 1.0
    prep, initial = [], []
    generated = []
    for floor in range(n):
        next_scale, next_opacity = position_scale, position_opacity
        for i, e in by_floor[floor]:
            typ = e["eventType"]
            if typ in ("PositionTrack", "AnimateTrack", "ColorTrack", "Hide"):
                prep.append(dict(source_index=i, floor=floor, floor_entry_s=times[floor],
                                 skipped_in_game=bool(e.get("editorOnly", False)), event=e))
            if typ == "AnimateTrack":
                anim_used = "trackAnimation" in e
                anim = e.get("trackAnimation", anim)
                disappear = e.get("trackDisappearAnimation", disappear)
                if anim_used:
                    ahead = e.get("beatsAhead", ahead)
                if "trackDisappearAnimation" in e:
                    behind = e.get("beatsBehind", behind)
                old_anim_speed, anim_speed = anim_speed, speed[floor]
            if typ == "PositionTrack" and not e.get("editorOnly", False):
                position_scale = e.get("scale", position_scale * 100) / 100
                position_opacity = e.get("opacity", position_opacity * 100) / 100
                if not e.get("justThisTile", False):
                    next_scale, next_opacity = position_scale, position_opacity
        initial.append(dict(floor=floor, scale=position_scale, opacity=position_opacity, appear=anim, disappear=disappear))
        beat = f(60 / f(bpm * speed[floor]))
        speed_ratio = f(speed[floor] / (anim_speed if anim_used else old_anim_speed))
        tile_ahead, tile_behind = f(ahead * speed_ratio), f(behind * speed_ratio)
        if anim != "None":
            start = max(f(f(times[floor]) - f(tile_ahead * beat * (2 if anim in ("Drop", "Rise") else 1))), 0)
            dur = f(beat * tile_ahead) if anim in ("Drop", "Rise") else min(f(beat * .5), .5)
            if start <= 15:
                generated.append(dict(kind="Appear", floor=floor, start_s=start, duration_s=dur, animation=anim,
                                      initial_scale=position_scale, initial_opacity=position_opacity))
        if disappear != "None" and floor < n - 1:
            # ffxFloorDisappearPlus.SetStartTime, checked against decompiled source.
            start = f(f(times[floor + 1]) + f(tile_behind * beat))
            if start <= 15:
                generated.append(dict(kind="Disappear", floor=floor, start_s=start, duration_s=min(f(beat * .5), .5), animation=disappear))
        position_scale, position_opacity = next_scale, next_opacity
    # Scrub0 iterates floors and attached components BEFORE final time sorting.
    scrub = sorted((r for r in rows if r["phase"] == "scrub0"), key=lambda r: (r["floor"], r["source_index"], r["repeat"]))
    play = sorted((r for r in rows if r["phase"] == "play"), key=lambda r: (r["start_s"], r["floor"], r["source_index"], r["repeat"]))
    summary = dict(map=str(path), sha256=hashlib.sha256(raw).hexdigest(), tiles=n, actions=len(actions),
                   clock="songposition_minusi; source-derived, not measured; 100% pitch, countdownTicks=4",
                   audio_countdown_offset_s=crotchet * settings["countdownTicks"],
                   scrub0_counts=dict(collections.Counter(r["kind"] for r in scrub)),
                   play_0_15_counts=dict(collections.Counter(r["kind"] for r in play)),
                   generated_counts=dict(collections.Counter(r["kind"] for r in generated)),
                   manual_rows=sum(r["manual"] for r in rows),
                   selected_floor_times={i: times[i] for i in (0, 1, 70, 120, 213, 330, 521, 565, 967)},
                   notes=["Preparation entries apply before playback, including late floors.",
                          "All future Fade FloorSetup calls also write opacity=0 during scrub0.",
                          "Rows describe schedule eligibility, not actual runtime frames or visual parity.",
                          "Absent JSON properties are disabled; not zero-valued targets."])
    out.mkdir(parents=True, exist_ok=True)
    (out / "timeline.json").write_text(json.dumps(dict(summary=summary, scrub0=scrub, play=play,
                                                      generated=generated, preparation=prep, initial=initial), ensure_ascii=False, indent=2), encoding="utf-8")
    with (out / "timeline.tsv").open("w", encoding="utf-8-sig", newline="") as file:
        fields = list(rows[0])
        writer = csv.DictWriter(file, fields, delimiter="\t")
        writer.writeheader()
        for row in scrub + play:
            writer.writerow(dict(row, values=json.dumps(row["values"], ensure_ascii=False)))
    (out / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--map", type=Path, default=DEFAULT)
    parser.add_argument("--out", type=Path, default=ROOT / "out/silentroom-tiles")
    args = parser.parse_args()
    analyze(args.map, args.out)
