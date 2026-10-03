# UI现状原理图

> **管什么**：当前工程里**游戏 UI 的真实实现** —— 每个面板谁创建它、运行时 uGUI 层级长什么样、关键数值（锚点 / 尺寸 / 位置 / 字号 / 颜色）、每块图是**纯色 Image** 还是贴图 / 九宫格、是否需要 `fillAmount`、以及开关与层级；末尾给出一份**素材接口清单**，供换皮 / 重构时按位置补 sprite。
> **不管什么**：HUD 的冷却与悬停细节看 [游戏HUD](游戏HUD.md)；任务与对话的数据、剧情、状态机看 [任务系统](../design/任务系统.md) 与 [对话系统](../design/对话系统.md)；存档逻辑看 [存档系统](../design/存档系统.md)；丹方与材料规则看 [炼丹](../guides/炼丹.md)；地块与作物规则看 [灵田](../guides/灵田.md)；宗门贡献来源看 [宗门贡献与兑换](../guides/宗门贡献与兑换.md)；塔的层数与掉落看 [镇妖塔](../guides/镇妖塔.md)；外观解锁看 [外观系统说明](../guides/外观系统说明.md)；交互按键规矩看 [交互系统](../design/交互系统.md)；引擎 / 管线 / 字体等基本盘看 [系统总览](系统总览.md)；外部视觉方案（三套皮肤、按钮素材包）看 `ui-rework-2026-10-03/README.md` 与 `ui-rework-2026-10-03/BUTTON-KIT.md`。
> **本文件怎么查**：§1 全局（三条生产线 / Canvas 规格 / `sortingOrder` 总表 / 字体与色板 / ESC 协调器）｜§2 HUD｜§3 角色面板｜§4 修炼页｜§5 丹房｜§6 灵田｜§7 功德堂｜§8 任务追踪｜§9 对话｜§10 暂停·主菜单·身陨·起名｜§11 黑幕字幕｜§12 Toast｜§13 镇妖塔｜§14 传送｜§15 纪年 HUD｜**§16 素材接口清单**｜§17 换皮工作量排序与未确认项。
> **来源**：2026-10-03 之后对 `cultivation/Assets` 的**只读**盘点（逐文件读 `.cs` + 对 `.scene` / `.prefab` 做 grep）。行号是写入本文件时的代码行号；**本文件不改任何代码、场景、Prefab**。

---

## 0. 2026-10-03 运行时接入后的现状

2026-10-04 第一阶段补图：当前 8 项神通的卡片、被动行、共享六槽、HUD 与拖影共用 `UIInkAbilityArt.Icon`；详情增加独立透明 `InkAbilityArtwork`，对应 8 幅水墨插画，正文下移让出图片区域，按钮入口不变。图标与画按真实神通 id 查找，原表和 `DivineAbilityDefinition.图标` 不修改。换页/清空选择会更新或隐藏旧画，缺图仍保留原有回退。槽名称移到玉符边缘下方，卡片操作区避开底部装饰。见 [素材与接入验收](../../ui-rework-2026-10-03/ability-art-v1/接入与素材验收.md)。

下文 §1–17 保留原生成器与序列化布局的盘点，作为接线和回退依据。**游玩画面会再经过运行时换皮；最终布局以本节为准**，没有重新生成或保存五个游玩场景。

- `场景自举` 给角色页补 `UIInkSkin`，给 HUD 补 `UIInkHudSkin`。`InkUIRuntimeSkin` 在已登记画布上处理运行时新节点；动态列表、丹房材料与按钮的状态由各原控制器刷新。
- 角色父窗口为 **1780×970**，左导航保持八页原顺序。三个页面的主动栏读同一份 `UIPanelData.主动技能`，六槽按索引顺时针 1–6。神通资源库左下三列卡片，生效被动中下独立列表，右侧通高详情；二者都可滚动，被动仍通过原入口启停。
- 修炼三页仍属于 `CultivationCanvas`；转修列表改为滚动内容，不再按固定窗口高度截断功法。丹房独立，仍为一主材四辅材，点击成品区域返回丹方，材料扣除与结算由 `炼丹炉` 负责。
- 所有角色 `ScrollRect` 和登记画布里的竖向列表使用常显滚动条；空列表也保留轨道。战阵使用行对象池。本机目录实际为 **231** 项，施工单的 240 是容量要求，不能为了凑数补假条目。
- 境界 `Fill` 补上 Sprite 并保留 `Filled`；HUD 冷却保留 `Filled/Vertical/Top`。异形槽的几何命中允许中心、排除四角；地块纸签使用实际透明通道命中。
- 外观继续使用真实 `RawImage`/隔离相机预览，补 `UnscaledTime/AlwaysAnimate`；坐骑原来已有此设置，现在两种预览共用碰撞与 NPC 行为组件剥离。没有修改 3D 模型和能力数据表。
- 功德堂改为滚动条目，展示真实价格和兑换后余额。修复其关闭后幕布残留，并在关闭时登记 ESC；业务仍走原子扣贡献入口。
- 原 `sortingOrder` 保留。传送板既有值 **500** 保留；等级置灰仍为无人调用的旧路径，不把它描述成已接通。
- 地块纸签运行时为 **640×740**，子内容独立加内边距，保留四种操作；对话按真实 id 查找立绘，缺图时透明，不显示黄色占位。镇妖塔暗幕文字保持原明亮颜色。

素材从 `Assets/resources/UI/InkUI` 读取，95 张 PNG 已导入为 Sprite。`sprite-rects.json` 记录透明留白边界，`InkUITheme.Load` 构造裁去留白的运行时 Sprite，原 PNG 不变。共用底、边框、按钮、文字、图标仍各自独立。

2026-10-04 增加共用 `UIInkMotion`、`UIInkPulse` 及显示层数值/填充/网格裁剪组件：纸面左侧固定展开，子内容一起裁出，文字不缩放；角色及设施的关闭入口仍立即关闭并还原时间，短暂离场 Canvas 只复制可见 Image/Text，无射线。暂停菜单新增“减少动效”偏好开关；动画使用 unscaled 时间，统一六元素预算，隐藏页面会释放墨晕名额。原六槽 `InkUIHoverMotion` 停用，由共用卡片/槽位动效负责。纸纹、飞白与素底素材统一尚未完成。

**视觉未定稿**：目前大幕布仍是九宫格矩形。2026-10-04 导航修正为暗墨立板、透明未选项和独立外突素纸选中牌；菜单图标由 30px 增至 44px，保持原色，浅色字只用于暗底未选项。图标仍带繁复装饰，素材风格统一尚未完成。能力大图和方形插画未交付：不新增占位画，图标继续取真实 `DisplayIcon`，缺失时保留旧品阶色块。

验收入口：`.dsh` 文件桥 `inkqa:<命令>`，截图脚本在项目根 `ui-rework-2026-10-03/capture-ui.ps1`。只在 Play 中构造验收状态，不保存场景或存档；退出 Play 回到磁盘场景。截图位置为 `cultivation/screenshots/UI_<界面>_前/后_<分辨率>.png`，截图与日志不入库。详细映射及复现步骤见 [UI换皮](../guides/UI换皮.md)。

## 1. 全局：UI 是怎么搭出来的

### 1.1 三条生产线

| 线 | 做法 | 落盘形态 | 用它的地方 |
|---|---|---|---|
| **A 编辑器生成器** | 点菜单 → 建 uGUI 节点 → 存场景 | **序列化进 `.scene`** | `CharacterUI`（`Assets/Editor/Builders/CharacterPanelBuilder.cs:21-26`，菜单「修仙/构建角色面板 UI」）、`HudCanvas`（`Assets/Editor/Builders/HudBuilder.cs:60-62`）、`MenuCanvas`（`Assets/Editor/Builders/MainMenuBuilder.cs:27-29`）、塔的界面节点（`Assets/Editor/Builders/TowerBuilder.cs:464`） |
| **B 运行时自建 Canvas** | 组件 `Awake()` 里 `new GameObject(..., typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))` | **不落场景**，关掉就没了 | 暂停、身陨、Toast、纪年、任务追踪、对话、黑幕字幕、丹房、灵田（地块界面 / 总览）、功德堂、传送面板；修炼页的 Canvas 也是 `Awake` 自建（预制体只是壳） |
| **C 数据驱动动态行** | 行数取决于数据，运行时 `new GameObject` / `Destroy` | 不落场景（`UIEntryList` 还显式打 `HideFlags.DontSave`） | `UIEntryList.AddRow`（`Assets/Scripts/UI/UIEntryList.cs:413-479`）、`功德堂兑换.建行`（`功德堂兑换.cs:196-226`）、`炼丹界面.重建背包`、`DialogueUI` 的回答按钮（`DialogueUI.cs:425`） |

- 场景侧清点：`3C_Testbed` / `Sect` / `Sect_Wilderness` / `village` / `Demon-Suppressing Tower` **各有一份** `CharacterUI` + `HudCanvas`；`StartScene` 有 `MenuCanvas`；`Transition subtitles` 场景里**没有任何 UI 对象**（黑幕字幕是运行时建的）。
- 预制体只有两个 UI 壳：`Assets/Prefabs/CultivationUI.prefab`（2.3 KB）与 `Assets/Prefabs/炼丹界面.prefab`（2.0 KB）—— 里面**只有根 + 组件 + 字体**，界面全在 `Awake` 里搭。
- **UI 一律 uGUI 传统 `Text` + `Assets/Fonts/SimHei.ttf`，工程没导 TMP**（`UIBuildUtils.cs:6`）。

### 1.2 Canvas 规格

| 项 | 值 | 例外 |
|---|---|---|
| renderMode | 全部 `ScreenSpaceOverlay` | 设施名 / F 提示 / 灵田地块牌 / 血条 / 飘字是世界空间 Canvas |
| CanvasScaler | `ScaleWithScreenSize`，参考分辨率 **1920×1080**，`matchWidthOrHeight = 0.5f` | `传送面板`（`Assets/Scripts/Core/Teleporter.cs:274-276`）与 `功德堂兑换`（`:139-140`）**没设 `matchWidthOrHeight`** |
| GraphicRaycaster | 需要点击的都挂 | `纪年HUD`（`纪年HUD.cs:164`）与 `任务引导`（`任务引导.cs:526`）挂了但 **`enabled = false`** |
| EventSystem | 场上没有时由生成器 / 面板自建 | `CharacterPanelBuilder.cs:222-226`、`PauseMenuUI.cs:184-188`、`DialogueUI.cs:658-662` |
| 编辑器可见性 | 场景里的 `Canvas` **组件 `enabled` 默认关**，运行时由 `UICanvasBoot.Awake` 打开 | `Assets/Scripts/UI/UICanvasBoot.cs:34-46` |

### 1.3 `sortingOrder` 总表（换皮时最不能动的一张表）

| sortingOrder | 界面 | 依据 |
|---|---|---|
| **-1** | `HudCanvas`（常驻 HUD） | `HudBuilder.cs:80` |
| 0 → **2450**（运行时抬） | 角色面板 `CharacterUI`：场景里是 `0`，`场景自举` 在运行时抬到 2450 | `Assets/Scripts/Core/场景自举.cs:269-291`（常量 `角色面板层级 = 2450` 在 `:291`） |
| **500** | 传送面板 | `Teleporter.cs:273` |
| **1500** | 任务追踪（主线追踪牌） | `任务引导.cs:524` |
| **1520** | 纪年 HUD | `纪年HUD.cs:162` |
| **1800** | 灵田地块界面（F） | `灵田地块界面.cs:76` |
| **1900** | 灵田摆放器（世界预览提示） | `Assets/Scripts/SpiritField/灵田摆放器.cs:462` |
| **2500** | 镇妖塔 `TowerUI` / 修炼页 `CultivationUI` / 设施占位幕布 | `TowerUI.cs:191`、`CultivationUI.cs:409`、`StationInteractor.cs:691` |
| **2600** | 对话 `DialogueUI` / 灵田总览（F4）/ 功德堂兑换 | `DialogueUI.cs:649`、`灵田界面.cs:153`、`功德堂兑换.cs:137` |
| **2650** | 丹房 `炼丹界面` | `炼丹界面.cs:527` |
| **2900** | 黑幕字幕 | `黑幕字幕.cs:867` |
| **2950** | 起名界面 | `起名界面.cs:327` |
| **3000** | 暂停菜单 | `PauseMenuUI.cs:195` |
| **4000** | 身陨界面 | `DeathScreenUI.cs:84` |
| **5000** | Toast（全场最高） | `ToastUI.cs:72` |
| 世界空间 100 / 150 / 199 / 200 / 210 | 血条 / 伤害飘字 / 设施名 / 设施「F 对话」提示 / 灵田地块牌 | `NpcIndicator.cs:280`、`StationInteractor.cs:398-399`、`:437-438`、`灵田地块牌.cs:44` |

> 规律（代码注释里写死过两次，`任务引导.cs:515-523` 与 `纪年HUD.cs:158-161`）：**面板层 ≥ 1800，常驻 HUD ≤ 1520，世界内提示 ≤ 500**。新增面板时照这条选值，别再靠"比大小"。

### 1.4 字体、描边与公共色板

- 字体：`Assets/Fonts/SimHei.ttf`；找不到时多数面板退回 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`（**DialogueUI 只有这条兜底，没有 SimHei 回退**，`DialogueUI.cs:598-602`）。
- 描边统一 `Outline`，`effectDistance = (1,-1)`（`UIBuildUtils.AddOutline`，`UIBuildUtils.cs:173-179`）；HUD 用 1.2（`HudBuilder.cs:327-332`），任务追踪 / 镇妖塔 / 纪年用 1（`AddOutline`）。

`UIBuildUtils` 色板（`UIBuildUtils.cs:11-20`，换皮的**唯一色源**）：

| 常量 | 原值 | 近似十六进制 | 用在哪 |
|---|---|---|---|
| `ColorDim` | 0,0,0,0.55 | `#000000` α0.55 | 暗底 |
| `ColorSidebar` | 0.82 ×3 | `#D1D1D1` | 角色面板侧栏 |
| `ColorTabNormal` | 0.55 ×3 | `#8C8C8C` | 页签常态、默认按钮底 |
| `ColorTabActive` | 0.34 ×3 | `#575757` | 页签选中 |
| `ColorPanel` | 0.72 ×3 | `#B8B8B8` | 内容面板 |
| `ColorPanelAlt` | 0.58 ×3 | `#949494` | 信息栏 |
| `ColorSlot` | 0.85 ×3 | `#D9D9D9` | 格子底 |
| `ColorText` | 0.12 ×3 | `#1F1F1F` | 正文 |
| `ColorTextOnDark` | 0.92 ×3 | `#EBEBEB` | 暗底正文 |
| `ColorAccent` | 0.78,0.52,0.33 | `#C78554` | 强调（橙） |

按钮默认四态（`UIBuildUtils.cs:61-77`）：normal `#FFFFFF`、highlighted `#E6E6E6`、pressed `#BFBFBF`，字色 `ColorText`，字号默认 22。

### 1.5 两个共用的「没有 sprite 会出事」的坑

1. **`Image` 的 `sprite == null` 时 `fillAmount` 直接被忽略**（`PlayerHud.cs:126-155`、`CultivationUI.cs:89-102`）。所以气血 / 灵力 / 修炼条、冷却遮罩、进度条都**必须**有 sprite —— 现在是运行时 `Sprite.Create` 的 4×4 纯白图。
2. **`UIBuildUtils.CreateImage` 默认 `raycastTarget = false`**（`UIBuildUtils.cs:38`）。所有可点 / 要吃射线的底图都得显式打开，全工程至少 14 处注释记着这条。

### 1.6 ESC 协调器 `UiEscRegistry`

`Assets/Scripts/UI/UiEscRegistry.cs`（全文 65 行）：

