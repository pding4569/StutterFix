"""dupcap CSV 요약: 화면 갱신 중 새 그림 비율과 갱신마다 바뀐 양."""
import csv, sys, statistics as st
for fn in sys.argv[1:]:
    rows = list(csv.DictReader(open(fn, encoding="utf-8")))[1:]   # 첫 줄은 비교 대상이 없다
    if not rows:
        print(fn, "없음"); continue
    t = [float(r["qpc_s"]) for r in rows]; span = t[-1] - t[0]
    mad = [float(r["mad"]) for r in rows]; ch = [float(r["changed_pct"]) for r in rows]
    acc = [int(r["accumulated"]) for r in rows]
    frozen = sum(1 for m in mad if m == 0)
    nz = sorted(m for m in mad if m > 0)
    gaps = [b - a for a, b in zip(t, t[1:])]
    ups_per_s = len(rows) / span
    new_per_s = (len(rows) - frozen) / span
    print("%s\n  갱신 %d (%.1f/s)  새 그림 %.1f/s  같은 그림 %.1f%%  누락(acc>1) %d  갱신간격 p50 %.2fms p99 %.2fms max %.2fms" % (
        fn.split("/")[-1].split("\\")[-1], len(rows), ups_per_s, new_per_s, 100 * frozen / len(rows), sum(1 for a in acc if a > 1),
        1000 * st.median(gaps), 1000 * sorted(gaps)[int(len(gaps) * .99)], 1000 * max(gaps)))
    if nz:
        print("  바뀐 양(mad) 중앙 %.3f  p10 %.3f  p90 %.3f" % (st.median(nz), nz[int(len(nz) * .1)], nz[int(len(nz) * .9)]))
    # 새 그림 사이 간격(갱신 칸 수): 1이면 매 갱신이 새 그림
    newt = [x for x, m in zip(t[1:], mad) if m > 0] if False else [x for x, m in zip(t, mad) if m > 0]
    ng = [(b - a) * 1000 for a, b in zip(newt, newt[1:])]
    if ng:
        print("  새 그림 사이 간격 p50 %.2fms p90 %.2fms max %.2fms  -> 체감 FPS(중앙) %.0f" % (st.median(ng), sorted(ng)[int(len(ng) * .9)], max(ng), 1000 / st.median(ng)))
