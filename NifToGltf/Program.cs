// I use this to convert the nif files to gltf
// Noesis is required to run this
// No, I won't make this readable, maybe in the future, uwu

// Quickest tutorial I can give:
// 1. Download Noesis
// 2. Export the nif files to Resources/Models and DDS textures to Resources/Models/Textures
// 3. Run this app — it copies the DDS files Noesis needs next to each NIF, then converts NIF -> GLTF in one pass
// 4. glhf

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using MaplePacketLib2.Tools;

string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
string modelDir = Path.Combine(solutionDir, "Maple2Storage", "Resources", "Models");
string texturesDir = Path.Combine(solutionDir, "Maple2Storage", "Resources", "Models", "Textures");
string output = GetOutputDir(args) ?? Path.Combine(solutionDir, "Maple2Storage", "Resources", "GLTF");
const char quote = '"';

string[] textures = Directory.GetFileSystemEntries(texturesDir, "*.dds", SearchOption.AllDirectories);
string[] models = Directory.GetFileSystemEntries(modelDir, "*.nif", SearchOption.AllDirectories);

// Optional: filter by NPC/model id(s). e.g. `dotnet run -- --filter-ids 21000174,21000173`
string[] filterIds = GetFilterIds(args);
bool dryRun = args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
bool postProcessExisting = args.Any(a => a.Equals("--post-process-existing", StringComparison.OrdinalIgnoreCase));

if (dryRun) {
    Console.WriteLine("Dry run active. No GLTF files will be changed.");
}

if (postProcessExisting) {
    Console.WriteLine("Post-processing existing GLTF files when conversion is skipped.");
}

Console.WriteLine($"Output directory: {output}");

if (filterIds.Length > 0) {
    models = models
        .Where(m => filterIds.Any(id => m.Contains(id, StringComparison.OrdinalIgnoreCase)))
        .ToArray();
    Console.WriteLine($"Filter ids active ({string.Join(", ", filterIds)}) -> {models.Length} NIF(s) match.");
} else {
    Console.WriteLine($"Processing all {models.Length} NIF(s).");
}

