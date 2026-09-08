// Run after pnpm check with Sassy equipped via the UI; no new live snapshot needed.
(async () => {
  const job = window.sassyMotionReview = { running: true, checks: [], images: {} };
  const check=(name,value)=>{job.checks.push({name,passed:Boolean(value)});if(!value)throw new Error(name);};
  try {
    const { HairMotionPlayback,hairMotionSchema,sampleHairMotion }=await import('/src/lib/outfits/hairMotion.ts');
    const { sourceName }=await import('/src/lib/outfits/sharedSkeleton.ts');
    const { Vector3,Quaternion,SkinnedMesh }=await import('/node_modules/.vite/deps/three.js');
    const viewer=document.querySelector('[aria-label="Outfit preview"]').outfitViewer;
    const sample=hairMotionSchema.parse(await(await fetch('/gltf/sassy-pigtails-preview-01/motion-fitting-idle.json')).json());
    viewer.hairPlacementControls.forEach(c=>{c.reset();c.setScale(1);});
    const colors=JSON.stringify(viewer.inspect().colors);
    const parts=viewer.equipmentRoot('10200010').children.slice(1);
    const tails=parts.map(part=>{const bones=new Set();part.traverse(n=>{if(n instanceof SkinnedMesh)for(const b of n.skeleton.bones)if(['Bone01','Bone02','Bone03'].includes(sourceName(b)))bones.add(b);});return ['Bone01','Bone02','Bone03'].map(name=>[...bones].find(b=>sourceName(b)===name));});
    const before=tails.map(bones=>bones.slice(1).map(b=>[...b.position.toArray(),...b.quaternion.toArray(),...b.scale.toArray()]));
    const motion=new HairMotionPlayback(viewer,sample,reason=>job.lastStop=reason);
    window.sassyMotionReviewPlayback=motion;
    viewer.selectClip(sample.clip);
    for(const time of [0,1,3,5,8]) {
      motion.seek(time);
      const expected=sampleHairMotion(sample,time);
      for(let t=0;t<2;t++)for(let b=1;b<3;b++) {
        const node=tails[t][b],p=new Vector3(),q=new Quaternion();
        node.getWorldPosition(p);node.getWorldQuaternion(q);
        check(`destination position ${time}/${t}/${b}`,p.distanceTo(expected[t][b].position)<1e-6);
        check(`destination rotation ${time}/${t}/${b}`,q.normalize().angleTo(expected[t][b].rotation.clone().normalize())<1e-5);
      }
      check('colors preserved '+time,JSON.stringify(viewer.inspect().colors)===colors);
      for(const angle of ['front','side']) {
        viewer.view(angle);await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
        job.images[`${time}-${angle}`]=viewer.screenshot();
      }
    }
    motion.stop();
    check('rest transforms restored exactly',JSON.stringify(before)===JSON.stringify(tails.map(bones=>bones.slice(1).map(b=>[...b.position.toArray(),...b.quaternion.toArray(),...b.scale.toArray()]))));
    viewer.hairPlacementControls[0].set(1);
    let rejected=false;
    try{new HairMotionPlayback(viewer,sample,()=>{});}catch{rejected=true;}
    check('changed placement rejected',rejected);
    viewer.hairPlacementControls[0].reset();
    motion.start();await new Promise(r=>setTimeout(r,350));
    viewer.hairPlacementControls[1].set(1);
    await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
    check('running sample stops on changed placement',job.lastStop.includes('controls changed'));
    check('cancel restores rest transforms',JSON.stringify(before)===JSON.stringify(tails.map(bones=>bones.slice(1).map(b=>[...b.position.toArray(),...b.quaternion.toArray(),...b.scale.toArray()]))));
    viewer.hairPlacementControls[1].reset();viewer.seek(0);viewer.view('front');
  }catch(error){job.error=String(error);}
  finally{job.running=false;}
})()
