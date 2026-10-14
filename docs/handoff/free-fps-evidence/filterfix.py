from pathlib import Path
p=Path('docs/handoff/free-fps-evidence/filterparse.py');s=p.read_text().replace("'Color_Brightness','Color_Contrast'", "'Color_BrightContrastSaturation','Color_Brightness','Color_Contrast'");p.write_text(s)
