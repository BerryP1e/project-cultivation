// 把一段 C# 脚本文件包成 exec_runtime_script / exec_editor_script 的参数 JSON
// 用法: node .dsh/_diag/mkargs.mjs <脚本.cs.txt> <输出.json> "<summary>" [tool]
//   tool = runtime（默认）| editor
//
// 为什么不直接用 PowerShell 的 ConvertTo-Json：它会把字符串包成
// `{"value":"...","Count":...}` 这种对象，服务端直接报
// `script: Expected string, received object`（实测踩过）。
import fs from 'node:fs';

const [csPath, outPath, summary, tool = 'runtime'] = process.argv.slice(2);
if (!csPath || !outPath) {
    console.error('用法: node .dsh/_diag/mkargs.mjs <脚本.cs.txt> <输出.json> "<summary>" [runtime|editor]');
    process.exit(2);
}

const script = fs.readFileSync(csPath, 'utf8');
const args = tool === 'editor'
    ? { script, summary: summary || '编辑模式脚本', capture_logs: true, timeoutSeconds: 300 }
    : { script, summary: summary || '运行时脚本', capture_logs: true, timeoutSeconds: 300 };

fs.writeFileSync(outPath, JSON.stringify(args, null, 2), 'utf8');
console.log(`已写入 ${outPath}（script ${script.length} 字符 → ${tool}）`);
