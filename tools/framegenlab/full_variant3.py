"""Four fresh-process whole-song trials of the selected lazy block matcher; no capture."""
import argparse
import json
import subprocess
import sys
from pathlib import Path
from summarize_validation25 import full_song

HERE=Path(__file__).resolve().parent


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--chapter',default='ch26')
    p.add_argument('--hello',type=Path,required=True)
    p.add_argument('--arche',type=Path,required=True)
    p.add_argument('--resume',action='store_true')
    a=p.parse_args()
    if not a.hello.is_file() or not a.arche.is_file(): p.error('Both exact map paths must exist')
    out=HERE/'results'/f'{a.chapter}-full-variant3'
    out.mkdir(exist_ok=a.resume)
    result={}
    for name,map_path in [('hello',a.hello),('arche',a.arche)]:
        for mode in [2,4]:
            label=f'{a.chapter}-{name}-{mode}-full'
            if a.resume and (HERE/'results'/label/'summary.json').exists():
                result[label]=full_song(label)
                continue
            command=[sys.executable,str(HERE/'measure_outside.py'),'--map',str(map_path),'--label',label,
                     '--mode',str(mode),'--seconds','600','--full-song','--block-flow','--block-variant','3',
                     '--out',str(HERE/'results'/label),'--timeout-min','15']
            print('START '+label,flush=True)
            previous=out/f'{label}.log'
            if previous.exists(): previous.rename(out/f'{label}.preflight.log')
            done=subprocess.run(command,capture_output=True,text=True,encoding='utf-8',errors='replace')
            (out/f'{label}.log').write_text(done.stdout+done.stderr,encoding='utf-8')
            if done.returncode: raise RuntimeError('Whole-song trial failed; preserve evidence: '+label)
            row=full_song(label)
            result[label]=row
            (out/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
            print(json.dumps(row,ensure_ascii=False),flush=True)


if __name__=='__main__': main()
