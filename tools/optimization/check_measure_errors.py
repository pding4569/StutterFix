"""Keep UI object names out of the sfmeasure error counter."""
import importlib.util
from pathlib import Path

root=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location("sfmeasure",root/"tools/sfmeasure/server.py")
sf=importlib.util.module_from_spec(spec)
spec.loader.exec_module(sf)
cases=[
    ("[StutterFix] [UI cost] [Error Meter(Clone) ScreenSpaceOverlay]",False),
    ("[StutterFix] [UI cost] [QuartzErrorMeter (Text)]",False),
    ("[StutterFix] [Error] failed",True),
    ("[StutterFix] Error: failed",True),
    ("[StutterFix] System.NullReferenceException: failed",True),
    ("[StutterFix] [install] patch failed",True),
]
for line,expected in cases:
    assert bool(sf.RE_ERR.search(line))==expected,line
print(f"Measurement error classifier {len(cases)} cases, errors 0")
