# InkUI 全套 UI 重构 · 施工单（给执行方）

> **一句话任务**：把游戏里**所有界面**换成 `ui-rework-2026-10-03/` 这套水墨 UI（素材已齐），
> **并保证每个界面的原有功能、数据与交互一个都不能丢**，改完交前后对照截图。
> 工程根：`D:\project：cultivation`，Unity 工程在子目录 `cultivation/`（团结 Tuanjie 2022.3 / Built-in RP）。

---

## 1. 先读这些（按顺序，别跳）

| # | 文档 | 读它为了什么 |
|---|---|---|
| 1 | `docs/INDEX.md` | 全局地图：什么问题该翻哪篇 |
| 2 | `docs/architecture/系统总览.md` | 三条生产线、`场景自举`、`SceneRigSyncer` |
| 3 | **`docs/architecture/UI现状原理图.md`** | **最重要**：每个面板**由谁生成**、运行时层级、锚点/尺寸/字号/颜色、用了什么图、谁开关、`sortingOrder`；**§16 是 55 条素材接口清单** |
| 4 | **`docs/guides/UI换皮.md`** | 素材在哪、导入规则、运行时怎么接、**节点名 → 素材映射表**、三个坑 |
| 5 | `ui-rework-2026-10-03/素材规格与提示词.md`、`提示词-第二轮.md` | 素材规格、还缺哪三类图 |
| 6 | `ui-rework-2026-10-03/README.md`、`BUTTON-KIT.md` | 三套皮肤、按钮四态与切图 |
| 7 | 各面板的功能口径（**换皮不许改这些**）：`architecture/游戏HUD.md`、`design/交互系统.md`、`design/对话系统.md`、`architecture/战斗与伤害.md`、`architecture/主动技能与神通.md`、`guides/炼丹.md`、`guides/灵田.md`、`guides/宗门贡献与兑换.md`、`guides/镇妖塔.md`、`guides/外观系统说明.md`、`architecture/坐骑与御风.md`、`design/存档系统.md` | 每页"功能上必须保住什么" |
| 8 | `docs/ai/踩坑总库.md`（**A8** 保存场景写脏、**B93** 非序列化容器为 null、**§H** 画面/shader）+ `docs/ai/工作规范.md` | 纪律与坑 |
| 9 | `docs/PROGRESS.md`（最后几行） | 最近状态：**UI 换皮已经起头了**，别推倒重来 |

---

## 2. 素材：在哪、怎么导、怎么用

```
cultivation/Assets/resources/UI/InkUI/        ← ⚠️ 必须是 resources（全小写）才能 Resources.Load
├─ Buttons/            3 材质(素纸/玉绿/朱砂) × 4 态，520×157
├─ SkillsPage/         神通页：common / navigation / skills / status
├─ CultivationAlchemy/ 丹房 + 修炼页（丹炉、材料格四态、墨环、功法卡、丹方行）
├─ CommonPanels/       灵田纸签、对话纸卷/名牌/选项、Toast、塔层条、传送板、题匾
├─ Icons/              菜单八项 256×256
├─ Portraits/          8 张对话立绘 1144×1614
├─ Parts/              active-slot-frame、ability-library-card、enabled-passive-row、导航页签、主/次按钮
├─ Skeleton/           parent-curtain-9slice-source（含山水的大幕布）、左导航立板、nav-tab-active/inactive、ornate-divider、title-plaque
└─ BigPieces/          left-parent-navigation-panel、learned-ability-library-panel、ability-detail-panel
```

- **导入**：菜单 `修仙 / UI / 导入 InkUI 素材（Sprite + 九宫格）`（`Assets/Editor/Tools/InkUI素材导入.cs`）。
  已设：`Sprite (2D and UI)` + `Single` + **`FullRect`** + PPU 100 + Center 枢轴 + `AlphaIsTransparency` + 无 mip + Clamp；
  大图 `CompressedHQ`、小件不压缩。**九宫格 border 取各包 `manifest.json` 的 `nineSlice` × 2**（素材 2× 交付）。
  ⚠️ 另外三包（`Parts/Skeleton/BigPieces`）的 manifest **只有裁剪框、没有 border**，工具里是手填的保守值，**需要目视微调**。
