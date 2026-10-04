#!/usr/bin/env node
/**
 * validate.mjs —— 用 harness **自己**的校验器检查 MCP 工具列表是否合法。
 *
 * 为什么需要这个：dsh-mcp-client 遵循「要么完整世代，要么没有」，
 * 只要有一个工具的 inputSchema 落在「受支持 JSON Schema 子集」之外，
 * **整批工具都注册不上**（实测 codely 原始输出 0/17 通过）。
 * 这个脚本让 `unity-mcp-proxy.mjs` 的消毒逻辑可以在本地复验，
 * 不需要 spawn 进程、不需要重启 DSH。
 *
 * 校验器**不随本仓库分发**：每次运行时从已安装的 app.asar 里现取
 * `@deepseek-ai/dsh-tools/lib/types/json-schema.js`，所以 DSH 升级后
 * 这里会自动用上新的约束（子集变了会立刻暴露）。
 *
 * 用法:
 *   node validate.mjs <toolslist.json> [app.asar 路径]
 *
 * toolslist.json 可以是完整 JSON-RPC 响应，也可以只是 tools 数组。
 * 通常是这么来的：
 *   codely serve unity-mcp --stdio  ──(喂 initialize + tools/list)──> 原始响应
 *   node unity-mcp-proxy.mjs --transform <原始响应> <消毒后响应>
 *   node validate.mjs <消毒后响应>
 */

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const TARGET = '/node_modules/@deepseek-ai/dsh-tools/lib/types/json-schema.js';

const toolsFile = process.argv[2];
const 指定应用根 = process.argv[3] || process.env.DSH_APP_PATH || '';

if (!toolsFile) {
    console.error('用法: node validate.mjs <toolslist.json> [DSH 应用根]');
    console.error('  DSH 应用根 = app.asar 文件，或解包安装的 resources/app 目录');
    console.error('  不传则自动找；也可用环境变量 DSH_APP_PATH 指定');
    process.exit(2);
}

// ⚠️ 别再写死安装路径（第一台机器的 `%LOCALAPPDATA%\Programs\DSH Desktop\resources\app.asar`
// 在这台机器上不存在 —— 本机 DSH 是**解包版** `D:\dsh\DSH NEXT\resources\app\`，没有 asar）。
// 所以：显式参数 > DSH_APP_PATH > 自动探测；两种形态（asar 文件 / 解包目录）都认。
const 应用根候选 = (() => {
    const 出 = [];
    const 加 = (p) => { if (p && !出.includes(p)) 出.push(p); };
    if (指定应用根) 加(指定应用根);

    const 资源根 = [];
    const localPrograms = path.join(process.env.LOCALAPPDATA || '', 'Programs');
    try {
        for (const d of fs.readdirSync(localPrograms)) {
            if (/^dsh/i.test(d)) 资源根.push(path.join(localPrograms, d, 'resources'));
        }
    } catch { /* 没有这个目录就跳过 */ }
    for (const d of ['DSH Desktop', 'DSH NEXT', 'DSH Next']) 资源根.push(path.join(localPrograms, d, 'resources'));
    资源根.push('C:\\Program Files\\DSH Desktop\\resources');
    资源根.push('D:\\dsh\\DSH NEXT\\resources');

    for (const r of 资源根) { 加(path.join(r, 'app.asar')); 加(path.join(r, 'app')); }
    return 出;
})();

function 定位应用() {
    for (const p of 应用根候选) {
        try {
            if (!fs.existsSync(p)) continue;
            if (fs.statSync(p).isFile()) return { 形态: 'asar', 路径: p };
            const 内部 = path.join(p, ...TARGET.split('/').filter(Boolean));
            if (fs.existsSync(内部)) return { 形态: '目录', 路径: p, 内部 };
        } catch { /* 没权限/坏路径，继续找下一个 */ }
    }
    return null;
}

// ---------------------------------------------------------- 从 asar 现取校验器

