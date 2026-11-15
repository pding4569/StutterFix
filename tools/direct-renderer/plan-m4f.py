"""Generate M4f plans only. No launch/installation."""
import runpy,sys
from pathlib import Path
sys.dont_write_bytecode=True
root=Path(__file__).resolve().parents[2]
sys.argv[1:1]=['--milestone','m4f','--output',str(root/'out/direct-renderer/m4f/plans')]
runpy.run_path(str(Path(__file__).with_name('plan-m4c.py')),run_name='__main__')