- **用法两条**：① 九宫格图 `Image.type = Sliced` + **`pixelsPerUnitMultiplier = 2`**；按钮包是 1× 交付 ⇒ 用 **2.6**。
  ② `bar-fill` 之类的**填充段必须保持 `Image.type = Filled`**，换成 Simple 就没填充了。
- **还缺三类**（等出图，见 `提示词-第二轮.md`）：**神通大图** `art-ability-*.png`、**神通方形图标** `icon-ability-*.png`、面板内衬山水。
  没到位期间：大图框**留空但不要摆占位图**；图标位继续用数据侧 `DisplayIcon`，**不许改数据表**。

---

## 3. 两条红线（违反任何一条都算没做完）

**红线 1：不要为了换皮去"重新生成面板 + 保存场景"。**
角色面板等节点是 `CharacterPanelBuilder` 在编辑器里生成、**序列化进 5 个游玩场景**的；
重新生成 + 存场景会把运行时 UI 列表（`Row_*` 的坐标）一起重排 —— **踩坑 A8：一次写脏 +19787 行**。
- **首先考虑运行时换**：由 `场景自举` 补组件（`Assets/Scripts/UI/UIInkSkin.cs` 已经这么做，`UI神通页重排.cs` 是布局那一半）。
- **确实要改生成器**（例如把列表改成卡片网格）：改完必须 `修仙/同步通用角色与UI到所有游玩场景`，
  然后 **`git diff --numstat` 逐个场景核对**，只保留预期改动，多出来的行**一律还原**并在汇报里说明原因。

**红线 2：不许把"没实现的功能"画成能用，不许自己发明玩法/数值/文案。**
未实现的（如法宝/灵阵的实际效果）要在界面上**明确写"待开发"**；数值一律读现有数据，不新增编造的奖励或规则。

---

## 4. 施工顺序

| 阶段 | 内容 | 为什么这个顺序 |
|---|---|---|
| ① 共享组件 | 按钮（四态 `SpriteSwap`）、面板底九宫格、行底三态、页签三态、滚动条、进度条（**顺手修好 `RealmShow/Fill` 因为 `sprite=null` 而不显示填充**） | 一次改完全工程受益，风险最低 |
| ② HUD | `PlayerHud`（功法印章 + 六枚小玉符 + 气血/灵气墨线 + 冷却竖向遮罩） | 常驻可见，玩家最先注意到 |
| ③ 角色面板 8 页 | **神通页优先**（原型图已定稿：`screen-studies/skills-scroll-passives-v2.png`）→ 背包 / 境界 / 法宝 / 灵阵 / 战阵 / 坐骑 / 外观 | 主界面 |
| ④ 修炼页 + 丹房 | 素材在 `CultivationAlchemy` | 玩法最重 |
| ⑤ 其余 | 灵田（地块纸签 + 总览）、功德堂、对话、镇妖塔、传送、Toast、暂停/主菜单/身陨/起名 | 复用 ① 的组件 |
| ⑥ 收尾 | 选中态/悬停/禁用、`timeScale`、`sortingOrder` 复核、**1920×1080 与 1280×720 双分辨率**验收 | 见 §7 |

---

## 5. 逐面板：换皮不许动的功能口径

