const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),cp=require('node:child_process');
const inkPaths=require('./paths.cjs'),repo=inkPaths.repository,kit=inkPaths.toolkit,art=inkPaths.authoring;
const walk=d=>fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);
const read=f=>fs.readFileSync(f,'utf8').replace(/^\uFEFF/,'');const sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
let errors=[],checks=0;
const manifest=JSON.parse(read(path.join(kit,'relocation-manifest.json')));
for(const r of manifest.files){const file=path.join(repo,r.new);if(!fs.existsSync(file))errors.push('Missing '+r.new);else if(/\.(png|zip|bak)$/.test(r.new)&&sha(file)!==r.sha256)errors.push('Bytes changed '+r.new);checks++;}
for(const file of walk(kit).filter(f=>/\.(cjs|js)$/.test(f))){const p=cp.spawnSync(process.execPath,['--check',file],{encoding:'utf8'});if(p.status!==0)errors.push(p.stderr);checks++;}
for(const file of walk(kit).filter(f=>/\.(html|css)$/.test(f))){const text=read(file);const refs=[...text.matchAll(/(?:src|href)=["']([^"']+)["']|url\(["']?([^)'"\s]+)["']?\)/g)];
 for(const m of refs){const value=(m[1]||m[2]).split('#')[0];if(!value||value.includes('${')||value.endsWith('-')||/^(https?:|data:|javascript:|%23)/.test(value))continue;const target=path.resolve(path.dirname(file),value);if(!fs.existsSync(target))errors.push('Missing preview reference '+path.relative(repo,file)+' -> '+value);checks++;}
}
const active=['docs/INDEX.md','docs/PROGRESS.md','docs/guides/UI换皮.md','docs/architecture/UI现状原理图.md','docs/architecture/战阵真灵.md','docs/architecture/主动技能与神通.md',...walk(path.join(repo,'docs/design/UI重制')).map(f=>path.relative(repo,f)),...walk(path.join(repo,'docs/reference/UI素材')).map(f=>path.relative(repo,f))];
for(const rel of active){const file=path.join(repo,rel);for(const m of read(file).matchAll(/\]\(([^)]+)\)/g)){const value=m[1].replace(/^<|>$/g,'').split('#')[0];if(!value||/^(https?:|app:)/.test(value))continue;if(!fs.existsSync(path.resolve(path.dirname(file),value)))errors.push('Broken doc link '+rel+' -> '+value);checks++;}}
for(const pack of ['ability-art-v1','dynamic-assets-v1'])for(const item of JSON.parse(read(path.join(art,pack,'generation-map.json')))){if(!fs.existsSync(path.join(art,pack,item.Source)))errors.push('Missing generation input '+item.Source);checks++;}
const images=walk(art).filter(f=>f.endsWith('.png'));
const runtime=walk(path.join(repo,'cultivation/Assets/resources/UI/InkUI')).filter(f=>!f.endsWith('.meta'));
const result={checks,errors,authoringPng:images.length,runtimeFiles:runtime.length,runtimeHashes:runtime.map(f=>({file:path.relative(repo,f).replaceAll('\\','/'),sha256:sha(f)}))};
fs.mkdirSync(path.join(repo,'cultivation/screenshots'),{recursive:true});fs.writeFileSync(path.join(repo,'cultivation/screenshots/InkUI-organization-verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify({...result,runtimeHashes:undefined},null,2));if(errors.length)process.exitCode=1;
