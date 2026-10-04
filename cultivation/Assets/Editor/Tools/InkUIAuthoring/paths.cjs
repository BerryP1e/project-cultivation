const path = require('node:path');
const fs = require('node:fs');
const toolkit = __dirname;
const project = path.resolve(toolkit, '../../../..');
const repository = path.dirname(project);
const authoring = path.join(project, 'Assets/UIResources/InkUI/Authoring');
function assetDirectory(toolDirectory) {
    return path.join(authoring, path.relative(toolkit, toolDirectory));
}
function dependency(name) {
    try { return require(name); } catch (original) {
        const configured = process.env.INKUI_NODE_MODULES;
        const local = path.join(process.env.USERPROFILE || '', '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules');
        const location = configured || local;
        if (!fs.existsSync(path.join(location, name))) throw original;
        return require(path.join(location, name));
    }
}
function sourceDirectory() {
    return process.env.INKUI_GENERATED_SOURCE || path.join(authoring, 'GeneratedSources');
}
function document(file) {
    const relative = path.relative(authoring, file);
    const target = path.join(repository, 'docs/reference/UI素材/工具导出', relative);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    return target;
}
module.exports = { toolkit, project, repository, authoring, assetDirectory, dependency, sourceDirectory, document };
