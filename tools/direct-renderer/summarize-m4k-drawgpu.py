"""M4k resource intervals. Prep envelopes repeat per row; never add them twice.
Timestamps measure elapsed GPU time, not residency/page faults or busy time.
"""
import argparse
import csv
import importlib.util
import json
import math
from pathlib import Path

module = importlib.util.spec_from_file_location('m4j', Path(__file__).with_name('summarize-m4j-drawgpu.py'))
base = importlib.util.module_from_spec(module)
module.loader.exec_module(base)


def summarize(path):
    result = base.summarize(path)
    identities = path.with_name(path.stem + '-draws.tsv')
    if identities.exists():
        with identities.open(encoding='utf-8-sig', newline='') as stream:
            draws = {int(row['draw']): row for row in csv.DictReader(stream, delimiter='\t')}
        for rank in result['ranking']:
            rank['identity'] = draws.get(rank['draw'])
    with path.open(encoding='utf-8-sig', newline='') as stream:
        rows = list(csv.DictReader(stream, delimiter='\t'))
    for segment in result['segments']:
        values = [r for r in rows if int(r['frame']) == segment['frame'] and int(r['segment']) == segment['segment']]
        prep = [(float(r['prepare_gpu_us']), float(r['prepare_constants_us'])) for r in values]
        if len(set(prep)) != 1:
            raise ValueError('Inconsistent preparation envelope')
        gpu, constants = prep[0]
        grabs = [float(r['grab_us']) for r in values]
        if not all(math.isfinite(v) and v >= 0 for v in (gpu, constants, *grabs)):
            raise ValueError('Non-finite/negative resource time')
        if any(grab > float(row['gpu_us']) + .01 for grab, row in zip(grabs, values)):
            raise ValueError('Grab exceeds enclosing draw')
        remaining = segment['segment_us'] - gpu - constants - segment['covered_draw_us']
        if remaining < -.01:
            raise ValueError('Prep + draw exceeds segment')
        segment.update(prepare_gpu_us=gpu, prepare_constants_us=constants, covered_grab_us=sum(grabs),
                       covered_draw_excluding_grab_us=segment['covered_draw_us'] - sum(grabs),
                       unmeasured_us=max(0, remaining))
    result['validity'] += ' Prep is counted once per segment. Grab is a subset of draw elapsed; do not add it again. Empty/out-of-range captures fail; see sibling status.txt.'
    return result


def self_test():
    directory = Path(__file__).resolve().parents[2] / 'out/direct-renderer/m4k/analyzer-fixture'
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / 'synthetic.tsv'
    header = 'frame\tsegment\tordinal\tdraw\tgpu_us\tsegment_us\tprepare_gpu_us\tprepare_constants_us\tgrab_us\n'
    valid = '4\t1\t256\t20\t39000\t41000\t300\t200\t38000\n4\t1\t257\t21\t1000\t41000\t300\t200\t0\n'
    path.write_text(header + valid, encoding='utf-8')
    segment = summarize(path)['segments'][0]
    assert segment['unmeasured_us'] == 500 and segment['covered_grab_us'] == 38000
    rejected = 0
    for content in ('', valid.replace('38000', '40000'), valid.replace('\t300\t200', '\t-300\t200'), valid.replace('\t300\t200', '\tnan\t200'), valid.replace('\t300\t200', '\t3000\t2000'), valid + valid):
        path.write_text(header + content, encoding='utf-8')
        try:
            summarize(path)
        except ValueError:
            rejected += 1
    assert rejected == 6
    path.write_text(header + valid, encoding='utf-8')
    print('PASS M4k analyzer prep_once=500us grab_subset=38000us unmeasured=500us invalid_inputs_rejected=6 hardware=UNMEASURED')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path, nargs='?')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    if args.self_test:
        self_test()
    else:
        if not args.input:
            parser.error('input dump required')
        text = json.dumps(summarize(args.input), ensure_ascii=False, indent=2)
        if args.output:
            args.output.write_text(text, encoding='utf-8')
        else:
            print(text)
