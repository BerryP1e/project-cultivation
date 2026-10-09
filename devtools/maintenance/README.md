# 只读资产审查

`asset-audit.py --targets Assets/某目录 ...` 建立该范围的外部引用入口和动态路径入口，写 `.local/diagnostics/asset-cleanup/inputs.json`。默认检查导入环境、共享特效与战斗特效库。它不删除或移动文件。

通过 Unity `AssetDatabase.GetDependencies(seeds, true)` 取得递归闭包并写入同目录 dependencies.json，再运行 `--merge-dependencies` 补查闭包中脚本的 C# 类型依赖，再次计算 Unity 闭包直到入口稳定。删除前还要检查 Shader.Find、拼接路径、FBX 内的 Clip/Avatar 和团结加密 GUID，不能仅凭文本搜索。

`check-asset-references.py` 对本机整理前快照比较新增的十六进制 GUID 缺失；没有快照会拒绝运行。它不能解密团结 GUID，应配合编辑器加载与运行回归。快照、审查清单和报告均不提交。
