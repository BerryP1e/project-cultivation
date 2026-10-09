# Unity HTTP 控制

打开 cultivation 工程，执行 `powershell -NoProfile -ExecutionPolicy Bypass -File devtools/unity/start-server.ps1 -Background`。需要本机安装 Tuanjie Cowork/codely，服务器默认只监听本地 8765。

`node devtools/unity/mcp-call.mjs list` 列工具，`schema <工具名>` 查真实参数；`call <工具名> '@.local/参数.json'` 执行。失败结果会返回非零退出码。

`node devtools/unity/script-args.mjs devtools/validation/配方.cs.txt .local/参数.json 说明 editor` 包装编辑脚本，运行时配方把最后一项改为 runtime。随后调用 exec_editor_script 或 exec_runtime_script。

检查场景是否保存以及是否在 Play；刷新、编辑脚本与切场景会影响运行状态。结果须看 success / Console，不能靠“已发出命令”判断完成。
