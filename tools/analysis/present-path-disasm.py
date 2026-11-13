"""Offline only: pinned Unity PE identity, PDB symbol ranges and string xrefs."""
import bisect
import hashlib
import sys
import uuid
import struct
from pathlib import Path

WT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(WT / 'out/engine-hot/deps'))
import capstone
import pefile

path = Path(r'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\UnityPlayer.dll')
pe = pefile.PE(str(path))
rsds = next(pe.get_data(d.struct.AddressOfRawData, d.struct.SizeOfData)
            for d in pe.DIRECTORY_ENTRY_DEBUG if d.struct.Type == 2)
assert rsds[:4] == b'RSDS' and uuid.UUID(bytes_le=rsds[4:20]).hex.upper() == '0BD6084081424C169079D97BFE021469' and int.from_bytes(rsds[20:24], 'little') == 1
pdb_path = WT.parent / 'symbols/UnityPlayer_Win64_player_mono_x64.pdb/0BD6084081424C169079D97BFE0214691/UnityPlayer_Win64_player_mono_x64.pdb'
with pdb_path.open('rb') as pdb:
    header = pdb.read(56)
    assert header.startswith(b'Microsoft C/C++ MSF 7.00')
    block_size, _, _, directory_size, _, block_map = struct.unpack_from('<6I', header, 32)
    def blocks(ids):
        parts = []
        for block in ids:
            pdb.seek(block * block_size)
            parts.append(pdb.read(block_size))
        return b''.join(parts)
    count = (directory_size + block_size - 1) // block_size
    pdb.seek(block_map * block_size)
    directory = blocks(struct.unpack('<' + 'I'*count, pdb.read(4*count)))[:directory_size]
    streams, = struct.unpack_from('<I', directory)
    sizes = struct.unpack_from('<' + 'I'*streams, directory, 4)
    pos = 4 + 4*streams
    for index, size in enumerate(sizes):
        n = 0 if size == 0xffffffff else (size + block_size-1)//block_size
        ids = struct.unpack_from('<'+'I'*n, directory, pos)
        pos += n*4
        if index == 1:
            info = blocks(ids)[:size]
            assert info[12:28] == rsds[4:20] and info[8:12] == rsds[20:24], 'PDB GUID/age mismatch'
            break
symbols = sorted((int(p[0], 16), p[2]) for l in (WT.parent / 'night-results/unityplayer-syms.tsv').read_text(encoding='utf-8-sig').splitlines() if len(p := l.split('\t', 2)) == 3)
starts = sorted(set(a for a, _ in symbols))
addresses = [a for a, _ in symbols]
def label(a):
    i = bisect.bisect_right(addresses, a)-1
    return f'{symbols[i][1]}+0x{a-addresses[i]:x}' if i >= 0 else hex(a)
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
md.detail = True
names = ['D3D11SwapChain::', 'GfxDeviceClient::WaitForPendingPresent', 'GfxDeviceClient::BeginFrame',
         'GfxDeviceClient::GfxDeviceClient', 'GfxDeviceClient::PresentFrame', 'PlatformGfxSemaphore::',
         'GfxDeviceD3D11::PresentFrame', 'QualitySettings_Set_Custom_PropMaxQueuedFrames',
         'QualitySettings_Get_Custom_PropMaxQueuedFrames', 'GfxDeviceD3D11::WaitForLastPresentationAndGetTimestamp',
         'GfxDeviceWorker::RunCommand', 'GfxDeviceWorker::SignalEvent', 'SwapChainWaitingContext::', 'GfxDevice::SetMax', 'GfxDeviceClient::SetMax', 'GfxDeviceD3D11::SetMax']
lines = [str(path), 'RSDS=' + rsds[4:24].hex(), 'SHA256=' + hashlib.sha256(path.read_bytes()).hexdigest(),
         'PDB GUID/age verified from MSF info stream: ' + str(pdb_path),
         'Symbols TSV SHA256=' + hashlib.sha256((WT.parent / 'night-results/unityplayer-syms.tsv').read_bytes()).hexdigest(),
         'Symbol ranges are nearest distinct RVAs, not recovered source types.']
string_rvas = {}
for sec in pe.sections:
    data = sec.get_data()
    import re
    for match in re.finditer(rb'[ -~]{5,}', data):
        s = match.group().decode('ascii')
        if any(k in s.lower() for k in ['flip-model', 'window-mode', 'tearing', 'frame-latency', 'queuedframes', 'maxqueued', 'swapchain', 'swap-chain', 'd3d11', 'gfx-enable', 'gfx-disable']):
            a = sec.VirtualAddress + match.start()
            string_rvas[a] = s
            lines.append(f'STRING {a:x} {s}')
for a, name in symbols:
    if not any(name.startswith(n) for n in names): continue
    end = starts[bisect.bisect_right(starts, a)]
    lines.append(f'\n## {name} {a:x}..{end:x}')
    for ins in md.disasm(pe.get_data(a, min(end-a, 0x9000)), a):
        suffix = ''
        if ins.mnemonic in ('call', 'jmp') and ins.op_str.startswith('0x'):
            suffix = ' ; ' + label(int(ins.op_str, 16))
        for op in ins.operands:
            if op.type == capstone.x86.X86_OP_MEM and op.mem.base == capstone.x86.X86_REG_RIP:
                target = ins.address + ins.size + op.mem.disp
                suffix += f' ; [{target:x}] ' + string_rvas.get(target, label(target))
        lines.append(f'{ins.address:08x} {ins.bytes.hex():24} {ins.mnemonic:8} {ins.op_str}{suffix}')
out = WT / 'out/present-path'
out.mkdir(exist_ok=True)
(out / 'disasm.txt').write_text('\n'.join(lines), encoding='utf-8')
print('\n'.join(lines[:45]))
print(out / 'disasm.txt')
