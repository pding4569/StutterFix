import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
expected = {
    'GpuDecoDrawCollapse.cs', 'GpuDecoImagePlayback.cs',
    'docs/handoff/codex-imageown-result.md', 'docs/handoff/codex-imageown-summary.txt',
    'docs/handoff/codex-master-20261022.md', 'docs/handoff/im-autoloop-ledger.md',
    'docs/handoff/imageown-autoloop-ledger.md',
}
changed = set(subprocess.check_output(['git', 'diff', '--name-only'], cwd=root, text=True).splitlines())
assert changed == expected, changed
subprocess.run(['git', 'diff', '--check'], cwd=root, check=True)
summary = (root / 'docs/handoff/codex-imageown-summary.txt').read_text(encoding='utf-8')
result = (root / 'docs/handoff/codex-imageown-result.md').read_text(encoding='utf-8')
assert len(summary.splitlines()) == 10
assert summary.splitlines()[0] == result.splitlines()[0]
assert result.startswith('IMAGEOWN=FAIL') and 'reason=TEXTURE_ADDRESS_MISSING' in result.splitlines()[0]
for name in expected:
    content = (root / name).read_text(encoding='utf-8-sig')
    assert '\ufffd' not in content, name
def read_log(name):
    data = (out / name).read_bytes()
    return data.decode('utf-16' if data.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')

for edition, count in [('dev',279),('player',229),('playerauto',281)]:
    build = read_log(f'build-{edition}.txt')
    load = read_log(f'loadcheck-{edition}.txt')
    assert '오류 0개' in build
    assert f'{count}곳 확인, 틀림 0' in load
assert 'checks=20 failures=0' in (out / 'coverage-check.txt').read_text(encoding='utf-8-sig')
assert 'Ran 8 tests' in (out / 'generator-tests.txt').read_text(encoding='utf-8-sig')
print('FINAL_CHECK=PASS tracked_changes=7 summary_lines=10 builds=3 loadchecks=3 method_checks=20 generator_checks=8')
# The stable observer directory is outside this task's write scope. Keep this
# task-local checkpoint with the verification evidence; do not change memories.
(out / 'observer-checkpoint.txt').write_text('2026-10-25: task-observer checkpoint; no new reusable observations retained; existing stderr/PowerShell quoting issues corrected in task-local runners.\n', encoding='utf-8')
