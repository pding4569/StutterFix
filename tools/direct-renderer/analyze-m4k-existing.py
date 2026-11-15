"""Read/copy prior game evidence; never launch a game or write installed files."""
import argparse
import csv
import hashlib
import importlib.util
import json
import re
import shutil
from pathlib import Path

spec = importlib.util.spec_from_file_location('drawgpu', Path(__file__).with_name('summarize-m4j-drawgpu.py'))
analyzer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(analyzer)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix'))
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[2]/'out/direct-renderer/m4k/prior-game-evidence')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    reports = []
    # Each draw dump is joined ONLY to its own preceding probe list. IDs 490,
    # 848, 849 actually refer to different shaders across these two sessions.
    pairs = [('160141-236','160129-140'), ('160341-926','160329-807')]
    sources = []
    for draw_time, probe_time in pairs:
        name = 'drfull-drawgpu-20261115-'+draw_time
        probe_name = 'drfull-probe-20261115-'+probe_time+'.tsv'
        for filename in (name+'.tsv', name+'-textures.tsv', probe_name):
            original = args.source/filename
            shutil.copyfile(original, args.output/filename)
            sources.append(dict(path=str(original), sha256=hashlib.sha256(original.read_bytes()).hexdigest()))
        with (args.output/probe_name).open(encoding='utf-8-sig', newline='') as f:
            identities = {}
            for row in csv.DictReader(f, delimiter='\t'):
                for match in re.finditer(r'native:(\d+):(\d+):([^;]+);', row['route']):
                    identities[int(match[1])] = dict(path=row['path'], shader=match[3], instance=row['instance'])
        result = analyzer.summarize(args.output/(name+'.tsv'))
        with (args.output/(name+'.tsv')).open(encoding='utf-8-sig', newline='') as f:
            rows = list(csv.DictReader(f, delimiter='\t'))
        for rank in result['ranking']:
            rank['identity'] = identities.get(rank['draw'])
        segments = []
        for segment in result['segments']:
            measured = [row for row in rows if int(row['frame'])==segment['frame'] and int(row['segment'])==segment['segment']]
            grabs = [row for row in measured if 'BlendModes/' in identities.get(int(row['draw']),{}).get('shader','')]
            segments.append(dict(segment, grab_shader_draws=len(grabs), grab_shader_elapsed_us=sum(float(row['gpu_us']) for row in grabs)))
        result['segments'] = segments
        reports.append(result)
    output = dict(sources=sources, reports=reports, game_launched=False, install_written=False,
        validity=['Existing M4j game samples, not new M4k measurements',
                  'Never merge native IDs or sum same-frame costs across separate runs',
                  'Grab shader elapsed includes copy, bindings and sprite sampling; copy-only and migration causes are UNMEASURED',
                  'NRE affected texture metadata rows; timestamp bodies are present and valid'])
    (args.output/'analysis.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
    for report in reports:
        grouped = {}
        for segment in report['segments']:
            grouped.setdefault(segment['segment'], []).append(segment)
        print(Path(report['source']).name, 'rows='+str(report['rows']))
        for segment, values in grouped.items():
            print(' segment='+str(segment), 'samples='+str(len(values)),
                  'envelope_us='+str(sum(v['segment_us'] for v in values)/len(values)),
                  'covered_grab_shader_us='+str(sum(v['grab_shader_elapsed_us'] for v in values)/len(values)))


if __name__ == '__main__':
    main()