foreach (string modelPath in models) {
    string fileName = Path.GetFileNameWithoutExtension(modelPath);
    string outputFolder = new(Path.Combine(output, fileName));
    if (!dryRun) {
        Directory.CreateDirectory(outputFolder);
    }
    string directoryName = Path.GetDirectoryName(modelPath)!;

    if (dryRun) {
        Console.WriteLine($"Dry run: would copy referenced DDS textures for {fileName}.");
    } else {
        try {
            GetTexturesFromNif();
        } catch (Exception e) {
            Console.WriteLine($"  Skipping texture copy for {fileName}: {e.Message}");
        }
    }

    try {
        Console.WriteLine($"Converting {fileName} nif to gltf");

        // check if gltf already exists
        string baseGltfPath = @$"{output}\{fileName}\{fileName}.gltf";
        if (File.Exists(baseGltfPath)) {
            Console.WriteLine("Skipping " + fileName + " base model.");
            if (postProcessExisting) {
                PostProcessGltf(baseGltfPath, dryRun);
            }
        } else if (dryRun) {
            Console.WriteLine($"Dry run: would convert {fileName} base model.");
        } else {
            string strCmdText2 = @$"/C noesis.exe ?cmode {quote}{modelPath}{quote} {quote}{output}\{fileName}\{fileName}.gltf{quote}";
            Process? process2 = Process.Start("CMD.exe", strCmdText2);
            // wait for process to finish
            process2.WaitForExit();

            Console.WriteLine("Finished converting " + fileName + " base model.");
            PostProcessGltf(baseGltfPath, dryRun);
        }

        // Only do animations for NPC folder
        if (!directoryName.Contains("Npc")) {
            continue;
        }

        // get output textures .png
        string[] baseTextures = Directory.GetFileSystemEntries(outputFolder, "*.png", SearchOption.TopDirectoryOnly);

        // find kf files that match file name
        string[] animations = Directory.GetFileSystemEntries(directoryName, "*.kf", SearchOption.TopDirectoryOnly);
        foreach (string animation in animations) {
            string animationName = Path.GetFileNameWithoutExtension(animation);

            Console.WriteLine($"Converting {fileName} kf to gltf");
            // check if gltf dont exists
            string animationGltfPath = @$"{output}\{fileName}\{animationName}.gltf";
            if (!File.Exists(animationGltfPath)) {
                if (dryRun) {
                    Console.WriteLine($"Dry run: would convert {fileName} {animationName} animation.");
                    continue;
                }

                // CD into input directory
                string cdCmdText = $"/C cd /d {quote}{directoryName}{quote}";
                string strCmdText = @$"{cdCmdText} && noesis.exe ?cmode {quote}{animation}{quote} {quote}{output}\{fileName}\{animationName}.gltf{quote}";
                Process? process = Process.Start("CMD.exe", strCmdText);
                // wait for process to finish
                process.WaitForExit();

                // get output animation textures .png
                string[] pngs = Directory.GetFileSystemEntries(outputFolder, "*.png", SearchOption.TopDirectoryOnly);
                string[] animationTextures = pngs.Where(
                    x => Path.GetFileName(x).StartsWith(animationName)
                ).ToArray();

                // check if animations and base count match if not continue
                if (animationTextures.Length != baseTextures.Length) {
                    Console.WriteLine("Skipping cleanup of " + fileName + " animation.");
                    continue;
                }

                // open gltf file, rename textures to use base texture for all animations
                string gltf = File.ReadAllText(animationGltfPath);
                for (int i = 0; i < animationTextures.Length; i++) {
                    string animationTexture = Path.GetFileName(animationTextures[i]);
                    string baseTexture = Path.GetFileName(baseTextures[i]);
                    gltf = gltf.Replace(animationTexture, baseTexture);
                }

                File.WriteAllText(animationGltfPath, gltf);

                // delete animation textures
                foreach (string animationTexture in animationTextures) {
                    File.Delete(animationTexture);
                }

                Console.WriteLine("Finished converting " + animationName + " animation.");
                PostProcessGltf(animationGltfPath, dryRun);
            } else {
                Console.WriteLine("Skipping " + fileName + " animation.");
                if (postProcessExisting) {
                    PostProcessGltf(animationGltfPath, dryRun);
                }
            }
        }
    } catch (Exception e) {
        Console.WriteLine(e);
        throw;
    }

    void GetTexturesFromNif() {
        byte[] fileBytes = File.ReadAllBytes(modelPath);
        PacketReader reader = new(fileBytes);
        reader.Skip(39);
        reader.Skip(4); // version
        reader.Skip(1); // endianness

        reader.Skip(4); // user version
        int numBlocks = reader.ReadInt(); // num blocks
        int metadataSize = reader.ReadInt(); // metadata size
        reader.Skip(metadataSize);

        short count = reader.ReadShort();
        for (int i = 0; i < count; i++) {
            reader.ReadString();
        }

        reader.Skip(numBlocks * 2);
        reader.Skip(numBlocks * 4);
        int count2 = reader.ReadInt();
        reader.Skip(4);
        List<string> ddsFiles = new();
        for (int i = 0; i < count2; i++) {
            string values = reader.ReadString();
            if (values.Contains(".dds", StringComparison.OrdinalIgnoreCase)) {
                ddsFiles.Add(values);
            }
        }

        foreach (string ddsFile in ddsFiles) {
            string? file = textures.FirstOrDefault(x => x.Contains(ddsFile, StringComparison.OrdinalIgnoreCase));
            if (file is null) {
                continue;
            }

            // get path without filename
            string path = directoryName;

            string destFileName = path + '\\' + file.Split('\\').Last();
            if (File.Exists(destFileName)) {
                continue;
            }

            Console.WriteLine($"Copying {ddsFile} to {destFileName}");
            File.Copy(file, destFileName);
        }
    }
}

