# Windows 打包与发布

目标平台为 Windows x64，使用团结 1.6.13 / 2022.3.61t14。软件包发布到 [GitHub Releases](https://github.com/BerryP1e/project-cultivation/releases)，二进制文件不进入源码仓库。

## 当前测试版（2026-10-09）

[争渡 · 御风、战斗特效与体素更新](https://github.com/BerryP1e/project-cultivation/releases/tag/v0.1.0-alpha.20261009)：`zhengdu-2026.10.09-windows-x64.zip`，附SHA-256文件。包含八向御风、腕指与持械姿态、灵虚飞剑收放、全状态水龙炮/冰暴/瞬雷施法、ExtremeFX命中特效和玄霄四式，以及焚天38%召唤、每颗火球接触破坏、体素范围/深度独立控制和连续攻击及时提交。此前开始界面、主线和体素地图功能保留。

本轮构建成功，0错误、39条第三方素材着色器/缺失脚本警告，六个有效场景（大小写重复项去重），构建约120秒。独立D3D11程序启动进入StartScene，146个妖魔、1306次动作切换、146个模型骨骼运动验证通过。编辑器实测浅坑比例正确、巨剑半径减半而保留深度；真实焚天/巨剑、重叠和持续攻击均无缺面，最终网格和碰撞一致，焚天首坑约接触后0.14秒可见。检查见[正式地图体素环境](正式地图体素试点.md)。仍为开发测试版，建筑体素转换、远处LOD和破坏区流式管理尚未接入。

上一版：[2026-10-08 体素破坏与动作修复](https://github.com/BerryP1e/project-cultivation/releases/tag/v0.1.0-alpha.20261008)。

## 构建

先保存手工场景修改并退出 Play，然后执行编辑器菜单 `Cultivation / Release / Build Windows x64`。构建工具只读取已保存的场景，不调用场景生成器，不保存或重建场景。

入口是 `StartScene`，采用 Build Settings 中已启用、实际存在的场景；同一场景的大小写重复项只构建一次。动态查找的自定义着色器会登记到 Graphics Settings，避免正式运行时被剥离。

输出程序为 `cultivation/Builds/Windows-x64/cultivation.exe`，构建报告为 `cultivation/Builds/windows-release-report.json`。必须确认报告 `status` 为 `Succeeded` 且 `errors` 为 0。

也可以在编辑器未占用该工程时进行命令行构建：

```powershell
& 'D:\Tuanjie 2022.3.61t14\Editor\Tuanjie.exe' -batchmode -projectPath 'D:\project：cultivation\cultivation' -executeMethod WindowsReleaseBuilder.Build -logFile 'D:\project：cultivation\cultivation\Builds\build.log'
```

`Assets/Editor` 下的工具代码不会进入软件包，包括 `DshBridge`，无需删除源码。

## 软件包

保留同一输出目录下的 exe、数据目录、引擎 DLL 和 Mono 运行库，整目录压缩成 ZIP。解压到可写目录后运行 `cultivation.exe`；不能只复制 exe。

正式上传前检查程序启动日志。发布说明标明对应源码提交，附上 ZIP 和 SHA-256 校验文件。存档由游戏写入系统用户数据目录，不随软件包打包。邮箱恢复文档、访问令牌和临时诊断文件不发布。

## NPC 动画回归验证

NPC 动作表不能在正式运行时通过反射读取编辑器的 `AnimatorController.layers`。`NpcAnimationCatalogBuilder` 在每次构建前生成 `resources/NPC数据/NPC动作映射.asset`，保存真实控制器引用、状态名、Action 条件索引及动作片段。不会改动场景或预制体；没有 Action 参数的控制器按状态名切换，空片段不当作可播放动作。

发布前运行独立程序的专项检查：

```powershell
& './cultivation/Builds/Windows-x64/cultivation.exe' -batchmode -force-d3d11 -npc-animation-smoke -npc-animation-report './cultivation/Builds/npc-animation-player-report.json' -logFile './cultivation/Builds/npc-animation-player.log'
```

检查全部 146 个妖魔预制体的可播放动作：请求是否成功、片段时长、实际进入的状态和骨骼运动。报告 `passed` 必须为 `true`，程序通过返回 0，失败返回 1。这是动画回归验证，不代替完整战斗与 AI 行为验收。
