"""Generate M4g plans only. Never launch/install."""
import runpy,sys
from pathlib import Path
sys.dont_write_bytecode=True
root=Path(__file__).resolve().parents[2]
sys.argv[1:1]=['--milestone','m4g','--output',str(root/'out/direct-renderer/m4g/plans')]
runpy.run_path(str(Path(__file__).with_name('plan-m4c.py')),run_name='__main__')
