// Sample the existing body's animation as native-solver kinematic input.
// Run after pnpm check, in the local Sassy preview. No database reads.
(async () => {
  const job = window.sassyMotionInput = { running: true };
  try {
    const { parseNativeManifest } = await import('/src/lib/nativeAssets.ts');
    const { catalogSchema, resolveBundle } = await import('/src/lib/outfits/catalog.ts');
    const { applySassyPreview, loadSassyPreview } = await import('/src/lib/outfits/sassyPreview.ts');
    const { joinCatalog } = await import('/src/lib/outfits/search.ts');
    const { sourceName } = await import('/src/lib/outfits/sharedSkeleton.ts');
    const { Matrix4, Vector3, Quaternion, SkinnedMesh } = await import('/node_modules/.vite/deps/three.js');
    const base='/gltf/simulator-release-14/';
    const url=new URL(base+'native-manifest.json',location.href).href;
    const assets=[...parseNativeManifest(await (await fetch(url)).json(),url),...await loadSassyPreview(fetch,location.href)];
    const catalog=catalogSchema.parse(await (await fetch(base+'simulator-catalog.json')).json());
    const items=joinCatalog(applySassyPreview(catalog.items,assets),[]);
    const viewer=document.querySelector('[aria-label="Outfit preview"]').outfitViewer;
    await viewer.setBody(assets.find(a=>a.bodyVariant==='female'&&!a.skeleton));
    const item=items.find(i=>i.id===10200010&&i.gender===1);
    await viewer.equipBundle(resolveBundle(item,assets,'female'));
    const group=viewer.equipment.get('10200010');
    const tails=group.children.slice(1).map(part=>{
      const found=new Set();
      part.traverse(node=>{if(node instanceof SkinnedMesh)for(const bone of node.skeleton.bones)if(['Bone01','Bone02','Bone03'].includes(sourceName(bone)))found.add(bone);});
      const bones=['Bone01','Bone02','Bone03'].map(name=>[...found].find(b=>sourceName(b)===name));
      if(bones.some(b=>!b)||found.size!==3)throw new Error('Ambiguous tail skeleton');
      return bones;
    });
    const rigid=bone=>{bone.updateWorldMatrix(true,false);const p=new Vector3(),q=new Quaternion(),s=new Vector3();bone.matrixWorld.decompose(p,q,s);if(s.distanceTo(new Vector3(.01,.01,.01))>1e-5)throw new Error('Only size 1 supported');return new Matrix4().compose(p,q,new Vector3(1,1,1));};
    const pack=m=>{const e=m.elements;return [e[0],e[4],e[8],e[1],e[5],e[9],e[2],e[6],e[10],e[12],e[13],e[14]];};
    const clip='fitting_idle_a',dt=1/60,count=481;
    viewer.selectClip(clip);viewer.seek(0);
    job.rest=tails.map(bones=>bones.map(b=>pack(rigid(b))));
    job.frames=[];
    for(let f=0;f<count;f++) {
      viewer.seek(f*dt);
      job.frames.push(tails.map(bones=>pack(rigid(bones[0]))));
      if(f%30===0)await new Promise(r=>requestAnimationFrame(r));
    }
    job.clip=clip;job.dt=dt;job.itemId=10200010;
    job.placements=[0,0];job.scale=1;job.bodyVariant='female';
    viewer.seek(0);viewer.view('front');
  } catch(error) {job.error=String(error);}
  finally {job.running=false;}
})()
