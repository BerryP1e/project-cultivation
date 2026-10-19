import fs from 'node:fs';
import {spawnSync} from 'node:child_process';
const folder=process.argv[2]||'.local/diagnostics/enclosure-20261019/motion';
const reports=[];
for(const file of fs.readdirSync(folder).filter(x=>/-frame\d+\.json$/.test(x))){
 const full=`${folder}/${file}`,data=JSON.parse(fs.readFileSync(full,'utf8'));
 const r=spawnSync(process.execPath,['devtools/validation/outfit-skirt-intersections.mjs',full],{encoding:'utf8'});
 if(r.status!==0)throw new Error(r.stderr);
 const intersections=JSON.parse(r.stdout).after;
 const c=spawnSync(process.execPath,['devtools/validation/outfit-skirt-containment.mjs',full],{encoding:'utf8'});
 if(c.status!==0)throw new Error(c.stderr);
 const rows=JSON.parse(fs.readFileSync(full.replace(/\.json$/,'.containment.json'),'utf8')).after.rows;
 const middle=rows.filter(r=>r.z>=.20&&r.z<=.39);
 reports.push({file,pairs:intersections.pairs,poseDrift:data.drift,middleTested:middle.reduce((s,r)=>s+r.tested,0),middleOutside:middle.reduce((s,r)=>s+r.outside,0),missingMiddleLoop:middle.filter(r=>!r.closedLoops).length});
}
const result={samples:reports.length,intersectionPairs:reports.reduce((s,r)=>s+r.pairs,0),midSkirtHeight:[.20,.39],containmentSamples:reports.reduce((s,r)=>s+r.middleTested,0),outside:reports.reduce((s,r)=>s+r.middleOutside,0),missingMiddleLoops:reports.reduce((s,r)=>s+r.missingMiddleLoop,0),maxPoseDrift:Math.max(...reports.map(r=>r.poseDrift)),reports};
fs.writeFileSync(`${folder}/final-geometry-results.json`,JSON.stringify(result,null,2));
const {reports:details,...summary}=result;console.log(JSON.stringify(summary,null,2));
if(result.samples!==48||result.intersectionPairs||result.outside||result.missingMiddleLoops||result.maxPoseDrift>.0001)process.exitCode=1;
