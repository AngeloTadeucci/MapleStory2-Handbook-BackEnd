using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NifToGltf.Native;

internal sealed record BatchModel(string Input) {
    public string? Id { get; init; }
    public string? Output { get; init; }
    public string? Kfm { get; init; }
    public string? Animations { get; init; }
    public string[]? Clips { get; init; }
    public string? Skeleton { get; init; }
    public string? Attach { get; init; }
    public string? Slot { get; init; }
    public string? BodyVariant { get; init; }
    public string? ExcludeEffect { get; init; }
    public string? ItemModel { get; init; }
    public string? ItemId { get; init; }
    public string? AlternateOf { get; init; }
    public string? Hand { get; init; }
    public bool Drawn { get; init; }
}
internal sealed record BatchPlan(int Version, BatchModel[] Models);

internal static class NativeBatch {
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static string Resolve(string root, string relative) {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) throw new ArgumentException($"Expected a relative asset path: {relative}");
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new ArgumentException($"Asset path escapes input/output root: {relative}");
        return path;
    }
    public static int Run(string input, string output, string? textures, string? manifest = null) {
        string root = Path.GetFullPath(input), destination = Path.GetFullPath(output);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        string sourcePrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (destination.Equals(root, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Batch output must be outside the input tree.");
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) throw new IOException($"Batch output must be empty: {destination}");
        BatchPlan plan = manifest is null ? new(1, Directory.GetFiles(root, "*.nif", SearchOption.AllDirectories).Order()
            .Select(file => new BatchModel(Path.GetRelativePath(root, file).Replace('\\', '/'))).ToArray()) :
            JsonSerializer.Deserialize<BatchPlan>(File.ReadAllText(manifest), Options) ?? throw new InvalidDataException("Empty batch manifest.");
        if (plan.Version != 1 || plan.Models is null || plan.Models.Length == 0) throw new InvalidDataException("Expected a version 1 manifest with models.");
        HashSet<string> targets = new(StringComparer.OrdinalIgnoreCase), ids = new(StringComparer.OrdinalIgnoreCase);
        foreach (BatchModel model in plan.Models) {
            Resolve(root, model.Input);
            string target = Resolve(destination, model.Output ?? Path.ChangeExtension(model.Input, ".gltf"));
            if (!target.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase) || !targets.Add(target)) throw new InvalidDataException("Duplicate or invalid batch output.");
            if (model.Id is not null && !ids.Add(model.Id)) throw new InvalidDataException($"Duplicate manifest id {model.Id}.");
            if (model.Attach is not null && model.Skeleton is null) throw new InvalidDataException("Attachment requires a skeleton.");
        }
        Directory.CreateDirectory(destination);
        List<object> converted = [], failed = [], missing = [], excluded = [];
        JsonArray assets = [];
        TextureCatalog catalog = new(textures);
        foreach (BatchModel model in plan.Models) {
            string relative = model.Input.Replace('\\', '/');
            string file = Resolve(root, relative);
            string targetRelative = (model.Output ?? Path.ChangeExtension(relative, ".gltf")).Replace('\\', '/');
            try {
                if (!File.Exists(file)) throw new FileNotFoundException($"Source {relative} is missing.");
                if (model.ExcludeEffect is { } reason) {
                    if (string.IsNullOrWhiteSpace(reason)) throw new InvalidDataException("Effect exclusion requires evidence/reason.");
                    excluded.Add(new { input = relative, reason });
                    continue;
                }
                NifDocument document = NifDocument.Load(file);
                document.OmitParticles();
                if (!document.Nodes.Values.Any(node => node.Mesh is not null) && document.Omitted.Any(entry => entry.Type is "NiPSParticleSystem" or "NiPSMeshParticleSystem")) {
                    excluded.Add(new { input = relative, reason = "Particle systems without ordinary NiMesh geometry.", omitted = document.Omitted });
                    continue;
                }
                string attachmentSource = file;
                KfmDocument? kfm = model.Kfm is null ? null : KfmDocument.Read(Resolve(root, model.Kfm), root);
                if (kfm is not null) {
                    if (!Path.GetFullPath(kfm.Model).Equals(file, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("KFM model does not match manifest input.");
                    // Distinct XML URNs can have KFM files that reference one NIF.
                    // Select the attachment by its KFM identity, not shared geometry.
                    attachmentSource = Resolve(root, model.Kfm!);
                }
                if (model.AlternateOf is { } primary) {
                    string primaryFile = Resolve(root, primary);
                    string stem = Path.GetFileNameWithoutExtension(file), primaryStem = Path.GetFileNameWithoutExtension(primaryFile);
                    if (model.Slot != "HR" || model.ItemModel is null || !File.Exists(primaryFile) ||
                        !primaryStem.EndsWith("_a", StringComparison.OrdinalIgnoreCase) ||
                        !(stem.EndsWith("_c", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("_d", StringComparison.OrdinalIgnoreCase)) ||
                        !stem[..^1].Equals(primaryStem[..^1], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Alternate hair form must be the same source family's C/D form.");
                    attachmentSource = primaryFile;
                }
                ItemModelAttachment? attachment = model.ItemModel is null ? null : ItemModelAttachment.Read(Resolve(root, model.ItemModel),
                    model.ItemId ?? throw new InvalidDataException("ItemModel requires itemId."), attachmentSource, model.BodyVariant ?? "", model.Slot);
                if (attachment is not null && model.Skeleton is null) throw new InvalidDataException("ItemModel attachment requires a skeleton.");
                if (attachment is not null && model.Attach is not null) throw new InvalidDataException("Use itemmodel attachment or an explicit attach, not both.");
                if (model.Hand is { } hand) {
                    if (hand is not ("RH" or "LH") || attachment?.Slot != "OH") throw new InvalidDataException("Hand selection requires a dual-wieldable OH itemmodel.");
                    // Client 0x141658150 selects these helpers for drawn weapons.
                    // The XML target and its dummy describe the stowed attachment.
                    attachment = attachment with { Slot = hand, TargetNode = hand == "RH" ? "Weapon_Hand_R_Point" : "Weapon_Hand_L_Point", Translation = null, Rotation = null };
                }
                if (model.Drawn) {
                    if (attachment?.Slot is not ("RH" or "LH") || string.IsNullOrWhiteSpace(attachment.AttachNode))
                        throw new InvalidDataException("Drawn placement requires the item's explicit hand attachnode.");
                    attachment = attachment with { TargetNode = attachment.AttachNode, Translation = null, Rotation = null };
                }
                if (model.Skeleton is { } skeleton) SkeletonGraft.Apply(document, NifDocument.Load(Resolve(root, skeleton)), attachment?.TargetNode ?? model.Attach, attachment?.SelfNode, attachment?.Replace ?? false, attachment?.DummyTransform);
                List<AnimationClip> clips = ClipSelection.Read(file, kfm, model.Animations is null ? null : Resolve(root, model.Animations), model.Clips is null ? null : string.Join(',', model.Clips));
                AnimationClip? embedded = EmbeddedAnimation.Read(document);
                if (embedded is not null) {
                    if (clips.Count > 0) throw new NotSupportedException("Both embedded controllers and external clips require verified playback selection.");
                    clips.Add(embedded);
                }
                JsonNode report = JsonSerializer.SerializeToNode(new GltfWriter(document, textures, catalog).Write(Resolve(destination, targetRelative), clips), Options)!;
                report["source"] = relative; report["output"] = targetRelative;
                converted.Add(new { input = relative, report });
                assets.Add(JsonSerializer.SerializeToNode(new { id = model.Id ?? Path.GetFileNameWithoutExtension(relative), input = relative,
                    uri = targetRelative, clips = clips.Select(clip => clip.Name), skeleton = model.Skeleton, attach = model.Attach,
                    slot = attachment?.Slot ?? model.Slot, bodyVariant = model.BodyVariant, itemId = model.ItemId,
                    attachmentSource = Path.GetRelativePath(root, attachmentSource).Replace('\\', '/'), attachment,
                    unboundAnimationTargets = report["unboundAnimationTargets"], omitted = document.Omitted }, Options));
            } catch (IOException e) when (e is FileNotFoundException or DirectoryNotFoundException) {
                missing.Add(new { input = relative, error = PortableError(e.Message, root, destination) });
            } catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException) {
                failed.Add(new { input = relative, error = PortableError(e.Message, root, destination) });
            } catch (Exception e) {
                // A converter defect is a per-input failure, never a reason to
                // lose every completed entry and checkpoint in a long batch.
                failed.Add(new { input = relative, error = $"Internal converter failure ({e.GetType().Name}): {PortableError(e.Message, root, destination)}", detail = e.StackTrace });
            }
            if ((converted.Count + failed.Count + missing.Count + excluded.Count) % 100 == 0) Console.WriteLine($"{converted.Count} converted, {excluded.Count} excluded, {missing.Count} missing, {failed.Count} failed");
        }
        object summary = new { version = 1, mode = manifest is null ? "static" : "selected", input = ".", total = plan.Models.Length,
            convertedCount = converted.Count, excludedCount = excluded.Count, missingCount = missing.Count, failedCount = failed.Count, converted, excluded, missing, failed };
        File.WriteAllText(Path.Combine(destination, "batch-report.json"), JsonSerializer.Serialize(summary, Options));
        File.WriteAllText(Path.Combine(destination, "native-manifest.json"), new JsonObject { ["version"] = 1, ["coordinateSystem"] = "gltf-y-up-meters", ["assets"] = assets }.ToJsonString(Options));
        Console.WriteLine($"Batch: {converted.Count} converted; {excluded.Count} effect-excluded; {missing.Count} missing; {failed.Count} failed.");
        return failed.Count + missing.Count == 0 ? 0 : 1;
    }
    private static string PortableError(string error, string root, string output) => error.Replace(root + Path.DirectorySeparatorChar, "").Replace(output + Path.DirectorySeparatorChar, "").Replace('\\', '/');
}
