# 参考素材管线

> **它是什么**：一套**开发期**脚本，从 GitHub 项目 `henjicc/yipiantian` 抓作物/土质素材，
> 转成 Unity 能原生导入的格式，放进 `cultivation/Assets/resources/灵田/`。
> **它不是运行时依赖** —— 跑完产出的是普通资产，游戏本身不碰 Python。

---

## 1. 为什么需要它（三条本机限制）

用户 2026-10-01 指定要用那个项目的素材（[来源与授权登记](../../docs/reference/外部素材来源.md)）。
但直接搬有三个坎：

| 坎 | 实际情况 | 这里的做法 |
|---|---|---|
| **本机 shell 的 TLS 是坏的** | PowerShell / curl 走系统 schannel，报 `SEC_E_NO_CREDENTIALS`，**所有 HTTPS 都失败**（不是 GitHub 被封） | 下载交给 **Python**（它用 OpenSSL，HTTPS 正常） |
| **素材是 Godot 工程的 `.glb`，Unity 不能原生导入** | Unity 只认 `.fbx/.obj/.dae…`；**本机也没装 Blender** | 自己解析 `.glb` 的 JSON+BIN 两个 chunk，写成 **`.obj` + `.mtl` + 贴图** |
| **原素材太重** | 单个阶段 1.2~1.8 MB（3~6 万三角面）；贴图 **4096×4096** | **顶点聚类减面**到约 1000 面；贴图压到 256² / 512² |

还有一个坑：**那个仓库的美术全在 Git LFS 里**。
`raw.githubusercontent.com` 只会给你一个 **131 字节的指针文件**，
真文件必须走 `media.githubusercontent.com/media/<owner>/<repo>/<branch>/<path>`（脚本里就是这么做）。

---

## 2. 怎么跑

```bash
python tools/参考素材/fetch_and_convert.py list      # 先看有什么、多大（不下载）
python tools/参考素材/fetch_and_convert.py all       # 下载 + 转换 + 压贴图（最常用）
python tools/参考素材/fetch_and_convert.py fetch     # 只下载到 _raw/
python tools/参考素材/fetch_and_convert.py convert   # 只把 _raw/*.glb 转成 .obj
```

> ⚠️ **顺序有讲究**：`convert` 会把**原始大贴图**重新写进 Assets，
> 所以每次 `convert` 之后**必须再跑一次压贴图**。`all` 已经把这三步串好了 —— 平时就用 `all`。
> 单独跑 `convert` 的话，补一条：
> `powershell -NoProfile -ExecutionPolicy Bypass -File tools\参考素材\shrink_textures.ps1`

### 换素材 / 换作物

`fetch_and_convert.py` 顶部有一张映射表，**改它就等于换素材**：

```python
CROPS = {
    'lingcao_shengling': 'greens',          # 我们的灵植 id  ->  它那边的作物名
    'lingcao_ninglu':    'chrysanthemum',
    ...
}
TARGET_TRIS = 1000                          # 减面目标（游戏里一株草该有多少面）
```

改完重跑 `all` 即可。**游戏代码一行都不用改** ——
它只认 `Assets/resources/灵田/<灵植id>/<灵植id>_<阶段>` 这个路径约定
（见 [guides/灵田 §9](../../docs/guides/灵田.md)）。

---

## 3. 里面有什么

| 文件 | 作用 |
|---|---|
| `fetch_and_convert.py` | 主脚本：列清单 / 下载 / GLB→OBJ / 减面 / 内嵌贴图抽取 / 来源登记 |
| `shrink_textures.ps1` | 用 Windows 自带的 GDI+（System.Drawing）把贴图压到 256² / 512² |
| `manifest.json` | **来源登记**：每个文件的源路径、URL、字节数、sha256、贴图哈希 |
| `_raw/` | 原始 `.glb` 下载（约 16 MB，**已 gitignore**，转换完就没用） |

### ⚠️ 两个脚本都必须是 **UTF-8 with BOM**

`shrink_textures.ps1` 里写了中文路径（`Assets\Art\灵田`）。
**PowerShell 5.1 读无 BOM 的脚本会按 ANSI(GBK) 解码**，`灵田` 直接变成 `鐏电敯`，
报 `Cannot find path`。这不是玄学，是项目里已经踩过好几次的同一类坑
（`docs/ai/踩坑总库.md` 里 `.cs`/`.csv` 的规矩现在要加上 `.ps1`）。

### 减面是怎么做的

`decimate()` 用 **numpy 顶点聚类**：把包围盒切成 N×N×N 的格子，
落进同一格的顶点求平均合成一个代表点，面跟着重映射、退化面丢掉，UV/法线也取平均。

> **要的是「不超预算里最好的那一档」**：格子越细面越多，所以循环**一直记着最后一次没超预算的结果**，
> 直到超了才停。写成「一满足就 break」会留下**最粗**的那一档 ——
> 实测第一批模型被减到只剩 45~240 面（形状都糊了），就是踩了这个。
