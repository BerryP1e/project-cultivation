"""Verify cleanup introduced no references to deleted GUIDs, and check moved metadata."""
import json,re,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];project=ROOT/'cultivation';out=ROOT/'.local/diagnostics/asset-cleanup'
baselines=[json.loads(p.read_text(encoding='utf-8')) for p in out.glob('baseline-guids*.json')]
original={g:p for b in baselines for g,p in b['guids'].items()}
current={}
for p in (project/'Assets').rglob('*.meta'):
    m=re.search(r'^guid:\s*(\S+)',p.read_text(encoding='utf-8-sig',errors='replace'),re.M)
    if m:current[m[1]]=p.with_suffix('').relative_to(project).as_posix()
lost=set(original)-set(current);bad=[]
for p in (project/'Assets').rglob('*'):
    if not p.is_file() or p.suffix not in {'.asset','.prefab','.scene','.unity','.mat','.controller','.anim','.meta'}:continue
    text=p.read_text(encoding='utf-8-sig',errors='replace')
    for g in set(re.findall(r'guid:\s*([0-9a-f]{32})',text))&lost:
        bad.append({'file':p.relative_to(project).as_posix(),'deletedDependency':original[g],'guid':g})
report={'deletedGuids':len(lost),'newMissingReferences':bad,'currentGuids':len(current)}
(out/'reference-check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report if bad else {k:v for k,v in report.items() if k!='newMissingReferences'}|{'newMissingReferences':0},ensure_ascii=False,indent=2))
sys.exit(bool(bad))
