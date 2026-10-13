# 验证配方

局部命中反馈：`combat-hit-feedback.cs.txt` 在独立 Play 检查真实玩家/野猪的双方/单方顿帧、自动恢复及施法定格、连续命中限制、免疫和关闭表现、物理特效外侧投影。三套近战的实际接触与渲染继续由 `melee-impact-runtime.cs.txt` 检查。

此目录保留当前玩法、场景、动画、特效和体素相关的可复用 C# 验证片段。用 [Unity 包装器](../unity/README.md) 执行，参数和报告放 `.local/diagnostics`。

文件内容决定其运行模式：使用 Editor API 检查资产的选 editor；依赖实际 Player / Coroutine 的选 runtime 并先进入 Play。部分配方会临时移动角色、切换功法或创建测试目标；在独立 Play 回归中执行，结束后停止 Play，不保存测试场景状态。

不要提交截图、完整 Console 日志、个人存档或本机连接配置。重新生成数据或保存场景不是只读验证，执行前确认实际影响。

水龙炮召龙吐息：`all-state-casts-runtime.cs.txt`在独立Play检查地面、御风、实际碧水兽骑乘的水炮和两种追踪弹，共9次施放；水炮覆盖玩家玄霄A3自然播放、倒放Hit、主角43%同步龙Attack55%定格与出水、主角自然完成后吐息继续、嘴部挂点、伤害及消融。`water-dragon-breath.cs.txt`补充后侧位置、手动落点、移动目标、超距、显形中断、施法者销毁、执行体停用和无残留。报告归`.local/diagnostics/dragon-mob`，不保存场景或存档。

`water-dragon-continuity.cs.txt`在Editor隔离预览检查龙模型两倍缩放、显形/姿势融合/定格/跑步循环接缝连续性及30/60/120Hz下震颤平滑退出；吐息Play配方额外检查放大后近距离炮口位于目标之前。

战斗管线回归：`combat-pipeline.cs.txt`在Play检查六种伤害路由、实际扣血/护盾/过量/死亡、重复碰撞体、事件与环境配置；`melee-impact-runtime.cs.txt`使用真实功法装载和剑身求交，检查三套近战三式、挥空、命中表现及无体素破坏。两者须独立进入Play，报告和多帧渲染证据写`.local/diagnostics`，结束后停止Play。

太初龙栖诀回归：`taichu-runtime.cs.txt`在独立 Play 检查实际伴龙轮廓、五式/远近循环、目标冰晶往返、三枚火弹、可打断特殊待机、普攻与主动加成隔离、取消和引导；`taichu-scenes.cs.txt`在 Editor 只读检查所有启用场景装载、腕指曲线与资源依赖。说明见 [太初龙栖诀](../../docs/guides/太初龙栖诀.md)。

定稿清理后运行91项、场景/成品93项、A1覆盖716项再次通过。场景脚本检查八套独立 Generic `.anim` / Avatar，原来源目录与重建器不存在且依赖闭包无 DragonPriest FBX；仍验证实际玩家腕指、A2/Guard03朝向和CPU/GPU网格标记。源动画对照及一次性提取器仅写本机诊断，不作为维护工具保留。


## 2026-10-11 法则与日常机会

`law-fragments.cs.txt`：7262项实际Play检查法则生成/击杀/开箱/装备/属性/存档/日常/3D界面。`law-handoff.cs.txt`：最新会话接力、真灵归属、移动倍率。`taichu-contact-blend.cs.txt`：148项A1窗口和八种伴龙切换连续性；须在干净玩家场景运行，报告归 `.local/diagnostics`，不保存场景。

收尾接力/真灵归属/移动倍率8项和六玩家场景/资源依赖83项检查通过。最新太初接触窗口与姿势过渡检查129项通过（帧采样项数随运行帧率变化）；五式真实战斗91项、法则7262项回归再次通过。共享编译0错误，11条既有素材/工具警告未增加。

