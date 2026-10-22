"""Extract the original FK run for editor baking; requires ufbx==0.0.5.

Usage: python extract-jiuba-source-reference.py SOURCE_FBX OUTPUT_JSON
This does not change the source FBX or the running Blender session.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import traceback

repo = Path(__file__).resolve().parents[2]
local_packages = repo / '.local/tools/fbx-review'
if local_packages.exists():
    sys.path.insert(0, str(local_packages))
import ufbx

# Keep native wrappers alive together. In 0.0.5, constructing transient node-list
# wrappers during traversal can stall their finalizers. This CLI exits after flush.
native_keep = []

def extract(source: Path, output: Path):
    scene = ufbx.load_file(str(source), target_axes=ufbx.axes_left_handed_y_up,
                           target_unit_meters=1.0)
    stacks = scene.anim_stacks
    stack = stacks[0]
    anim = stack.anim
    nodes = scene.nodes
    refs = [nodes[i] for i in range(len(nodes))]
    native_keep.extend([scene, stacks, stack, anim, nodes, refs])

    def matrix(node):
        m = node.node_to_world
        return [list(m.c0), list(m.c1), list(m.c2), list(m.c3)]

    names = [n.name for n in refs if n.name]
    rest = {n.name: matrix(n) for n in refs if n.name}
    rows = []
    for i in range(61):
        time = stack.time_begin + (stack.time_end - stack.time_begin) * i / 60
        evaluated = ufbx.evaluate_scene(scene, anim, time)
        view = evaluated.nodes
        frame_refs = [view[j] for j in range(len(view))]
        by_name = {n.name: n for n in frame_refs}
        native_keep.extend([evaluated, view, frame_refs, by_name])
        rows.append({'phase': i / 60,
                     'bones': {n: matrix(by_name[n]) for n in names}})
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({'begin': stack.time_begin,
                                  'end': stack.time_end,
                                  'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                                  'rest': rest, 'rows': rows}), encoding='utf-8')
    print(f'Extracted {len(rows)} FK frames to {output}', flush=True)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    try:
        extract(args.source.resolve(), args.output.resolve())
    except Exception:
        traceback.print_exc()
        sys.stderr.flush()
        os._exit(1)
    os._exit(0)
