using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace NifToGltf.Native;

// Base mip only. Unsupported DDS storage fails explicitly instead of emitting a bad texture.
internal static class DdsTexture {
    public static byte[] ToPng(byte[] dds) {
        if (dds.Length < 128 || Encoding.ASCII.GetString(dds, 0, 4) != "DDS ") {
            throw new InvalidDataException("Invalid DDS header.");
        }
        int height = checked((int) BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(12)));
        int width = checked((int) BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(16)));
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384) throw new InvalidDataException("Invalid DDS dimensions.");
        string format = Encoding.ASCII.GetString(dds, 84, 4);
        if (format is not ("DXT1" or "DXT3" or "DXT5")) throw new NotSupportedException($"DDS format {format}.");
        byte[] pixels = new byte[checked(width * height * 4)];
        int offset = 128;
        int size = format == "DXT1" ? 8 : 16;
        for (int y = 0; y < height; y += 4) {
            for (int x = 0; x < width; x += 4) {
                if (offset + size > dds.Length) throw new InvalidDataException("Truncated DDS mip.");
                DecodeBlock(dds.AsSpan(offset, size), format, pixels, width, height, x, y);
                offset += size;
            }
        }
        return Png(width, height, pixels);
    }
    private static void DecodeBlock(ReadOnlySpan<byte> block, string format, byte[] pixels, int width, int height, int x, int y) {
        int colorOffset = format == "DXT1" ? 0 : 8;
        ushort c0 = BinaryPrimitives.ReadUInt16LittleEndian(block[colorOffset..]);
        ushort c1 = BinaryPrimitives.ReadUInt16LittleEndian(block[(colorOffset + 2)..]);
        byte[][] colors = [Rgb565(c0), Rgb565(c1), new byte[4], new byte[4]];
        for (int c = 0; c < 3; c++) {
            if (c0 > c1 || format != "DXT1") {
                colors[2][c] = (byte) ((2 * colors[0][c] + colors[1][c]) / 3);
                colors[3][c] = (byte) ((colors[0][c] + 2 * colors[1][c]) / 3);
            } else {
                colors[2][c] = (byte) ((colors[0][c] + colors[1][c]) / 2);
            }
        }
        colors[2][3] = 255;
        colors[3][3] = (byte) (c0 > c1 || format != "DXT1" ? 255 : 0);
        uint selectors = BinaryPrimitives.ReadUInt32LittleEndian(block[(colorOffset + 4)..]);
        byte[] alpha = new byte[8];
        ulong alphaBits = 0;
        if (format == "DXT5") {
            alpha[0] = block[0]; alpha[1] = block[1];
            for (int i = 2; i < (alpha[0] > alpha[1] ? 8 : 6); i++) {
                int divisor = alpha[0] > alpha[1] ? 7 : 5;
                alpha[i] = (byte) (((divisor + 1 - i) * alpha[0] + (i - 1) * alpha[1]) / divisor);
            }
            if (alpha[0] <= alpha[1]) { alpha[6] = 0; alpha[7] = 255; }
            for (int i = 0; i < 6; i++) alphaBits |= (ulong) block[i + 2] << (8 * i);
        } else if (format == "DXT3") {
            alphaBits = BinaryPrimitives.ReadUInt64LittleEndian(block);
        }
        for (int i = 0; i < 16; i++) {
            int px = x + i % 4, py = y + i / 4;
            if (px >= width || py >= height) continue;
            byte[] color = colors[(selectors >> (2 * i)) & 3];
            int target = (py * width + px) * 4;
            color.CopyTo(pixels, target);
            if (format == "DXT3") pixels[target + 3] = (byte) (((alphaBits >> (4 * i)) & 15) * 17);
            if (format == "DXT5") pixels[target + 3] = alpha[(alphaBits >> (3 * i)) & 7];
        }
    }
    private static byte[] Rgb565(ushort value) {
        int r = value >> 11, g = (value >> 5) & 63, b = value & 31;
        return [(byte) ((r << 3) | (r >> 2)), (byte) ((g << 2) | (g >> 4)), (byte) ((b << 3) | (b >> 2)), 255];
    }
    private static byte[] Png(int width, int height, byte[] pixels) {
        using MemoryStream output = new();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6;
        Chunk(output, "IHDR", header);
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, true)) {
            for (int y = 0; y < height; y++) {
                zlib.WriteByte(0);
                zlib.Write(pixels, y * width * 4, width * 4);
            }
        }
        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }
    private static void Chunk(Stream output, string name, byte[] data) {
        byte[] integer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(integer, data.Length);
        output.Write(integer);
        byte[] type = Encoding.ASCII.GetBytes(name);
        output.Write(type); output.Write(data);
        uint crc = uint.MaxValue;
        foreach (byte value in type.Concat(data)) {
            crc ^= value;
            for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(integer, ~crc);
        output.Write(integer);
    }
}
