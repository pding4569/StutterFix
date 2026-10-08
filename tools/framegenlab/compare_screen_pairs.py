"""Visual-only source/screen pixel check; never launch during a performance trial."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
ROOT=HERE/'out/validation27'
DEST=HERE/'validation-27'


def main():
    DEST.mkdir(exist_ok=True)
    labels=[('pair-query','2: UI는 Present에서 복사'),('pair-split','3a 실패: UI를 너무 일찍 복사'),('pair-early','3b: 게임 그림만 먼저 그리기')]
    result=dict(scope='Separate fresh Arche4x runs, ~song20s;3440x1440 originals. Flip worldY, center60%, per-channel2.5/255. Diagnostic GPU readbacks invalidate performance and pacing; still images do not verify motion.',runs={})
    canvas=Image.new('RGB',(2064,408),'#101012');draw=ImageDraw.Draw(canvas)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
    draw.text((12,6),'Arche · 4배 · 곡20초 부근 · 실제3440×1440의 별도 정지 캡처',font=font,fill='white')
    for index,(name,label) in enumerate(labels):
        root=ROOT/name/'capture'
        world=np.asarray(Image.open(root/'snapshot-world.ppm'))[::-1].astype(np.int16)
        screen=np.asarray(Image.open(root/'snapshot-screen.ppm')).astype(np.int16)
        h,w=world.shape[:2]
        delta=np.abs(world-screen).max(2)
        center=delta[h//5:h-h//5,w//5:w-w//5]
        result['runs'][name]=dict(size=[w,h],world_flip_y=True,matching_percent=float((delta<=2.5).mean()*100),center_matching_percent=float((center<=2.5).mean()*100),mae=float(np.abs(world-screen).mean()),geometry=(root/'pair-geometry.txt').read_text())
        draw.text((index*688+12,41),label,font=font,fill='white')
        canvas.paste(Image.fromarray(screen.astype(np.uint8)).resize((688,288)),(index*688,76))
    draw.text((12,371),'축소 정지 표본 · 원본 쌍 시각은 약간 다름 · 성능/움직임 검증 자료와 분리',font=font,fill='#aaaaaa')
    canvas.save(DEST/'arche-ui-boundary-song20.png')
    (DEST/'screen-boundary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print({k:r['center_matching_percent'] for k,r in result['runs'].items()})


if __name__=='__main__': main()
