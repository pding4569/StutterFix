"""Summarize existing evidence only; never launch a game or change settings."""
import hashlib
import json
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
FPS=re.compile(r'\[FPS 구간\] (.*?) : ([\d.]+) FPS \((\d+)프레임 ([\d.]+)초\) \| GPU ([\d.]+) CPU메인 ([\d.]+) 렌더스레드 ([\d.]+) 현재대기 ([\d.]+)')
rows=[]
for folder in sorted((ROOT/'out').glob('continue-20261110-*-wf-*')):
    if not (folder/'Player.log').exists():continue
    log=(folder/'Player.log').read_text(encoding='utf-8',errors='replace')
    plan=json.loads((folder/'plan.json').read_text(encoding='utf-8'))
    restore=json.loads((folder/'restore.json').read_text(encoding='utf-8')) if (folder/'restore.json').exists() else None
    candidate=folder/'candidate.dll'
    row=dict(name=folder.name,path=str(folder.relative_to(ROOT)),
             candidate_sha256=hashlib.sha256(candidate.read_bytes()).hexdigest(),
             normal_quit='[자동 시험] 끝 - 게임을 끕니다' in log,
             command_failed='단계 실패' in log or '오류로 그만둠' in log,
             restore_exact=restore['equal'] if restore else False,
             restored_files=len(restore['before']) if restore else 0,
             environments=[l for l in log.splitlines() if '[곡 시작] 화면:' in l],
             song_targets=[s for s in plan['steps'] if s.startswith('waitsongpos ')],
             fps_windows=[dict(label=m[1],fps=float(m[2]),frames=int(m[3]),seconds=float(m[4]),
                               engine_gpu_ms=float(m[5]),engine_cpu_main_ms=float(m[6]),render_ms=float(m[7]),present_wait_ms=float(m[8])) for m in FPS.finditer(log)],
             array_payload_bytes=[int(x) for x in re.findall(r'DRAWCOLLAPSE_PREPARED .*? bytes=(\d+)',log)],
             array_plans=[l for l in log.splitlines() if 'IMAGEARRAY_PLAN ' in l],
             payload_audit=[l for l in log.splitlines() if 'IMAGEARRAY_AUDIT' in l or 'audit_samples=' in l],
             diagnostic_states=[l for l in log.splitlines() if 'COLLAPSE_DIAG ' in l],
             song_summaries=[l for l in log.splitlines() if '[곡]' in l or '곡 끝남 (' in l or 'OnLandOnPortal' in l],
             lifecycle=[l for l in log.splitlines() if 'IMAGEARRAY_RELEASE ' in l or 'shutdown_disposed=' in l or 'on=False owned=0 originals_restored=' in l],
             hitch_records=[l for l in log.splitlines() if '[모니터]' in l and ' | 곡 ' in l],
             original_filter_divide_by_zero=log.count('DivideByZeroException: Attempted to divide by zero.'),
             present_order_warnings=log.count('PresentFrame() was called without'),
             settings=dict(EngineWaitSkip=plan['settings'].get('EngineWaitSkip'),ImageMaxSide=plan['settings'].get('ImageMaxSide')))
    if (folder/'pixels.json').exists():row['pixels']=json.loads((folder/'pixels.json').read_text(encoding='utf-8'))
    if folder.name.endswith('wf-collapse-cost'):row['limitation']='First diagnostic restoration failed: restore enabled=0 instead of174; final comparison excluded. Fixed diagnostic snapshots in subsequent run.'
    if folder.name.endswith('-full'):
        row['stability_only']=True
        row['portal_completed']='[임무완주] OnLandOnPortal' in log and '곡 끝남 (' in log
        row['waitend_timeout']='곡이 끝나지 않음' in log
        stops=[l for l in log.splitlines() if 'on=False owned=0 originals_restored=' in l]
        row['early_gpu_stop']=not stops or any('originals_restored=True reason=Application.quitting ' not in l for l in stops)
        row['metrics']=json.loads((folder/'metrics.json').read_text(encoding='utf-8'))
    rows.append(row)
result=dict(scope='Local research, no adoption/release. Payload bytes are format/mip payload, not physical VRAM residency. FPS is Unity frame rate, not physical display FPS.',
            memory_payload=dict(reference_bytes=2723528704,leased_bytes=656904192,
                                reduction_percent=100*(1-656904192/2723528704),
                                full_resolution=True,original_textures_retained=True),
            conclusions=dict(fps_improvement='UNCONFIRMED: cache ABBA has large ON variation',
                             full_song_visual_parity='UNCONFIRMED: leased-bank transparent controls at95/258 changed0pixels',
                             gpu_payload='8 initialized lease samples,83320832bytes matched;2 scratch-only controls changed4bytes and restored0',
                             gpu_wait_cause='UNCONFIRMED; PerfView unavailable',
                             fps400='NOT_ACHIEVED',research_default_off=True),runs=rows)
result['conclusions']['late_pair']=dict(song_window_seconds=[270,282],off_fps=315.0,on_fps=322.5,
                                       change_percent=100*(322.5/315-1),samples_per_mode=1,
                                       alias_copies_in_on_window=0,full_song_copy_stall_cause='UNCONFIRMED',acceptance=False)
builds={}
for label in ['dev','player','auto']:
    path=ROOT/'out'/('array-audit-'+label+'-build.txt');raw=path.read_bytes()
    text=raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
    assert '오류 0개' in text
    builds[label]=dict(errors=0,log=str(path.relative_to(ROOT)))
result['validation']=dict(builds=builds,loadcheck=dict(DEV=299,Player=246,PlayerAuto=299,errors=0,dynamic_runtime_refs_per_build=7),
                          offline_policy_checks=59,offline_policy_errors=0,ordinary_player_audit_and_diag_methods=False,
                          git_diff_check='PASS',installed_originals_current='9/9 exact',branch='claude/adofai-renderer',head='63968c1',
                          main_push_release_zip=False)
destination=ROOT/'out/continue-20261110-imagearray-result.json'
destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(destination)
print(json.dumps(dict(runs=len(rows),normal_quit=sum(r['normal_quit'] for r in rows),restored=sum(r['restore_exact'] for r in rows)),ensure_ascii=False))
