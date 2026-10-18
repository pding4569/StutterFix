from pathlib import Path
import csv,collections,json
O=Path('docs/handoff')
def load(d,n):return list(csv.DictReader((O/d/n).open(encoding='utf-8-sig'),delimiter='\t'))
summary={}
for folder,key in [('codex-gpu3-20261018-fixture','rgba'),('codex-gpu3-20261018-fixture-dxt','editor_dxt')]:
 r=load(folder,'sampling.tsv')
 def stat(rows):return {'cases':len(rows),'failed':sum(int(x['different_pixels'])>0 for x in rows),'changed_pixels_sum':sum(int(x['different_pixels']) for x in rows),'max_byte_error':max((int(x['max_byte_error']) for x in rows),default=0),'blank':sum(int(x['covered_pixels'])==0 for x in rows),'source_mutations':sum(x['original_texture_unchanged']!='True' for x in rows),'repeat_fail':sum(int(x['same_state_repeat_diff'])>0 for x in rows)}
 result={'total':stat(r),'by_padding':{p:stat([x for x in r if x['padding']==p]) for p in ['1','4','16']},'by_mips':{m:stat([x for x in r if x['mip']==m]) for m in sorted({x['mip'] for x in r})},'formats':dict(collections.Counter(x['input_format'] for x in r))}
 result['non_mip_by_padding']={p:stat([x for x in r if x['mip']=='1' and x['padding']==p]) for p in ['1','4','16']}
 result['actual_compressed']=stat([x for x in r if x['input_format'].startswith('DXT')])
 result['non_mip_pot']={p:stat([x for x in r if x['mip']=='1' and x['pot']==p]) for p in ['False','True']}
 summary[key]=result
manifest=json.loads((O/'codex-gpu3-20261018-scan-manifest.json').read_text(encoding='utf8'))
summary['il']={'dll_files':len(manifest),'managed_scanned':sum(x['exit']==0 for x in manifest),'excluded':[{k:x[k] for k in ['path','exit','error']} for x in manifest if x['exit']!=0],'references':sum(x['references'] for x in manifest)}
(O/'codex-gpu3-20261018-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({k:v['total'] for k,v in summary.items() if 'total' in v},indent=2));print(summary['rgba']['non_mip_by_padding']);print(summary['editor_dxt']['formats'])
