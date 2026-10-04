# UI 水墨换皮（InkUI）

> **管什么**：外部交付的 UI 素材（`ui-rework-2026-10-03/`）**怎么进工程、怎么设导入参数、怎么在运行时接到已有的 uGUI 上**；素材命名与九宫格 border 的对应关系；换皮的开关与回退。
> **不管什么**：每个面板现在长什么样、由谁生成、锚点尺寸 —— 看 [UI现状原理图](../architecture/UI现状原理图.md)；素材本身要什么规格看 `ui-rework-2026-10-03/素材规格与提示词.md`；按钮素材的切图细节看 `ui-rework-2026-10-03/BUTTON-KIT.md`。
> **本文件怎么查**：§1 素材放哪、§2 导入规则、§3 运行时怎么接、§4 节点名→素材映射表、§5 现状与没做的。

---

## 0. 2026-10-03 接入更新

当前已接入共享皮肤、HUD、角色八页、修炼三页、丹房，以及登记画布的通用框和按钮。下文早期盘点仍保留；**最终运行时覆盖由本节与代码确定**，不需要重新生成场景。

### 0.1 全工程映射与状态入口

2026-10-04 第三阶段开工：按用户手绘与最新澄清，**父页面没有底板**。`UIInkNavigation` 在运行时隐藏 Window 与 Sidebar 的绘制，保留八个原按钮和数据事件；八颗墨点错落排布，图标中央、名称下方。`UIInkFluid` 以 UIInkDensitySimulation 的二维密度/速度状态及 InkDensity.shader 进行持续平流与扩散，叠加游走和阻尼牵引，划过直接形变墨点、点击沿不规则墨点边缘洇开（圆环仅为较弱附加反馈）；减少动效停止全部常态运动。没有整页纸纹覆盖。14 张新透明素材、双分辨率截图与录像入口见 [墨点父导航施工记录](../../ui-rework-2026-10-03/dynamic-assets-v1/施工记录.md)。**子页内容尚未按新总纲重制，以下卡片/环槽等仍是过渡布局。** 编辑器 `inkqa:dynamicbefore/dynamicafter` 后重进 Play 可对比父导航。

2026-10-04 第一阶段补图：`AbilityArt/` 已接当前真实目录 8 项神通的 8 枚图标与 8 幅详情画。`UIInkAbilityArt` 按 `神通id` 查找，仅在展示层使用，不改 `DisplayIcon` 数据字段、CSV 或 ScriptableObject。资源库/被动行/六槽/HUD/拖影共用图标查询，神通详情用独立不挡射线的 `InkAbilityArtwork`，正文让出图片区。缺图仍走原图标；无详情画保持空白。素材、提示词、alpha 统计与实机截图见 [插画接入验收](../../ui-rework-2026-10-03/ability-art-v1/接入与素材验收.md)。

2026-10-04 动效接入：`UIInkMotion` 共用落笔、晕开、提笔、干笔与 Timing 表，暂停菜单提供“减少动效”。面板绘制用 `UIInkReveal/UIInkClipRect` 展开，文字只淡入；关闭快照不复制业务脚本、不占射线，业务立即关闭。`UIInkNumber/UIInkFill` 只插值显示，冷却与编号排除；列表复用刷新不重启整库入场。文件桥 `inkmotion:record/status` 录制六类动作慢放；运行时墨晕优先读取 `Effects/fx-ink-blot`，缺图使用柔边几何。素底版与飞白尚缺，§10 统一纸纹未接，不能把本轮写成整套动效/材质全部完成。

