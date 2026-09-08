import { createRequire } from 'node:module';
import { mkdirSync,writeFileSync } from 'node:fs';
const { chromium,expect: baseExpect }=createRequire('/home/ubuntu/repos/MapleStory2-Handbook/package.json')('@playwright/test');
const expect=baseExpect.configure({timeout:90000}),evidence=process.env.DYE_SURFACES_EVIDENCE??'NifToGltf/obj/outfit-studio-20260908/dye-surfaces';mkdirSync(evidence,{recursive:true});
const browser=await chromium.launch({executablePath:'/home/ubuntu/.cache/ms-playwright/chromium-1243/chrome-linux-arm64/chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1100}});page.setDefaultTimeout(90000);
const errors=[],checks=[];page.on('pageerror',e=>errors.push(e.message));await page.route('**/*',r=>['GET','HEAD'].includes(r.request().method())?r.continue():r.abort());
const ready=()=>expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();
const scene=()=>page.locator('[aria-label="Outfit preview"]');
async function check(name,result){expect(result,name).toBeTruthy();checks.push(name);console.log('PASS',name);}
async function equip(name,id){await page.getByRole('button',{name:new RegExp('^'+name+':')}).click();await page.getByRole('searchbox',{name:'Find an item',exact:true}).fill(String(id));const button=page.getByRole('button',{name:/^Equip /}).first();await expect(button).toBeEnabled();await button.click();await ready();await page.locator('.customize summary').click();}
try {
 await page.goto('http://100.118.72.53:4000/outfits',{waitUntil:'domcontentloaded'});await ready();
 await equip('Tops',12220364);
 const clothing=page.locator('.customize .dye-control').first();await clothing.getByRole('searchbox').fill("Griffin's Brown Feather");await clothing.getByRole('button',{name:"Griffin's Brown Feather",exact:true}).click();
 await check('named dye applies to clothing',await scene().evaluate(el=>JSON.stringify(el.outfitViewer.itemColorControls('12220364')[0].colors[0])===JSON.stringify([127/255,67/255,65/255])));
 await equip('Face',10300003);
 const eyes=page.locator('.customize .dye-control').first();await eyes.getByRole('searchbox').fill("Griffin's Brown Feather");await eyes.getByRole('button',{name:"Griffin's Brown Feather",exact:true}).click();
 await check('face picker applies eye-specific dye values',await scene().evaluate(el=>JSON.stringify(el.outfitViewer.itemColorControls('10300003')[0].colors[0])===JSON.stringify([140/255,76/255,74/255])));
 await page.locator('.studio-settings summary').click();const skin=page.getByRole('group',{name:'Skin colors',exact:true});
 await check('skin offers authored skin tones without hair dyes',await skin.locator('.swatch-grid button').count()===20&&await skin.getByRole('button',{name:'Skin tone 1',exact:true}).count()===1);
 await skin.getByRole('button',{name:'Skin tone 2',exact:true}).click();
 await check('skin swatch changes the actual body colors',await scene().evaluate(el=>Math.abs(el.outfitViewer.bodyColorControls[0].colors[0][0]-252/255)<1e-6));
 await skin.getByRole('radio',{name:'Custom',exact:true}).check();await skin.getByLabel('Skin Primary R',{exact:true}).fill('200');await skin.getByLabel('Skin Primary R',{exact:true}).press('Tab');
 await check('custom RGB also works for skin',await scene().evaluate(el=>Math.abs(el.outfitViewer.bodyColorControls[0].colors[0][0]-200/255)<1e-6));
 await skin.scrollIntoViewIfNeeded();await page.screenshot({path:evidence+'/skin-and-eyes.png'});
 await check('no browser errors',errors.length===0);
}catch(e){errors.push(e.stack??String(e));process.exitCode=1;await page.screenshot({path:evidence+'/failure.png',timeout:10000}).catch(()=>{});}
finally{writeFileSync(evidence+'/report.json',JSON.stringify({checks,errors},null,2));console.log(JSON.stringify({checks:checks.length,errors},null,2));await browser.close();}
