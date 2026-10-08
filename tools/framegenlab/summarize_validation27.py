"""Collect exact completed stage evidence; partial stages remain explicit."""
import argparse
import hashlib
import json
import re
from pathlib import Path

HERE=Path(__file__).resolve().parent
ROOT=HERE/'out/validation27'
DEST=HERE/'validation-27'


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--refresh-presentmon',action='store_true',help='Postprocess saved CSVs only, after all game trials finish')
    args=parser.parse_args()
    DEST.mkdir(exist_ok=True)
    result=dict(stages={},fixtures={},scope='Actual3440x1440; one ordered OFF/connected/4x per cost stage, HELLO OFF/2/4. Arche2 has an explicit separate new closing OFF; earlier cost stages have no closing OFF. Physical display is ETW, not optical or input latency.')
    for label in ['baseline-qpc','rotate','query','split','early','early-fixed','hello','arche2','arche2off']:
        path=ROOT/label/'summary.json'
        if path.is_file():
            data=json.loads(path.read_text(encoding='utf-8'))
            data['complete']=len(data['runs'])==len(data['order'])
            for i,row in enumerate(data['runs']):
                run=ROOT/label/f'{i:02d}-{row["mode"]}x'
                detail=json.loads((run/'summary.json').read_text(encoding='utf-8'))
                if detail['native']:
                    for field in ['generated_gpu_ms','missing_camera_callbacks','output_worst_ms']:
                        row[field]=detail['native'][field]
                if args.refresh_presentmon:
                    from measure_fixed_cost import pm_summary
                    row['presentmon']=pm_summary(run)
            result['stages'][label]=data
            manifest={}
            for run in (ROOT/label).glob('*x'):
                for name in ['presentmon.csv','game.log','run.txt','capture/cost-sources.csv','capture/cost-state.txt','capture/sources.csv','capture/presents.csv','capture/ownership.txt','capture/safety.txt','capture/native-init.txt']:
                    p=run/name
                    if p.is_file(): manifest[str(p.relative_to(ROOT))]=dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
            (DEST/(label+'-manifest.json')).write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    for n in [1,2,3,4]:
        cases={}
        for flavor in ['research','normal']:
            p=ROOT/f'fixture{n}-{flavor}.txt'
            if p.is_file():
                text=p.read_text(encoding='utf-8-sig')
                rows=[r for r in text.splitlines() if r.startswith('movement=')]
                cases[flavor]=dict(conditions=len(rows),pixel_ui_state_failures=sum(not all(v in r for v in ['errors_above3=0','ui_errors=0','state_errors=0']) for r in rows))
                old=(HERE/'validation-26'/f'{flavor}-fixture.txt').read_text(encoding='utf-8-sig')
                old_rows=[r for r in old.splitlines() if r.startswith('movement=')]
                cases[flavor]['same_rows_as_ch26']=rows==old_rows
                (DEST/p.name).write_text(text,encoding='utf-8')
        p=ROOT/f'fixture{n}-handoff.txt'
        if p.is_file():
            text=p.read_text(encoding='utf-8-sig');cases['handoff']=text.strip()
            (DEST/p.name).write_text(text,encoding='utf-8')
        if cases: result['fixtures'][str(n)]=cases
    if 'arche2off' in result['stages'] and 'arche2' in result['stages']:
        reference=result['stages']['arche2off']['runs'][0]
        for row in result['stages']['arche2']['runs']:
            row['loss_against_older_off_percent']=row['loss_percent']
            row['loss_percent']=100*(1-row['real_fps']/reference['real_fps'])
        result['stages']['arche2']['reported_reference']='New closing OFF arche2off, recorded after the remaining2x run; older OFF preserved separately'
    (DEST/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for stage,data in result['stages'].items():
        print(stage,[(r['mode'],round(r['real_fps'],2),round(r['loss_percent'],2)) for r in data['runs']])


if __name__=='__main__': main()