法则交互/分层动效：`law-ui-optimized.cs.txt` 在独立 Play 检查六槽双环、鼠标拖拽、背包隔离、御风悬停半尺寸人物、周围粒子吸纳和松手平滑续转；`law-art-links.cs.txt` 接续该配方检查晶体/墨纹/灵气独立运动、实时光粒及神通连接网格。当前分别29项、6项通过。后者需要前者产生的法则测试集合，不保存测试状态；所有动画使用 unscaledTime，面板暂停游戏时也应正常播放。

`law-balance.cs.txt`：38项数值回归（只保留六项敏感配置、恢复普通副增幅/固定值及无专用截断、10万次高值稀有抽样、六槽总增量上限、回复参照、丹药保留和词条校准）。`law-arc-ui.cs.txt`：17项半弧收藏带回归（可见卡片池、切线/滚轮/滚动条、拖拽装备/卸下、真实射线不遮挡装备槽、半透明底与环境吸纳）。在干净3C_Testbed Play运行，脚本只修改本次Play数据；用 script-args.mjs 生成 runtime 参数，exec_runtime_script 执行，报告归本机 `.local/diagnostics`。停止Play后用 law-scenes.cs.txt 在 editor 模式审查六玩家场景依赖。

`law-presentation.cs.txt`：19项展示与交互检查，覆盖整区半透明底/右侧统一水墨底、人物仪轨六槽同步1.45倍取景与实际射线、主副属性单行淡底/悬停/滚动、切换节点复用、操作说明移除及重开。沿用干净3C_Testbed Play运行方式，截图与报告仅写 `.local/diagnostics`。

`law-refinement.cs.txt`：443项熔炼/洗练检查，覆盖九境界5倍基础产出/10同阶与50下阶消耗、品质增产、20次5%洗练封顶、副属性不变、装备保护、实际炉鼎射线与入炉动画、关面板时交易原子性、long余额与炉中UUID存档/跨场景、单行属性布局和生成素材。独立干净Play运行，不写正式存档，结束后丢弃测试状态。

`flight-dodge-editor.cs.txt`：Edit Mode隔离预览检查12段御风闪避、实际玩家骨骼/手指、左右完整镜像、连续姿态、零XZ根位移及六场景共享控制器；808项通过。`flight-dodge-runtime.cs.txt`：使用 `exec_runtime_script` 在独立3C_Testbed Play中创建临时角色和墙，检查相机方向、直线/斜向位移、六类伤害及直接伤害免疫、随机状态、撞墙、冷却、状态中断、骑乘禁用、施法占用和待发子弹取消。报告写 `.local/diagnostics/flight-dodge`，不保存正式场景或存档；结束后停止Play。不可用 `exec_editor_script` 运行后者，该工具会先停止Play。

`home-farming-runtime.cs.txt`：独立Play检查洞府连续地面扩建与通行、取消摆放退款、种子仓库事务、松土/播种/浇水/收获、满仓停工、离场数据任务、仓库面板和JSON持久化。使用3C_Testbed，关闭自动存档，不保存测试场景或正式存档；报告写 `.local/diagnostics/home`，结束后停止Play。场景一致性体检同时检查HomeAssets资源入口。

`home-puppet-rig-editor.cs.txt`：非Play隔离预览检查傀儡Avatar采用真实绑定T姿势、30个手指关节、完整骨骼映射与五种动作逐帧姿势，并检查待机/走路躯干与肩部朝向。此检查用于发现“Avatar有效但基准姿势错误”的扭曲，不能只检查isValid。报告归 `.local/diagnostics/home`，不修改场景或资产。

`sect-ground-editor.cs.txt`：非Play检查Sect圆角成品网格、原顶面碰撞、原物件变换（若有烘焙前本机快照）、材质、顶面拓扑和七组岩壁过渡带接缝；逐段验证顶面贴合、实际源岩壁截线贴合与过渡带内部连续，防止凸包穿出岩壁。不修改或保存场景。`home-farming-runtime.cs.txt`同时检查非松土动作原速、1.65米/秒实际移动、10秒六轮松土、四种作业的田中心位置；基站旋转315°后，检查生成及实际走回前方木板时的位置、朝向与用户运行场景放好的停靠姿态一致。

