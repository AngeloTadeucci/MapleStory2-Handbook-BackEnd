import {createRequire} from 'node:module';
import {mkdirSync,writeFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
const {chromium,expect:baseExpect}=createRequire('/home/ubuntu/repos/MapleStory2-Handbook/package.json')('@playwright/test');
const expect=baseExpect.configure({timeout:90000});
const browser=await chromium.launch({executablePath:'/home/ubuntu/.cache/ms-playwright/chromium-1243/chrome-linux-arm64/chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1100}});page.setDefaultTimeout(90000);
const dir='NifToGltf/obj/outfit-studio-20260908/makeup-debug';mkdirSync(dir,{recursive:true});
const ready=()=>expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();
async function capture(name){const state=await page.locator('[aria-label="Outfit preview"]').evaluate(el=>{const v=el.outfitViewer;v.playing=false;v.seek(.3);v.renderer.render(v.scene,v.camera);const uniforms=[];v.scene.traverse(n=>{if(n.isMesh)for(const m of Array.isArray(n.material)?n.material:[n.material]){const p=v.renderer.properties.get(m);if(p.uniforms?.faceDecal)uniforms.push({name:m.name,texture:p.uniforms.faceDecal.value.uuid,value:p.uniforms.faceDecalTransform.value.toArray()});}});return {control:v.makeupControls?.value,texture:v.decal?.texture.uuid,uniforms};});const screenshot=await page.locator('[aria-label="Outfit preview"] canvas').screenshot({path:dir+'/'+name+'.png'});return {name,...state,hash:createHash('sha256').update(screenshot).digest('hex')};}
try{await page.goto('http://100.118.72.53:4000/outfits');await ready();await page.getByRole('button',{name:/^Makeup:/}).click();const report=[];
for(const id of [10400011,10400012]){await page.getByRole('searchbox',{name:'Find an item',exact:true}).fill(String(id));await page.getByRole('button',{name:/^Equip /}).first().click();await ready();report.push(await capture(String(id)+'-first'));if(!await page.locator('.customize').evaluate(el=>el.open))await page.locator('.customize summary').click();await page.getByLabel('Makeup placement',{exact:true}).selectOption('1');report.push(await capture(String(id)+'-second'));}
writeFileSync(dir+'/report.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));}finally{await browser.close();}
