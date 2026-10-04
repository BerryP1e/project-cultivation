const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs'),path=require('path');
const sharp=inkPaths.dependency('sharp');
const generated=inkPaths.sourceDirectory();
const out=path.join(inkPaths.assetDirectory(__dirname),'spec-v1','icons');fs.mkdirSync(out,{recursive:true});
const sources={
 'icon-inventory':'exec-4b98b7a3-e1d8-442f-8fff-a44067217795.png',
 'icon-realm':'exec-4d07cbf0-83d9-4cba-ab56-9f48f6876deb.png',
 'icon-divine-ability':'exec-0e1c3761-0753-4d92-b127-c87ae3100a3e.png',
 'icon-treasure':'exec-4c7b06b1-ccd3-466d-b7e8-f1ef2f6d32eb.png',
 'icon-spirit-array':'exec-07a2627c-b4ff-434d-a0c0-76993be4757d.png',
 'icon-formation':'exec-1f3da19d-0c20-436e-9b6a-55809465f532.png',
 'icon-mount':'exec-12354f43-1a52-490a-8ce4-e4281dd96fc2.png',
 'icon-appearance':'exec-9262174f-263d-4a9e-a0ec-cec27dd8b12b.png'
};
async function main(){for(const [name,file] of Object.entries(sources))await sharp(path.join(generated,file)).resize({width:256,height:256,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).ensureAlpha().png().toFile(path.join(out,name+'.png'));fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({transparentPng32:true,size:'256x256',names:Object.keys(sources),note:'图标仅作初版风格与功能占位，真实数据 DisplayIcon 可替换。'},null,2))}
main().catch(e=>{console.error(e);process.exit(1)});
