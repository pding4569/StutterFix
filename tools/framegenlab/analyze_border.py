"""Offline, approximate shader reconstruction from a same-source visual readback.

No runtime instrumentation. PPMs are quantized; CSV poses are rounded. This
measures one saved GPU sample, not motion quality or unseen scene contents.
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

from measure_outside import read


def source_uv(width, height, old, camera, flip=True):
    y, x = np.mgrid[:height, :width].astype(np.float32)
    x = ((x + .5) / width - .5) * (width / height) * 2 * camera[2]
    y = (.5 - (y + .5) / height) * 2 * camera[2]
    c, s = math.cos(camera[3]), math.sin(camera[3])
    wx, wy = c*x-s*y+camera[0]-old[0], s*x+c*y+camera[1]-old[1]
    c, s = math.cos(-old[3]), math.sin(-old[3])
    u = .5 + (c*wx-s*wy) / (2*old[2]*(width/height))
    v = .5 - (s*wx+c*wy) / (2*old[2])
    return u, 1-v if flip else v


def sample(image, u, v, clamp=False):
    h, w = image.shape[:2]
    x, y = u*w-.5, v*h-.5
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x-x0, y-y0
    result = np.zeros((*u.shape, 3), np.float32)
    for dx, dy, weight in [(0,0,(1-fx)*(1-fy)), (1,0,fx*(1-fy)),
                            (0,1,(1-fx)*fy), (1,1,fx*fy)]:
        ix, iy = x0+dx, y0+dy
        valid = (ix>=0)&(ix<w)&(iy>=0)&(iy<h)
        value = image[np.clip(iy,0,h-1), np.clip(ix,0,w-1)]
        result += value * (weight if clamp else weight*valid)[...,None]
    return result


def analyze(folder):
    images = [np.asarray(Image.open(folder/name).convert('RGB')).astype(np.float32)
              for name in ['snapshot-world.ppm','snapshot-screen.ppm','early-generated.ppm']]
    world, screen, actual = images
    if any(im.shape != world.shape for im in images):
        raise RuntimeError('Expected equal full-resolution snapshots')
    # This readback trigger is the first generated render at song >=20s. Existing
    # early-pair was prediction-only: delayed mode saves a different image slot.
    pair_path=folder/'pair-pose.json'
    if pair_path.exists():
        pair=json.loads(pair_path.read_text())
    else:
        rows = read(folder/'presents.csv')
        row = next(r for r in rows if r['real']=='0' and float(r['song_s'])>=20)
        source = next(r for r in read(folder/'sources.csv') if r['unity_frame']==row['unity_frame'])
        pose = lambda r: [float(r['camera_'+key]) for key in ['x','y','size','angle']]
        pair=dict(source_camera=pose(source),display_camera=pose(row),flip_y=True,
                  source_frame=int(row['unity_frame']),display_frame=int(row['unity_frame']),song_s=float(row['song_s']))
    if not pair['flip_y']: raise RuntimeError('This identity classifier requires tested flip=1')
    old, camera = pair['source_camera'],pair['display_camera']
    h, w = world.shape[:2]
    u, v = source_uv(w,h,old,camera)
    identity = world[::-1]  # Tested packet flip=1; do not optimize orientation to fit.
    ui = np.max(np.abs(screen-identity),axis=2)>2.5
    border, clamp = sample(world,u,v), sample(world,u,v,clamp=True)
    reconstructed = np.where(ui[...,None],screen,border)
    error = np.max(np.abs(reconstructed-actual),axis=2)
    outside = (u<0)|(u>1)|(v<0)|(v>1)
    unmasked = outside & ~ui
    actual_dark = np.max(actual,axis=2)<=3
    # Require agreement with the GPU and a bright counterfactual edge sample;
    # this excludes the map's already-black letterbox in this single sample.
    attributable = unmasked & actual_dark & (np.max(clamp,axis=2)>8) & (error<=3)
    edge = np.zeros((h,w),bool)
    edge[:h//20]=True; edge[-h//20:]=True
    edge[:,:w//20]=True; edge[:,-w//20:]=True
    moved_dark = edge & ~outside & ~ui & actual_dark & (np.max(identity,axis=2)>8) & (error<=3)
    def black_bands(image):
        dark=np.max(image,axis=2)<=3
        # Central half avoids corner HUDs. A run can still include map graphics:
        # do not call it a viewport or letterbox without separate evidence.
        left=np.argmax(~dark[h//4:3*h//4],axis=1)
        right=np.argmax(~dark[h//4:3*h//4,::-1],axis=1)
        top=np.argmax(~dark[:,w//4:3*w//4],axis=0)
        bottom=np.argmax(~dark[::-1,w//4:3*w//4],axis=0)
        return {key:float(np.median(value)) for key,value in
                [('left',left),('right',right),('top',top),('bottom',bottom)]}
    def count(mask): return dict(pixels=int(mask.sum()),percent=float(mask.mean()*100))
    result = dict(scope='One same-source visual-only GPU sample;not motion proof or an exact GPU mask. See trial metadata for prediction/delayed mode and capture date.',
                  flip_y=True, frame=pair['display_frame'],source_frame=pair['source_frame'],song_s=pair['song_s'],
                  source_camera=old,display_camera=camera,size=[w,h],
                  reconstruction=dict(mean_max_rgb_error=float(error.mean()),
                                      p99_max_rgb_error=float(np.percentile(error,99)),
                                      agreement_within3=count(error<=3)),
                  outside_uv=count(outside),outside_not_ui=count(unmasked),
                  outside_not_ui_actual_dark=count(unmasked&actual_dark),
                  border_black_with_bright_clamp_and_gpu_agreement=count(attributable),
                  edge_inside_uv_original_bright_to_actual_dark=count(moved_dark),
                  median_black_edge_run_px=dict(source=black_bands(identity),generated=black_bands(actual)),
                  maximum_outside_texels=float(np.maximum.reduce([-u*w,(u-1)*w,-v*h,(v-1)*h]).max()),
                  limits='Quantized PPM/rounded poses;CLAMP is an offline counterfactual only, not a proposed fix or unseen-image reconstruction. Older pairs without exact image-slot metadata must be prediction-only.')
    return result, attributable


if __name__=='__main__':
    p=argparse.ArgumentParser()
    p.add_argument('directory',type=Path)
    p.add_argument('--out',type=Path,required=True)
    a=p.parse_args()
    metadata=json.loads((a.directory.parent/'summary.json').read_text(encoding='utf8'))
    if not metadata.get('scene_pair') or (metadata.get('camera_blend') and not (a.directory/'pair-pose.json').exists()):
        raise RuntimeError('Requires an explicit pair with the exact delayed image-slot metadata')
    result,_=analyze(a.directory)
    a.out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps(result,ensure_ascii=False))