| 控制器 / 节点 | 素材 / 行为 |
|---|---|
| `UIInkSkin` / `Window` | 1780×970 布局容器，最新父页面不绘制底板；旧幕布仅在 dynamicbefore 对照模式保留 |
| `UIInkNavigation` / `Tab_*` | `Dynamic/nav-ink-blot-1..8` 独立墨点；图标中央、名称下方，形变仅作用于绘制层；原 Button 切页，局部牵引 ≤8px；持续游走 ≤3px |
| `KnownList` / `PassiveList` | 三列 `Parts/ability-library-card` / 独立被动行；均有常显竖向滚动条 |
| `UIActiveSkillBar` / `UIActiveSkillSlot` | active-ring / slot-empty、slot-filled；编号固定1–6，数据共用 |
| `UIEntryList` / `UIEntryRow` | 池化复用；row-normal/hover/selected；逻辑选中与键盘焦点分别处理 |
| 外观列表动态行 | row-normal，装备按钮按真实已装备状态禁用 |
| `UIInkHudSkin` | ink-ring、slot-empty、bar-track、bar-fill、dialog-scroll；保留原数值、冷却与悬停 |
| `CultivationCanvas` 主面板 / 功法信息 / 功法行 | cultivation-frame / panel-inner / gongfa-card；转修滚动列表 |
| `AlchemyCanvas` 内容框 / 成品预览底 | panel-sheet / furnace-stage；成品透明点击区不遮住丹炉 |
| 丹房信息板底、丹方板底、背包板底 | panel-inner；丹方行 recipe-row 三态；材料格 empty/selected/filled/disabled 来自实际摆料与库存 |
| `灵田地块界面` / 面板 | plot-tag，Read/Write + alpha hit；保留种植、收获、挪动、升级业务 |
| `灵田总览` / 面板 | panel-sheet，清单滚动 |
| `功德堂Canvas` / 功德堂 | panel-sheet，兑换列表可滚动；关闭同步隐藏幕布 |
| `对话界面` / 对话框、名字底、回答按钮 | dialog-scroll、dialog-nameplate、dialog-choice 三态；立绘优先原路径，再按同 id 查 `Portraits/portrait-<id>` |
| `TowerCanvas` / 塔层HUD、按钮 | tower-floor-bar、共用按钮；倒计时和转层仍由原控制器负责 |
| `传送面板` / 底板 | teleport-plate 九宫格，沿用按选项数确定高度 |
| `ToastCanvas` / 底 | toast |
| `PauseMenuCanvas`、`DeathScreenCanvas`、`起名界面` / 标题 | title-plaque，文字独立；保留遮罩原透明度与既有保存/复活/输入业务 |
| `MenuCanvas` / SavePanel、Slot* | panel-sheet、row-normal，存档操作按钮用共用皮肤 |
| `QuestGuideCanvas` / 主线追踪 | dialog-scroll；纪年右上行与设施提示底用 toast；原排序不变 |

共有 95 张 PNG，包括 Parts、Skeleton、BigPieces。重跑 `Cultivation/Import InkUI Sprites` 会设置 Sprite/Single/FullRect 和保守 border。原 PNG 画布留白很大，直接铺图会缩小可见装饰；`InkUITheme` 从 `sprite-rects.json` 读取实际 alpha 边界，构造运行时裁边 Sprite，并约束 border。**不是把白底截图当透明件**，也不把标签字和图标烘进底板。

按钮由 `InkUITheme.Button` 设置三材质四态，尊重 `interactable`；透明点击关闭区 `Dim` / 幕布不套按钮图。`Image.Type.Filled` 在换皮时保留，不能被统一改成 Sliced。`InkUIHitShape` 给异形槽与按钮排除透明四角，卡片拖影挂在根 Canvas 独立层、关闭 raycast，不参与列表裁剪。

### 0.2 复现与回退

从仓库根执行 `ui-rework-2026-10-03/capture-ui.ps1 -Group 角色 -Phase 后 -Resolution 1280x720 -Prepare`。分组还有设施（village）、宗门、塔、标题。脚本退出 Play、选择真实 GameView 固定分辨率、重开场景后再进 Play，等待实时帧刷新后使用 `ScreenCapture`；不使用图片缩放来模拟低分辨率。`inkqa:seed` 仅为截图临时授予目录中真实技能、功法、丹方，**不保存验收状态**，不用于正常游玩。

编辑器回退：`inkqa:before` 设置编辑器验收开关，**重新进入 Play** 才得到生成器基础布局；`inkqa:after` 后重新进 Play 恢复新皮肤。已经换过图时禁用组件不能自动还原原 Sprite。构建版本默认启用新皮肤，不读取编辑器验收开关。前图属于“关闭运行时覆盖的基础布局”，不是从历史 commit 启动的旧版本录像。

