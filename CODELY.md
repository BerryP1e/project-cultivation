# 争渡：工作区入口

Unity/Tuanjie 工程在 `cultivation/`，文档唯一入口是 [docs/INDEX.md](docs/INDEX.md)。开工先查 [工作规范](docs/ai/工作规范.md)、[工程与工具链](docs/ai/工程与工具链.md)，再按 INDEX 找对应系统。玩法以用户当前指令和设计文档为准。

- 引擎：Tuanjie 2022.3.61t14，Built-in；源码 `.cs`、配置 `.csv` 使用 UTF-8 BOM。
- 资源布局与清理规则：[资产与目录](docs/architecture/资产与目录.md)。已经定稿的灵虚/太虚剑决使用成品动作和独立武器，不保留原第三方模型及重建器。
- 编辑器控制：`devtools/unity/start-server.ps1 -Background`，`node devtools/unity/mcp-call.mjs list`。工具说明见 [devtools](devtools/README.md)。旧 DSH 文件桥已经移除。
- 当前回归脚本在 `devtools/validation/`；报告、截图和本机备份写入被忽略的 `.local/`，不要写进 Assets。
- 修改 CSV 后显式导入生成资产；运行时不直接读 CSV。检查场景是否未保存，再进行 Refresh、切场景或进入 Play。
- 保留用户已有场景修改；迁移资产保留 `.meta` / GUID，同时更新动态 Resources 路径和文档。不要因为模型包含大量骨骼就当作冗余删除。
- 按新存档验证，不增加旧存档迁移。用户明确要求 push 时再提交推送；检查差异、提交、push 分开执行。
- 不提交凭据、代理/账号配置或个人日志。代理故障诊断只输出必要的地址、状态和脱敏错误。
