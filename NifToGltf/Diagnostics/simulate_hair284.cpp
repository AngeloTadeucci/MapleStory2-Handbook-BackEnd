// Offline playback generator using the locally installed client solver.
// The generated scene include contains inspected source descriptors, not SDK code.
#define PHYSX284_NO_PROBE_MAIN
#include "probe_physx284.cpp"
#include <array>
#include <cmath>
#include <stdexcept>

[[noreturn]] static void fail(const char* message) { throw std::runtime_error(message); }
static NxMat34 pose(std::initializer_list<float> values) {
    if (values.size() != 12) fail("Invalid pose width");
    const auto* v = values.begin();
    NxMat34 result;
    result.M.setRowMajor(v);
    result.t.set(v[9], v[10], v[11]);
    return result;
}
#include "hair_scene.inc"

class MotionInput {
    std::ifstream file;
public:
    explicit MotionInput(const std::filesystem::path& path) : file(path, std::ios::binary) {
        char magic[8]; file.read(magic, 8);
        if (!file || std::memcmp(magic, "PHXHAIR1", 8)) fail("Invalid motion input header");
    }
    template<class T> T read() {
        T value; file.read(reinterpret_cast<char*>(&value), sizeof(value));
        if (!file) fail("Truncated motion input");
        return value;
    }
    float scalar() {
        const auto value = read<float>();
        if (!std::isfinite(value)) fail("Nonfinite motion input");
        return value;
    }
    NxVec3 vector() { const float x=scalar(), y=scalar(), z=scalar(); return NxVec3(x,y,z); }
    NxMat34 transform() {
        float values[9]; for (auto& value : values) value = scalar();
        NxMat34 m; m.M.setRowMajor(values); m.t = vector();
        if (std::abs(m.M.determinant() - 1) > 0.001f) fail("Motion requires unscaled rigid transforms");
        for (unsigned i=0;i<3;++i) for (unsigned j=0;j<3;++j) {
            float dot=0;
            for (unsigned k=0;k<3;++k) dot += values[k*3+i]*values[k*3+j];
            if (std::abs(dot - (i==j ? 1.0f : 0.0f)) > 0.001f)
                fail("Motion requires orthonormal rotations");
        }
        return m;
    }
    void finish() { if (file.peek() != std::char_traits<char>::eof()) fail("Trailing motion input"); }
};

int wmain(int argc, wchar_t** argv) {
    if (argc != 4) {
        std::fputs("Usage: simulate_hair284 <client DLL directory> <scene/hulls directory> <motion input>\n", stderr);
        return 2;
    }
    try {
        MotionInput input(argv[3]);
        const auto frames = input.read<unsigned>();
        const auto tails = input.read<unsigned>();
        const float dt = input.scalar();
        const auto gravity = input.vector();
        if (frames < 2 || frames > 36000 || tails < 1 || tails > 2 || dt < .001f || dt > .1f)
            fail("Motion input outside diagnostic bounds");
        std::vector<NxMat34> initial;
        for (unsigned i=0;i<tails;++i) initial.push_back(input.transform());
        std::vector<std::vector<NxMat34>> targets(frames);
        for (auto& frame : targets) for (unsigned i=0;i<tails;++i) frame.push_back(input.transform());
        input.finish();
        const auto directory = std::filesystem::canonical(argv[1]);
        SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        AddDllDirectory(directory.c_str());
        const auto core = LoadLibraryExW((directory / L"PhysXCore64.dll").c_str(), nullptr,
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        const auto loader = LoadLibraryExW((directory / L"PhysXLoader64.dll").c_str(), nullptr,
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if (!core || !loader) fail("Client DLL load failed");
        const auto create = reinterpret_cast<decltype(&NxCreatePhysicsSDK)>(GetProcAddress(loader,"NxCreatePhysicsSDK"));
        const auto release = reinterpret_cast<decltype(&NxReleasePhysicsSDK)>(GetProcAddress(loader,"NxReleasePhysicsSDK"));
        if (!create || !release) fail("Client SDK exports missing");
        Output output; NxPhysicsSDKDesc settings; settings.flags = NX_SDKF_NO_HARDWARE;
        NxSDKCreateError error;
        auto* sdk = create(NX_PHYSICS_SDK_VERSION,nullptr,&output,settings,&error,PHYSX_284_CORE_GUID);
        if (!sdk) fail("Client SDK creation failed");
        HMODULE implementation = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>((*reinterpret_cast<void***>(sdk))[0]),&implementation);
        if (implementation != core) fail("Wrong SDK implementation");
        NxSceneDesc sceneDesc;
        sceneDesc.gravity = gravity;
        sceneDesc.simType = NX_SIMULATION_SW;
        sceneDesc.maxTimestep = dt;
        sceneDesc.maxIter = 1;
        sceneDesc.timeStepMethod = NX_TIMESTEP_FIXED;
        auto* scene = sdk->createScene(sceneDesc);
        if (!scene) fail("Scene creation failed");
        std::vector<NxConvexMesh*> meshes;
        std::vector<std::vector<NxActor*>> actors;
        for (unsigned i=0;i<tails;++i) actors.push_back(createHair(*sdk,*scene,initial[i],argv[2],meshes));
        std::printf("{\"solver\":\"client PhysX 2.8.4\",\"clientCoreVerified\":true,\"clientSceneParity\":false,\"dt\":%.9g,\"frames\":[", double(dt));
        for (unsigned f=0;f<frames;++f) {
            if (f) {
                for (unsigned t=0;t<tails;++t) actors[t][0]->moveGlobalPose(targets[f][t]);
                scene->simulate(dt); scene->flushStream();
                if (!scene->fetchResults(NX_RIGID_BODY_FINISHED,true)) fail("Simulation fetch failed");
            }
            if (f) std::putchar(','); std::putchar('[');
            for (unsigned t=0;t<tails;++t) {
                if (t) std::putchar(','); std::putchar('[');
                for (unsigned a=0;a<actors[t].size();++a) {
                    const auto p=actors[t][a]->getGlobalPose(); const NxQuat q(p.M);
                    if (!p.isFinite()) fail("Nonfinite simulated pose");
                    std::printf("%s[%.9g,%.9g,%.9g,%.9g,%.9g,%.9g,%.9g]",a ? "," : "",
                        double(p.t.x),double(p.t.y),double(p.t.z),double(q.x),double(q.y),double(q.z),double(q.w));
                }
                std::putchar(']');
            }
            std::putchar(']');
        }
        std::printf("]}\n");
        sdk->releaseScene(*scene);
        for (auto* mesh : meshes) sdk->releaseConvexMesh(*mesh);
        release(sdk); FreeLibrary(loader); FreeLibrary(core);
        return 0;
    } catch (const std::exception& error) {
        std::fprintf(stderr,"%s\n",error.what()); return 2;
    }
}
