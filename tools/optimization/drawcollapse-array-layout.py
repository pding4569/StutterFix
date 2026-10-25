"""외부 진단의 텍스처 목록으로 배열 추가 메모리를 재계산한다. 할당·게임 실행 없음."""
import argparse
import json
import re
from pathlib import Path


def estimate(text):
    tiers = {}
    pattern = (r'DRAWCOLLAPSE_ARRAY tier=(\d+)x(\d+)/([^/]+)/mips=(\d+)'
               r'.*?slices=(\d+).*?format=(\w+)')
    for match in re.finditer(pattern, text):
        width, height, fmt, mips, count, name = match.groups()
        key = (int(width), int(height), fmt, int(mips))
        value = (int(count), name)
        if key in tiers and tiers[key] != value:
            raise ValueError('texture population changed between diagnostic samples')
        tiers[key] = value
    if not tiers:
        raise ValueError('no texture tiers in evidence')
    arrays, direct = {}, 0
    for (width, height, fmt, mips), (count, name) in tiers.items():
        size = next((size for size in (512, 1024, 2048) if width <= size and height <= size), 0)
        if name != 'DXT5' or mips != 1 or width % 4 or height % 4:
            size = 0
        if size:
            key = f'{size}/{fmt}'
            arrays[key] = arrays.get(key, 0) + count
        else:
            direct += count
    allocated = sum(int(key.split('/')[0]) ** 2 * count for key, count in arrays.items())
    return dict(textures=sum(value[0] for value in tiers.values()), source_tiers=len(tiers),
                array_slices=arrays, direct_textures=direct, active_texture_slots=len(arrays) + direct,
                additional_array_bytes=allocated, additional_array_mib=allocated / 1048576,
                allocation_performed=False, free_vram='UNVERIFIED', game_pixels='UNMEASURED')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = estimate(args.evidence.read_text(encoding='utf-8-sig'))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