| 面板 | 必须保住的功能 / 数据入口 | 特别注意 |
|---|---|---|
| **HUD** | `PlayerHud` / `HudHoverTarget`；冷却**竖向填充**（`Filled/Vertical/Top`）与"最后一秒一位小数"；空槽不出提示卡 | 异形底板要用**有 sprite** 的 Image，否则 fill 不生效 |
| **背包** | 走 `UIEntryList` / `UIEntryInfo` / `UIPanelData`；单击选中→右侧详情；使用后数量同步 | 物品图标**没有就用数据里的**，别用颜色当唯一区分 |
| **境界/属性** | 属性列表左、当前功法右上、境界进度条右下；境界条修好填充 | 只展示，不改修炼逻辑 |
| **神通** | `ActiveSkillBar` 六槽（0–5，快捷键 1–6 **固定**）；拖拽装备走 `EquipToSlot`（**拖入占用位会覆盖**是现状）；**被动不能拖进主动槽**；被动启停走 `UIPanelData` 单一入口（御风 ⇄ 坐骑互斥在那里） | 六槽顺序顺时针 1–6 且**编号常显**；拖拽视觉单独一层，别让它参与 mask 裁剪判定 |
| **法宝 / 灵阵** | 共用同一组六槽；各自资源库与详情；未实现的效果**明说待开发** | 同步共享装备，别做两份状态 |
| **战阵** | 九宫保持方向、中间固定玩家；**同时最多 5 名**；名单 240 个真灵要**虚拟化/对象池** | 不旋转棋盘改变方向理解 |
| **坐骑 / 外观** | 复用真 3D 预览（`RawImage` + `RenderTexture` + 隔离相机 + 自带光）；`UnscaledTime` + `AlwaysAnimate`；御风互斥提示 | 预览对象要**去掉碰撞与 AI**；保留视角切换 |
| **修炼页** | 真实修为/概率/可用次数；转修展示"预估转修后境界"（**不在 UI 重算公式**） | 三页签：闭关修炼 / 境界突破 / 转修功法 |
| **丹房** | 开炉与**材料扣除在 `炼丹炉` 里执行**；概率/产量/消耗来自丹方与被动；主材 1 + 辅材 4；点配方自动摆料、手摆要汇总匹配；失败也扣材料 | 材料格**颜色是状态语义**（空/选中/已放料/不可用），换图后要保留换色能力 |
| **灵田** | 状态机 待种植→生长中→成熟；种植/收获/挪动/升阶按状态开放；**挪动回世界预览**（R 转 45°、左键确认、右键/ESC 取消）；合法性走现有 `摆放校验` | 纸签**异形**，透明区不该响应点击 |
| **功德堂** | 贡献余额与"兑换后余额"；**原子扣贡献**（不够不扣）；贡献只来自任务 | 兑换价读物品表 `兑换消耗贡献` |
| **对话** | 逐句演出、点继续；`UiEscRegistry` 防同帧 ESC 穿透；立绘按对话表 `立绘` 列 id | 结果面板开着时 `timeScale=0`（协程等待要 `Realtime`） |
| **任务追踪 / 纪年 / 设施提示** | 追踪面板右上、单目标突出、边缘箭头；纪年右上；F 提示贴设施 | 世界空间的提示牌 `sortingOrder ≤ 500` |
| **暂停 / 主菜单 / 身陨 / 起名** | 存档/复活调用既有系统；**不新增奖励** | 身陨"重生"= 朱砂档；标题只出匾，字由 Text 渲染 |
| **镇妖塔 / 传送** | 塔层 HUD、清完怪出现的传送点（**本层清过就一直留着**）；死亡面板"出塔 / 继续深入·下一层"；传送板高度随选项数变 ⇒ **必须九宫格** | 传送的"等级不够置灰"目前是**死路径**（`塔准入.cs` 已删、无人调 `刷新准入()`），要么接上要么明确注释 |

---

## 6. 每个面板怎么验收（照抄就能复现）

