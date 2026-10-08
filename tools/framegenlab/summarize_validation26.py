"""Chapter26 cost split, selected variant3 whole-song trials and ordinary-build checks."""
import hashlib
import json
import re
from pathlib import Path
from summarize_validation25 import full_song
from measure_outside import read

HERE=Path(__file__).resolve().parent


def main():
    cost=json.loads((HERE/'results/ch26-cost/summary.json').read_text(encoding='utf-8'))
    full={name:full_song(name) for name in [f'ch26-{song}-{mode}-full' for song in ['hello','arche'] for mode in [2,4]]}
    for name,row in full.items():
        if not isinstance(row,dict): continue
        root=HERE/'results'/name
        log=(root/'game.log').read_text(encoding='utf-8')
        conditions=re.findall(r'\[곡 시작\] 화면: 수직동기 (\d+), 목표 FPS (\d+), (\w+), (\d+)x(\d+) (\d+)Hz, 창 (\d+)x(\d+)',log)
        if not conditions or list(conditions[-1])!=list(cost['conditions'].values()): raise RuntimeError('Full-song actual screen conditions changed: '+name)
        if row['build']!=cost['build']: raise RuntimeError('Full-song build differs from selected candidate: '+name)
        row['missing_camera_song_s']=[float(r['song_s']) for r in read(root/'capture/sources.csv') if float(r['song_s'])>=5 and r['scene_rendered']=='0']
        row['true_camera_callback_rows']=row['source_rows']-row['missing_camera_callbacks']
    ordinary={}
    for name in ['off','switch','monitor','fsr']:
        p=HERE/f'results/ch26-normal-{name}/summary.json'
        ordinary[name]=json.loads(p.read_text(encoding='utf-8')) if p.exists() else '확인 안 됨'
    performance=HERE/'results/ch26-arche-normal/summary.json'
    normal_perf=json.loads(performance.read_text(encoding='utf-8')) if performance.exists() else '확인 안 됨'
    build_path=HERE/'validation-26/build.json'
    build_checks=json.loads(build_path.read_text(encoding='utf-8')) if build_path.exists() else '확인 안 됨'
    mod=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix')
    restore={n:hashlib.sha256((mod/n).read_bytes()).hexdigest() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    zips={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in HERE.parents[1].glob('dist/*test*.zip')}
    result=dict(conditions=cost['conditions'],cost_split=cost,whole_song_variant3=full,ordinary=ordinary,ordinary_performance=normal_perf,build_checks=build_checks,
                restored_files=restore,existing_test_zips=zips,actual_3440='확인 안 됨',physical_display_latency='확인 안 됨',
                whole_song_pixel_quality='확인 안 됨',whole_song_fps_loss_without_full_off='확인 안 됨')
    out=HERE/'validation-26';out.mkdir(exist_ok=True)
    (out/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(cost=[{k:r[k] for k in ['mode','real_scene_fps','loss_vs_off_percent']} for r in cost['runs']],
                          whole_song={n:{k:r[k] for k in ['completed','missing_camera_callbacks','failed_present']} if isinstance(r,dict) else r for n,r in full.items()},
                          ordinary={n:isinstance(r,dict) for n,r in ordinary.items()},restored=restore),ensure_ascii=False),flush=True)


if __name__=='__main__': main()
