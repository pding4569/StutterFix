"""PresentMon 2.x CSV 에서 "놓친 갱신"(표시 변화 간격이 한 주사율 칸보다 훨씬 긴 것)을 구간별로 센다.

  py -3 pm_missed.py <csv> [<csv> ...] [--win 15] [--hz 165] [--from 0] [--to 9999]

- 표시 변화 간격 MsBetweenDisplayChange 가 1.5칸을 넘으면 놓친 갱신으로 센다 (칸 하나가 같은 그림 반복).
- 제출/s 는 행 수, 표시/s 는 MsBetweenDisplayChange 가 있는 행 수.
- 독점 전체 화면(Hardware: Independent Flip)은 표시=제출이라 칸 개념이 달라 놓침은 제출 간격이 1.5칸을 넘는 것으로 센다.
CSV 열 이름은 PresentMon 2.x(TimeInMs, MsBetweenPresents, MsBetweenDisplayChange). 1.x 열(msBetweenPresents 등)도 받는다.
"""
import argparse, csv, collections


def col(r, *names):
    for n in names:
        v = r.get(n)
        if v is not None:
            return None if v in ("NA", "") else float(v)
    return None


def load(path):
    rows = []
    with open(path, encoding="utf-8-sig", newline="") as f:
        for r in csv.DictReader(f):
            if not r.get("Application", "").startswith("A Dance"):
                continue
            t = col(r, "TimeInMs")
            t = t / 1000.0 if t is not None else col(r, "TimeInSeconds")
            rows.append((t, col(r, "MsBetweenPresents", "msBetweenPresents"),
                         col(r, "MsBetweenDisplayChange", "msBetweenDisplayChange"), r.get("PresentMode", "")))
    return rows


def analyze(path, win, hz, t_from, t_to):
    rows = load(path)
    if not rows:
        return None
    t0 = rows[0][0]
    period = 1000.0 / hz
    independent = sum(1 for r in rows if r[3].startswith("Hardware")) > len(rows) / 2
    w = collections.defaultdict(lambda: [0, 0, 0])
    for t, b, d, mode in rows:
        s = t - t0
        if s < t_from or s >= t_to:
            continue
        k = int(s // win)
        w[k][0] += 1
        gap = b if independent else d
        if not independent and d is not None:
            w[k][1] += 1
        elif independent:
            w[k][1] += 1
        if gap is not None and gap > 1.5 * period:
            w[k][2] += 1
    return w, independent


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("csv", nargs="+")
    ap.add_argument("--win", type=float, default=15)
    ap.add_argument("--hz", type=float, default=165)
    ap.add_argument("--from", dest="t_from", type=float, default=0)
    ap.add_argument("--to", dest="t_to", type=float, default=1e9)
    a = ap.parse_args()
    results = []
    for p in a.csv:
        r = analyze(p, a.win, a.hz, a.t_from, a.t_to)
        results.append((p, r))
    keys = sorted({k for _, r in results if r for k in r[0]})
    head = " win |" + "".join(" %-26s|" % p.replace("\\", "/").split("/")[-1][:26] for p, _ in results)
    print(head)
    print("     |" + "".join(" subm/s shown/s missed   |" for _ in results))
    totals = [0] * len(results)
    for k in keys:
        line = "%4d |" % (k * a.win)
        for i, (p, r) in enumerate(results):
            if not r or k not in r[0]:
                line += " %-26s|" % "-"
                continue
            n, shown, miss = r[0][k]
            totals[i] += miss
            line += " %6.0f %7.0f %6d     |" % (n / a.win, shown / a.win, miss)
        print(line)
    print("sum  |" + "".join(" %-26d|" % t for t in totals))


if __name__ == "__main__":
    main()