| 成员 | 行号 | 语义 |
|---|---|---|
| `LastCloseTime` | `:30` | 最近一次「任何全屏界面」关闭的时刻，初值 `-99f` |
| `Window = 0.15f` | `:33` | 多久之内算「刚刚才关过」 |
| `NotifyClosed()` / `记录关闭()` | `:36` / `:62` | 界面关闭时记一笔（用 `Time.unscaledTime`） |
| `JustClosed(窗口)` / `刚关闭过()` | `:42` / `:63` | 暂停菜单开之前问一句 |
| `AnyOtherUiOpen()` / `有别的界面开着()` | `:48` / `:64` | 只认两个来源：`StationInteractor.有界面打开`（`:51`）与场上所有 `CharacterPanelUI.IsOpen`（`:54-55`） |

- **写入方只有两处**：`CharacterPanelUI.cs:128`（关面板）与 `StationInteractor.cs:631`（关设施界面）。
- **读取方两处**：`PauseMenuUI.Update`（`PauseMenuUI.cs:66` 查「有别的界面开着」、`:72` 查「刚关闭过」）与 `ActiveSkillCaster.cs:172`（界面开着时禁止施放）。
- **所以：丹房 / 灵田 / 功德堂 / 对话 / 修炼页都不注册** —— 它们靠 `StationInteractor` 那个静态标记间接被看见；而 `灵田界面`（F4）**整个不在协调器视野里**，关掉后立刻按 ESC 会同时弹暂停菜单（`灵田界面.cs:106` 只读 F4，不调 `记录关闭`）。
- **已知缺口**：`AnyOtherUiOpen()` **只认** `StationInteractor.有界面打开` 与场上的 `CharacterPanelUI`（`UiEscRegistry.cs:48-58`）。对话（2600）、黑幕字幕（2900）、起名（2950）、镇妖塔（2500）、丹房（2650）**都不在内**；好在它们自己也不读 ESC，实际冲突没暴露。
- **另有一套旧机制**：`StationInteractor.上次关闭界面时间`（`StationInteractor.cs:89` 定义、`:630` 写入）**全工程已无读取方** —— 换皮 / 重构时别把它当活代码。

---

## 2. HUD（`PlayerHud` / `HudHoverTarget` / `HudBuilder`）

| 项 | 值 |
|---|---|
| 生成器 | `Assets/Editor/Builders/HudBuilder.cs:62`，菜单「修仙/生成游戏界面 HUD」；**幂等**：先删旧 `HudCanvas` 再重建（`:71-74`） |
| 运行时刷新 | `Assets/Scripts/UI/PlayerHud.cs`（挂在 `HudCanvas` 上），`Update` 每帧刷四块（`:108-114`） |
| 悬停转发 | `Assets/Scripts/UI/HudHoverTarget.cs`（`:27-35`，槽位 -1 = 功法） |
| Canvas | `HudCanvas`，`ScreenSpaceOverlay`，`sortingOrder = -1`，1920×1080 match 0.5，有 `GraphicRaycaster`（`HudBuilder.cs:77-90`） |
| 贴图 | `Assets/Resources/UI/Hud/hud_square.png`（4×4 纯白，生成器自动产出，`HudBuilder.cs:337-368`）；另有 `hud_circle.png` 未被本线使用 |

### 2.1 层级树（节点名照代码）

```
HudCanvas                      [Canvas sortingOrder=-1]  (HudBuilder.cs:77)
├─ 功法                        anchorMin=anchorMax=(0,0) pivot=(0,0) pos=(20,80) size=68×68   (:101, 锚左下 :278-287)
│  └─ 图标                    铺满 padding=5   (Image, sprite=hud_square)   (:102)
├─ Skill1 … Skill6            锚左下，pos=(98 + i*51, 80+68-46=102)，size=46×46   (:106-113)
│  ├─ 图标                    铺满 padding=4   (:116)
│  ├─ 灰罩                    铺满 padding=0   (:117)
│  ├─ 冷却遮罩                铺满, Filled/Vertical/fillOrigin=Top, fillAmount=1   (:118-123)
│  ├─ 冷却文字                铺满, 20 号, 白 + 描边   (:125-127)
│  ├─ 按键                    13 号 LowerRight, offset(0,1)~(-4,-1) + 描边   (:129-135)
│  └─ 底                      **SetAsFirstSibling**（最底层，唯一吃射线的图）   (:138, 184-186)
├─ 气血                       锚左下 pos=(20,59) size=379×15   (:145, 建条 :194-214)
│  ├─ 底                      铺满, 条底色   (:198)
│  ├─ 填充                    铺满, Filled/Horizontal/Left, fillAmount   (:201-205)
│  └─ 文字                    铺满, 13 号, 白 + 描边   (:207-211)
├─ 灵力                       同上，pos=(20,38)   (:147)
├─ 修炼                       同上，pos=(20,17)，填充色 = 修炼色（金）   (:151)
└─ 信息幕布                   锚左下 pos=(20,160) size=400×215，默认 SetActive(false)   (:228, 226-274)
   ├─ 顶线                    anchor (0,1)-(1,1) pivot(0.5,1) pos=(0,0) size=(0,2)    (:235-240)
   ├─ 名称                    19 号 UpperLeft, pos=(12,-10) size=(-100,26)   (:242-247)
   ├─ 品阶                    14 号 UpperRight, pos=(-12,-13) size=(90,22)   (:249-254)
   ├─ 分隔线                  anchor (0,1)-(1,1) pivot(0.5,1) pos=(0,-40) size=(-24,1)，白 α0.18   (:256-261)
   └─ 正文                    13 号 UpperLeft, offset(12,10)~(-12,-46)，Wrap + Truncate   (:263-270)
```

### 2.2 关键数值

| 部件 | 位置 / 尺寸 | 依据 |
|---|---|---|
| 左内边距 / 底边距 | 20 / 17 | `HudBuilder.cs:36-37` |
| 功法格 | (20, 80) 68×68 | `:43`, `:46` |
| 技能格 6 个 | x = 20+68+10 + i×(46+5) = **98 + i×51**，y = 102，46×46 | `:38-41`, `:106-107` |
| 三条数值条 | x=20，宽 **379**，高 **15**；y = 59 / 38 / 17 | `:42-45`, `:144` |
| 信息幕布 | (20, 160) 400×**215** | `:47-49`, `:228` |
| 字号 | 冷却 20 / 按键 13 / 条文字 13 / 幕布名称 19 / 品阶 14 / 正文 13 | `:125`, `:129`, `:207`, `:242`, `:249`, `:263` |

> 注意：`docs/architecture/游戏HUD.md` 里写的幕布高 190 已过期 —— **代码是 215**（`HudBuilder.cs:49`）。

### 2.3 用了什么图

| 节点 | 创建处 | sprite | Image.Type | fillAmount | raycastTarget |
|---|---|---|---|---|---|
| 功法「图标」 | `HudBuilder.cs:102` | `hud_square`；运行时换成 `功法.DisplayIcon`（`PlayerHud.cs:167-173`） | Simple | 否 | false |
| 技能「图标」 | `HudBuilder.cs:116` | `hud_square` → `entry.DisplayIcon`（`PlayerHud.cs:198-203`） | Simple | 否 | false |
| 技能「灰罩」 | `:117` | `hud_square` | Simple | 否 | false |
| 技能「冷却遮罩」 | `:118-123` | `hud_square` | **Filled / Vertical / fillOrigin Top** | **是** = `施放器.冷却比例(i)`（`PlayerHud.cs:218`） | false |
| 技能「底」 | `:138` | `hud_square` | Simple | 否 | **true（唯一射线体）** |
| 功法格底「底」 | `:103` | `hud_square` | Simple | 否 | **true** |
| 气血/灵力/修炼「填充」 | `:201-205` | `hud_square` | **Filled / Horizontal / Left** | **是**（`PlayerHud.cs:247-248`, `:306`） | false |
| 条「底」/ 顶线 / 分隔线 | `:198`, `:235`, `:256` | `hud_square` | Simple | 否 | false |
| 信息幕布底 | `:230-232` | 无（`AddComponent<Image>` 直接给色） | Simple | 否 | false |

### 2.4 颜色（`HudBuilder.cs:51-58`、`PlayerHud.cs:82-94`）

| 名 | 原值 | 近似十六进制 |
|---|---|---|
| 底盘色 | 0.10,0.10,0.12,0.88 | `#1A1A1F` α0.88 |
| 功法盘色 | 0.14,0.12,0.10,0.90 | `#241F1A` α0.90 |
| 条底色 | 0.08,0.08,0.10,0.90 | `#14141A` α0.90 |
| 灰罩色 | 0,0,0,0.62 | `#000000` α0.62 |
| 冷却色 | 0,0,0,0.66 | `#000000` α0.66 |
| 幕布色 | 0.04,0.05,0.07,0.78 | `#0A0D12` α0.78 |
| 幕布线色（顶线） | 0.78,0.52,0.33,0.85 | `#C78554` α0.85 |
| 修炼色（金） | 0.86,0.72,0.32 | `#DBB852` |
| 气血色 | 0.80,0.20,0.20 | `#CC3333` |
| 灵力色 | 0.26,0.54,0.88 | `#4289E0` |

### 2.5 开关与交互

- 常驻显示，**没有开关**；`Info幕布` 由 `HudHoverTarget` 的 `OnPointerEnter/Exit` 控制（`HudHoverTarget.cs:27-35`）。
- **不注册 `UiEscRegistry`**、**从不写 `Time.timeScale`**。
- 修炼条有**平滑追赶**：`修炼追赶速度 = 1.6`（次/秒）、`修炼满格停顿 = 0.18` 秒（`PlayerHud.cs:76-79`，逻辑 `:279-309`）。

---

## 3. 角色面板（背包 / 境界 / 神通 / 法宝 / 灵阵 / 战阵 / 坐骑 / 外观）

| 项 | 值 |
|---|---|
| 创建者 | `Assets/Editor/Builders/CharacterPanelBuilder.cs:28`（`Build`），菜单「修仙/构建角色面板 UI」（`:21-22`）；**幂等**，先删旧 `CharacterUI`（`:38-43`） |
| 另两条会连带生成 / 覆盖它的路径 | ① `Assets/Editor/Data/DemoDataSetup.cs:29` 调 `CharacterPanelBuilder.Build(true)`（菜单「修仙/一键初始化项目数据」）；② `Assets/Editor/Tools/SceneRigSyncer.cs:47` 把 `CharacterUI` 同步（覆盖）进所有游玩场景（菜单「修仙/同步通用角色与UI到所有游玩场景」，`总是覆盖` 名单在 `:42`） |
| 主控 | `Assets/Scripts/UI/CharacterPanelUI.cs` —— **I 键开 / ESC 关**（`:49`, `:57`, `:96-100`），默认页 = 境界（`:64`） |
| 挂载 | 与 Canvas 同一个 `CharacterUI` 节点；同节点还有 `UIPanelData`（`:57`）、`UIPanelHint`（`:97`）、跨场景数据（`:111`）、`UICanvasBoot`（`:60`） |
| Canvas | `CharacterUI`，Overlay，1920×1080 match 0.5；场景里 `sortingOrder = 0` → 运行时被抬到 **2450**（`场景自举.cs:278`, `:291`） |
| 开关 | `SetOpen(bool)`（`CharacterPanelUI.cs:104`）；开 = `Cursor.visible = true` + `lockState = None`（`:113-117`）+ `Time.timeScale = 0`（`:118-123`）；关 = 还原 timeScale（`:127`）+ `UiEscRegistry.记录关闭()`（`:128`） |
| 页签 | 8 个：背包 / 境界 / 神通 / 法宝 / 灵阵 / 战阵 / 坐骑 / 外观（`CharacterPanelBuilder.cs:123`；枚举 `CharacterPanelUI.cs:10-19` **只有 7 项，外观页靠下标 7 对应**，`:891-893` 有警告） |

### 3.1 层级树

```
CharacterUI                     [Canvas, sortingOrder=0→2450]  (CharacterPanelBuilder.cs:48-60)
├─ PanelRoot                    铺满（随开关显隐）   (:63-65)
│  ├─ Dim                       铺满, 0,0,0,0.62, **raycastTarget=true + Button(Transition.None)**，点空白关面板   (:68-73)
│  └─ Window                    居中 1440×860, 色 0.13,0.13,0.13,0.98, + Outline(0.45×3,1px)   (:76-82)
│     ├─ HintBar                anchor(0,0)-(1,0) pivot(0.5,0) offset(202,18)~(-16,54)，色 0.10,0.10,0.10,0.92   (:86-91)
│     │  └─ Text                18 号 MiddleCenter, 色 0.98,0.92,0.70, Stretch(6)   (:93-95)
│     ├─ Sidebar                anchor(0,0)-(0,1) pivot(0,0.5) offset(16,62)~**(186,-16)** ⇒ 宽 170   (:116-121)
│     │  └─ Tab_背包 / Tab_境界 / Tab_神通 / Tab_法宝 / Tab_灵阵 / Tab_战阵 / Tab_坐骑 / Tab_外观
│     │                         每个 54 高，y = -16 - i×66，左右各缩 14，字号 24   (:126-139)
│     └─ Content                anchor 铺满, offset(202,62)~(-16,-16)，底部给 HintBar 让位   (:142-146)
│        ├─ Page_背包  / Page_境界 / Page_神通 / Page_法宝 / Page_灵阵 / Page_战阵 / Page_坐骑 / Page_外观
│        （占满 Content，`UIBuildUtils.Stretch`，一次只显示一个 —— `CharacterPanelUI.cs:156-165`）
```

**被动技能列表 / 掌握列表 / 背包格子等列表容器**（`CreateScrollList`，`:259-292`）统一是：

```
<面板名>(CreatePanel: 底板 + Title 20号)      ← PlacePanel 归一化锚点 + 6px 内缩   (:231-255)
└─ Viewport                              铺满, offsetMax=(0,-34) 让出标题, + RectMask2D   (:264-267)
   └─ Content                            anchor(0,1)-(1,1) pivot(0.5,1) + VerticalLayoutGroup(spacing=4, padding 6) + ContentSizeFitter(vertical=PreferredSize)   (:269-278)
      └─ Row ×N                          rowHeight 34（UIEntryList.rowHeight，:316）   (UIEntryList.cs:415-479)
```
`ScrollRect`：`horizontal=false, vertical=true, MovementType.Clamped, scrollSensitivity=24`（`:280-286`）。带滚动条的列表再加 `UIBuildUtils.AddVerticalScrollbar`（宽 18 / 顶部留白 34 / 间距 6，`UIBuildUtils.cs:125-170`）。

### 3.2 各页布局（`PlacePanel` 的归一化锚点）

