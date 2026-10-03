# docs/ 索引 —— 查文档从这里开始

> **管什么**：整个 `docs/` 的入口 —— 目录结构、**"我要做 X 就看哪份"** 的查找表、开工铁律、以及**文档怎么写**。
> **不管什么**：具体内容都在各文档里，这一页只做导航，不重复正文。
> **本文件怎么查**：§0 开工三步｜§0.1 零号铁律（不知道就去 grep）｜§0.2 场景/地点对照｜§1 文档地图｜**§2 按任务找文档（主查找表）**｜§3 常见事实｜§4 文档怎么写
> **来源**：本轮重写（旧版 36 行的任务表保留并补全）。

---

## 0. 开工三步（每台电脑都要做）

```powershell
git rev-parse --show-toplevel      # 1) 仓库根
where.exe git                      # 2) git 在哪（免安装，随机器变）
git pull                           # 3) 先拉最新
```

- **工程根** = `<仓库根>\cultivation\`（文档一律用相对路径，不写死盘符）
- 引擎 **团结 Tuanjie 2022.3.61t14**（Unity 2022.3 系），**Built-in 渲染管线**，**无 asmdef**
- **换新环境永远 `git clone`，不要下载 zip** —— zip 没有 `.git` 历史，
  重新 `init` 出来的提交与远端没有共同祖先，push 会把整个项目重传一遍

### ⚠️ 0.1 零号铁律：**不知道某个东西在哪，先 `grep` 文档，不要猜**

> **"新建一个"是最贵的错误答案。**

踩过的实例：要做「洞府后山灵田」，没查文档，凭"项目里没有洞府"的印象，
**先把灵田错放到宗门主广场**，接着**差点新建一个 `Cave.scene`**。
实际上 **`3C_Testbed` 就是个人洞府** —— [design/主线剧情](design/主线剧情.md) 第五幕写得清清楚楚。

**动手前的固定动作**：

```powershell
# 一次搜全（这是该用的命令，范围别缩）
rg -n "<关键词>" docs cultivation/Assets/Data/Tables
```

**固定搜索范围**（缺一个都可能漏）：

| 范围 | 里面有什么 |
|---|---|
| `docs/design/` | 策划原文与设计（**冲突时以它为准**） |
| `docs/architecture/` | 各系统"现在长什么样" |
| `docs/guides/` | 某个机制怎么做 |
| `docs/ai/踩坑总库.md` | 所有坑（按 A~H 主题 + 专题分段编号） |
| `docs/ai/archive/工作日志-*.md` | ★ **最容易漏** —— 大量"有意为之"的决定只写在这里 |
| `cultivation/Assets/Data/Tables/*.csv` | 任务/对话/物品/境界表的实际内容 |

> **`工作日志` 最有信息量却最容易被漏。** 很多"某样东西为什么长这样"只写在工作日志里，
> 代码和 `design/` 都查不到。`git log` 的 commit message 里也常有。

**判据**：一个"地方"在策划文档里被提到、而你以为它不存在 —— **九成是你没找，不是它没有。**

### 0.2 场景 / 地点对照表（★ 改"地点相关"的东西前先看）

| 策划里的叫法 | 场景文件 | 备注 |
|---|---|---|
| **古古镇** | `village` | 开场、野猪、村民 |
| **太虚宗（宗门）** | `Sect` | 主广场 `碰撞地板(平坦)` 在 `(0,-1,0)` 附近（106×91）；主角出生/复活点 = `(22.069, -0.685, -3.794)`（= 任务表 `q_main_004` 阶段4 的落点，在主广场地板上）；**功德堂那栋楼（`environment_Building_luoxiaguan_001_d`）是兑换处** —— 上面的 `功德堂兑换` 组件是**运行时补的**，场景里没有（见 [guides/宗门贡献与兑换](guides/宗门贡献与兑换.md)） |
| **宗门野外** | `Sect_Wilderness` | 400×400 森林 |
| **个人洞府 / 专属洞府 / 洞府后山** | **`3C_Testbed`** | 入口 `sect1 to 3c`；内有 `cultivation room`、**可摆放物**（**灵田地块 / 练功木桩**，都是**玩家自己摆**、都要存档；场景里没有预置物件、也没有点位）；洞府那一幕的大师兄**不在场景里** —— 他是**任务系统在他不在场时就地补出来**的（见 [design/主线剧情 §6.1](design/主线剧情.md)）。⚠️ 本场景里序列化着一份运行时 UI 列表，**保存它会连带重排**（实测一次保存带出 1541 行无关改动），别为摆一个 NPC 去存它 |
| **镇妖塔** | `Demon-Suppressing Tower` | 1000 层 |
| 主菜单 | `StartScene` | |
| 过场字幕 | `Transition subtitles` | |

**`Sect` 里的可互动楼**（用途只写在 [归档工作日志](ai/archive/工作日志-主线开发-第五轮起.md) 与提交 `e3ef6dc5` 里）：

| 场景物体名 | 名字 | 类型 | 用途 | 能用吗 |
|---|---|---|---|---|
| `environment_Building_qingsangcheng_001_o` | 炼丹阁 | `炼丹` | 炼丹 | ✅ 挂了 `炼丹界面.prefab` |
| `environment_building_minju_003_a` | 炼器阁 | `炼器` | 炼器 | ❌ 空（法宝系统未做） |
| `environment_building_qinghuacifangwu_02_a` | 阵法阁 | `布阵` | 布阵 | ❌ 空（无灵阵界面组件） |
| `environment_Building_diaojiaolou_001_h` | 执事阁 | `修炼`⚠️占位 | 悬赏任务 | ❌ 待开发 |
| `environment_Building_luoxiaguan_001_b` | 传法阁 | `修炼`⚠️占位 | 兑换神通/功法 | ❌ 待开发 |
| `environment_Building_luoxiaguan_001_d` | 功德堂 | `修炼`⚠️占位 | 兑换修炼物品 | ❌ 待开发 |
| `environment_Building_luoxiaguan_001_h` | 宗门大殿 | `修炼`⚠️占位 | 任务互动/讲课 | ❌ 待开发 |

> ⚠️ **最后 4 座的 `类型=修炼` 是占位值，它们不是修炼建筑！**
> **别给它们挂 `CultivationUI`**（那会让"在执事阁按 F 弹出修炼面板"）。
> 要做先给 `StationInteractable.StationKind` 补类型。
> 它们开着 `显示靠近提示` 是**正常的**（原设计想用 `现在可交互=false` 关，但那是
> `[NonSerialized]`、场景里存不住）。
>
> **修炼**只有一处：`3C_Testbed` 的 `cultivation room`。

详细场景事实与全局约定见 **[architecture/系统总览](architecture/系统总览.md)**。

---

## 1. 文档地图（先认结构）

**分工原则：一份主题 = 一份文档，文件名直接说出主题。**

| 想知道 | 去哪 |
|---|---|
| 这个系统**现在**怎么运作 | `architecture/` |
| 策划**要求**它怎样 | `design/`（**权威**，冲突时以它为准） |
| 某个操作**怎么做** | `guides/` |
| 某张表**有哪些列** | `reference/` |
| 踩过什么**坑** | `ai/踩坑总库.md` |
| **怎么干活**（流程 / 编码 / 验证） | `ai/工作规范.md` |
| 某天**发生了什么** | `ai/archive/`（**只读**） |

```
docs/
├─ INDEX.md            ← 本文件（入口）
├─ PROGRESS.md         ★ 需求 ←→ 实现 总进度（四阶段）
│
├─ architecture/       ★ 各系统"现在长什么样"（改系统前必读）
│   ├─ 系统总览.md         项目基本盘 · 场景地图 · 全局机制 · 系统一览
│   ├─ 战斗与伤害.md        伤害公式 · 受伤飘字
│   ├─ 修炼与境界.md        修炼小屋 · 功法等级换算 · 破境
│   ├─ 主动技能与神通.md     技能槽 · 施放流程 · 特效摆放
│   ├─ 游戏HUD.md          左下角 HUD · 冷却表现 · 悬停幕布 · 修炼次数进度条
│   ├─ 敌人AI.md           无寻路的接近模型 · 各只怪的实现
│   ├─ 战阵真灵.md          站位 · 真灵列表
│   ├─ 坐骑与御风.md        摆位 · 对地高度 · Shift 切换 · 互斥
│   ├─ 传送系统.md          `Teleporter` 与切场景流程
│   └─ 遮挡与特效开关.md     遮挡描边 · 建筑/树冠透明 · 按场景开关表
│
├─ design/             策划原文与设计（★ 冲突时以它为准）
│   ├─ 主线剧情.md          剧情设计（谁在哪一幕做了什么）
│   ├─ 任务系统.md          任务/阶段/条件/动作/标记/触发区
│   ├─ 对话系统.md          对话表 · 选段 · 对话 UI
│   ├─ 境界与功法设计.md     境界层级 · 灵气曲线 · 难度系数 · 功法转换
│   ├─ 物品系统.md · 存档系统.md · 交互系统.md
│
├─ guides/             某个机制怎么做（按主题一份）
│   ├─ 灵田.md · 时间系统.md · 炼丹.md · 任务引导.md · 宗门贡献与兑换.md · 日常循环.md
│   ├─ 飞弹与子弹.md · 怪物近战判定.md · 特效系统.md · 特效资源清单.md
│   ├─ 镇妖塔.md · 村庄场景生成说明.md · 宗门野外生成说明.md
│   ├─ 玄霄雷决与雷动千闪.md · 千劫雷狱.md · 瞬雷天闪.md · 雷云.md · 闪电链特效.md
│   ├─ 沧澜寒渊录.md · 寒墟.md · 冰暴术.md
│   ├─ 画面统一调色.md · 外观系统说明.md · 动画竖直处理.md
│   └─ 备份与恢复说明.md
│
├─ reference/          配置表字段.md（每张 CSV 的列与生成目标）· 外部素材来源.md（外部素材的授权 / 出处 / sha256）
│
└─ ai/
    ├─ 工作规范.md      ★ "在这个项目里怎么干活"（含 §0 文档怎么写）
    ├─ 踩坑总库.md      ★ 唯一的坑库（A~H 主题 + 专题 + 硬性规矩）
    ├─ 工程与工具链.md   环境事实 · MCP/文件桥 · 常用命令 · 编辑器菜单
    └─ archive/        ★ 历史流水（原样保留，**不要改**）
```

---

## 2. 按任务找文档

| 我要做的事 | 先读 | 再读 |
|---|---|---|
| **★ 总进度：需求 ←→ 实现对照** | [PROGRESS](PROGRESS.md) | — |
| **改主线剧情** | [design/主线剧情](design/主线剧情.md)（剧情设计） | [design/任务系统](design/任务系统.md) · [reference/配置表字段](reference/配置表字段.md) |
| **加/改任务阶段、条件、动作、标记** | [design/任务系统](design/任务系统.md) | [reference/配置表字段](reference/配置表字段.md) · [ai/踩坑总库 §A/C](ai/踩坑总库.md) |
| **左侧主线追踪 / 头顶感叹号 / 屏幕边缘箭头**（`任务引导`） | [guides/任务引导](guides/任务引导.md) | [design/任务系统](design/任务系统.md) · [reference/配置表字段 §4.3](reference/配置表字段.md) |
| **宗门贡献 / 功德堂兑换 / 给物品定价（上架·改价·下架）** | [guides/宗门贡献与兑换](guides/宗门贡献与兑换.md) | [design/物品系统 §3.2](design/物品系统.md) · [reference/配置表字段 §6](reference/配置表字段.md) · [design/存档系统 §3](design/存档系统.md) |
| **每日清单（今日四件事，无常驻 UI）** | [guides/日常循环](guides/日常循环.md) | [guides/时间系统](guides/时间系统.md) · [design/存档系统 §3](design/存档系统.md) · [design/任务系统](design/任务系统.md) |
| **对话 / 对话表 / 对话 UI** | [design/对话系统](design/对话系统.md) | [ai/踩坑总库 §C](ai/踩坑总库.md) |
| **加/改物品、加可使用效果** | [design/物品系统](design/物品系统.md) | [reference/配置表字段](reference/配置表字段.md) |
| **存档相关（加要存的东西）** | [design/存档系统](design/存档系统.md) | `Assets/Scripts/Save/SaveData.cs`（字段即清单）· [ai/踩坑总库 §G](ai/踩坑总库.md) |
| **境界 / 功法 / 破境 / 灵气数值** | [design/境界与功法设计](design/境界与功法设计.md)（权威） | [architecture/修炼与境界](architecture/修炼与境界.md) |
| **修炼小屋 / 转修 / 功法等级换算** | [architecture/修炼与境界](architecture/修炼与境界.md) | [design/境界与功法设计](design/境界与功法设计.md) |
| **洞府灵田 / 灵植 / 可摆放物（自由摆放 · 开拓令 · 练功木桩 · 走近看信息牌 · 按 F 种·收·挪·升级）** | [guides/灵田](guides/灵田.md) | [guides/炼丹](guides/炼丹.md) · [reference/外部素材来源](reference/外部素材来源.md) |
| **在洞府里摆东西 / 摆放与吸附规则（占用边距 · SAT · 8 朝向 · 跨种类互斥）** | [guides/灵田 §4](guides/灵田.md) | [PROGRESS §2.2](PROGRESS.md) |
| **摆练功木桩 / 加一种新的可摆放物** | [guides/灵田 §4.5~4.6](guides/灵田.md) | [design/存档系统](design/存档系统.md) · [ai/踩坑总库 §B](ai/踩坑总库.md) |
| **外部素材（作物模型 / 土质贴图）怎么拉、怎么转** | [tools/参考素材/README.md](../tools/参考素材/README.md) | [reference/外部素材来源](reference/外部素材来源.md) |
| **外部素材的授权 / 出处 / sha256（★ 必读授权状况）** | [reference/外部素材来源](reference/外部素材来源.md) | [guides/灵田 §7](guides/灵田.md) |
| **时间 / 纪年 / 修炼机会** | [guides/时间系统](guides/时间系统.md) | [architecture/修炼与境界](architecture/修炼与境界.md) |
| **炼丹 / 丹方 / 品（= 丹药对应境界）/ 服丹提破境成功率** | [guides/炼丹](guides/炼丹.md) | `Assets/Scripts/Alchemy/炼丹炉.cs` |
| **伤害公式 / 属性 / 受伤飘字** | [architecture/战斗与伤害](architecture/战斗与伤害.md) | `Assets/Scripts/Combat/CombatCalculator.cs` |
| **主动技能 / 神通 / 特效怎么摆** | [architecture/主动技能与神通](architecture/主动技能与神通.md) | [guides/特效系统](guides/特效系统.md) |
| **左下角 HUD / 冷却显示 / 悬停提示 / 修炼进度条** | [architecture/游戏HUD](architecture/游戏HUD.md) | [architecture/主动技能与神通](architecture/主动技能与神通.md) · [guides/时间系统](guides/时间系统.md) |
| **加一个 NPC 的 AI** | [architecture/敌人AI](architecture/敌人AI.md) | [ai/踩坑总库 §专题：NPC/敌人 AI](ai/踩坑总库.md) |
| **怪物近战 / 出手站位** | [guides/怪物近战判定](guides/怪物近战判定.md) | [architecture/敌人AI](architecture/敌人AI.md) |
| **飞弹 / 子弹 / 出生点** | [guides/飞弹与子弹](guides/飞弹与子弹.md) | [guides/特效资源清单](guides/特效资源清单.md) |
| **战阵 / 真灵** | [architecture/战阵真灵](architecture/战阵真灵.md) | [ai/踩坑总库](ai/踩坑总库.md) |
| **坐骑 / 御风 / 飞行高度** | [architecture/坐骑与御风](architecture/坐骑与御风.md) | [guides/动画竖直处理](guides/动画竖直处理.md) |
| **屏幕空间角色描边（Built-in）** | [guides/描边](guides/描边.md) | [guides/画面统一调色](guides/画面统一调色.md) · [architecture/游戏HUD](architecture/游戏HUD.md) |
| **UI 水墨换皮（InkUI 素材怎么进工程 / 运行时怎么接）** | [guides/UI换皮](guides/UI换皮.md) | [architecture/UI现状原理图](architecture/UI现状原理图.md) · `ui-rework-2026-10-03/素材规格与提示词.md` |
| **镇妖塔 / 刷怪 / 关卡难度** | [guides/镇妖塔](guides/镇妖塔.md) | [ai/踩坑总库 §H](ai/踩坑总库.md) |
| **传送门 / 落点 / 切场景** | [architecture/传送系统](architecture/传送系统.md) | [design/任务系统 §8](design/任务系统.md) · [ai/踩坑总库 §B](ai/踩坑总库.md) |
| **加可交互物 / 「按 F 打开」的界面** | [architecture/系统总览 §2.5](architecture/系统总览.md) · [design/交互系统](design/交互系统.md) | [ai/踩坑总库](ai/踩坑总库.md) |
| **改了 UI / 角色 / 相机要同步到所有场景** | [architecture/系统总览 §2.6](architecture/系统总览.md)（`SceneRigSyncer`） | [ai/踩坑总库](ai/踩坑总库.md) |
| **编辑场景时 UI 挡视线 / F1 面板默认开** | [architecture/系统总览 §2.7](architecture/系统总览.md)（`UICanvasBoot`） | [architecture/游戏HUD](architecture/游戏HUD.md) |
| **鼠标选中/锁定敌人 · 神识范围** | [design/交互系统](design/交互系统.md) | [ai/踩坑总库 §B](ai/踩坑总库.md) |
| **树冠/建筑挡住视线、镜头看不见主角** | [architecture/遮挡与特效开关](architecture/遮挡与特效开关.md) | — |
| **场景生成 / 摆点（村庄）** | [guides/村庄场景生成说明](guides/村庄场景生成说明.md) | [architecture/系统总览 §1](architecture/系统总览.md) |
| **场景生成 / 摆点（宗门野外）** | [guides/宗门野外生成说明](guides/宗门野外生成说明.md) | [architecture/系统总览 §1](architecture/系统总览.md) |
| **画面色调 / 场景昏暗 / 材质不统一 / 后处理** | [guides/画面统一调色](guides/画面统一调色.md) | [architecture/系统总览 §2.2~2.3](architecture/系统总览.md) |
| **外观 / 换装** | [guides/外观系统说明](guides/外观系统说明.md) | — |
| **特效放哪 / 加新特效包怎么整理** | [guides/特效系统](guides/特效系统.md) | [guides/特效资源清单](guides/特效资源清单.md) |
| **找某个特效的路径** | [guides/特效资源清单](guides/特效资源清单.md)（自动生成） | — |
| **电系功法「玄霄雷决」+ 雷动千闪** | [guides/玄霄雷决与雷动千闪](guides/玄霄雷决与雷动千闪.md) | [guides/瞬雷天闪](guides/瞬雷天闪.md) · [guides/千劫雷狱](guides/千劫雷狱.md) · [guides/雷云](guides/雷云.md) · [guides/闪电链特效](guides/闪电链特效.md) |
| **冰系功法「沧澜寒渊录」+ 寒墟 / 冰暴术** | [guides/沧澜寒渊录](guides/沧澜寒渊录.md) | [guides/寒墟](guides/寒墟.md) · [guides/冰暴术](guides/冰暴术.md) |
| **动画上下晃 / 人悬空** | [guides/动画竖直处理](guides/动画竖直处理.md) | [ai/踩坑总库 §B](ai/踩坑总库.md) |
| **要动编辑器工具 / MCP 桥** | [ai/工程与工具链](ai/工程与工具链.md) | [ai/工作规范](ai/工作规范.md) |
| **★ 先看看别人踩过什么坑** | [ai/踩坑总库](ai/踩坑总库.md) | — |
| **★ 干活流程 / 编码 / 验证规矩** | [ai/工作规范](ai/工作规范.md) | — |
| **出事故 / 要回滚** | [guides/备份与恢复说明](guides/备份与恢复说明.md) | — |
| **查某天到底改了什么** | [ai/archive/](ai/archive/) 下的工作日志 | `git log` |

---

## 3. 几条会被反复问到的事实

| 问题 | 答案 |
|---|---|
| 渲染管线？ | **Built-in**（`m_CustomRenderPipeline = null`），无 URP/HDRP，无 PPSv2 |
| UI 用什么？ | uGUI 传统 `Text` + **SimHei**，**项目没导 TMP** |
| 文件编码？ | `.cs` / `.csv` **必须 UTF-8 with BOM**；`.shader` 的 **properties 块只能 ASCII**（连中文注释都不行） |
| 命名约定？ | 文件/类名 ASCII，**public 成员中文** |
| 通用组件谁挂？ | **`场景自举` 运行时补，不写进场景**（写进场景会漂移） |
| 后处理顺序？ | Bloom（order -100）→ Grade（调色）→ 屏幕 |
| 本机 shell？ | **Windows PowerShell 5.1** —— `-Encoding utf8BOM` 不存在；`Get-Content`/`Select-String` 读**无 BOM** 文件会按 ANSI 解码成乱码，读文件优先用 read 工具或加 `-Encoding UTF8` |
| 为什么有 `ai/archive/`？ | 那是**只读历史**；活文档在 `architecture/` / `design/` / `guides/` / `reference/` / `ai/` |

---

## 4. 文档怎么写（改文档前必读）

1. **一个文档只讲一个主题，文件名直接说出主题。**
   - 反例：`主线-任务与对话`（要打开才知道里面是任务系统 + 对话系统）、`灵田与时间系统`（两个系统挤一份）—— 这两份已按主题拆开。
   - 正例：`任务系统`、`对话系统`、`主线剧情`、`灵田`、`时间系统`。
2. **文档不是日志，不许有任何"日志式增加"。**
   - 不写"第一轮 / 第二轮 / 某天我又改了 / 用户反馈之后"这种**时间线叙事**；只写**现在是什么状态、为什么这么定、参数是多少、要注意什么**。
   - 不留从 6300 行流水日志继承来的章节号（`§八`、`十二、`、`## 26.`、`34.13`）。标题一律**按主题、在本文件内自洽**编号：`## 1. 主题` / `### 1.1`。
   - 日期只在"某个值被后来的决定取代了、需要说明先后"时才写，且只写在该处。
3. **每份文档顶部必须有统一导航头**（四行）：

   ```markdown
   > **管什么**：一句话说清覆盖范围
   > **不管什么**：哪些相关的事不在这里 + 该去看哪份文档（带相对链接）
   > **本文件怎么查**：本文件 3~8 个关键小节的一句话索引
   > **来源**：出处（历史流水一律进 `ai/archive/`，不在这里复述）
   ```

4. **做完一件事就更新文档**：机制写进 `architecture/` 或 `design/`，做法写进 `guides/`，表列写进 `reference/`，
   坑写进 `ai/踩坑总库.md`（**新坑要编号**），进度更新 `PROGRESS.md`，**并回本文件 §1/§2 登记**。
   新增 / 改名 / 删除文档都必须改这两张表，否则等于没写文档。
5. **瘦身优先于堆砌**：能一句话说清的不要写三句；能做成表格的不要写散文；重复内容只留一处 + 链接。
6. **新功能上线后跑一次场景一致性体检**（见 [ai/踩坑总库](ai/踩坑总库.md) 的「专题：场景一致性」）。
7. **别把通用组件写进场景** —— 放进 `场景自举`。
8. **别新建"另一个 X"** —— 先查文档有没有。见 §0.1。
9. **历史流水不许改** —— `ai/archive/` 只读。
