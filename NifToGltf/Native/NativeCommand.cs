using System.Text.Json;

namespace NifToGltf.Native;

internal static class NativeCommand {
    public static int Run(string[] args) {
        try {
            if (args.Contains("--help")) {
                Console.WriteLine("""
                    Native NIF 30.2.0.3 converter
                      --native --input model.nif --output model.gltf [--textures directory]
                      --native --kfm model.kfm --clips all --output model.gltf [--textures directory]
                      --native --input gear.nif --skeleton body.nif --output gear.gltf [--attach "bone name"]
                      --native --input body.nif --animations directory --clips idle_a,walk_a --output body.gltf
                      --native --batch --input model-directory --output empty-directory [--textures directory]

                    --overwrite explicitly replaces one output. Batch always requires an empty destination.
                    CP attachments infer Bip01 Head. Other rigid slots require --attach.
                    Batch writes static exports and batch-report.json. It does not infer equipment skeletons.
                    Nonlinear animation curves are sampled with adaptive error checks.
                    Unsupported data fails explicitly. Noesis remains the default mode without --native.
                    """);
                return 0;
            }
            Dictionary<string, string> options = [];
            bool batch = false, overwrite = false;
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--native") continue;
                if (args[i] == "--batch") { batch = true; continue; }
                if (args[i] == "--overwrite") { overwrite = true; continue; }
                if (args[i] is not ("--input" or "--output" or "--textures" or "--skeleton" or "--attach" or "--animations" or "--clips" or "--kfm") || i + 1 >= args.Length) {
                    throw new ArgumentException("Usage: --native --input model.nif --output model.gltf [--textures directory] [--skeleton body.nif] [--attach bone]");
                }
                options.Add(args[i], args[++i]);
            }
            KfmDocument? kfm = options.TryGetValue("--kfm", out string? kfmPath) ? KfmDocument.Read(kfmPath) : null;
            if (kfm is not null && !options.ContainsKey("--input")) options["--input"] = kfm.Model;
            if (!options.TryGetValue("--input", out string? input) || !options.TryGetValue("--output", out string? output)) {
                throw new ArgumentException("--native requires --input and --output.");
            }
            if (batch) {
                if (overwrite) throw new ArgumentException("Batch output must be an empty directory.");
                if (options.Keys.Any(key => key is not ("--input" or "--output" or "--textures"))) {
                    throw new ArgumentException("--batch currently converts static models; per-file skeletons and clip selections must be supplied in individual conversions.");
                }
                return NativeBatch.Run(input, output, options.GetValueOrDefault("--textures"));
            }
            if (!string.Equals(Path.GetExtension(output), ".gltf", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("Native output must have a .gltf extension.");
            }
            if (File.Exists(output) && !overwrite) throw new IOException($"Output already exists: {output}. Choose a new path or use --overwrite.");
            NifDocument document = NifDocument.Load(input);
            if (options.TryGetValue("--skeleton", out string? skeleton)) {
                SkeletonGraft.Apply(document, NifDocument.Load(skeleton), options.GetValueOrDefault("--attach"));
            } else if (options.ContainsKey("--attach")) throw new ArgumentException("--attach requires --skeleton.");
            List<AnimationClip> clips = [];
            List<string> animationErrors = [];
            AnimationClip? ReadClip(string path, string? sequenceName = null) {
                try { return AnimationReader.Read(path, sequenceName: sequenceName); }
                catch (Exception e) when (e is InvalidDataException or NotSupportedException or IOException) {
                    animationErrors.Add(e.Message);
                    return null;
                }
            }
            if (options.GetValueOrDefault("--clips") == "all" && Path.GetFileNameWithoutExtension(input) is "f_body" or "m_body") {
                throw new ArgumentException("Player bodies require an explicit curated --clips list, not all.");
            }
            if (kfm is not null) {
                if (options.ContainsKey("--animations")) throw new ArgumentException("Use --kfm or --animations, not both.");
                if (!options.TryGetValue("--clips", out string? selection)) throw new ArgumentException("--kfm requires --clips all or a comma-separated sequence list.");
                HashSet<string> requested = selection.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                KfmClip[] selected = selection == "all" ? kfm.Clips : kfm.Clips.Where(clip => requested.Remove(clip.Name)).ToArray();
                if (selection != "all" && requested.Count > 0) throw new FileNotFoundException($"KFM clips not found: {string.Join(", ", requested)}.");
                foreach (KfmClip clip in selected) {
                    AnimationClip? parsed = ReadClip(clip.File, clip.Name);
                    if (parsed is not null) clips.Add(parsed with { Name = string.IsNullOrEmpty(clip.Name) ? parsed.Name : clip.Name });
                }
            } else if (options.TryGetValue("--animations", out string? animationDirectory)) {
                if (!options.TryGetValue("--clips", out string? selection)) throw new ArgumentException("--animations requires --clips all or a comma-separated filename list. Select player clips explicitly.");
                string[] paths = Directory.GetFiles(animationDirectory, "*.kf");
                if (selection != "all") {
                    string[] requested = selection.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    paths = requested.Select(name => paths.SingleOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new FileNotFoundException($"Animation {name} not found in {animationDirectory}.")).ToArray();
                }
                if (paths.Length == 0) throw new FileNotFoundException("No selected KF files.");
                foreach (string path in paths.Order()) {
                    if (ReadClip(path) is { } parsed) clips.Add(parsed);
                }
            } else if (options.ContainsKey("--clips")) throw new ArgumentException("--clips requires --animations.");
            if (animationErrors.Count > 0) throw new InvalidDataException($"{animationErrors.Count} animation(s) rejected; no glTF written:\n{string.Join("\n", animationErrors)}");
            if (options.ContainsKey("--clips") && clips.Count == 0) throw new InvalidDataException("No animation clips selected.");
            if (clips.Select(clip => clip.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != clips.Count) throw new InvalidDataException("Duplicate clip names.");
            object report = new GltfWriter(document, options.GetValueOrDefault("--textures")).Write(output, clips, overwrite);
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        } catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or NotSupportedException or OverflowException) {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
}
