// Run asynchronously in the existing Sassy T3 tab, after pnpm check.
// Supply the existing read-only snapshot as liveRows, then poll browserHairReview.
(async () => {
  const job = window.browserHairReview = { running: true, checks: [], images: {}, states: [] };
  const check = (name, value) => { job.checks.push({ name, passed: Boolean(value) }); if (!value) throw new Error(name); };
  try {
    const { parseNativeManifest } = await import('/src/lib/nativeAssets.ts');
    const { catalogSchema, resolveBundle } = await import('/src/lib/outfits/catalog.ts');
    const { applySassyPreview, loadSassyPreview } = await import('/src/lib/outfits/sassyPreview.ts');
    const { joinCatalog } = await import('/src/lib/outfits/search.ts');
    const { sourceName } = await import('/src/lib/outfits/sharedSkeleton.ts');
    const { BrowserHair } = await import('/src/lib/outfits/browserHair.ts');
    const { Vector3, Quaternion } = await import('/node_modules/.vite/deps/three.js');
    const base = '/gltf/simulator-release-14/';
    const url = new URL(base + 'native-manifest.json', location.href).href;
    const assets = [...parseNativeManifest(await (await fetch(url)).json(), url), ...await loadSassyPreview(fetch, location.href)];
    const catalog = catalogSchema.parse(await (await fetch(base + 'simulator-catalog.json')).json());
    const items = joinCatalog(applySassyPreview(catalog.items, assets), []);
    const viewer = document.querySelector('[aria-label="Outfit preview"]').outfitViewer;
    const equip = id => viewer.equipBundle(resolveBundle(items.find(i => i.id === id && i.gender === 1), assets, 'female'));
    const fixture = window.liveRows.find(r => r.kind === 'item' && r.name === 'EIIie' && r.slot === 1 && r.appearance?.Color);
    check('existing saved dye fixture', fixture);
    const dye = ['Primary', 'Secondary', 'Tertiary'].map(k => ['Red', 'Green', 'Blue'].map(c => fixture.appearance.Color[k][c] / 255));
    viewer.setBrowserHairEnabled(false);
    await equip(10200010); await equip(12220360);
    viewer.setItemColors('10200010', dye);
    viewer.hairPlacementControls.forEach(c => c.setScale(1));
    const colors = JSON.stringify(viewer.inspect().colors);
    const tails = () => viewer.equipmentRoot('10200010').children.slice(1).map(part => {
      const found = new Set(); part.traverse(n => { if (n.isSkinnedMesh) n.skeleton.bones.forEach(b => found.add(b)); });
      return ['Bone01', 'Bone02', 'Bone03'].map(name => [...found].find(b => sourceName(b) === name));
    });
    const transforms = () => tails().flat().map(b => [...b.position.toArray(), ...b.quaternion.toArray(), ...b.scale.toArray()]);
    const positions = () => tails().map(bones => bones.map(b => b.getWorldPosition(new Vector3())));
    viewer.selectClip('fitting_idle_a');
    for (let preset = 0; preset < 3; preset++) {
      viewer.setBrowserHairEnabled(false);
      viewer.hairPlacementControls.forEach(c => c.set(preset)); viewer.seek(.7);
      const before = JSON.stringify(transforms()), roots = positions().map(p => p[0]);
      viewer.setBrowserHairEnabled(true);
      const after = positions();
      check('roots preserved preset ' + preset, after.every((p, i) => p[0].distanceTo(roots[i]) < 1e-6));
      check('tails settle below roots preset ' + preset, after.every(p => p[2].y < p[0].y - .03));
      check('dye preserved preset ' + preset, JSON.stringify(viewer.inspect().colors) === colors);
      job.states.push({ preset, positions: after.map(p => p.map(v => v.toArray())) });
      for (const view of ['front', 'side']) { viewer.view(view); job.images[`preset-${preset + 1}-${view}`] = viewer.screenshot(); }
      viewer.setBrowserHairEnabled(false);
      check('exact rest restore preset ' + preset, JSON.stringify(transforms()) === before);
    }
    viewer.hairPlacementControls.forEach(c => c.reset());
    viewer.selectClip('run_a'); viewer.seek(0);
    const motion = new BrowserHair(viewer.equipmentRoot('10200010'));
    motion.update(0, true);
    const start = tails().map(b => b[1].quaternion.clone());
    let sway = 0;
    for (let frame = 1; frame <= 180; frame++) {
      viewer.seek(frame / 60); motion.update(1 / 60);
      tails().forEach((b, i) => { sway = Math.max(sway, b[1].quaternion.angleTo(start[i])); });
      if ([45, 90, 180].includes(frame)) for (const view of ['front', 'side']) {
        viewer.view(view); job.images[`run-${frame}-${view}`] = viewer.screenshot();
      }
    }
    motion.restore();
    job.maxSwayRadians = sway;
    check('tail movement relative to the head', sway > .02);
    check('colors survive motion', JSON.stringify(viewer.inspect().colors) === colors);
    viewer.selectClip('fitting_idle_a'); viewer.seek(.7); viewer.setBrowserHairEnabled(true);
    for (const scale of [.8, 1.2]) {
      viewer.hairPlacementControls.forEach(c => c.setScale(scale)); viewer.seek(.7);
      check('size retained ' + scale, viewer.hairPlacementControls.every(c => c.scale === scale));
      check('finite resized pose ' + scale, transforms().flat().every(Number.isFinite));
    }
    for (const [id, form] of [[11300002, 'c'], [11300003, 'd']]) {
      await equip(id); viewer.seek(.7);
      check('hair form ' + form, viewer.equippedItems.find(b => b.item.id === 10200010).hairForm === form);
      check('motion survives hair replacement ' + form, viewer.inspect().browserHair.active);
      check('dye survives hair replacement ' + form, JSON.stringify(viewer.inspect().colors.filter(c => c.label === 'Sassy Pigtails')[0].colors) === JSON.stringify(dye));
    }
    await viewer.removeItem('11300003'); viewer.seek(.7);
    check('full outfit occupies top and pants', viewer.equippedItems.find(b => b.item.id === 12220360).slots.join(',') === 'CL,PA');
    viewer.setBrowserHairEnabled(false);
    await viewer.removeItem('10200010');
    check('unequip releases simulation', !viewer.inspect().browserHair.active);
    await equip(10200010); viewer.setItemColors('10200010', dye);
    viewer.hairPlacementControls.forEach(c => { c.reset(); c.setScale(1); });
    viewer.selectClip('fitting_idle_a'); viewer.seek(.7); viewer.view('front');
    job.final = viewer.inspect(); job.dyeFixture = { name: 'EIIie', colors: dye };
  } catch (error) { job.error = String(error); }
  finally { job.running = false; }
})()
