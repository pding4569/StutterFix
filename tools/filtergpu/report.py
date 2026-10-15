from pathlib import Path
import json,statistics as st
out=Path('docs/handoff/filters-evidence')
d=json.loads((out/'metrics.json').read_text())
q=json.loads((out/'quality-metrics.json').read_text())
assert len(d['songs'])==3 and all(x['completed'] for x in d['songs'])
assert len(q['trials'])==20
assert len(q['quality'])==5 and all(x['samples']==8 and x['control_max']==0 for x in q['quality'].values())
for t in q['trials']:
 assert t['fps'] is not None and t['gpu'] is not None and t['over30'] is not None
 if t['mode']=='B':assert t['proof'] and all(int(p[1])>0 and p[0]=='CameraFilterPack_'+t['type'] for p in t['proof'])
lines=['## 최종 집계','','전 판 D3D11, 3440×1440 165Hz, vSync 0, FPS 제한 없음, FrameGenOutside=0. 세 맵 모두 OnLandOnPortal 완주 로그 있음. 아래 FPS·GPU 평균·30ms 수는 계측 판의 관측치이며 최적화 전후 성능 수치가 아니다.','','| 곡 | 평균 FPS | 프레임 GPU 평균 ms | 30ms 초과 | CameraFilterPack GPU 합계 ms | GPU 비중 추정 |','|---|---:|---:|---:|---:|---:|']
for r in d['songs']:lines.append(f"| {r['song']} | {r['fps']} | {r['gpu_mean_ms']:.1f} | {r['over30']} | {r['filter_gpu_ms']:.3f} | {r['approx_gpu_share_percent']:.1f}% |")
lines+=['','Arche에는 CameraFilterPack 필터가 관측되지 않았다. 맵 원문에는 구형 SetFilter 이벤트 1,538개가 있으며 그 구현의 GPU 비용을 0으로 측정한 것은 아니다.','', '### 전체 GPU 합계 상위 5개','', '| 순위 | 필터 | 합계 ms |','|---:|---|---:|']
s={}
for r in d['filters']:s[r['type']]=s.get(r['type'],0)+r['sum_ms']
top=sorted(s,key=lambda k:-s[k])[:5]
for i,k in enumerate(top,1):lines.append(f"| {i} | {k.removeprefix('CameraFilterPack_')} | {s[k]:.3f} |")
lines+=['','### 같은 프레임 화질 대조','','각 8표본. 원본 반복의 최대 차이는 전 후보 0/255. 아래 PSNR은 RGBA 기준이다. 최대 차이가 큰 후보를 종합 PSNR만으로 승인하지 않았다.','','| 필터 | 최대 차이 /255 | 표본 최저 PSNR dB | 종합 PSNR dB | 채택 |','|---|---:|---:|---:|---|']
for k in top:
 r=q['quality'][k]
 lines.append(f"| {k.removeprefix('CameraFilterPack_')} | {r['max']} | {r['min_psnr']:.2f} | {r['cumulative_psnr']:.2f} | 안 함 |")
lines+=['','### 별도 성능 ABBA','','최종 근거는 abba-final.log만 사용. 상위 5개 각각 워밍업 1판을 버리고 A/B/B/A 4판, 총 20판. 같은 게임 세션, 후보마다 같은 선택 타일에서 20초씩. A=원래 경로, B=해당 필터 반해상도. GPU 쿼리 계측과 ReadPixels는 꺼져 있다. B판마다 실제 곡 중 호출 수 > 0 확인. 전체 곡 성능으로 일반화하지 않는다. Movie는 14번, BlurHole/Radial은 71번, SuperComputer는 1154번, WideScreen은 1번 타일에서 시작.','','| 필터 | A 평균 FPS | B 평균 FPS | A GPU ms | B GPU ms | A 30ms 합 | B 30ms 합 | B 호출 수 (2판) |','|---|---:|---:|---:|---:|---:|---:|---|']
for k in top:
 name=k.removeprefix('CameraFilterPack_')
 a=[r for r in q['trials'] if r['type']==name and r['mode']=='A'];b=[r for r in q['trials'] if r['type']==name and r['mode']=='B'];assert len(a)==len(b)==2;assert [r['mode'] for r in q['trials'] if r['type']==name]==['A','B','B','A']
 calls=[sum(int(p[1]) for p in r['proof']) for r in b]
 lines.append(f"| {name} | {st.mean(r['fps'] for r in a):.1f} | {st.mean(r['fps'] for r in b):.1f} | {st.mean(r['gpu'] for r in a):.2f} | {st.mean(r['gpu'] for r in b):.2f} | {sum(r['over30'] for r in a)} | {sum(r['over30'] for r in b)} | {calls[0]}, {calls[1]} |")
lines+=['','프레임 GPU 평균은 원 로그에서 0.1ms로 반올림되므로 위 평균의 두 자리 표시는 정밀도가 늘었다는 뜻이 아니다. 반해상도의 추가 축소·확대 Blit 비용도 B에 포함된다. FPS 차이만으로 화면 대기·클럭 영향을 분리하지 않는다.','','최종 채택은 **0개**. 화질 최대 차이 기준을 모두 넘었다. 새 사용자 설정은 추가하지 않았고 반해상도는 DEV/AUTOTEST 명령에서만 실행된다. 단일 Blit 제거·필터 결합은 금지된 셰이더 변경 없이 같은 결과를 증명할 대체가 없어 적용하지 않았다. 이는 다른 구현의 최적화가 불가능하다는 결론이 아니다.','','카운터 없는 quality-abba.log의 첫 Movie ABBA와 정상 종료한 abba-extra.log 준비 판은 성능 근거에서 제외한다. 최종 20판은 별도이며 중간 판을 섞어 평균하지 않았다.','','### 동시에 실행된 CameraFilterPack 수 분포','','같은 타입이 여러 카메라에 있으면 각각 셈. 각 칸은 프레임 수 (%). 마지막 집계 경계 1프레임의 불확실성은 앞의 한계 절 참고.','','| 수 | HELLO | Windflower | Arche |','|---:|---:|---:|---:|']
by={r['song']:r for r in d['songs']}
for n in sorted({int(n) for r in d['songs'] for n in r['distribution']}):
 vals=[]
 for song in ['final-hello','final-windflower','arche']:
  r=by[song];v=r['distribution'].get(str(n),0);vals.append(f"{v} ({100*v/r['frames']:.2f}%)")
 lines.append('| '+str(n)+' | '+' | '.join(vals)+' |')
lines+=['','### 필터별 전체 표','','켜진 프레임당 시간은 같은 타입의 여러 인스턴스/호출을 합친 GPU 합계 ÷ 해당 타입이 켜진 프레임 수. 호출당 평균과는 다르다. 회수 표본 기준이며 µs. 원자료는 metrics.json과 각 맵 .log.','',(out/'filter-table.md').read_text(encoding='utf8')]
p=Path('docs/handoff/filters-findings.md');base=p.read_text(encoding='utf-8-sig').split('## 최종 집계')[0]
p.write_text(base+'\n'+'\n'.join(lines)+'\n',encoding='utf-8-sig')
print('report generated; twenty ABBA trials and all five controls validated')
