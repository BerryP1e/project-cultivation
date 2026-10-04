const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs');
const path=require('path');
const sharp=inkPaths.dependency('sharp');
const root=inkPaths.assetDirectory(__dirname);
const input=path.join(root,'transparent-ui-atlas-v1.png');
const out=path.join(root,'transparent-ui-v1');
fs.mkdirSync(out,{recursive:true});
const crops={
  'active-slot-frame':[25,10,510,560],
  'parent-navigation-tab':[510,0,950,225],
  'ability-library-card':[510,220,950,315],
  'enabled-passive-row':[510,525,950,235],
  'scrollbar-track-and-thumb':[1300,35,180,650],
  'primary-action-button':[15,680,815,200],
  'secondary-action-button':[870,680,660,200]
};
async function main(){
 for(const [name,[left,top,width,height]] of Object.entries(crops)){
  console.log(name,left,top,width,height);
  try { await sharp(input).extract({left,top,width,height}).trim({background:{r:0,g:0,b:0,alpha:0}}).png().toFile(path.join(out,name+'.png')); }
  catch(e) { console.warn('skip '+name+': '+e.message); }
 }
 const manifest={source:path.basename(input),transparent:true,note:'外部透明；卡片和列表内部的纸色是皮肤本体，不是误抠背景。',crops};
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify(manifest,null,2),'utf8');
}
main().catch(e=>{console.error(e);process.exit(1)});
