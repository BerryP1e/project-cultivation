import fs from 'node:fs';
const file=process.argv[2]||'.local/diagnostics/enclosure-20261019/exact-pose-result.json';
const d=JSON.parse(fs.readFileSync(file,'utf8'));
const leg=new Set([3,4,5,6,12,13,15,16]);
const isLeg=i=>d.bodyWeights[i].reduce((sum,b,j)=>sum+(leg.has(b)?d.weights[i][j]:0),0)>.999;
function tris(points,indices,filter){const result=[];for(let t=0;t<indices.length;t+=3){const ids=indices.slice(t,t+3);if(!ids.every(filter))continue;const p=ids.map(i=>points[i]);result.push({id:t/3,p,lo:[0,1,2].map(k=>Math.min(...p.map(v=>v[k]))),hi:[0,1,2].map(k=>Math.max(...p.map(v=>v[k])))});}return result;}
const sub=(a,b)=>a.map((x,k)=>x-b[k]),dot=(a,b)=>a.reduce((s,x,k)=>s+x*b[k],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
function hit(a,b,p){const delta=sub(b,a),e1=sub(p[1],p[0]),e2=sub(p[2],p[0]),h=cross(delta,e2),den=dot(e1,h);if(Math.abs(den)<1e-12)return null;const s=sub(a,p[0]),u=dot(s,h)/den;if(u<-.00001||u>1.00001)return null;const q=cross(s,e1),v=dot(delta,q)/den;if(v<-.00001||u+v>1.00001)return null;const t=dot(e2,q)/den;if(t<.00001||t>.99999)return null;return a.map((x,k)=>x+t*delta[k]);}
const body=tris(d.body,d.bodyFaces,isLeg),reports={};
for(const state of ['before','after'].filter(x=>d[x])){
 const cloth=tris(d[state],d.faces,i=>d.inner[i]>.5);let pairs=0;const examples=[];
 for(const a of body)for(const b of cloth){if([0,1,2].some(k=>a.lo[k]>b.hi[k]+1e-6||b.lo[k]>a.hi[k]+1e-6))continue;
  let point=null;for(let j=0;j<3&&!point;j++)point=hit(a.p[j],a.p[(j+1)%3],b.p)||hit(b.p[j],b.p[(j+1)%3],a.p);
  if(point){pairs++;if(examples.length<20)examples.push({bodyFace:a.id,clothFace:b.id,point});}
 }
 reports[state]={pairs,examples};
}
fs.writeFileSync(file.replace(/\.json$/,'.intersections.json'),JSON.stringify(reports,null,2));
console.log(JSON.stringify({file,...reports}));