`inkqa:validate` 验证共享六槽、顺时针编号、占用位覆盖、空槽点选、被动禁拖、列表池化、境界填充、异形命中与关闭后的时间恢复。场景文件差异必须保持 0；禁止运行生成器并保存。

本轮交互检查 26 项通过。根目录 `ui-rework-2026-10-03/runtime-review.html` 可切换 30 个界面或状态及两种分辨率查看前后对照；`verify-captures.ps1` 逐个核对 PNG 头的真实像素尺寸。地块纸签为 640×740，内容另加内边距；缺立绘时透明，镇妖塔暗幕文字保留明亮颜色。宗门场景没有对话来源，已跳过这项。地块三态由真实作物定义构造临时上下文，仅验证 UI，不证明村庄允许摆地。

### 0.3 仍待完成的视觉和业务验证

当前九宫格大底和导航标签与用户意向图存在差距；异形幕布、选中标签外突、内衬山水是下一轮视觉调整。缺失能力插画继续留空，不修改数据补假内容。传送等级置灰仍是既有死路径（未接 `刷新准入`）。种植全周期、移动放置合法性、炼制成败扣料、贡献购买、真实存档/复活和跨场景任务应另做流程回归；只截图不等于这些流程已验证。

## 1. 素材放哪

```
Assets/resources/UI/InkUI/            ← ⚠️ 必须是 `resources`（小写那个）才能 Resources.Load
├─ Buttons/           按钮 3 材质 × 4 态（520×157，1× 交付，border 120/28/120/28）
├─ SkillsPage/        神通页：common / navigation / skills / status
├─ CultivationAlchemy/ 丹房 + 修炼页
├─ CommonPanels/      灵田纸签 / 对话 / Toast / 塔层条 / 传送板 / 题匾
├─ Icons/             8 个菜单图标（256×256）
└─ Portraits/         8 张对话立绘（1144×1614）
```
95 张 PNG（包括 Parts / Skeleton / BigPieces），实际总量以目录为准。**大图在导入时压 `CompressedHQ`（BC7）**，小件（图标/按钮/槽位/徽章/滚动条）留不压缩。

## 2. 导入规则（**别手填**）

菜单 **`修仙 / UI / 导入 InkUI 素材（Sprite + 九宫格）`**（`Assets/Editor/Tools/InkUI素材导入.cs`）：

| 项 | 值 | 为什么 |
|---|---|---|
| Texture Type | `Sprite (2D and UI)` + `Single` | uGUI 用 |
| **Mesh Type** | **`FullRect`** | 九宫格 + 异形命中都要它（`Tight` 会裁掉透明边） |
| Pixels Per Unit | 100 | 与全工程一致 |
| **Pivot** | **Center (0.5,0.5)** | 面板/槽位都按中心摆 |
| Alpha Is Transparency | 开 | 异形边缘不发黑 |
| Mip Maps / Wrap | **关 / Clamp** | UI 不做缩小采样、不重复贴图 |
| Compression | 大图 `CompressedHQ`、小件 `Uncompressed` | 见 §1 |
| **Sprite Border** | 按各包 `manifest.json` 的 `nineSlice` **× 2** | 素材是 **2× 交付**，manifest 写的是 1× |

工具会**逐张打日志**（尺寸 + border），并且 **border 超过图的一半就自动夹取 + 告警**（填大了九宫格会直接坏）。
> `bar-fill` / `icon-*` / `portrait-*` / `active-ring` / `slot-*` / `ink-ring` / `furnace-stage` **不加 border**（它们不是九宫格）。

⚠️ **2× 素材在 Image 上要配倍率**：`Image.type = Sliced` 时 `pixelsPerUnitMultiplier = 2`（本工程 `UIInkSkin.九宫倍率`），
按钮包是 1× 交付、建议显示高 48–64 ⇒ 用 `2.6`（`UIInkSkin.按钮倍率`）。

## 3. 运行时怎么接（**不改场景、不改生成器**）

角色面板是 `CharacterPanelBuilder` **在编辑器里生成、然后序列化进 5 个游玩场景**的；
重新生成 + 保存场景会把运行时 UI 列表一起重排（**踩坑 A8：写脏过 +19787 行**）。所以这一版走：