| 页 | 子块（节点名） | anchorMin / anchorMax | 备注 | 依据 |
|---|---|---|---|---|
| 背包 | `BagGrid` | (0,0) / (0.70,1) | 滚动列表「背包格子」 | `:796-797` |
| 背包 | `Info`（选中物品，带 120×120 图标） | (0.72,0.42) / (1,1) | `CreateEntryInfo(withIcon:true)` | `:800-801` |
| 背包 | `ItemDesc`（选中物品介绍） | (0.72,0) / (1,0.40) | 17 号字 | `:804-809` |
| 境界 | `AttrList` | (0,0) / (0.30,1) | 标题「角色属性列举」 | `:476-479` |
| 境界 | `GongFaShow`（功法展示） | (0.32,0.42) / (1,1) | 常驻显示 `data.当前功法`（`显示当前功法=true`，`:515-516`） | `:486-487` |
| 境界 | `RealmShow`（境界展示） | (0.32,0) / (1,0.40) | 见下 | `:518-519` |
| 神通 | `ActiveSkillBar` | (0.20,0.50) / (0.76,1) | 六格 | `:576-577` |
| 神通 | `PassiveList` | (0.78,0.52) / (1,1) | 标题「生效中的被动神通」 | `:580-583` |
| 神通 | `KnownList` | (0,0) / (0.70,0.44) | 标题「掌握神通列表（可拖动到主动技能装备栏）」 | `:594-596` |
| 神通 | `Info` | (0.72,0) / (1,0.44) | 无图标 | `:599-600` |
| 法宝 | `ActiveSkillBar`(0.20,0.50)/(0.76,1) + `Info`(0.78,0.52)/(1,1，**带图标**) + `OwnedList`(0,0)/(1,0.44) | 同左 | `Info` 标题「法宝展示区」；`OwnedList` 标题「拥有的法宝」 | `:617-626` |
| 灵阵 | `ActiveSkillBar`(0.20,0.50)/(0.76,1) + `ArrayList`(0,0)/(0.66,0.44) + `Info`(0.68,0)/(1,0.44) | 同左 | `ArrayList` 标题「掌握的灵阵（拖动到主动技能装备）」；`Info` 标题「选中灵阵的相关信息介绍」（无图标） | `:643-652` |
| 战阵 | `FormationGrid`（标题带 `SpiritFormationLayout.可上阵数`） | (0.20,0.50) / (0.80,1) | 内含 `GridArea`（缩 10，上让 40）+ `SpiritFormationBar` | `:674-685` |
| 战阵 | `Info`（信息简介） | (0,0) / (0.28,0.46) | — | `:688-689` |
| 战阵 | `SpiritList`（带滚动条） | (0.30,0) / (1,0.46) | 240 个真灵 | `:692-696` |
| 坐骑 | `MountShow` | (0,0.50) / (1,1) | 标题「坐骑 idle 动画展示」，内含 `Preview`= `RawImage`（RenderTexture 3D 预览） | `:836-846` |
| 坐骑 | `MountList`（带滚动条） | (0,0) / (0.62,0.48) | — | `:858-861` |
| 坐骑 | `Info` | (0.64,0) / (1,0.48) | 带图标 | `:864-865` |
| 外观 | `AppearanceShow`（标题「模型3D展示」） | (0.52,0) / (1,1) | 内含 `Preview` = `RawImage` + `外观预览` | `:900-910`, `:933-935` |
| 外观 | `AppearanceList`（带滚动条） | (0,0) / (0.5,1) | 行由 `外观页.cs` 运行时生成 | `:921-924` |

### 3.3 复用组件与子页细节

**六格主动技能装备栏**（`CreateActiveSkillBar`，`:295-394`）：

| 节点 | 数值 | 依据 |
|---|---|---|
| `ActiveSkillBar` | 根 | `:297` |
| `Hint` | 18 号 LowerCenter，文字「主动技能装备（神通 / 法宝 / 灵阵 共用）」，高 28 | `:300-306` |
| `Slot_0…5` | 84×84，anchor/pivot 居中，位置 = xs{-60,60,-110,110,-60,60} × ys{88,88,0,0,-88,-88}（**六边形**） | `:308-320` |
| `Slot` 的 `Image` | `ColorSlot`，`Button.targetGraphic` | `:322-325` |
| `Slot/Icon` | 铺满缩 6 ⇒ **72×72**，色 0.9×3 | `:327-328` |
| `Slot/Label` | 15 号 UpperCenter，`sizeDelta=(130,22)`，**运行时被提到 bar 根下**（否则被邻格盖住） | `:331-338`, `:361-375` |
| `Clear`（右上小叉，文字「×」） | 24×24，18 号，**运行时也提到 bar 根下** | `:341-347`, `:378-392` |
| 格子底色三态 | 空+待装备 → `emptyReadyColor`(0.55,0.88,0.55)；否则 `ColorSlot` | `UIActiveSkillSlot.cs:63-66` |

**战阵九宫格**（`CreateSpiritGrid`，`:713-788`）：

| 节点 | 数值 | 依据 |
|---|---|---|
| `SpiritFormationBar` / `Slot_0…8` | 边长 **104**，间距 **14**；位置 = ((列-1)×118, (1-行)×118)（行 0 在最上 = 阵型最前排） | `:718-732` |
| `Slot` 的 `Image` | `ColorSlot`，**显式 `raycastTarget = true`** | `:734-737` |
| `Slot/Swatch`（左侧竖色条，妖魔紫红 / 人类青） | anchor(0,0)-(0,1) pivot(0,0.5) offset(8,10)~**(20,-10)** ⇒ 宽 12 | `:743-749` |
| `Slot/Label` | 16 号 MiddleCenter，offset(24,8)~(-8,-8) | `:752-757` |
| `Slot/Lock`（「已满」） | 15 号，色 0.82×3 | `:760-765` |
| `Clear` | 24×24，右上 | `:768-774` |
| 格子底色三态 | 玩家格 `玩家格色`(0.32,0.38,0.46) / 已满 `已满色`(0.45,0.42,0.42) / 空位待放 (0.55,0.88,0.55) / 常态 `ColorSlot` | `UISpiritSlot.cs:32-38`, `:89-94` |

**境界展示 `RealmShow`**（`:518-564`）：`RealmName` 30 号（anchor y 0.62~1）、`Track` 高 16（anchor y=0.40，offset(20,-8)~(-20,8)，色 0.92×3）、`Fill` = **Filled / Horizontal / fillAmount**（`:562-564`，色 `ColorAccent`）、`Percent` 20 号、`Exp` 18 号（色 0.3×3）。`UIRealmBar` 还会自己再建一个多行信息区（`UIRealmBar.cs:34-35`）。
> ⚠️ **换皮必看**：`Fill` 的 `sprite` 是 **null**（`:535`），而工程别处反复记录「`sprite == null` 时 `fillAmount` 被忽略」（见 §1.5）。也就是说这条境界进度条**大概率不显示填充**（未实机验证）。给它一张 sprite 即可顺手修好 —— 这属于换皮时必须一起处理的一处。

**属性列表 `AttrList`**（`UIAttributeList`）：`rowHeight = 26`、`fontSize = 18`（`UIAttributeList.cs:25`, `:28`），行内 `Name` MiddleLeft / `Value` MiddleRight（`:77`, `:83`）。

**外观页的行**（`外观页.cs`，运行时生成，不在 builder 里）：`行_<id>` = `sizeDelta (0,56)` + `LayoutElement.preferredHeight 56`（`:113-115`）；`行/底` 铺满，色 0.86×3 ≈ `#DBDBDB`（`:120`, 字段 `:38`）；`行/名字` offset(16,0)~(-120,0)，**26 号**（`:127`, `:42`），字色 0.08,0.08,0.10（`:39`）；`行/装备` 按钮 **100×40**，pos(-10,0)，底 0.95,0.78,0.35 ≈ `#F2C759`（未装备）/ 0.55×3（已装备），`interactable = !已装备`（`:130-140`, `:40-41`）。

**两块 3D 预览（换皮时不用给图，但要知道边界）**：

| 项 | 坐骑页 `UIMountPage` | 外观页 `外观预览` |
|---|---|---|
| 场地位置 | y = **−4000**（`UIMountPage.cs:100` 起） | y = **−1000**（`外观预览.cs:107` 起） |
| 相机 | fov **32**、near 0.05、far **60**、depth **−100** | fov **30**、near 0.1、far **12**，localPos (0,1.2,−3.2) |
| RenderTexture | 边长 **512** | **512×512（16 bit）** |
| 相机背景 | 0.15,0.16,0.19 ≈ `#262930` | 0.62×3 ≈ `#9E9E9E` |
| 光照 | 主光/副光/背光三个 Point Light | 主光 1.1 + 补光 0.45（Directional） |
| 旋转 | 偏航 200 / 俯仰 8（clamp ±70），灵敏度 Mouse X×4 / Y×3 | 只取 `delta.x`，灵敏度 **0.4 度/px**，初始偏航 180 |
| 隔离手段 | 禁用所有 Collider、`相机.enabled` 随页开关 | 剥掉 `NpcInstance`/`NpcIndicator`/`NpcDialogue`/`StationInteractable`/`NpcAi*` 与 Collider |

**列表行 `Row`**（`UIEntryList.cs:415-479`）：

| 子节点 | 数值 | 依据 |
|---|---|---|
| `Row` 本体 | `LayoutElement` min/preferredHeight = **34**；`Image` 色 0.80,0.80,0.80,1 + `Button` | `:416-424` |
| `Swatch`（品阶色块） | anchor(0,0.15)-(0,0.85) pivot(0,0.5) offset(4,0)~**(30,0)** ⇒ 宽 26 | `:427-432` |
| `Label` | 18 号 MiddleLeft，offset(38,0)~**(-176,0)** | `:435-440` |
| `Tag`（主动/被动/法宝/…） | 15 号 MiddleRight，offset(-172,0)~(-98,0) | `:443-449` |
| `Action`（启用/停用/上阵/下阵） | **88 × 26**，字号 15，色随状态（启用绿 0.40,0.70,0.45 / 停用暖 0.85,0.45,0.35） | `:452-459`; `:168-188` |
| 行生成 | `HideFlags.DontSave`（不写进场景） | `:476` |

**条目信息栏 `Info`**（`CreateEntryInfo`，`:397-465`）：`Icon` **120×120**（色 0.62×3，无 sprite 时 `UIEntryInfo.cs:131-135` 换成品阶色）、`Name` 22 号、`Tier` 16 号（色 0.35×3）、`Kind` 16 号（色 0.45×3）、`Action` **88×30**（16 号）、`Desc` 17 号。

### 3.4 用了什么图

**角色面板没有一个贴图 sprite —— 全部是纯色 `Image`。** `CharacterPanelBuilder.cs` 里 `sprite` 赋值 **0 次**、`Image.Type` 赋值 **0 次**（唯一的 `Filled` 是 `RealmShow/Fill`，`:562`）。

| 节点 | 创建处 | sprite | Type | fillAmount | raycastTarget |
|---|---|---|---|---|---|
| `Dim` | `CharacterPanelBuilder.cs:68` | null | Simple | 否 | **true**（点空白关面板） |
| `Window` / `Sidebar` / 各 `CreatePanel` 底板 / `HintBar` | `:76`, `:116`, `:233` | null | Simple | 否 | false |
| `Tab_*` | `:128`（`UIBuildUtils.CreateButton`） | null | Simple | 否 | true（Button.targetGraphic） |
| `Slot_*`（六格 / 九宫） | `:322`, `:734` | null | Simple | 否 | 六格 false，九宫 **true** |
| `Icon` / `Swatch` / `Row` 底 / `Action` | `:327`, `:427`, `:420`, `:452` | null | Simple | 否 | Row:true，其余 false |
| `RealmShow/Fill` | `:535` | null（`fillMaxWidth=0` 走 Filled 模式） | **Filled / Horizontal** | **是**（`UIRealmBar.cs:192-195`） | false |
| 进度条 `Track` | `:528` | null | Simple | 否 | false |
| `MountShow/Preview` / `AppearanceShow/Preview` | `:844`, `:908` | **RawImage**（RenderTexture，非 Sprite） | — | 否 | **true**（拖动旋转） |
| 拖拽影子 `DragGhost` | `UIEntryList.cs:24-33` | null（色 = 品阶色） | Simple | 否 | false |
| 滚动条 `Scrollbar` / `Handle` | `UIBuildUtils.cs:131-143` | null | Simple | 否 | **均 true** |

---

## 4. 修炼页 `CultivationUI`

| 项 | 值 |
|---|---|
| 谁创建 | 场景 `3C_Testbed` 的 `cultivation room` 上的 `StationInteractable` 把 `界面预制体` 指向 `Assets/Prefabs/CultivationUI.prefab`（`3C_Testbed.scene` 里 `界面预制体` 的 fileID `182561180186654261` 与预制体根 `CultivationUI.prefab:3` **同一 fileID** ⇒ 确认是同一资产）；运行时由 `StationInteractor.打开界面` → `Instantiate(...)`（`StationInteractor.cs:610-613`），实例名被改写成 **`StationUI_修炼`**（`:613`）。**预制体本身只有根 + 1 个脚本，无 Canvas、无子物体**；仓库里没有生成它的编辑器脚本（`Assets/Editor/Builders` 下无对应文件）—— 见 §17.3 |
| 全工程设施对照 | `StationInteractable` 共 **8 处**，只有 **2 处**挂了界面预制体：`cultivation room`（修炼 → `CultivationUI.prefab`）与 `Sect` 的「炼丹」（→ `Assets/Prefabs/炼丹界面.prefab`）。其余 6 处 `界面预制体 = {fileID: 0}`（含「功德堂」那栋） |
| 脚本 | `Assets/Scripts/UI/CultivationUI.cs`（640 行） |
| 打开 | 玩家走近按 **F** → `StationInteractor.Update`（`StationInteractor.cs:193-197`）→ `Instantiate`；`CultivationUI.Awake` 里自建 Canvas 后**直接 `显示()`**（`:104-113`）——「被实例化 = 就是要显示」（`:109-111` 有明确注释）。`cultivation room` 的 `交互键覆盖 = 0 (None)`、`交互距离 = 4` 米、最大高度差 3 米 |
| 关闭 | **ESC 或再按 F**（`StationInteractor.cs:37`/`:36`/`:158`）或**走出 4 米范围自动关**（`:168-172`）→ `关闭界面()` → `Destroy(当前界面)`（`:675`，关闭冷却 0.25s）。⇒ `CultivationUI.隐藏()`（`:171`）在游玩路径上**无调用者** |
| Canvas | `CultivationCanvas`，Overlay，`sortingOrder = **2500**`，1920×1080 match 0.5（`:405-413`） |
| 按键 | **本文件不读任何按键、不写 `timeScale`、不注册 `UiEscRegistry`**（关闭时的 `记录关闭()` 由 `StationInteractor.cs:631` 代劳） |
| 数据 | 订阅 `PlayerCultivation.修为变化`（`:129-135`，成对退订 `:137-140`）；`修为` 在预制体里是 **null**，运行时 `FindObjectOfType<PlayerCultivation>()`（`:160`）；兜底功法表 `全部功法` 是预制体里序列化的 **2 门**（`CultivationUI.prefab:50-52`，对应 `太虚炼气诀` 与 `青云剑诀`） |

### 4.1 层级树

