// Offline diagnostic for a locally installed PhysX 2.8.4 runtime.
// Reads existing cooked convex data. Does not cook or export release assets.
#define NOMINMAX
#include <windows.h>
#include <NxPhysics.h>
#include <NxStream.h>
#include <PhysXLoader.h>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <vector>

class ReadStream final : public NxStream {
    std::vector<unsigned char> bytes;
    mutable size_t offset = 0;
    template<class T> T read() const { T value; readBuffer(&value, sizeof(value)); return value; }
    [[noreturn]] static void reject() { std::fputs("Invalid cooked stream operation\n", stderr); std::exit(3); }
public:
    explicit ReadStream(const std::filesystem::path& path) {
        std::ifstream file(path, std::ios::binary);
        if (!file) reject();
        bytes.assign(std::istreambuf_iterator<char>(file), {});
        if (bytes.size() < 8 || std::memcmp(bytes.data(), "NXS", 3) != 0) reject();
    }
    size_t consumed() const { return offset; }
    size_t size() const { return bytes.size(); }
    NxU8 readByte() const override { return read<NxU8>(); }
    NxU16 readWord() const override { return read<NxU16>(); }
    NxU32 readDword() const override { return read<NxU32>(); }
    NxF32 readFloat() const override { return read<NxF32>(); }
    NxF64 readDouble() const override { return read<NxF64>(); }
    void readBuffer(void* buffer, NxU32 size) const override {
        if (size > bytes.size() - offset) reject();
        std::memcpy(buffer, bytes.data() + offset, size);
        offset += size;
    }
    NxStream& storeByte(NxU8) override { reject(); }
    NxStream& storeWord(NxU16) override { reject(); }
    NxStream& storeDword(NxU32) override { reject(); }
    NxStream& storeFloat(NxF32) override { reject(); }
    NxStream& storeDouble(NxF64) override { reject(); }
    NxStream& storeBuffer(const void*, NxU32) override { reject(); }
};

class Output final : public NxUserOutputStream {
public:
    void reportError(NxErrorCode code, const char* message, const char*, int) override {
        std::fprintf(stderr, "PhysX %d: %s\n", int(code), message);
    }
    NxAssertResponse reportAssertViolation(const char* message, const char*, int) override {
        std::fprintf(stderr, "PhysX assertion: %s\n", message);
        std::exit(4);
    }
    void print(const char* message) override { std::fputs(message, stderr); }
};

#ifndef PHYSX284_NO_PROBE_MAIN
int wmain(int argc, wchar_t** argv) {
    if (argc < 2) {
        std::fputs("Usage: probe_physx284 <client DLL directory> [cooked convex blobs...]\n", stderr);
        return 2;
    }
    const auto directory = std::filesystem::canonical(argv[1]);
    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    AddDllDirectory(directory.c_str());
    const auto core = LoadLibraryExW((directory / L"PhysXCore64.dll").c_str(), nullptr,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    const auto loader = LoadLibraryExW((directory / L"PhysXLoader64.dll").c_str(), nullptr,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!core || !loader) { std::fprintf(stderr, "DLL load failed: %lu\n", GetLastError()); return 2; }
    const auto create = reinterpret_cast<decltype(&NxCreatePhysicsSDK)>(GetProcAddress(loader, "NxCreatePhysicsSDK"));
    const auto release = reinterpret_cast<decltype(&NxReleasePhysicsSDK)>(GetProcAddress(loader, "NxReleasePhysicsSDK"));
    if (!create || !release) return 2;
    Output output;
    NxPhysicsSDKDesc settings;
    settings.flags = NX_SDKF_NO_HARDWARE;
    NxSDKCreateError error;
    NxPhysicsSDK* sdk = create(NX_PHYSICS_SDK_VERSION, nullptr, &output, settings, &error, PHYSX_284_CORE_GUID);
    if (!sdk) { std::fprintf(stderr, "SDK creation failed: %d\n", int(error)); return 2; }
    HMODULE implementation = nullptr;
    const auto firstMethod = *reinterpret_cast<void***>(sdk);
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(firstMethod[0]), &implementation);
    if (implementation != core) {
        std::fputs("SDK implementation does not belong to the specified client core DLL\n", stderr);
        release(sdk);
        return 2;
    }
    NxU32 api, desc, branch;
    const auto version = sdk->getInternalVersion(api, desc, branch);
    NxD6JointDesc joint;
    const auto position = [&](const void* field) { return size_t(static_cast<const char*>(field) - reinterpret_cast<const char*>(&joint)); };
    std::printf("{\"version\":%u,\"apiRevision\":%u,\"descriptorRevision\":%u,\"branch\":%u,\"clientCoreVerified\":true,", version, api, desc, branch);
    std::printf("\"d6Offsets\":{\"localNormal0\":%zu,\"localAxis0\":%zu,\"linearLimit\":%zu,\"swing1Limit\":%zu,\"swing2Limit\":%zu,\"twistLow\":%zu,\"twistHigh\":%zu,\"xDrive\":%zu},\"meshes\":[",
        position(&joint.localNormal[0]), position(&joint.localAxis[0]), position(&joint.linearLimit),
        position(&joint.swing1Limit), position(&joint.swing2Limit), position(&joint.twistLimit.low),
        position(&joint.twistLimit.high), position(&joint.xDrive));
    bool success = true;
    for (int i = 2; i < argc; ++i) {
        ReadStream stream(argv[i]);
        NxConvexMesh* mesh = sdk->createConvexMesh(stream);
        if (i > 2) std::putchar(',');
        if (!mesh) { std::printf("{\"argument\":%d,\"loaded\":false}", i); success = false; continue; }
        NxConvexMeshDesc geometry;
        const bool saved = mesh->saveToDesc(geometry);
        if (!saved) geometry.setToDefault();
        const bool complete = stream.consumed() == stream.size();
        success = success && saved && complete;
        std::printf("{\"argument\":%d,\"loaded\":true,\"complete\":%s,\"bytes\":%zu,\"vertices\":%u,\"triangles\":%u,\"points\":[",
            i, complete ? "true" : "false", stream.consumed(), geometry.numVertices, geometry.numTriangles);
        if (saved) for (NxU32 v = 0; v < geometry.numVertices; ++v) {
            NxVec3 point;
            std::memcpy(&point, static_cast<const char*>(geometry.points) + v * geometry.pointStrideBytes, sizeof(point));
            std::printf("%s[%.9g,%.9g,%.9g]", v ? "," : "", double(point.x), double(point.y), double(point.z));
        }
        std::printf("]}");
        sdk->releaseConvexMesh(*mesh);
    }
    std::printf("]}\n");
    release(sdk);
    FreeLibrary(loader);
    FreeLibrary(core);
    return success ? 0 : 1;
}
#endif
