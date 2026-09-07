using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 1) throw new ArgumentException("Usage: NifToGltf.Provenance converter.dll");
string binary = Path.GetFullPath(args[0]), pdb = Path.ChangeExtension(binary, ".pdb");
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
using var stream = File.OpenRead(pdb);
using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
var reader = provider.GetMetadataReader();
var documents = reader.Documents.Select(handle => {
    var document = reader.GetDocument(handle);
    return new { path = reader.GetString(document.Name), algorithm = reader.GetGuid(document.HashAlgorithm), checksum = Convert.ToHexString(reader.GetBlobBytes(document.Hash)).ToLowerInvariant() };
}).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new { binary, binaryHash = Hash(binary), pdb, pdbHash = Hash(pdb), documents }));
