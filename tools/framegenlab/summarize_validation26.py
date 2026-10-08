"""Chapter26 cost split, selected variant3 whole-song trials and ordinary-build checks."""
import hashlib
import json
from pathlib import Path
from summarize_validation25 import full_song

HERE=Path(__file__).resolve().parent


def main():
    cost=json.loads((HERE/'results/ch26-cost/summary.json').read_text(encoding='utf-8'))
    full={name:full_song(name) for name in [f'ch26-{song}-{mode}-full' for song in ['hello','arche'] for mode in [2,4]]}
    ordinary={}
    for name in ['off','switch','monitor','fsr']:
        p=HERE/f'results/ch26-normal-{name}/summary.json'
        ordinary[name]=json.loads(p.read_text(encoding='utf-8')) if p.exists() else '확인 안 됨'
    mod=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix')
    restore={n:hashlib.sha256((mod/n).read_bytes()).hexdigest() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    zips={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in HERE.parents[1].glob('dist/*test*.zip')}
    result=dict(conditions=cost['conditions'],cost_split=cost,whole_song_variant3=full,ordinary=ordinary,
                restored_files=restore,existing_test_zips=zips,actual_3440='확인 안 됨',physical_display_latency='확인 안 됨',
                whole_song_pixel_quality='확인 안 됨',whole_song_fps_loss_without_full_off='확인 안 됨')
    out=HERE/'validation-26';out.mkdir(exist_ok=True)
    (out/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(cost=[{k:r[k] for k in ['mode','real_scene_fps','mean_scene_ms','loss_vs_off_percent','increment_vs_previous_stage_ms','output_submissions_per_second']} for r in cost['runs']],whole_song=full,ordinary=list(ordinary),restored=restore),ensure_ascii=False),flush=True)


if __name__=='__main__': main()
