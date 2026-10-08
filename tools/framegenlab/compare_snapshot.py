"""Compare same-source world/screen readbacks, outside the performance window.

The small world texture is bilinearly resampled for a CPU approximation of the
shader's UI classifier. This is not a pixel-exact GPU classifier measurement.
"""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageChops


def compare(folder):
    world = Image.open(folder/'snapshot-world.ppm').convert('RGB')
    screen = Image.open(folder/'snapshot-screen.ppm').convert('RGB')
    original_size = world.size
    # The tested packet uses flip=1. Do not choose the orientation with best results.
    world = world.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    if world.size != screen.size:
        world = world.resize(screen.size, Image.Resampling.BILINEAR)
    red, green, blue = ImageChops.difference(world, screen).split()
    delta = ImageChops.lighter(ImageChops.lighter(red, green), blue)
    w, h = delta.size
    histogram = delta.histogram()
    different = sum(histogram[3:])
    center = delta.crop((w//4, h//4, 3*w//4, 3*h//4))
    return dict(world_size=original_size, screen_size=screen.size, flip_y=True,
                cpu_classifier_approximation=True, threshold_rgb_8bit=2.5,
                different_pixels=different, pixels=w*h,
                different_percent=different*100/(w*h),
                center_different_percent=sum(center.histogram()[3:])*100/(center.width*center.height),
                mean_max_rgb_difference=sum(i*n for i,n in enumerate(histogram))/(w*h))


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--before', type=Path, required=True)
    p.add_argument('--after', type=Path, required=True)
    p.add_argument('--out', type=Path, required=True)
    a = p.parse_args()
    result = dict(before=compare(a.before), after=compare(a.after))
    if result['after']['world_size'] != result['after']['screen_size']:
        raise RuntimeError('Snapshot pixel grid still differs from output')
    a.out.write_text(json.dumps(result, indent=2)+'\n', encoding='utf8')
    print(json.dumps(result, indent=2))
