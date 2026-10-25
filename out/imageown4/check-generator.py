import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
result = subprocess.run([sys.executable, str(root / 'tools/optimization/imageown-steps.py'), '--self-test'], capture_output=True)
(Path(__file__).parent / 'generator-tests.txt').write_bytes(result.stdout + result.stderr)
print('generator exit=' + str(result.returncode))
raise SystemExit(result.returncode)
