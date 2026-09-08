# Approximate browser hair, 2026-09-08

The user authorized an approximate implementation instead of waiting for exact
client PhysX integration. Sassy Pigtails now has an optional Browser hair motion
checkbox in `/outfits?hairPreview=sassy` and `/outfits?hairPreview=twins`.
It runs locally in the browser and does not load the native motion sample.
The earlier PhysX experiment remains available under a separate disclosure.
Starting either mode through the UI stops the other.

## Model and limits

`src/lib/outfits/browserHair.ts` drives the two existing three-bone Sassy chains.
It keeps the selected Point01 attachment and all source bone translations and
scales. Only bone rotations change. A virtual final segment continues the last
authored link so Bone03 can bend as well. Disabling motion restores the exact
saved local rotations. Placement and size changes rebuild the settled chain;
hat-form replacement binds a new controller to the replacement skeleton.

The model uses positional integration at 120 Hz, gravity `[0,-9.8,0]` in metres,
exponential damping with coefficient 4, and fixed segment lengths. Two seconds
of fixed-head steps produce the initial settled pose. Half the head displacement
is carried directly with the strand to reduce excessive inertial response.
Catch-up after a suspended tab is capped at 0.1 seconds. Pause freezes motion;
seeking or changing body clips settles at the new pose instead of carrying stale
velocity into a discontinuous animation jump.

A sphere derived from the base hair bounds approximates the head. Segment
directions are constrained against its tangent cone, not only at their endpoints.
It is deliberately smaller than the rendered head and reduces near an attachment
inside the bound. This is not an exact head surface or strand-thickness collision.
Shoulder, torso, hat and tail-to-tail collisions remain absent.

The first running comparison threw tails over the head. Numeric observations
confirmed that solver and rendered bone positions agreed; the error was in the
unconstrained approximation, not the pose conversion. Reducing full inertial
transfer to one half helped but still allowed a fold-over. The final model limits
free swing to the downward hemisphere before applying the head constraint.
Fast animation can still spread the tails sideways and intersect visible hair.
This is a visual approximation, not a claim of physical or game-client parity.

## Verification

- Full frontend suite: 155 passed, 15 skipped. `pnpm check`: zero errors/warnings.
- Five solver tests cover downward settling, length preservation, frame-rate
  consistency, moving anchors, bounded catch-up, whole-segment head avoidance,
  exaggerated running bob, transformed skeletons and exact rest restoration.
- `Diagnostics/verify_browser_hair.js`: 27 browser checks pass. All 12 front/side
  captures were inspected, covering three placements and sampled run motion.
  The existing EIIie snapshot dye survives, roots retain their attachment points,
  size 0.8/1.2 stays finite, C/D hair forms rebuild the simulation, and the wedding
  dress keeps both clothing slots. Disabling restores exact rest transforms.
- The actual checkbox change handler enables and disables the controller. During
  final UI verification, T3 delivered no animation-frame callbacks, including an
  independent one-shot probe. Continuous playback and its pause behavior could
  not be verified in that session. The motion captures above use explicit sampled
  steps and do not establish continuous playback.

Evidence: `obj/hair-investigation/sassy/browser-motion/`. The earlier unconstrained
captures remain under `before-swing-limit/`. This work does not rebake native
motion, regenerate Sassy meshes, modify release 14, or rebuild production.

## Twin-tail source repairs

The user confirmed the two targets as Banded Twin Tails 10200011 and Cutesy Twin
Tails 10200012.
Both are now static previews in `/outfits?hairPreview=twins`. That route also keeps
Sassy and its optional browser motion available.

Each P2_A KFM explicitly names its own P_A NIF. Both first tails already exist in
release 14. Only the two missing second attachments were newly exported into
`static/gltf/twin-tails-preview-01/`. Banded's base is explicitly declared as
Sassy's BasicHair02 in KMS `itemmodel/102.xml`; Cutesy declares BasicHair03.
All A/C/D base forms are reused from release 14. No replacement hairstyle,
invented texture, release inventory or review hash was introduced.

The local XML SHA-256 is
`729e0effb06a8c86cd5035511dc172d303cd155bd1b48792ba3d97e1b063ac4b`.
Banded's NIF SHA-256 is
`c6697d56d3a51e0b1c4ed970d662b7dc6fb0585583b335cf81da7d415fc7fbd3`.
Cutesy's NIF SHA-256 is
`f79bed98c1f72ab507d9ab0ee676cadba179f8772007f0f10ee28019dbbb6d3b`.
The exact KFM hashes, references, selected XML and plan are in
`obj/hair-investigation/twin-tails/`. Preview file hashes are in its separate
`preview-provenance.json`. Normal catalog entries remain unavailable.

Both new glTFs validate with zero errors and warnings. Banded has four skin bones
and no KFM clips; its source includes physics omitted from the static export.
Cutesy has one skin bone and an empty posed sequence, with no source PhysX blocks.
Their XML jointangle records are not applied as static rotations or claimed to
be implemented physics. The Sassy three-bone solver is not silently applied to
these different skeletons.

`Diagnostics/verify_twin_tail_preview.js` passes 53 browser checks. All 20
front/side images were inspected: three presets per hair, C/D hats, independent
tail controls, size, reset, saved dye and full-outfit slots. Evidence is in
`obj/hair-investigation/twin-tails/renders/`. Hat intersections remain visible
in some placements. Curled Pigtails' missing direction-texture binding is a
separate unresolved material issue.

## User placement review

The user reported that Sassy motion looked acceptable but wanted the attachment
farther back, rather than above the ears. Both controls were changed from source
Position 1 to Position 3 in T3. Front, side and back renders confirm the lower
rear roots. Colors and the original motion settings were preserved. This was a
viewer selection, not a new default or a persisted outfit. Temporary collider
and rigid-root experiments were discarded without changing repository code.
