// 诊断：本机 HTTP 端点到底通不通（Node 侧）
// 用法: node .dsh/_diag/mcp-http-probe.mjs [url...]
const urls = process.argv.slice(2);
if (urls.length === 0) urls.push('http://127.0.0.1:8765/mcp', 'http://127.0.0.1:62707/');

function 展开(e) {
  const parts = [];
  let cur = e;
  for (let i = 0; i < 5 && cur; i++) {
    parts.push(`${cur.name || 'Error'}: ${cur.message}${cur.code ? ' [' + cur.code + ']' : ''}${cur.errno !== undefined ? ' errno=' + cur.errno : ''}${cur.syscall ? ' syscall=' + cur.syscall : ''}${cur.address ? ' addr=' + cur.address + ':' + cur.port : ''}`);
    cur = cur.cause;
  }
  return parts;
}

for (const url of urls) {
  try {
    const res = await fetch(url, { method: 'GET' });
    console.log(`✅ ${url} → HTTP ${res.status}`);
  } catch (e) {
    console.log(`❌ ${url}`);
    for (const line of 展开(e)) console.log('     ' + line);
  }
}

// 再试一次原生 http 模块（有些环境 fetch/undici 被拦而 net 允许）
import http from 'node:http';
for (const url of urls) {
  await new Promise((resolve) => {
    const u = new URL(url);
    const req = http.request({ host: u.hostname, port: u.port, path: u.pathname, method: 'GET', timeout: 4000 }, (res) => {
      console.log(`✅ http模块 ${url} → ${res.statusCode}`);
      res.resume(); resolve();
    });
    req.on('timeout', () => { console.log(`❌ http模块 ${url} → timeout`); req.destroy(); resolve(); });
    req.on('error', (e) => { console.log(`❌ http模块 ${url} → ${e.code || e.message}`); resolve(); });
    req.end();
  });
}
