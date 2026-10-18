from pathlib import Path
import hashlib,json,subprocess,tempfile
R=Path.cwd(); O=R/'docs/handoff'; T=Path(tempfile.gettempdir())/'stutterfix-gpu3-20261018'; T.mkdir(exist_ok=True)
paths=['AutoTest.cs','GpuCensus.cs','GpuProbe.cs','GpuHide.cs','StutterFix.cs','StutterFix.csproj','.codex/config.toml']
game=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice')
paths += [str(p) for p in (game/'Mods/StutterFix').rglob('*') if p.is_file() and '.cache' not in p.parts]
paths += [str(game/'A Dance of Fire and Ice_Data/boot.config'),str(game/'A Dance of Fire and Ice_Data/globalgamemanagers')]
def snap():
 return {p:hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in paths if Path(p).is_file()}
baseline=O/'codex-gpu3-20261018-preservation-before.json'
if not baseline.exists(): baseline.write_text(json.dumps(snap(),ensure_ascii=False,indent=2),encoding='utf-8')
for p in ['MaterialPoolFixture.cs','DecorationMaterialPool.cs','AlphaCropFixture.cs']:
 (T/p).write_bytes(subprocess.check_output(['git','show','5d42c57^:'+p]))
# Copy the existing metadata-only scanner; extend matching in the isolated copy only.
S=T/'ILAudit';S.mkdir(exist_ok=True)
s=(R/'tools/ilscan/Program.cs').read_text(encoding='utf-8-sig')
s=s.replace('md = pe.GetMetadataReader();', 'if (!pe.HasMetadata) { System.Console.Error.WriteLine("no managed metadata"); return 2; } md = pe.GetMetadataReader();')
s=s.replace('if (!IsCall(op)) continue;', 'if (!IsCall(op) && op != ILOpCode.Ldfld && op != ILOpCode.Stfld && op != ILOpCode.Ldflda && op != ILOpCode.Ldsfld && op != ILOpCode.Stsfld) continue;')
s=s.replace('h.Kind != HandleKind.MethodSpecification) continue;', 'h.Kind != HandleKind.MethodSpecification && h.Kind != HandleKind.FieldDefinition) continue;')
s=s.replace('if (ShortName(h) != name) continue;', 'if (!System.Text.RegularExpressions.Regex.IsMatch(MemberName(h), name)) continue;')
(S/'Program.cs').write_text(s,encoding='utf-8');(S/'ILAudit.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><Nullable>disable</Nullable></PropertyGroup></Project>')
print(T)
# IL call/field reference audit over all installed managed game assemblies and every installed mod DLL.
import os,time
subprocess.run(['dotnet','build',str(S/'ILAudit.csproj'),'-c','Release','--nologo','-v','q'],check=True)
scanner=T/'ILAudit/bin/Release/net8.0/ILAudit.dll'
pattern=r'(UnityEngine\.(Sprite|SpriteRenderer|SpriteMask)::|UnityEngine\.Renderer::(get_materials?|get_sharedMaterials?|set_materials?|set_sharedMaterials?)$|UnityEngine\.Texture::(set_wrapMode.*|set_filterMode|set_anisoLevel)$|UnityEngine\.Texture2D::(Compress|Apply|Reinitialize|Resize|LoadRawTextureData)|scrVisualDecoration::(.*[Ss]prite.*|.*[Cc]ollider.*|SetTile|SetBlendMode|SetMasking.*|SetTextureScaleMultiplier|GetDecoration.*|UpdateShader)|RDUtils::SetPointSamplingVariant|scrVisualDecoration::_sprite|TextureManager\+CustomSprite::)'
managed=game/'A Dance of Fire and Ice_Data/Managed'
dlls=sorted(managed.rglob('*.dll'))+sorted(p for p in (game/'Mods').rglob('*.dll') if '.cache' not in p.parts)
rows=[];manifest=[]
for dll in dlls:
 env=dict(os.environ);env.pop('METHOD',None);env.pop('TYPES',None)
 p=subprocess.run(['dotnet',str(scanner),str(dll),pattern],capture_output=True,text=True,encoding='utf-8',env=env)
 calls=[x for x in p.stdout.splitlines() if '  ->  ' in x]
 manifest.append({'path':str(dll),'sha256':hashlib.sha256(dll.read_bytes()).hexdigest(),'exit':p.returncode,'references':len(calls),'error':p.stderr[:500] if p.returncode else ''})
 rows.extend(str(dll)+'\t'+x.replace('  ->  ','\t') for x in calls)
(O/'codex-gpu3-20261018-consumers.tsv').write_text('assembly\tcaller\ttarget\n'+'\n'.join(rows)+'\n',encoding='utf-8')
(O/'codex-gpu3-20261018-scan-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('assemblies',len(manifest),'references',len(rows),'errors',sum(x['exit']!=0 for x in manifest))
for typ in ['scrVisualDecoration','RDUtils','TextureManager','scrDecorationManager']:
 env=dict(os.environ,METHOD=typ,FIELDS='1')
 p=subprocess.run(['dotnet',str(R/'tools/ilscan/bin/Release/net8.0/ILScan.dll'),str(managed/'Assembly-CSharp.dll'),'ZZZ'],capture_output=True,text=True,encoding='utf-8',env=env)
 (O/('codex-gpu3-20261018-'+typ+'-il.txt')).write_text(p.stdout,encoding='utf-8')