```
CultivationCanvas               [Canvas 2500]  (CultivationUI.cs:405)
├─ 幕布                        铺满, 0,0,0,0.55, raycastTarget=true   (:415-417)
└─ 主面板                      居中 1120×660, 色 0.72×3, raycastTarget=true   (:419-421, 尺寸 :53)
   ├─ 页签_闭关修炼 / 页签_境界突破 / 页签_转修功法
   │                           靠左(主面板尺寸)，x=12, y=660-12-64-i×76, 176×64, 24 号字   (:424-441)
   │                           （侧栏宽 200，块宽 = 200-24 = 176）
   └─ 内容区                    anchor 铺满, offset(200,12)~(-12,-12)   (:444-448)
      ├─ 页_闭关修炼            (:458-506)   [w=896, h=636]
      │  ├─ 功法信息            260×180，靠左 y=h-200=**436**，17 号 UpperLeft + 内边距 14   (:466-470)
      │  ├─ 当前境界            **52 号** MiddleCenter，靠左 (260, h-240=**396**) 636×90   (:473-475)
      │  ├─ 等级                20 号，色 0.25×3，靠左 (260, **336**) 636×30   (:476-478)
      │  ├─ 进度底              **896×34**，靠左 y=**150**，色 0.45×3，**sprite = 4×4 白图**   (:481-483)
      │  │  └─ 进度填充         铺满, **Filled / Horizontal / Left**, 色 0.32,0.62,0.36   (:484-491)
      │  ├─ 进度文字            18 号，靠左 (0,**118**) 896×28   (:492-494)
      │  ├─ 剩余次数            20 号 UpperLeft，靠左 (0,40) 448×60   (:497-499)
      │  ├─ 修炼一次            190×60，pos(**448**, 20)，24 号   (:502)
      │  └─ 闭关               190×60，pos(**658**, 20)   (:503)
      ├─ 页_境界突破            (:509-529)
      │  ├─ 突破所需物品信息     **465.92×636**（= w*0.52），靠左 (0,0)，19 号 UpperLeft + 内边距 20   (:516-520)
      │  ├─ 确认突破            300×62，pos(**555.52**, 24)   (:522)
      │  └─ 提示                18 号，色 0.55,0.15,0.15 ≈ `#8C2626`（暗红），靠左 (**501.76**, 180) 394.24×60   (:524-526)
      └─ 页_转修功法            (:532-581)
         ├─ 当前学会的功法       **896×550**，靠左 y=**86**，色 0.58×3   (:539-540)
         │  ├─ 标题            22 号 UpperLeft，靠左(列表块坐标系) (18, **504**) 860×32   (:542-549)
         │  ├─ 功法行0…N        **868×46**，靠左 (14, **450 - i×54**)，19 号 MiddleLeft + 内边距 16，可点；行数 = `取可转修功法().Count`（几何上限 9 行，`y < 10f` 就 break）   (:552-570)
         │  └─ 选中行底         `功法行图[i].color = (i == 选中功法) ? (0.80,0.80,0.55) ≈ #CCCC8C : 面板色`（`:304`）；当前修炼的那门**只改字色** `(0.15,0.35,0.15) ≈ #265926`（`:303`）
         ├─ 转修后境界预估       **591.36×62**（= w*0.66），靠左 y=12，19 号 + 内边距 18   (:572-576)
         └─ 确认转修            240×62，pos(**627.2**, 12)   (:578)
```

> 小坑：`内容区` 实际宽度是 **908**（offset x = 200 ~ −12，`:444-448`），而页面内 `靠左` 用的是 `w = 1120 − 200 − 24 = 896` —— **右边多出 12px**。页签与各块都按 `w` 定宽，所以右缘是齐的，不影响观感；换皮改布局时要记得这 12px 的存在。
```

其中 `w = 1120 - 200 - 24 = 896`，`h = 660 - 24 = 636`（`:462-463`）。

### 4.2 数值 / 颜色 / 图

| 项 | 值 | 依据 |
|---|---|---|
| 幕布色 | 0,0,0,0.55 | `:42` |
| 主面板色 | 0.72×3 | `:43` |
| 侧栏色 / 标签色 / 标签选中色 | 0.82×3 / 0.55×3 / 0.34×3 | `:44-46` |
| 面板色 | 0.58×3 | `:47` |
| 字色 | 0.12×3 | `:48` |
| 进度底色 / 进度填充色 | 0.45×3 / 0.32,0.62,0.36 | `:49-50` |
| 按钮底色 | 0.66×3，highlighted 0.92×3，pressed 0.78×3 | `:587-602` |
| 页签选中字色 | 0.95×3 | `:183` |

**图**：全部纯色 `Image`（`sprite == null`）—— **只有进度底 / 进度填充两处被赋了 4×4 白图 sprite**（`:482`, `:485`），因为 `fillAmount` 需要（`:89` 注释明写）。**没有九宫格、没有贴图。**

### 4.3 开关与交互

- 打开：`StationInteractor.打开界面` 实例化预制体 → `Awake` 里 `显示()`；**本文件不读 ESC / F**。
- `timeScale`：**不改**（面板开着游戏照跑）；`Cursor` 不管。
- `UiEscRegistry`：**不注册**（靠 `StationInteractor.有界面打开` 间接被协调）。
- 刷新：订阅 `PlayerCultivation.修为变化`（`:125-134`，成对退订 `:126-127`），并在 `显示()` 与 `切换页()` 时各刷一次（`:167`, `:185`）。
- **页签块与功法行的 `Button` 都没设 `ColorBlock`**（只设了 `targetGraphic`，`:435`, `:562`）⇒ 走 Unity 默认高亮；而 `建按钮` 那 4 个按钮设了 normal 白 / highlighted 0.92 / pressed 0.78（`:598-602`），是**乘在 0.66 灰底上的 tint**（高亮实际 ≈ 0.61）。
- 文字统一 `horizontalOverflow = Wrap` + `verticalOverflow = Overflow`（`UIBuildUtils.cs:53-56`）⇒ `功法信息`（260×180、17 号字）**不截断、会溢出框外**。换皮把板做小之前先确认文案长度。

---

## 5. 丹房 `炼丹界面`

| 项 | 值 |
|---|---|
| 谁创建 | 预制体 `Assets/Prefabs/炼丹界面.prefab`（由 `Assets/Editor/Tools/设施界面接线.cs:96-119` 生成，菜单「工具/主线/④ 生成炼丹界面预制体 + 挂到炼丹阁」）；运行时由 `StationInteractor.打开界面` **Instantiate**（`StationInteractor.cs:612`）。`Sect.scene` 的「炼丹」设施已挂该预制体（`Sect.scene` 里 `StationInteractable` 的 `界面预制体` guid 与预制体一致） |
| 脚本 | `Assets/Scripts/UI/炼丹界面.cs`（687 行），`Awake` 里无条件 `打开()`（`:103`） |
| Canvas | `AlchemyCanvas`，Overlay，`sortingOrder = **2650**`，1920×1080 match 0.5（`:523-532`） |
| 开关 API | `打开/关闭/切换/Open/Close/Toggle`（`:136-150`、`:684-686`）；`开关按键 = KeyCode.None`（`:51`）⇒ **本界面自己不读按键**，ESC / F 由 `StationInteractor` 负责 |
| timeScale / ESC | **都不改、不注册**（靠 `StationInteractor`） |

### 5.1 层级树与数值（坐标都是「内容框左下角为原点」，由 `放(...)` 摆）

```
AlchemyCanvas                    [Canvas 2650]
└─ 炼丹幕布                     **全屏**，色 0,0,0,0.72, raycastTarget=true，**它就是「面板」本体**   (:534-537)
   └─ 内容框                   居中 1560×880，色 0.15,0.14,0.17,0.97, raycastTarget=true   (:539-546)
      ├─ 标题位 → 标题         (0,16) 1560×56，44 号 MiddleCenter，色 0.97,0.93,0.80   (:549)
      ├─ 灵气位 → 灵气         (1100,26) 420×40，20 号 MiddleRight   (:551)
      ├─ 成品预览（放板）        (40,100) 360×260，色 `板色` 0.20,0.19,0.22   (:555)
      │  ├─ 预览方块            (20,20) **320×150**，底 0.24,0.23,0.26（运行时按成功/失败改色），可点 → 切回丹方   (:556-559)
      │  └─ 预览名              (20,178) 320×66，22 号 UpperCenter   (:560)
      ├─ 信息板                 (420,100) 700×260   (:564)
      │  ├─ 信息标题            (18,12) 664×30，20 号，色 `灰字色` 0.62,0.61,0.64   (:565)
      │  └─ 信息                (18,46) 664×200，19 号 UpperLeft   (:567)
      ├─ 右栏位                 (1140,100) 380×460（同一位置二选一显示）   (:571)
      │  ├─ 丹方板（节点名由 `放板` 生成：`丹方板底`）  (0,0) 380×460；`标位/标`「获得的丹方」22 号 (16,-12) 348×32；`提位/提` 提示 15 号灰 (16,-48) 348×52；`视口` (16,106) **348×338** + RectMask2D；`内容` 挂 **VerticalLayoutGroup(spacing 6)** + ContentSizeFitter(vertical=PreferredSize) + **ScrollRect(sensitivity 30, Clamped)**；行 `丹方0..N` = `LayoutElement` 高 **46**   (:572, :628-656)
      │  └─ 背包板              同上，行 `背包0..N` 高 **44**，提示文字不同，默认 SetActive(false)   (:574-576)
      ├─ 主材格                 (40,384) **200×132**，可点   (:583-589)
      ├─ 辅材格0…3              (260 + i×216, 384) **200×132**，可点   (:583-589)
      ├─ 清空位 → 清空材料       (40,530) 200×46，18 号   (:593-599)
      ├─ 开炼位 → 开炼！         (1140,580) **380×170**，46 号，底 `开炼色` 0.84,0.17,0.14，字 1,0.97,0.94   (:602-611)
      ├─ 提示位 → 提示          (260,540) 860×40，19 号   (:614)
      └─ 关闭位 → 关闭          (40,800) 1560×48，20 号，底 `按钮暗色` 0.30,0.29,0.32   (:617-624)
```

### 5.2 颜色与图

| 名 | 原值 | 依据 |
|---|---|---|
| 幕布色 | 0,0,0,0.72 | `:57` |
| 面板色 | 0.15,0.14,0.17,0.97 | `:58` |
| 板色 | 0.20,0.19,0.22 | `:59` |
| 标题色 / 正文色 / 灰字色 | 0.97,0.93,0.80 / 0.88,0.87,0.86 / 0.62,0.61,0.64 | `:60-62` |
| 强调色 | 0.55,0.85,0.75 | `:63` |
| 按钮色 / 按钮暗色 / 按钮字色 | 0.72,0.58,0.30 / 0.30,0.29,0.32 / 0.10,0.09,0.08 | `:64-66` |
| 开炼色 | 0.84,0.17,0.14 | `:67` |
| 警告色 / 选中格色 | 1,0.66,0.40 / 0.52,0.44,0.26 | `:68-69` |
| 材料格常态底 | 0.24,0.23,0.26；选中 = 选中格色 | `:513`, `:331` |
| 背包按钮选中底 | 按钮色；未选 0.34,0.32,0.30 | `:482` |
| 丹方行常态底（按能否开炼三态） | 选中 = 按钮色 0.72,0.58,0.30 ≈ `#B8944C` / 材料够 0.42,0.38,0.30 ≈ `#6B614C` / 不够 0.24,0.23,0.26 ≈ `#3D3B42` | `:404` |
| 预览方块 | 有解 0.78,0.70,0.42 ≈ `#C7B26B`；无解 0.35,0.33,0.36 ≈ `#59545C` | `:373-376`, `:557` |
| 开炼按钮字 | 1,0.97,0.94 ≈ `#FFF7F0`（46 号） | `:608-610` |
| 材料格内小字 | `格标题` 15 号灰字色 (10,-6) 180×24；`格字` 18 号正文色 (10,-30) 180×96（有料但不够时改警告色 1,0.66,0.40 ≈ `#FFA866`） | `:328`, `:660-662` |

**图**：全部纯色 —— 4 处 `UIBuildUtils.CreateImage` + 1 处 `AddComponent<Image>`（材料格底，`:512`）；`sprite` 赋值 **0 处**、`Image.Type` **0 处**、`fillAmount` **0 处**。
射线：幕布 `:535`、内容框 `:540`、材料格 `:514`、清空 `:595`、开炼 `:605`、关闭 `:620` 显式打开。

---

## 6. 灵田（地块界面 + 总览）

### 6.1 灵田地块界面（走近按 F 的小窗）

| 项 | 值 |
|---|---|
| 谁创建 | **静态工厂** `灵田地块界面.造(灵田地块)`（`灵田地块界面.cs:71-89`）；调用者 `Assets/Scripts/SpiritField/灵田地块.cs:132-137` 的 `开面板()`；由 `StationInteractor` 的 `灵田地块` 分支触发（`StationInteractor.cs:573-593`） |
| 销毁 | `灵田地块.关面板()` = `Destroy(面板)`（`灵田地块.cs:139`）—— 幕布与面板**同根**，不存在「面板关了幕布还在」（`:25-29` 有这条血泪注释） |
| Canvas | `灵田地块界面`，Overlay，`sortingOrder = **1800**`，1920×1080 match 0.5（`:74-81`） |
| timeScale / ESC | **都不改、不注册** |

```
灵田地块界面 Canvas            [Canvas 1800]  (灵田地块界面.cs:73)
├─ 幕布                       铺满, 0,0,0,0.55, raycastTarget=true   (:99-101)
└─ 面板                       居中 **500×570**, 色 0.16,0.14,0.12,0.98, raycastTarget=true   (:104-110, 尺寸 :51-52)
   ├─ 标题                    26 号 MiddleLeft，锚顶 y=-14 高 36   (:112-113, 锚顶 :148-155)
   ├─ 状态                    19 号 UpperLeft，锚顶 y=-56 高 92   (:115-116)
   ├─ 动作区                  anchor 铺满, offset(16,178)~(-16,-156)   (:119-123)
   │  ├─ 种子标题             16 号 UpperLeft，高 22，色 正文色 α0.75   (:234-241)
   │  ├─ 种子排               anchor(0,1)-(1,1)，高 32×5+5×4 = **180**，VerticalLayoutGroup(spacing 5)   (:244-250)
   │  │  └─ 种子_<id> ×N      16 号 MiddleLeft，`LayoutElement` 30 高，选中时底 = 强调色 (0.78,0.52,0.33)   (:264-281)
   │  └─ 主按钮              anchor(0,0)-(1,0) 高 **44**，20 号   (:301-307)
   ├─ 提示                    15 号 UpperLeft，色 1,0.72,0.40，pos y=122 高 48   (:126-132)
   ├─ 挪动地块                160×42，18 号，左下 (16,70)   (:134-136, 锚底 :158-165)
   ├─ 升级田地（升级这块地）    300×42，18 号，右下 (-16,70)   (:138-141)
   └─ 关闭（「关　闭（ESC）」）  220×42，18 号，底部居中 (0,16)   (:143-145)
```
颜色（`:36-49`）：幕布 0,0,0,0.55；面板 0.16,0.14,0.12,0.98；标题 0.97,0.93,0.80；正文 0.88,0.87,0.83；按钮字 0.10,0.09,0.07；强调 0.78,0.52,0.33；**灰字 0.80,0.78,0.74**（`:43-49` 解释了为什么不能把深色字调暗）。
**图**：全纯色；`sprite` / `Image.Type` / `fillAmount` **各 0 处**；只有选中种子按钮时改 `Image.color`（`:279`）。

### 6.2 灵田总览（F4）

| 项 | 值 |
|---|---|
| 谁创建 | **每场景自举**：`场景自举.确保组件<灵田界面>(相机, 场景名)`（`场景自举.cs:248`）⇒ 挂主相机 |
| Canvas | `灵田总览`，Overlay，`sortingOrder = **2600**`，1920×1080 match 0.5（`灵田界面.cs:150-158`） |
| 开关 | **F4**（`开关按键 = KeyCode.F4`，`:47`；`Update :106`）；`打开/关闭/切换`（`:117-132`） |
| 风险 | `:106` 在已打开时 F4 也能关，但**不向 `UiEscRegistry` 记录关闭** ⇒ 关掉后立刻按 ESC 会同时弹暂停菜单 |

```
灵田总览 Canvas                [Canvas 2600]  (灵田界面.cs:150-158)
├─ 幕布                       铺满, raycastTarget=true   (:161-164)
└─ 面板                       anchor(0.5,1) pivot(0.5,1) pos=(0,-90) **720×620**，色 0.16,0.14,0.12,0.97   (:167-173)
   ├─ 标题「洞府灵田 · 总览」   28 号 MiddleLeft，pos(0,-14) (−36)×38，色 0.97,0.93,0.80   (:175-181, :58)
   ├─ 汇总                    19 号 UpperLeft，pos(0,-56) (−36)×46   (:183-189)
   ├─ 清单                    18 号，offset(18,84)~(-18,-108) ⇒ 684×428，**lineSpacing = 1.25** + 富文本上色   (:191-201)
   ├─ 提示                    17 号 LowerCenter，pos(0,56) (−36)×30，色 1,0.85,0.42 ≈ `#FFD96B`   (:203-209, :60)
   ├─ 一键收取（一键收取全部成熟） **240×44**，左下 (18,16)，20 号   (:211-218)
   └─ 关闭（「关　闭（F4）」）   **180×44**，右下 (-18,16)，20 号   (:220-227)
