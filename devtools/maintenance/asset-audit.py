"""Prepare conservative Unity cleanup roots and dynamic-path seeds; reports stay local."""
import json
import re
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'cultivation'
ASSETS = PROJECT / 'Assets'
OUT = ROOT / '.local' / 'diagnostics' / 'asset-cleanup'
OUT.mkdir(parents=True, exist_ok=True)
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--targets', nargs='+', default=['Assets/Art/Environment/Imported',
    'Assets/Art/VFX/Shared', 'Assets/resources/CombatVFX'])
parser.add_argument('--merge-dependencies', action='store_true',
    help='Merge the preceding Unity dependency result before resolving C# type dependencies; repeat Unity closure afterwards.')
args = parser.parse_args()
TARGETS = args.targets
if any(not t.startswith('Assets/') or '..' in Path(t).parts for t in TARGETS):
    parser.error('Targets must be project-relative Assets directories')
files = [p for p in ASSETS.rglob('*') if p.is_file() and p.suffix != '.meta']
paths = {p.relative_to(PROJECT).as_posix(): p for p in files}
def targeted(path):
    return any(path == t or path.startswith(t + '/') for t in TARGETS)
seeds = {path for path in paths if not targeted(path)}
reasons = {}
def keep(path, reason):
    seeds.add(path)
    reasons.setdefault(path, []).append(reason)
source = '\n'.join(p.read_text(encoding='utf-8-sig', errors='replace')
                   for path, p in paths.items() if not targeted(path) and
                   p.suffix in ('.cs', '.csv', '.json', '.shader', '.asset', '.prefab', '.scene', '.unity'))
outside_code = '\n'.join(p.read_text(encoding='utf-8-sig', errors='replace')
                        for path, p in paths.items() if p.suffix == '.cs' and not targeted(path))
literals = set(re.findall(r'"([^"\r\n]+)"', source))
import csv
for p in files:
    if p.suffix == '.csv':
        for row in csv.reader(p.read_text(encoding='utf-8-sig').splitlines()): literals.update(row)
for s in tuple(literals):
    if '\\u' in s:
        try: literals.add(json.loads('"' + s + '"'))
        except ValueError: pass
for path, p in paths.items():
    if not targeted(path):
        continue
    resource = path.split('/resources/', 1)[-1].rsplit('.', 1)[0]
    if path in literals or resource in literals or any(
            path.lower().endswith(s.lower()) for s in literals
            if s.startswith('/') and '.' in s and len(s) > 5):
        keep(path, 'exact code/resource path')
    if p.stem in literals and (resource.rsplit('/', 1)[0] + '/' in literals):
        keep(path, 'Resources path built from prefix + asset name')
    # Scene builders look up imported environment models by their original file names.
    if '/Environment' in path and p.suffix.lower() in ('.prefab', '.fbx', '.obj'):
        if p.stem in literals or p.name in literals:
            keep(path, 'scene builder model name')
    if p.suffix == '.shader':
        shader_name = re.search(r'\bShader\s+"([^"]+)"', p.read_text(encoding='utf-8-sig'))
        if shader_name and shader_name.group(1) in literals: keep(path, 'Shader.Find name')
    if p.suffix == '.cs':
        classes = re.findall(r'\b(?:class|struct|enum)\s+(\w+)', p.read_text(encoding='utf-8-sig'))
        if any(re.search(r'\b' + re.escape(c) + r'\b', outside_code) for c in classes):
            keep(path, 'C# type used by project tools/runtime')
if args.merge_dependencies:
    seeds.update(json.loads((OUT / 'dependencies.json').read_text(encoding='utf-8')))
# Compiler dependencies between retained source files are not reported by AssetDatabase.
while True:
    code = outside_code + '\n' + '\n'.join(paths[p].read_text(encoding='utf-8-sig')
            for p in seeds if p in paths and paths[p].suffix == '.cs')
    added = False
    for path, p in paths.items():
        if not targeted(path) or path in seeds or p.suffix != '.cs': continue
        classes = re.findall(r'\b(?:class|struct|enum)\s+(\w+)', p.read_text(encoding='utf-8-sig'))
        if any(re.search(r'\b' + re.escape(c) + r'\b', code) for c in classes):
            keep(path, 'transitive C# type dependency'); added = True
    if not added: break
(OUT / 'inputs.json').write_text(json.dumps({'targets': TARGETS, 'seeds': sorted(seeds),
    'dynamicReasons': reasons}, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'assets': len(paths), 'seeds': len(seeds), 'dynamicSeeds': len(reasons),
    'targetFiles': {t: sum(p.startswith(t + '/') for p in paths) for t in TARGETS}}, ensure_ascii=False))
