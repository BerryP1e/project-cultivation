# 只读资产审查

`asset-audit.py --targets Assets/某目录 ...` 建立该范围的外部引用入口和动态路径入口，写 `.local/diagnostics/asset-cleanup/inputs.json`。默认检查导入环境、共享特效与战斗特效库。它不删除或移动文件。

通过 Unity `AssetDatabase.GetDependencies(seeds, true)` 取得递归闭包并写入同目录 dependencies.json，再运行 `--merge-dependencies` 补查闭包中脚本的 C# 类型依赖，再次计算 Unity 闭包直到入口稳定。删除前还要检查 Shader.Find、拼接路径、FBX 内的 Clip/Avatar 和团结加密 GUID，不能仅凭文本搜索。

`runtime-resource-roots.json` 登记多段拼接等无法从单个字符串推断的完整 Resources prefab 路径，不带扩展名。审查脚本强制将登记资产加入保留入口；任何资产缺失即失败，不能把不存在的必需资源排除后继续清理。新增怪物特效须同步登记并运行 `npc-projectile-assets.cs.txt`、`npc-projectile-resources.cs.txt`，后者对九类真实 AI 默认配置逐招检查清单覆盖和实际发射。

`check-asset-references.py` 对本机整理前快照比较新增的十六进制 GUID 缺失；没有快照会拒绝运行。它不能解密团结 GUID，应配合编辑器加载与运行回归。快照、审查清单和报告均不提交。