function readFromAsar(asar, innerPath) {
    const fd = fs.openSync(asar, 'r');
    try {
        const head = Buffer.alloc(16);
        fs.readSync(fd, head, 0, 16, 0);
        const headerSize = head.readUInt32LE(12);       // 真 header 长度在字节 12
        const headerBuf = Buffer.alloc(headerSize);
        fs.readSync(fd, headerBuf, 0, headerSize, 16);
        const header = JSON.parse(headerBuf.toString('utf8').replace(/\0+$/, ''));
        const base = 16 + headerSize;

        let node = header.files;
        for (const seg of innerPath.split('/').filter(Boolean)) {
            if (!node || !node[seg]) return null;
            node = node[seg].files ? node[seg].files : node[seg];
        }
        if (node.offset === undefined) return null;

        const buf = Buffer.alloc(node.size);
        fs.readSync(fd, buf, 0, node.size, base + parseInt(node.offset, 10));
        return buf.toString('utf8');
    } finally {
        fs.closeSync(fd);
    }
}

const SHIM = `
export class HarnessError extends Error {
    constructor(message, code) { super(message); this.name = 'HarnessError'; this.code = code; }
}
export function assertNever(value, label) { throw new Error(\`assertNever(\${label}): \${String(value)}\`); }
export function isJsonValue(value, seen = new Set()) {
    if (value === null) return true;
    const t = typeof value;
    if (t === 'string' || t === 'boolean') return true;
    if (t === 'number') return Number.isFinite(value);
    if (t !== 'object') return false;
    if (seen.has(value)) return false;
    seen.add(value);
    try {
        if (Array.isArray(value)) return value.every((v) => isJsonValue(v, seen));
        const proto = Object.getPrototypeOf(value);
        if (proto !== null && proto !== Object.prototype) return false;
        return Object.values(value).every((v) => isJsonValue(v, seen));
    } finally { seen.delete(value); }
}
`;

async function loadValidator(应用) {
    const source = 应用.形态 === 'asar'
        ? readFromAsar(应用.路径, TARGET)
        : fs.readFileSync(应用.内部, 'utf8');
    if (!source) throw new Error(`${应用.路径} 里找不到 ${TARGET}（DSH 版本变了？）`);

    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dsh-jsonschema-'));
    const patched = source
        .replace(/from '@deepseek-ai\/dsh-llm'/g, "from './shim.mjs'")
        .replace(/from '@deepseek-ai\/dsh-util-values'/g, "from './shim.mjs'");
    if (/from '@deepseek-ai\//.test(patched)) throw new Error('还有未替换的 bare import，垫片需要更新');

    fs.writeFileSync(path.join(dir, 'shim.mjs'), SHIM, 'utf8');
    fs.writeFileSync(path.join(dir, 'json-schema.mjs'), patched, 'utf8');
    return import(pathToFileURL(path.join(dir, 'json-schema.mjs')).href);
}

// ------------------------------------------------------------------ 跑校验

const 应用 = 定位应用();
if (!应用) {
    console.error('找不到 DSH 应用（校验器要从它里面现取）。找过这些位置：');
    for (const p of 应用根候选) console.error('  ' + p);
    console.error('请显式指定：node validate.mjs <toolslist.json> <app.asar 或 resources/app 目录>');
    process.exit(3);
}
console.log(`校验器来源：${应用.形态} —— ${应用.路径}`);

let mod;
try {
    mod = await loadValidator(应用);
} catch (e) {
    console.error(`无法加载校验器: ${e.message}`);
    process.exit(3);
}

const raw = JSON.parse(fs.readFileSync(toolsFile, 'utf8'));
const tools = raw && raw.result && Array.isArray(raw.result.tools) ? raw.result.tools
    : Array.isArray(raw) ? raw : null;
if (!tools) { console.error('tools 数组没找到'); process.exit(2); }

let failed = 0;
for (const t of tools) {
    const problems = [];
    // 1) 整棵树必须在受支持子集内
    try { mod.assertSupportedJsonSchema(t.inputSchema); }
    catch (e) { problems.push('subset: ' + (e.violations ?? [e.message]).join(' | ')); }
    // 2) 工具入口 schema 的根必须是 type:"object"
    try { mod.assertObjectJsonSchema(t.inputSchema); }
    catch (e) { problems.push('object-root: ' + (e.violations ?? [e.message]).join(' | ')); }

    if (problems.length) {
        failed++;
        console.log(`FAIL  ${t.name}`);
        for (const p of problems) console.log(`        ${p}`);
    } else {
        console.log(`ok    ${t.name}`);
    }
}

console.log(`\n${tools.length - failed}/${tools.length} 通过`);
process.exit(failed ? 1 : 0);
