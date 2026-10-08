"""Offline image displacement, with camera motion only as a separate hypothesis.
Stored native PPMs are step6 samples. No compressed preview pixels are used.
"""
import argparse,csv,json,math
from pathlib import Path
import numpy as np
from PIL import Image

def phase(a,b):
    a=a.astype(np.float32); b=b.astype(np.float32)
    h,w=a.shape; window=np.outer(np.hanning(h),np.hanning(w))
    fa=np.fft.fft2((a-a.mean())*window); fb=np.fft.fft2((b-b.mean())*window)
    cross=fb*np.conj(fa); cross/=np.maximum(abs(cross),1e-9)
    response=np.fft.ifft2(cross).real
    y,x=np.unravel_index(response.argmax(),response.shape)
    peak=float(response[y,x]); others=response.copy()
    for dy in range(-3,4):
        for dx in range(-3,4): others[(y+dy)%h,(x+dx)%w]=np.nan
    psr=float((peak-np.nanmean(others))/(np.nanstd(others)+1e-9))
    def sub(left,center,right):
        denom=left-2*center+right
        return float(np.clip(.5*(left-right)/denom,-.5,.5)) if abs(denom)>1e-9 else 0.
    dx=(x if x<=w//2 else x-w)+sub(response[y,(x-1)%w],peak,response[y,(x+1)%w])
    dy=(y if y<=h//2 else y-h)+sub(response[(y-1)%h,x],peak,response[(y+1)%h,x])
    return dict(dx=float(dx),dy=float(dy),length=math.hypot(dx,dy),psr=psr,peak=peak,texture_std=min(float(a.std()),float(b.std())))

def load(folder,mode):
    rows=list(csv.DictReader((folder/'clip.csv').open(newline='')))
    result=[]
    for row in rows:
        image=np.asarray(Image.open(folder/f"clip-{mode}x-{int(row['index']):03d}-{'real' if row['real']=='1' else 'generated'}.ppm").convert('RGB'),np.float32)
        gray=image@np.array([.299,.587,.114],np.float32)
        h,w=gray.shape
        result.append((row,gray))
    sources={int(r['unity_frame']):r for r in csv.DictReader((folder/'sources.csv').open(newline=''))}
    return result,sources,[w,h]

def analyze(folder,mode):
    frames,sources,size=load(folder,mode)
    values=[]
    for (before,a),(after,b) in zip(frames,frames[1:]):
        h,w=a.shape; roi=np.s_[h//5:4*h//5,w//5:4*w//5]
        r=phase(a[roi],b[roi]); r.update(index=int(after['index']),song_s=float(after['song_s']),previous_song_s=float(before['song_s']),real=after['real']=='1',previous_real=before['real']=='1')
        r['accepted']=r['psr']>=8 and r['texture_std']>=3
        if r['real'] and r['previous_real']:
            old=sources[int(before['unity_frame'])]; new=sources[int(after['unity_frame'])]
            dx=float(old['camera_x'])-float(new['camera_x']); dy=float(old['camera_y'])-float(new['camera_y'])
            angle=float(new['camera_angle']); c,s=math.cos(angle),math.sin(angle); z=float(new['camera_size'])
            # Approximate step6 pixels at the previous camera center.
            # Orthographic pixels/world = physical height / (2*size).
            x=(c*dx+s*dy)/(2*z)*1440/6
            y=-(-s*dx+c*dy)/(2*z)*1440/6
            da=math.atan2(math.sin(angle-float(old['camera_angle'])),math.cos(angle-float(old['camera_angle'])))
            dz=z/float(old['camera_size'])-1
            from analyze_border import source_uv,sample
            pose=lambda q:[float(q['camera_'+k]) for k in ['x','y','size','angle']]
            u,v=source_uv(w,h,pose(old),pose(new),flip=False)
            warped=sample(np.repeat(a[...,None],3,axis=2),u,v)[...,0]
            r.update(identity_gray_mae=float(np.abs(a[roi]-b[roi]).mean()),camera_warp_gray_mae=float(np.abs(warped[roi]-b[roi]).mean()))
            r.update(camera_center_dx=x,camera_center_dy=y,camera_center_length=math.hypot(x,y),rotation_delta_deg=math.degrees(da),zoom_delta_percent=dz*100,
                     near_translation=abs(da)<.003 and abs(dz)<.003)
        values.append(r)
    accepted=[r for r in values if r['accepted']]
    motions=[r['length'] for r in accepted]
    reversals=sum(bool(a['dx']*b['dx']+a['dy']*b['dy']<0) for a,b in zip(values,values[1:]) if a['accepted'] and b['accepted'] and a['length']>.25 and b['length']>.25)
    real=[r for r in accepted if r['real'] and r['previous_real'] and r.get('near_translation')]
    mismatch=[r for r in real if r['length']<.5 and r['camera_center_length']>2]
    return dict(scope='Existing uncompressed GPU samples, center60% ROI, adjacent raw stored frames; step6 pixel units. PSR>=8 and texture std>=3; all rejected rows retained. Readbacks affect pacing. Translation excludes >0.3% zoom or >0.172deg rotation. Not exact full-resolution motion or physical display pacing.',mode=mode,size=size,summary=dict(pairs=len(values),accepted=len(accepted),length_median=float(np.median(motions)) if motions else None,length_p95=float(np.percentile(motions,95)) if motions else None,length_max=max(motions) if motions else None,reversals_above_quarter_stored_pixel=reversals,accepted_real_translation_pairs=len(real),static_image_with_camera_moving_pairs=len(mismatch)),rows=values)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('capture',type=Path);p.add_argument('--mode',type=int,required=True);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    # Sign and subpixel conventions: independently translated synthetic texture.
    rng=np.random.default_rng(192); image=rng.normal(128,30,(80,100)); check=phase(image,np.roll(image,(2,-3),(0,1)))
    assert abs(check['dx']+3)<.1 and abs(check['dy']-2)<.1,check
    data=analyze(a.capture,a.mode);a.out.write_text(json.dumps(data,indent=2)+'\n');print(json.dumps(data['summary']))
