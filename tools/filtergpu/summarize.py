from pathlib import Path
import re,json
out=Path('docs/handoff/filters-evidence')
names=['final-hello','final-windflower','arche']
allrows=[];songs=[]
for name in names:
 p=out/(name+'.log')
 if not p.exists():continue
 text=p.read_text(encoding='utf8')
 rows={}
 for m in re.finditer(r'\[filtergpu\] (Camera\S+) samples=(\d+) mean_us=([\d.]+) sum_ms=([\d.]+) frames=(\d+) total_frames=(\d+) active_seconds=([\d.]+) song_seconds=([\d.]+)',text):
  typ,n,us,ms,f,total,active,secs=m.groups()
  rows[typ]=dict(song=name,type=typ,samples=int(n),mean_call_us=float(us),sum_ms=float(ms),frames=int(f),total_frames=int(total),active_seconds=float(active),song_seconds=float(secs),active_percent=float(active)/float(secs)*100,mean_active_frame_us=float(ms)*1000/int(f))
 dist={int(a):int(b) for a,b in re.findall(r'\[filtergpu\] concurrent=(\d+) frames=(\d+)',text)}
 gpu=re.findall(r'GPU 평균 ([\d.]+)ms',text)
 fps=re.findall(r'\[곡\] 평균 (\d+) FPS',text)
 over=re.findall(r'\[임무30\] 시작 연출 뒤 30ms 초과 프레임 (\d+)개',text)
 total=max((r['total_frames'] for r in rows.values()),default=0)
 if not total:
  count=re.findall(r'\[곡\] 평균 .*?프레임 (\d+)개',text)
  total=int(count[-1]) if count else 0
 if total:dist[0]=max(0,total-sum(dist.values()))
 sumgpu=sum(r['sum_ms'] for r in rows.values())
 item=dict(song=name,completed='[임무완주]' in text,gpu_mean_ms=float(gpu[-1]) if gpu else None,fps=int(fps[-1]) if fps else None,over30=int(over[-1]) if over else None,frames=total,filter_gpu_ms=sumgpu,distribution=dist,query_status=re.findall(r'\[filtergpu\] skipped=(\d+) invalid=(\d+)',text)[-1:])
 if gpu and total:item['approx_gpu_share_percent']=sumgpu/(float(gpu[-1])*total)*100
 songs.append(item);allrows+=list(rows.values())
(out/'metrics.json').write_text(json.dumps(dict(songs=songs,filters=allrows),indent=2),encoding='utf8')
lines=['| 곡 | 필터 | 등장 시간 % | 켜진 프레임당 GPU µs | GPU 합계 ms | 표본 |','|---|---|---:|---:|---:|---:|']
for r in sorted(allrows,key=lambda r:(r['song'],-r['sum_ms'])):
 lines.append(f"| {r['song']} | {r['type'].removeprefix('CameraFilterPack_')} | {r['active_percent']:.2f} | {r['mean_active_frame_us']:.2f} | {r['sum_ms']:.3f} | {r['samples']} |")
(out/'filter-table.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
print(json.dumps(songs,indent=2))
