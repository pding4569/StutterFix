"""Generate original-DXBC test cases; no D3D/game/installation calls."""
import json, math, struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'out/direct-renderer/m4bc'
audit=json.loads((OUT/'shaders/bindings.json').read_text(encoding='utf-8'))
data=bytearray()
def integer(x):data.extend(struct.pack('<I',x))
def blob(b):integer(len(b));data.extend(b)
def text(s):blob(s.encode())
def floats(v):data.extend(struct.pack('<'+'f'*len(v),*v))
def identity():return [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
cases=[]
for shader in audit['shaders']:
    if shader['name'] not in ('Sprites/Tile','ADOFAI/FloorMesh'):continue
    for variant in shader['passes'][0]['variants']:
        for mode in range(4):
            for sampler in (0,1):cases.append((shader,variant,mode,sampler))
integer(0x34575244);integer(len(cases));manifest=[]
for case_index,(shader,variant,mode,sampler) in enumerate(cases):
    floor=shader['name']=='ADOFAI/FloorMesh';snap='PIXELSNAP_ON' in variant['keywords'];point='USE_POINT_SAMPLING' in variant['keywords']
    label=f"{'floor' if floor else 'tile'}-{'-'.join(variant['keywords']) or 'base'}-mode{mode}-sampler{sampler}"
    values={p['m_Name']:[p[f'm_DefValue[{i}]'] for i in range(4)] for p in shader['properties']}
    values.update(_Color=[.73,.51,.89,.62],RepeatX=[1.7],RepeatY=[.65],_FlashColor=[.19],_MainTex_TexelSize=[1/8,1/8,8,8],
                  _TileTex_ST=[1.2,.8,.41,.57],_Tint=[.81,.64,.47,.74],_ShadowColor=[.15,.26,.42,.68],
                  _IconColor=[.8,.5,.7,.66],_CommentColor=[.12,.3,.2,.23],_UV2Offset=[.04,-.06,.17,0],
                  _Flash=[.21],_Alpha=[.82],_IconAngle=[.37],_Active=[1 if mode!=3 else 0],_TileScale=[.91],
                  _Inset=[.17],_AbsoluteInset=[mode%2],_PerlinStrength=[.13],_ShadowTune=[.78],_PerlinScale=[1.4],
                  _TextureRatio=[1.12],_IconScale=[.73],_IconFlipped=[mode%2],_Sprite=[1 if mode==2 else 0])
    angle=.23;world=[math.cos(angle)*.83,math.sin(angle)*.83,0,0,-math.sin(angle)*1.07,math.cos(angle)*1.07,0,0,0,0,1,0,.071,-.083,.5,1]
    vp=identity();vp[0]=.91;vp[5]=-.87;vp[12]=-.037;vp[13]=.043
    screen=[192,128,1+1/192,1+1/128]
    values.update(unity_ObjectToWorld=world,unity_MatrixVP=vp,_ScreenParams=screen)
    constants=bytearray(struct.pack('<4f',1,1,1,1));buffers=[];patches=[];textures=[]
    for st_index,st in enumerate(('vs','ps')):
        stage=variant[st]
        for cb in stage['cbs']:
            offset=len(constants);constants.extend(bytes(cb['size']));buffers.append((st_index,cb['slot'],cb['size'],offset))
            for var in cb['variables']:
                n=var['rows']*var['cols'];value=values.get(var['name'],[0]*n)
                assert len(value)>=n,(var['name'],value,n)
                struct.pack_into('<'+('i' if var['type']==1 else 'f')*n,constants,offset+var['offset'],*value[:n])
                kind={'unity_ObjectToWorld':1,'unity_MatrixVP':3,'_ScreenParams':9}.get(var['name'])
                if kind:patches.append((offset+var['offset'],kind))
        for t in stage['textures']:
            pattern={'_MainTex':0,'_PerlinTex':1,'_TileTex':2,'_IconTex':3,'_IconOutlineTex':4}[t['name']]
            textures.append((st_index,t['slot'],t['sampler'],pattern))
    text(label)
    for x in (int(floor),int(snap),int(point),mode,sampler):integer(x)
    for st in ('vs','ps'):blob((OUT/'shaders'/(variant[st]['sha256']+'.dxbc')).read_bytes())
    s=shader['passes'][0]['state'];state=[]
    for k in ('srcBlend','destBlend','srcBlendAlpha','destBlendAlpha','blendOp','blendOpAlpha','colMask'):state.append(int(s['rtBlend0'][k]['val']))
    for k in ('zClip','zTest','zWrite','culling','offsetFactor','offsetUnits','alphaToMask','stencilReadMask','stencilWriteMask','stencilRef'):state.append(int(s[k]['val']*(1000000 if k=='offsetFactor' else 1)))
    for face in ('stencilOp','stencilOpFront','stencilOpBack'):
        for k in ('pass','fail','zFail','comp'):state.append(int(s[face][k]['val']))
    for x in state:integer(x)
    integer(len(buffers))
    for b in buffers:
        for x in b:integer(x)
    integer(len(patches))
    for b in patches:
        for x in b:integer(x)
    blob(constants);integer(len(textures))
    for b in textures:
        for x in b:integer(x)
    ref=world+vp+screen+values['_Color']+[values['RepeatX'][0],values['RepeatY'][0],values['_FlashColor'][0],0]
    for name in ('_TileTex_ST','_Tint','_ShadowColor','_IconColor','_CommentColor','_UV2Offset'):ref+=values[name]
    for names in (('_Flash','_Alpha','_IconAngle','_Active'),('_TileScale','_Inset','_AbsoluteInset','_PerlinStrength'),('_ShadowTune','_PerlinScale','_TextureRatio','_IconScale')):ref += [values[n][0] for n in names]
    ref += [values['_IconFlipped'][0],values['_Sprite'][0],0,0]
    blob(struct.pack('<'+'f'*len(ref),*ref));floats(world);floats(vp);floats(screen)
    manifest.append(dict(case=label,shader=shader['name'],keywords=variant['keywords'],values=values))
(OUT/'warp-cases.bin').write_bytes(data)
(OUT/'warp-cases.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('WARP case inputs',len(cases),'bytes',len(data))
