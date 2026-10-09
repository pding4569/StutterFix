"""Two fresh ordinary PlayerAuto full songs; exact installation restore, no captures/PM."""
import argparse, hashlib, json, re, subprocess, sys
from pathlib import Path
from measure_maps import ROOT, sf


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--hello',type=Path,required=True)
    p.add_argument('--arche',type=Path,required=True)
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--steam-exe',type=Path,required=True)
    a=p.parse_args()
    if sf.game_running():p.error('Close existing game normally first')
    a.out.mkdir(parents=True,exist_ok=False)
    mod=Path(sf.MOD_DIR)
    originals={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    for n,b in originals.items():(a.out/(n+'.original')).write_bytes(b)
    cfg={k:False for k in ['FxColor','FxSharp','FxAA','FxGlow','FxLight','FxVignette','FxLut','FxGlowStack','FxToneMap','FxRays','FxStreak','FxFlare','FxChromatic','FxGrain','FxCrt','FxPixel','FxPosterize','FxBlur','LowFsr','LowSharpen','LowHalfRender']}
    cfg.update(FxPreset=0,LowRenderScale=100)
    settings=a.out/'settings.json';settings.write_text(json.dumps(cfg),encoding='utf8')
    results=[]
    try:
        (mod/'sfnative.dll').write_bytes((ROOT/'native/sfnative.dll').read_bytes())
        for name,map_path in [('hello',a.hello),('arche',a.arche)]:
            print('START '+name,flush=True)
            trial=a.out/name
            # Legacy false deliberately exercises migration: rest is integral to mode9.
            cmd=[sys.executable,str(Path(__file__).with_name('normal_smoke.py')),'--map',str(map_path),'--out',str(trial),'--mode','2','--refresh','--full-song','--no-shot','--settings',str(settings),'--steam-exe',str(a.steam_exe)]
            with (a.out/(name+'.log')).open('w',encoding='utf8') as f:
                subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,check=True)
            data=json.loads((trial/'summary.json').read_text(encoding='utf8'))
            log=(trial/'game.log').read_text(encoding='utf8')
            conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
            if len(conditions)!=1 or not all(s in conditions[0] for s in ['3440x1440 165Hz','창 3440x1440','수직동기 0']):
                raise RuntimeError('Actual screen conditions changed; preserve evidence')
            if '[상태] PlayerControl' not in log or '[상태] Won' not in log or '곡 끝남' not in (trial/'run.txt').read_text(encoding='utf8'):
                raise RuntimeError('Real playback/end missing')
            if not any('deadline=True' in s for s in data['storage_states']):
                raise RuntimeError('Deadline mode not observed')
            warnings={kind:log.count(kind) for kind in ['DOTWEEN','MissingReferenceException','NullReferenceException','AccessViolation','Crash!!!','native failure']}
            result=dict(map=name,conditions=conditions[0],completed=True,unexpected_exit=0,
                errors=data['game_metrics']['errors'],won_confirmed=True,storage_states=data['storage_states'],
                managed_sha256=data['managed_sha256'],native_sha256=data['native_sha256'],
                game_metrics=data['game_metrics'],sample35s=data['sample'],
                framegen_failure_lines=[s for s in log.splitlines() if '[프레임 늘리기]' in s and any(k in s for k in ['실패','중단','status='])],
                warnings=warnings,log_sha256=hashlib.sha256((trial/'game.log').read_bytes()).hexdigest())
            results.append(result)
            (a.out/'summary.json').write_text(json.dumps(dict(scope='General PlayerAuto full-song completion, not physical display/visual quality proof. 35s sample is not whole-song FPS.',runs=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
            print('DONE '+name+' errors='+str(result['errors']),flush=True)
    finally:
        if sf.game_running():sf.sf_quit()
        if sf.game_running():raise RuntimeError('Normal quit required before restoring bytes')
        for n,b in originals.items():(mod/n).write_bytes(b)
        assert all((mod/n).read_bytes()==b for n,b in originals.items())
        print('Exact installation originals restored',flush=True)


if __name__=='__main__':main()