```
**图**：全纯色（2 处 `CreateImage` + 2 个 `CreateButton`）；`sprite` / `Image.Type` / `fillAmount` **各 0 处**。**没有 ScrollRect / LayoutGroup / 遮罩** —— 清单是一整块多行 `Text`。

---

## 7. 功德堂兑换

| 项 | 值 |
|---|---|
| 谁创建 | 组件由 `场景自举.补功德堂` 运行时 `AddComponent<功德堂兑换>()` 补到 `Sect` 场景里名为 **`environment_Building_luoxiaguan_001_d`** 的建筑上（`场景自举.cs:294-311`）；面板**懒建**（首次 `开面板` 才 `建面板`，`功德堂兑换.cs:71`） |
| 驱动 | 开：`StationInteractor.cs:557-568`；关：`StationInteractor.cs:648-660` |
| Canvas | `功德堂Canvas`，Overlay，`sortingOrder = **2600**`，1920×1080（**未设 `matchWidthOrHeight`**，`:136-140`） |
| API | `开面板 / 关面板 / Open / Close`（`:69`, `:77`, `:257-258`） |
| 上架规则 | 扫物品库取 `ItemDefinition.兑换消耗贡献 > 0`，按贡献升序（`:81-97`） |
| timeScale / ESC | **都不改、不注册** |

```
功德堂Canvas                   [Canvas 2600]
├─ 幕布                       铺满, 0,0,0,0.55, raycastTarget=true   (:142-144)
└─ 功德堂                     居中 **900×640**, 色 0.06,0.06,0.08,0.96   (:146-150)
   ├─ 标题「功德堂 · 贡献兑换」 34 号 MiddleCenter，顶部 pos(0,-12) 高 56，色 1,0.84,0.42   (:152-158)
   ├─ 贡献                    22 号 MiddleCenter，pos(0,-70) 高 34，色 0.92,0.92,0.88   (:160-166)
   ├─ 行0…N（品名 + 【消耗 贡献】+ 介绍）
   │                          22 号 MiddleLeft，高度 `行高-8 = 46`，pos(-140, -116 - i×54)，`行高 = 54`   (:207-213, :42)
   ├─ 兑换0…N                 150×42，右上 pos(-24, -116 - i×54)，22 号；够 → 0.20,0.42,0.26，不够 → 0.24,0.24,0.26 且 `interactable=false`   (:215-224, :233-241)
   ├─ 提示                    20 号 MiddleCenter，底部 pos(0,62) 高 30，色 1,0.84,0.42   (:168-174)
   └─ 关闭                    220×46，底部居中 pos(0,10)，24 号   (:176-184)
```
其它颜色（`:34-40`）：底板 0.06,0.06,0.08,0.96；正常色 0.92,0.92,0.88；不足色 0.72,0.45,0.42。
**图**：全纯色（幕布、面板 2 处 `CreateImage` + 按钮若干）；`sprite` / `Type` / `fillAmount` **各 0 处**。
已知小坑：
- 面板 Root 的 `raycastTarget` 保持默认 **false**（`:146` 复用 `CreateImage` 未打开）—— 点面板空白处不拦射线（拦截靠幕布 + 按钮）。
- **没有 ScrollRect / 遮罩**，行是绝对定位（首行 y = −116，行距 = `行高 54`，`:205`）。面板高 640 ⇒ 约 **8 行**以内不溢出，再多会画到面板外面。
- `刷新()` 末尾 `说("")` 会清提示（`:193`），所以「换到了…」只显示到下一次刷新。
- `兑换i` 按钮字号**写死 22**（`:215`），不跟 `字号` 字段走。

---

## 8. 任务追踪 `任务引导`

| 项 | 值 |
|---|---|
| 谁创建 | `场景自举.确保组件<任务引导>(相机, 场景名)`（`场景自举.cs:249`）⇒ **每场景一份，挂主相机** |
| Canvas | `QuestGuideCanvas`，Overlay，`sortingOrder = **1500**`，1920×1080 match 0.5；`GraphicRaycaster` 挂了但 **`enabled = false`**（`任务引导.cs:511-531`） |
| 三个部分 | ① 右上角追踪面板 ② 目标头顶「！」③ 屏幕边缘箭头（全在**屏幕空间**，不往场景塞物件，`:35-42`） |
| timeScale / ESC | **都不改、不注册**（纯只读展示） |

```
QuestGuideCanvas                [Canvas 1500, 不吃射线]
├─ 标记层                     铺满, pivot(0.5,0.5)（anchoredPosition 即「以屏幕中心为原点」的坐标）   (:536-538)
│  ├─ 感叹号                  52×52, 52 号「！」，色 1,0.82,0.2，+ 描边（1,1,-1 全部 0,0,0,0.85），默认隐藏   (:583-590, 尺寸/色 :86-89)
│  └─ 边缘箭头                44×44，sprite = **运行时生成的 64×64 金三角**（`:606-629`），靠 `localRotation` 指方向   (:593-599)
├─ 主线追踪（面板）           anchor(1,1) pivot(1,1) pos=(-18,-150) **宽 400**   (:546-556, 默认值 :59-68)
│  ├─ 标题                   25 号 UpperRight，色 1,0.84,0.42（金）   (:565, :70)
│  ├─ 目标                   24 号 UpperRight，色 0.97,0.96,0.92   (:566, :71)
│  ├─ 说明                   18 号 UpperRight，色 0.72,0.73,0.70   (:567, :72)
│  ├─ 距离                   18 号 UpperRight，色 0.85,0.78,0.55   (:568, :73)
│  └─ 排版                   VerticalLayoutGroup(spacing **8**, padding 16/16/14/14) + ContentSizeFitter(vertical=PreferredSize)   (:558-563)
```
- 全部文字 + `AddOutline`（黑 α0.85，`:576`），因为底板是半透明的。
- **唯一有 sprite 的节点是「边缘箭头」**，且 sprite 是运行时 `Texture2D` 画的（工程里没有箭头图，`:602-605` 明说）。
- **`主线追踪` 是后建的**（先 `搭标记()` 再 `搭面板()`：`任务引导.cs:540-541`），所以照 uGUI 兄弟序**面板画在标记层之上**。
- ⚠️ 与 Toast 的间隙：面板 `上边缘 = 150`（`:62`），而 Toast 默认第 1 条的底边约 **156**（`顶边距 104 + 高 52`）—— 两者可能压住面板标题 6px（代码注释写的「Toast ≈144」与实际不符）。换皮把面板做高时顺手把 `上边缘` 调大。

---

## 9. 对话 `DialogueUI`

| 项 | 值 |
|---|---|
| 谁创建 | 静态 `确保()` = `new GameObject("对话界面")` + `AddComponent<DialogueUI>()`（`DialogueUI.cs:88-93`）—— **场景与预制体里都不存在**；Canvas 在 `Awake` 里自建（`:124-130`），默认 `收起来()`（`:269-276`） |
| 调用者 | `NpcDialogue.打开对话()` → `DialogueUI.打开(this)`（`Assets/Scripts/Npc/NpcDialogue.cs:277`）；任务系统 `演出多行`（`任务管理器.cs:1324`、`:1329`） |
| Canvas | `对话界面`，Overlay，`sortingOrder = **2600**`，1920×1080 match 0.5，自建 `EventSystem`（`:646-662`） |
| 关闭 | `关闭()`（`:116`）→ `收起来()`；玩家 **Escape**（`:469`）、点「结束」/最后一段回答（`:411`, `:419`）、任务管理器起黑幕前强关（`任务管理器.cs:1258`） |
| 按键 | 继续键 `KeyCode.F`（可改，`:66`）+ **Space**；打开那一帧不吃同一个键（`:83`, `:140`, `:467`） |
| timeScale / 鼠标 / ESC | 都不改 timeScale、不锁鼠标、**不注册 `UiEscRegistry`**（靠 `StationInteractor.cs:178-183` 把「有界面打开」置真间接生效） |

### 9.1 层级树与数值

```
对话界面                        [Canvas 2600]  (DialogueUI.cs:646-649)
└─ 对话根                     铺满   (:664-667)
   ├─ 人物立绘                anchor(0,0)-(0.298,0.747) ⇒ 左 29.8% 宽、74.7% 高   (:670-671)
   │  └─ 人物立绘字           铺满，**34 号** MiddleCenter，色 0.06,0.06,0.08   (:672-674)
   ├─ 玩家立绘                anchor(0.774,0)-(1,0.747)   (:677-678)
   │  └─ 玩家立绘字           铺满，**30 号**   (:679-681)
   ├─ 对话框                  anchor(0.132,0)-(0.684,0.277)，色 0.62,0.62,0.62,0.92，**raycastTarget=true**   (:684-686)
   │  ├─ 名字底               anchor(0,1) pivot(0,1) pos=(18,-12) **240×56**，色 0.24,0.24,0.26,0.95   (:689-694)
   │  │  └─ 名字             **28 号** MiddleLeft，offset(18,0)~(-10,0)，色 0.96,0.96,0.96   (:695-696)
   │  └─ 对话内容            **32 号** MiddleCenter，offset(38,26)~(-38,-76)，色 0.06,0.06,0.08，Wrap + Overflow   (:699-700, :623-624)
   └─ 回答列                  anchor(0.729,0.098)-(0.885,0.277)；VerticalLayoutGroup(spacing **14**)   (:703-714)
      └─ 回答1 / 回答2 / …     由 VLG 控高；`LayoutElement` preferredHeight **74**、minHeight **52**；26 号 MiddleLeft，Wrap + **Truncate**   (:425-455)
```
说话方缩放：`说话方缩放 = 1`、`非说话方缩放 = 0.82`（`:59`, `:62`，施加在 `:261-262`、`:377-378`）。

### 9.2 用了什么图（这是全工程最缺素材的地方）

| 节点 | 创建处 | sprite | Type | raycastTarget |
|---|---|---|---|---|
| 人物立绘 / 玩家立绘 | `建图`（`DialogueUI.cs:604-612`），调用 `:670`, `:677` | `Resources.Load<Sprite>("立绘/" + 资源名)`（`:384`）；资源名空 ⇒ **null 纯色**（色 0.93,0.90,0.43,0.92 ≈ `#EDE66E`，`:386`） | Simple | false |
| 名字底 / 对话框 | 同上 | **null 纯色** | Simple | 对话框 **true** |
| 回答按钮 | `:425-441` | null 纯色（常态 0.80×3 α0.96 / hover 0.95,0.92,0.70 / pressed 0.7,0.68,0.55） | Simple | true（Button） |
| `泡`（情绪特效） | `:535-546` | `取圆点()` = 运行时 `64×64` 圆点 sprite（`:577-594`） | Simple | false |

**关键事实**：`Assets/resources/立绘/` 目录下**只有 `说明.txt`，没有任何 PNG** ⇒ 现在两个立绘位**必然**是黄块 + 中文占位字。`说明.txt` 写明命名规矩：文件名 = 对话表 `立绘` 列的值（玩家立绘写进 `玩家立绘` 列）。

---

## 10. 暂停 / 主菜单 / 身陨 / 起名

### 10.1 暂停菜单 `PauseMenuUI`

| 项 | 值 |
|---|---|
| 谁创建 | **场景里有根物件 `暂停菜单`**（挂 `PauseMenuUI`，**没有 Canvas**），5 个游玩场景各一份；装 / 卸的菜单是 `Cultivation/安装暂停菜单`（`Assets/Editor/Tools/PauseMenuInstaller.cs:22-67`，装完**会自动保存场景**）。5 份副本由 `Assets/Editor/Tools/SceneRigSyncer.cs` 从 `3C_Testbed` 统一同步（`:42` 的 `总是覆盖` 名单含 `暂停菜单`）。**`PauseMenuCanvas` 是运行时建的** |
| Canvas | `PauseMenuCanvas`，Overlay，`sortingOrder = **3000**`，1920×1080 match 0.5（`PauseMenuUI.cs:191-200`） |
| 开关 | ESC（`暂停键`，`:20`）；`暂停()` / `继续()` / `切换()`（`:77-105`） |
| timeScale | 暂停 `= 0`（`:90`），继续还原（`:102`），**存值时会防 0**（`:88`）；`不改时间缩放` 开关可关掉这套（`:36`，场景里都是 false） |
| 鼠标 | 暂停时 `Cursor.visible = true` + `lockState = None`（`:92-93`）；**继续时不恢复锁定** |
| ESC 协调 | `UiEscRegistry.有别的界面开着()`（`:66`）+ `刚关闭过()`（`:72`）双闸 |
| UI | `暗色底` 铺满（色 0,0,0,0.72，`raycastTarget=true`）；`标题`「暂停」**84 号** 居中 pos(0,-140) 600×110；**4 个**按钮「保存游戏 / 设置 / 返回主界面 / 退出游戏」，**230×52**，起点 y=-300，**步长 70**（52+18）⇒ -300/-370/-440/-510，30 号字，底 0.88×3 ≈ `#E0E0E0`，色四态 normal 白 / highlighted 0.80,0.85,1.0 ≈ `#CCD9FF` / pressed 0.65,0.72,0.95 ≈ `#A6B8F2` / fade 0.06（`:22-28`, `:202-223`, `:264-277`） |
| 保存 | 右上 Toast「保存中…」→ `WaitForSecondsRealtime(0.35f)` → 「已保存到槽位 N」（`:132-146`，走 `SaveSystem`） |
| 设置 | **只打日志**，设置面板未实现（`:114-119`，TODO 在 `:118`） |
| 图 | 全纯色；`sprite` / `Type` / `fillAmount` **各 0 处** |
| 注释过期 | 类注释（`:8`）写「三个浅色按钮」，实际是 **4 个**（`:215`） |

### 10.2 主菜单 `MainMenuBuilder` + `MainMenuUI`

| 项 | 值 |
|---|---|
| 谁创建 | `Assets/Editor/Builders/MainMenuBuilder.cs:29`，菜单「修仙/生成开始界面场景」；产出并保存 `Assets/Scenes/StartScene.scene` |
| Canvas | `MenuCanvas`，Overlay，1920×1080 match 0.5，有 `GraphicRaycaster` + 自建 `EventSystem`（`:47-58`） |
| 背景 | `Background` 铺满，**`sprite = Assets/Resources/UI/MainMenu/begin_ui.png`**，`Image.Type.Simple`，`preserveAspect = false`（`:63-67`）—— **全工程唯一一张全屏 UI 贴图**（该 PNG 有 **7.2 MB**，注意分辨率与压缩） |
| 标题 | `Title`「修仙」**150 号** UpperCenter，色 0.06,0.09,0.10，anchor (0,1)-(1,1) offset(0,-230)~(0,-40)，+ `Outline`(0.75,0.95,1,0.85) 2px（`:70-76`） |
| 按钮列 | `Buttons` anchor(0,0.5) pivot(0,0.5) pos=(60,-40) **240×320**，`VerticalLayoutGroup(spacing 18)`；4 个按钮 `BtnNewGame` / `BtnLoad` / `BtnSettings` / `BtnQuit`，**28 号**（`:79-98`） |
| 存档面板 | `SavePanel` 居中偏右 **760×780**（pos(150,-20)），色 0.86,0.87,0.87,0.96，默认隐藏（`:101-107`, `:152`）；`PanelTitle`「读取存档」**30 号**；`BtnClose`「✕」**52×52** 右上 (18,18)；`Slots` offset(34,40)~(-34,-92) + `VerticalLayoutGroup(spacing 14, childAlignment=UpperCenter)`（`:109-130`） |
| 槽位行（运行时建，`MainMenuUI.cs:115-170`） | `Slot0..Slot4`（**槽位数 = 5**）：`LayoutElement` min/preferred **78**；底 = 有档 0.62×3 ≈ `#9E9E9E` / 空 0.5×3 ≈ `#808080`，**显式 `raycastTarget = true`**（`:128`）；行内 `Name`（22 号，offset(16,0)~(-16,-14)）、`Time`（18 号，仅有档，offset(16,12)~(-130,0)）、`Delete#`（**96×44**，18 号，常态 0.45×3 / 待确认 0.85,0.35,0.3 ≈ `#D9594D`，两步确认 3 秒过期） |
| 提示 | `Hint` **24 号** LowerCenter，色 0.95,0.85,0.6 ≈ `#F2D999`（`:133-136`） |
| 图 | 背景 1 张 PNG；其余纯色；`fillAmount` **0 处** |
| 注意 | 按钮监听**全部在运行时接**（`MainMenuUI.cs:52-59`）—— 生成器里 `AddListener` 不会被序列化；`Assets/Resources` 在磁盘上实际是小写 `Assets/resources`（`begin_ui.png` 也在其中） |

