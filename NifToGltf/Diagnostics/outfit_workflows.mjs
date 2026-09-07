import {createRequire} from 'node:module';import {writeFileSync,mkdirSync} from 'node:fs';
const evidence = process.env.WARDROBE_EVIDENCE_DIR ?? 'NifToGltf/obj/wardrobe/ui-workflows-14';
mkdirSync(evidence,{recursive:true});
const preview = process.env.WARDROBE_PREVIEW_URL ?? 'http://127.0.0.1:4002';
const {chromium,expect:baseExpect}=createRequire(process.env.HANDBOOK_FRONTEND+'/package.json')('@playwright/test');
const expect=baseExpect.configure({timeout:60000});
const browser=await chromium.launch({headless:true,executablePath:process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1000}});page.setDefaultTimeout(60000);
const errors=[],blocked=[],states=[];page.on('pageerror',e=>errors.push(e.message));
await page.route('**/*',r=>['GET','HEAD'].includes(r.request().method())?r.continue():(blocked.push(r.request().url()),r.abort()));
const controls=()=>page.locator('[aria-label="Outfit preview"]');
async function equip(id){
 console.log('Equip',id);
 await page.getByRole('searchbox').fill(String(id));
 const button=page.getByRole('button',{name:/^Equip /}).filter({hasText:String(id)}).first();
 await expect(button).toBeEnabled();await button.click();
 await expect.poll(async()=>controls().evaluate((e,id)=>e.outfitViewer.inspect().equipped.some(i=>i.id===id),id),{timeout:60000}).toBe(true);
}
try{
 await page.goto(preview+'/outfits',{waitUntil:'domcontentloaded',timeout:60000});
 await expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();
 await page.locator('select').filter({has:page.locator('option[value="verified"]')}).selectOption('preview');
 for(const body of (process.env.RUN_ONLY_PRIVATE_PREVIEW==='1'?[]:['female','male'])){
  await page.locator('select').filter({has:page.locator('option[value="male"]')}).selectOption(body);
  await expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();
  const ids=[body==='female'?10300003:10300001,body==='female'?10200124:10200121,11400367,11500004,11600004,11700004,11200001,11300001];
  for(const id of ids)await equip(id);
  const makeup=body==='female'?10400011:10400002;await equip(makeup);
  await page.locator('select').filter({has:page.locator('option[value="happy"]')}).selectOption('happy');
  await controls().evaluate(e=>{e.outfitViewer.selectClip('fitting_idle_a');e.outfitViewer.seek(.65);});
  for(const angle of ['Front','Side','Back']){await page.getByRole('button',{name:angle,exact:true}).click();await controls().screenshot({path:`${evidence}/${body}-complete-${angle.toLowerCase()}.png`});}
  writeFileSync(evidence+'/workflow-dom-'+body+'.txt',await page.locator('body').innerText());
  let before=await controls().evaluate(e=>e.outfitViewer.inspect());
  await page.getByLabel(/Makeup placement/).selectOption('1');
  await page.getByRole('button',{name:'Reset makeup',exact:true}).click();
  const hairState=await controls().evaluate(e=>{const c=e.outfitViewer.colorControls.find(c=>c.shader.includes('Hair'));if(!c)throw Error('Missing hair dye control');return {label:c.label,colors:structuredClone(c.colors)};});
  if(!(await page.getByText('Colors',{exact:true}).evaluate(e=>e.closest('details').open)))await page.getByText('Colors',{exact:true}).click();
  const hairPalette=page.getByLabel(hairState.label+' palette',{exact:true});
  await hairPalette.selectOption('1');
  const dyed=await controls().evaluate((e,label)=>structuredClone(e.outfitViewer.colorControls.find(c=>c.label===label).colors),hairState.label);
  if(JSON.stringify(dyed)===JSON.stringify(hairState.colors))throw Error('Hair palette did not change');
  const primary=page.getByRole('group',{name:hairState.label,exact:true}).getByLabel('Primary',{exact:true});
  await primary.fill('#123456');await primary.blur();await expect(primary).toHaveValue('#123456');
  const manualDye=await controls().evaluate((e,label)=>structuredClone(e.outfitViewer.colorControls.find(c=>c.label===label).colors[0]),hairState.label);
  expect(manualDye).toEqual([0x12/255,0x34/255,0x56/255]);
  await equip(11300002);await equip(11300001);
  expect(await controls().evaluate((e,label)=>e.outfitViewer.colorControls.find(c=>c.label===label).colors[0],hairState.label)).toEqual(manualDye);
  await page.getByRole('group',{name:hairState.label,exact:true}).getByRole('button',{name:'Reset colors',exact:true}).click();
  const reset=await controls().evaluate((e,label)=>e.outfitViewer.colorControls.find(c=>c.label===label).colors,hairState.label);
  if(reset.some((c,i)=>c.some((v,j)=>Math.abs(v-hairState.colors[i][j])>1e-6)))throw Error('Hair reset lost source defaults');
  states.push({body,hairPalette:{id:'1',initial:hairState.colors,dyed,manualDye,reset},hatReplacement:[11300002,11300001]});
  await equip(12200002);
  let robe=await controls().evaluate(e=>e.outfitViewer.inspect());
  if(robe.equipped.some(i=>[11400367,11500004].includes(i.id)))throw Error('Full outfit did not replace top and pants');
  await equip(11500004);
  if((await controls().evaluate(e=>e.outfitViewer.inspect())).equipped.some(i=>i.id===12200002))throw Error('Pants did not evict full outfit');
  await equip(11400367);
  const download=page.waitForEvent('download');await page.getByRole('button',{name:'Save image',exact:true}).click();await(await download).saveAs(`${evidence}/${body}-export.png`);
  await expect(page.getByRole('alert')).toHaveCount(0);
  states.push({body,ids:[...ids,makeup],pose:'fitting_idle_a',time:.65,expression:'happy',background:'studio',before,robe,after:await controls().evaluate(e=>e.outfitViewer.inspect())});
  await equip(11050055);
  const animation=page.locator('select').filter({has:page.locator('option[value="Attack_Idle_A"]')});
  for(const name of ['Idle_A','Attack_Idle_A']){
    await animation.selectOption(name);
    await controls().evaluate(e=>{e.outfitViewer.selectExpression('happy');e.outfitViewer.seek(.65);});
    const selected=await controls().evaluate(e=>e.outfitViewer.equipmentAnimationControls.map(c=>({label:c.label,current:c.current})));
    if(!selected.some(c=>c.current===name))throw Error('Source equipment clip was not selected');
    await page.getByRole('button',{name:'Front',exact:true}).click();
    await controls().screenshot({path:`${evidence}/${body}-nose-${name}.png`});
    states.push({body,itemId:11050055,equipmentAnimation:name,selected,time:.65});
  }
  await equip(11304858);
  const paletteStates=[];
  for(const time of [.1,.6]){
    const state=await controls().evaluate((e,time)=>{const v=e.outfitViewer;v.seek(time);const name=v.equippedItems.find(b=>b.item.id===11304858).item.name;return v.colorControls.filter(c=>c.label===name).map(c=>structuredClone(c.colors));},time);
    if(!state.length)throw Error('Headphone palette control missing');
    paletteStates.push(state);await controls().screenshot({path:`${evidence}/${body}-headphones-${time}.png`});
  }
  if(JSON.stringify(paletteStates[0])===JSON.stringify(paletteStates[1]))throw Error('Source colorDelay cycle did not advance');
  states.push({body,itemId:11304858,times:[.1,.6],paletteStates});
  await equip(13400306);
  await page.getByRole('button',{name:/^Equip .* in left hand$/}).first().click();
  await expect.poll(async()=>(await controls().evaluate(e=>e.outfitViewer.inspect())).equipped.filter(i=>i.id===13400306).length).toBe(2);
  await controls().evaluate(e=>{e.outfitViewer.selectClip('star_attack_idle_a');e.outfitViewer.seek(.65);});
  for(const placement of ['stowed','drawn']){
    await page.getByRole('button',{name:placement==='stowed'?'Stow weapons':'Draw weapons',exact:true}).click();
    await expect.poll(async()=>controls().evaluate((e,p)=>e.outfitViewer.equippedItems.filter(b=>b.item.id===13400306).every(b=>b.weaponPlacement===p),placement)).toBe(true);
    await page.getByRole('button',{name:placement==='stowed'?'Back':'Front',exact:true}).click();
    await controls().screenshot({path:`${evidence}/${body}-stars-${placement}.png`});
    states.push({body,itemId:13400306,hands:['RH','LH'],placement,pose:'star_attack_idle_a',time:.65,state:await controls().evaluate(e=>e.outfitViewer.inspect())});
  }
  const hair=await controls().evaluate(e=>e.outfitViewer.hairControls.map(c=>({label:c.label,values:c.values,value:c.value})));
  for(const control of hair){
    const selector=page.locator('label').filter({hasText:control.label}).locator('select').filter({has:page.locator('option:disabled')}).first();
    for(const value of [control.values[0],control.values.at(-1)]){
      await selector.selectOption(String(value));
      await controls().screenshot({path:`${evidence}/${body}-hair-${hair.indexOf(control)}-${value}.png`});
    }
    await page.getByRole('button',{name:'Reset '+control.label,exact:true}).click();
    await expect.poll(async()=>controls().evaluate((e,label)=>e.outfitViewer.hairControls.find(c=>c.label===label).value,control.label)).toBe(0);
  }
  states.push({body,hairLengthControls:hair});

 }
 if(process.env.RUN_PRIVATE_PREVIEW!=='0'){
 await page.goto(preview+'/outfits?preview=gelo-07',{waitUntil:'domcontentloaded'});
 await expect(page.getByRole('button',{name:'Save image',exact:true})).toBeEnabled();
 await expect.poll(async()=>controls().evaluate(e=>e.outfitViewer.inspect().equipped.length),{timeout:60000}).toBe(12);
 await controls().evaluate(e=>{e.outfitViewer.selectClip('star_attack_idle_a');e.outfitViewer.selectExpression('happy');e.outfitViewer.seek(.65);});
 await controls().screenshot({path:evidence+'/gelo-07-regression.png'});
 const gelo=await controls().evaluate(e=>e.outfitViewer.inspect());states.push({privateRegression:true,state:gelo});
 }
 if(errors.length)throw Error(errors.join('\n'));console.log(JSON.stringify({outfits:states.length,errors}));
}finally{writeFileSync(evidence+'/workflow-last-dom.txt',await page.locator('body').innerText());await page.screenshot({path:evidence+'/workflow-last.png',fullPage:true});writeFileSync(evidence+'/outfit-workflows.json',JSON.stringify({states,errors,blocked},null,2));await browser.close();}
