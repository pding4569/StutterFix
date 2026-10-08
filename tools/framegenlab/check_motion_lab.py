"""Independent byte check of the lab's GPU decimation, mask, UI and vertical flip."""
import argparse,json
from pathlib import Path
from PIL import Image

def check(folder):
    output={}
    for mode in [0,2,4]:
        expected=[]
        for y in range(144//6):
            for x in range(256//6):
                px,py=x*6,y*6
                v=(px*37217+py*159733+19)&0xffffffff;v^=v>>13;v=(v*1274126177)&0xffffffff;v^=v>>16
                g=40+v%140; color=(g,g,g)
                if mode:
                    if not (24<=px<256-24 and 12<=py<144-12):color=(0,0,0)
                    if 8<=px<20 and 6<=py<18:color=(255,0,0)
                expected.extend(color)
        names=[f"clip-{mode}x-000-{'real' if mode==0 else 'generated'}.ppm",f'reference-{mode}x-000.ppm',f'next-reference-{mode}x-000.ppm']
        for name in names:
            image=Image.open(folder/name).convert('RGB')
            assert image.size==(42,24),(name,image.size)
            assert image.tobytes()==bytes(expected),name
        output[str(mode)]=dict(pixels=1008,all_three_exact=True,mask=bool(mode),ui_after_mask=bool(mode),flip_y=mode==4)
    return output

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('folder',type=Path);p.add_argument('--out',type=Path);a=p.parse_args()
    result=check(a.folder);data=json.dumps(result,indent=2)+'\n';print(data)
    if a.out:a.out.write_text(data)
