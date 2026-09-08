import { createRequire } from 'node:module';
import { mkdirSync,writeFileSync } from 'node:fs';
const { chromium,expect: baseExpect }=createRequire('/home/ubuntu/repos/MapleStory2-Handbook/package.json')('@playwright/test');
const expect=baseExpect.configure({timeout:90000});
const directory=process.env.COLOR_PERF_EVIDENCE??'NifToGltf/obj/outfit-studio-20260908/color-performance-before';mkdirSync(directory,{recursive:true});
const browser=await chromium.launch({executablePath:'/home/ubuntu/.cache/ms-playwright/chromium-1243/chrome-linux-arm64/chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1100}});page.setDefaultTimeout(90000);
await page.route('**/*',r=>['GET','HEAD'].includes(r.request().method())?r.continue():r.abort());
const reports=[];
try{
 await page.goto('http://100.118.72.53:4000/outfits',{waitUntil:'domcontentloaded'});await expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();
 for(const id of (process.env.COLOR_PERF_IDS??'10200070,10200010,10200124').split(',').map(Number)){
  await page.getByRole('button',{name:/^Hair:/}).click();await page.getByRole('searchbox').fill(String(id));const button=page.getByRole('button',{name:/^Equip /}).first();await expect(button).toBeEnabled();await button.click();await expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();
  const report=await page.locator('[aria-label="Outfit preview"]').evaluate(async (el,id)=>{
   const viewer=el.outfitViewer;viewer.playing=false;
   const control=viewer.itemColorControls(String(id))[0];const saved=control.colors.map(c=>[...c]);
   const textures=()=>{const list=[];viewer.scene.traverse(n=>{if(n.isMesh)for(const m of Array.isArray(n.material)?n.material:[n.material])if(m.map?.image?.getContext)list.push(m.map.image);});return [...new Set(list)];};
   control.set(1,[0,0,0]);const images=textures(),original=images.map(c=>c.getContext('2d').getImageData(0,0,c.width,c.height).data);
   const channelChanges=[];
   for(const channel of [0,1,2]){
    control.set(channel,[0,0,0]);const before=images.map(c=>c.getContext('2d').getImageData(0,0,c.width,c.height).data);
    control.set(channel,[1,1,1]);const after=images.map(c=>c.getContext('2d').getImageData(0,0,c.width,c.height).data);
    channelChanges.push(after.reduce((sum,bytes,i)=>sum+bytes.reduce((count,v,n)=>count+Number(v!==before[i][n]),0),0));
   }
   const times=[];for(let i=0;i<12;i++){const start=performance.now();control.set(0,[i/12,0.3,0.6]);times.push(performance.now()-start);}
   control.setColors(saved);
   const referenceTimes=[];
   if (id === 10200124) {
     const { bakeColors }=await import('/src/lib/outfits/materialColors.ts');
     const sources=[];
     for (const part of viewer.equippedItems.find(bundle=>bundle.item.id===id).parts) {
       const gltf=await fetch(part.url).then(r=>r.json());
       const texture=async index=>{
         const entry=gltf.textures[index], sampler=gltf.samplers?.[entry.sampler]??{};
         const uri=gltf.images[entry.source].uri;
         const image=await createImageBitmap(await fetch(new URL(uri,part.url)).then(r=>r.blob()));
         const canvas=document.createElement('canvas');canvas.width=image.width;canvas.height=image.height;const ctx=canvas.getContext('2d');ctx.drawImage(image,0,0);image.close();
         return {pixels:ctx.getImageData(0,0,canvas.width,canvas.height),mode:{linear:sampler.magFilter!==9728,wrapS:(sampler.wrapS??10497)===10497,wrapT:(sampler.wrapT??10497)===10497}};
       };
       for (const material of gltf.materials??[]) {
         const info=material.extras;
         if (!info?.nifBaseColorTexture || !info?.nifColorControlTexture) continue;
         sources.push([await texture(info.nifBaseColorTexture.index),await texture(info.nifColorControlTexture.index)]);
       }
     }
     for (let i=0;i<3;i++) { const start=performance.now(); for (const [d,c] of sources) bakeColors(d.pixels,c.pixels,saved,d.mode,c.mode);referenceTimes.push(performance.now()-start); }
   }
   return {id,name:control.label,times,referenceTimes,channelChanges,textures:images.map(c=>[c.width,c.height]),activeChannels:control.activeChannels??null};
  },id);reports.push(report);console.log(JSON.stringify(report));
 }
}finally{writeFileSync(directory+'/report.json',JSON.stringify(reports,null,2));await browser.close();}
