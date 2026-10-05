# 游戏HUD

> **管什么**：气血/灵力、左下弧链主动技能、墨晕冷却、悬停提示与验证。
> **不管什么**：修炼次数来源见 [时间系统](../guides/时间系统.md)；境界页见 [UI接入与维护](../guides/UI换皮.md)；施放与伤害见 [主动技能与神通](主动技能与神通.md)。
> **本文件怎么查**：§1 接入与布局｜§2 数值与冷却｜§3 悬停与输入｜§4 验证。
> **来源**：当前HUD脚本与Play实测；用户的新布局取代旧方格HUD和修炼次数条。

## 1. 接入与布局

PlayerHud管原节点的数据；UIInkHudSkin在Play中重排现有HudCanvas。换皮不运行HudBuilder、不保存场景。关闭水墨主题时保留基础HUD。

| 部件 | 展示 | 1920×1080参考布局 |
|---|---|---|
| 气血、灵力 | 暗红连续血条、骨白轮廓与靛蓝枝脉；轻微波动与游动粒子，血量不分格 | 左下宽380，左缘282；底部159 / 112（从上一版右移76、下移42） |
| 境界圈 | 大笔刷墨圈、纯墨衬底与实时境界名；保留功法悬停入口 | 左下160×160，位置78 / 128 |
| 技能1–6 | 共用神通页三维双环、动态连线与同源图标，额外叠加墨晕冷却 | 左下弧链，六槽72×72 |
| 修炼次数 | HUD不显示，境界页下栏显示 | 原“修炼”节点隐藏，序列化引用保留 |

CanvasScaler按1920×1080缩放；1280×720采用相同相对布局。按键数字独立于图标，保留暗描边。

I父页面打开时，CharacterPanelUI隐藏HudCanvas、QuestGuideCanvas、ChronicleCanvas并关闭交互/射线；关闭后恢复此前状态。HUD数据继续更新，每0.5秒补扫晚创建画布。

## 2. 数值与冷却

UIInkVitalBranch读取原Filled Image的fillAmount，绘制连续血条及骨白轮廓、靛蓝枝脉。原Image停止渲染，但类型与业务填充值不改。新Graphic必须带CanvasRenderer。布局草图只决定位置，最终造型按枝脉参考及神通页。

枝脉以不受暂停影响的时间作小幅波动，30Hz刷新网格。两端有封口与收尖端饰；光尖严格跟随fillAmount对应的填充末端，只在光尖后方约8像素内有4颗细粒子逸散并淡出，不再沿整条分布。空值不显示光尖或粒子，减少动效时保留静态边界光尖、关闭粒子与波动。装饰不接收射线，不改变数值与填充值。

HUD直接调用神通页的UIInkSkillVolume.InitializeHud和UIInkConstellation.HudNodes：真实三维双环与图标平面渲染到透明RenderTexture，保留深度遮挡、慢转与鼠标视差；空槽也有双环。旧灰圆绘制仅作三维初始化失败时的回退。每套舞台隔离位置，避免HUD相机拍到神通页的环。

ActiveSkillCaster提供冷却剩余秒、冷却总秒、冷却比例。比例1为刚进冷却，0为就绪。UIInkHudSkill把比例传给UI/InkCooldown Shader：初期轮廓扩散、模糊、沉入墨色，背后出现墨晕；随比例降低逐渐恢复清晰。中央保留倒计时：≥1秒显示整数，最后一秒显示一位小数。旧纵向填充遮罩不再展示；业务不可用的灰罩与冷却分开。

图标由UIInkAbilityArt.Icon读取，和神通页装备位一致。Shader放Resources供构建加载；每格独立Material，销毁时释放。三维图标采用同款InkCooldown并打开深度写入与测试；UI默认仍关闭深度写入。减少动效停止呼吸和双环转动，冷却仍正确更新。

## 3. 悬停与输入

HudHoverTarget沿用原入口：0–5为技能，-1为功法。只有槽底接收射线，连接线、装饰图和文字不挡场景点击。提示墨幕出现在左下技能链上方，并限制在屏幕内。移开隐藏，空槽不显示。

提示仍读取原神通、灵阵、法宝、功法数据；更换展示不改装备或施放。

调试属性面板使用**P**。PlayerStatsDebugPanel.Awake把旧场景序列化的F1映射到P，其他自定义按键保留。关闭面板用组件内部显示开关；不要禁用Player GameObject，否则玩家与冷却都停止。

## 4. 验证与维护

仓库根`.dsh/_diag/hud-dialogue-ink-check.cs.txt`使用独立UIPanelData与禁用的ActiveSkillCaster，临时替换HUD引用，结束恢复；不改真实玩家功法/装备/冷却，不施放技能。

| 检查 | 实测 |
|---|---|
| 分辨率 | 1920×1080、1280×720 |
| 连续图形 | 首尾封口与端饰；光尖随填充边界定位，额外4颗粒子仅在边界附近 |
| 六槽位置 | 左下锚；(40,240)、(48,162)、(110,72)、(207,91)、(305,76)、(403,87) |
| 三维舞台 | 共用神通页组件，三维就绪=True，6星位 |
| 冷却 | 固定样本1、0.5、0；Shader supported=True |
| 修炼HUD / 调试键 | activeSelf=False / P |

截图在`cultivation/screenshots/hud-ink-1920x1080.png`与`hud-ink-1280x720.png`，不入库。截图技能和冷却为测试数据，不代表存档装备。

维护脚本在`Assets/Scripts/UI/`：PlayerHud、UIInkHudSkin、UIInkHudSkill、UIInkVitalBranch。Shader在`Assets/resources/UI/InkUI/Dynamic/InkCooldown.shader`。不能为换皮重跑生成器或保存场景，不能把Filled改Simple。
