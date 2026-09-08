import {createRequire} from 'node:module';
import {mkdirSync,writeFileSync} from 'node:fs';
const {chromium,expect:baseExpect}=createRequire('/home/ubuntu/repos/MapleStory2-Handbook/package.json')('@playwright/test');
const expect=baseExpect.configure({timeout:90000}),dir=process.env.MAKEUP_LIGHTING_EVIDENCE??'NifToGltf/obj/outfit-studio-20260908/makeup-lighting-after';mkdirSync(dir,{recursive:true});
const browser=await chromium.launch({executablePath:'/home/ubuntu/.cache/ms-playwright/chromium-1243/chrome-linux-arm64/chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1100}});page.setDefaultTimeout(90000);const errors=[];page.on('pageerror',e=>errors.push(e.message));
try{await page.goto('http://100.118.72.53:4000/outfits');await expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();await page.getByRole('button',{name:/^Makeup:/}).click();await page.getByRole('searchbox',{name:'Find an item',exact:true}).fill('10400011');await page.getByRole('button',{name:/^Equip /}).first().click();await expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();
const result=await page.locator('[aria-label="Outfit preview"]').evaluate(el=>{const v=el.outfitViewer;v.playing=false;v.seek(.3);const c=v.decal.control;const original=c.colors.map(rgb=>[...rgb]);const lights=[];v.scene.traverse(n=>{if(n.isLight)lights.push([n,n.intensity]);});
const draw=()=>{v.renderer.render(v.scene,v.camera);const canvas=document.createElement('canvas');canvas.width=v.renderer.domElement.width;canvas.height=v.renderer.domElement.height;const ctx=canvas.getContext('2d');ctx.drawImage(v.renderer.domElement,0,0);return ctx.getImageData(0,0,canvas.width,canvas.height).data;};
const changes=[];
for(const mode of ['ambient','direct','studio']){for(const [light,intensity] of lights)light.intensity=mode==='ambient'&&light.isDirectionalLight||mode==='direct'&&light.isAmbientLight?0:intensity;
c.setColors([[0,0,0],[0,0,0],[0,0,0]]);const black=draw();c.setColors([[1,1,1],[1,1,1],[1,1,1]]);const white=draw();let changed=0,max=0;for(let i=0;i<black.length;i+=4){const d=Math.max(...[0,1,2].map(c=>Math.abs(black[i+c]-white[i+c])));if(d>2)changed++;max=Math.max(max,d);}changes.push({mode,changed,max});}
for(const [light,intensity]of lights)light.intensity=intensity;c.setColors(original);v.renderer.render(v.scene,v.camera);return {changes,alphaMax:Math.max(...v.decal.texture.image.data.filter((_,i)=>i%4===3))};});
await page.locator('.fitting-room').screenshot({path:dir+'/heart.png'});writeFileSync(dir+'/report.json',JSON.stringify({result,errors},null,2));console.log(JSON.stringify({result,errors},null,2));if(!process.env.MAKEUP_LIGHTING_BASELINE){expect(result.changes.every(c=>c.changed>10&&c.max>20)).toBe(true);expect(errors).toEqual([]);}
}finally{await browser.close();}
