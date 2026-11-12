"""Run the pending RunLod checks with the shared lock and exact install restore."""
import hashlib
import json
import shutil
import sys
import time
import subprocess
import threading
from pathlib import Path

ROOT = Path('E:/StutterFixWork')
WT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / 'StutterFix/tools/sfmeasure'))
import bench_v14 as B
import server as S
from gamelock import GameLock

MOD = Path(S.MOD_DIR)
BOOT = Path(S.GAME_DIR) / 'A Dance of Fire and Ice_Data/boot.config'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None


def analyze(output):
    from PIL import Image, ImageChops
    plan = json.loads((output / 'plan.json').read_text(encoding='utf-8'))
    name = plan['tag']
    comparisons = {}
    for label in ['base', 'noise', 'cpu', 'particles', 'restore']:
        destination = output / f'{label}.png'
        if not destination.exists():
            shutil.copy2(MOD / 'framecmp' / f'{name}_{label}.png', destination)
    with Image.open(output / 'base.png') as raw:
        base = raw.convert('RGBA')
    for label in ['noise', 'cpu', 'particles', 'restore']:
        with Image.open(output / f'{label}.png') as raw:
            current = raw.convert('RGBA')
        assert current.size == base.size
        difference = ImageChops.difference(base, current)
        bands = difference.split()
        maximum = bands[0]
        for band in bands[1:]: maximum = ImageChops.lighter(maximum, band)
        comparisons[label] = dict(max_rgba=[v[1] for v in difference.getextrema()], changed_pixels=base.width * base.height - maximum.histogram()[0])
    (output / 'pixels.json').write_text(json.dumps(comparisons, indent=2), encoding='utf-8')
    print(json.dumps(comparisons), flush=True)


def analyze_reuse(output):
    from PIL import Image, ImageChops
    plan=json.loads((output/'plan.json').read_text(encoding='utf-8'))
    results={}
    for point in plan['capture_points']:
        suffixes=['base','noise','full','restore']+(['control','control-restore'] if plan.get('negative_control') else [])
        labels=[f'p{point}-{s}' for s in suffixes]
        for label in labels:
            destination=output/f'{label}.png'
            if not destination.exists():shutil.copy2(MOD/'framecmp'/f"{plan.get('capture_prefix',plan['tag'])}_{label}.png",destination)
        with Image.open(output/f'{labels[0]}.png') as raw:base=raw.convert('RGBA')
        for label in labels[1:]:
            with Image.open(output/f'{label}.png') as raw:current=raw.convert('RGBA')
            assert current.size==base.size
            d=ImageChops.difference(base,current);bands=d.split();mask=bands[0]
            for band in bands[1:]:mask=ImageChops.lighter(mask,band)
            results[label]=dict(max_rgba=[v[1] for v in d.getextrema()],changed_pixels=base.width*base.height-mask.histogram()[0])
    results['exact_pass']=all(v['changed_pixels']==0 for k,v in results.items() if not k.endswith('-control'))
    if plan.get('negative_control'):
        results['visible_control_pass']=any(v['changed_pixels']>0 for k,v in results.items() if k.endswith('-control'))
    (output/'pixels.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    print(json.dumps(results),flush=True)


def screen_preferences():
    import winreg
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,r'Software\7th Beat Games\A Dance of Fire and Ice') as key:
        values={}
        for i in range(winreg.QueryInfoKey(key)[1]):
            name,value,typ=winreg.EnumValue(key,i)
            if name.startswith('Screenmanager '):
                assert typ==winreg.REG_DWORD,'Unrecognized screen preference type'
                values[name]=[value,typ]
        return values


def restore_screen_preferences(saved):
    import winreg
    now=screen_preferences()
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,r'Software\7th Beat Games\A Dance of Fire and Ice',0,winreg.KEY_SET_VALUE) as key:
        for name in now.keys()-saved.keys():winreg.DeleteValue(key,name)
        for name,(value,typ) in saved.items():winreg.SetValueEx(key,name,0,typ,value)
    return screen_preferences()==saved


def observe_boot(output, stop):
    """Read-only boot health; ends before gameplay and performs no UI action."""
    started=time.time(); deadline=time.monotonic()+90; position=0; tail=b''
    path=Path(S.PLAYER_LOG); status='NO_FIRST_STEP_WITHIN_90S'
    try:
        while not stop.wait(1) and time.monotonic()<deadline:
            if not path.exists() or path.stat().st_mtime<started:continue
            if path.stat().st_size<position:position=0;tail=b''
            with path.open('rb') as stream:
                stream.seek(position);chunk=stream.read(256*1024);position=stream.tell()
            text=(tail+chunk).decode('utf-8',errors='replace');tail=(tail+chunk)[-512:]
            if '[StutterFix] Not loaded.' in text:
                status='MOD_LOAD_FAILED';break
            if '[자동 시험] [임무단계]' in text:
                status='FIRST_STEP_OBSERVED';break
        if stop.is_set() and status=='NO_FIRST_STEP_WITHIN_90S':status='RUN_ENDED_BEFORE_FIRST_STEP_OBSERVED'
    except Exception as error:status='OBSERVER_ERROR:'+repr(error)
    (output/'boot-health.json').write_text(json.dumps(dict(status=status,read_only=True,worker_ends_at_first_step=True)),encoding='utf-8')