### 10.3 身陨界面 `DeathScreenUI`

| 项 | 值 |
|---|---|
| 谁创建 | 运行时自建 Canvas（`DeathScreenUI.cs:78-89`），默认 `隐藏()`；由 `PlayerDeathSequence` 在死亡 2 秒后打开 |
| Canvas | `DeathScreenCanvas`，Overlay，`sortingOrder = **4000**`（盖过暂停菜单） |
| 层级 | `幕布` 铺满（0,0,0,0.62，`raycastTarget=true`）→ `标题`「杀伐道途，终是棋差一招」**64 号** 1400×120 @Y=150 → `副标题`「你死了」30 号 800×60 @Y=60 → `重生按钮` **210×84** 底 `1,0.92,0.10`（亮黄 ≈ `#FFEB1A`）@Y=-90 + 黑字 34 号（`:18-35`, `:92-125`） |
| 图 | 全纯色 |

### 10.4 起名界面 `起名界面`

| 项 | 值 |
|---|---|
| 谁创建 | **纯运行时、无场景物件**：静态 `确保()`（`起名界面.cs:64-69`）`new GameObject("起名界面")` + `AddComponent`（**无父节点 = 场景根**）。单例 `实例`（`:27`）。唯一调用方 `主线开场.cs:102`（`起名界面.取名("那个少年的名字叫做：", …)`） |
| Canvas | 就挂在 `起名界面` 这个物件上；Overlay，`sortingOrder = **2950**`，1920×1080 match 0.5（`:324-335`） |
| 层级 | `黑幕`（全屏纯黑 α1，`raycastTarget = true`，`:338-343`）→ `标题` 44 号，anchor (0.1,0.62)-(0.9,0.72)，色 0.94×3（`:346-350`）→ `输入` **64 号** MiddleCenter，anchor (0.5,0.52)，1400×**80**（`:359-365`）→ `光标`「_」64 号，pivot(0,0.5)，40×74，每帧按文字宽度右移（`:367-373`）→ `提示`「输入名字，回车确认（最多 5 个字）」22 号，anchor (0.1,0.30)-(0.9,0.38)，色 0.55×3（`:376-380`） |
| 规则 | 最大字数 **5**（`:40`）、光标周期 **0.7f**（`:43`）、回车屏蔽 **0.15s**（`:168`）；确认 = 回车 / 小键盘回车（`:234-245`）；退格删代理对（`:145-153`）；空名不许确认（`:156`） |
| timeScale / 鼠标 / ESC | 都不动；**不注册 `UiEscRegistry`**；演出锁走 `黑幕字幕.开始演出()/结束演出()`（`:98`, `:302`） |
| 图 | 只有 `黑幕` 一张纯色 Image；其余全是 Text（**无任何贴图、无 TMP**） |

---

## 11. 黑幕字幕

| 项 | 值 |
|---|---|
| 谁创建 | 静态 `确保()` = `new GameObject("黑幕字幕")` + `AddComponent<黑幕字幕>()`（`黑幕字幕.cs:98-103`）；`Awake` 里自建 Canvas + **`DontDestroyOnLoad`**（`:474-482`），默认收起（`:481`） |
| Canvas | `黑幕字幕`，Overlay，`sortingOrder = **2900**`，1920×1080 match 0.5（`:866-873`） |
| 调用者 | `主线开场.cs:96-119`；`任务管理器`（`:1272`, `:1280`, `:1359`, 闪白 `:1066`）；`Teleporter.cs:385`；`TowerController.cs:421` |

```
黑幕字幕                        [Canvas 2900, DontDestroyOnLoad]
├─ 黑幕  [sibling 0]          铺满，**纯色黑 #000000**，raycastTarget=true（吃掉点击）   (黑幕字幕.cs:878, 建图 :893-907)
├─ 白闪  [sibling 1]          铺满，纯色白 α=0（闪白时才动），raycastTarget=true   (:880)
└─ 字幕                        anchor(0.12,0.35)-(0.88,0.65)，**44 号** MiddleCenter，色 0.94×3，`lineSpacing = 1.35`，Wrap + Overflow   (:883-890, 色/字号 :34-36)
```
兄弟序由 `建图(名, 色, 序)` 的 `SetSiblingIndex` 决定（黑幕 0 / 白闪 1，`黑幕字幕.cs:905`）；`字幕` 用 `建字` 追加到末尾 ⇒ **字幕渲染在白闪之上**。

| 动画参数 | 值 | 依据 |
|---|---|---|
| 幕色 / 字色 / 字号 | `Color.black` / 0.94×3 / **44** | `:35-36`, `:34` |
| 淡入淡出 | **纯 `Image.color.a`**，`fillAmount` 全程不用；**阶梯跳档**（不是平滑 Lerp），`淡步频 = 12 步/秒` | `:673-679`, `:690-707`, `:61` |
| 默认淡入 / 淡出步数 | **0 / 0 ⇒ 默认立刻全黑、立刻收掉** | `:55`, `:58` |
| 字幕比幕晚出 | `字a = Clamp01((a - 0.5) * 2)` | `:677-678` |
| 打字机速度 | **18 字/秒**；按住左键 **6×** 加速；每帧步进上限 `1/30` 秒；行间停顿 **0.7 秒** | `:40`, `:43`, `:625`, `:46` |
| 闪白 | 默认 0.3 秒：白起 → 0.35×t 淡入 + 0.06 秒 + 0.6×t 淡出 | `:470`, `:840-842` |
| 切场景 | 字打完后停 **2 秒**（常量），再淡出 | `:152`, `:275` |
| timeScale / ESC / 鼠标 | **从不写 timeScale**（全程 `unscaled*`）；不注册 `UiEscRegistry`；**不锁鼠标**，屏蔽靠「幕 & 白闪吃射线」+ 静态演出锁 `演出中` | `:371`, `:556-557` |

**图**：黑幕 / 白闪都是 **`sprite == null` 的纯色 `Image`**（`建图` 从不赋 sprite）；`fillAmount` **0 处**。

---

## 12. Toast `ToastUI`

| 项 | 值 |
|---|---|
| 谁创建 | 静态 `提示(文本, 时长=0)`（`ToastUI.cs:51`）→ 现建一条 `Toast`；共享 Canvas 懒建 `ToastCanvas`（`:64-71`）并 `DontDestroyOnLoad` |
| Canvas | `ToastCanvas`，Overlay，`sortingOrder = **5000**`（全场最高），1920×1080 match 0.5 |
| 调用者 | `PauseMenuUI.cs:132/145/149`、`Teleporter.cs:344`、`NpcTargeting.cs:231` |
| 关闭 | **无外部 API**，到时自毁（`:144-150`） |

```
ToastCanvas                     [Canvas 5000]
└─ Toast                       anchor(1,1) pivot(1,1) **340×52**；pos = (-24, -(104 + y))；CanvasGroup 控 alpha
   ├─ 底                       铺满 padding=0，色 0.06,0.07,0.10,0.86（≈ #0F121A α0.86）
   └─ 文字                    铺满 padding=10，**22 号** MiddleCenter，色 0.96,0.95,0.90
```
- 多条**从上往下排队**：第 n 条 y 累加 `52 + 8 = 60`（`行间距 = 8`，`:131-132`）。
- `顶边距 = 104` 是**故意的**：让开右上角 `纪年HUD`（顶边距 18 + 约 70 高）与任务追踪，`:28-39` 有完整解释。
- 动画：淡入 **0.15s** / 停留 **2.2s** / 淡出 **0.45s**，用 `Time.unscaledDeltaTime`（暂停时照走）。
- **图**：`底` 是 `UIBuildUtils.CreateImage` 的纯色（sprite==null）；`文字` raycastTarget=false；整条不吃射线。

---

## 13. 镇妖塔 `TowerUI`

| 项 | 值 |
|---|---|
| 谁创建 | 场景 `Demon-Suppressing Tower` 的根物件 `塔控` → 子节点 **`塔界面`**（`TowerUI` 挂在这里）；由生成器 `修仙/镇妖塔/装配镇妖塔场景`（`Assets/Editor/Builders/TowerBuilder.cs:139`）产出，`AddComponent<TowerUI>()` 在 `:464`，`界面.塔` 接线在 `:465`；体检项在 `:173-179`。**`TowerCanvas` 是运行时建的**（`TowerUI.cs:185-186`） |
| Canvas | `TowerCanvas`，Overlay，`sortingOrder = **2500**`，1920×1080 match 0.5（`TowerUI.cs:185-196`） |
| 两个部分 | ① 层数 HUD（顶部居中，进塔就有）② 死亡选择面板（10 秒倒数，由 `TowerController.cs:569/575/596` 开关） |

```
TowerCanvas                     [Canvas 2500]
├─ 塔层HUD                    anchor(0.5,1) pivot(0.5,1) **900×40**，pos=(0,-18)，**24 号** UpperCenter，色 0.98,0.96,0.90，+ Outline 黑 α0.7   (TowerUI.cs:207-215, 默认值 :57-66)
└─ 死亡选择                   全屏幕布, 色 0,0,0,0.62, raycastTarget=true   (:221-224, 搭选择面板 :219)
   ├─ 标题「你在第 N 层倒下」    **40 号** MiddleCenter，居中 pos(0,150) 1200×70，+ Outline   (:226-234)
   ├─ 按钮排                  居中 pos(0,-40) **340 宽**，高 = 按钮数×96，VerticalLayoutGroup(spacing **18**)   (:236-247)
   │  └─ 出塔 / 继续深入 · 下一层
   │                          每个 `CreateButton`（30 号），**LayoutElement 78 高**，底 `按钮底色` 1,0.92,0.10 ≈ `#FFEB1A`，字 0.10,0.10,0.10   (:249-268, :51-52)
   └─ 倒计时「自动退出：N 秒」   **28 号**，居中 pos(0,-175) 700×44，色 1,0.42,0.32 ≈ `#FF6B52`，+ Outline   (:282-290, :53)
```
> 「清完怪上楼」**不在**这个界面 —— 它已改成场景里的传送圈，由 `TowerController` 管（`TowerUI.cs:14-21`）。

**图**：全纯色；按钮底图的 `raycastTarget` 必须显式打开（`:254-257` 注释）。

**两个换皮时会撞上的小问题**：
- 死亡面板的按钮排 `childControlHeight = false`（`:245`），而 `CreateRect` 又不赋 `sizeDelta` ⇒ 那句 `LayoutElement 78 高`（`:262-264`）**实际不生效**，按钮高度取 RectTransform 引擎默认值。换图前先把高度钉死，否则素材会被拉成想不到的比例。
- `正文色`（0.90,0.90,0.88）声明后**全文未使用**（但已序列化进场景）—— 换皮时可忽略。

---

## 14. 传送面板 `Teleporter`

| 项 | 值 |
|---|---|
| 谁创建 | `Teleporter.建面板()`（`Assets/Scripts/Core/Teleporter.cs:262`）—— **运行时自建、行数取决于选项数**；**没有调用 `SetParent`，所以 `传送面板` 是场景根对象**（`TowerController.cs:511`、`StationInteractor.cs:143` 的注释都强调过） |
| 谁打开 | `StationInteractor.cs:543`（传送分支 `:532-550`）→ `Teleporter.开面板()`（`:198`），按键 **F**（`Teleporter.交互键覆盖 = KeyCode.F`，`:179`）；关闭 = ESC 或 F（`StationInteractor.cs:37`/`:158`）或走出范围（`:168-172`），或执行传送时先关（`Teleporter.cs:350`） |
| Canvas | `传送面板`，Overlay，**`sortingOrder = 500`**（世界提示层之上、常驻 HUD 之下），1920×1080（**未设 `matchWidthOrHeight`**，`:269-276`） |
| 字体 | **借场上已有 `Text` 的字体**（`Resources.FindObjectsOfTypeAll<Text>()` 里第一个，`:265-267`）—— 换皮时要留意这条隐式依赖 |

```
传送面板                        [Canvas 500]
├─ 底板                      居中 pos=(0,40)，**460 × (120 + 56×选项数)**，色 0.06,0.05,0.07,0.92   (:278-285)
│  ├─ 文本                   anchor(0,1)-(1,1) pivot(0.5,1) pos=(0,-14) (−24)×96，**26 号** UpperCenter，色 0.95,0.92,0.8   (:287-296)
│  └─ 按钮_<名称> ×N          anchor(0.5,0) pivot(0.5,0) **400×46**，pos=(0, 18 + 56×(N-1-i))    (:302-308)
│                            可点 → 0.18,0.32,0.22；灰 → 0.22,0.2,0.22 且 `interactable=false`；字 24 号（灰 0.55,0.53,0.55）   (:309-323)
```
**图**：全纯色（`typeof(Image)` 2 处）；`sprite` / `Image.Type` / `fillAmount` **各 0 处**。**所有 Image / Text 的 `raycastTarget` 全文未赋值**（走 Unity 引擎默认）—— 它不用 `UIBuildUtils`，所以没有那条 `false` 约定。

**两个要注意的点**：
- 「等级不够就置灰」是**死路径**：`Teleporter.cs:90`/`:119` 的注释说由 `塔准入` 写入 `需要等级` 并调 `刷新准入()`，但**仓库里根本没有 `塔准入.cs`**，也**没有任何代码调 `刷新准入()`**（只有 `:121` 的定义）。场景里三个 `Teleporter` 的 `需要等级` 都是 **0** ⇒ 准入恒通过。
- 塔里的那台（`TowerController.cs:495` 会 `重建面板()`）**没有 `DontDestroyOnLoad`**，切场景即销毁。

---

## 15. 纪年 HUD `纪年HUD`

| 项 | 值 |
|---|---|
| 谁创建 | `场景自举.确保组件<纪年HUD>(相机, 场景名)`（`场景自举.cs:247`）⇒ 挂**主相机**，**除主菜单 / 过场字幕外的所有场景都常驻**（`场景自举.cs:26`、`:47-48` 跳过 `StartScene` 与 `Transition subtitles`）；没有编辑器生成器 |
| Canvas | `ChronicleCanvas`，Overlay，**`sortingOrder = 1520`**，1920×1080 match 0.5；`GraphicRaycaster.enabled = false`（`纪年HUD.cs:154-169`） |
| 订阅 | `OnEnable/OnDisable` 成对订阅 `时间管理器.过了一天`（`:75-87`，**:64-74 记录了「订阅重复 3 次」的坑**） |

```
ChronicleCanvas                 [Canvas 1520, 不吃射线]
└─ 右上行                     anchor(1,1) pivot(1,1) **460×70**，pos=(-20,-18)，VerticalLayoutGroup(spacing 4, UpperRight)   (纪年HUD.cs:172-184, 边距 :36)
   ├─ 日期「太虚历 X 年 Y 月 Z 日 · 时辰」  **22 号** UpperRight，460×30，色 0.96,0.94,0.86，+ Outline 黑 α0.75   (:186-189, :33/:39)
   └─ 机会「修炼机会 n/N 今日已用 n」      **18 号**（= 字号-4）UpperRight，460×26，色 0.72,0.90,0.98，+ Outline   (:191-194, :42)
