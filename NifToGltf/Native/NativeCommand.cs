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
                      --native --texture-batch --input dds-directory --output empty-directory

                    --overwrite explicitly replaces one output. Batch always requires an empty destination.
                    CP attachments infer Bip01 Head. Other rigid slots require --attach.
                    Batch accepts --manifest plan.json for per-model clips, skeletons and exclusions.
                    Batch writes batch-report.json and portable native-manifest.json.
                    Nonlinear animation curves are sampled with adaptive error checks.
                    Unsupported data fails explicitly. Noesis remains the default mode without --native.
                    """);
                return 0;
            }
            Dictionary<string, string> options = [];
            bool batch = false, overwrite = false, textureBatch = false;
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--native") continue;
                if (args[i] == "--batch") { batch = true; continue; }
                if (args[i] == "--texture-batch") { textureBatch = true; continue; }
                if (args[i] == "--overwrite") { overwrite = true; continue; }
                if (args[i] is not ("--input" or "--output" or "--textures" or "--skeleton" or "--attach" or "--animations" or "--clips" or "--kfm" or "--manifest") || i + 1 >= args.Length) {
                    throw new ArgumentException("Usage: --native --input model.nif --output model.gltf [--textures directory] [--skeleton body.nif] [--attach bone]");
                }
                options.Add(args[i], args[++i]);
            }
            KfmDocument? kfm = options.TryGetValue("--kfm", out string? kfmPath) ? KfmDocument.Read(kfmPath) : null;
            if (kfm is not null && !options.ContainsKey("--input")) options["--input"] = kfm.Model;
            if (!options.TryGetValue("--input", out string? input) || !options.TryGetValue("--output", out string? output)) {
                throw new ArgumentException("--native requires --input and --output.");
            }
            if (textureBatch) return TextureBatch.Run(input, output);
            if (batch) {
                if (overwrite) throw new ArgumentException("Batch output must be an empty directory.");
                if (options.Keys.Any(key => key is not ("--input" or "--output" or "--textures" or "--manifest"))) {
                    throw new ArgumentException("Supply per-model clip selections and skeletons in --manifest plan.json.");
                }
                return NativeBatch.Run(input, output, options.GetValueOrDefault("--textures"), options.GetValueOrDefault("--manifest"));
            }
            if (!string.Equals(Path.GetExtension(output), ".gltf", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("Native output must have a .gltf extension.");
            }
            if (File.Exists(output) && !overwrite) throw new IOException($"Output already exists: {output}. Choose a new path or use --overwrite.");
            if (options.ContainsKey("--manifest")) throw new ArgumentException("--manifest requires --batch.");
            NifDocument document = NifDocument.Load(input);
            document.OmitParticles();
            if (options.TryGetValue("--skeleton", out string? skeleton)) {
                SkeletonGraft.Apply(document, NifDocument.Load(skeleton), options.GetValueOrDefault("--attach"));
            } else if (options.ContainsKey("--attach")) throw new ArgumentException("--attach requires --skeleton.");
            List<AnimationClip> clips = ClipSelection.Read(input, kfm, options.GetValueOrDefault("--animations"), options.GetValueOrDefault("--clips"));
            object report = new GltfWriter(document, options.GetValueOrDefault("--textures")).Write(output, clips, overwrite);
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        } catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or NotSupportedException or OverflowException or JsonException) {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
}
