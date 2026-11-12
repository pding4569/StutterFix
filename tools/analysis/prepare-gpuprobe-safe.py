"""Create a separate diagnostic query plugin; leave Claude's evidence unchanged."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = root / 'tools/gpudeco-lab/evidence/gpuprobe-query-pstat.cpp'
out = root / 'out/gpuprobe-safe'
out.mkdir(exist_ok=True)
original = source.read_text(encoding='utf-8')
text = original
old = 'ctx->End(ring[active].b.Get());ctx->End(ring[active].disjoint.Get());'
assert text.count(old) == 1
text = text.replace(old, 'auto& f=ring[active];for(int j=0;j<f.used;j++)if(!f.seg[j].closed)ctx->End(f.seg[j].b.Get());ctx->End(f.ps.Get());ctx->End(f.b.Get());ctx->End(f.disjoint.Get());')
old = 'release(); tick=0; ++epoch; complete=bad=full=overflow=notReady=errors=outsideCamera=0;'
assert text.count(old) == 1
text = text.replace(old, old + ' psFrames=psInv=cPrim=cInv=0;')
old = 'hr=get(f.ps.Get(),&pst,sizeof pst);if(hr==S_FALSE){++notReady;return;}'
assert text.count(old) == 1
text = text.replace(old, old + 'if(FAILED(hr)){++errors;f.pending=false;return;}')
commit = '++psFrames;psInv+=pst.PSInvocations;cPrim+=pst.CPrimitives;cInv+=pst.CInvocations;'
assert text.count(commit) == 1
text = text.replace(commit, '')
guard = 'if(!TryAcquireSRWLockExclusive(&lock))return;'
assert text.count(guard) == 1
text = text.replace(guard, guard + '\n // Publish once only after ALL queries are ready and the statistics lock is held.\n ' + commit)
(out / 'query.cpp').write_text(text, encoding='utf-8')
(out / 'provenance.json').write_text(json.dumps(dict(source=str(source), source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(), candidate_sha256=hashlib.sha256((out/'query.cpp').read_bytes()).hexdigest(), changes=['end active pipeline query on release', 'reset pipeline statistics at initialization', 'reject failed pipeline query', 'publish pipeline counters after all queries and lock acquisition'], production_query_counts_changed=False, async_getdata_donotflush=True, gpu_waits_added=False, diagnostic_only=True), indent=2), encoding='utf-8')
print(out)
