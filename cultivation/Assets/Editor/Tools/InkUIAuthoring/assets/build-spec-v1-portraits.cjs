const inkPaths=require('./../paths.cjs');
'use strict';
const fs=require('fs'),path=require('path');
const sharp=inkPaths.dependency('sharp');
const generated=inkPaths.sourceDirectory();
const out=path.join(inkPaths.assetDirectory(__dirname),'spec-v1','portraits');fs.mkdirSync(out,{recursive:true});
const sources={
 '大师兄':'exec-68eb7443-80d9-4dca-8c1f-29dc9c41307c.png',
 '村民':'exec-4e1a211e-f17d-4869-a3be-4961046217da.png',
 '成男村民':'exec-9747ba7b-9b09-477a-a9b3-95f738cb5678.png',
 '成女村民':'exec-a0c4a8b9-1370-4685-abd5-58d1ddaa76b7.png',
 '老头村民':'exec-08627b7a-7546-49c4-816f-22c46ed95205.png',
 '胖叔村民':'exec-8ae4cb3d-1014-4e0c-8376-279c64bde3b0.png',
 '小梅':'exec-f0725ab2-e217-4ca9-acbe-c7b436674e5c.png',
 '正太村民':'exec-aebad655-f5e7-44cf-9c23-2689d59b23e0.png'
};
async function main(){for(const [id,file] of Object.entries(sources))await sharp(path.join(generated,file)).resize({width:1144,height:1614,fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).ensureAlpha().png().toFile(path.join(out,'portrait-'+id+'.png'));fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({transparentPng32:true,size:'1144x1614',sourceIds:Object.keys(sources),unityResourceName:'Resources/立绘/<id>.png',note:'本目录文件名按规格为 portrait-<id>.png；导入 Unity Resources/立绘时去掉 portrait- 前缀。'},null,2))}
main().catch(e=>{console.error(e);process.exit(1)});
