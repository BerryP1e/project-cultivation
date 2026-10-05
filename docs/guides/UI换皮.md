# UI 接入与维护

> **管什么**：已接入 UI 的文件位置、运行方式、验证方法与下一步入口。
> **不管什么**：业务规则见 [主动技能与神通](../architecture/主动技能与神通.md)、[战阵真灵](../architecture/战阵真灵.md)；设计原话见 [八页重制总纲](../design/UI重制/八页重制总纲.md)。
> **本文件怎么查**：目录归属 → 当前完成范围 → 操作与验证 → 后续接手。
> **来源**：当前 Assets 脚本与素材清点、Play 验证、用户确认的背包/境界/神通/战阵效果。

## 1. 文件放在哪里

所有下列路径从仓库根计算；Unity 工程是 cultivation/。文档仅放 docs/，截图和录像仅放 cultivation/screenshots/，不入库。

| 内容 | 位置 | 用途 |
|---|---|---|
| 运行时 UI 脚本 | cultivation/Assets/Scripts/UI/ | UIInkSkin、父导航、子页、共用动效及原业务控制器 |
| UI Shader | cultivation/Assets/Shaders/ | 墨密度/边缘/裁剪、神通与战阵预览 |
| Unity 导入与验收脚本 | cultivation/Assets/Editor/Tools/ | InkUI素材导入、InkUIQa、各子页 QA |
| 游戏实际读取的图片 | cultivation/Assets/resources/UI/InkUI/ | Resources.Load 的 UI/InkUI 根；保持原路径及 GUID |
| 制作原图、切图结果、原型、清单、旧素材包 | cultivation/Assets/UIResources/InkUI/Authoring/ | 制作来源，不在 Resources 中，不会仅因放在此处就进入游戏构建 |
| Node/PowerShell 制作与网页预览工具 | cultivation/Assets/Editor/Tools/InkUIAuthoring/ | 编辑器侧制作工具；Backups~/ 保存旧脚本副本且不编译 |
| 用户设计要求 | [design/UI重制/](../design/UI重制/) | 八页总纲及施工单；最新用户澄清见本页完成范围 |
| 素材规格与提示词 | [reference/UI素材/](../reference/UI素材/) | 素材规格与第二轮提示词 |
| 原始制作记录 | [ai/archive/UI重制/](../ai/archive/UI重制/) | 历史来源，只读；不能作为当前施工状态 |

原根目录 ui-rework-2026-10-03/ 的制作文件已迁入上述目录。314 个文件迁移前后逐件校验 SHA-256；另保留旧工具所用的42张原始生成输入，制作区合计248张PNG；映射在工具目录的 relocation-manifest.json。运行时资源不迁移，避免破坏 Resources 路径、序列化引用和九宫格设置。源素材与运行时成品分开，不要把源包直接覆盖运行时资源。

## 2. 当前完成范围

父页面仍包含背包、境界、神通、法宝、灵阵、战阵、坐骑、外观八个子页。丹房独立；闭关修炼、境界突破、转修功法属于另一个父页面。运行时接入复用原业务数据与开关，不重跑生成器、不保存场景。交互界面统一用 `UiEscRegistry.SceneInputBlocked` 阻断场景输入，保留 UI 滚动与拖拽；登记规则见 [游戏HUD §3](../architecture/游戏HUD.md)。

