"""Summarize preserved evidence without starting games or changing raw logs."""
import hashlib,json,re,statistics
from pathlib import Path

ROOT=Path('E:/StutterFixWork')
WT=ROOT/'wt-partial'
RAW=ROOT/'night-results'
OUT=WT/'docs/deco-spec/results/evidence/cpu-extreme'
OUT.mkdir(parents=True,exist_ok=True)

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def extract(name):
    p=RAW/(name+'.log')
    lines=p.read_text(encoding='utf-8',errors='replace').splitlines()
    keep=[x for x in lines if any(k in x for k in ('[FPS 구간]','cost scope=manual','BARRIER_NOTIFY',
        'WORKLOAD','flash_fixture','effect_cache_fixture','[idlefloor]','[holdreads]','live_state',
        '[framecmp] diff ','framecmp RESULT','framecmp VERDICT','단계 실패','effect_exception','원본 처리로 복귀'))
        and '시작:' not in x and '임무단계' not in x]
    (OUT/(name+'.txt')).write_text('\n'.join(keep)+'\n',encoding='utf-8')
    result={'raw_log':str(p),'raw_sha256':sha(p),'selected_log':name+'.txt'}
    integrity=RAW/(name+'.integrity.json')
    if integrity.exists():result['integrity']=json.loads(integrity.read_text(encoding='utf-8'))
    fps=[float(m.group(1)) for x in keep if (m:=re.search(r'\[FPS 구간\].* : ([\d.]+) FPS',x))]
    if fps:result['fps']=fps[-1];result['frame_ms']=1000/fps[-1]
    manual=next((x for x in keep if 'cost scope=manual' in x),None)
    if manual:result['managed_elapsed_ms']=float(re.search(r'\btotal_ms=([\d.]+)',manual).group(1))
    return result

names=['cpu-flash-smoke','cpu-flash-smoke2','cpu-idle-oracle','cpu-ar262-cand','cpu-native-symbol']
names += [f'cpu-idle-exit-{i}' for i in range(4)]
names += [f'cpu-hold-exit-{i}' for i in range(5)]
names += [f'cpu-flash-wf-{i}' for i in range(4)]
names += [f'cpu-notify-cost-{i}' for i in range(4)]
names += ['cpu-notify-'+k for k in ('exit','ar','dd')]
names += ['cpu-notify-fixed-'+k for k in ('exit','ar','dd')]
names += ['cpu-admission-'+k for k in ('exit','ar','wf')]
names += ['cpu-profile-'+k for k in ('ar','wf')]+['cpu-notify-exit-diagnose']
runs={n:extract(n) for n in names if (RAW/(n+'.log')).exists()}
groups={}
for name,ids,a,b in [('idle',[f'cpu-idle-exit-{i}' for i in range(4)],[0,3],[1,2]),
                   ('hold',[f'cpu-hold-exit-{i}' for i in range(1,5)],[0,3],[1,2]),
                   ('flash',[f'cpu-flash-wf-{i}' for i in range(4)],[0,3],[1,2]),
                   ('notify',[f'cpu-notify-cost-{i}' for i in range(4)],[0,3],[1,2])]:
    values=[runs[n]['frame_ms'] for n in ids]
    groups[name]={'runs':ids,'fps':[runs[n]['fps'] for n in ids],
        'off_ms':statistics.mean(values[i] for i in a),'on_ms':statistics.mean(values[i] for i in b),
        'off_range_ms':abs(values[a[0]]-values[a[1]]),'on_range_ms':abs(values[b[0]]-values[b[1]])}
    groups[name]['delta_ms']=groups[name]['on_ms']-groups[name]['off_ms']
    if name=='notify':
        groups[name]['off_managed_elapsed_ms']=statistics.mean(runs[ids[i]]['managed_elapsed_ms'] for i in a)
        groups[name]['on_managed_elapsed_ms']=statistics.mean(runs[ids[i]]['managed_elapsed_ms'] for i in b)

integrity=list(RAW.glob('cpu-*.integrity.json'))
restored=[json.loads(p.read_text(encoding='utf-8')) for p in integrity]
assert all(r['restored_dll'] and r['probe_absent'] and r['gpu_default_zero'] for r in restored)
final=[runs['cpu-notify-fixed-'+k]['integrity']['dll_sha256'] for k in ('exit','ar','dd')]
final += [runs[f'cpu-notify-cost-{i}']['integrity']['dll_sha256'] for i in range(4)]
assert len(set(final))==1
for name in [f'cpu-notify-cost-{i}' for i in range(4)]:
    txt=(OUT/(name+'.txt')).read_text(encoding='utf-8')
    assert 'live_state active=True' in txt and 'native_errors=0' in txt and '원본 처리로 복귀' not in txt
assert final[0]==sha(WT/'bin/PlayerAuto/StutterFix.dll')
artifacts={str(p):sha(p) for p in (RAW/'cpu-ar262-cand-artifacts').glob('*.png')}
summary={'groups':groups,'runs':runs,'preserved_integrity_records':len(integrity),
    'restoration_passed':len(restored),'final_autotest_sha256':final[0],
    'player_sha256':sha(WT/'bin/Player/StutterFix.dll'),'cand_pixel_artifacts':artifacts}
(OUT/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
for n in ('cpu-native-symbol.txt','cpu-player-check.txt','cpu-collapse-check.txt'):
    (OUT/n).write_bytes((RAW/n).read_bytes())
print(json.dumps({'groups':groups,'integrity':len(integrity),'same_final_dll':True},indent=2))
