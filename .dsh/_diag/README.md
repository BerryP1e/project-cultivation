# .dsh/_diag —— MCP 工具 schema 的离线校验台

这套小工具是为了解决一个具体的坑：**harness 会整批拒绝 MCP 工具列表**。

## 背景

`@deepseek-ai/dsh-mcp-client` 遵循「要么完整世代，要么没有」——只要有一个工具的
`inputSchema` 落在 harness 的「受支持 JSON Schema 子集」之外，**这一批工具全部注册不上**，
日志里只有一条很含糊的 `Invalid input: expected "object"`。

codely unity-mcp 的原始输出实测是 **0/17 通过**，踩的坑：

| 问题 | 涉及工具 |
|---|---|
| `$schema` 不在支持子集内 | 全部 17 个 |
| 根是 `anyOf`（不是 `type:"object"`） | `unity_editor` |
| 嵌套 `anyOf` | `unity_gameobject` |
| `additionalProperties` 是 schema 不是布尔 | 7 个 |

所以 `.dsh\unity-mcp-proxy.mjs` 里加了递归消毒。**这套脚本就是验证消毒结果用的**，
不需要 spawn 进程、不需要重启 DSH，在受限沙箱里也能跑。

## 用法

```powershell
# 1. 抓一份原始 tools/list（推荐走 HTTP 端点：不依赖 harness，也不需要在沙箱里 spawn 子进程）
pwsh -File .dsh\serve-unity-mcp.ps1 -Background
node .dsh\_diag\mcp-toolslist.mjs .dsh\_diag\toolslist.json
# 2. 消毒
node .dsh\unity-mcp-proxy.mjs --transform .dsh\_diag\toolslist.json .dsh\_diag\toolslist.fixed.json
# 3. 过 harness 自己的校验器
node .dsh\_diag\validate.mjs .dsh\_diag\toolslist.fixed.json
#    → 期望 "17/17 通过"，退出码 0
# 4. 想确认没"靠掏空 schema 换通过"
node .dsh\_diag\fidelity.js .dsh\_diag\toolslist.json .dsh\_diag\toolslist.fixed.json
```

**要验 stdio 那条链路本身**（harness 真正用的就是它，包括"编辑器没开时垫片不退出"）：

```powershell
node .dsh\_diag\stdio-bridge-test.mjs     # ⚠️ 会 spawn 子进程，受限沙箱里 EPERM，要放开一次
```

`validate.mjs` 的校验器是**从已安装的 DSH 里现取**的，位置**自动探测**（`app.asar` 与解包版 `resources/app` 都认），
也可以用 `DSH_APP_PATH` 或第二个参数显式指定；开跑会先打印"校验器来源"。
（以前写死过第一台机器的 `app.asar` 路径 → 见 [踩坑 F10](../../docs/ai/踩坑总库.md)。）

## 文件

| 文件 | 作用 |
|---|---|
| `validate.mjs` | 用 harness 的真校验器检查工具列表。校验器**运行时**从已安装的 DSH 现取（自动探测 asar / 解包目录），所以 DSH 升级、子集变了会立刻暴露 |
| `stdio-bridge-test.mjs` | 端到端验 stdio 链路：A 真工程拿 17 个工具且 schema 已消毒；B 编辑器没开时垫片**不退出**、改为等待重试 |
| `mcp-toolslist.mjs` | 从 HTTP MCP 端点抓**原始** `tools/list` 落盘（第 1 步的推荐做法） |
| `mcp-schema.mjs` | 打印指定工具的**真实** `inputSchema`（不做消毒，用来看签名） |
| `extract.js` | 从 MCP stdio 响应流里挑出 tools/list 那一行（老办法，需要已有一份 stdio 输出） |
| `fidelity.js` | 对比消毒前后：属性数、required、additionalProperties、description 是否被改动（防止「靠掏空 schema 换通过」） |

## 什么时候要重跑

codely / Tuanjie Cowork CLI / cn.tuanjie.codely.bridge **升级之后**，
或 DSH 升级之后。任何一次都跑一遍第 2、3 步。

## 附：子集的权威定义

来自 `@deepseek-ai/dsh-tools/lib/types/json-schema.js`（不随仓库分发，运行时现取）：

```
CONSTRAINT_KEYWORDS = type, oneOf, properties, required,
                      additionalProperties, items, enum, const
ANNOTATION_KEYWORDS = description, title, default, examples
```

其余 key 一律违规，且**递归**检查。另有：根必须 `type:"object"`；
`required` 的名字必须在 `properties` 里；`additionalProperties` 必须是布尔；
`type` 只能是单个字符串；`enum`/`const` 只能用于标量。
