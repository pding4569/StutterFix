import json,re,hashlib
from pathlib import Path
R=Path('E:/StutterFixWork/StutterFix');out=R/'tools/effects/validation-15';out.mkdir(exist_ok=True)
result={'scope':'Six fresh normal PlayerAuto games, fixed FrameGen4x, no capture/readback/ETW. FX OFF/strong-neon6/OFF per map. Unity frame / Stopwatch QPC35s after wait10, not exact native5..45s, scene callback coverage unmeasured. Incremental shader cost only, not total FrameGen loss.','maps':{}}
for name,folder in [('HELLO 2026','ch15-hello-neon6-cli2'),('Arche','ch15-arche-neon6-cli')]:
 base=R/'tools/effects/out'/folder;data=json.loads((base/'summary.json').read_text(encoding='utf8'))
 for row in data['runs']:
  log=(base/row['profile']/'game.log').read_text(encoding='utf8')
  row['fx_clock_endpoints']=re.findall(r'\[화면효과시각\] ([^\r\n]+)',log)
  row['game_log_sha256']=hashlib.sha256((base/row['profile']/'game.log').read_bytes()).hexdigest()
  row['existing_dotween_summary']=re.findall(r'- (\d+) (missing target or field errors|startup errors)',log)
 result['maps'][name]=data
result['excluded']=[{'folder':'ch15-hello-neon6','cause':'Steam ShowInterstitials user response pending; launch timeout90s; samples0'},{'folder':'ch15-hello-neon6-cli','cause':'Prior delayed launch was already running; rejected before installing; samples0'}]
result['unmeasured']=['Physical display FPS and intervals','Whole-frame GPU execution','Added display/input-to-photon latency','Effect execution fraction between endpoints','Whole-song motion quality','Comparison against FrameGen OFF','Physical keyboard/mouse entry']
(out/'strong-neon-cost.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({name:{'fps':[r['sample']['real_fps'] for r in d['runs']],'loss':d['effect_real_fps_loss_percent_vs_controls']} for name,d in result['maps'].items()},ensure_ascii=False))
