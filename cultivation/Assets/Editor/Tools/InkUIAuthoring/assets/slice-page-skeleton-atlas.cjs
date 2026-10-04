const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs');
const path=require('path');
const sharp=inkPaths.dependency('sharp');
const root=inkPaths.assetDirectory(__dirname),input=path.join(root,'transparent-page-skeleton-atlas-v1.png');
const out=path.join(root,'transparent-page-skeleton-v1');fs.mkdirSync(out,{recursive:true});
const crops={
 'parent-curtain-9slice-source':[275,0,1370,690],
 'parent-navigation-rail':[20,0,260,940],
 'nav-tab-inactive':[350,675,500,125],
 'nav-tab-active':[300,775,600,177],
 'ornate-divider':[900,675,753,125],
 'title-plaque':[900,775,753,177]
};
async function main(){
 for(const [name,[left,top,width,height]] of Object.entries(crops)) {
   console.log(name,left,top,width,height);
   try { await sharp(input).extract({left,top,width,height}).png().toFile(path.join(out,name+'.png')); }
   catch(e) { console.warn('skip '+name+': '+e.message); }
 }
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({source:path.basename(input),transparent:true,usage:'parent page skeleton; curtain is intended as 9-slice source',crops},null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
