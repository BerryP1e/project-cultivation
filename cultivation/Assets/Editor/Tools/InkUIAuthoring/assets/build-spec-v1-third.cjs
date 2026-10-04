const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs'),path=require('path');
const sharp=inkPaths.dependency('sharp');
const generated=inkPaths.sourceDirectory();
const root=inkPaths.assetDirectory(__dirname),out=path.join(root,'spec-v1','common-panels');
fs.mkdirSync(out,{recursive:true});
const files={
 'plot-tag':['exec-2496ac5f-7ca0-491e-ba81-4f73bf8f2401.png',1000,1140,'plot-tag.png'],
 'dialog-scroll':['exec-0abb9ff5-e57a-417e-91ff-ffe4ec6409d1.png',1024,512,'dialog-scroll.png'],
 'dialog-nameplate':['exec-434fdb95-0b03-4286-bd17-470a3ea06e27.png',480,112,'dialog-nameplate.png'],
 'dialog-choice-normal':['exec-08663ee1-9bbe-4374-86ac-975f70358fbf.png',720,148,'dialog-choice-normal.png'],
 'toast':['exec-6b32af37-63c4-4993-a785-6548e1c28b88.png',680,104,'toast.png'],
 'tower-floor-bar':['exec-bbcf74a8-c760-4ab4-b87f-1e8792d8c285.png',1800,80,'tower-floor-bar.png'],
 'teleport-plate':['exec-02fd4bc0-0d7f-4edb-9a7d-ee5df9f5076d.png',1024,1024,'teleport-plate.png'],
 'title-plaque':['exec-5fbe03a5-b01f-4540-8310-14f3470568d7.png',1024,240,'title-plaque.png']
};
async function render(id){const [,w,h,name]=files[id];return sharp(path.join(generated,files[id][0])).resize({width:w,height:h,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).ensureAlpha().png().toFile(path.join(out,name)).then(()=>path.join(out,name))}
async function rawOf(file){return sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true})}
async function variant(base,target,fn){const {data,info}=await rawOf(base);for(let i=0;i<data.length;i+=4){if(!data[i+3])continue;const [r,g,b]=fn(data[i],data[i+1],data[i+2]);data[i]=Math.max(0,Math.min(255,Math.round(r)));data[i+1]=Math.max(0,Math.min(255,Math.round(g)));data[i+2]=Math.max(0,Math.min(255,Math.round(b)));}return sharp(data,{raw:info}).png().toFile(target)}
const blend=(c,f,b=1)=>(r,g,bl)=>[(r*(1-f)+c[0]*f)*b,(g*(1-f)+c[1]*f)*b,(bl*(1-f)+c[2]*f)*b];
async function main(){
 const base={};for(const id of Object.keys(files))base[id]=await render(id);
 await variant(base['dialog-choice-normal'],path.join(out,'dialog-choice-hover.png'),(r,g,b)=>[r*1.08,g*1.08,b*1.08]);
 await variant(base['dialog-choice-normal'],path.join(out,'dialog-choice-selected.png'),blend([82,111,92],.24,1.02));
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({version:'素材规格与提示词 v1',transparentPng32:true,dimensions:{'plot-tag':'1000x1140','dialog-scroll':'1024x512','dialog-nameplate':'480x112','dialog-choice-*':'720x148','toast':'680x104','tower-floor-bar':'1800x80','teleport-plate':'1024x1024','title-plaque':'1024x240'},nineSlice:{'plot-tag':[48,48,48,48],'dialog-scroll':[48,32,48,32],'dialog-nameplate':[24,16,24,16],'dialog-choice-*':[32,16,32,16],'toast':[24,16,24,16],'tower-floor-bar':[24,12,24,12],'teleport-plate':[48,48,48,48],'title-plaque':[48,48,48,48]}},null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
