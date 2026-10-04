// 诊断：打印 MCP 工具的原始 inputSchema（不做消毒，用来看真实签名）
// 用法: node .dsh/_diag/mcp-schema.mjs [工具名...]
const ENDPOINT = process.env.MCP_ENDPOINT || 'http://127.0.0.1:8765/mcp';

async function post(body, sid) {
  const headers = { Accept: 'application/json, text/event-stream', 'Content-Type': 'application/json' };
  if (sid) headers['mcp-session-id'] = sid;
  const res = await fetch(ENDPOINT, { method: 'POST', headers, body: JSON.stringify(body) });
  const text = await res.text();
  const newSid = res.headers.get('mcp-session-id') || sid;
  let json = null;
  const ctype = res.headers.get('content-type') || '';
  if (ctype.includes('application/json')) { try { json = JSON.parse(text); } catch {} }
  if (!json) {
    for (const line of text.split('\n')) {
      const t = line.trim();
      const cand = t.startsWith('data:') ? t.slice(5).trim() : t;
      if (cand.startsWith('{')) { try { json = JSON.parse(cand); } catch {} }
    }
  }
  if (!json && (res.status === 202 || text.length === 0)) return { json: null, sid: newSid };
  if (!json) throw new Error('无法解析响应: ' + text.slice(0, 400));
  return { json, sid: newSid };
}

const { json: init, sid } = await post({
  jsonrpc: '2.0', id: 1, method: 'initialize',
  params: { protocolVersion: '2024-11-05', capabilities: {}, clientInfo: { name: 'schema-probe', version: '1' } },
}, null);
await post({ jsonrpc: '2.0', method: 'notifications/initialized' }, sid);

const { json } = await post({ jsonrpc: '2.0', id: 2, method: 'tools/list' }, sid);
const wanted = process.argv.slice(2);
for (const t of json.result?.tools || []) {
  if (wanted.length && !wanted.includes(t.name)) continue;
  console.log('=== ' + t.name + ' ===');
  console.log(t.description ? t.description.split('\n')[0] : '(无描述)');
  console.log(JSON.stringify(t.inputSchema, null, 2));
  console.log('');
}
