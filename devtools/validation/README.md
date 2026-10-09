# 验证配方

此目录保留当前玩法、场景、动画、特效和体素相关的可复用 C# 验证片段。用 [Unity 包装器](../unity/README.md) 执行，参数和报告放 `.local/diagnostics`。

文件内容决定其运行模式：使用 Editor API 检查资产的选 editor；依赖实际 Player / Coroutine 的选 runtime 并先进入 Play。部分配方会临时移动角色、切换功法或创建测试目标；在独立 Play 回归中执行，结束后停止 Play，不保存测试场景状态。

不要提交截图、完整 Console 日志、个人存档或本机连接配置。重新生成数据或保存场景不是只读验证，执行前确认实际影响。
