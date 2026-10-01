#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
参考素材管线 —— 从 GitHub 项目 henjicc/yipiantian 取作物/田地素材，转成 Unity 能直接导入的 OBJ。

为什么要这个脚本（本机限制）：
  · 本机 PowerShell 的 TLS 是坏的（schannel SEC_E_NO_CREDENTIALS），所有 HTTPS 从 shell 都失败；
    而 Python 用 OpenSSL，HTTPS 正常 —— 所以下载走 Python。
  · 那个仓库是 Godot 工程，模型是 .glb，而 Unity 不能原生导入 glTF；
    本机也没有 Blender 可以转 FBX。所以这里自己解析 .glb 的 JSON+BIN 两个 chunk，
    直接写成 .obj + .mtl + 贴图（Unity 原生支持 OBJ）。

它是**开发期工具**，不是运行时依赖：跑完产出的是普通资产，游戏不依赖 Python。

用法：
    python fetch_and_convert.py list          # 列出可取的作物/阶段/大小，不下载
    python fetch_and_convert.py fetch         # 下载 .glb 与贴图到 _raw/
    python fetch_and_convert.py convert       # 把 _raw/*.glb 转成 Assets 下的 .obj
    python fetch_and_convert.py all           # fetch + convert

来源与授权：该仓库**没有 LICENSE**（GitHub API `license: null`），属于「保留所有权利」。
用户已明确「先不管版权，直接用它的素材」。产出物来源逐条登记在 manifest.json，
以便日后署名或整体撤换。
"""

import argparse
import hashlib
import json
import os
import struct
import sys
import urllib.request

REPO = 'henjicc/yipiantian'
BRANCH = 'main'
API_TREE = 'https://api.github.com/repos/%s/git/trees/%s?recursive=1' % (REPO, BRANCH)
# 真实文件（LFS 内容）走 media.githubusercontent；raw.githubusercontent 只会给 131 字节的指针
MEDIA = 'https://media.githubusercontent.com/media/%s/%s/' % (REPO, BRANCH)

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, '_raw')
MANIFEST = os.path.join(HERE, 'manifest.json')

# 项目根：tools/参考素材/ -> 上溯三层
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
ASSETS = os.path.join(ROOT, 'cultivation', 'Assets', 'Art', '灵田')

# 我们的灵植  ->  参考项目里长得最接近的作物（可随时改这张表重跑）
CROPS = {
    'lingcao_shengling': 'greens',          # 生灵草  —— 一丛叶子
    'lingcao_ninglu':    'chrysanthemum',   # 凝露花  —— 开花
    'lingcao_huichun':   'coriander',       # 回春草  —— 细叶
    'lingcao_qingxin':   'lettuce',         # 清心莲  —— 莲座状
    'lingcao_chiyan':    'radish',          # 赤焰芝  —— 赤红
}
STAGES = ['sprout', 'young', 'mature']
SOIL = ['Game/art/environment/soil/loam.png']
# 注：soil/loam_baked_normal.png 是 4096²、28 MB，**刻意不取** ——
#     一株田里的土不需要 4K 法线，收进仓库纯属负担（要的话把上面这行加上即可）。


def http_get(url, timeout=90):
    req = urllib.request.Request(url, headers={'User-Agent': 'ref-asset-pipeline'})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def tree_paths():
    """一次拿全仓库文件清单（只花 1 次 API 配额）"""
    data = json.loads(http_get(API_TREE).decode('utf-8'))
    return [t['path'] for t in data.get('tree', []) if t.get('type') == 'blob']


def pick_models(paths):
    """挑出每个作物每个阶段的模型。优先 _low（LOD 版，三角面少）。"""
    picked = {}
    for 植, crop in CROPS.items():
        picked[植] = {}
        for st in STAGES:
            cands = [p for p in paths
                     if p.startswith('Game/art/crops/%s/' % crop)
                     and p.endswith('.glb')
                     and os.path.basename(p).startswith('%s_%s' % (crop, st))]
            if not cands:
                continue
            low = [p for p in cands if '_low' in os.path.basename(p)]
            chosen = sorted(low or cands)[0]
            picked[植][st] = chosen
    return picked


def size_of(path):
    """media 上的真实字节数（HEAD 请求）"""
    req = urllib.request.Request(MEDIA + urllib.parse.quote(path), method='HEAD',
                                 headers={'User-Agent': 'ref-asset-pipeline'})
    with urllib.request.urlopen(req, timeout=90) as r:
        return int(r.headers.get('Content-Length', -1))


def cmd_list(paths):
    picked = pick_models(paths)
    total = 0
    print('=== 可取的作物（每个阶段一个模型） ===')
    for 植, crop in CROPS.items():
        print('%-20s -> %-16s' % (植, crop))
        for st in STAGES:
            p = picked.get(植, {}).get(st)
            if not p:
                print('    %-8s (无)' % st)
                continue
            try:
                n = size_of(p)
            except Exception as e:
                n = -1
            total += max(0, n)
            print('    %-8s %9s 字节  %s' % (st, n, p))
    print('模型合计约 %.1f MB（未含贴图）' % (total / 1048576.0))
    print('=== 田地贴图 ===')
    for s in SOIL:
        try:
            print('    %9s 字节  %s' % (size_of(s), s))
        except Exception as e:
            print('    取不到 %s (%s)' % (s, e))


def cmd_fetch(paths):
    os.makedirs(RAW, exist_ok=True)
    picked = pick_models(paths)
    man = []
    jobs = []
    for 植, per in picked.items():
        for st, p in per.items():
            jobs.append((植, st, p))
    for s in SOIL:
        jobs.append(('_soil', os.path.basename(s)[:-4], s))

    for 植, st, p in jobs:
        # 土质那张保持原文件名，转换阶段按名字找它
        name = st if 植 == '_soil' else '%s_%s' % (植, st)
        dst = os.path.join(RAW, name + os.path.splitext(p)[1])
        try:
            blob = http_get(MEDIA + urllib.parse.quote(p))
        except Exception as e:
            print('  ✗ %-28s %s' % (name, e))
            continue
        with open(dst, 'wb') as f:
            f.write(blob)
        sha = hashlib.sha256(blob).hexdigest()
        rec = {'我们的名字': name, '源路径': p, '源URL': MEDIA + urllib.parse.quote(p),
               '字节': len(blob), 'sha256': sha}
        print('  ✓ %-28s %8d 字节' % (name, len(blob)))

        # .glb 要连着它的贴图一起拿
        if p.endswith('.glb'):
            try:
                info = glb_json(blob)
                for img in info.get('images', []):
                    uri = img.get('uri')
                    if not uri:
                        continue          # 内嵌贴图，转换时会一起处理
                    tex_src = os.path.dirname(p) + '/' + uri
                    tex_dst = os.path.join(RAW, name + os.path.splitext(uri)[1])
                    tb = http_get(MEDIA + urllib.parse.quote(tex_src))
                    with open(tex_dst, 'wb') as f:
                        f.write(tb)
                    rec['贴图'] = {'源路径': tex_src, 'sha256': hashlib.sha256(tb).hexdigest(),
                                   '字节': len(tb)}
                    print('    ✓ 贴图 %-24s %7d 字节' % (uri, len(tb)))
            except Exception as e:
                print('    ✗ 贴图失败 %s' % e)
        man.append(rec)

    old = {}
    if os.path.exists(MANIFEST):
        try:
            old = {r['我们的名字']: r for r in json.load(open(MANIFEST, encoding='utf-8'))}
        except Exception:
            old = {}
    for r in man:
        old[r['我们的名字']] = r
    with open(MANIFEST, 'w', encoding='utf-8') as f:
        json.dump(sorted(old.values(), key=lambda r: r['我们的名字']), f,
                  ensure_ascii=False, indent=2)
    print('来源登记写入 %s（共 %d 条）' % (MANIFEST, len(old)))


# ---------------------------------------------------------------- GLB → OBJ

def glb_json(blob):
    magic, ver, length = struct.unpack_from('<4sII', blob, 0)
    if magic != b'glTF':
        raise ValueError('不是 glb：%r' % magic)
    off, js = 12, None
    while off < length:
        clen, ctype = struct.unpack_from('<I4s', blob, off)
        body = blob[off + 8:off + 8 + clen]
        if ctype.rstrip(b'\x00') == b'JSON':
            js = json.loads(body.decode('utf-8'))
        off += 8 + clen
    return js


def glb_bin(blob):
    magic, ver, length = struct.unpack_from('<4sII', blob, 0)
    off = 12
    while off < length:
        clen, ctype = struct.unpack_from('<I4s', blob, off)
        body = blob[off + 8:off + 8 + clen]
        if ctype.rstrip(b'\x00').startswith(b'BIN'):
            return body
        off += 8 + clen
    return b''


# ── 减面：把 Tripo 出的高模压到「一株田里的小草」该有的面数 ──────────────
#
# 【为什么必须减】原始模型一个阶段 1.2~1.8 MB（约 3~6 万三角面）。
#   一块田要摆几十株，直接放会同时压垮渲染和仓库体积。
#   目标 1000 面左右：第三人称看一眼足够，且在田里密植也不心疼。
#
# 算法：**顶点聚类**（grid clustering）—— 把包围盒切成 N×N×N 的格子，
# 落进同一格的顶点求平均合成一个代表点，面跟着重映射，退化面丢掉。
# 用 numpy 向量化，几十万顶点也是瞬间的事。UV/法线一并取平均。

TARGET_TRIS = 1000


def decimate(verts, uvs, norms, faces, target=TARGET_TRIS):
    import numpy as np

    V = np.asarray(verts, dtype=np.float64)
    U = np.asarray(uvs, dtype=np.float64)
    N = np.asarray(norms, dtype=np.float64)
    F = np.asarray(faces, dtype=np.int64)
    if len(F) <= target:
        return verts, uvs, norms, faces

    lo, hi = V.min(axis=0), V.max(axis=0)
    size = np.maximum(hi - lo, 1e-6)

    best = None
    for grid in range(6, 257):
        cell = size / grid
        key = np.floor((V - lo) / cell).astype(np.int64)
        # 把三维格子坐标压成一个整数键
        k = (key[:, 0] * (grid + 2) + key[:, 1]) * (grid + 2) + key[:, 2]
        uniq, inv = np.unique(k, return_inverse=True)
        nv = len(uniq)

        # 代表点 = 格子内顶点的平均
        cnt = np.bincount(inv, minlength=nv).astype(np.float64)
        newV = np.stack([np.bincount(inv, weights=V[:, i], minlength=nv) / cnt
                         for i in range(3)], axis=1)
        newU = np.stack([np.bincount(inv, weights=U[:, i], minlength=nv) / cnt
                         for i in range(2)], axis=1)
        newN = np.stack([np.bincount(inv, weights=N[:, i], minlength=nv) / cnt
                         for i in range(3)], axis=1)

        nf = inv[F]
        # 丢掉退化面（三个点落进同一格 / 重复点）
        ok = (nf[:, 0] != nf[:, 1]) & (nf[:, 1] != nf[:, 2]) & (nf[:, 0] != nf[:, 2])
        nf = nf[ok]
        # 去重（同一组顶点只留一个面）
        _, uidx = np.unique(np.sort(nf, axis=1), axis=0, return_index=True)
        nf = nf[np.sort(uidx)]

        # 【关键】格子越细面越多。要的是「不超预算里最好的那个」——
        # 所以一直记着，直到超了才停；不能一满足就 break，否则留下的是最粗的一档。
        if len(nf) <= target:
            best = (newV, newU, newN, nf)
        else:
            break

    if best is None:            # 连最粗的格子都超预算（不该发生）
        cell = size / 6
        key = np.floor((V - lo) / cell).astype(np.int64)
        k = (key[:, 0] * 8 + key[:, 1]) * 8 + key[:, 2]
        uniq, inv = np.unique(k, return_inverse=True)
        cnt = np.bincount(inv, minlength=len(uniq)).astype(np.float64)
        newV = np.stack([np.bincount(inv, weights=V[:, i], minlength=len(uniq)) / cnt
                         for i in range(3)], axis=1)
        newU = np.stack([np.bincount(inv, weights=U[:, i], minlength=len(uniq)) / cnt
                         for i in range(2)], axis=1)
        newN = np.stack([np.bincount(inv, weights=N[:, i], minlength=len(uniq)) / cnt
                         for i in range(3)], axis=1)
        best = (newV, newU, newN, inv[F])

    newV, newU, newN, nf = best
    # 法线重新归一化（取平均后长度会变）
    ln = np.linalg.norm(newN, axis=1, keepdims=True)
    ln[ln < 1e-9] = 1.0
    newN = newN / ln
    return ([tuple(x) for x in newV], [tuple(x) for x in newU],
            [tuple(x) for x in newN], [tuple(int(i) for i in f) for f in nf])


def _落贴图(out_dir, data, ext, base_name):
    """把贴图写进 out_dir；内容相同的复用已有文件（同作物的几个阶段常共用一张图）"""
    sha = hashlib.sha256(data).hexdigest()
    for fn in os.listdir(out_dir):
        if not fn.lower().endswith(('.jpg', '.jpeg', '.png')):
            continue
        p = os.path.join(out_dir, fn)
        if os.path.getsize(p) == len(data):
            if hashlib.sha256(open(p, 'rb').read()).hexdigest() == sha:
                return fn                      # 已经有了，直接用
    name = '%s_Color%s' % (base_name, ext)
    with open(os.path.join(out_dir, name), 'wb') as f:
        f.write(data)
    return name


CTYPE = {5120: ('b', 1), 5121: ('B', 1), 5122: ('h', 2),
         5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}
NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def read_accessor(js, bin_, idx):
    """把 glTF accessor 读成 list（每项是元组）"""
    acc = js['accessors'][idx]
    n = NCOMP[acc['type']]
    fmt, sz = CTYPE[acc['componentType']]
    bv = js['bufferViews'][acc['bufferView']]
    base = bv.get('byteOffset', 0) + acc.get('byteOffset', 0)
    stride = bv.get('byteStride') or (n * sz)
    out = []
    for i in range(acc['count']):
        o = base + i * stride
        out.append(struct.unpack_from('<' + fmt * n, bin_, o))
    return out, acc['componentType']


def convert_one(glb_path, out_dir, base_name, 外部贴图=None):
    blob = open(glb_path, 'rb').read()
    js = glb_json(blob)
    bin_ = glb_bin(blob)

    # ── 贴图 ──────────────────────────────────────────────────────────
    # Tripo 出的这批模型两种都有：greens 是**外部** .jpg（fetch 阶段下好了），
    # 其余是**内嵌**在 bufferView 里的 jpeg。统一落到 out_dir，
    # 并按内容 sha 去重 —— 同一作物的 sprout/young/mature 常常共用同一张贴图，
    # 不去重的话仓库里会白躺三份一样的图。
    tex_name = None
    if 外部贴图 and os.path.exists(外部贴图):
        data = open(外部贴图, 'rb').read()
        tex_name = _落贴图(out_dir, data, os.path.splitext(外部贴图)[1], base_name)
    elif js.get('images'):
        img = js['images'][0]
        uri = img.get('uri')
        if uri:
            cand = os.path.join(os.path.dirname(外部贴图 or glb_path), uri)
            if os.path.exists(cand):
                data = open(cand, 'rb').read()
                tex_name = _落贴图(out_dir, data, os.path.splitext(uri)[1], base_name)
        elif 'bufferView' in img:
            bv = js['bufferViews'][img['bufferView']]
            off = bv.get('byteOffset', 0)
            data = bin_[off:off + bv['byteLength']]
            ext = '.png' if (img.get('mimeType') or '').endswith('png') else '.jpg'
            tex_name = _落贴图(out_dir, data, ext, base_name)

    # 节点变换：这批资产（Tripo 出的）都是单个节点、无 TRS，但稳妥起见还是应用一次
    nodes = js.get('nodes', [])
    t = (0.0, 0.0, 0.0)
    r = (0.0, 0.0, 0.0, 1.0)
    s = (1.0, 1.0, 1.0)
    if nodes:
        t = tuple(nodes[0].get('translation', t))
        r = tuple(nodes[0].get('rotation', r))
        s = tuple(nodes[0].get('scale', s))
    if nodes and (t != (0, 0, 0) or r != (0, 0, 0, 1) or s != (1, 1, 1)):
        print('    注意：节点带 TRS %s %s %s（已按四元数+缩放应用）' % (t, r, s))

    def xf(p):
        x, y, z = p[0] * s[0], p[1] * s[1], p[2] * s[2]
        qx, qy, qz, qw = r
        # 四元数旋转
        ix = qw * x + qy * z - qz * y
        iy = qw * y + qz * x - qx * z
        iz = qw * z + qx * y - qy * x
        iw = -qx * x - qy * y - qz * z
        x = ix * qw + iw * -qx + iy * -qz - iz * -qy
        y = iy * qw + iw * -qy + iz * -qx - ix * -qz
        z = iz * qw + iw * -qz + ix * -qy - iy * -qx
        return (x + t[0], y + t[1], z + t[2])

    verts, uvs, norms, faces = [], [], [], []
    for mesh in js.get('meshes', []):
        for prim in mesh.get('primitives', []):
            if prim.get('mode', 4) != 4:
                print('    ⚠ 跳过非三角图元 mode=%s' % prim.get('mode'))
                continue
            attrs = prim['attributes']
            pos, _ = read_accessor(js, bin_, attrs['POSITION'])
            nrm = None
            if 'NORMAL' in attrs:
                nrm, _ = read_accessor(js, bin_, attrs['NORMAL'])
            uv = None
            if 'TEXCOORD_0' in attrs:
                uv, _ = read_accessor(js, bin_, attrs['TEXCOORD_0'])
            idx = None
            if 'indices' in prim:
                idx, ct = read_accessor(js, bin_, prim['indices'])
                idx = [i[0] for i in idx]
            else:
                idx = list(range(len(pos)))

            base = len(verts)
            for i, p in enumerate(pos):
                verts.append(xf(p))
                uvs.append(uv[i] if uv else (0.0, 0.0))
                norms.append(nrm[i] if nrm else (0.0, 1.0, 0.0))
            for k in range(0, len(idx), 3):
                a, b, c = idx[k] + base, idx[k + 1] + base, idx[k + 2] + base
                faces.append((a, b, c))

    os.makedirs(out_dir, exist_ok=True)

    # 减面（原始是 3~6 万面的高模，田里密植放不下）
    原三角 = len(faces)
    verts, uvs, norms, faces = decimate(verts, uvs, norms, faces)
    if 原三角 > len(faces):
        print('      减面 %d -> %d 三角' % (原三角, len(faces)))

    obj = os.path.join(out_dir, base_name + '.obj')
    mtl = base_name + '.mtl'
    with open(obj, 'w', encoding='utf-8') as f:
        f.write('# 由 henjicc/yipiantian 的 %s 转换而来（见 manifest.json）\n' % base_name)
        f.write('mtllib %s\n' % mtl)
        f.write('o %s\n' % base_name)
        # [Unity 的 OBJ 导入按右手系→左手系自动处理，这里保持 glTF 原始 Y-up]
        for v in verts:
            f.write('v %.5f %.5f %.5f\n' % v)
        for u in uvs:
            # OBJ 的 V 轴朝上，glTF 的 V 轴朝下 —— 翻过来，否则贴图是倒的
            f.write('vt %.5f %.5f\n' % (u[0], 1.0 - u[1]))
        for n in norms:
            f.write('vn %.5f %.5f %.5f\n' % n)
        f.write('usemtl %s_mat\n' % base_name)
        f.write('s off\n')
        for (a, b, c) in faces:
            f.write('f %d/%d/%d %d/%d/%d %d/%d/%d\n'
                    % (a + 1, a + 1, a + 1, b + 1, b + 1, b + 1, c + 1, c + 1, c + 1))

    texs = [tex_name] if tex_name else []
    with open(os.path.join(out_dir, mtl), 'w', encoding='utf-8') as f:
        f.write('newmtl %s_mat\nKa 1 1 1\nKd 1 1 1\nKs 0 0 0\nNs 0\nd 1\nillum 2\n' % base_name)
        if texs:
            f.write('map_Kd %s\n' % texs[0])

    return len(verts), len(faces), tex_name


def cmd_convert():
    if not os.path.isdir(RAW):
        print('先跑 fetch')
        return
    import shutil
    total_v = total_f = 0
    for fn in sorted(os.listdir(RAW)):
        if not fn.endswith('.glb'):
            continue
        base = fn[:-4]
        植 = next((k for k in CROPS if base.startswith(k)), None)
        if 植 is None:
            print('  ? 认不出属于哪个灵植：%s（跳过）' % base)
            continue
        out_dir = os.path.join(ASSETS, 植)
        os.makedirs(out_dir, exist_ok=True)

        # 外部贴图（fetch 阶段下到 RAW 的那种）在这里传进去
        外部 = None
        for ext in ('.jpg', '.jpeg', '.png'):
            src = os.path.join(RAW, base + ext)
            if os.path.exists(src):
                外部 = src
                break

        v, f, tex = convert_one(os.path.join(RAW, fn), out_dir, base, 外部)
        total_v += v
        total_f += f
        print('  ✓ %-32s 顶点 %5d 三角 %5d 贴图=%s' % (base, v, f, tex or '(无)'))

    soil_dir = os.path.join(ASSETS, '_土质')
    os.makedirs(soil_dir, exist_ok=True)
    for s in SOIL:
        src = os.path.join(RAW, os.path.basename(s))
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(soil_dir, os.path.basename(s)))
            print('  ✓ 土质贴图 %s' % os.path.basename(s))
    print('合计顶点 %d、三角 %d' % (total_v, total_f))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('cmd', choices=['list', 'fetch', 'convert', 'all'])
    a = ap.parse_args()
    if a.cmd in ('list', 'fetch', 'all'):
        paths = tree_paths()
        print('仓库文件数：%d' % len(paths))
    if a.cmd == 'list':
        cmd_list(paths)
    elif a.cmd == 'fetch':
        cmd_fetch(paths)
    elif a.cmd == 'convert':
        cmd_convert()
    else:
        cmd_fetch(paths)
        cmd_convert()


if __name__ == '__main__':
    import urllib.parse
    main()
