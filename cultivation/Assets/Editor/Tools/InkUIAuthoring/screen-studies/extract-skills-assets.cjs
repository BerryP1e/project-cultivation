const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs');
const path=require('path');
const sharp=inkPaths.dependency('sharp');
const root=inkPaths.assetDirectory(__dirname);
const input=path.join(root,'skills-scroll-passives-v3-clean.png');
const out=path.join(root,'skills-assets-v1');
fs.mkdirSync(out,{recursive:true});
const crops={
  '00-full-clean': [0,0,1653,952],
  '01-parent-curtain-and-frame': [150,55,1360,800],
  '02-parent-navigation': [155,85,185,735],
  '03-active-ring': [500,70,465,400],
  '04-learned-ability-library': [325,455,445,390],
  '05-enabled-passive-list': [770,455,350,390],
  '06-ability-detail-panel': [1110,115,400,735],
  '07-hud-and-active-slots': [105,815,900,137],
  '08-library-scrollbar': [735,515,24,310],
  '09-passive-scrollbar': [1090,515,24,310],
  '10-active-slot-1': [610,82,115,130],
  '11-active-slot-2': [785,82,115,130],
  '12-active-slot-3': [850,200,115,140],
  '13-active-slot-4': [780,320,120,135],
  '14-active-slot-5': [600,320,120,135],
  '15-active-slot-6': [520,200,120,140]
};
async function main(){
  for(const [name,[left,top,width,height]] of Object.entries(crops)){
    await sharp(input).extract({left,top,width,height}).png().toFile(path.join(out,name+'.png'));
  }
  const manifest={source:path.basename(input),generatedAt:new Date().toISOString(),note:'合成稿的第一轮功能裁片；含纸底与原画纹理，非最终透明通道素材。',crops};
  fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify(manifest,null,2),'utf8');
}
main().catch(err=>{console.error(err);process.exit(1)});
