from pathlib import Path
p=Path('docs/handoff/free-fps-evidence/parse.py');s=p.read_text();s=s.replace("s=log.read_text(encoding='utf-8',errors='replace')", "logs=list((Path.home()/'AppData/Local/StutterFix/runs').glob('*freefps*abba.log'))\ns='\\n'.join(p.read_text(encoding='utf-8',errors='replace') for p in logs)\nif not logs:s=log.read_text(encoding='utf-8',errors='replace')");p.write_text(s)
