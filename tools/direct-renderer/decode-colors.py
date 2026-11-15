"""Decode every nativeCB slot using stage/buffer offsets, never raw CB offsets.

Reads existing colors TSV only; never runs Unity or writes to the installation.
Material values absent from old captures cannot be reconstructed: use the new
companion .constants.tsv for live typed Unity API/packet comparisons.
"""
import argparse,base64,csv,json,re,struct
from pathlib import Path
root=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('input',type=Path)
p.add_argument('--output',type=Path,default=root/'out/direct-renderer/m4g/decoded-colors.tsv')
args=p.parse_args()
catalog=json.loads((root/'out/direct-renderer/m4bc/shaders/bindings.json').read_text(encoding='utf-8'))
shaders={s['name']:s for s in catalog['shaders']}
fields=['renderer','path','shader','material_slot_inferred','pass','native_id','variant','stage','cb','cb_slot','cb_size','cb_offset','packet_offset','variable','value','status']
args.output.parent.mkdir(parents=True,exist_ok=True)
slots=variables=ambiguous=unmapped=0;clue=[]
with args.input.open(encoding='utf-8-sig',newline='') as inp,args.output.open('w',encoding='utf-8',newline='') as out:
    writer=csv.DictWriter(out,fields,delimiter='\t');writer.writeheader()
    for row in csv.DictReader(inp,delimiter='\t'):
        for match in re.finditer(r'pass=(\d+):id=(\d+):grab=[^:;]+:bytes=([^;]+);',row['nativeCB']):
            slots+=1;pass_id,native_id,data=match.groups();data=base64.b64decode(data,validate=True)
            shader=shaders.get(row['shader']);ps=next((p for p in shader['passes'] if p['index']==int(pass_id)),None) if shader else None
            if ps is None:unmapped+=1;continue
            active=set() if row['keywords']=='-' else set(row['keywords'].split(','))
            candidates=[v for v in ps['variants'] if 16+sum(b['size'] for st in ('vs','ps') for b in v[st]['cbs'])==len(data)]
            exact=[v for v in candidates if set(v['keywords'])==active]
            if exact:candidates=exact
            if not candidates:unmapped+=1;continue
            def layout(v):return [(st,b['slot'],b['size'],[(x['name'],x['offset'],x['rows'],x['cols'],x['type']) for x in b['variables']]) for st in ('vs','ps') for b in v[st]['cbs']]
            if any(layout(v)!=layout(candidates[0]) for v in candidates[1:]):ambiguous+=1;continue
            variant=candidates[0];offset=16
            for st in ('vs','ps'):
                for cb in variant[st]['cbs']:
                    for v in cb['variables']:
                        count=v['rows']*v['cols'];at=offset+v['offset'];assert at+count*4<=offset+cb['size']
                        value=struct.unpack_from('<'+('i' if v['type']==1 else 'f')*count,data,at);variables+=1
                        record=dict(renderer=row['id'],path=row['path'],shader=row['shader'],material_slot_inferred='old capture omits slot',
                                    **{'pass':pass_id},native_id=native_id,variant=','.join(variant['keywords']),stage=st,cb=cb['name'],cb_slot=cb['slot'],cb_size=cb['size'],
                                    cb_offset=v['offset'],packet_offset=at,variable=v['name'],value=json.dumps(value),status='decoded; Unity value not captured')
                        writer.writerow(record)
                        if 'space-art-5626853' in row['path'] and row['shader']=='Legacy Shaders/Particles/Additive' and v['name'] in ('_TintColor','_MainTex_ST'):clue.append(record)
                    offset+=cb['size']
            assert offset==len(data)
summary=dict(slots=slots,variables=variables,ambiguous=ambiguous,unmapped=unmapped,hello_clue=clue,
             material_comparison='Unavailable in old capture; new ColorAudit companion reads typed Unity APIs independently',game_run=False)
args.output.with_suffix('.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
if clue:
    assert any(r['variable']=='_MainTex_ST' and r['packet_offset']==64 and json.loads(r['value'])==[1,1,0,0] for r in clue)
    assert any(r['variable']=='_TintColor' and r['packet_offset']==672 and json.loads(r['value'])==[.5,.5,.5,.5] for r in clue)
print(f'DECODE slots={slots} variables={variables} ambiguous={ambiguous} unmapped={unmapped} output={args.output}')
for r in clue:print(r['stage'],r['variable'],'cb_offset='+str(r['cb_offset']),'packet_offset='+str(r['packet_offset']),r['value'])
