// .dsh/mcp-call.mjs —— 直接调 Unity MCP 的 HTTP 端点（不依赖 harness 注册）
//
// 用法:
//   node .dsh/mcp-call.mjs list
//   node .dsh/mcp-call.mjs call <工具名> '<json参数>'
//
// 为什么要这个脚本：
//   .dsh/mcp.ps1 用 Invoke-WebRequest，在 DSH 沙箱里会撞
//   「读取控制台输出缓冲区时出现 Win32 内部错误 Access is denied」，
//   而 Node 的 http 模块走正常 socket，不受影响。
//
// 前置：MCP 服务器要在跑 —— `node .dsh/mcp-serve.mjs` 或
//       `codely serve unity-mcp --http --http-port 8765 --unity-project-path <工程>`

const ENDPOINT = process.env.MCP_ENDPOINT || 'http://127.0.0.1:8765/mcp';

async function post(body, sid) {
  const headers = {
    'Accept': 'application/json, text/event-stream',
    'Content-Type': 'application/json',
  };
  if (sid) headers['mcp-session-id'] = sid;

  const res = await fetch(ENDPOINT, { method: 'POST', headers, body: JSON.stringify(body) });
  const text = await res.text();
  const newSid = res.headers.get('mcp-session-id') || sid;
  const ctype = res.headers.get('content-type') || '';

  // 两种响应形态都要认：
  //   ① content-type: application/json    → 整个 body 就是一条 JSON
  //   ② text/event-stream（SSE）           → `data: {...}` 行，取最后一条
  let json = null;
  if (ctype.includes('application/json')) {
    try { json = JSON.parse(text); } catch {}
  }
  if (!json) {
    for (const line of text.split('\n')) {
      const t = line.trim();
      const cand = t.startsWith('data:') ? t.slice(5).trim() : t;
      if (cand.startsWith('{')) { try { json = JSON.parse(cand); } catch {} }
    }
  }
  // 通知类请求（notifications/initialized）返回 202 + 空 body —— 这是正常的，不算错
  if (!json && (res.status === 202 || text.length === 0)) return { json: null, sid: newSid };
  if (!json) throw new Error('无法解析响应 (content-type=' + ctype + ', len=' + text.length + '):\n' + text.slice(0, 800));
  return { json, sid: newSid };
}

async function 握手() {
  const init = await post({
    jsonrpc: '2.0', id: 1, method: 'initialize',
    params: {
      protocolVersion: '2024-11-05', capabilities: {},
      clientInfo: { name: 'dsh-mcp-call', version: '1.0' },
    },
  }, null);
  const sid = init.sid;
  await post({ jsonrpc: '2.0', method: 'notifications/initialized' }, sid);
  return sid;
}

const [cmd, tool, argsJson] = process.argv.slice(2);

// 参数也可以从文件读（`@路径`）—— PowerShell 转义 JSON 太痛，写文件更可靠
async function 取参数() {
  if (!argsJson) return {};
  const fs = await import('node:fs');
  if (argsJson.startsWith('@')) return JSON.parse(fs.readFileSync(argsJson.slice(1), 'utf8'));
  return JSON.parse(argsJson);
}

try {
  const sid = await 握手();

  if (cmd === 'list') {
    const { json } = await post({ jsonrpc: '2.0', id: 2, method: 'tools/list' }, sid);
    const tools = json.result?.tools || [];
    console.log(`共 ${tools.length} 个工具:`);
    for (const t of tools) console.log(`  ${t.name}`);
  } else if (cmd === 'call') {
    if (!tool) throw new Error('缺工具名');
    const { json } = await post({
      jsonrpc: '2.0', id: 3, method: 'tools/call',
      params: { name: tool, arguments: await 取参数() },
    }, sid);
    const texts = (json.result?.content || []).filter(c => c.type === 'text').map(c => c.text);
    console.log(texts.length ? texts.join('\n') : JSON.stringify(json, null, 2));
  } else {
    console.log('用法: node .dsh/mcp-call.mjs list | call <工具名> \'<json>\' | call <工具名> @参数文件.json');
  }
} catch (e) {
  console.error('❌ ' + e.message);
  process.exit(1);
}