| 页面 | 已接入行为 | 边界与后续 |
|---|---|---|
| 父导航 | 无父底板，八枚独立墨点；持续流动、局部牵引、标签边缘晕开；点击墨晕保留 | 鼠标划过的方向贴图水痕已取消；I 打开隐藏血量、任务、纪年 HUD，关闭恢复原状态 |
| 背包 | 包裹开场后退场；物品瀑布流占满内容区；按数量调整尺寸，较圆的墨底，图标中间、文字靠下；墨边消隐、滚轮与滚动条；纯墨详情与墨点按钮；正常有限滚动，首尾不循环 | 复用原选择/使用回调；不得恢复宣纸详情底或常驻大包裹 |
| 境界 | 墨圈 0.5 秒慢起笔后连续加速；右侧圆弧属性对齐墨圈，整体左移；完整属性滚动与纯墨下栏 | 无中途停笔；属性位置按手绘图在右侧 |
| 神通 | 六槽立体轨道、悬浮/视差；双向拖拽装备/换位/卸下；跟手光核/粒子；完整滚动神通库；纯墨详情；启用被动在空白区随机占位、避让槽位与其他星点，悬停说明、点击选中 | 无独立“生效中的被动神通”栏；同一主动神通最多占一个槽，同 ID 的不同 SO 也去重；旧重复状态保留最前槽 |
| 战阵 | 独立水墨布阵底，透视九宫格与真实模型深度遮挡；左键左右拖动整块战阵环视，固定俯视角，滚轮缩放；右侧真灵为可滚动的柔边墨云、鼠标牵引、拖动拉伸；上阵落笔显形；下方纯墨信息与上/下阵 | 上下拖动不改视角；中央玩家格不可放真灵，最多 5 个；模型为烘焙姿态，尚非连续 idle 动画；不是战斗场景 |
| 外观 | 已拥有列表为柔和墨底；右侧同脸、朝左的两套半身立绘与独立可滚动介绍；墨迹换装按钮 | 外观跨场景与存档复核见 [外观系统说明](外观系统说明.md) |
| 法宝、灵阵、坐骑 | 保留基础换皮及原功能 | 尚未完成新的逐页互动重制；法宝/灵阵按用户要求暂缓 |
| HUD | 左下大境界墨圈、普通封闭细框血蓝条，保留短收尖与简洁端饰、末端亮点和淡出粒子；六技能共用神通页三维双环和连线，追加墨晕冷却；修炼次数仅境界页 | 血蓝条按最新黄框左移，取消枝杈式轮廓；机制和复验见[游戏HUD](../architecture/游戏HUD.md) |
| 对话 | 无立绘，姓名在正文框左上，墨边正文与居中回答；长回答滚动、背包同款滚动条、视口渐隐 | 原条件/跳转/任务回调保留；见[对话系统](../design/对话系统.md) |
| 传送 | 独立墨底、墨迹目的地按钮、滚动与渐隐、未开放/准入原因 | 不改塔内接管与跨场景过渡，见 [传送系统](../architecture/传送系统.md) |
| 丹房 | 卷竹简丹方与逐片展开、左右同源主辅材拖拽、独立炉身/炉盖、投料吸入/取消归位、持续炼制与出丹 | 保留业务规则，暂存投料、演出结束一次结算、成丹领取，见 [炼丹](炼丹.md) |
| 修炼、灵田及其他面板 | 基础换皮与共用动效已接 | 不代表八页重制与全套材质统一全部完成 |

滚动条统一复用背包样式。图标、底、边框、文字、按钮分件；运行时墨云和部分交互由网格/Shader 生成，不需要为每项重复制作贴图。共用动效用 unscaled 时间，暂停菜单有“减少动效”。

## 3. 怎么运行与验证

正常打开工程、进 Play、按 I 即可；场景自举补 UIInkSkin，由它接入父导航与 UIInkBagPage / UIInkRealmPage / UIInkSkillsPage / UIInkFormationPage。基准生成器布局只用于理解原节点关系，不代表当前运行时排版。

从仓库根使用已有文件桥：

~~~powershell
. './.dsh/uni.ps1'
Uni 'play:off' 20
Uni 'refresh' 30
# 等待编译完成后再进 Play；同时检查 Library/Bee/tundra.log.json 的编译结果。
Uni 'play:on' 25
Uni 'inkqa:seed' 20
Uni 'screen:1920x1080' 20
Uni 'inkqa:role:5' 20
Uni 'inkqa:formation:seed' 20
Uni 'inkqa:formation:validate' 30
Uni 'inkqa:validate' 30
Uni 'console:errors' 20
Uni 'play:off' 20
~~~

role 的页号为背包0、境界1、神通2、法宝3、灵阵4、战阵5、坐骑6、外观7。独立验证命令为 inkqa:bag:validate、inkqa:realm:validate、inkqa:skills:validate、inkqa:formation:validate。再以 1280x720 验证排版、滚动和拖拽；检查返回的 Failures，而不是只看脚本是否执行。

验收数据只在 Play 使用，不调用保存场景或存档。战阵 validate 会保存并恢复站位与选择；seed 刻意放入临时验收状态，退出 Play 恢复磁盘场景。截图命令 shot2:screenshots/文件名.png 异步保存，等保存完成后再切页。各页前后图及慢放录像留在 screenshots/。

