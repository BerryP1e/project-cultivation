// 验证 .dsh/unity-mcp-proxy.mjs 的 stdio 链路（harness 走的就是这条）
// 用法: node .dsh/_diag/stdio-bridge-test.mjs
//
// A 用例：真工程（不给参数 = 走垫片默认值，和 harness 一样）→ 期望拿到 17 个工具的 tools/list
// B 用例：假工程 + 必然失败的上游 → 期望**代理不退出**，改为等待重试（这正是编辑器没开时的现场）
//
// ⚠️ 这个脚本自己 spawn 子进程，受限沙箱里会 EPERM，需要放开一次。
import { spawn } from 'node:child_process';
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.dirname(path.dirname(HERE));
const PROXY = path.join(ROOT, '.dsh', 'unity-mcp-proxy.mjs');
const FAKE_PROJECT = path.join(ROOT, '.dsh', '_diag', 'fake-project');

const HANDSHAKE = [
    JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2024-11-05', capabilities: {}, clientInfo: { name: 'stdio-bridge-test', version: '1' } } }),
    JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }),
    JSON.stringify({ jsonrpc: '2.0', id: 2, method: 'tools/list' }),
].join('\n') + '\n';

const 起代理 = (args) => spawn(process.execPath, [PROXY, ...args], { stdio: ['pipe', 'pipe', 'pipe'] });

async function 用例A() {
    console.log('=== A. 真工程：stdio 链路能不能拿到 tools/list ===');
    const p = 起代理([]); // 不给参数 = 走垫片默认值，和 harness 的调用方式一致
    let stdout = '';
    let stderr = '';
    p.stdout.on('data', (d) => { stdout += d.toString(); });
    p.stderr.on('data', (d) => { stderr += d.toString(); });

    p.stdin.write(HANDSHAKE);

    const 结果 = await new Promise((resolve) => {
        const 截止 = Date.now() + 40000;
        const t = setInterval(() => {
            if (stdout.includes('"tools"')) { clearInterval(t); resolve('有响应'); }
            else if (p.exitCode !== null) { clearInterval(t); resolve('代理已退出 code=' + p.exitCode); }
            else if (Date.now() > 截止) { clearInterval(t); resolve('超时'); }
        }, 300);
    });

    let 工具数 = 0;
    for (const line of stdout.split('\n')) {
        const t = line.trim();
        if (!t.startsWith('{')) continue;
        try {
            const m = JSON.parse(t);
            if (m.id === 2 && m.result && Array.isArray(m.result.tools)) 工具数 = m.result.tools.length;
        } catch { /* 忽略非 JSON 行 */ }
    }
    const 残留schema关键字 = /"\$schema"|"anyOf"|"\$ref"/.test(stdout);
    console.log(`  结果: ${结果}`);
    console.log(`  tools/list 工具数: ${工具数}`);
    console.log(`  响应里还有 harness 不支持的 schema 关键字吗: ${残留schema关键字 ? '❌ 有（消毒没生效）' : '✅ 没有'}`);
    if (工具数 === 0) {
        console.log('  --- 代理 stderr（前 12 行）---');
        for (const l of stderr.split('\n').slice(0, 12)) if (l.trim()) console.log('  | ' + l.trim());
    }
    try { p.kill(); } catch { /* 已退出 */ }
    return 工具数 === 17 && !残留schema关键字;
}

async function 用例B() {
    console.log('');
    console.log('=== B. 编辑器没开（假工程 + 必然失败的上游）：代理应当等待重试而不是退出 ===');
    fs.mkdirSync(FAKE_PROJECT, { recursive: true });

    // argv[0] 给一个必然失败的"上游"（node 把 serve 当脚本名，立刻报错退出）
    const p = 起代理([process.execPath, FAKE_PROJECT]);
    let stderr = '';
    let 退出码 = null;
    p.stderr.on('data', (d) => { stderr += d.toString(); });
    p.on('exit', (c) => { 退出码 = c; });
    p.stdin.write(HANDSHAKE);

    await new Promise((r) => setTimeout(r, 9000));

    const 重试次数 = (stderr.match(/上游没起来/g) || []).length;
    const 端口提示 = (stderr.match(/端口文件还没出现/g) || []).length;
    console.log(`  9 秒内：重试日志 ${重试次数} 次 / 端口文件提示 ${端口提示} 次 / 进程 ${退出码 === null ? '还活着 ✅' : '退出码 ' + 退出码 + ' ❌'}`);
    const 关键行 = stderr.split('\n').filter((l) => /上游没起来|端口文件还没出现|spawning/.test(l)).slice(0, 6);
    for (const l of 关键行) console.log('  | ' + l.trim());

    try { p.kill(); } catch { /* 已退出 */ }
    return 退出码 === null && 重试次数 >= 2;
}

const a = await 用例A();
const b = await 用例B();
console.log('');
console.log(`A（真工程 stdio 链路）= ${a ? '✅ 通过' : '❌ 不通过'}`);
console.log(`B（编辑器没开时不退出）= ${b ? '✅ 通过' : '❌ 不通过'}`);
process.exit(a && b ? 0 : 1);
