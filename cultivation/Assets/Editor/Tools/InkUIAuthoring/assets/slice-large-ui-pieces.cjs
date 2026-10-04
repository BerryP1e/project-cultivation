const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs'),path=require('path');
const sharp=inkPaths.dependency('sharp');
const root=inkPaths.assetDirectory(__dirname),input=path.join(root,'large-ui-pieces-atlas-v1.png');
const out=path.join(root,'large-ui-pieces-v1');fs.mkdirSync(out,{recursive:true});
const crops={
 'left-parent-navigation-panel':[10,5,280,884],
 'learned-ability-library-panel':[295,100,955,730],
 'ability-detail-panel':[1250,15,505,870]
};
async function main(){
 for(const [name,[left,top,width,height]] of Object.entries(crops))
  await sharp(input).extract({left,top,width,height}).png().toFile(path.join(out,name+'.png'));
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({source:path.basename(input),transparent:true,note:'三块完整大面板皮肤；内部纸面/玉面是组件材质，外部透明。',crops},null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
