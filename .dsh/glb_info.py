# -*- coding: utf-8 -*-
"""Print the structure of a .glb file (chunks + JSON summary). No third-party deps."""
import json
import struct
import sys


def read_glb(path):
    with open(path, 'rb') as f:
        data = f.read()
    magic, version, length = struct.unpack_from('<4sII', data, 0)
    assert magic == b'glTF', 'not a glb: %r' % magic
    off = 12
    chunks = []
    while off < length:
        clen, ctype = struct.unpack_from('<I4s', data, off)
        body = data[off + 8: off + 8 + clen]
        chunks.append((ctype.decode('ascii', 'replace').strip('\x00'), body))
        off += 8 + clen
    return version, length, chunks


def main(path):
    version, length, chunks = read_glb(path)
    print('file      :', path)
    print('glb ver   :', version, ' total', length, 'bytes')
    for name, body in chunks:
        print('chunk     :', name, len(body), 'bytes')

    js = None
    bin_len = 0
    for name, body in chunks:
        if name == 'JSON':
            js = json.loads(body.decode('utf-8'))
        elif name.startswith('BIN'):
            bin_len = len(body)
    print('bin chunk :', bin_len, 'bytes')

    print('asset     :', js.get('asset'))
    for key in ('scenes', 'nodes', 'meshes', 'materials', 'textures', 'images',
                'samplers', 'accessors', 'bufferViews', 'buffers', 'skins',
                'animations', 'cameras'):
        v = js.get(key)
        print('%-11s: %s' % (key, len(v) if v is not None else 0))

    if js.get('extensionsUsed'):
        print('extUsed   :', js['extensionsUsed'])

    for i, m in enumerate(js.get('meshes', [])):
        prims = m.get('primitives', [])
        print('mesh[%d] %-24s prims=%d' % (i, m.get('name'), len(prims)))
        for j, p in enumerate(prims):
            attrs = p.get('attributes', {})
            idx = p.get('indices')
            count = None
            if idx is not None:
                count = js['accessors'][idx].get('count')
            print('   prim[%d] mode=%s mat=%s verts=%s indices=%s attrs=%s' % (
                j, p.get('mode', 4), p.get('material'),
                js['accessors'][attrs['POSITION']]['count'] if 'POSITION' in attrs else '?',
                count, sorted(attrs.keys())))

    for i, mat in enumerate(js.get('materials', [])):
        pbr = mat.get('pbrMetallicRoughness', {})
        print('mat[%d] %-24s baseColor=%s tex=%s metallic=%s rough=%s' % (
            i, mat.get('name'), pbr.get('baseColorFactor'),
            pbr.get('baseColorTexture'), pbr.get('metallicFactor'), pbr.get('roughnessFactor')))

    for i, img in enumerate(js.get('images', [])):
        print('img[%d] %-22s mime=%s bufferView=%s uri=%s' % (
            i, img.get('name'), img.get('mimeType'), img.get('bufferView'), img.get('uri')))

    for i, n in enumerate(js.get('nodes', [])):
        print('node[%d] %-22s mesh=%s children=%s T=%s R=%s S=%s' % (
            i, n.get('name'), n.get('mesh'), n.get('children'),
            n.get('translation'), n.get('rotation'), n.get('scale')))


if __name__ == '__main__':
    main(sys.argv[1])
