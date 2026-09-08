// Compare all authored Sassy positions without modifying rotation data.
// Finish pnpm check first. Supply the existing read-only EIIie dye fixture in liveRows.
(async () => {
  const job = window.sassyOrientation = { running: true, checks: [], states: [], images: {} };
  const check = (name, passed) => {
    job.checks.push({ name, passed: Boolean(passed) });
    if (!passed) throw new Error(name);
  };
  try {
    const { parseNativeManifest } = await import('/src/lib/nativeAssets.ts');
    const { catalogSchema, resolveBundle } = await import('/src/lib/outfits/catalog.ts');
    const { applySassyPreview, loadSassyPreview } = await import('/src/lib/outfits/sassyPreview.ts');
    const { joinCatalog } = await import('/src/lib/outfits/search.ts');
    const { sourceName } = await import('/src/lib/outfits/sharedSkeleton.ts');
    const { isSkinColor } = await import('/src/lib/outfits/skinColors.ts');
    const { Vector3 } = await import('/node_modules/.vite/deps/three.js');
    const base = '/gltf/simulator-release-14/';
    const url = new URL(base + 'native-manifest.json', location.href).href;
    const assets = [...parseNativeManifest(await (await fetch(url)).json(), url), ...await loadSassyPreview(fetch, location.href)];
    const catalog = catalogSchema.parse(await (await fetch(base + 'simulator-catalog.json')).json());
    const items = joinCatalog(applySassyPreview(catalog.items, assets), []);
    const viewer = document.querySelector('[aria-label="Outfit preview"]').outfitViewer;
    const fixture = window.liveRows.find(r => r.kind === 'item' && r.name === 'EIIie' && r.slot === 1 && r.appearance?.Color);
    check('existing saved dye fixture', fixture);
    const dye = ['Primary', 'Secondary', 'Tertiary'].map(k => ['Red', 'Green', 'Blue'].map(c => fixture.appearance.Color[k][c] / 255));
    await viewer.setBody(assets.find(a => a.bodyVariant === 'female' && !a.skeleton));
    for (const id of [10200010, 12220360]) {
      await viewer.equipBundle(resolveBundle(items.find(i => i.id === id && i.gender === 1), assets, 'female'));
    }
    viewer.setItemColors('10200010', dye);
    viewer.selectClip('fitting_idle_a');
    const controls = viewer.hairPlacementControls;
    check('two source attachment controls', controls.length === 2);
    const point = b => b.getWorldPosition(new Vector3()).toArray();
    const axes = () => viewer.equipmentRoot('10200010').children.slice(1).map(part => {
      const found = new Set();
      part.traverse(n => { if (n.isSkinnedMesh) for (const b of n.skeleton.bones) found.add(b); });
      const bones = ['Bone01', 'Bone02', 'Bone03'].map(name => [...found].find(b => sourceName(b) === name));
      check('complete tail chain', bones.every(Boolean));
      const root = point(bones[0]), tip = point(bones[2]);
      return { root, tip, rootToTip: tip.map((v, i) => v - root[i]) };
    });
    for (let preset = 0; preset < 3; preset++) {
      controls.forEach(c => c.set(preset));
      viewer.seek(.7);
      check('selected authored preset ' + preset, controls.every(c => c.value === preset && c.scale === 1));
      check('saved dye retained ' + preset, (viewer.equipmentColors.get('10200010') ?? []).filter(c => !isSkinColor(c)).every(c => c.colors.every((rgb, i) => rgb.every((v, j) => Math.abs(v - dye[i][j]) < 1e-6))));
      job.states.push({ preset, tails: axes() });
      for (const view of ['front', 'side']) {
        viewer.view(view);
        // screenshot() renders synchronously; no animation-frame wait is needed
        // for a fixed pose, including when the shared preview is backgrounded.
        job.images[`preset-${preset + 1}-${view}`] = viewer.screenshot();
      }
    }
    check('first authored preset points upward', job.states[0].tails.every(t => t.rootToTip[1] > 0));
    check('third authored preset points downward', job.states[2].tails.every(t => t.rootToTip[1] < 0));
    // Leave the low authored preset visible for comparison, without changing defaults.
    viewer.view('front');
    viewer.renderFrame();
    job.dyeFixture = { name: 'EIIie', itemId: fixture.itemId, colors: dye };
    job.final = viewer.inspect();
  } catch (error) { job.error = String(error); }
  finally { job.running = false; }
})()