`home-ground-editor.cs.txt`：非Play预览单块/两块融合草地，实际碰撞射线及五种布局的网格封闭/非流形检查，包含保留中间空洞的环形扩建。输出预览和报告到 `.local/diagnostics/home`；不修改场景或正式资产。

`home-boundary-editor.cs.txt`：树石烘焙、石头X轴-90°源朝向、无额外碰撞、同布局无变化、扩建后远端实例及位置保持、还原确定布局；输出实际围挡预览。`home-boundary-runtime.cs.txt`：真实HomeWorld地面重建下的围挡实例保留、开口清理、四向寻路、重置和切场景回归。独立Play运行，关闭自动存档，结束后丢弃测试状态；报告写 `.local/diagnostics/home`。

`law-chest-drop.cs.txt`：25项10%掉箱/5%条件升阶回归，包含100万次击杀抽样、实际Catalog资产、概率边界、失败掉箱不得升阶和九境界封顶。只读检查，不临时修改掉率。

`taichu-a1-coverage.cs.txt`：A1命中覆盖与连续动画重启回归；复现旧表面判定漏掉矮小目标，检查0.4/0.8/1.2/2米高目标在0.6/1.5/2.4/3米距离、四种朝向的实际普攻伤害，窗口前不伤害、每招一次、友方/范围外/过高目标不误伤，以及跨过整个窗口仍补采甩尾弧线。仅修改临时Play数据和克隆配置，结束后停止Play，不写存档或场景。

`law-essence-stream.cs.txt`：13项精粹汇入动效回归，覆盖左上计数、放大炉鼎的真实射线、产出与粒子数量关系、实际贴图/网格、炉火后发射、暂停播放、抵达脉冲和关闭仅清理表现。报告/截图只写 `.local/diagnostics`，修改的数据仅限当前Play。

`npc-projectile-assets.cs.txt`：Editor检查七NPC主题及玩家Priest实际粒子网格、使用的材质/Shader与玄蜂箭矢；忽略未启用trails时不用的第二材质槽。`npc-projectile-resources.cs.txt`：118项Play检查24个强制资源入口、九类AI真实攻击默认配置、直接生成子弹而非延迟命中回退、飞行粒子/箭矢网格、接触/躲避/最大距离消散。不改怪物参数或正式场景。

- `flight-dodge-feedback.cs.txt`：Play下 exec_runtime_script 验证闪避尾段直接融合、残影池与消散、擦身慢镜真实时长、镜头恢复/暂停优先级、扩大的飞弹和近战擦身范围以及灵虚地面身侧剑（不写存档或正式场景）。
- `flight-dodge-blend.cs.txt`：Play下 exec_runtime_script 检查中段到结束的递增融合，覆盖八方向、30/60/120Hz、三档播放倍率、源动作独立速度、目标片段相位连续、结束不跳待机、速度交接、模型Yaw补偿和中断复位。报告归 `.local/diagnostics/flight-dodge`。

- `lingxu-takeoff-scale.cs.txt`：隔离Play中验证灵虚剑的默认大小、升空/落地逐帧大小、原攻击缩放、正式模型缩放及升空混合攻击，报告归本地diagnostics。

- `home-production-editor.cs.txt`：非Play验证三种建筑的源网格非凸碰撞、兽栏空腔与实际栏杆射线、五种中立家畜/原生动画、15件物品与兑换图标，以及六种退役动物清理后的聚合引用和旧档兼容。
- `home-production-runtime.cs.txt`：Play执行生产数值、饥饿/食物偏好/繁育/容量/满仓、堆肥与炼丹事务、途中存档、实际背包转移、界面和逐帧姿态；使用exec_runtime_script，测试后停止Play，不写存档。
- `home-production-integration.cs.txt`：Play执行真实游戏时钟、暂停、离场与重建、功德堂目录、F面板与关闭输入归还、炼丹三列间距和格选择、真实施肥扣除与冷却、摆放互斥。报告/截图归 `.local/diagnostics/home-production`。

