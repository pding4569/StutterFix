"""Separate visual/functional run. Never use its frame times as performance data."""
import argparse,hashlib,json,re,shutil,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'framegenlab'))
from measure_maps import sf,ROOT

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    if sf.game_running():raise RuntimeError('Close the existing game normally first')
    a.out.mkdir(parents=True,exist_ok=False);mod=Path(sf.MOD_DIR)
    original={n:(mod/n).read_bytes() for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
    names=['fx-color','fx-off','fx-sharp','fx-aa','fx-glow','fx-light','fx-settings','fx-fsr','fx-half']
    files=[mod/'shots'/(n+'.png') for n in names]+[mod/'shots'/n for n in ['shader-inventory.txt','fx-input.png','fx-output.png','identity16.png']]
    shots={p:p.read_bytes() if p.exists() else None for p in files}
    try:
        (mod/'shots').mkdir(exist_ok=True);(mod/'shots/identity16.png').write_bytes((ROOT/'effects/identity16.png').read_bytes())
        (mod/'StutterFix.dll').write_bytes((ROOT/'bin/PlayerAuto/StutterFix.dll').read_bytes())
        steps=['wait 3','fxstate','shaders','fxfixture','game D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai','auto on','press','wait 12',
          'set FxColor true','wait 2','fxstate','fxcapture','shot fx-color','wait 1','ui 8','wait 2','shot fx-settings','wait 1','ui close',
          'set FxColor false','wait 2','fxstate','shot fx-off','wait 1']
        for setting,name in [('FxSharp','sharp'),('FxAA','aa'),('FxGlow','glow'),('FxLight','light')]:
            steps+=['set '+setting+' true','wait 3','fxstate','shot fx-'+name,'wait 1','set '+setting+' false']
        steps+=['set LowRenderScale 67','set LowFsr true','set FxColor true','wait 3','fxstate','shot fx-fsr','wait 1',
          'set LowFsr false','set LowRenderScale 100','set LowHalfRender true','wait 3','fxstate','shot fx-half','wait 1',
          'set LowHalfRender false','set FxColor false','wait 2','fxstate','quit']
        summary,metrics=sf.sf_run(steps,settings={'FrameGenOutside':0,'FrameStats':False,'FxColor':False,'FxSharp':False,'FxAA':False,'FxGlow':False,'FxLight':False,'FxVignette':False,'FxLut':False,'LowSharpen':False,'LowHalfRender':False},tag='effects-visual',timeout_min=5)
        log=sf.read_text(sf.PLAYER_LOG);(a.out/'game.log').write_text(log,encoding='utf-8');(a.out/'run.txt').write_text(summary,encoding='utf-8')
        for f in files:
            if f.exists():shutil.copyfile(f,a.out/f.name)
        states=re.findall(r'\[화면효과상태\] ([^\r\n]+)',log)
        fixture=re.findall(r'\[화면 효과 fixture\] ([^\r\n]+)',log)
        if metrics['errors'] or any(x in log for x in ['[화면 효과] 실패:','단계 실패','Crash!!!','native failure']) or 'mismatch=0' not in str(fixture):raise RuntimeError('Effects visual validation failed; preserve logs')
        if not states or 'enabled=False ready=False' not in states[0] or 'enabled=False ready=False' not in states[-1]:raise RuntimeError('Default/OFF material cleanup not observed')
        (a.out/'summary.json').write_text(json.dumps(dict(states=states,fixture=fixture,game_metrics=metrics,scope='Visual/functional only; no performance inference'),ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps(dict(states=states,fixture=fixture,errors=metrics['errors']),ensure_ascii=False))
    finally:
        if sf.game_running():sf.sf_quit()
        if sf.game_running():raise RuntimeError('Normal game shutdown required before restoration')
        for n,b in original.items():(mod/n).write_bytes(b)
        for path,b in shots.items():
            if b is None:path.unlink(missing_ok=True)
            else:path.write_bytes(b)
        assert all((mod/n).read_bytes()==b for n,b in original.items())
        print('Installed DLL/native/settings and overwritten shots restored exactly')
if __name__=='__main__':main()
