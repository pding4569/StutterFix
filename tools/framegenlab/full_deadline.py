"""Chapter29 whole-song trials; four fresh games, no images or PresentMon/elevation."""
import argparse, json, re, subprocess, sys
from pathlib import Path
from analyze_full import analyze_full

HERE=Path(__file__).resolve().parent


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--hello',type=Path,required=True)
    p.add_argument('--arche',type=Path,required=True)
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--settings',type=Path,required=True)
    p.add_argument('--resume',action='store_true')
    p.add_argument('--analyze-only',action='store_true',help='Require all four saved trials; never launch a game')
    a=p.parse_args()
    if a.analyze_only:
        if not all((a.out/f'{name}-{mode}x'/'summary.json').is_file() for name in ['hello','arche'] for mode in [9,4]):
            p.error('Analyze-only requires four completed saved trials')
        a.resume=True
    a.out.mkdir(parents=True,exist_ok=a.resume)
    result=dict(scope='Four fresh whole-song games, no capture/PresentMon. GPU new-picture counters remain limited to5..45s; diagnostic motion capacity65536 does not cover all later frames.',runs=[])
    condition=build=None
    for name,map_path in [('hello',a.hello),('arche',a.arche)]:
        for mode in [9,4]:
            root=a.out/f'{name}-{mode}x';label='ch29-full-'+root.name
            if not (a.resume and (root/'summary.json').exists()):
                command=[sys.executable,str(HERE/'measure_outside.py'),'--map',str(map_path),'--out',str(root),
                    '--label',label,'--mode','4','--seconds','600','--full-song','--block-flow','--block-variant','3',
                    '--fixed-cost-stage','1','--settings',str(a.settings.resolve()),'--timeout-min','15']
                if mode==9:command+=['--refresh']
                print('START '+label,flush=True)
                with (a.out/(root.name+'.log')).open('w',encoding='utf8') as log:
                    done=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
                if done.returncode:raise RuntimeError('Whole-song trial failed; preserve evidence: '+label)
            data=json.loads((root/'summary.json').read_text(encoding='utf8'))
            log=(root/'game.log').read_text(encoding='utf8')
            conditions=re.findall(r'\[곡 시작\] 화면: ([^\r\n]+)',log)
            if len(conditions)!=1 or not all(s in conditions[0] for s in ['3440x1440 165Hz','창 3440x1440','수직동기 0']):
                raise RuntimeError('Actual whole-song screen conditions changed')
            if condition is not None and conditions[0]!=condition:raise RuntimeError('Do not combine different screen conditions')
            if build is not None and data['build']!=build:raise RuntimeError('Binary changed between whole-song trials')
            condition=conditions[0];build=data['build']
            closing=dict(x.split('=',1) for x in (root/'capture/screen-end.txt').read_text().split())
            if any(int(closing[k])!=v for k,v in dict(window_width=3440,window_height=1440,display_width=3440,display_height=1440,sync=0).items()) or round(float(closing['refresh_hz']))!=165:
                raise RuntimeError('End-of-trial display/window conditions changed; preserve data')
            full=analyze_full(root,begin=0)
            row=dict(map=name,mode=mode,conditions=condition,build=build,safety=data['safety'],game_metrics=data['game_metrics'],
                closing_conditions=closing,completion_confirmed='곡 끝남' in (root/'run.txt').read_text(encoding='utf8'),full_native=full)
            result['runs'].append(row)
            (a.out/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
            print(json.dumps({k:v for k,v in row.items() if k!='full_native'},ensure_ascii=False),flush=True)


if __name__=='__main__':main()