static void PostProcessGltf(string gltfPath, bool dryRun) {
    if (!File.Exists(gltfPath)) {
        Console.WriteLine($"  Post-process skipped, GLTF does not exist: {gltfPath}");
        return;
    }

    JsonNode? root;
    try {
        root = JsonNode.Parse(File.ReadAllText(gltfPath));
    } catch (JsonException e) {
        Console.WriteLine($"  Post-process skipped, invalid GLTF JSON in {Path.GetFileName(gltfPath)}: {e.Message}");
        return;
    }

    if (root is null) {
        Console.WriteLine($"  Post-process skipped, GLTF JSON is empty: {gltfPath}");
        return;
    }

    JsonArray? scenes = root["scenes"] as JsonArray;
    JsonArray? nodes = root["nodes"] as JsonArray;
    JsonArray? meshes = root["meshes"] as JsonArray;
    int sceneIndex = root["scene"]?.GetValue<int>() ?? 0;
    JsonObject? scene = scenes?.ElementAtOrDefault(sceneIndex) as JsonObject;
    JsonArray? sceneNodes = scene?["nodes"] as JsonArray;

    if (scene is null || sceneNodes is null || nodes is null || meshes is null) {
        Console.WriteLine($"  Post-process skipped, missing scenes/nodes/meshes: {Path.GetFileName(gltfPath)}");
        return;
    }

    List<int> originalSceneNodes = sceneNodes
        .Select(node => node?.GetValue<int>())
        .Where(node => node.HasValue)
        .Select(node => node!.Value)
        .ToList();

    bool hasDummyAvatarSignature = HasDummyAvatarSignature(originalSceneNodes, nodes, meshes);
    if (!hasDummyAvatarSignature) {
        Console.WriteLine($"  Post-process: no dummy avatar signature found in {Path.GetFileName(gltfPath)}.");
        return;
    }

    List<int> filteredSceneNodes = originalSceneNodes
        .Where(nodeIndex => !IsDummyAvatarMeshRoot(nodeIndex, nodes, meshes))
        .ToList();

    if (filteredSceneNodes.Count == originalSceneNodes.Count) {
        Console.WriteLine($"  Post-process: no dummy mesh roots found in {Path.GetFileName(gltfPath)}.");
        return;
    }

    Console.WriteLine($"  Post-process {Path.GetFileName(gltfPath)}:");
    Console.WriteLine($"    Scene roots before: {FormatNodeList(originalSceneNodes, nodes, meshes)}");
    Console.WriteLine($"    Scene roots after:  {FormatNodeList(filteredSceneNodes, nodes, meshes)}");

    if (dryRun) {
        Console.WriteLine("    Dry run: file not changed.");
        return;
    }

    JsonArray newSceneNodes = new();
    foreach (int nodeIndex in filteredSceneNodes) {
        newSceneNodes.Add(nodeIndex);
    }

    scene["nodes"] = newSceneNodes;

    JsonSerializerOptions options = new() {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    File.WriteAllText(gltfPath, root.ToJsonString(options));
}

static bool IsDummyAvatarMeshRoot(int nodeIndex, JsonArray nodes, JsonArray meshes) {
    if (nodeIndex < 0 || nodeIndex >= nodes.Count || nodes[nodeIndex] is not JsonObject node) {
        return false;
    }

    int? meshIndex = node["mesh"]?.GetValue<int>();
    if (!meshIndex.HasValue || meshIndex.Value < 0 || meshIndex.Value >= meshes.Count) {
        return false;
    }

    string? meshName = (meshes[meshIndex.Value] as JsonObject)?["name"]?.GetValue<string>();
    return IsDummyAvatarMeshName(meshName);
}

static bool HasDummyAvatarSignature(IEnumerable<int> sceneNodeIndexes, JsonArray nodes, JsonArray meshes) {
    List<string> meshNames = sceneNodeIndexes
        .Select(nodeIndex => GetRootMeshName(nodeIndex, nodes, meshes))
        .Where(meshName => !string.IsNullOrWhiteSpace(meshName))
        .Select(meshName => meshName!)
        .ToList();

    bool hasSkinOrShell = meshNames.Any(meshName => meshName.Equals("SH", StringComparison.OrdinalIgnoreCase));
    bool hasPantsOrParts = meshNames.Any(meshName =>
        meshName.Equals("PA", StringComparison.OrdinalIgnoreCase)
        || meshName.StartsWith("PA:", StringComparison.OrdinalIgnoreCase));
    bool hasHair = meshNames.Any(meshName => meshName.Equals("HR", StringComparison.OrdinalIgnoreCase));
    bool hasKeptBody = meshNames.Any(meshName => !IsDummyAvatarMeshName(meshName));

    return hasSkinOrShell && hasPantsOrParts && hasHair && hasKeptBody;
}

static string? GetRootMeshName(int nodeIndex, JsonArray nodes, JsonArray meshes) {
    if (nodeIndex < 0 || nodeIndex >= nodes.Count || nodes[nodeIndex] is not JsonObject node) {
        return null;
    }

    int? meshIndex = node["mesh"]?.GetValue<int>();
    if (!meshIndex.HasValue || meshIndex.Value < 0 || meshIndex.Value >= meshes.Count) {
        return null;
    }

    return (meshes[meshIndex.Value] as JsonObject)?["name"]?.GetValue<string>();
}

static bool IsDummyAvatarMeshName(string? meshName) {
    if (string.IsNullOrWhiteSpace(meshName)) {
        return false;
    }

    return meshName.Equals("SH", StringComparison.OrdinalIgnoreCase)
        || meshName.Equals("PA", StringComparison.OrdinalIgnoreCase)
        || meshName.StartsWith("PA:", StringComparison.OrdinalIgnoreCase)
        || meshName.Equals("FA01", StringComparison.OrdinalIgnoreCase)
        || meshName.Equals("HR", StringComparison.OrdinalIgnoreCase);
}

static string FormatNodeList(IEnumerable<int> nodeIndexes, JsonArray nodes, JsonArray meshes) {
    return string.Join(", ", nodeIndexes.Select(nodeIndex => FormatNode(nodeIndex, nodes, meshes)));
}

static string FormatNode(int nodeIndex, JsonArray nodes, JsonArray meshes) {
    if (nodeIndex < 0 || nodeIndex >= nodes.Count || nodes[nodeIndex] is not JsonObject node) {
        return $"{nodeIndex}:<missing node>";
    }

    string nodeName = node["name"]?.GetValue<string>() ?? "<unnamed node>";
    int? meshIndex = node["mesh"]?.GetValue<int>();
    if (!meshIndex.HasValue || meshIndex.Value < 0 || meshIndex.Value >= meshes.Count) {
        return $"{nodeIndex}:{nodeName}";
    }

    string meshName = (meshes[meshIndex.Value] as JsonObject)?["name"]?.GetValue<string>() ?? "<unnamed mesh>";
    return $"{nodeIndex}:{nodeName}/{meshName}";
}

static string[] GetFilterIds(string[] args) {
    string? filterIdsArg = null;
    for (int i = 0; i < args.Length; i++) {
        string arg = args[i];
        if (arg.Equals("--filter-ids", StringComparison.OrdinalIgnoreCase)) {
            if (i + 1 >= args.Length) {
                throw new ArgumentException("--filter-ids requires a comma-separated value, e.g. --filter-ids 21000174,21000173");
            }

            filterIdsArg = args[i + 1];
            break;
        }

        const string prefix = "--filter-ids=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
            filterIdsArg = arg[prefix.Length..];
            break;
        }
    }

    if (string.IsNullOrWhiteSpace(filterIdsArg)) {
        return [];
    }

    return filterIdsArg
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static string? GetOutputDir(string[] args) {
    for (int i = 0; i < args.Length; i++) {
        string arg = args[i];
        if (arg.Equals("--output-dir", StringComparison.OrdinalIgnoreCase)) {
            if (i + 1 >= args.Length) {
                throw new ArgumentException("--output-dir requires a path value.");
            }

            return Path.GetFullPath(args[i + 1]);
        }

        const string prefix = "--output-dir=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
            return Path.GetFullPath(arg[prefix.Length..]);
        }
    }

    return null;
}
