"""Production copy lifecycle and evaluation recording; native/Unity calls use managed adapters."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[3]
copy = (root / 'GpuDecoCopy.cs').read_text(encoding='utf-8-sig')
native = {
    'sf_deco_copy_version': 'return 1;',
    'sf_deco_copy_create': 'return 1;', 'sf_deco_copy_create8': 'return 1;',
    'sf_deco_copy_create8masked': 'return 1;', 'sf_deco_copy_create_masked': 'return 1;',
    'sf_deco_copy_event': 'return (IntPtr)1;',
    'sf_deco_copy_submit': 'return Adapter.Submit(ulong.MaxValue,ulong.MaxValue);',
    'sf_deco_copy_submit_mask': 'return Adapter.Submit(low,high);',
    'sf_deco_copy_stats': 'output=default; return 1;',
    'sf_deco_copy_destroy': 'Adapter.Destroyed++;', 'sf_deco_copy_thread': 'return 1;',
}
pattern = r'\[DllImport\([^\n]+?\)\] static extern (\w+) (sf_deco_copy_\w+)\(([^\n]*?)\);'
seen = []

def replace(m):
    seen.append(m[2])
    args = m[3].replace('[In] ', '')
    return f'static {m[1]} {m[2]}({args}) {{ {native[m[2]]} }}'

copy = re.sub(pattern, replace, copy)
assert set(seen) == set(native), seen
gpu = (root / 'GpuDecoGpu.cs').read_text(encoding='utf-8-sig')
start = gpu.index('        internal void RecordLiveEvaluation(')
brace = gpu.index('{', start)
depth = 1
end = brace + 1
while depth:
    depth += (gpu[end] == '{') - (gpu[end] == '}')
    end += 1
wrapper = '''
namespace StutterFix {
internal class GpuDecoGpu {
    internal bool UseLiveMode=true, hasLiveKernel=true, hasImageGeometry;
    internal UnityEngine.ComputeShader shader=new();
    internal int liveKernel=1;
    internal UnityEngine.ComputeBuffer liveChannelsBuffer=new(),sourceEBuffer=new(),staticBuffer=new(),uvBuffer=new(),values=new(),imageShapeBuffer=new();
    internal UnityEngine.GraphicsBuffer vertices=new(UnityEngine.GraphicsBuffer.Target.Raw,8*9,4);
    internal Data data=new();
    internal class Data { internal float Pitch=1.25f,TweenScale=2f; internal int[] Items=new int[2]; }
    static void Require(bool ok,string reason) { if(!ok)throw new InvalidOperationException(reason); }
'''
out = Path(__file__).parent / 'obj' / 'Production.cs'
out.parent.mkdir(exist_ok=True)
out.write_text(copy + wrapper + gpu[start:end] + '\n}}\n', encoding='utf-8')
print('Extracted actual GpuDecoCopy and RecordLiveEvaluation; only native/Unity calls replaced')
