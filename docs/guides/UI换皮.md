# UI 水墨换皮（InkUI）

> **管什么**：外部交付的 UI 素材（`ui-rework-2026-10-03/`）**怎么进工程、怎么设导入参数、怎么在运行时接到已有的 uGUI 上**；素材命名与九宫格 border 的对应关系；换皮的开关与回退。
> **不管什么**：每个面板现在长什么样、由谁生成、锚点尺寸 —— 看 [UI现状原理图](../architecture/UI现状原理图.md)；素材本身要什么规格看 `ui-rework-2026-10-03/素材规格与提示词.md`；按钮素材的切图细节看 `ui-rework-2026-10-03/BUTTON-KIT.md`。
> **本文件怎么查**：§1 素材放哪、§2 导入规则、§3 运行时怎么接、§4 节点名→素材映射表、§5 现状与没做的。

---

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
79 张、约 53 MB（源 PNG）。**大图在导入时压 `CompressedHQ`（BC7）**，小件（图标/按钮/槽位/徽章/滚动条）留不压缩，免得纸纹被压出色带。

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
**不动布局与数据**。分组开关：`换面板底 / 换页签 / 换列表行 / 换按钮 / 换进度条 / 换滚动条 / 换字色`，总开关 `启用`。

> **字色**：纸底上原来的金/白字看不清 ⇒ 把"偏亮的字"统一改成墨色 `#303D37` 并关掉黑 `Outline`（只改亮字，深色字不动）。

## 4. 节点名 → 素材映射（第一版覆盖到的）

| 节点名（生成器里的中文串） | 素材 | 类型 |
|---|---|---|
| `Window` | `SkillsPage/common/panel-sheet` | Sliced ×2 |
| `Sidebar` | `SkillsPage/navigation/nav-rail` | Sliced ×2 |
| `Tab_背包` … `Tab_外观` | `SkillsPage/navigation/tab-normal` | Sliced ×2 |
| `ActiveSkillBar` | `SkillsPage/skills/active-ring` | Simple（**整图、中央透明**） |
| `Slot_0`…`Slot_5` 及其 `Icon` 子物体 | `SkillsPage/skills/slot-empty` | Simple（异形） |
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

## 5. 现状与没做的

**已生效（2026-10-03 实测，Sect）**：面板底 22 / 页签 8 / 列表行 45 / 按钮 69 / 进度条 2 / 滚动条 3 / 改字色 18；
截图 `screenshots/换皮_神通页4.png`。

**没做**：
1. **布局**：① 六槽环的编号 1–6 与"主动技能栏"小字；② `Info` 现在是**右下 0.44 高**，规格要**右侧通高**；
   ③ `KnownList` 是 **34 高列表**，规格要**卡片网格**（要改生成器）；
2. **选中态**：页签选中/列表选中现在**全都一样**（我把 `Tab_*` 统一换成了 `tab-normal`；原来靠改颜色区分，
   换成 sprite 后被覆盖）⇒ 需要把"当前页/选中行"的 sprite 换成 `tab-active` / `row-selected`（要接一次 `CharacterPanelUI` 的切页回调）；
3. **HUD**（左下功法印章 + 六枚小玉符 + 血/灵条）没换（它不在 `CharacterUI` 下）；
4. `修炼页 / 丹房 / 对话 / 灵田 / 功德堂 / 塔 / 传送 / Toast` 都没接（素材已就位，映射照 §4 扩表即可）。