```
场景自举.补HUD层()  →  给 CharacterUI 补一个 UIInkSkin（和 功德堂兑换 同一条路子）
UIInkSkin.Start()   →  按**节点名**把 Image.sprite / Button 四态换掉
```

`Assets/Scripts/UI/UIInkSkin.cs`，只做三件事：**换图**（含 `type` 与倍率）、**换按钮**（`SpriteSwap` + 三档材质）、
**通过 `UI神通页重排` 调整运行时布局，保留原数据关系**。分组开关：`换面板底 / 换页签 / 换列表行 / 换按钮 / 换进度条 / 换滚动条 / 换字色`，总开关 `启用`。回退方式以 §0.2 为准。

> **字色**：纸底上原来的金/白字看不清 ⇒ 把"偏亮的字"统一改成墨色 `#303D37` 并关掉黑 `Outline`（只改亮字，深色字不动）。

## 4. 节点名 → 素材映射（第一版覆盖到的）

| 节点名（生成器里的中文串） | 素材 | 类型 |
|---|---|---|
| `Window` | `SkillsPage/common/panel-sheet` | Sliced ×2 |
| `Sidebar` | `SkillsPage/navigation/nav-rail` | Sliced ×2 |
| `Tab_背包` … `Tab_外观` | `SkillsPage/navigation/tab-normal` | Sliced ×2 |
| `ActiveSkillBar` | `SkillsPage/skills/active-ring` | Simple（**整图、中央透明**） |
| `Slot_0`…`Slot_5` | `SkillsPage/skills/slot-empty` | Simple（异形）；Icon 只取数据图标 |
| `Row` / `行_*` / `丹方*` / `背包行*` | `SkillsPage/skills/row-normal` | Sliced ×2 |
| `BagGrid`/`Info`/`ItemDesc`/`AttrList`/`GongFaShow`/`RealmShow`/`PassiveList`/`KnownList`/`MountShow`/`MountList`/`AppearanceShow`/`FormationGrid`/`SpiritList`/`OwnedList`/`ArrayList`/`GridArea` | `SkillsPage/common/panel-inner` | Sliced ×2 |
| `Info`（**在 `Page_神通` 下**时） | `SkillsPage/skills/panel-detail` | Sliced ×2 |
| `Track` / `进度底` / `*条底` | `SkillsPage/status/bar-track` | Sliced ×2 |
| `Fill` / `*进度填充` | `SkillsPage/status/bar-fill` | **Filled**（保持原 type） |
| `Scrollbar` 及其 `Handle` | `SkillsPage/status/scroll-track` / `scroll-thumb` | Sliced ×2 |
| 任意 `Button`（按名字判三档） | `Buttons/{ivory,jade,cinnabar}-{normal,hover,pressed,disabled}` | Sliced，`SpriteSwap` |

**三档判定**（`UIInkSkin.套按钮`）：名字含 `破境/转修/重生/开炉/开炼/死亡/卸下` → **朱砂**；
含 `装备/修炼/闭关/突破/启用/确认/兑换/使用/收获/播种/一键` → **玉绿**；其余 → **素纸**。

> ⚠️ **两个坑**：
> ① 生成器给底板/页签设了**深色 `color`**（如 `Window` 0.13 灰）—— 换 sprite 时必须把 `color` **重置成白色**，
>    否则纸白 × 0.13 = 还是黑的（第一版就是这样，看着"没换"）；
> ② 换 sprite **不能碰 `Image.type`**：`Fill` 那条是 `Filled`，改成 Simple 就没有填充了。

## 5. 早期盘点与当前限制

**已生效（2026-10-03 实测，Sect）**：面板底 22 / 页签 8 / 列表行 45 / 按钮 69 / 进度条 2 / 滚动条 3 / 改字色 18；
截图 `screenshots/换皮_神通页4.png`。

上面的数量属于第一轮截图，不能作为最终验收数字。六槽编号、通高详情、资源库卡片、逻辑选中态、HUD 与其他登记画布的接入现已补充，详见 §0。

当前限制仍为 §0.3 所列视觉差距、缺失能力插画、传送等级旧路径和未完成的完整业务回归。截图中的临时目录与灵田三状态只用于验收，退出 Play 后不保存；QA 会停止被动攻击组件和主线演出，避免截图时攻击 NPC 或触发剧情。
