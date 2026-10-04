// 把原始 tools/list 响应落盘，供 unity-mcp-proxy.mjs --selftest / --transform 使用
// 用法: node .dsh/_diag/mcp-toolslist.mjs [输出路径]
import fs from 'node:fs';
const ENDPOINT = process.env.MCP_ENDPOINT || 'http://127.0.0.1:8765/mcp';
const out = process.argv[2] || '.dsh/_diag/toolslist.json';

async function post(body, sid) {
  const headers = { Accept: 'application/json, text/event-stream', 'Content-Type': 'application/json' };
  if (sid) headers['mcp-session-id'] = sid;
  const res = await fetch(ENDPOINT, { method: 'POST', headers, body: JSON.stringify(body) });
  const text = await res.text();
  const newSid = res.headers.get('mcp-session-id') || sid;
  let json = null;
  if ((res.headers.get('content-type') || '').includes('application/json')) { try { json = JSON.parse(text); } catch {} }
  if (!json) {
    for (const line of text.split('\n')) {
      const t = line.trim();
      const cand = t.startsWith('data:') ? t.slice(5).trim() : t;
      if (cand.startsWith('{')) { try { json = JSON.parse(cand); } catch {} }
    }
  }
  return { json, sid: newSid };
}

const { sid } = await post({
  jsonrpc: '2.0', id: 1, method: 'initialize',
  params: { protocolVersion: '2024-11-05', capabilities: {}, clientInfo: { name: 'toolslist-dump', version: '1' } },
}, null);
await post({ jsonrpc: '2.0', method: 'notifications/initialized' }, sid);
const { json } = await post({ jsonrpc: '2.0', id: 2, method: 'tools/list' }, sid);
fs.writeFileSync(out, JSON.stringify(json, null, 2), 'utf8');
console.log(`已写入 ${out}（工具 ${(json.result?.tools || []).length} 个）`);