```
跨天高亮：日期文字改 `跨天色` 1,0.90,0.42，持续 `跨天高亮秒 = 2.5`（`:45`, `:49`, `:106-112`）。
**图**：**没有任何 Image** —— 纯文字（`CreateText` ×2），靠描边保证可读性。

---

## 16. 素材接口清单

> 列的含义：「像素尺寸」是**建议交付尺寸**（当前界面 1 UI px = 1920×1080 参考分辨率下的 1 屏幕 px；标注 `2×` 表示按 2 倍像素密度出图以便高分屏与九宫格缩放）。九宫格 border 写 `左/下/右/上`。「fill」= 该图会被喂 `fillAmount`。「异形命中」= 视觉透明区不该响应点击，需要 alpha 命中或多边形 `ICanvasRaycastFilter`。
> `纯色` 一律指 `sprite == null` 的 `Image`（含运行时 `Sprite.Create` 的 4×4 纯白图）。按钮素材规格与边框见 `ui-rework-2026-10-03/BUTTON-KIT.md`（三档 520×157、border 120/28/120/28）。
> **条目数：55 条** = 全局 10 条（§16.1）+ 按面板 45 条（§16.2）。按表格列统计：**建议九宫格 41 条**、**需要 `fillAmount` 4 条**（HUD 冷却遮罩、全局进度填充、角色面板境界条、修炼页进度条）、**需要异形命中 24 条**。

### 16.1 全局（改一次全工程受益）

| 面板 | 节点 | 当前画法 | 建议替换成 | 像素尺寸 | 九宫格 border | fill | 异形命中 |
|---|---|---|---|---|---|---|---|
| 全局 | 所有按钮底（`UIBuildUtils.CreateButton` + 各面板自建按钮，合计数十处） | 纯色 + `ColorTint` 四态 | `ivory-` / `jade-` / `cinnabar-` 各 4 态（normal/hover/pressed/disabled），`Button.Transition = SpriteSwap` | 520×157（沿用素材包） | **是** 120/28/120/28 | 否 | **是**（两端尖角透明区） |
| 全局 | 全屏幕布（`Dim` / `幕布` / `炼丹幕布` / `暗色底` / `黑幕` 等十余处） | 纯色半透明 | 不用换图，**只换颜色** | — | 否 | 否 | 否 |
| 全局 | 面板底板（角色面板 `Window` 1440×860、修炼 `主面板` 1120×660、丹房 `内容框` 1560×880、功德堂 900×640、灵田 500×570 / 720×620、各 `CreatePanel` 子板） | 纯色 | 纸面板 / 玉简面板九宫格（可用 `transparent-page-skeleton-v1/parent-curtain-9slice-source.png` 作源） | 建议 1024×1024 源（含 2× 细节） | **是**（建议 96/96/96/96，按内纹位置定） | 否 | 否 |
| 全局 | 列表行 `Row`（34 高，`UIEntryList`） | 纯色 0.80×3 | 行底九宫格 ×3 态（常态 / 悬停 / 选中） | 512×68（2×） | **是** 32/8/32/8 | 否 | 否 |
| 全局 | `Swatch` 品阶色块（26×34；战阵 12×(高−20)） | 纯色（颜色来自 `QualityTier` / `NpcKind` 数据） | **保留纯色**（它承载玩法信息，不要换成图） | — | 否 | 否 | 否 |
| 全局 | 滚动条 `Scrollbar` 轨道（宽 18） | 纯色黑 α0.18 | 细滑轨九宫格（可用 `transparent-ui-v1/scrollbar-track-and-thumb.png`） | 32×64 | **是** 8/8/8/8 | 否 | 否 |
| 全局 | 滚动条 `Handle` | 纯色 0.78,0.52,0.33 α0.95 | 滑块九宫格，带 3 态 | 32×32 | **是** 12/12/12/12 | 否 | 否 |
| 全局 | 图标位：`UIEntryInfo/Icon` 120×120、六格 `Icon` 72×72、`预览方块` 320×150、HUD `功法图标` 58×58、HUD 技能图标 46×46、外观/坐骑列表行（无图标位） | 有 `DisplayIcon` 用数据图；**无图时用品阶色纯色顶** | 物品 / 神通 / 法宝 / 灵阵 / 功法 / 坐骑 图标集（数据侧 `DisplayIcon`） | 128×128（2× 出 256×256） | 否 | 否 | 否 |
| 全局 | 进度 / 冷却填充（HUD 气血·灵力·修炼、HUD 冷却遮罩、角色面板 `RealmShow/Fill`、修炼页 `进度填充`） | 运行时 4×4 纯白 sprite + `fillAmount` | **必须保留 sprite**；建议做一段可平铺的条纹理 | 8×32（源） | 否 | **是**（HUD 冷却 = 竖；气血/灵力/修炼/修为/境界 = 横） | 否 |
| 全局 | 进度条底 / 条底（HUD `底`、`Track`、`条底`） | 纯色 | 凹槽九宫格 | 64×32 | **是** 10/10/10/10 | 否 | 否 |

### 16.2 按面板

| 面板 | 节点 | 当前画法 | 建议替换成 | 像素尺寸 | 九宫格 border | fill | 异形命中 |
|---|---|---|---|---|---|---|---|
| HUD | `功法/底`、`Skill1-6/底`（68×68 / 46×46） | 纯色 + `hud_square` 4×4 白图 | 方形玉符底框 ×2 态（常态 / 悬停） | 136×136（2×） | **是** 20/20/20/20 | 否 | 是（若做切角） |
| HUD | `Skill*/灰罩`、`Skill*/冷却遮罩` | 纯色 + 白图；遮罩 **Filled/Vertical/fillOrigin Top** | 冷却遮罩建议做**竖向渐变**的方图（不改 fill 参数） | 92×92 | 否 | **是**（竖） | 否 |
| HUD | `信息幕布` + `顶线`（400×215，顶线高 2）+ `分隔线`（高 1） | 纯色 | 幕布底九宫格（纸卷）+ 云纹分隔线/顶线 | 幕布 512×280（2×）；分隔线 512×8 | 幕布 **是** 24/16/24/16 | 否 | 否 |
| 角色面板 | `Window`（1440×860）、`Sidebar`（170 宽）、`HintBar`、各 `CreatePanel` 底板、`ItemDesc` | 纯色 | 主窗口大框 + 侧栏竖条 + 提示条 + 内容板（分级：外框 / 内板） | 外框 1024×1024 源；侧栏 256×1024；提示条 1024×72 | **是**（外框 96，内板 48） | 否 | 否 |
| 角色面板 | `Tab_背包…Tab_外观`（54 高 × 8） | 纯色 + `ColorTint`，选中改 `background.color` | 页签 ×2 态（未选 / 选中），**注意选中态是代码改色**，换图要么改成 `SpriteSwap` 要么保留 tint | 480×108（2×） | **是** 24/8/24/8 | 否 | 是（尖角页签） |
| 角色面板 | `ActiveSkillBar/Slot_0-5`（84×84 六边形排布） | 纯色 `ColorSlot`（空位/待装备两态由代码改色） | 玉符异形底框（**六边形环**，对应 `ui-rework-2026-10-03/screen-studies/skills-assets-v1/03-active-ring.png`、`10-15-active-slot-*.png`） | 168×168（2×） | 否（异形建议整图） | 否 | **是**（这是全工程最需要异形命中的地方） |
| 角色面板 | `ActiveSkillBar/Slot/Clear`「×」（24×24）+ 九宫 `Clear`（24×24） | 纯色按钮 | 小叉按钮（紧凑实例，端部 14–18px） | 64×64 | **是** 16/16/16/16 | 否 | 是 |
| 角色面板 | `SpiritFormationBar/Slot_0-8`（104×104，间距 14） | 纯色 + `Swatch` 竖色条（12 宽） | 九宫棋盘格底（矩形可保留；异形重点在「格」的纸边） | 208×208（2×） | **是** 20/20/20/20 | 否 | 否 |
| 角色面板 | `RealmShow/Track`（高 16）+ `Fill`（Filled 横） | 纯色；**`Fill` 的 sprite 是 null ⇒ 现在多半根本没显示填充**（见 §3.3 警告） | 墨线进度条（底槽九宫格 + 填充段 sprite，换上即可修好不显示的问题） | 底 512×32；填充 32×32 | 底 **是** 12/8/12/8 | **是**（横） | 否 |
| 角色面板 | `MountShow/Preview`、`AppearanceShow/Preview`（RawImage + RenderTexture） | **不是 Sprite** | 不需要 2D 素材；但**框**（`MountShow` / `AppearanceShow` 板）需要画框 sprite | 框 1024×640 | **是** 64/64/64/64 | 否 | 否 |
| 角色面板 | `Info/Icon`（120×120，无图时品阶色） | 纯色占位 | 图标位底板（玉牌/木牌） + 真图标 | 240×240（2×） | **是** 24/24/24/24 | 否 | 是（玉牌异形） |
| 角色面板 | 拖拽影子 `DragGhost`（200×40） | 纯色（品阶色） | 抬起的玉简/纸签（带阴影） | 400×80 | **是** 24/8/24/8 | 否 | 否 |
| 角色面板 | 属性列表行 `Row_<属性名>`（高 26，`UIAttributeList`） | 纯色（`Name` 左 / `Value` 右，18 号） | 细行底线或 zebra 条纹（**不要给每行加框**，属性有十几行） | 512×52（2×） | **是** 8/4/8/4 | 否 | 否 |
| 角色面板 | 外观页行 `行_<id>`（高 56；`底` + `名字` 26 号 + `装备` 按钮 100×40） | 纯色（行底 0.86×3；按钮 0.95,0.78,0.35 / 已装备 0.55×3） | 外观行底九宫格 + 装备/已装备两态按钮 | 行底 1024×112；按钮 520×157（或 200×80 紧凑实例） | **是** 行 24/8/24/8 | 否 | 按钮是 |
| 修炼页 | `主面板`、`页签_*`（176×64 ×3）、`功法信息`/`突破所需物品信息`/`当前学会的功法`/`转修后境界预估`、`功法行` 6×（46 高） | 纯色 | 大面板框 + 三页签 ×2 态 + 内容板 + 可点行 ×3 态 | 大框 1024×1024；页签 352×128；行 1024×92 | **是**（大框 96 / 页签 24 / 行 16/8/16/8） | 否 | 页签是 |
| 修炼页 | `进度底`（896×34）+ `进度填充`（Filled 横，4×4 白图） | 纯色 + 白图 | 灵气进度槽 + 金/绿填充段 | 底 1024×68；填充 32×64 | 底 **是** 16/16/16/16 | **是**（横） | 否 |
| 修炼页 | 按钮：`修炼一次`/`闭关`（190×60）、`确认突破`（300×62）、`确认转修`（240×62） | 纯色 + `ColorTint` | 三档按钮（主要动作 = jade，仪式动作 = cinnabar） | 520×157 | **是** 120/28/120/28 | 否 | 是 |
| 丹房 | `炼丹幕布`（=面板本体，全屏）、`内容框`（1560×880）、`成品预览`/`信息板`/`丹方板`/`背包板`（板） | 纯色 | 丹房大框 + 三级内板（成品 / 信息 / 列表） | 大框 1024×1024；内板 512×384 | **是**（大框 96，内板 40） | 否 | 否 |
| 丹房 | `预览方块`（320×150，成功/失败改色）、`主材格`/`辅材格0-3`（200×132，选中改色） | 纯色（**颜色是状态语义**） | 材料格底 ×4 态（空 / 选中 / 已放料 / 不可用）；保留换色能力 | 400×264（2×） | **是** 32/32/32/32 | 否 | 是（可选切角） |
| 丹房 | `开炼`（380×170，底 0.84,0.17,0.14）、`清空`/`关闭`（按钮） | 纯色 | 主行动按钮用 **cinnabar**（朱砂）；次级用 **ivory** | 520×157 | **是** 120/28/120/28 | 否 | 是 |
| 丹房 | 丹方/背包列表行（`丹方按钮`、`背包按钮`，VerticalLayoutGroup spacing 6） | 纯色（选中改色 0.52,0.44,0.26） | 行底九宫格 ×3 态 | 720×64（2×） | **是** 24/8/24/8 | 否 | 否 |
| 丹房 | `视口`（348×338）+ `RectMask2D` | 无图 | 可选：列表内阴影/纸纹（不要用 sprite 做遮罩） | — | 否 | 否 | 否 |
| 灵田 | `面板`（500×570）、`动作区` | 纯色 | 地块信息卡（**异形纸签**，对应 `ui-rework-2026-10-03` 里「地块信息卡」重点） | 1000×1140（2×） | **是** 48/48/48/48 | 否 | **是**（纸签尖角） |
| 灵田 | `种子_*` 行（30 高，选中改强调色）、`主按钮`（44 高）、`挪动地块`/`升级田地`/`关闭`（42 高） | 纯色 | 种子行 ×3 态（可选/不可选/已选）；主行动按钮 jade | 种子行 1024×60；按钮 520×157（小的高 42 用紧凑实例） | **是** 行 16/8/16/8 | 否 | 按钮是 |
| 灵田 | `灵田总览/面板`（720×620）、`一键收取`/`关闭` | 纯色 | 总览板 + 按钮 | 1024×1024 / 520×157 | **是** | 否 | 按钮是 |
| 功德堂 | `功德堂`（900×640）、`行*`（46 高）、`兑换*`（150×42）、`关闭`（220×46） | 纯色（够/不够改色） | 兑换行（含价格列）+ 可点/禁用按钮两态 | 板 1024×1024；行 1024×92 | **是** | 否 | 按钮是 |
| 任务追踪 | `主线追踪`（400 宽，自适应高，底色 0.05,0.06,0.08,0.74） | 纯色 | 追�踪牌（纸牌九宫格，右上角对齐，高度自适应 ⇒ **必须九宫格**） | 512×256（2×） | **是** 24/24/24/24 | 否 | 否 |
| 任务追踪 | `感叹号`（「！」文字 52×52）、`边缘箭头`（44×44，**运行时生成的 64×64 金三角**） | 文字 / 运行时纹理 | 金色「！」印章 + 箭头 sprite（可复用同一套金色） | 128×128 / 128×128 | 否 | 否 | 是（箭头） |
| 对话 | `人物立绘` / `玩家立绘`（左 29.8% 宽、右 22.6% 宽，高 74.7%；无色图时是黄块 `#EDE66E`） | **纯色占位**（`Resources/立绘/` 目录下无任何 PNG） | NPC 立绘 / 玩家立绘 PNG，**按对话表 `立绘` / `玩家立绘` 列的 id 命名** | 建议 **1024×1408**（对齐 0.298×1920 ≈ 572 × 0.747×1080 ≈ 807，2× ≈ 1144×1614） | 否 | 否 | 是（透明区不该挡点击，现在 raycastTarget 已是 false） |
| 对话 | `对话根` / `对话框`（13.2%–68.4% 宽 × 0–27.7% 高）、`名字底`（240×56） | 纯色（对话框 0.62×3 α0.92） | 纸卷对话框九宫格（**必须九宫格**：宽度随分辨率变）+ 名牌 | 对话 1024×512；名牌 480×112 | **是** 对话 48/32/48/32；名牌 24/16/24/16 | 否 | 否 |
| 对话 | `回答1/回答2/…`（74 高，3 态：常态 0.80×3 α0.96 / hover 0.95,0.92,0.70 / pressed 0.7,0.68,0.55） | 纯色 | 选项按钮九宫格 ×3 态 | 720×148（2×） | **是** 32/16/32/16 | 否 | 是 |
| 对话 | `泡`（情绪气泡，运行时 64×64 圆点） | 运行时纹理 | 情绪气泡（问/叹/怒/喜） | 128×128 | 否 | 否 | 是 |
| 暂停 | `暗色底`（全屏）、`标题`「暂停」84 号、`按钮_*`（230×52 ×4） | 纯色 | 暗底保留纯色；标题加纸匾（`transparent-page-skeleton-v1/title-plaque.png`）；按钮三档 | 匾 1024×240；按钮 520×157 | 是（匾 48/48/48/48） | 否 | 按钮是 |
| 主菜单 | `Background` | **已有 sprite** `Assets/Resources/UI/MainMenu/begin_ui.png`（`Image.Type.Simple`，`preserveAspect=false`，**7.2 MB**） | 按三套皮肤各出一张 16:9 背景；**建议同时压缩尺寸** | 1920×1080（1×） | 否 | 否 | 否 |
| 主菜单 | `Title`「修仙」150 号、`Buttons/Btn*`（240 宽列，spacing 18，28 号）、`SavePanel`（760×780）、`BtnClose`（52×52）、`Slots` 行 | 纯色 + 描边 | 游戏标题匾（书法）+ 按钮三档 + 存档板 + 槽位行 ×2 态 | 匾 1024×400；按钮 520×157；板 1024×1024 | 是 | 否 | 按钮是 |
| 身陨 | `幕布`、`标题`（64 号）、`副标题`（30 号）、`重生按钮`（210×84，亮黄 `#FFEB1A` + 黑字 34 号） | 纯色 | 暗幕保留；标题用朱砂印章风；重生按钮 = cinnabar | 按钮 520×157 | 是 120/28/120/28 | 否 | 是 |
| 起名 | `黑幕`（全屏纯黑 α1）、`标题`（44 号）、`输入`（64 号，1400×80）、`光标`（40×74）、`提示`（22 号） | 只有黑幕一张纯色 Image，其余纯 Text | 黑幕保留；标题建议配「题名」装饰线或纸签；输入行可加一条下划墨线 | 装饰线 1400×16 | 否 | 否 | 否 |
| 灵田（世界空间，对照项） | `灵田地块牌/底` + `标题`（25 号 Bold）+ `状态`（21 号） | 纯色（成熟 0.16,0.11,0.02,0.95 / 未成熟 0.05,0.06,0.08,0.95）+ 黑描边 1.6px；WorldSpace Canvas `sortingOrder = 210`，480×92、`localScale = 0.004` | 异形纸签（地块信息牌） | 960×184（2×） | **是** 40/24/40/24 | 否 | **是**（纸签尖角；目前整块矩形都能被射线打到） |
| 黑幕字幕 | `黑幕`（全屏纯黑）、`白闪`（全屏纯白 α0） | 纯色（**`fillAmount` 不用**，只动 alpha） | 保留纯色；若要加纸纹/墨晕再叠一层纸纹图（透明度由代码控） | 若加纸纹：1920×1080 平铺 | 否 | 否 | 否 |
| 黑幕字幕 | `字幕`（44 号文字，区域 0.12–0.88 × 0.35–0.65） | 无图（纯 Text） | 可选：竖排/题跋装饰线 | — | 否 | 否 | 否 |
| Toast | `底`（340×52，色 0.06,0.07,0.10 α0.86）、`文字`（22 号） | 纯色 | 提示条九宫格（**必须九宫格**：文字长度会变，但当前尺寸是固定的 340×52） | 680×104（2×） | **是** 24/16/24/16 | 否 | 否 |
| 镇妖塔 | `塔层HUD`（900×40，24 号，顶部居中）、`死亡选择` 幕布 + `标题`（40 号）+ 按钮排（340 宽，78 高 ×2）+ `倒计时`（28 号） | 纯色 + 描边 | 塔层条（暗色横条九宫格） + 死亡面板 + 按钮三档 | 塔层条 1800×80；按钮 520×157 | 是 | 否 | 按钮是 |
| 传送 | `底板`（460×(120+56n)）、`文本`（26 号）、`按钮_*`（400×46 ×N） | 纯色（按 `选项数` 动态改高度 ⇒ **必须九宫格**） | 传送选项板（异形纸签/玉牒）+ 按钮两态 | 板 1024×1024；按钮 520×157 | **是** 板 48/48/48/48 | 否 | 板可异形 |
| 纪年 | `右上行` / `日期` / `机会` | **没有 Image**（纯 Text + Outline） | 若要底板，加一块右上小纸牌九宫格 | 520×100 | 是 20/20/20/20 | 否 | 否 |
| 设施提示（对照项） | `StationHint/底`、`StationHint/键`（世界空间 Canvas 230×56，scale 0.011） | **已有程序生成圆角九宫格 sprite**（32×32、border 12/12/12/12，`StationInteractor.cs:496-515`） | 这是全工程**唯一已经用上九宫格**的自建 UI；换皮时替换成正式圆角/纸牌素材 | 64×64 | **是** 12/12/12/12（现） | 否 | 否 |

