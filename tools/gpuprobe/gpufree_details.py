"""Extract D3D11 shader assembly and full IL offline; requires gpufree_audit output."""
import ctypes
import json
import os
from pathlib import Path
import subprocess
import sys
import hashlib
import re

import lz4.block


def disassemble(blob):
    fn = ctypes.WinDLL('d3dcompiler_47').D3DDisassemble
    fn.argtypes = [ctypes.c_void_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_char_p,
                   ctypes.POINTER(ctypes.c_void_p)]
    fn.restype = ctypes.c_long
    out = ctypes.c_void_p()
    src = ctypes.create_string_buffer(blob)
    hr = fn(src, len(blob), 0, None, ctypes.byref(out))
    if hr < 0:
        raise RuntimeError('D3DDisassemble HRESULT ' + hex(hr & 0xffffffff))
    vtable = ctypes.cast(out, ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents
    ptr = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(vtable[3])
    size = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(vtable[4])
    release = ctypes.WINFUNCTYPE(ctypes.c_ulong, ctypes.c_void_p)(vtable[2])
    try:
        return ctypes.string_at(ptr(out), size(out)).rstrip(b'\0').decode('utf-8')
    finally:
        release(out)


def run(folder):
    data = json.loads((folder / 'audit.json').read_text())
    selected = []
    for row in data['assets']:
        if row['type'] != 'Shader':
            continue
        name = row['name']
        if not (name.startswith('CameraFilterPackLegacy/') or name in
                ('Sprites/Tile', 'ADOFAI/FloorMesh', 'ADOFAI/zWriteUI', 'Hidden/VideoBloom')):
            continue
        tree = json.loads(Path(row['tree']).read_text())
        passes = [p for sub in tree['m_ParsedForm']['m_SubShaders'] for p in sub['m_Passes']]
        states = [{k: p['m_State'][k] for k in ('zWrite', 'zTest', 'stencilOp', 'stencilRef')}
                  for p in passes]
        record = {'name': name, 'states': states, 'programs': []}
        for i, platform in enumerate(tree['platforms']):
            if platform != 4:  # Unity ShaderCompilerPlatform.D3D11
                continue
            for off, comp, decomp in zip(tree['offsets'][i], tree['compressedLengths'][i],
                                         tree['decompressedLengths'][i]):
                raw = lz4.block.decompress(bytes(tree['compressedBlob'][off:off+comp]),
                                           uncompressed_size=decomp)
                start = 0
                while True:
                    start = raw.find(b'DXBC', start)
                    if start < 0:
                        break
                    length = int.from_bytes(raw[start+24:start+28], 'little')
                    text = disassemble(raw[start:start+length])
                    target = folder / (name.replace('/', '_') + '-' + str(len(record['programs'])) + '.asm')
                    target.write_text(text, encoding='utf-8')
                    record['programs'].append(str(target))
                    start += length
        selected.append(record)
    (folder / 'shader-details.json').write_text(json.dumps(selected, indent=2), encoding='utf-8')
    # Reuse the project's metadata scanner, with raw instructions only in an isolated TEMP copy.
    root = Path(__file__).resolve().parents[2]
    scanner = folder / 'ILDetails'
    scanner.mkdir(exist_ok=True)
    code = (root / 'tools/ilscan/Program.cs').read_text(encoding='utf-8-sig')
    code = code.replace('n.EndsWith("+" + part)', 'n.EndsWith("+" + part) || n.StartsWith(part, StringComparison.Ordinal)')
    code = code.replace('var seen = new HashSet<string>();\n                foreach (var (op, tok) in Tokens(m))',
                        'DumpRaw(m);\n                var seen = new HashSet<string>();\n                foreach (var (op, tok) in Tokens(m))')
    extra = r'''
    static void DumpRaw(MethodDefinition m)
    {
        if (m.RelativeVirtualAddress == 0) return;
        var r = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader();
        while (r.RemainingBytes > 0)
        {
            int pos = r.Offset;
            byte b = r.ReadByte();
            ILOpCode op = (ILOpCode)(b == 0xfe ? 0xfe00 | r.ReadByte() : b);
            int size = OperandSize(op, ref r, out bool token);
            string value = "";
            if (size == 1) value = r.ReadSByte().ToString();
            if (size == 2) value = r.ReadInt16().ToString();
            if (size == 8) value = r.ReadInt64().ToString();
            if (size == 4)
            {
                int n = r.ReadInt32();
                if (op == ILOpCode.Ldstr) value = md.GetUserString(MetadataTokens.UserStringHandle(n & 0xffffff));
                else if (token) value = MemberName(Handle(n));
                else if (op == ILOpCode.Ldc_r4) value = BitConverter.Int32BitsToSingle(n).ToString(System.Globalization.CultureInfo.InvariantCulture);
                else value = n.ToString();
            }
            Console.WriteLine("    IL_" + pos.ToString("X4") + " " + op + " " + value);
        }
    }
'''
    code = code.replace('    // 메서드 서명을 글자로', extra + '\n    // 메서드 서명을 글자로')
    (scanner / 'Program.cs').write_text(code, encoding='utf-8')
    (scanner / 'ILDetails.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>')
    subprocess.run(['dotnet', 'build', str(scanner / 'ILDetails.csproj'), '-c', 'Release', '-v', 'q', '--nologo'], check=True)
    managed = Path(data['assets'][0]['file']).parent / 'Managed'
    for dll, typ in [('Assembly-CSharp.dll', 'scrCamera'), ('Assembly-CSharp.dll', 'VideoBloom'),
                     ('Assembly-CSharp-firstpass.dll', 'CameraFilterPackLegacy_')]:
        env = dict(os.environ, METHOD=typ, FIELDS='1')
        output = subprocess.check_output(['dotnet', str(scanner / 'bin/Release/net8.0/ILDetails.dll'),
                                         str(managed / dll), 'ZZZ'], env=env)
        (folder / (typ + '-il.txt')).write_bytes(output)
    print('shader records', len(selected), 'DXBC programs', sum(len(x['programs']) for x in selected))
    return data, selected


if __name__ == '__main__':
    folder = Path(sys.argv[1])
    data, shaders = run(folder)
    if len(sys.argv) > 2:
        prefix = Path(sys.argv[2])
        summary = {k: data[k] for k in ('png', 'census', 'errors', 'shader_objects', 'camera_objects')}
        summary['cameras'] = [x for x in data['assets'] if x['type'] == 'Camera'
            and x['name'] in ('BGStaticCam', 'BGMovingCam', 'Camera', 'OverlayCam')
            and Path(x['file']).name in ('level1', 'resources.assets')]
        summary['shaders'] = []
        all_asm = []
        for shader in shaders:
            record = {'name': shader['name'], 'states': shader['states'], 'programs': []}
            for file in shader['programs']:
                text = Path(file).read_text()
                record['programs'].append({'file': Path(file).name,
                    'sha256': hashlib.sha256(text.encode()).hexdigest(),
                    'stage': 'pixel' if re.search(r'^ps_', text, re.M) else 'vertex',
                    'sample_instructions': len(re.findall(r'^sample', text, re.M))})
                all_asm.append('=== ' + Path(file).name + '\n' + text)
            summary['shaders'].append(record)
        prefix.with_suffix('.json').write_text(json.dumps(summary, indent=2, ensure_ascii=True), encoding='utf-8')
        prefix.with_name(prefix.name + '-shaders.txt').write_text('\n'.join(all_asm), encoding='utf-8')
        sections = re.split(r'^== ', (folder / 'CameraFilterPackLegacy_-il.txt').read_text(encoding='utf-8'), flags=re.M)[1:]
        names = ('Distortion_FishEye', 'Color_GrayScale', 'Atmosphere_Rain', 'Color_Chromatic_Aberration',
                 'FX_Hexagon_Black', 'Sharpen_Sharpen', 'Color_Contrast', 'Light_Water2')
        il = []
        for section in sections:
            if section.splitlines()[0] not in ['CameraFilterPackLegacy_' + n for n in names]:
                continue
            methods = re.split(r'^  (?=\S)', section, flags=re.M)
            il.append('== ' + section.splitlines()[0] + '\n' + next(s for s in methods if s.startswith('OnRenderImage\n')))
        camera = (folder / 'scrCamera-il.txt').read_text(encoding='utf-8')
        start = camera.index('  SetupRTCam\n')
        end = camera.index('    IL_00BA Call ADOBase::get_customLevel', start)
        il.append('== scrCamera\n' + camera[start:end])
        il.append((folder / 'VideoBloom-il.txt').read_text(encoding='utf-8'))
        prefix.with_name(prefix.name + '-il.txt').write_text('\n'.join(il), encoding='utf-8')
