const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs'),path=require('path');
const sharp=inkPaths.dependency('sharp');
const generated=inkPaths.sourceDirectory();
const root=inkPaths.assetDirectory(__dirname),out=path.join(root,'spec-v1','cultivation-alchemy');
fs.mkdirSync(out,{recursive:true});
const files={
 'furnace-stage':['exec-e0968124-a5a4-451f-9ba5-bd3b157fbec1.png',1600,1200,'furnace-stage.png'],
 'material-slot-empty':['exec-0ef026e1-4738-4c4e-933c-b26444dc5174.png',400,264,'material-slot-empty.png'],
 'cultivation-frame':['exec-1a4af8b3-b044-4f9b-96de-04b34c660167.png',2048,2048,'cultivation-frame.png'],
 'ink-ring':['exec-8efd4ea3-67ca-459c-ae9f-cf707b9d5df1.png',1400,1400,'ink-ring.png'],
 'gongfa-card':['exec-b6afdad4-d6e8-43ca-bfeb-6e6a9bc0de33.png',1024,1024,'gongfa-card.png']
};
async function render(id){
 const [,w,h,name]=files[id];
 return sharp(path.join(generated,files[id][0])).resize({width:w,height:h,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).ensureAlpha().png().toFile(path.join(out,name)).then(()=>path.join(out,name));
}
async function rawOf(file){return sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true})}
async function variant(base,target,fn){const {data,info}=await rawOf(base);for(let i=0;i<data.length;i+=4){if(!data[i+3])continue;const [r,g,b]=fn(data[i],data[i+1],data[i+2]);data[i]=Math.max(0,Math.min(255,Math.round(r)));data[i+1]=Math.max(0,Math.min(255,Math.round(g)));data[i+2]=Math.max(0,Math.min(255,Math.round(b)));}return sharp(data,{raw:info}).png().toFile(target)}
const blend=(c,f,b=1)=>(r,g,bl)=>[(r*(1-f)+c[0]*f)*b,(g*(1-f)+c[1]*f)*b,(bl*(1-f)+c[2]*f)*b];
async function main(){
 const base={};for(const id of Object.keys(files))base[id]=await render(id);
 await variant(base['material-slot-empty'],path.join(out,'material-slot-selected.png'),blend([82,111,92],.38,1.06));
 await variant(base['material-slot-empty'],path.join(out,'material-slot-filled.png'),blend([82,111,92],.10,.88));
 await variant(base['material-slot-empty'],path.join(out,'material-slot-disabled.png'),(r,g,b)=>{const v=(r+g+b)/3*.58;return[v,v,v]});
 for(const state of ['normal','hover','selected']){
  const src=path.join(root,'spec-v1','skills-page','skills','row-'+state+'.png');
  await sharp(src).resize({width:720,height:64,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png().toFile(path.join(out,'recipe-row-'+state+'.png'));
 }
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({version:'素材规格与提示词 v1',transparentPng32:true,dimensions:{'furnace-stage':'1600x1200','material-slot-*':'400x264','cultivation-frame':'2048x2048','ink-ring':'1400x1400','gongfa-card':'1024x1024','recipe-row-*':'720x64'},nineSlice:{'cultivation-frame':[96,96,96,96],'material-slot-*':[32,32,32,32],'gongfa-card':[48,48,48,48],'recipe-row-*':[24,8,24,8]}},null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
