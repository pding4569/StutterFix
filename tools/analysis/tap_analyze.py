"""audiotap(AutoTest 빌드)으로 녹음한 게임 소리 출력에서 히트음 예약 묶기의 조각 이음매 '틱'을 찾는다.
   python tap_analyze.py <녹음 폴더> <이름>...   (이름.f32 + 이름.json)
   이음매 앞뒤 8샘플의 2차 차분 최댓값을, 같은 조각 안 다른 60곳의 같은 값과 비교한다.
   median_ratio ~ 1, frac_above95 ~ 0.05 이면 이음매가 다른 곳과 구별되지 않음(틱 없음). 곡 음량 0 으로 녹음(audiotap begin <초> mute).
   한계: 모드 안 진단(리스너 출력 녹음)이며 귀로 비교한 것은 아니다. 이음매에 우연히 히트음 시작이 겹치면 값이 커질 수 있다."""
import numpy as np, json, sys, os
BASE=sys.argv[1]
def load(name):
    m=json.load(open(os.path.join(BASE,name+'.json'))); x=np.fromfile(os.path.join(BASE,name+'.f32'),dtype=np.float32).reshape(-1,m['channels']); return m,x
def analyze(name, win=8, ncontrol=60):
    m,x=load(name); h=m['hit']; rate=m['rate']
    mono=x.sum(axis=1).astype(np.float64)
    d2=np.abs(np.diff(mono,2))   # d2[n] ~ mono[n+2]-2mono[n+1]+mono[n]
    n=len(d2)
    step=h['frames']/h['rate']          # 0.5 s
    first=int(np.ceil((m['t0']-h['origin']-h['lead']/h['rate']+0.3)/step))
    bsm=[]; ctl=[]
    k=first
    while True:
        tb=h['origin']+k*step+h['lead']/h['rate']
        b=int(round((tb-m['t0'])*rate))
        if b+int(0.52*rate)>=n-20: break
        bsm.append(d2[b-win:b+win].max())
        # control: same-size windows at other offsets inside this segment (avoid 10 ms around any boundary)
        offs=np.linspace(0.02,0.48,ncontrol)
        c=[d2[b+int(o*rate)-win:b+int(o*rate)+win].max() for o in offs]
        ctl.append(c)
        k+=1
    bsm=np.array(bsm); ctl=np.array(ctl)
    # per-boundary: percentile of boundary metric among that segment's controls, and excess ratio vs median control
    pct=np.array([(ctl[i]<bsm[i]).mean() for i in range(len(bsm))])
    ratio=bsm/np.maximum(np.median(ctl,axis=1),1e-9)
    rms=np.sqrt((mono**2).mean())
    return dict(name=name,segments=len(bsm),rms=round(float(rms),4),
                boundary_mean=float(bsm.mean()),control_mean=float(ctl.mean()),
                mean_pct=float(pct.mean()),frac_above95=float((pct>0.95).mean()),
                median_ratio=float(np.median(ratio)),p90_ratio=float(np.percentile(ratio,90)),max_boundary=float(bsm.max()),
                control_p99=float(np.percentile(ctl,99)))
res=[]
for n in sys.argv[2:]:
    r=analyze(n); res.append(r); print(json.dumps(r,ensure_ascii=False))
if os.environ.get('TAP_OUT'): json.dump(res,open(os.environ['TAP_OUT'],'w',encoding='utf-8'),ensure_ascii=False,indent=1)
