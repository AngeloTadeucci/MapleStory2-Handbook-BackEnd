using System.Text;
using System.Text.Json;

namespace FsbExtractor;

public enum Fsb5Codec {
    None = 0,
    Pcm8 = 1,
    Pcm16 = 2,
    Pcm24 = 3,
    Pcm32 = 4,
    PcmFloat = 5,
    GcAdpcm = 6,
    ImaAdpcm = 7,
    Vag = 8,
    Hevag = 9,
    Xma = 10,
    Mpeg = 11,
    Celt = 12,
    At9 = 13,
    Xwma = 14,
    Vorbis = 15,
}

public record Fsb5Sample(
    string Name,
    int Frequency,
    int Channels,
    long DataOffset,
    long DataLength,
    int NumSamples,
    int LoopStart,
    int LoopEnd
) {
    public double DurationSeconds => Frequency > 0 ? (double)NumSamples / Frequency : 0;
}

public class Fsb5File {
    private static readonly int[] FrequencyTable = [4000, 8000, 11000, 11025, 16000, 22050, 24000, 32000, 44100, 48000];
    private static readonly int[] ChannelTable = [1, 2, 6, 8];

    public int Version { get; private set; }
    public int SampleCount { get; private set; }
    public Fsb5Codec Codec { get; private set; }
    public List<Fsb5Sample> Samples { get; } = [];

    private long _dataOffset;
    private string _filePath = "";

    public static Fsb5File Parse(string filePath) {
        var fsb = new Fsb5File { _filePath = filePath };
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        // Header (60 bytes)
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic != "FSB5") throw new InvalidDataException($"Not an FSB5 file: {filePath}");

        fsb.Version = reader.ReadInt32();
        fsb.SampleCount = reader.ReadInt32();
        int sampleHeadersSize = reader.ReadInt32();
        int nameTableSize = reader.ReadInt32();
        long dataSize = reader.ReadUInt32();
        fsb.Codec = (Fsb5Codec)reader.ReadInt32();

        // Skip extra bytes (zero/hash) - 32 bytes
        reader.ReadBytes(32);

        long headerEnd = stream.Position; // should be 60
        long nameTableOffset = headerEnd + sampleHeadersSize;
        fsb._dataOffset = nameTableOffset + nameTableSize;

        // Parse sample headers
        var rawSamples = new List<(long dataOffsetRaw, int numSamples, int frequency, int channels, int loopStart, int loopEnd)>();

        for (int i = 0; i < fsb.SampleCount; i++) {
            ulong raw = reader.ReadUInt64();

            bool hasChunks = (raw & 1) != 0;
            int freqIndex = (int)((raw >> 1) & 0xF);
            int channels = ChannelTable[(int)((raw >> 5) & 0x3)];
            long dataOffsetRaw = (long)((raw >> 7) & 0x7FFFFFF) << 5; // 27 bits, units of 32 bytes
            int numSamples = (int)((raw >> 34) & 0x3FFFFFFF); // 30 bits
            int frequency = freqIndex < FrequencyTable.Length ? FrequencyTable[freqIndex] : 44100;

            int loopStart = 0;
            int loopEnd = 0;

            // Parse optional chunks
            while (hasChunks) {
                uint chunkRaw = reader.ReadUInt32();
                hasChunks = (chunkRaw & 1) != 0;
                int chunkSize = (int)((chunkRaw >> 1) & 0xFFFFFF);
                int chunkType = (int)((chunkRaw >> 25) & 0x7F);

                long chunkDataStart = stream.Position;

                switch (chunkType) {
                    case 1: // Channels override
                        channels = reader.ReadByte();
                        break;
                    case 2: // Frequency override
                        frequency = reader.ReadInt32();
                        break;
                    case 3: // Loop
                        loopStart = reader.ReadInt32();
                        loopEnd = reader.ReadInt32();
                        break;
                }

                stream.Position = chunkDataStart + chunkSize;
            }

            rawSamples.Add((dataOffsetRaw, numSamples, frequency, channels, loopStart, loopEnd));
        }

        // Parse name table
        var names = new string[fsb.SampleCount];
        if (nameTableSize > 0) {
            stream.Position = nameTableOffset;
            var nameOffsets = new uint[fsb.SampleCount];
            for (int i = 0; i < fsb.SampleCount; i++) {
                nameOffsets[i] = reader.ReadUInt32();
            }

            for (int i = 0; i < fsb.SampleCount; i++) {
                stream.Position = nameTableOffset + nameOffsets[i];
                names[i] = ReadNullTerminatedString(reader);
            }
        } else {
            for (int i = 0; i < fsb.SampleCount; i++) {
                names[i] = $"sample_{i:D4}";
            }
        }

        // Calculate data lengths from consecutive offsets
        for (int i = 0; i < fsb.SampleCount; i++) {
            var (dataOff, numSamp, freq, ch, loopStart, loopEnd) = rawSamples[i];
            long nextOffset = i + 1 < fsb.SampleCount ? rawSamples[i + 1].dataOffsetRaw : dataSize;
            long dataLength = nextOffset - dataOff;

            fsb.Samples.Add(new Fsb5Sample(
                Name: names[i],
                Frequency: freq,
                Channels: ch,
                DataOffset: fsb._dataOffset + dataOff,
                DataLength: dataLength,
                NumSamples: numSamp,
                LoopStart: loopStart,
                LoopEnd: loopEnd
            ));
        }

        return fsb;
    }

    public void ExtractAll(string outputDir) {
        Directory.CreateDirectory(outputDir);

        string extension = Codec switch {
            Fsb5Codec.Mpeg => ".mp3",
            Fsb5Codec.Vorbis => ".ogg",
            Fsb5Codec.Pcm16 => ".wav",
            _ => ".bin",
        };

        using var stream = File.OpenRead(_filePath);
        var buffer = new byte[64 * 1024]; // 64KB read buffer

        Console.WriteLine($"Extracting {Samples.Count} samples from {Path.GetFileName(_filePath)} (codec: {Codec})...");

        for (int i = 0; i < Samples.Count; i++) {
            var sample = Samples[i];
            string safeName = SanitizeFileName(sample.Name);
            string outPath = Path.Combine(outputDir, safeName + extension);

            stream.Position = sample.DataOffset;

            using var outFile = File.Create(outPath);
            long remaining = sample.DataLength;
            while (remaining > 0) {
                int toRead = (int)Math.Min(remaining, buffer.Length);
                int read = stream.Read(buffer, 0, toRead);
                if (read == 0) break;
                outFile.Write(buffer, 0, read);
                remaining -= read;
            }

            if ((i + 1) % 20 == 0 || i + 1 == Samples.Count) {
                Console.WriteLine($"  [{i + 1}/{Samples.Count}] {safeName}{extension} ({sample.DurationSeconds:F1}s)");
            }
        }
    }

    public void WriteMetadata(string outputPath) {
        var metadata = Samples.Select((s, i) => new {
            index = i,
            name = s.Name,
            frequency = s.Frequency,
            channels = s.Channels,
            durationSeconds = Math.Round(s.DurationSeconds, 2),
            loopStart = s.LoopStart,
            loopEnd = s.LoopEnd,
        }).ToList();

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(metadata, options);
        File.WriteAllText(outputPath, json);
    }

    private static string ReadNullTerminatedString(BinaryReader reader) {
        var bytes = new List<byte>();
        byte b;
        while ((b = reader.ReadByte()) != 0) {
            bytes.Add(b);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public static string SanitizeFileName(string name) {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (char c in name) {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }
}
