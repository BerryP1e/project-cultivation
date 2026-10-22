// Read-only local Markdown target audit. Run from any directory with Node.
import { readdir, readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
async function walk(dir){
 const entries=await readdir(dir,{withFileTypes:true});
 const groups=await Promise.all(entries.map(e=>e.isDirectory()?walk(path.join(dir,e.name)):e.name.endsWith('.md')?[path.join(dir,e.name)]:[]));
 return groups.flat();
}
const files=(await Promise.all(['docs','devtools'].map(p=>walk(path.join(root,p))))).flat();
let links=0;const missing=[];
for(const file of files){
 const source=(await readFile(file,'utf8')).replace(/```[^]*?```/g,m=>m.replace(/[^\n]/g,' ')).replace(/`[^`\n]*`/g,'');
 for(const m of source.matchAll(/!?\[[^\]\n]*\]\((<[^>]+>|[^\s)]+)(?:\s+"[^"]*")?\)/g)){
  const target=m[1].replace(/^<|>$/g,'');if(/^(?:[a-z]+:|#)/i.test(target))continue;
  const raw=decodeURIComponent(target.split('#')[0]);if(!raw)continue;links++;
  const full=path.resolve(path.dirname(file),raw);
  try{await stat(full);}catch{missing.push(`${path.relative(root,file).replaceAll('\\','/')}:${source.slice(0,m.index).split('\n').length} -> ${target}`);}
 }
}
console.log(`Checked ${files.length} Markdown files and ${links} local links; ${missing.length} missing targets.`);
for(const item of missing)console.log(item);
if(missing.length)process.exitCode=1;
