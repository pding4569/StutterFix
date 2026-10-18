"""Read-only aggregation of independent Editor GPU4 outputs; writes handoff JSON only."""
from pathlib import Path
import csv, json, collections, re, hashlib, os
root=Path(__file__).resolve().parents[2]
base=root/'docs/handoff'
folder=base/os.environ.get('SF_GPU4_RESULT','codex-tight-20261018-fixture-final')
dxtfolder=base/'codex-tight-20261018-fixture-dxtgpu'
def tsv(name,where=None):
    p=(where or folder)/name
    return list(csv.DictReader(p.open(encoding='utf-8-sig'),delimiter='\t')) if p.exists() else []
raw=tsv('validation.tsv');pngv=[x for x in raw if x['input']!='cap1024_dxt'];dxtv=tsv('validation.tsv',dxtfolder)
v=pngv+dxtv;g=[x for x in tsv('geometry.tsv') if x['input']!='cap1024_dxt']+tsv('geometry.tsv',dxtfolder)
alpha=[x for x in tsv('alpha.tsv') if x['input']!='cap1024_dxt']+tsv('alpha.tsv',dxtfolder);dif=tsv('differences.tsv')+tsv('differences.tsv',dxtfolder)
summary={'fixture':str(folder),'dxt_fixture':str(dxtfolder),'completed':(folder/'done.txt').exists() and (dxtfolder/'done.txt').exists(),'png_count':len(pngv),'dxt_count':len(dxtv),'superseded_cpu_dxt_rows':len(raw)-len(pngv),'validation_count':len(v),'mismatches':sum(int(x['diff_bytes'])>0 for x in v),'repeat_mismatches':sum(int(x['repeat_bytes'])>0 for x in v),'invisible':sum(int(x['visible_bytes'])==0 for x in v),'max_error':max((int(x['max_error']) for x in v),default=None),'groups':{},'alpha':alpha,'geometry':g,'difference_samples':dif[:20]}
for method in ['bbox','grid16','tight']:
    a=[x for x in v if x['method']==method]
    summary['groups'][method]={'tested':len(a),'mismatch':sum(int(x['diff_bytes'])>0 for x in a),'max_error':max((int(x['max_error']) for x in a),default=None),'first_fail':next((x for x in a if int(x['diff_bytes'])>0),None)}
from PIL import Image
sources={}
for name in ['fog.png','nes_black_fog.png','nes_large_fog.png','nev_fog.png']:
    p=Path('D:/얼불춤 맵 파일/Arche')/name;im=Image.open(p).convert('RGBA');pixels=list(im.getdata());hist=collections.Counter(x[3] for x in pixels)
    sources[name]={'sha256_png':hashlib.sha256(p.read_bytes()).hexdigest(),'dimensions':im.size,'alpha_histogram':dict(sorted(hist.items())),'rgba_bottom_up_sha256':hashlib.sha256(im.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()).hexdigest()}
summary['source_png']=sources
inside=[]
for row in dif:
    if row['condition'].startswith('fog.png|original|False|Bilinear|2|grid16|3440') and int(row['nearest_alpha'])>8:
        x,y=map(int,row['local_texel'].split(','));im=Image.open(Path('D:/얼불춤 맵 파일/Arche/fog.png')).getchannel('A');y=im.height-1-y
        inside.append(dict(row,alpha_min_9x9=min(im.crop((x-4,y-4,x+5,y+5)).getdata())))
        if len(inside)==3:break
summary['interior_samples']=inside
census=Path.home()/'AppData/Local/StutterFix/gpu-census-20261018-220315-835.tsv'
camera='';cameras={};header=None
for line in census.read_text(encoding='utf-8-sig').splitlines():
    if line.startswith('# camera='):
        camera=line.split(' enabled=')[0][9:];cameras[camera]={'fog_aabb_pixels':collections.Counter(),'group_count':0,'renderer_count':collections.Counter(),'draw_count':collections.Counter(),'textures':set()}
    elif line.startswith('count\taabb_pixels'):header=line.split('\t')
    elif line.startswith('draw\t'):
        cols=line.split('\t');image=next((n for n in ['nes_large_fog.png','nes_black_fog.png','nev_fog.png','fog.png'] if cols[15].startswith(n+'#')),None)
        if image:cameras[camera]['draw_count'][image]+=1;cameras[camera]['textures'].add(cols[15])
    elif header and line and not line.startswith('#') and line.split('\t')[0].isdigit():
        row=dict(zip(header,line.split('\t')));texture=row.get('primaryTexture','')
        image=next((n for n in ['nes_large_fog.png','nes_black_fog.png','nev_fog.png','fog.png'] if texture.startswith(n+'#')),None)
        if image:cameras[camera]['fog_aabb_pixels'][image]+=float(row['aabb_pixels']);cameras[camera]['renderer_count'][image]+=int(row['count']);cameras[camera]['group_count']+=1
for camera,row in cameras.items():row['textures']=sorted(row['textures']);row['fog_area_screens']=sum(row['fog_aabb_pixels'].values())/(3440*1440)
summary['census']={'sha256':hashlib.sha256(census.read_bytes()).hexdigest(),'cameras':cameras,'note':'Grouped clipped world AABB area, not actual pixels. No per-renderer transforms.'}
out=base/'codex-tight-20261018-summary.json';out.write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:summary[k] for k in ['completed','validation_count','mismatches','repeat_mismatches','invisible','max_error','groups']},ensure_ascii=False,indent=2))
print('Census',json.dumps({k:{a:b for a,b in x.items() if a!='textures'} for k,x in cameras.items()},ensure_ascii=False))
