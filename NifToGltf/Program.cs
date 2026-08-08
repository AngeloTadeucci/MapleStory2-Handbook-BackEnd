// I use this to convert the nif files to gltf
// Noesis is required to run this
// No, I won't make this readable, maybe in the future, uwu

// Quickest tutorial I can give:
// 1. Download Noesis
// 2. Export the nif files to Resources/Models and DDS textures to Resources/Models/Textures
// 3. Run this app — it copies the DDS files Noesis needs next to each NIF, then converts NIF -> GLTF in one pass
// 4. glhf

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using MaplePacketLib2.Tools;

string solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
string modelDir = Path.Combine(solutionDir, "Maple2Storage", "Resources", "Models");
string texturesDir = Path.Combine(solutionDir, "Maple2Storage", "Resources", "Models", "Textures");
string output = GetOutputDir(args) ?? Path.Combine(solutionDir, "Maple2Storage", "Resources", "GLTF");

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

// `--repair-only` walks an existing GLTF library and only runs the JSON repair, without
// needing the source NIFs staged. Useful for cleaning up output from older Noesis runs.
if (args.Any(a => a.Equals("--repair-only", StringComparison.OrdinalIgnoreCase))) {
    RepairLibrary(output, dryRun);
    return;
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
            RunNoesis(directoryName, modelPath, baseGltfPath);

            Console.WriteLine("Finished converting " + fileName + " base model.");
            PostProcessGltf(baseGltfPath, dryRun);
        }

        // find kf files that match file name. Almost all of them belong to an NPC, but a few NPCs
        // use a model that sits under Map, so look for the animations instead of the folder.
        string[] animations = Directory.GetFileSystemEntries(directoryName, "*.kf", SearchOption.TopDirectoryOnly);
        if (animations.Length == 0) {
            continue;
        }

        // get output textures .png
        string[] baseTextures = Directory.GetFileSystemEntries(outputFolder, "*.png", SearchOption.TopDirectoryOnly);

        foreach (string animation in animations) {
            string animationName = Path.GetFileNameWithoutExtension(animation);

            // The name of a kf file is not always the name of the sequence it holds: it can carry
            // the name of the model, or the name of the model and the sequence together. The
            // handbook asks for the sequence name, so prefer the name that the kfm gives.
            string? sequenceName = GetSequenceNameFromKfm(directoryName, animationName);
            string source = "the kfm";
            if (sequenceName is null) {
                sequenceName = StripModelPrefix(animationName, fileName);
                source = "the name of the kf file";
            }

            if (sequenceName is not null && sequenceName != animationName) {
                Console.WriteLine($"Naming the {animationName} animation of {fileName} {sequenceName}, taken from {source}.");
                animationName = sequenceName;
            } else if (sequenceName is null && string.Equals(animationName, fileName, StringComparison.OrdinalIgnoreCase)) {
                // Without another name this animation would overwrite the base model.
                Console.WriteLine($"Skipping the {fileName} animation: it has the name of the model and the kfm gives no other name.");
                continue;
            }

            Console.WriteLine($"Converting {fileName} kf to gltf");
            // check if gltf dont exists
            string animationGltfPath = @$"{output}\{fileName}\{animationName}.gltf";
            if (!File.Exists(animationGltfPath)) {
                if (dryRun) {
                    Console.WriteLine($"Dry run: would convert {fileName} {animationName} animation.");
                    continue;
                }

                // A .kf holds animation only, so Noesis needs the model nif as well. Passing it
                // keeps the Noesis file dialog closed.
                RunNoesis(directoryName, animation, animationGltfPath, "-kfpairnif", modelPath);

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

// Some kfm files hold no name for a sequence. Those models name the kf file after the model and
// the sequence together, so the sequence is what remains once the name of the model is removed.
static string? StripModelPrefix(string animationName, string modelName) {
    string prefix = $"{modelName}_";
    return animationName.Length > prefix.Length && animationName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? animationName[prefix.Length..].ToLower()
        : null;
}

// A kfm lists each sequence as the path of its kf file followed by the name of the sequence. Read
// those two strings out of the binary to find what a given kf is called.
static string? GetSequenceNameFromKfm(string directory, string animationName) {
    foreach (string kfmFile in Directory.GetFiles(directory, "*.kfm", SearchOption.TopDirectoryOnly)) {
        List<string> values = [];
        StringBuilder value = new();
        foreach (char character in Encoding.ASCII.GetString(File.ReadAllBytes(kfmFile))) {
            if (character is >= ' ' and <= '~') {
                value.Append(character);
                continue;
            }

            if (value.Length >= 3) {
                values.Add(value.ToString());
            }

            value.Clear();
        }

        for (int i = 0; i < values.Count - 1; i++) {
            if (!values[i].EndsWith(".kf", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileNameWithoutExtension(values[i]), animationName, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // The name runs up to the first byte that cannot be part of it. A kfm holds no
            // separator between the name and the field that follows, so "Idle_A%" is common.
            string candidate = values[i + 1];
            int length = 0;
            while (length < candidate.Length && (char.IsAsciiLetterOrDigit(candidate[length]) || candidate[length] == '_')) {
                length++;
            }

            if (length != 0) {
                return candidate[..length].ToLower();
            }
        }
    }

    return null;
}

// Runs noesis.exe directly instead of through CMD.exe, so no console window is created for each
// of the thousands of conversions. Noesis still resolves .kfm/.nif references relative to the
// working directory, so the caller passes the folder that holds the source model.
static void RunNoesis(string workingDirectory, string inputPath, string outputPath, params string[] extraArgs) {
    ProcessStartInfo startInfo = new() {
        FileName = "noesis.exe",
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    startInfo.ArgumentList.Add("?cmode");
    startInfo.ArgumentList.Add(inputPath);
    startInfo.ArgumentList.Add(outputPath);
    foreach (string extraArg in extraArgs) {
        startInfo.ArgumentList.Add(extraArg);
    }

    using Process? process = Process.Start(startInfo);
    process?.WaitForExit();
}

static void RepairLibrary(string libraryDir, bool dryRun) {
    if (!Directory.Exists(libraryDir)) {
        Console.WriteLine($"Repair-only: directory does not exist: {libraryDir}");
        return;
    }

    string[] gltfFiles = Directory.GetFiles(libraryDir, "*.gltf", SearchOption.AllDirectories);
    Console.WriteLine($"Repair-only: scanning {gltfFiles.Length} GLTF file(s) under {libraryDir}");

    int repaired = 0;
    foreach (string gltfPath in gltfFiles) {
        string text = File.ReadAllText(gltfPath);
        if (!TryRepairGltfText(text, out string repairedText, out List<string> repairs)) {
            continue;
        }

        repaired++;
        Console.WriteLine($"  {Path.GetRelativePath(libraryDir, gltfPath)}: {string.Join(", ", repairs)}");
        if (dryRun) {
            continue;
        }

        // Keep the original next to the repaired file so the change is reversible.
        string backupPath = gltfPath + ".prerepair";
        if (!File.Exists(backupPath)) {
            File.Copy(gltfPath, backupPath);
        }

        File.WriteAllText(gltfPath, repairedText);
    }

    Console.WriteLine(dryRun
        ? $"Repair-only dry run: {repaired} file(s) would be repaired."
        : $"Repair-only: repaired {repaired} file(s).");
}

static void PostProcessGltf(string gltfPath, bool dryRun) {
    if (!File.Exists(gltfPath)) {
        Console.WriteLine($"  Post-process skipped, GLTF does not exist: {gltfPath}");
        return;
    }

    string gltfText = File.ReadAllText(gltfPath);
    if (TryRepairGltfText(gltfText, out string repairedText, out List<string> repairs)) {
        Console.WriteLine($"  Post-process repaired {Path.GetFileName(gltfPath)}: {string.Join(", ", repairs)}");
        if (dryRun) {
            Console.WriteLine("    Dry run: repair not written.");
        } else {
            File.WriteAllText(gltfPath, repairedText);
        }

        gltfText = repairedText;
    }

    JsonNode? root;
    try {
        root = JsonNode.Parse(gltfText);
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

// Noesis passes two kinds of garbage straight through into its GLTF output, and both make the file
// invalid JSON so nothing downstream can read it:
//   * non-finite floats from degenerate bones, written as bare "nan" or as the MSVC "1.#QNAN" form;
//   * raw control bytes embedded in NIF strings, which land unescaped inside JSON string literals.
// Rewrite non-finite numbers to 0.0 and escape control bytes. Returns true when anything changed.
static bool TryRepairGltfText(string text, out string repaired, out List<string> repairs) {
    const string tokenChars = "+-.#()";
    int nonFinite = 0;
    int controlChars = 0;
    int invalidEscapes = 0;

    StringBuilder builder = new(text.Length);
    bool inString = false;
    bool escaped = false;

    for (int i = 0; i < text.Length; i++) {
        char c = text[i];

        if (inString) {
            if (escaped) {
                escaped = false;
            } else if (c == '\\') {
                // Node names sometimes hold the original artist's Windows path, e.g.
                // "D:\ms2team\ms2-design\...", whose lone backslashes are not valid JSON escapes.
                if (IsValidEscape(text, i)) {
                    escaped = true;
                } else {
                    builder.Append(@"\\");
                    invalidEscapes++;
                    continue;
                }
            } else if (c == '"') {
                inString = false;
            } else if (c < 0x20) {
                builder.Append($"\\u{(int) c:x4}");
                controlChars++;
                continue;
            }

            builder.Append(c);
            continue;
        }

        if (c == '"') {
            inString = true;
            builder.Append(c);
            continue;
        }

        if (!char.IsLetterOrDigit(c) && !tokenChars.Contains(c)) {
            builder.Append(c);
            continue;
        }

        // Outside a string the only bare tokens JSON allows are numbers and true/false/null.
        // Grab the whole token so a malformed number is replaced as a unit rather than in pieces.
        int start = i;
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || tokenChars.Contains(text[i]))) {
            i++;
        }

        string token = text[start..i];
        i--;

        // double.TryParse accepts "NaN" and "Infinity" itself, so check the parsed value is finite too.
        bool isFiniteNumber = double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value);
        if (token is "true" or "false" or "null" || isFiniteNumber) {
            builder.Append(token);
            continue;
        }

        builder.Append("0.0");
        nonFinite++;
    }

    repairs = [];
    if (nonFinite > 0) {
        repairs.Add($"{nonFinite} non-finite number(s) -> 0.0");
    }

    if (controlChars > 0) {
        repairs.Add($"{controlChars} control character(s) escaped");
    }

    if (invalidEscapes > 0) {
        repairs.Add($"{invalidEscapes} invalid backslash escape(s) escaped");
    }

    repaired = repairs.Count > 0 ? builder.ToString() : text;
    return repairs.Count > 0;
}

// True when the backslash at index starts an escape sequence JSON actually allows.
static bool IsValidEscape(string text, int index) {
    if (index + 1 >= text.Length) {
        return false;
    }

    char next = text[index + 1];
    if (next is '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't') {
        return true;
    }

    if (next != 'u' || index + 5 >= text.Length) {
        return false;
    }

    for (int i = index + 2; i <= index + 5; i++) {
        if (!Uri.IsHexDigit(text[i])) {
            return false;
        }
    }

    return true;
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
