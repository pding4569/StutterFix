"""dupcap CSV: 이웃한 두 갱신의 변화량 비(1=고름). 낮을수록 진짜 프레임에서만 크게 움직이는 것."""
import csv, statistics as st, sys
col = 'mad'
for fn in sys.argv[1:]:
    if fn.startswith('--col='): col = fn[6:]; continue
    rows = list(csv.DictReader(open(fn)))[1:]
    m = [float(r[col]) for r in rows]; t = [float(r['qpc_s']) for r in rows]
    pairs = [min(m[i], m[i-1]) / max(m[i], m[i-1]) for i in range(1, len(m)) if (t[i]-t[i-1])*1000 < 8 and max(m[i], m[i-1]) > 0.3]
    span = t[-1] - t[0]
    new = sum(1 for x in m if x > 0.05)
    print('%-34s 갱신 %.0f/s 새 그림 %.0f/s | 고름 중앙 %.2f, 0.4 미만 %.0f%% | 갱신당 변화 중앙 %.2f' % (
        fn.split('/')[-1], len(m)/span, new/span, st.median(pairs), 100*sum(1 for p in pairs if p < .4)/len(pairs), st.median([x for x in m if x > 0.05])))
