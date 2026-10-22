# 验证配方

当前版本：2026-10-22。通过[Unity包装器](../unity/README.md)执行，输出写忽略的.local/diagnostics。配方是可复用检查，不代表每次提交全部重跑；当前结论见[统筹报告](../../docs/reports/2026-10-22系统与文档统筹.md)。

## 最新定向检查

| 配方 | 模式与范围 | 最近结果 |
| --- | --- | --- |
| [spirit-flight-grip-editor.cs.txt](spirit-flight-grip-editor.cs.txt) | 非Play独立预览；实际玩家Avatar+武器，Rebind后检查九方向、手指/安装角、32相位胸髋和剑尖 | 261项通过 |
| [treasure-carousel-editor.cs.txt](treasure-carousel-editor.cs.txt) | 非Play；真实图卡中间帧、连滚/反向/循环、目录刷新及生命周期 | 17项通过 |
| [weapon-motion-editor.cs.txt](weapon-motion-editor.cs.txt) | 非Play；长戟四个平刺、飞剑地面八向、武器idle分流 | 最近79项通过 |
| [notice-entry-runtime.cs.txt](notice-entry-runtime.cs.txt) | 隔离Play；统一通知、真实帧末淡入滑入与生命周期 | 历史通知阶段12项；未作为本轮全回归 |
| [direct-scene-inventory-runtime.cs.txt](direct-scene-inventory-runtime.cs.txt) | 编辑器直接启动洞府，检查专用测试背包与正式/QA排除 | 按当前目录逐项比对并检查武器拥有；本轮未重跑Play |
| [paper-close-runtime.cs.txt](paper-close-runtime.cs.txt) | 隔离Play；斜叉、悬停、按住/取消、淡出和输入恢复 | 历史11项，不代表最新全界面验收 |

武器三组合、长戟79项和宣纸页108项的阶段证据见统筹报告；未提交本机截图/完整日志。当前新武器中央显示已实现，不要运行旧“武器待开发”断言。旧硬件光标10像素/32画布断言也不适用，现为高分辨率RawImage、UV裁切和实际可见尺寸。

## 系统配方索引

- 战斗：combat-pipeline、melee-impact-runtime、combat-hit-feedback、all-state-casts-runtime。
- 家园：home-idle-integration、home-farming-runtime及home-*编辑器配方；正式地图当前三张。
- 外观：appearance-cloth-runtime、appearance-goldencore-shell、appearance-goldencore-hair、appearance-goldencore-preview；Avatar有效不代表姿态正确。
- 法则：law-balance、law-chest-drop、law-fragments；新纸面双环替代独立旧法则页，旧UI断言需先更新。
- 御风：flight-dodge-editor/runtime；持握定稿以[动作登记](../../docs/guides/动作定稿与重建边界.md)为准。
- 地图/渲染：world-map-rendering-runtime、world-authoring-map-repairs、world-map-fog-depth、tree-spatial-aa。

以上为文件名前缀，可在本目录查找对应.cs.txt。太初龙栖诀、八九秘籍、旧主线/五场景及旧独立面板的配方保留源证据价值，**不再是当前玩法验收入口**；不要把其中旧成功数量累加到当前版本。

## 执行与清理

Editor配方不进入Play；runtime配方必须真实Play和真实帧末，不能用编辑器协程或手动渲染主相机替代。只渲染自己的隔离预览相机。

自动Play前记录真实存档指纹、设QA目录；已隔离不能重复setup。结束停止Play、撤销SessionState覆盖、核对正式目录指纹及原Game View分辨率。不得重算测试后的指纹冒充原基线，也不得覆盖用户存档“恢复”。直接场景赠物是独立测试模式，不能让其覆盖QA流程。

不保存正式场景、不提交截图/日志/个人数据。改变动作前检查成品GUID、全链持握与工具范围；安装动画子节点后先Animator.Rebind再采样。
