"""Offline resize-path extension; inherits PE/PDB identity checks from present-path."""
import runpy
from pathlib import Path

c = runpy.run_path(str(Path(__file__).with_name('present-path-disasm.py')))
pe, md, symbols, starts, label = (c[k] for k in ('pe', 'md', 'symbols', 'starts', 'label'))
import bisect
names = ('Screen_CUSTOM_SetResolution', 'ScreenManagerWin::SetRequestedResolution',
         'ScreenManagerWin::SetResolutionImmediate', 'ScreenManagerWin::HandleWindowResize',
         'ScreenManagerWin::UpdateResolutionAfterPresent', 'ScreenManagerWin::UpdateResolutionPrefs',
         'PlayerMainWndProc', 'GfxDisplayManagerD3D11::SetResolution',
         'D3D11SwapChain::Adjust', 'D3D11SwapChain::ReleaseBackbufferResources',
         'D3D11SwapChain::CreateBackBufferResources', 'WinScreenSetup::SetResolution')
lines = []
for a, name in symbols:
    if name not in names: continue
    end = starts[bisect.bisect_right(starts, a)]
    lines.append(f'\n## {name} {a:x}..{end:x}')
    for ins in md.disasm(pe.get_data(a, end-a), a):
        suffix = ''
        if ins.mnemonic in ('call', 'jmp') and ins.op_str.startswith('0x'):
            suffix = ' ; ' + label(int(ins.op_str, 16))
        for op in ins.operands:
            if op.type == c['capstone'].x86.X86_OP_MEM and op.mem.base == c['capstone'].x86.X86_REG_RIP:
                suffix += ' ; ' + label(ins.address + ins.size + op.mem.disp)
        lines.append(f'{ins.address:08x} {ins.bytes.hex():24} {ins.mnemonic:8} {ins.op_str}{suffix}')
out = c['WT'] / 'out/present-buffers'
out.mkdir(exist_ok=True)
(out / 'disasm.txt').write_text('\n'.join(lines), encoding='utf-8')
print(out / 'disasm.txt')
