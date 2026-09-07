// Run after pnpm check in /outfits?hairPreview=sassy with the read-only liveRows snapshot.
// EIIie's saved hair dye is a regression fixture here, not a claim she wears Sassy.
(async () => {
  const result = window.sassyExport = { running: true, checks: [], images: {}, states: [] };
  const check = (name, value) => { result.checks.push({ name, passed: Boolean(value) }); if (!value) throw new Error(name); };
  try {
    const { parseNativeManifest } = await import('/src/lib/nativeAssets.ts');
    const { catalogSchema, resolveBundle } = await import('/src/lib/outfits/catalog.ts');
    const { applySassyPreview, loadSassyPreview } = await import('/src/lib/outfits/sassyPreview.ts');
    const { joinCatalog } = await import('/src/lib/outfits/search.ts');
    const { isSkinColor } = await import('/src/lib/outfits/skinColors.ts');
    const { bakeColors } = await import('/src/lib/outfits/materialColors.ts');
    const { NearestFilter, RepeatWrapping, Vector3 } = await import('/node_modules/.vite/deps/three.js');
    const base = '/gltf/simulator-release-14/';
    const url = new URL(base + 'native-manifest.json', location.href).href;
    const assets = [...parseNativeManifest(await (await fetch(url)).json(), url), ...await loadSassyPreview(fetch, location.href)];
    const catalog = catalogSchema.parse(await (await fetch(base + 'simulator-catalog.json')).json());
    const items = joinCatalog(applySassyPreview(catalog.items, assets), []);
    const viewer = document.querySelector('[aria-label="Outfit preview"]').outfitViewer;
    const equip = async id => { const item = items.find(i => i.id === id && i.gender === 1); check('item available ' + id, item?.library.availability !== 'unavailable' && item); await viewer.equipBundle(resolveBundle(item, assets, 'female')); };
    const hair = () => viewer.equippedItems.find(b => b.item.id === 10200010);
    const placements = () => viewer.hairPlacementControls;
    const saved = window.liveRows?.find(r => r.kind === 'item' && r.name === 'EIIie' && r.slot === 1 && r.appearance?.Color);
    check('read-only saved dye fixture exists', saved);
    const dye = ['Primary', 'Secondary', 'Tertiary'].map(k => ['Red', 'Green', 'Blue'].map(c => saved.appearance.Color[k][c] / 255));
    result.dyeFixture = { character: 'EIIie', itemId: saved.itemId, colors: dye };
    await viewer.setBody(assets.find(a => a.bodyVariant === 'female' && !a.skeleton));
    await equip(10200010);
    viewer.selectClip('fitting_idle_a'); viewer.seek(.7);
    const capture = async name => {
      for (const view of ['front', 'side']) {
        viewer.view(view); await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
        viewer.camera.updateMatrixWorld(true);
        let inside = true;
        viewer.equipment.get('10200010').traverse(node => {
          if (!node.isMesh) return;
          const point = new Vector3();
          for (let i=0;i<node.geometry.attributes.position.count;i++) {
            node.getVertexPosition(i,point).applyMatrix4(node.matrixWorld).project(viewer.camera);
            inside &&= Math.abs(point.x)<1 && Math.abs(point.y)<1 && Math.abs(point.z)<1;
          }
        });
        check('complete hair inside capture '+name+' '+view, inside);
        result.images[name + '-' + view] = viewer.screenshot();
      }
      result.states.push({ name, form: hair()?.hairForm, placements: placements().map(p => ({ value: p.value, scale: p.scale })), state: viewer.inspect() });
    };
    check('three equipped hair parts', hair().parts.length === 3);
    check('two independent placement controls', placements().length === 2);
    await capture('default');
    viewer.setItemColors('10200010', dye);
    const dyeMatches = () => (viewer.equipmentColors.get('10200010') ?? []).filter(c => !isSkinColor(c)).every(c => c.colors.every((rgb,i) => rgb.every((v,j) => Math.abs(v-dye[i][j]) < 1e-6)));
    check('one shared hair dye control', (viewer.equipmentColors.get('10200010') ?? []).filter(c => !isSkinColor(c)).length === 1);
    check('saved dye applied to all pieces', dyeMatches());
    const readPixels = image => { const canvas = document.createElement('canvas'); canvas.width=image.width; canvas.height=image.height; const ctx=canvas.getContext('2d'); ctx.drawImage(image,0,0); return ctx.getImageData(0,0,canvas.width,canvas.height); };
    const materialOf = group => { let material; group.traverse(n => {if(n.isMesh) material=n.material;}); return material; };
    const checkDyePixels = async stage => {
      for (const [i, asset] of hair().parts.entries()) {
        const original = await viewer.loader.loadAsync(asset.url);
        const info = materialOf(original.scene).userData;
        const diffuse = await original.parser.getDependency('texture',info.nifBaseColorTexture.index);
        const mask = await original.parser.getDependency('texture',info.nifColorControlTexture.index);
        const mode = t => ({linear:t.magFilter!==NearestFilter,wrapS:t.wrapS===RepeatWrapping,wrapT:t.wrapT===RepeatWrapping});
        const expected = bakeColors(readPixels(diffuse.image),readPixels(mask.image),dye,mode(diffuse),mode(mask));
        const actual = readPixels(materialOf(viewer.equipment.get('10200010').children[i]).map.image);
        check('saved dye pixels '+stage+' part '+i, actual.width===expected.width && actual.height===expected.height && actual.data.every((value,index)=>value===expected.data[index]));
      }
    };
    await checkDyePixels('A');
    placements()[0].set(1); viewer.seek(1.1);
    check('first preset does not change second', placements()[0].value === 1 && placements()[1].value === 0);
    await capture('first-tail-preset-2');
    placements()[1].set(2); viewer.seek(.7);
    check('second preset does not change first', placements()[0].value === 1 && placements()[1].value === 2);
    await capture('independent-presets');
    placements()[0].set(2); placements()[1].set(1); viewer.seek(.7);
    await capture('remaining-presets');
    viewer.setHairLengths([.8, 1]);
    check('saved back length scales both tails', placements().every(p => p.scale === .8));
    await capture('length-08');
    viewer.hairControls.find(c => c.label.endsWith('tail size')).reset();
    check('length reset restores one', placements().every(p => p.scale === 1));
    viewer.hairControls.find(c => c.label.endsWith('tail size')).set(1.2);
    check('length control scales both tails', placements().every(p => Math.abs(p.scale - 1.2) < 1e-6));
    await capture('length-12');
    for (const [id, form] of [[11300002, 'c'], [11300003, 'd']]) {
      await equip(id); viewer.seek(.7);
      check('hat selects ' + form, hair().hairForm === form);
      check('hat preserves two placements ' + form, placements().length === 2 && placements()[0].value === 2 && placements()[1].value === 1);
      check('hat preserves tail length ' + form, placements().every(p => Math.abs(p.scale - 1.2) < 1e-6));
      check('hat preserves saved dye ' + form, dyeMatches());
      await checkDyePixels(form);
      check('hat preserves attachment metadata ' + form, hair().parts.filter(p => p.attachment?.replace === false).length === 2);
      await capture('hat-' + form);
    }
    await viewer.removeItem('11300003'); viewer.seek(.7);
    check('hat removal restores A form', hair().hairForm === 'a');
    check('hat removal preserves saved dye', dyeMatches());
    const single = slot => items.find(i => i.gender === 1 && i.library.availability !== 'unavailable' && i.library.parts.length && i.library.slots.length === 1 && i.library.slots[0] === slot);
    const top = single('CL'), pants = single('PA');
    await equip(top.id); await equip(pants.id); await equip(12220360); viewer.seek(.7);
    check('full outfit replaces top and pants', !viewer.equippedItems.some(b => b.item.id === top.id || b.item.id === pants.id));
    check('full outfit retains hair and dye', Boolean(hair()) && dyeMatches());
    check('full outfit occupies CL and PA', viewer.equippedItems.find(b => b.item.id === 12220360)?.slots.join(',') === 'CL,PA');
    placements().forEach(p => p.reset()); viewer.hairControls.find(c => c.label.endsWith('tail size')).reset(); viewer.seek(.7);
    check('both placement resets work', placements().every(p => p.value === 0 && p.scale === 1));
    await capture('outfit-reset');
    result.final = viewer.inspect();
    viewer.view('front');
  } catch (error) { result.error = String(error); }
  result.running = false;
})();