- `active-wave1-editor.cs.txt`：非Play检查五门天降定义/玉简/面板与共享表现引用、剑尖/预算/多道落雷/去蓄力、正确命名/固定插地剑、能量02前半及三段实际玩家腕指。151项通过。
- `active-wave1-runtime.cs.txt`：独立Play检查五技能地面/御风/实际碧水兽共15次施放，实际接触、主动加成/属性、逐弹及多碰撞体去重、友方/范围、特效真实发射、雷陨24道散布与附近目标命中、鼠标落点、暂停/中断/目标消失与失败不扣费。276项通过，证据归 `.local/diagnostics/active-wave1`，不写存档或正式场景。
- `active-wave1-stability.cs.txt`：经共享表现入口实际播放两种剑雨命中，在0.15～0.75秒连续采样剑的位置/旋转/尺寸并验证剑尖朝下；16项通过。
- `spiral-sword-editor.cs.txt`：变形椭圆/均匀弧长、斜铺朝向、不同区域落点、强起步、曲线转直线位置/速度连续及末段方向固定、连续速率、目录/Shader/预算及剑雨落花静音；1250项通过。
- `spiral-sword-runtime.cs.txt`：三种状态完整288次接触，真实渲染剑间距均匀且不成簇、稀疏边缘微光、引导剑/幻影、独立出剑时刻、两段施法、周围敌人实际伤害、地表范围散布、插地网格不扭转、去重/友方、手动点、暂停/中断/失去目标及失败原子性；96项通过。
- `spiral-sword-edges.cs.txt`：20fps、移动目标新旧弹道连续性、真实粒子暂停、有限并发/288次接触、取消清理及资源错误不扣费；当前51项通过（含逐帧检查，数量随完成帧数变化）。报告和实拍归 `.local/diagnostics/spiral-sword`。
- `thunder-palette-runtime.cs.txt`：玄霄四式匹配A1、EpicZeus动态子特效继承调色、原生粒子/灯光及共享源材质/其他技能不受影响，19项通过；不保存场景或存档。

- `spiral-sword-release.cs.txt`：实际第二段动作38%下蹲节点出剑，覆盖0.5/1/1.8倍动作速度、暂停/恢复、节点前打断、无提前伤害和无积压齐射。本次459项含逐帧检查通过；报告归 `.local/diagnostics/spiral-sword/release-report.txt`，不写场景或存档。

行游轮盘：`traversal-wheel-runtime.cs.txt` 在独立 Play 使用真实玩家骨架与碧水兽，333 项检查覆盖 Tab 释放、扇区/遮罩、输入锁、双向模式过渡、动态学习及内存 JSON 保存恢复，不写场景和存档。

角色界面新版：`ui-unified-runtime.cs.txt` 在独立Play验证背包、法则总览/背包分层、筛选、拖拽、熔炼/精粹洗练、原3D星空装备栏及独立表现预览、未选中卡片的真实鼠标拖拽、暂停中的墨流/鼠标搅动、外观柔边底、法宝/坐骑空预览与透明模型；两种分辨率各68项通过，使用临时数据并恢复，不保存场景和存档。`new-ability-icons.cs.txt` 为Editor图标导入与引用检查，真实alpha/不同Sprite/导入参数/神通玉简共25项。旧包裹开场与随机错列的外观断言已由新版规则取代。

`ui-presentation-bugs.cs.txt` 为材质、冰冢长光芒、八种飞弹主体同步/沿路空间、人物对木桩循环、实际粒子与换装专项回归（112项），仅独立Play运行。临时外观会话恢复，不写场景或存档。
