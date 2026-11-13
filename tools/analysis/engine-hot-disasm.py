"""Offline PE/PDB-list cross reference; never loads or changes the player."""
import bisect
import hashlib
import sys
import uuid
from pathlib import Path

WT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(WT / 'out/engine-hot/deps'))
import capstone
import pefile

ROOT = WT.parent
PE = Path(r'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\UnityPlayer.dll')
symbols = sorted((int(a, 16), c) for line in (ROOT / 'night-results/unityplayer-syms.tsv').read_text(encoding='utf-8-sig').splitlines()
                 if len(parts := line.split('\t', 2)) == 3 for a, _, c in [parts])
addresses = [r for r, _ in symbols]


def label(rva):
    i = bisect.bisect_right(addresses, rva)-1
    return f'{symbols[i][1]}+0x{rva-addresses[i]:x}' if i >= 0 else hex(rva)


pe = pefile.PE(str(PE))
rsds = next(pe.get_data(d.struct.AddressOfRawData, d.struct.SizeOfData) for d in pe.DIRECTORY_ENTRY_DEBUG if d.struct.Type == 2)
guid = uuid.UUID(bytes_le=rsds[4:20]).hex.upper()
if rsds[:4] != b'RSDS' or guid != '0BD6084081424C169079D97BFE021469' or int.from_bytes(rsds[20:24], 'little') != 1:
    raise SystemExit('Refusing mismatched UnityPlayer PDB identity: ' + guid)
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
names = ['ujob_wait_for', 'RenderManager::RenderOffscreenCameras', 'TransformChangeDispatch::ScanAndHandleChangesJob',
         'RendererUpdateManager::UpdateAll', 'ExtractSceneRenderNodeQueue', 'Camera::CustomCull',
         'UI::RectTransform::InitializeClass', 'PhysicsManager::InitializeClass',
         'TransformChangeDispatch::ScheduleGetAndClearChangedBatchGroup',
         'TransformChangeDispatch::ScheduleGetAndClearChangedAsIndexedBatchedJobs_Internal',
         'TransformChangeDispatch::RegisterSystem', 'TransformChangeDispatch::RegisterPermanentInterestSystem']
lines = [f'PE {PE}', f'Timestamp {pe.FILE_HEADER.TimeDateStamp:x} SizeOfImage {pe.OPTIONAL_HEADER.SizeOfImage:x}',
         f'PDB GUID {guid} age {int.from_bytes(rsds[20:24], "little")} SHA256 {hashlib.sha256(PE.read_bytes()).hexdigest()}',
         'Names use supplied symbol RVAs; nearest-symbol labels are bounds, not complete type recovery.']
for name in names:
    start = next(r for r, n in symbols if n == name)
    end = next(r for r, _ in symbols if r > start)
    lines += [f'\n## {name} 0x{start:x}..0x{end:x}']
    for ins in md.disasm(pe.get_data(start, min(end-start, 0x1800)), start):
        suffix = ''
        if ins.mnemonic in ('call', 'jmp') and ins.op_str.startswith('0x'):
            suffix = ' ; ' + label(int(ins.op_str, 16))
        lines += [f'{ins.address:08x} {ins.mnemonic:8} {ins.op_str}{suffix}']

targets = {r: n for r, n in symbols if n in names[-2:]}
lines += ['\n## direct E8 references to registration functions (opcode candidates; inspect boundaries)']
for section in pe.sections:
    if not section.Characteristics & 0x20000000:
        continue
    data = section.get_data()
    pos = 0
    while (pos := data.find(b'\xe8', pos)) >= 0:
        if pos+5 <= len(data):
            rva = section.VirtualAddress + pos
            target = rva + 5 + int.from_bytes(data[pos+1:pos+5], 'little', signed=True)
            if target in targets:
                lines += [f'{rva:08x} {label(rva)} -> {targets[target]}']
        pos += 1
out = WT / 'out/engine-hot/disasm.txt'
out.write_text('\n'.join(lines), encoding='utf-8')
print(out)
print('\n'.join(lines[lines.index('\n## direct E8 references to registration functions (opcode candidates; inspect boundaries)'):]))
