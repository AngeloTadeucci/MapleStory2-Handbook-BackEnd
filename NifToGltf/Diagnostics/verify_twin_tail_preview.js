// Existing /outfits?hairPreview=twins tab, after pnpm check. Reuse liveRows snapshot.
(async () => {
  const job = window.twinTailReview = { running: true, checks: [], images: {}, states: [] };
  const check = (name, passed) => { job.checks.push({ name, passed: Boolean(passed) }); if (!passed) throw new Error(name); };
  try {
    const { parseNativeManifest } = await import('/src/lib/nativeAssets.ts');
    const { catalogSchema, resolveBundle } = await import('/src/lib/outfits/catalog.ts');
    const { applyTwinTailPreview, loadTwinTailPreview } = await import('/src/lib/outfits/twinTailPreview.ts');
    const { joinCatalog } = await import('/src/lib/outfits/search.ts');
    const { isSkinColor } = await import('/src/lib/outfits/skinColors.ts');
    const { Vector3 } = await import('/node_modules/.vite/deps/three.js');
    const base = '/gltf/simulator-release-14/';
    const url = new URL(base + 'native-manifest.json', location.href).href;
    const assets = [...parseNativeManifest(await (await fetch(url)).json(), url), ...await loadTwinTailPreview(fetch, location.href)];
    const catalog = catalogSchema.parse(await (await fetch(base + 'simulator-catalog.json')).json());
    const items = joinCatalog(applyTwinTailPreview(catalog.items, assets), []);
    const viewer = document.querySelector('[aria-label="Outfit preview"]').outfitViewer;
    const equip = id => viewer.equipBundle(resolveBundle(items.find(i => i.id === id && i.gender === 1), assets, 'female'));
    const fixture = window.liveRows.find(r => r.kind === 'item' && r.name === 'EIIie' && r.slot === 1 && r.appearance?.Color);
    check('saved dye fixture exists', fixture);
    const dye = ['Primary', 'Secondary', 'Tertiary'].map(k => ['Red', 'Green', 'Blue'].map(c => fixture.appearance.Color[k][c] / 255));
    viewer.setBrowserHairEnabled(false); await equip(12220360);
    viewer.selectClip('fitting_idle_a');
    for (const id of [10200011, 10200012]) {
      await equip(id); viewer.setItemColors(String(id), dye); viewer.seek(.7);
      const controls = () => viewer.hairPlacementControls;
      const dyeMatches = () => (viewer.equipmentColors.get(String(id)) ?? []).filter(c => !isSkinColor(c)).every(c => c.colors.every((rgb, i) => rgb.every((v, j) => Math.abs(v - dye[i][j]) < 1e-6)));
      const capture = name => {
        for (const view of ['front', 'side']) {
          viewer.view(view); job.images[`${id}-${name}-${view}`] = viewer.screenshot();
          let inside = true;
          viewer.equipmentRoot(String(id)).traverse(n => { if (n.isMesh) {
            for (let i = 0; i < n.geometry.attributes.position.count; i++) {
              const p = n.getVertexPosition(i, new Vector3()).applyMatrix4(n.matrixWorld).project(viewer.camera);
              inside &&= Math.abs(p.x) < 1 && Math.abs(p.y) < 1 && Math.abs(p.z) < 1;
            }
          }});
          check(`framed ${id}/${name}/${view}`, inside);
        }
      };
      check('two independent controls ' + id, controls().length === 2);
      check('three loaded hair pieces ' + id, viewer.equipmentRoot(String(id)).children.length === 3);
      controls()[0].set(1); check('independent first placement ' + id, controls()[1].value === 0);
      for (let preset = 0; preset < 3; preset++) {
        controls().forEach(c => c.set(preset)); viewer.seek(.7); capture('preset-' + preset);
        check(`saved dye ${id}/${preset}`, dyeMatches());
      }
      controls()[0].set(2); controls()[1].set(1);
      const size = viewer.hairControls.find(c => c.label.endsWith('tail size'));
      size.set(.8); viewer.seek(.7); check('size applies to both tails ' + id, controls().every(c => Math.abs(c.scale - .8) < 1e-6));
      for (const [hat, form] of [[11300002, 'c'], [11300003, 'd']]) {
        await equip(hat); viewer.seek(.7);
        check(`hat form ${id}/${form}`, viewer.equippedItems.find(b => b.item.id === id).hairForm === form);
        check(`saved controls ${id}/${form}`, controls()[0].value === 2 && controls()[1].value === 1 && controls().every(c => Math.abs(c.scale - .8) < 1e-6));
        check(`saved dye under hat ${id}/${form}`, dyeMatches()); capture('hat-' + form);
      }
      await viewer.removeItem('11300003'); viewer.seek(.7);
      check('restores base hair after hat removal ' + id, viewer.equippedItems.find(b => b.item.id === id).hairForm === 'a');
      controls().forEach(c => c.reset()); viewer.hairControls.find(c => c.label.endsWith('tail size')).reset(); viewer.seek(.7);
      check('reset restores source controls ' + id, controls().every(c => c.value === 0 && c.scale === 1));
      check('dress retains both slots ' + id, viewer.equippedItems.find(b => b.item.id === 12220360).slots.join(',') === 'CL,PA');
      job.states.push(viewer.inspect());
    }
    viewer.view('front'); viewer.renderFrame();
  } catch (error) { job.error = String(error); }
  finally { job.running = false; }
})()
