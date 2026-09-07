# Local PhysX 2.8.4 probe

`Diagnostics/probe_physx284.cpp` is an offline diagnostic, not a browser solver.
It loads existing client DLLs and reads cooked convex blobs through
`NxPhysicsSDK::createConvexMesh`. It never calls a cooking API, writes client
files or changes simulator release assets. Output is JSON on stdout; diagnostics
go to stderr. No server or game process is started.

## Inputs and provenance

The inspected client directory is `D:/MS2/KMS2 Debug/x64`. Its PhysXCore64.dll
file version is 2.8.4.6. The probe initializes its loader with hardware disabled
and verifies the SDK vtable implementation belongs to that specified core DLL.
The SDK returns version 284, API revision 1 and descriptor revision 0.

The reference headers are NVIDIA-authored files from the public
[SDK mirror](https://github.com/q4a/physx-sdk2.8.4-win/tree/ccd730fd80c75ae43710ec13192a7111c6c2f347/SDKs),
pinned at `ccd730fd80c75ae43710ec13192a7111c6c2f347`. They are stored only under
ignored `obj/hair-investigation/physx-sdk-reference/`, with URL and SHA-256
records in `header-provenance.json`. No installer or downloaded executable ran.
The diagnostic was compiled with the existing MSVC 14.44 x64 compiler.
Headers and DLLs are not committed or redistributed with this change.

From an x64 Visual Studio Developer shell, with the current directory set to
`NifToGltf/obj/hair-investigation`, compile using:

```powershell
cl /nologo /std:c++17 /EHsc /MD /DWIN32 /DWIN64 /DNX64 `
  /Iphysx-sdk-reference/SDKs/Physics/include `
  /Iphysx-sdk-reference/SDKs/Foundation/include `
  /Iphysx-sdk-reference/SDKs/PhysXLoader/include `
  ../../Diagnostics/probe_physx284.cpp /Fe:probe_physx284.exe /Fo:probe_physx284.obj
./probe_physx284.exe 'D:/MS2/KMS2 Debug/x64'
```

Additional arguments name raw cooked convex blobs, excluding the NIF string
index, byte-count prefix and descriptor flags. The existing source extraction
is read through `inspect_nif.read_document`; `NiPhysXMeshDesc` begins with a
32-bit string index and a 32-bit cooked-byte count. The complete authored blob
is copied for the probe. Render geometry is never used as collision geometry.

Run the optional integration test from the backend root:

```powershell
py -m unittest discover -s NifToGltf/Diagnostics -p test_physx284_probe.py
```

`MS2_PHYSX_DLL_DIR` can override the local client DLL directory. The test skips
when the probe, source extraction or local DLL is absent. It checks the actual
runtime, both Curled Pigtails tails, complete stream consumption, descriptor
offsets and rejection of a truncated input. It does not test browser motion.

## Observed results and limits

All 34 hulls in the inspected 25 source NIFs loaded completely, producing
426 vertices and 716 triangles. Results, source/DLL hashes and copied cooked
blobs are under ignored `obj/hair-investigation/cooked-probe/`.

The x64 D6 descriptor offsets are localNormal[0] 0x20, localAxis[0] 0x38,
linearLimit 0xa8, swing1Limit 0xb8, swing2Limit 0xc8, twistLimit.low 0xd8,
twistLimit.high 0xe8, and xDrive 0xf8. These agree with the client helper's writes.
`NxJointLimitSoftDesc.value` is an angle in radians for angular limits and a
distance for linear limits. Zero spring means a hard limit. The client's
`soft` XML attribute writes this value and zeroes restitution, spring and
damping; it is not a spring-stiffness value. Only matched joint records receive
the override. Missing soft defaults to approximately 0.01, while explicit XML
values still take precedence.

The probe does not create a hair scene or advance simulation. Final hair-scene
configuration, source/step/destination order and the browser execution route
remain open. The native DLL is not a WebAssembly implementation. Modern PhysX
is not established as a behavior-compatible replacement. Do not enable a
physics UI based on this diagnostic alone.
