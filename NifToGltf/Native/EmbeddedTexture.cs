namespace NifToGltf.Native;

internal static class EmbeddedTexture {
    public static byte[] ToPng(NifDocument document, int block) => Decode(document, block).ToPng();
    public static TexturePixels Decode(NifDocument document, int block) {
        NifReader r = document.Reader(block, "NiPixelData");
        uint format = r.U32();
        int bits = r.Byte();
        r.U32(); r.U32(); r.Byte();
        if (r.U32() != 0) throw new NotSupportedException("Tiled embedded texture.");
        r.Bool(); // Color space is assigned by material usage in glTF.
        uint[] masks = new uint[4];
        int shift = 0;
        for (int i = 0; i < 4; i++) {
            uint channel = r.U32(), convention = r.U32();
            int width = r.Byte();
            bool signed = r.Bool();
            if (format is 0 or 1) {
                if (width > 32 || shift + width > 32 || signed || (width > 0 && convention != 0)) throw new NotSupportedException("Embedded RGB channel representation.");
                if (channel < 4 && width > 0) masks[channel] = (uint) (((1UL << width) - 1) << shift);
                shift += width;
            }
        }
        int palette = r.I32();
        int mips = r.Count(12);
        int bytesPerPixel = checked((int) r.U32());
        if (mips == 0 || palette != -1) throw new NotSupportedException("Missing mipmaps or palettized embedded texture.");
        (int Width, int Height, int Offset)[] levels = Enumerable.Range(0, mips)
            .Select(_ => (checked((int) r.U32()), checked((int) r.U32()), checked((int) r.U32()))).ToArray();
        int length = checked((int) r.U32());
        if (r.U32() != 1) throw new NotSupportedException("Embedded cube texture.");
        ReadOnlyMemory<byte> data = r.Take(length);
        r.Finish();
        var first = levels[0];
        if (first.Offset < 0 || first.Offset >= length) throw new InvalidDataException("Embedded mip offset outside pixels.");
        ReadOnlySpan<byte> mip = data.Span[first.Offset..];
        byte[] rgba = format switch {
            4 or 5 or 6 => DdsTexture.DecodeCompressed(mip, first.Width, first.Height, format == 4 ? "DXT1" : format == 5 ? "DXT3" : "DXT5"),
            0 or 1 when bytesPerPixel * 8 == bits => DdsTexture.DecodeRgb(mip, first.Width, first.Height, bits, checked(first.Width * bytesPerPixel), masks),
            _ => throw new NotSupportedException($"Embedded pixel format {format}, {bits} bits.")
        };
        return new TexturePixels(first.Width, first.Height, rgba);
    }
}