def run(mapkey, kind, windowed=False, isolated_mods=False):
    stamp = time.strftime('%Y%m%d-%H%M%S')
    name = f'continue-{stamp}-{mapkey}-{kind}'
    capture_prefix='ia'+time.strftime('%m%d%H%M%S')+'-'+mapkey
    output = WT / 'out' / name
    output.mkdir(parents=True, exist_ok=False)
    reuse=kind.startswith('reuse-')
    single_visual='-singlearray-' in kind and kind.endswith('-visual')
    single_perf='-singlearray-' in kind and kind.endswith('-perf')
    if '-singlearray-' in kind:
        lab=WT/'out/singlearray-lab'
        fixture=json.loads((lab/'result.json').read_text(encoding='utf-8'))
        assert not fixture['error'] and fixture['bundleBuilt'] and fixture['genericDifferences']==0 and fixture['differences']==0,'Shader fixture incomplete or failed'
        if '-direct-' in kind:assert fixture['directConditions']>=768,'Direct texture fixture incomplete'
        if '-lean-' in kind:assert fixture.get('leanConditions')==1152,'Lean vertex fixture incomplete'
        assert (lab/'bundle/stutterfix_singlearray_research').stat().st_mtime >= (lab/'Assets/Shaders/SingleArray.shader').stat().st_mtime,'Shader bundle predates its source'
    reuse_control=single_visual or reuse and (kind.endswith('-control') or kind.endswith('-check'))
    reuse_visual=reuse and (kind.endswith('-visual') or reuse_control)
    collapse_cost=kind in ('collapse-cost','collapse-cost-wait') or kind.endswith('-collapse-cost')
    collapse_phase=kind.endswith('-phase-collapse-cost')
    fragment_cost=kind.endswith('-fragment-cost')
    live_profile=kind.endswith('-live-profile')
    probe_cost=kind.endswith('-probe-selfcost')
    unowned_cost=kind.endswith('-unowned-cost')
    render_split=kind.endswith('-render-split')
    render_textures=kind.endswith('-render-textures')
    repeat_audit=kind.endswith('-repeat-audit')
    sprite_visual=kind.endswith('-sprite-discard')
    sprite_cost=kind.endswith('-sprite-discard-cost')
    sprite_early=kind.endswith('-sprite-early-cost')
    wait_perf='-waitskip-' in kind and kind.endswith('-perf')
    cpu_profile=kind.endswith('-thread-cpu-profile')
    leaf_profile=kind.endswith('-leaf-profile')
    manager_profile=kind.endswith('-game-manager-profile')
    gpudeco_profile=kind.endswith('-game-gpudeco-profile')
    callback_profile=manager_profile or gpudeco_profile or kind.endswith('-game-callback-profile')
    cycle_gpu_profile=kind.endswith('-game-cycle-profile')
    cycle_profile=cycle_gpu_profile or callback_profile
    game_cpu=kind.endswith('-game-thread-cpu-profile') or cycle_profile
    cohort_mode='-cohort-' in kind
    clockarrays='-clockarrays-' in kind
    clock_visual=kind.endswith(('-clock-visual','-clock-force-visual','-cohort-visual','-clockarrays-on-visual'))
    clock_verify=kind.endswith('-clock-verify') or clock_visual
    clock_perf=('-clockmemo-' in kind or cohort_mode or clockarrays) and kind.endswith('-perf')
    clock_command='clockarrays' if clockarrays else 'clockcohort' if cohort_mode else 'clockmemo'
    mask_visual=kind.endswith('-sprite-mask')
    mask_cost=kind.endswith('-sprite-mask-cost')
    mask_live_visual=kind.endswith('-mask-live-visual')
    mask_live_perf='-mask-live-' in kind and kind.endswith('-perf')
    mask_live=mask_live_visual or mask_live_perf
    if mask_live:
        callbacks=json.loads((WT/'out/singlearray-lab/sprite-callback-result.json').read_text())
        assert callbacks['passed'] and callbacks['selfUnregister'] and callbacks['explicitUnregister'],'Sprite callback lifecycle fixture failed'
    mask_mode=mask_visual or mask_cost or mask_live
    row_visual=kind.endswith('-sprite-rows')
    row_cost=kind.endswith('-sprite-rows-cost')
    row_mode=row_visual or row_cost
    channel_visual=kind.endswith('-sprite-channels') or mask_visual or mask_live_visual or row_visual
    channel_cost=kind.endswith('-sprite-channels-cost') or mask_cost or row_cost
    channel_lab=WT/'out/singlearray-lab'
    channel_kind='row' if row_mode else 'mask' if mask_mode else 'channel'
    channel_variant='rows' if row_mode else 'mask' if mask_mode else 'channels'
    source_bundle=channel_lab/(channel_kind+'-bundle')/('stutterfix_sprite_'+channel_variant)
    prepare_channels='spritechannels prepare'+(' rows' if row_mode else ' mask' if mask_mode else '')
    if channel_visual or channel_cost or mask_live_perf:
        fixture=json.loads((channel_lab/('sprite-'+channel_kind+'-result.json')).read_text())
        assert fixture['passed'] and fixture['bundleBuilt'] and fixture['conditions']==(120 if row_mode else 432 if mask_mode else 192),'Sprite shader fixture failed'
    if channel_cost:
        proofs=sorted((WT/'out').glob('continue-*-ar-reuse-gpuonly-map-on-sprite-'+channel_variant+'/pixels.json'))
        assert proofs,'No actual channel game parity evidence'
        proof=json.loads(proofs[-1].read_text());assert proof['exact_pass'] and proof['visible_control_pass'],'Channel game parity failed'
        assert digest(WT/'bin/PlayerAuto/StutterFix.dll')==digest(proofs[-1].parent/'candidate.dll'),'Game parity used a different managed DLL'
        assert digest(source_bundle)==digest(proofs[-1].parent/'channels.bundle'),'Game parity used a different shader bundle'
    if mask_live_perf:
        proofs=sorted((WT/'out').glob('continue-*-ar-reuse-gpuonly-map-on-mask-live-visual/pixels.json'))
        assert proofs,'No live endpoint game parity evidence'
        proof=json.loads(proofs[-1].read_text());assert proof['exact_pass'] and proof['visible_control_pass'],'Live endpoint parity failed'
        assert digest(WT/'bin/PlayerAuto/StutterFix.dll')==digest(proofs[-1].parent/'candidate.dll'),'Live endpoint used another managed DLL'
        assert digest(WT/'out/singlearray-lab/mask-bundle/stutterfix_sprite_mask')==digest(proofs[-1].parent/'channels.bundle'),'Live endpoint used another shader bundle'
    if sprite_visual or sprite_cost or sprite_early:
        fixture=json.loads((WT/'out/singlearray-lab/sprite-discard-result.json').read_text())
        assert fixture['passed'] and fixture['bundleBuilt'] and fixture['conditions']==144,'Sprite discard fixture incomplete'
    if sprite_cost:
        proof=json.loads((WT/'out/continue-20261111-023401-ar-reuse-gpuonly-map-on-vertex-sprite-discard/pixels.json').read_text())
        assert proof['exact_pass'] and proof['visible_control_pass'],'Sprite discard game parity missing'
    collapse_clean=kind=='collapse-clean'
    cfg, commands = B.CFG2['gBs3k' if kind == 'array' or reuse or collapse_cost or collapse_clean else 'rlP']
    if wait_perf:
        wait_enabled='-on-perf' in kind
        cfg=dict(cfg,EngineWaitSkip=wait_enabled,DefaultsGen=3)
        commands=['waitskip '+('on' if wait_enabled else 'off') if c.startswith('waitskip ') else c for c in commands]
    if collapse_clean:
        cfg=dict(cfg,EngineWaitSkip=False,DefaultsGen=2)
        commands=[c for c in commands if not c.startswith('waitskip ')]
    steps = ['timeout 900', 'wait 2', 'auto on', 'fpscap 0']
    if reuse:steps+=['imagearrayreuse '+('off' if kind=='reuse-off' else 'on'),'imagearrayreuse gpuonly '+('on' if 'gpuonly' in kind else 'off')]
    map_batch=kind.startswith(('reuse-gpuonly-map-off','reuse-gpuonly-map-on'))
    map_on=kind.startswith('reuse-gpuonly-map-on')
    if map_batch:steps+=['gpudeco drawcollapse mapbatch '+('on' if map_on else 'off')]
    if (clock_verify or clock_perf) and not clockarrays:
        steps+=['gpudeco '+clock_command+' '+('verify' if clock_verify else 'on' if '-on-perf' in kind else 'off')]
    if '-singlearray-' in kind:
        steps+=['gpudeco drawcollapse singlearray '+str(output/'singlearray.bundle')]
        steps+=['gpudeco drawcollapse singlearray optimize '+('off' if kind.endswith('-off-perf') and '-lean-' not in kind else 'on')]
        steps+=['gpudeco drawcollapse singlearray lean '+('on' if '-lean-' in kind and not kind.endswith('-off-perf') else 'off')]
    steps+=(list(commands)+['game '+B.M+B.MAPS[mapkey][0],'press']) if game_cpu else ['open ' + B.M + B.MAPS[mapkey][0]] + list(commands)
    if live_profile:
        steps+=['gpuprobe on'] # Existing explicit probe; installation is outside the measured window.
    points=([258] if kind.endswith('-check') or single_visual else [95,258]) if reuse_control else [258,264,269]
    if single_visual:points=[264 if mapkey=='wf' else B.MAPS[mapkey][1]]
    at = points[0] if reuse_control else B.MAPS[mapkey][1]
    full=reuse and kind.endswith('-full')
    camera_probe='-camera-' in kind
    steps += ([] if kind == 'array' or reuse or collapse_cost or collapse_clean else ['runlod on'])
    if cycle_profile:
        steps += [f'waitsongpos {point} 120' for point in (60,120,180,240,258)]
        callback_mode='manager' if manager_profile else 'gpudeco' if gpudeco_profile else 'on'
        steps += ['phasewindow on','phasewindow callbacks '+callback_mode if callback_profile else 'gpuprobe on',
                  'waitsongpos 262 90']
    else:
        steps += [f'waitsongpos {point} 120' for point in (60,120,180,240,262)] if game_cpu else ['play','waitsongpos 1 90'] if full else ['seek 262 8','waitsongpos 260 400'] if mask_live else [f'seek {at} 8', f'waitsongpos {at} 400']
    if mask_live:
        steps+=['spritechannels '+str(output/'channels.bundle'),'spritechannels prepare mask live','wait 1','spritechannels state']
        if mask_live_visual or '-on-perf' in kind:steps+=['spritechannels on live']
    if camera_probe:steps+=['cameraframeprobe on']
    if full:
        steps+=['imagearrayreuse state']
        if map_batch:steps+=['gpudeco drawcollapse mapbatch reset']
        if map_batch and not camera_probe:steps+=['slowfx arm 274 275 0.5']
        if '-slow-' in kind:steps+=['slowfx arm 1 365 0.5']
        steps+=['waitend 600']
        if '-slow-' in kind:steps+=['slowfx report','slowfx off']
        if camera_probe:steps+=['cameraframeprobe report','cameraframeprobe off']
        if map_batch and not camera_probe:steps+=['slowfx report','slowfx off','gpudeco drawcollapse mapbatch state','pause','gpudeco drawcollapse mapbatch check','resume']
        if map_batch and camera_probe:steps+=['gpudeco drawcollapse mapbatch state']
        steps+=['imagearrayreuse state','gpudeco cost','wait 1','quit']
    elif collapse_cost:
        if kind.endswith('-wait'):steps+=['waitskip off']
        steps+=['pause','wait 2','gpudeco drawcollapse diag state']
        if collapse_phase:steps+=['phasewindow on']
        for label,freeze,draw in [('normal',False,True),('frozen',True,True),('neither',True,False),('compute-only',False,False),('restore',False,True)]:
            steps+=['gpudeco drawcollapse diag freeze '+('on' if freeze else 'off'),'gpudeco drawcollapse diag draw '+('on' if draw else 'off'),'wait 2',
                    'gpudeco drawcollapse diag reset']
            if collapse_phase:steps+=['phasewindow begin']
            steps+=['fpsmark start','wait 8',f'fpsmark end {label}']
            if collapse_phase:steps+=['phasewindow end']
            steps+=['gpudeco drawcollapse diag state']
        if collapse_phase:steps+=['phasewindow off']
        steps+=['framecmp filters on','framecmp mask none']
        for label in ['base','noise','hidden','restore']:
            if label=='hidden':steps+=['gpudeco drawcollapse diag draw off','wait 2']
            if label=='restore':steps+=['gpudeco drawcollapse diag draw on','wait 2']
            steps+=[f'framecmp grab {capture_prefix}_{label}',f'framecmp save {capture_prefix}_{label}','framecmp clear']
        steps+=['gpudeco drawcollapse diag state','resume','wait 1']
        if collapse_phase:steps+=['set GpuDeco 0','gpudeco drawcollapse singlearray state']
        steps+=['quit']
    elif kind == 'visual':
        def shot(label):
            return [f'framecmp grab {name}_{label}', f'framecmp save {name}_{label}', 'framecmp clear']
        steps += ['pause', 'wait 2', 'framecmp filters on', 'framecmp mask none', 'runlod force', 'runlod resume', 'wait 1', 'runlod state']
        steps += shot('base') + shot('noise')
        steps += ['runlod cpu on', 'runlod verify on', 'wait 2', 'runlod state'] + shot('cpu')
        steps += ['runlod particles on', 'wait 2', 'runlod state'] + shot('particles')
        steps += ['runlod particles off', 'runlod cpu off', 'runlod verify off', 'wait 1', 'runlod state'] + shot('restore')
        steps += ['runlod force off', 'resume', 'wait 1', 'quit']
    elif cycle_profile:
        if cycle_gpu_profile:steps+=['gpuprobe reset']
        steps+=['gpudeco cost reset','cpuwindow begin','phasewindow begin','phasewindow cpu begin',
                'fpsmark start','waitsongpos 274 90','fpsmark end phase-cpu-game','phasewindow cpu end','phasewindow end',
                'cpuwindow end','gpudeco cost']
        if cycle_gpu_profile:steps+=['gpuprobe state','gpuprobe off']
        steps+=['phasewindow off','set GpuDeco 0','wait 1','quit']
    elif clock_visual:
        for point in (262,266,270,274):
            steps += [f'waitsongpos {point} 90','gpudeco clockaudit']
        steps+=['gpudeco '+clock_command+' state','pause','wait 2','framecmp filters on','framecmp mask none']
        def clock_grab(label):return [f'framecmp grab {capture_prefix}_p274-{label}',f'framecmp save {capture_prefix}_p274-{label}','framecmp clear']
        steps+=clock_grab('base')+clock_grab('noise')+['gpudeco clockmemo reference','wait 2']+clock_grab('full')+clock_grab('restore')
        hide='suppress on' if '-clock-force-' in kind or cohort_mode or clockarrays else 'draw off'
        show='suppress off' if '-clock-force-' in kind or cohort_mode or clockarrays else 'draw on'
        steps+=['gpudeco drawcollapse diag '+hide,'wait 2']+clock_grab('control')
        steps+=['gpudeco drawcollapse diag '+show,'wait 2']+clock_grab('control-restore')
        steps+=['gpudeco cost','set GpuDeco 0','wait 1','quit']
    elif clock_verify or kind.endswith('-clock-audit'):
        for point in (262,266,270,274):
            steps += [f'waitsongpos {point} 90','gpudeco clockaudit']
        steps+=['gpudeco clockmemo state','gpudeco cost','set GpuDeco 0','wait 1','quit']
    elif clock_perf:
        steps+=['gpudeco '+clock_command+' state','gpudeco cost reset']
        if clockarrays:steps+=['cpuwindow begin']
        steps+=['fpsmark start','waitsongpos 274 90','fpsmark end '+clock_command+'-'+('on' if '-on-perf' in kind else 'off')]
        if clockarrays:steps+=['cpuwindow end']
        steps+=['gpudeco '+clock_command+' state','gpudeco cost','set GpuDeco 0','gpudeco '+clock_command+' state','wait 1','quit']
    elif leaf_profile:
        steps+=['leafprobe begin 50 '+str(output/'leaf.json'),'waitsongpos 274 90','leafprobe end',
                'gpudeco cost','set GpuDeco 0','wait 1','quit']
    elif cpu_profile:
        steps+=['cpuwindow begin','fpsmark start','waitsongpos 274 90','fpsmark end thread-cpu-live','cpuwindow end',
                'gpudeco cost']
        if game_cpu:steps+=['waitend 120','gpudeco cost']
        steps+=['set GpuDeco 0','wait 1','quit']
    elif wait_perf:
        steps+=['fpsmark start','waitsongpos 274 90','fpsmark end waitskip-'+('on' if wait_enabled else 'off'),
                'gpudeco cost','set GpuDeco 0','wait 1','quit']
    elif mask_live_perf:
        steps+=['waitsongpos 262 90','spritechannels state','fpsmark start','waitsongpos 274 90','fpsmark end live-mask-'+('on' if '-on-perf' in kind else 'off'),
                'spritechannels state','gpudeco cost','spritechannels off','spritechannels stop','set GpuDeco 0','wait 1','quit']
    elif mask_live_visual:
        point=269
        steps+=['waitsongpos 269 90','spritechannels state','pause','wait 2','spritechannels state','framecmp filters on','framecmp mask none']
        def live_grab(label):return [f'framecmp grab {capture_prefix}_p{point}-{label}',f'framecmp save {capture_prefix}_p{point}-{label}','framecmp clear']
        steps+=live_grab('full')+['spritechannels off','wait 2']+live_grab('base')+live_grab('noise')
        steps+=['spritechannels on','spritechannels control on','wait 2']+live_grab('control')
        steps+=['spritechannels control off','wait 2']+live_grab('control-restore')+['spritechannels off','wait 2']+live_grab('restore')
        steps+=['spritechannels stop','resume','wait 1','set GpuDeco 0','quit']
    elif sprite_early:
        steps+=['pause','wait 2','spritediscard '+str(output/'sprite.bundle'),'gpuprobe on']
        for label,active in [('original',False),('early-discard',True),('restored',False)]:
            steps+=['spritediscard on vertex','spritediscard early on'] if active else ['spritediscard early off','spritediscard off'] if label=='restored' else ['spritediscard off']
            steps+=['wait 3','spritediscard state','gpuprobe reset','fpsmark start','wait 10','fpsmark end '+label,'gpuprobe state','spritediscard state']
        steps+=['gpuprobe off','wait 3','framecmp filters on','framecmp mask none']
        for label in ['base','noise','hidden','restore']:
            if label=='hidden':steps+=['spritediscard on vertex','spritediscard early on','wait 2']
            if label=='restore':steps+=['spritediscard early off','spritediscard off','wait 2']
            steps+=[f'framecmp grab {capture_prefix}_{label}',f'framecmp save {capture_prefix}_{label}','framecmp clear']
        steps+=['spritediscard stop','resume','wait 1','set GpuDeco 0','quit']
    elif channel_cost:
        steps+=['pause','wait 2','spritechannels '+str(output/'channels.bundle'),'wait 3','spritechannels state',
                'fpsmark start','wait 10','fpsmark end original-clean','spritechannels state',
                prepare_channels,'wait 5','spritechannels state']
        if row_cost:steps+=['gpuprobe on']
        for label,active in [('off-before',False),('on',True),('off-after',False)]:
            steps+=['spritechannels on' if active else 'spritechannels off','wait 3','spritechannels state']
            if row_cost:steps+=['gpuprobe reset']
            steps+=['fpsmark start','wait 10','fpsmark end '+label]
            if row_cost:steps+=['gpuprobe state']
            steps+=['spritechannels state','gpudeco drawcollapse diag state']
        if row_cost:steps+=['gpuprobe off']
        steps+=['spritechannels stop','wait 3','spritechannels state','fpsmark start','wait 10','fpsmark end original-released',
                'spritechannels state','resume','wait 1','set GpuDeco 0','quit']
    elif channel_visual:
        steps+=['pause','wait 2','spritechannels '+str(output/'channels.bundle'),prepare_channels,'wait 5','spritechannels state',
                'framecmp filters on','framecmp mask none']
        def channel_grab(label):return [f'framecmp grab {capture_prefix}_p{at}-{label}',f'framecmp save {capture_prefix}_p{at}-{label}','framecmp clear']
        steps+=channel_grab('base')+channel_grab('noise')+['spritechannels on','wait 2','spritechannels state']+channel_grab('full')
        steps+=['spritechannels off','wait 2']+channel_grab('restore')
        steps+=['spritechannels on','spritechannels control on','wait 2']+channel_grab('control')
        steps+=['spritechannels control off','spritechannels off','wait 2']+channel_grab('control-restore')
        steps+=['spritechannels stop','resume','wait 1','set GpuDeco 0','quit']
    elif sprite_cost:
        steps+=['pause','wait 2','spritediscard '+str(output/'sprite.bundle')]
        for label,active in [('off-before',False),('on',True),('off-after',False)]:
            steps+=['spritediscard on vertex' if active else 'spritediscard off','wait 3','spritediscard state',
                    'fpsmark start','wait 10','fpsmark end '+label,'spritediscard state','gpudeco drawcollapse diag state']
        steps+=['spritediscard stop','resume','wait 1','set GpuDeco 0','quit']
    elif sprite_visual:
        sprite_on='spritediscard on vertex' if '-vertex-' in kind else 'spritediscard on'
        steps+=['pause','wait 2','spritediscard '+str(output/'sprite.bundle'),'framecmp filters on','framecmp mask none']
        def sprite_grab(label):return [f'framecmp grab {capture_prefix}_p{at}-{label}',f'framecmp save {capture_prefix}_p{at}-{label}','framecmp clear']
        steps+=sprite_grab('base')+sprite_grab('noise')+[sprite_on,'wait 2','spritediscard state']+sprite_grab('full')
        steps+=['spritediscard off','wait 2']+sprite_grab('restore')
        steps+=[sprite_on,'spritediscard control on','wait 2']+sprite_grab('control')
        steps+=['spritediscard control off','spritediscard off','wait 2']+sprite_grab('control-restore')
        steps+=['spritediscard state','spritediscard stop','resume','wait 1','set GpuDeco 0','quit']
    elif repeat_audit:
        steps+=['pause','wait 2','repeataudit '+str(output/'repeat-renderers.json'),
                'gpudeco drawcollapse diag state','resume','wait 1','set GpuDeco 0','quit']
    elif render_split or render_textures:
        steps+=['pause','wait 2','gpuprobe on']
        conditions=[('all-before',[])]
        if render_textures:
            # Sprite asset names and the bound textures differ for these decorations.
            # Use the observed order buckets and retain the exact hidden item logs.
            for label,order_range in (('bg-950','-950,-950'),('bg-6','-6,-6'),
                                      ('bg-5','-5,-5'),('city-band','-809,-801'),('grass-band','97,100')):
                conditions.append(('without-'+label,['hidegroup unowned off','hidegroup unowned-order:'+order_range+' on']))
        else:
            conditions+=[
                ('without-proxies',['gpudeco drawcollapse diag suppress on']),
                ('without-either',['hidegroup unowned on']),
                ('without-unowned',['gpudeco drawcollapse diag suppress off']),
            ]
        conditions.append(('all-after',['hidegroup unowned off']))
        for label,toggles in conditions:
            steps+=toggles+['wait 2','gpudeco drawcollapse diag state','gpuprobe reset',
                            'fpsmark start','wait 8','fpsmark end '+label,'gpuprobe state']
        steps+=['gpuprobe off','resume','wait 1','set GpuDeco 0','quit']
    elif unowned_cost:
        steps+=['pause','wait 2']
        for label,hide in [('normal',False),('unowned-hidden',True),('restore',False)]:
            steps+=['hidegroup unowned '+('on' if hide else 'off'),'wait 2','fpsmark start','wait 8','fpsmark end '+label]
        # Descriptor reads and screenshots are outside all FPS intervals.
        steps+=['gpucensus','wait 3','framecmp filters on','framecmp mask none']
        for label in ['base','noise','hidden','restore']:
            if label=='hidden':steps+=['hidegroup unowned on','wait 2']
            if label=='restore':steps+=['hidegroup unowned off','wait 2']
            steps+=[f'framecmp grab {capture_prefix}_{label}',f'framecmp save {capture_prefix}_{label}','framecmp clear']
        steps+=['gpudeco drawcollapse diag state','resume','wait 1','set GpuDeco 0','quit']
    elif probe_cost:
        steps+=['pause','wait 2']
        for label,mode in [('probe-off-before','off'),('probe-on','on'),('probe-off-after','off')]:
            steps+=['gpuprobe '+mode,'wait 3']
            if mode=='on':steps+=['gpuprobe reset']
            steps+=['fpsmark start','wait 8','fpsmark end '+label]
            if mode=='on':steps+=['gpuprobe state']
        # Synchronous existing vertex readback occurs ONLY after all FPS windows.
        steps+=['coverage','gpudeco drawcollapse diag state','resume','wait 1','set GpuDeco 0','quit']
    elif live_profile:
        steps+=['phasewindow on','wait 2','gpuprobe reset','phasewindow begin','fpsmark start','wait 8',
                'fpsmark end instrumented-live','phasewindow end','gpuprobe state','gpudeco cost',
                'phasewindow off','gpuprobe off','set GpuDeco 0','quit']
    elif fragment_cost:
        steps+=['pause','wait 2','sfflat off','sfdebug 0']
        for label,flat,debug in [('normal',False,0),('flat-color',True,0),('early-discard',False,3),('late-discard',False,1),('quarter-output',False,2),('restore',False,0)]:
            steps+=['sfflat '+('on' if flat else 'off'),'sfdebug '+str(debug),'wait 2','fpsmark start','wait 8',f'fpsmark end {label}','gpudeco drawcollapse diag state']
        steps+=['framecmp filters on','framecmp mask none']
        for label in ['base','noise','hidden','restore']:
            if label=='hidden':steps+=['sfflat on','wait 2']
            if label=='restore':steps+=['sfflat off','sfdebug 0','wait 2']
            steps+=[f'framecmp grab {capture_prefix}_{label}',f'framecmp save {capture_prefix}_{label}','framecmp clear']
        steps+=['gpudeco drawcollapse singlearray state','resume','wait 1','set GpuDeco 0','quit']
    elif single_perf:
        steps+=['gpudeco drawcollapse singlearray state','fpsmark start','wait 12',f'fpsmark end {name}',
                'gpudeco drawcollapse singlearray state','gpudeco cost','set GpuDeco 0',
                'gpudeco drawcollapse singlearray state','wait 1','quit']
    elif map_batch and not reuse_visual:
        steps+=['waitsongpos 270 90','gpudeco drawcollapse mapbatch reset','slowfx arm 274 275 0',
                'fpsmark start','waitsongpos 276 90','fpsmark end map-window','slowfx report','slowfx off',
                'gpudeco drawcollapse mapbatch state','pause','gpudeco drawcollapse mapbatch check']
        if map_on:steps+=['gpudeco drawcollapse mapbatch fixture']
        steps+=['resume','wait 1','gpudeco drawcollapse mapbatch state','gpudeco cost','quit']
    elif reuse and kind.endswith('-tag-window'):
        steps+=['waitsongpos 270 90','gpudeco drawcollapse tagcensus p8-9','slowfx arm 274 275 0',
                'waitsongpos 276 90','slowfx report','slowfx off','gpudeco drawcollapse tagcensus p8-9','imagearrayreuse state','gpudeco cost','quit']
    elif reuse and kind.endswith('-slow-window'):
        steps+=['slowfx arm 270 282 0','waitsongpos 282 90','slowfx report','slowfx off','imagearrayreuse state','gpudeco cost','quit']
    elif reuse and kind.endswith('-audit'):
        steps+=['pause','wait 2','imagearrayreuse state','imagearrayreuse audit','imagearrayreuse state','resume','wait 1','quit']
    elif reuse_visual:
        for index,point in enumerate(points):
            if index:steps+=['resume','stop',f'seek {point} 8',f'waitsongpos {point} 400']
            steps+=['pause','wait 2','framecmp filters on','framecmp mask none','imagearrayreuse state']
            prefix='gpudeco drawcollapse singlearray' if single_visual else 'imagearrayreuse'
            def grab(label):return ([] if single_visual else ['imagearrayreuse refresh'])+[f'framecmp grab {capture_prefix}_p{point}-{label}',f'framecmp save {capture_prefix}_p{point}-{label}','framecmp clear']
            steps+=grab('base')+grab('noise')+[prefix+' reference on','wait 2']+grab('full')
            steps+=[prefix+' reference off','wait 2']+grab('restore')
            if reuse_control:
                control=' control2d' if single_visual and '-direct-' in kind else ' control'
                steps+=[prefix+control+' on','wait 2']+grab('control')+[prefix+control+' off','wait 2']+grab('control-restore')
            if single_visual:steps+=[prefix+' state']
        steps+=['imagearrayreuse state','resume','wait 1','quit']
    elif kind == 'array' or reuse or collapse_clean:
        steps += (['imagearrayreuse state'] if reuse else [])+['fpsmark start', 'wait 12', f'fpsmark end {name}']
        steps += (['imagearrayreuse state'] if reuse else [])+['gpudeco cost', 'wait 1', 'quit']
    elif kind == 'verify':
        steps += ['runlod cpu on', 'runlod verify on', 'wait 12', 'runlod state', 'runlod verify off', 'runlod off', 'runlod state', 'quit']
    else:
        for index, on in enumerate([False, True, True, False]):
            if index:
                steps += ['stop', f'seek {at} 8', f'waitsongpos {at} 400']
            steps += ['runlod cpu ' + ('on' if on else 'off'), 'wait 2', 'fpsmark start', 'wait 12', f'fpsmark end {name}_{index}', 'runlod state']
        steps += ['quit']
    plan = dict(steps=steps, settings=dict(B.CLEAN, **cfg), timeout_min=15, tag=name)
    if reuse_visual:
        plan['capture_points']=points
        plan['capture_prefix']=capture_prefix
        if reuse_control:plan['negative_control']=True
    if collapse_cost or fragment_cost or unowned_cost or sprite_early:plan['capture_prefix']=capture_prefix
    if sprite_visual:plan.update(capture_prefix=capture_prefix,capture_points=[at],negative_control=True)
    if channel_visual:plan.update(capture_prefix=capture_prefix,capture_points=[at],negative_control=True)
    if mask_live_visual:plan.update(capture_prefix=capture_prefix,capture_points=[269],negative_control=True)
    if clock_visual:plan.update(capture_prefix=capture_prefix,capture_points=[274],negative_control=True)
    for step in steps:
        if step.startswith(('framecmp grab ','framecmp save ')):
            assert len(step.split()[-1])<=64,'Capture identifier exceeds AutoTest limit'
    if single_visual:
        assert sum(step.startswith('framecmp grab ') for step in steps)==6,'Incomplete shader comparison plan'
        assert any('singlearray reference on' in step for step in steps)
        assert any('singlearray control' in step and step.endswith(' on') for step in steps)
    (output / 'plan.json').write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
    with GameLock(name, lambda s: print(s, flush=True)):
        if S.game_running():
            raise RuntimeError('Existing game: no installation or launch')
        files = [MOD / n for n in ('StutterFix.dll', 'sfnative.dll', 'Settings.xml', 'gpuprobe.dll', 'autotest.txt', 'autotest-steps.json', 'autotest.done', 'autotest.end')] + [BOOT]
        umm=BOOT.parent/'Managed/UnityModManager/Params.xml'
        if isolated_mods:
            assert umm.exists(),'No UMM configuration to snapshot'
            files.append(umm)
        saved_prefs=screen_preferences() if windowed else None
        original_launch=S.launch_game
        if windowed:
            import winreg
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER,r'Software\Valve\Steam') as key:steam=winreg.QueryValueEx(key,'SteamExe')[0]
            app_id=S.STEAM_URL.rstrip('/').rsplit('/',1)[-1]
            assert app_id.isdigit(),'Unrecognized configured Steam launch URL'
            app_manifest=Path(S.GAME_DIR).parents[1]/('appmanifest_'+app_id+'.acf')
            assert app_manifest.exists(),'Configured Steam app is not installed in the expected library'
            assert Path(S.GAME_DIR).name in app_manifest.read_text(encoding='utf-8'),'Steam manifest does not match configured game'
            S.launch_game=lambda: subprocess.Popen([steam,'-applaunch',app_id,'-screen-fullscreen','0'])
            (output/'screen-preferences-before.json').write_text(json.dumps(saved_prefs,indent=2),encoding='utf-8')
        before = {str(p): digest(p) for p in files}
        for i, p in enumerate(files):
            if p.exists(): shutil.copy2(p, output / f'original-{i}')
        source = WT / ('bin/PlayerAutoClockArrays/StutterFix.dll' if clockarrays and '-clockarrays-on-' in kind
                       else 'bin/PlayerAuto/StutterFix.dll')
        shutil.copy2(source, output / 'candidate.dll')
        bundle=None
        if '-singlearray-' in kind:
            bundle=output/'singlearray.bundle'
            shutil.copy2(WT/'out/singlearray-lab/bundle/stutterfix_singlearray_research',bundle)
        if sprite_visual or sprite_cost or sprite_early:
            bundle=output/'sprite.bundle'
            shutil.copy2(WT/'out/singlearray-lab/sprite-bundle/stutterfix_sprite_discard',bundle)
        if channel_visual or channel_cost or mask_live_perf:
            bundle=output/'channels.bundle'
            shutil.copy2(source_bundle,bundle)
        (output / 'before.json').write_text(json.dumps(dict(files=before, candidate=digest(source),shader_bundle=digest(bundle) if bundle else None), indent=2), encoding='utf-8')
        def no_forced_kill():
            raise RuntimeError('Owned run timed out; force termination disabled; originals retained in ' + str(output))
        S.kill_game = no_forced_kill
        boot_stop=threading.Event()
        boot_thread=None
        try:
            if isolated_mods:
                import xml.etree.ElementTree as ET
                root=ET.fromstring(umm.read_bytes())
                disabled=[]
                for entry in root.findall('./ModParams/Mod'):
                    if entry.get('Id')!='StutterFix':
                        if entry.get('Enabled')=='true':disabled.append(entry.get('Id'))
                        entry.set('Enabled','false')
                assert root.find('./ModParams/Mod[@Id="StutterFix"]').get('Enabled')=='true'
                umm.write_bytes(ET.tostring(root,encoding='utf-8',xml_declaration=True))
                (output/'mod-isolation.json').write_text(json.dumps(dict(disabled=disabled,temporary=True,restore_file_index=9),indent=2),encoding='utf-8')
            if live_profile or cycle_gpu_profile or probe_cost or unowned_cost or render_split or render_textures or sprite_early or row_cost:
                native=WT/'out/gpuprobe-safe/gpuprobe.dll'
                assert native.exists() and (native.parent/'check-passed.json').exists(),'Safe query fixture has not passed'
                check=json.loads((native.parent/'check-passed.json').read_text(encoding='utf-8-sig'))
                assert check['passed'] and check['plugin_sha256'].lower()==digest(native),'Safe query DLL differs from its fixture'
                shutil.copy2(native,output/'gpuprobe.dll')
                shutil.copy2(output/'gpuprobe.dll',MOD/'gpuprobe.dll')
            shutil.copy2(output / 'candidate.dll', MOD / 'StutterFix.dll')
            boot_thread=threading.Thread(target=observe_boot,args=(output,boot_stop),daemon=True)
            boot_thread.start()
            text, metrics = S.sf_run(**{k:v for k,v in plan.items() if k not in ('capture_points','negative_control','capture_prefix')})
            (output / 'summary.txt').write_text(text, encoding='utf-8')
            (output / 'metrics.json').write_text(json.dumps(metrics, ensure_ascii=False, indent=2), encoding='utf-8')
        finally:
            boot_stop.set()
            if boot_thread is not None:boot_thread.join(timeout=2)
            if S.game_running():
                raise RuntimeError('Game still running; retained backups; no DLL overwritten')
            S.launch_game=original_launch
            shutil.copy2(S.PLAYER_LOG, output / 'Player.log')
            for i, p in enumerate(files):
                saved = output / f'original-{i}'
                if saved.exists(): shutil.copy2(saved, p)
                else: p.unlink(missing_ok=True)
            after = {str(p): digest(p) for p in files}
            (output / 'restore.json').write_text(json.dumps(dict(before=before, after=after, equal=before == after), indent=2), encoding='utf-8')
            assert before == after, 'Install restoration mismatch'
            if saved_prefs is not None:
                equal=restore_screen_preferences(saved_prefs)
                (output/'screen-preferences-restore.json').write_text(json.dumps(dict(equal=equal,values=len(saved_prefs),startup='Steam -applaunch -screen-fullscreen 0; no resolution override')),encoding='utf-8')
                assert equal,'Screen preference restoration mismatch'
        log = (output / 'Player.log').read_text(encoding='utf-8', errors='replace')
        assert '[자동 시험] 끝 - 게임을 끕니다' in log, 'No successful quit marker'
        assert '단계 실패' not in log and '오류로 그만둠' not in log, 'Command failure'
        if render_textures:
            import re
            hidden=re.findall(r'\[hidegroup\] unowned-order:[^\s]+ hidden=(\d+)',log)
            assert len(hidden)==5 and all(int(n)>0 for n in hidden),'Empty or missing renderer order bucket'
        if repeat_audit:
            audit=json.loads((output/'repeat-renderers.json').read_text(encoding='utf-8-sig'))
            assert audit.get('rows') and not audit['truncated'],'Missing/truncated native renderer records'
            assert all(not row.get('error') for row in audit['rows']),'Native mesh audit has failed rows'
        if full:
            assert '[임무완주] OnLandOnPortal' in log and '곡 끝남 (' in log and '곡이 끝나지 않음' not in log, 'No confirmed natural portal completion'
            stops=[line for line in log.splitlines() if 'on=False owned=0 originals_restored=' in line]
            assert stops and all('originals_restored=True reason=Application.quitting ' in line for line in stops), 'GPU ended before normal shutdown or failed restoration'
        if kind == 'visual':
            analyze(output)
        if reuse_visual or sprite_visual or channel_visual:analyze_reuse(output)
        print('RESULT ' + str(output), flush=True)
        for line in log.splitlines():
            if any(k in line for k in ['[runlod]', '[FPS 구간]', ' 검증 다름 ', 'api=', 'shutdown', 'DRAWCOLLAPSE_ARRAY_PLAN', 'DRAWCOLLAPSE_ARRAY_TOTAL', 'DRAWCOLLAPSE_PREPARED','IMAGEARRAY_','[imagearrayreuse]','COLLAPSE_DIAG','MAP_UPLOAD_']):
                print(line, flush=True)
        return output


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    if sys.argv[1] == '--analyze-reuse':analyze_reuse(Path(sys.argv[2]))
    elif sys.argv[1] == '--analyze-only': analyze(Path(sys.argv[2]))
    else: run(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else 'visual','--windowed-startup' in sys.argv[3:],'--isolated-mods' in sys.argv[3:])
