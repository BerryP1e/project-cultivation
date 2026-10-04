const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs'),path=require('path');
const sharp=inkPaths.dependency('sharp');
const generated=inkPaths.sourceDirectory();
const root=inkPaths.assetDirectory(__dirname);
const out=path.join(root,'spec-v1','skills-page');
const dirs=['common','navigation','skills','status'];
for(const d of dirs)fs.mkdirSync(path.join(out,d),{recursive:true});
const files={
 'panel-sheet':['exec-c0eee237-2e85-4969-b81f-1e8ddd094f28.png',2048,2048,'common','panel-sheet.png'],
 'panel-inner':['exec-d034fc34-05cc-482f-a7a0-ffb8da250663.png',1024,1024,'common','panel-inner.png'],
 'nav-rail':['exec-f14645e8-eac7-4c93-9bcd-93c653ebf5a3.png',512,2048,'navigation','nav-rail.png'],
 'tab-normal':['exec-a2608f56-2170-47b4-b107-f62927714053.png',960,216,'navigation','tab-normal.png'],
 'row-normal':['exec-bec17297-190c-46fa-8ffe-54f81db533a5.png',1024,136,'skills','row-normal.png'],
 'active-ring':['exec-348633d3-f04f-4206-ac4f-0488a98a467a.png',1400,1400,'skills','active-ring.png'],
 'slot-empty':['exec-dc977179-c2d7-4d8c-b8b6-ab134df62544.png',336,336,'skills','slot-empty.png'],
 'panel-detail':['exec-23a9f11c-7126-4ed9-b47d-50679a25787b.png',1024,2048,'skills','panel-detail.png'],
 'badge-tier-jade':['exec-aee5bf8e-b7df-4ca5-a222-a5b3b3e2aed3.png',512,144,'skills','badge-tier-jade.png'],
 'scroll-track':['exec-a7e5d764-66d2-4120-9189-28009ff38262.png',64,128,'status','scroll-track.png'],
 'scroll-thumb':['exec-6effefa3-a1d5-461a-b5be-96fe1998ce65.png',64,64,'status','scroll-thumb.png'],
 'bar-track':['exec-463dda0e-0791-4208-84a1-3a983b154d1b.png',1024,64,'status','bar-track.png'],
 'bar-fill':['exec-f2549084-2340-41e1-bf38-977a3a734e61.png',64,64,'status','bar-fill.png']
};
const sourcePath=id=>path.join(generated,files[id][0]);
async function render(id){
 const [,w,h,folder,name]=files[id];
 const target=path.join(out,folder,name);
 await sharp(sourcePath(id)).resize({width:w,height:h,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).ensureAlpha().png().toFile(target);
 return target;
}
async function rawOf(file){
 return sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
}
async function variant(base,target,fn){
 const {data,info}=await rawOf(base);
 for(let i=0;i<data.length;i+=4){const a=data[i+3];if(!a)continue;let [r,g,b]=fn(data[i],data[i+1],data[i+2]);data[i]=Math.max(0,Math.min(255,Math.round(r)));data[i+1]=Math.max(0,Math.min(255,Math.round(g)));data[i+2]=Math.max(0,Math.min(255,Math.round(b)));}
 await sharp(data,{raw:info}).png().toFile(target);
}
const brighten=f=> (r,g,b)=>[r*f,g*f,b*f];
const blend=(c,f,bright=1)=>(r,g,b)=>[r*(1-f)+c[0]*f,g*(1-f)+c[1]*f,b*(1-f)+c[2]*f].map(x=>x*bright);
async function main(){
 const bases={};for(const id of Object.keys(files))bases[id]=await render(id);
 await variant(bases['tab-normal'],path.join(out,'navigation','tab-hover.png'),brighten(1.08));
 await variant(bases['tab-normal'],path.join(out,'navigation','tab-active.png'),blend([82,111,92],.28,1.03));
 await variant(bases['row-normal'],path.join(out,'skills','row-hover.png'),brighten(1.06));
 await variant(bases['row-normal'],path.join(out,'skills','row-selected.png'),blend([82,111,92],.20,1.02));
 await variant(bases['slot-empty'],path.join(out,'skills','slot-hover.png'),brighten(1.12));
 await variant(bases['slot-empty'],path.join(out,'skills','slot-filled.png'),blend([82,111,92],.18,.92));
 await variant(bases['badge-tier-jade'],path.join(out,'skills','badge-tier-cinnabar.png'),blend([158,78,61],.50,1.0));
 await variant(bases['bar-fill'],path.join(out,'status','bar-fill.png'),()=>[82,111,92]);
 const manifest={version:'素材规格与提示词 v1',transparentPng32:true,generatedAt:new Date().toISOString(),folders:{common:'panel-sheet.png, panel-inner.png',navigation:'nav-rail.png, tab-normal.png, tab-hover.png, tab-active.png',skills:'row-normal.png, row-hover.png, row-selected.png, active-ring.png, slot-empty.png, slot-hover.png, slot-filled.png, panel-detail.png, badge-tier-jade.png, badge-tier-cinnabar.png',status:'scroll-track.png, scroll-thumb.png, bar-track.png, bar-fill.png'},nineSlice:{'panel-sheet':[96,96,96,96],'panel-inner':[48,48,48,48],'nav-rail':[24,24,24,24],'tab-*':[24,8,24,8],'row-*':[32,8,32,8],'panel-detail':[64,64,64,64],'badge-tier-*':[24,8,24,8],'scroll-track':[8,8,8,8],'scroll-thumb':[12,12,12,12],'bar-track':[12,8,12,8]}};
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify(manifest,null,2),'utf8');
}
main().catch(e=>{console.error(e);process.exit(1)});
