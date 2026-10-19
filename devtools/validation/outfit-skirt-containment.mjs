import fs from 'node:fs';
const folder='.local/diagnostics/enclosure-20261019';
const file=process.argv[2]||`${folder}/exact-pose-result.json`;
const d=JSON.parse(fs.readFileSync(file,'utf8'));
const leg=new Set([3,4,5,6,12,13,15,16]);
const isLeg=i=>d.bodyWeights[i].reduce((sum,b,j)=>sum+(leg.has(b)?d.weights[i][j]:0),0)>.999;
const bf=[];for(let t=0;t<d.bodyFaces.length;t+=3)if(d.bodyFaces.slice(t,t+3).every(isLeg))bf.push(d.bodyFaces.slice(t,t+3));
const cf=[];for(let t=0;t<d.faces.length;t+=3)if(d.faces.slice(t,t+3).every(i=>d.inner[i]>.5))cf.push(d.faces.slice(t,t+3));
const key=p=>p.map(x=>Math.round(x*1e5)).join(',');
function slice(points,faces,z){
 const segments=new Map();
 for(const f of faces){const hits=[];for(let e=0;e<3;e++){
  const a=points[f[e]],b=points[f[(e+1)%3]];if((a[2]<z)!==(b[2]<z)){
   const t=(z-a[2])/(b[2]-a[2]);hits.push([a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])]);
  }
 }if(hits.length===2&&key(hits[0])!==key(hits[1]))segments.set(hits.map(key).sort().join('|'),hits);}
 return [...segments.values()];
}
function distance(p,a,b){const v=[b[0]-a[0],b[1]-a[1]],q=[p[0]-a[0],p[1]-a[1]],t=Math.max(0,Math.min(1,(q[0]*v[0]+q[1]*v[1])/(v[0]*v[0]+v[1]*v[1])));return Math.hypot(q[0]-t*v[0],q[1]-t*v[1]);}
function contains(p,segments){let inside=false,min=Infinity;for(const [a,b] of segments){min=Math.min(min,distance(p,a,b));if((a[1]>p[1])!==(b[1]>p[1])&&p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0])inside=!inside;}return {inside:inside||min<.0005,distance:min};}
function loops(segments){
 const cross=(a,b)=>a[0]*b[1]-a[1]*b[0],sub=(a,b)=>[a[0]-b[0],a[1]-b[1]];
 const splits=segments.map(()=>[0,1]);
 for(let i=0;i<segments.length;i++)for(let j=i+1;j<segments.length;j++){
  const [a,b]=segments[i],[c,d]=segments[j],u=sub(b,a),v=sub(d,c),den=cross(u,v);if(Math.abs(den)<1e-12)continue;
  const w=sub(c,a),t=cross(w,v)/den,s=cross(w,u)/den;
  if(t>1e-6&&t<1-1e-6&&s>=-1e-6&&s<=1+1e-6)splits[i].push(t);
  if(s>1e-6&&s<1-1e-6&&t>=-1e-6&&t<=1+1e-6)splits[j].push(s);
 }
 const adj=new Map(),coords=new Map();
 segments.forEach(([a,b],i)=>{const ts=[...new Set(splits[i])].sort((x,y)=>x-y);for(let j=1;j<ts.length;j++){
  const p=[a[0]+ts[j-1]*(b[0]-a[0]),a[1]+ts[j-1]*(b[1]-a[1])],q=[a[0]+ts[j]*(b[0]-a[0]),a[1]+ts[j]*(b[1]-a[1])],x=key(p),y=key(q);if(x===y)continue;
  coords.set(x,p);coords.set(y,q);for(const [x1,y1] of [[x,y],[y,x]]){if(!adj.has(x1))adj.set(x1,new Set());adj.get(x1).add(y1);}
 }});
 const ordered=new Map([...adj].map(([a,ns])=>[a,[...ns].sort((x,y)=>{const p=coords.get(a),u=coords.get(x),v=coords.get(y);return Math.atan2(u[1]-p[1],u[0]-p[0])-Math.atan2(v[1]-p[1],v[0]-p[0]);})]));
 const visited=new Set(),polys=[];
 for(const [a,ns] of ordered)for(const b of ns){if(visited.has(`${a}|${b}`))continue;const polygon=[];let u=a,v=b;
  for(let step=0;step<segments.length*8+10;step++){
   const k=`${u}|${v}`;if(visited.has(k))break;visited.add(k);polygon.push(coords.get(u));
   const nn=ordered.get(v),next=nn[(nn.indexOf(u)+nn.length-1)%nn.length];u=v;v=next;
   if(u===a&&v===b)break;
  }
  let area=0;for(let i=0;i<polygon.length;i++)area+=cross(polygon[i],polygon[(i+1)%polygon.length]);
  if(Math.abs(area)>.0002)polys.push(polygon.map((p,i)=>[p,polygon[(i+1)%polygon.length]]));
 }
 return polys;
}
const report={};
for(const state of ['before','after'].filter(k=>d[k])){
 const rows=[];
 for(let z=.09;z<.45;z+=.012){
  const skirt=slice(d[state],cf,z),body=slice(d.body,bf,z);
  const polygons=loops(skirt);
  let outside=0,worst=0,tested=0;
  for(const [a,b] of body)for(const t of [0,.5,1]){const p=[a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])],rs=polygons.map(poly=>contains(p,poly));tested++;if(!rs.some(r=>r.inside)){outside++;worst=Math.max(worst,...rs.map(r=>r.distance));}}
  const graph=new Map();for(const s of skirt)for(const p of s){const k=key(p);graph.set(k,(graph.get(k)||0)+1);}
  const adj=new Map();for(const [a,b] of skirt)for(const [p,q] of [[a,b],[b,a]]){const k=key(p);if(!adj.has(k))adj.set(k,new Set());adj.get(k).add(key(q));}
  const seen=new Set(),components=[];
  for(const a of adj.keys()){if(seen.has(a))continue;const stack=[a],group=[];while(stack.length){const v=stack.pop();if(seen.has(v))continue;seen.add(v);group.push(v);stack.push(...[...adj.get(v)].filter(x=>!seen.has(x)));}const verts=group.map(k=>k.split(',').map(x=>Number(x)/1e5));components.push({vertices:group.length,closed:group.every(k=>adj.get(k).size===2),bounds:[0,1].map(k=>[Math.min(...verts.map(p=>p[k])),Math.max(...verts.map(p=>p[k]))])});}
  const areas=polygons.map(poly=>Math.abs(poly.reduce((s,[a,b])=>s+a[0]*b[1]-a[1]*b[0],0))*.5);
  rows.push({z,skirtSegments:skirt.length,closedLoops:polygons.length,areas,openEnds:[...graph.values()].filter(n=>n===1).length,components,bodySegments:body.length,tested,outside,worst});
 }
 const closed=rows.filter(r=>r.closedLoops>0);
 report[state]={outside:rows.reduce((s,r)=>s+r.outside,0),tested:rows.reduce((s,r)=>s+r.tested,0),worst:Math.max(...rows.map(r=>r.worst)),closedOutside:closed.reduce((s,r)=>s+r.outside,0),closedTested:closed.reduce((s,r)=>s+r.tested,0),closedRows:closed.length,rows};
}
fs.writeFileSync(file.replace(/\.json$/,'.containment.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({file,...Object.fromEntries(Object.entries(report).map(([k,v])=>[k,{outside:v.outside,tested:v.tested,worst:v.worst,closedOutside:v.closedOutside,closedTested:v.closedTested,closedRows:v.closedRows,closedFailedHeights:v.rows.filter(r=>r.outside&&r.openEnds===0).map(r=>r.z)}]))}));