```csharp
// 在 Play 里（或 MCP exec_runtime_script）
var ui = GameObject.Find("CharacterUI");              // 各面板的根节点名见 UI现状原理图 §1
Object.FindObjectOfType<CharacterPanelUI>().SetOpen(true);   // 也可以直接在游戏里按 I
yield return new WaitForSecondsRealtime(0.4f);         // ⚠️ 面板一开 Time.timeScale = 0
                                                      //    必须用 Realtime，否则协程永不返回（踩过：超时 400s）
foreach (var t in ui.GetComponentsInChildren<Transform>(true))
    if (t.name == "Tab_神通") t.GetComponent<Button>().onClick.Invoke();
yield return new WaitForSecondsRealtime(0.5f);
ui.GetComponent<UIInkSkin>().刷新();                    // 换图 + 重排
yield return new WaitForSecondsRealtime(0.5f);
ScreenCapture.CaptureScreenshot("screenshots/verify.png");   // Overlay UI **只能** ScreenCapture，
yield return new WaitForEndOfFrame();                        // 相机渲到 RT 拍不到 Overlay
```

**判据**：截图里能看到新皮肤，**且**功能还能用（选中/拖拽/开关/数值都试一遍）。
**交付**：每个面板 **1920×1080 与 1280×720 各一张**"改前/改后"，命名 `UI_<面板>_前/后_<分辨率>.png`，放 `cultivation/screenshots/`（不入库）。**没截图 = 没做完。**

---

## 7. 横切坑清单（逐条检查）

| 坑 | 症状 | 正确做法 |
|---|---|---|
| 底板 `Image.color` 是深色 | 换了 sprite 看着像没换（纸白 × 0.13 灰） | 换图时**把 color 重置成白**；只有"用颜色当状态"的（行/页签选中）才保留 |
| 改 `Image.type` | 进度条不显示了 | `bar-fill` 保持 `Filled`，其余按需 `Sliced`/`Simple` |
| 九宫格 border 填太大 | 图片错位/拉伸怪 | `L+R ≥ 宽` 会坏；用 `InkUI素材导入` 的自动夹取 + 告警 |
| 素材不是 `FullRect` | 九宫格/异形边缘被裁 | 导入工具已设 `FullRect`；手改过要检查 |
| 2× 素材当 1× 用 | 边框巨粗 / 面板巨大 | `pixelsPerUnitMultiplier = 2`（按钮 2.6） |
| 面板开着 `timeScale=0` | 测试脚本卡死 | 一律 `WaitForSecondsRealtime` |
| Overlay UI 截不到 | 截图全黑/没 UI | 用 `ScreenCapture`，别用相机 RT |
| 异形命中 | 透明角也能点到 | 需要 `alphaHitTestMinimumThreshold`（要开 Read/Write）或 `ICanvasRaycastFilter` |
| `sortingOrder` | 面板被 HUD 盖 / 提示压对话 | 它是一张**有语义的表**（面板 ≥1800、常驻 HUD ≤1520、世界提示 ≤500；角色面板运行时被抬到 **2450**）——**最不能动** |
| 重跑生成器 + 存场景 | 5 个场景被写脏 | 见 §3 红线 1 |
| 新增序列化字段 | 场景里已有实例上是 null | 见踩坑 **B93**：容器类字段要 `确保容器()` 兜底 |

---

## 8. 交付物与汇报格式

1. **改动清单**：代码文件、资产目录、文档；
2. **每个面板的改前/改后截图**（两种分辨率，路径见 §6）；
3. **文档更新**：至少更新 `docs/architecture/UI现状原理图.md`（改成"新皮肤下的现状"）与 `docs/guides/UI换皮.md`（映射表扩到全工程）；
   跑 `.dsh/verify.ps1 -FixBom`，**DEAD / malformed / 控制字符 / BOM / H1 / 导航头 六项必须都是 0**；
4. **未完成项与风险**（诚实列，不许把"没做"写成"已做"）；
5. **提交**：本地 commit 即可，**不要 push**（用户会单独下指令）。

> 汇报时请按"**我改了什么 → 我量到什么 → 我看到什么**"三段写：**数字 + 截图**，不要只写"已完成"。
