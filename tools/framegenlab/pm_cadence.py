"""PresentMon CSV -> 체감 문제를 보는 지표 (진짜 FPS 가 주사율보다 낮을 때의 프레임 생성).

  py -3 pm_cadence.py <csv> [--hz 165] [--from 8] [--to 40] [--json out.json]

화면 표시 시각(QPCTime + msUntilDisplayed)으로 만든 표시 사건만 본다(Dropped=1 은 화면에 안 나온 것).
 - 제출 수 / 표시 수: Present 호출 수와 실제로 화면 갱신에 쓰인 수
 - 표시 간격 분포: 한 주사율 칸(1/hz) 단위로 1칸·2칸·3칸 이상 (고르게 새 그림이 나가면 1칸이 대부분)
 - 칸 채움률: 구간 안의 주사율 칸 중 새 표시가 한 번이라도 있던 칸의 비율 (반복 표시 = 체감 FPS 가 낮음)
 - 최악 간격, 99% 간격
"""
import argparse, csv, json, statistics as st


def load(path, t0, t1):
    rows = []
    with open(path, encoding="utf-8-sig", newline="") as f:
        for r in csv.DictReader(f):
            if not r.get("Application", "").startswith("A Dance"):
                continue
            try:
                row = dict(t=float(r["TimeInSeconds"]), dropped=int(r["Dropped"]),
                           between=float(r.get("msBetweenPresents") or r.get("MsBetweenPresents") or 0),
                           until=float(r.get("msUntilDisplayed") or r.get("MsUntilDisplayed") or 0),
                           disp=float(r.get("msBetweenDisplayChange") or r.get("MsBetweenDisplayChange") or 0),
                           mode=r.get("PresentMode", ""), tear=int(r.get("AllowsTearing", 0)))
            except (KeyError, ValueError):
                continue
            rows.append(row)
    if not rows:
        raise SystemExit("게임 행이 없음")
    # 맵 불러오기·메뉴 구간을 빼고, 마지막으로 0.5초 넘게 비었던 뒤부터를 재생으로 본다
    start = 0
    for i in range(1, len(rows)):
        if rows[i]["t"] - rows[i - 1]["t"] > 0.5:
            start = i
    rows = rows[start:]
    base = rows[0]["t"]
    return [r for r in rows if t0 <= r["t"] - base <= t1]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("csv")
    p.add_argument("--hz", type=float, default=165)
    p.add_argument("--from", dest="t0", type=float, default=0)
    p.add_argument("--to", dest="t1", type=float, default=1e9)
    p.add_argument("--json")
    a = p.parse_args()
    rows = load(a.csv, a.t0, a.t1)
    span = rows[-1]["t"] - rows[0]["t"]
    shown = [r for r in rows if not r["dropped"] and r["until"] > 0]
    slot = 1000.0 / a.hz
    # 표시 시각 = 제출 시각 + 화면까지 지연
    times = sorted(r["t"] * 1000 + r["until"] for r in shown)
    gaps = [b - a_ for a_, b in zip(times, times[1:])]
    bins = {"1칸 이하": 0, "2칸": 0, "3칸 이상": 0}
    for g in gaps:
        k = g / slot
        bins["1칸 이하" if k < 1.5 else "2칸" if k < 2.5 else "3칸 이상"] += 1
    nslots = max(1, int(span * a.hz))
    filled = len(set(int(t / slot) for t in times))
    out = dict(
        seconds=round(span, 2), submitted=len(rows), submitted_fps=round(len(rows) / span, 1),
        dropped=sum(r["dropped"] for r in rows), shown=len(shown), shown_fps=round(len(shown) / span, 1),
        slot_fill_pct=round(100 * filled / nslots, 1), gap_bins=bins,
        gap_p50_ms=round(st.median(gaps), 2) if gaps else None,
        gap_p99_ms=round(sorted(gaps)[int(len(gaps) * .99)], 2) if gaps else None,
        gap_max_ms=round(max(gaps), 2) if gaps else None,
        modes={m: sum(1 for r in rows if r["mode"] == m) for m in set(r["mode"] for r in rows)},
        tearing=sum(r["tear"] for r in rows),
    )
    print(json.dumps(out, ensure_ascii=False, indent=2))
    if a.json:
        open(a.json, "w", encoding="utf-8").write(json.dumps(out, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
