# Kimodo 与 Blender 动作工作流

2026-10-12：本地 Blender MCP 已接通，御风左旋闪避经用户确认后导入 Unity。

## 本机连接

- Blender 5.2.1 LTS；社区 `mcp-for-blender` 2.1.9，Codex 全局连接名为 `blender`。
- 独立 Python 环境在仓库根 `.local/tools/blender-mcp/`；该目录是 MCP 连接依赖，不能作为临时垃圾删除。换位置时同步更新本机 Codex MCP 配置。
- 用户目录中的 `Blender/5.2/scripts/addons/blender_mcp.py` 已安装并启用，监听 `127.0.0.1:9876`，遥测关闭。本机直接调用 `blender_mcp.server.main`，无需 uv。
- 使用独立 Blender 工作窗口，原来的未保存场景保留。制作文件和诊断放在 `.local/art/AirborneSpinDodge/`，没有复制到 Unity Assets。

## 当前动作

| 资源 | 用途 |
| --- | --- |
| `Assets/Animations/凭虚御风/Kimodo/御风_左旋闪避.anim` | 独立 Humanoid 成品；30 FPS、约 2.367 秒、非循环，含40条手指曲线 |
| 同目录 `Airborne_LeftSpinDodge.fbx` | 已按玩家骨架烘焙的动画源；不含模型、贴图、灯光或相机 |
| 仓库根 `.local/art/AirborneSpinDodge/Airborne_LeftSpinDodge.blend` | Blender 制作文件及玩家预览，供继续编辑 |
| 同制作目录 `AirborneSpinDodge_jump_raw.bvh` | 选用的纯提示词 Kimodo 原片；无全身姿势约束 |
| 同制作目录 `Airborne_LeftSpinDodge_preview.gif` | 已确认版本的预览 |

先纯提示词生成，再局部修改。选用 Kimodo-SOMA-RP-v1、seed 20261014、100 步、生成时长2.4秒：

> A person jumps and spins one full turn counterclockwise in the air, pulling their knees up and sweeping both arms around, then lands facing forward.

直接描述悬浮闪避的候选倾向地面闪避；此候选得到自然的完整转身，再修整悬浮高度、侧移、迈步和收势。用户认为首版密集姿势约束过僵硬，该版本弃用。最终按反馈收住明显摆臂，保留躯干发力及轻微腕部跟随。

## 导入与验证

玩家预览映射53根骨骼，包含30根手指骨。Unity 使用实际 `better_player_test` 的人形映射建立有效 Humanoid Avatar，提取独立动画。初次导入时未接玩法；2026-10-12通过独立衍生片段接入[御风闪避](御风闪避.md)，加入输入、碰撞位移和伤害免疫，保留原定稿动画及共享控制器。

源片段根旋转和Y高度烘入姿势，XZ侧移保留为根运动；闪避衍生片段归零XZ并使用御风高度基准，实际位移交给角色碰撞控制器。源帧范围0～71对应约2.367秒，预览采样文件有72帧。

Blender FBX 回导对照的关节位置最大误差约0.00000173米；回导设 `anim_offset=0`，避免默认一帧偏移。Unity以真实玩家Avatar通过Playable采样72帧，手脚/髋骨数值有效、足部始终离地，最低约0.325米；成品为Humanoid，130条曲线中包含40条手指曲线。

FBX 导入提示额外中间颈骨 `NeckTwist02` 的独立旋转会被 Humanoid 丢弃；没有 Avatar 配置错误。后续若要完全逐骨一致，可在制作阶段合并这根颈骨的运动到人形映射骨，再复核，不应修改已定稿的其他动作。

## 后续制作规则

先保存提示词、种子与原片，再进行局部修整；不要用密集姿势约束把动作压成固定姿势旋转。重定向补偿源/目标静止姿势和骨轴，保留腕部与手指。完成后导出无额外末端骨的FBX并烘焙完整帧，做回导与真实玩家Avatar检查。工作 `.blend` 不直接放进 Unity Assets 触发自动转换。