---

## 17. 换皮工作量排序与未确认项

### 17.1 换皮工作量最大的面板（按「纯色 `Image` 节点数」排序）

统计口径：脚本里 `CreateImage` / `AddComponent<Image>` / `typeof(Image)` 三类建图语句的出现次数（粗略但可复现）。

| 名次 | 面板 / 脚本 | 建图语句 | 说明 |
|---|---|---|---|
| 1 | **角色面板** `CharacterPanelBuilder.cs` | 13（+ 19 处建文字） | 8 个页 × 5~6 块纯色板 + 六格 + 九宫格 + 55 个子节点 —— **换皮工作量最大**，且它把节点序列化进了 **5 个场景**，改一次要重建 5 次 |
| 2 | **修炼页** `CultivationUI.cs` | 12 | 3 页 + 页签 + 2 处 `fillAmount` 条；结构简单但页数多 |
| 3 | **通用件** `UIBuildUtils.cs` | 6 | 一处改、全场生效（性价比最高） |
| 4 | **丹房** `炼丹界面.cs` | 5（+ 10 建文字） | 材料格 / 丹方行 / 背包行都是纯色且靠改色表达状态 |
| 5 | **对话** `DialogueUI.cs` | 3 | 节点少但**立绘位完全没有素材**（`Resources/立绘/` 空目录） |
| 5 | **黑幕字幕** `黑幕字幕.cs` | 3（黑幕 / 白闪 / 字幕） | 结构最简，且不使用 `fillAmount` |
| 5 | **身陨** `DeathScreenUI.cs` | 3 | 一个按钮 + 两行字 |
| 5 | **功德堂** `功德堂兑换.cs` | 3 | 行数随数据变 |
| 5 | **灵田地块界面** `灵田地块界面.cs` | 3 | 面板 + 幕布 + 动态动作区 |
| 5 | **镇妖塔** `TowerUI.cs` | 3 | HUD + 死亡面板 |
| 10 | **暂停 / 主菜单 / 灵田总览 / 传送 / Toast / 纪年 / 任务追踪** | 0~2 | 改动量小；`纪年HUD` 与 `任务追踪` 甚至没有一张图 |

**结论**：**最依赖纯色 `Image` 的是 ① 角色面板（含 8 页 + 六格 + 九宫格）② 修炼页 ③ 丹房**。这三处同时决定了「通用件」（按钮、列表行、面板框、滚动条）的观感 —— 先把 `UIBuildUtils.cs` + 按钮素材包接上，再动 `CharacterPanelBuilder`，收益最大、返工最少。**第二梯队**是对话（缺立绘素材，属"没有素材"而非"纯色画法"）与灵田地块界面（异形纸签）。**最省力的**是纪年 HUD 与任务追踪面板（纯文字，换字体/配色即可）。

### 17.2 换皮时必须知道的八条硬约束

1. **`fillAmount` 必须有 sprite**（`PlayerHud.cs:126-129`、`CultivationUI.cs:89`）。把进度条 / 冷却遮罩换成无 sprite 的装饰图会**直接失去填充功能**；反过来，角色面板 `RealmShow/Fill` 现在**正因为没有 sprite 而大概率不显示填充**（`CharacterPanelBuilder.cs:535`）。
2. **`CreateImage` 默认 `raycastTarget = false`**（`UIBuildUtils.cs:38`），但 `AddComponent<Image>` 与 `CreateButton` 的内建图**默认 true**。换图时若顺手换了创建方式，务必确认可点底图仍然吃射线（全工程至少 14 处注释记着这条）。
3. **角色面板有 5 份场景副本**（`CharacterUI` 在 5 个游玩场景里各一份），生成器是幂等的但**要跑 5 次**（`CharacterPanelBuilder.cs:38-43`）；`暂停菜单` 同样 5 份，由 `SceneRigSyncer` 维护。
4. **`sortingOrder` 是有语义的**（§1.3）。尤其别把面板调到 1500/1520 以下，也别把常驻 HUD 调到 1800 以上 —— 这两条各有过一次用户实测 bug（`任务引导.cs:515-523`、`纪年HUD.cs:158-161`）。
5. **修炼页 / 丹房是"被实例化 = 就是要显示"**，`Awake` 里不能隐藏（`CultivationUI.cs:109-112`、`炼丹界面.cs:102-103`）。
6. **列表行是运行时建的、带 `HideFlags.DontSave`**（`UIEntryList.cs:476`）。换皮改行样式只改 `UIEntryList.AddRow` 一处即可，不必碰场景（`炼丹界面`、`功德堂`、`外观页` 各有自己的一套行，要分别改）。
7. **`LayoutElement` 的高度只有在 `childControlHeight = true` 时才生效**。镇妖塔死亡面板就是反例：`VerticalLayoutGroup.childControlHeight = false`（`TowerUI.cs:245`）让那句 `LayoutElement 78` 白写（`:262-264`），按钮高度落到引擎默认值。给这种位置换图前先把高度钉死。
8. **字体是隐式的**：`传送面板` 借「场上第一个 `Text` 的 `font`」（`Teleporter.cs:265-267`）；`DialogueUI` 只有 `LegacyRuntime.ttf` 兜底（`DialogueUI.cs:598-602`）；其余面板都在 `Awake` 里找 `SimHei` 或 `<含 simhei>`。换字体时每一处都要单独确认。

### 17.3 未确认清单（查了哪里，但没定论）

| # | 未确认的事 | 已查到什么 / 查了哪里 |
|---|---|---|
| 1 | **`Assets/Prefabs/CultivationUI.prefab` 是哪个生成器产出的** | `Assets/Editor/Builders/` 与 `Assets/Editor/Tools/` 下**没有**生成它的脚本；`设施界面接线.cs:15/19/107` 只在注释里提到它。预制体很可能早期手工搭的。预制体被场景引用的证据是 **fileID 相同**（`182561180186654261` 同时出现在 `CultivationUI.prefab:3` 与 `3C_Testbed.scene:35267`） |
| 2 | **`.meta` 长格式 guid ↔ YAML 32 位 guid 的映射** | 本仓库 `.meta` 里是 44 / 56 字符的长格式，而场景写 32 位 hex（`docs/architecture/修炼与境界.md:68-81` 说「是同一根 guid 的两种写法」）。全工程多处身份判定都靠 **fileID 相同** 或**字段名吻合**，**没做官方解码验证**（`Library/` 下按 guid 搜也零命中） |
| 3 | **角色面板的境界进度条到底显不显示** | 代码事实：`Fill` 的 sprite 为 null 且 `Type = Filled`（`CharacterPanelBuilder.cs:535`/`:562`），而 `PlayerHud.cs:126-143` 与 `CultivationUI.cs:89` 都明确补了 1×1 白图兜底、本面板**没有**。⇒ 推断不显示，但**未实机验证** |
| 4 | **`塔准入.cs` 不存在**（传送「等级不够置灰」是死路径） | `Teleporter.cs:90`/`:119` 注释称由它写 `需要等级` 并调 `刷新准入()`，但全工程无该文件、**无人调 `刷新准入()`**（只有 `:121` 定义）；场景里三个 `Teleporter` 的 `需要等级` 都是 0 |
| 5 | **`CharacterTab` 枚举只有 7 项，UI 有 8 个页签** | 枚举 `CharacterPanelUI.cs:11-19`（坐骑 = 6），而 `tabNames`（builder `:123`）与 `pages`（`:150-157`）都是 8 项，「外观」靠 `(CharacterTab)7` 越界值工作（`:154-159`）。是否有别处按枚举长度判断**未确认** |
| 6 | **`Sect.scene` 里「炼丹阁」那栋楼的对象名** | 该 `StationInteractable` 是**被剥离的 prefab instance**（`Sect.scene` 中 `界面预制体` 指向 `Assets/Prefabs/炼丹界面.prefab`），源 prefab guid 在 `.meta` 里 grep 不到 ⇒ 只能确认它是「类型=炼丹、显示名=炼丹、交互距离=11、按键 F」的那台设施 |
| 7 | **`DialogueUI` 的中文到底显示成不成方块** | 代码事实：`字体` 字段全仓库**没有一处赋值**，`取字体()` 只有 `LegacyRuntime.ttf` 兜底（`DialogueUI.cs:598-602`）。但没实测 `LegacyRuntime.ttf` 是否含中文字形 ⇒ **不下结论** |
| 8 | **`Assets/Data/Generated/GongFaDefinition/` 有 4 门功法，修炼页预制体只列 2 门** | 预制体 `全部功法` 只有 `太虚炼气诀`（难度 100）与 `青云剑诀`（难度 260）两门；另两门（`沧澜寒渊录` / `玄霄雷决`）为何没列，**代码里查不到原因**（属数据口径） |
| 9 | **修炼页「内容区」比 `w` 宽 12px** | `内容区` 实宽 908（`CultivationUI.cs:444-448`），而页面内 `w = 896`（`:462-463`）。当前不影响观感（各块按 `w` 定宽），但**是不是有意为之未确认** |
| 10 | **纪年 HUD 与 Toast 是否真的不重叠** | `任务引导` 注释写「让开 Toast ≈144」（`任务引导.cs:60-61`），而 Toast 默认第 1 条底边 = `顶边距 104 + 高 52 = 156`（`ToastUI.cs:26`/`:39`）⇒ 可能压住追踪面板标题 6px，**未截图验证** |
| 11 | **`Assets/resources/` 目录全小写的可移植性** | `立绘/`、`UI/MainMenu/` 都在小写 `resources` 下（`Resources.Load` 在 Windows 编辑器下不暴露大小写问题），**打包 / 跨平台是否安全未验证** |
| 12 | **`黑幕字幕.每行等点击` 是否死字段** | 全仓库 grep 只命中声明行（`黑幕字幕.cs:49`），无读取处；未做反射级验证 |
| 13 | **`黑幕字幕` 的「字幕渲染在白闪之上」是否有意为之** | 从 `SetSiblingIndex` 与 `建字` 的调用顺序可推出渲染次序，但**没有实机截图**确认观感 |
| 14 | **三套皮肤与现有色板的映射** | 外部方案只给了基础色（`ui-rework-2026-10-03/README.md:27`：纸 `#EEE9DC`、正文 `#303D37`、玉绿 `#526F5C`、朱砂 `#9E4E3D`、哑金 `#9C8355`）与按钮状态表；`UIBuildUtils` 现有 10 个常量与它们的**一一对应关系尚未定义**，需要美术 / 策划拍板 |
| 15 | **`灵田地块牌` 的逐行复核** | 本文件的数值（480×92、`localScale = 0.004`、`sortingOrder = 210`、字号 25 / 21、描边 1.6px）来自对 `灵田地块牌.cs` 的盘点记录，**本轮没有逐行复读该文件**；它也不在任务要求的面板清单内 |
| 16 | **场地内 3D 预览的实际画面** | 坐骑页与外观页的相机 / 灯光 / RenderTexture 参数都已确认，但**没有实机截图**确认取景、比例与背景是否合适 |