已量测的双分辨率检查：背包22项、境界16项、神通43项、战阵26项；战阵实际真灵库231项，240项压力数据池只创建16个可见单元。具体校验以当前 QA 代码返回为准。神通预览是三维轨道和图标平面，战阵预览才是 NPC 模型；不要把它们描述成同一种完整角色动画。

## 4. 素材导入与制作工具

背包的 UIInkWaterfall.AllowLoop=false；滚轮、拖拽惯性和滚动条都限制在 0～MaxScrollOffset，首尾不重复物品。总高度包含列间错位与底部余量，滚动条按可滚动距离映射；内容更新或窗口大小变化后重算并夹紧偏移。墨边渐隐与物品错列保持。共用的神通、战阵列表保留原滚动模式，不能把背包调整扩散到其他页。实机脚本 .dsh/_diag/jiuba-manual-bag-check.cs.txt 同时验证秘籍使用扣除、重复学习限制以及滚动首尾、可见项唯一性。

现用 Sprite 已导入，日常运行无需重新导入。新增运行时成品放 resources/UI/InkUI 对应包；用“修仙 / UI / 导入 InkUI 素材（Sprite + 九宫格）”时检查日志与 border，尤其手填的 Parts / Skeleton / BigPieces。该菜单可覆盖导入参数，不要为了归档重跑它。

换 sprite 时深色 Image.color 复位白；进度填充保留 Filled，不能改 Simple；2× 素材 pixelsPerUnitMultiplier=2，按钮包1×按现用2.6。纯边框与有底贴图不可混用，禁止把文本、图标与选中框烘焙成不可复用的大图。细节见 [素材规格](../reference/UI素材/素材规格与提示词.md)。

网页原型入口在工具目录 index.html（早期意向预览，不是当前游戏画面）。paths.cjs / paths.ps1 统一定位 Assets 制作源和工程根。Node 工具需要 sharp、jszip、playwright；可通过 INKUI_NODE_MODULES 指定依赖目录，正常游戏运行不需要 Node。旧 build-spec 使用 Authoring/GeneratedSources 中按原生成文件名保存的42张输入，也可通过 INKUI_GENERATED_SOURCE 指定其他来源；缺少输入不能把导出成品当作原图二次生成。ability-art-v1、dynamic-assets-v1 的导出脚本已改为读取包内 source/，不依赖个人生成缓存。截图脚本从仓库根调用，输出仍在 screenshots/。

可从仓库根运行 Node 工具 cultivation/Assets/Editor/Tools/InkUIAuthoring/verify-organization.cjs；它只读检查文件校验和、脚本语法、预览路径、现状文档链接及生成来源，结果写入忽略的 screenshots/InkUI-organization-verification.json。

## 5. 接手顺序与禁忌

先读本页当前状态，再读 [八页重制总纲](../design/UI重制/八页重制总纲.md) 的原话对照及对应草图。草图在 Authoring/screen-studies/mockups-v2/；遵循用户最后澄清：无父底板、纯墨详情、被动随机星位、统一背包滚动条。下一未完成页由用户确定，不把保留基础皮肤的页面写成重制完成。

先复用已有控制器、数据入口与 QA；不要为了换皮重跑生成器或保存五个游玩场景。绝不能 git checkout 场景目录。配置表必须导入时使用“修仙 / 工具 / 安全导入配置表（不动场景）”，相关坑查 [踩坑总库](../ai/踩坑总库.md)。战阵隐藏世界 Renderer 时必须恢复原 enabled，隔离预览模型不能携带 NPC AI/碰撞/音频；MaterialPropertyBlock 在运行初始化时构造，不能在 MonoBehaviour 字段初始化器调用 Unity API。

文档按 [INDEX 的文档规范](../INDEX.md) 分类：当前机制写 architecture，步骤写 guides，用户要求写 design，规格写 reference，坑写唯一踩坑库；历史记录只读。新增/移动说明同步 INDEX 的目录与任务表及 PROGRESS。禁止往 docs 放素材/脚本，也禁止向现状指南不断追加按日期排列的施工流水。

提交前检查场景与原材质是否混入，明确列出本次文件。本地 commit；未经用户单独指令不 push。
