// Evaluate in the existing local /outfits T3 tab after loading window.liveRows.
// All database access happens separately in read_live_outfits.py.
(async () => {
  const { parseNativeManifest } = await import('/src/lib/nativeAssets.ts');
  const { catalogSchema, resolveBundle, bundleKey } = await import('/src/lib/outfits/catalog.ts');
  const { joinCatalog } = await import('/src/lib/outfits/search.ts');
  const { enableApproximateHair } = await import('/src/lib/outfits/approximateHair.ts');
  const { hairPlacementSource, createHairPlacement } = await import('/src/lib/outfits/hairPlacement.ts');
  const { isSkinColor } = await import('/src/lib/outfits/skinColors.ts');
  const base = '/gltf/simulator-release-14/';
  const url = new URL(base + 'native-manifest.json', location.href).href;
  const assets = parseNativeManifest(await (await fetch(url)).json(), url);
  const catalog = catalogSchema.parse(await (await fetch(base + 'simulator-catalog.json')).json());
  const items = joinCatalog(enableApproximateHair(catalog.items, assets), []);
  const custom = await (await fetch(base + 'customization.json')).json();
  const viewer = document.querySelector('[aria-label="Outfit preview"]').outfitViewer;
  const rgb = c => ['Red', 'Green', 'Blue'].map(k => (c?.[k] ?? 0) / 255);
  const vector = v => ['X', 'Y', 'Z'].map(k => v?.[k] ?? 0);
  const order = window.liveRenderNames ?? ['Blaze', 'EIIie', 'Chiisa', 'Skillet', 'Asthoria', 'GolemSoldier', 'Areki', 'Gelo', 'Tree', 'Robbit'];
  window.liveExport = { running: true, results: [], images: {}, sides: {} };
  try {
    for (const name of order) {
      const character = window.liveRows.find(r => r.kind === 'character' && r.name === name);
      if (!character) throw new Error('Missing character ' + name);
      const body = character.gender === 0 ? 'male' : 'female';
      const report = { name, level: character.level, savedAt: character.savedAt, equipped: [], omitted: [], nonvisual: [], notes: [], dyeChecks: [] };
      window.liveExport.current = name;
      await viewer.setBody(assets.find(a => a.bodyVariant === body && !a.skeleton));
      viewer.setCustomization(custom, base);
      const primary = rgb(character.skin.Primary);
      const palette = custom.palettes['1'].find(p => p.colors[0].every((v, i) => Math.abs(v - primary[i]) < 1e-6));
      if (palette) viewer.colorControls.find(c => c.label === 'Skin').setColors(palette.colors);
      else {
        const skin = viewer.colorControls.find(c => c.label === 'Skin');
        skin.setColors([primary, rgb(character.skin.Secondary), skin.colors[2]]);
        report.notes.push('No exact skin palette match; retained default shadow color.');
      }
      const records = window.liveRows.filter(r => r.kind === 'item' && r.name === name);
      if (records.some(r => r.group === 3)) throw new Error('Outfit2 priority needs inspection');
      const selected = new Map();
      for (const r of records.filter(r => r.group === 1 || r.group === 2).sort((a,b) => a.group-b.group)) selected.set(r.slot, r);
      const full = items.find(i => i.id === selected.get(8)?.itemId && i.gender === character.gender);
      if (full?.library.slots.includes('PA')) selected.delete(9);
      // Constants table lists transparency slots in this order. Record use for review.
      const badgeSlots = [6,13,8,9,7,14,12,10,16,11];
      for (const r of records.filter(r => r.group === 4 && r.itemId === 70100001)) {
        r.subType?.Transparency?.forEach((hidden, index) => {
          if (hidden) { const slot = badgeSlots[index]; report.notes.push('Transparency badge hides slot ' + slot); selected.delete(slot); }
        });
      }
      report.notes.push('Badge particles, pets and nameplates are outside this outfit render.');
      const loaded = [];
      for (const saved of [...selected.values()].sort((a,b) => a.slot-b.slot)) {
        const item = items.find(i => i.id === saved.itemId && i.gender === character.gender);
        if (item?.library.classification === 'nonvisual') {
          report.nonvisual.push({ id: saved.itemId, slot: saved.slot, reason: item.library.reason });
          continue;
        }
        if (!item || item.library.availability === 'unavailable') {
          report.omitted.push({ id: saved.itemId, slot: saved.slot, reason: item?.library.reason ?? 'Not in release catalog' });
          continue;
        }
        try {
          const hand = item.library.handParts && [4,5].includes(saved.slot) ? (saved.slot === 4 ? 'LH' : 'RH') : undefined;
          const bundle = resolveBundle(item, assets, body, hand);
          await viewer.equipBundle(bundle);
          loaded.push({ saved, bundle });
        } catch (e) { report.omitted.push({id:saved.itemId, slot:saved.slot, reason:String(e)}); }
      }
      // Apply saved dyes after hat-driven hair replacements have completed.
      for (const {saved,bundle} of loaded) {
        const key = bundleKey(bundle);
        const color = saved.appearance?.Color;
        if (color && (color.Primary || color.Secondary || color.Tertiary)) {
          const expected = ['Primary','Secondary','Tertiary'].map(k => rgb(color[k]));
          viewer.setItemColors(key, expected);
          const controls = bundle.slots.includes('FA') ? [viewer.face.control] : (viewer.equipmentColors.get(key) ?? []).filter(c => !isSkinColor(c));
          report.dyeChecks.push({id:saved.itemId, expected, controls:controls.length,
            matches: controls.length ? controls.every(c => c.colors.every((rgb, i) => rgb.every((v, j) => Math.abs(v - expected[i][j]) < 1e-6))) : null});
        }
        report.equipped.push({id:saved.itemId,slot:saved.slot,name:bundle.item.name,
          availability:bundle.item.library.availability,limitations:bundle.item.library.limitations ?? []});
        if (saved.appearance?.['!'] === 'hair') {
          const current = viewer.equippedItems.find(b => bundleKey(b) === key);
          const group = viewer.equipment.get(key);
          const controls = [];
          let index = 0;
          for (const [partIndex, asset] of current.parts.entries()) {
            const part = hairPlacementSource(current.item.library?.presetId ?? current.item.id, asset);
            if (!part) continue;
            const prefix = index++ === 0 ? 'Back' : 'Front';
            const p = saved.appearance[prefix + 'Position1'], r = saved.appearance[prefix + 'Position2'];
            if (p && r) controls.push(createHairPlacement(group.children[partIndex], {...part, presets:[{position:vector(p),rotation:vector(r)}]}, prefix, 0, () => {}));
          }
          if (controls.length) viewer.hairPlacements.set(key, controls);
          report.hairLengths = viewer.setHairLengths([
            saved.appearance.BackLength ?? 0, saved.appearance.FrontLength ?? 0
          ]);
        }
        if (saved.appearance?.['!'] === 'decal' && viewer.makeupControls) {
          const a = saved.appearance;
          if (a.Position1 !== undefined || a.Position2 !== undefined || a.Position3 !== undefined || a.Position4 !== undefined)
            report.notes.push('Saved makeup placement retained in snapshot; render uses source preset.');
        }
      }
      viewer.selectClip('fitting_idle_a');
      viewer.playing = false;
      viewer.seek(0.3);
      viewer.refreshHatAttachments(true);
      // Foreground hair, tiara and weapon tips can touch the 823x650 capture
      // edge with the viewer's default perspective framing. Pad the camera.
      report.captureDistanceScale = 1.08;
      const captureView = angle => {
        viewer.view(angle);
        viewer.camera.position.sub(viewer.controls.target).multiplyScalar(report.captureDistanceScale).add(viewer.controls.target);
        viewer.controls.update();
      };
      captureView('front');
      await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
      window.liveExport.images[name] = viewer.screenshot();
      captureView('side');
      await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
      window.liveExport.sides[name] = viewer.screenshot();
      captureView('front');
      report.state = viewer.inspect();
      report.slotCheck = report.state.equipped.every((item, index, all) =>
        all.slice(index + 1).every(other => !item.slots.some(slot => other.slots.includes(slot))));
      window.liveExport.results.push(report);
    }
  } catch (error) { window.liveExport.error = String(error); }
  finally { window.liveExport.running = false; }
})()
