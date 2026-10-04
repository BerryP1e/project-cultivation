# 宣纸墨卷 × 云纹鎏边 · 按钮素材 v1
> **管什么**：只读保存的 UI 制作原始记录；不代表当前实现。
> **不管什么**：当前接入状态、资产位置和操作步骤见 [UI换皮](../../../guides/UI换皮.md)。
> **本文件怎么查**：使用下方原有主题标题；先查当前指南再读本文件。
> **来源**：用户提供或制作过程中形成的 ui-rework-2026-10-03/BUTTON-KIT.md，归档时保留正文。


根据 2026-10-03 的新参考：整体继续采用风格 1 的宣纸水墨，吸收尖角按钮、双层细边和繁复端部装饰。将西式／星空感的装饰改写为中国云纹、如意纹和编结纹。素纸用于次级动作，青玉用于主要动作，朱砂用于破境、转修等仪式动作。繁纹集中在两端，按钮中央给文字留空间。

## 已交付

- [按钮展示与状态交互](../../../../cultivation/Assets/Editor/Tools/InkUIAuthoring/button-lab.html)：三个材质 × 四个状态、长短按钮、宣纸面板装配和透明棋盘检查。
- [完整交互原型](../../../../cultivation/Assets/Editor/Tools/InkUIAuthoring/index.html)：风格 1 的主要按钮、确认框、选中页签已应用新皮肤，面板增加细角饰。
- [Unity 素材包](../../../../cultivation/Assets/UIResources/InkUI/Authoring/InkOrnate-Unity-Sprites.zip)：12 张无字 RGBA PNG 与对应 Sprite `.meta`。包内有 `Assets/UIResources/InkOrnate/Buttons/` 结构，可按原路径合并到工程。
- `assets/buttons/`：原始透明图集、12 张独立按钮、9 张左／中／右分片、1 张独立 SVG 角饰、切片清单 `manifest.json`。

所有独立按钮统一为 **520×157 px**。生成图确有真实 Alpha：外围包含 Alpha=0 像素，按钮内部含接近不透明的像素。图集透明区域的 RGB 仍保留颜色，这是正常 PNG 数据；显示与合成以 Alpha 为准。拆分脚本只裁切、补透明画布并打包，没有用颜色阈值抠图，因此没有把宣纸浅色误抠除。

文字未烘焙在图片中。示例的“返回”“装备神通”“确认破境”都是网页单独渲染的文字。实际 Unity 中用独立 Text／TextMeshPro 子对象，字号、翻译、动态文案与按钮美术分开维护。

## 文件命名与按钮状态

| 前缀 | 用途 |
|---|---|
| `ivory-` | 素纸，返回／取消／次级动作 |
| `jade-` | 青玉，装备／开炉／主要动作 |
| `cinnabar-` | 朱砂，破境／转修／仪式动作 |

| 后缀 | Unity Button 使用位置 |
|---|---|
| `normal.png` | Image.sprite，常态 |
| `hover.png` | SpriteState.highlightedSprite 与 selectedSprite |
| `pressed.png` | SpriteState.pressedSprite |
| `disabled.png` | SpriteState.disabledSprite |

使用 `Button.Transition = SpriteSwap`。键盘／手柄选中态沿用 hover，再加独立焦点轮廓；不能仅凭金边区分鼠标悬停和键盘焦点。若后续要求四态像素级完全锁定纹样，可保留 normal 作为共同底图，把高光、内阴影、禁用遮罩拆成运行时叠层。目前四态是生成的美术版本，已有同画布与居中处理，但精细纹样存在小差异，发布前需在引擎中检查跳变。

## Unity 导入与切片

包内 `.meta` 已写入以下设置；尚未在当前场景中安装或替换旧按钮。

| 设置 | 值 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Mesh Type | Full Rect |
| Pixels Per Unit | 100 |
| Pivot | Center (0.5, 0.5) |
| Alpha Is Transparency | 开启 |
| Mip Maps | 关闭 |
| Wrap Mode | Clamp |
| Filter Mode | Bilinear |
| Compression | None，先保留细纹质量 |
| Sprite Border | Left 120 / Bottom 28 / Right 120 / Top 28 |

Image Type 设为 **Sliced**，开启 Fill Center。按钮建议显示高度 48–64px；`Image.pixelsPerUnitMultiplier` 可从 2.5–3.0 起调，使端部保留约 40–48px。非常短的导航标签用紧凑实例，端部约 14–18px；不要把所有按钮直接拉到相同的长宽比。九宫格保存端部比例，但中央细小纹样随中段伸缩，若扩展到极长横条应把中央纹章进一步分离成独立层。

当前网页使用 `border-image` 对应九宫格思想，边框宽度 12px / 42px；三个示例宽度 150、260、420px。左／中／右分片供不用九宫格时组三段式按钮。Unity 推荐优先使用完整单张 Sprite + Border，避免多张 Image 带来的缝隙和维护成本。

## 异形命中与动效

视觉上的透明角不应响应点击。简单尖角按钮可实现多边形 `ICanvasRaycastFilter`；如果使用 `alphaHitTestMinimumThreshold`，先检查纹理 Read/Write 和图集打包兼容性。当前包默认 Read/Write 关闭，不应直接承诺 Alpha 命中可用；可根据选用方式开启或使用几何判断。网页通过 clip-path 缩小命中轮廓，仅作形状示意。

推荐层级：`ButtonRoot（命中与状态） / Skin（无字底图） / OrnamentHighlight（可选） / Label / FocusRing`。按钮按下位移约 1px，悬停只提高金边亮度，避免整片黄金闪烁。动效使用非缩放时间，跟随角色面板暂停时仍可响应。拖拽玉简仍沿用已有原型的抬升、倾斜与阴影，按钮四态不改变物品拖拽规则。

## 验证

`button-verification.json` 记录七项检查：12 PNG 的 Alpha 与尺寸、图片加载、悬停／按下／禁用状态切换、主按钮点击装备、丹房确认／取消、1280px 横向布局、JavaScript 错误。实际截图为 `assets/button-lab-preview.png`、`assets/ornate-skills-preview.png`、`assets/ornate-alchemy-preview.png`。

当前已经交付真实可拆分素材与网页装配效果。Unity 包仅包含美术与导入参数，尚未完成编辑器内 Sprite 导入和按钮 Prefab 替换验证。
