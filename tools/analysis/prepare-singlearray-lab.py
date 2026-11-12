"""Prepare an isolated shader experiment; never overwrite the production bundle."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LAB = ROOT / 'out/singlearray-lab'
source_path = ROOT / 'tools/gpudeco-lab/GpuDecoBatch.shader'
source = source_path.read_text(encoding='utf-8')
start = source.index('#ifdef NO_DIRECT_TEXTURES\n')
assert source.count('#ifdef NO_DIRECT_TEXTURES\n') == 1
specialized = source[:start] + '''#ifdef SF_SINGLE_ARRAY // Proven single-array span only; binding selection eliminated.
                if(usePoint) c=_SfSingleArray.SampleLevel(sampler_point_clamp,float3(uv,asfloat(v.info.y)),0);
                else c=_SfSingleArray.SampleLevel(sampler_linear_clamp,float3(uv,asfloat(v.info.y)),0);
#elif defined(SF_SINGLE_2D)
                if(usePoint) c=_SfSingle2D.SampleLevel(sampler_point_clamp,uvIn,0);
                else c=_SfSingle2D.SampleLevel(sampler_linear_clamp,uvIn,0);
#elif defined(NO_DIRECT_TEXTURES)
''' + source[start+len('#ifdef NO_DIRECT_TEXTURES\n'):]
specialized = specialized.replace('Shader "StutterFix/GpuDecoBatch"', 'Shader "StutterFix/ResearchSingleArray"', 1)
specialized = specialized.replace('#pragma multi_compile_local _ SF_SIMPLE','#pragma multi_compile_local _ SF_SIMPLE\n            #pragma multi_compile_local _ SF_SINGLE_ARRAY SF_SINGLE_2D',1)
specialized = specialized.replace('            Texture2DArray _SfDecoTex0;', '            Texture2DArray _SfSingleArray;\n            Texture2D _SfSingle2D;\n            Texture2DArray _SfDecoTex0;', 1)
specialized = specialized.replace('#pragma multi_compile_local _ SF_SINGLE_ARRAY SF_SINGLE_2D', '#pragma multi_compile_local _ SF_SINGLE_ARRAY SF_SINGLE_2D\n            #pragma multi_compile_local _ SF_LEAN_VERTEX', 1)
# The VS and PS compile separately: the VS otherwise retains DIRECT_FEW's
# dynamic constant-buffer lookup even when the PS never consumes binding bits.
specialized = specialized.replace('#if defined(DIRECT_FEW) && !defined(NO_DIRECT_TEXTURES)', '#if defined(DIRECT_FEW) && !defined(NO_DIRECT_TEXTURES) && !(defined(SF_LEAN_VERTEX) && (defined(SF_SINGLE_ARRAY) || defined(SF_SINGLE_2D)))', 1)
old_region='o.color=asfloat(_CollapseVerts.Load4(a+12));o.uv=uvi.xy;o.region=asfloat(_CollapseVerts.Load4(a+44));'
assert specialized.count(old_region)==1
specialized=specialized.replace(old_region, '''o.color=asfloat(_CollapseVerts.Load4(a+12));o.uv=uvi.xy;
#if defined(SF_LEAN_VERTEX) && defined(SF_SINGLE_2D)
                o.region=0; // Direct textures sample uvIn; region is not consumed.
#else
                o.region=asfloat(_CollapseVerts.Load4(a+44));
#endif''')
shaders = LAB / 'Assets/Shaders'
editor = LAB / 'Assets/Editor'
for folder in (shaders, editor, LAB / 'Packages', LAB / 'ProjectSettings'):
    folder.mkdir(parents=True, exist_ok=True)
(shaders / 'Reference.shader').write_text(source, encoding='utf-8')
(shaders / 'SingleArray.shader').write_text(specialized, encoding='utf-8')
shutil.copy2(ROOT / 'tools/analysis/SingleArrayFixture.cs', editor / 'SingleArrayFixture.cs')
for relative in ('Packages/manifest.json', 'ProjectSettings/ProjectVersion.txt'):
    shutil.copy2(ROOT / 'tools/gpudeco-lab/project' / relative, LAB / relative)
license_text=(ROOT/'effects/license.txt').read_text(encoding='utf-8').split('ScreenEffects.shader')[0]
(LAB/'LICENSE').write_text(license_text,encoding='utf-8')
manifest = dict(reference_sha256=hashlib.sha256(source_path.read_bytes()).hexdigest(),
                candidate_sha256=hashlib.sha256((shaders/'SingleArray.shader').read_bytes()).hexdigest(),
                change='optional lean VS: omit proven-single binding remap and unused direct-texture region load',
                admitted_scope='all future textures proven to use one physical binding; plain non-GrabPass only',
                production_bundle_or_shader_files_modified=False,
                live_admission='AUTOTEST explicit opt-in and all-future-event proof required', performance='UNMEASURED')
(LAB / 'experiment.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print(LAB)
