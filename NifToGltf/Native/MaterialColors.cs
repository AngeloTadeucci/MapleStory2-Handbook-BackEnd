using System.Numerics;

namespace NifToGltf.Native;

internal static class MaterialColors {
    // Client MS2Character[Skin/Hair]Material ColorOverride shader fragment.
    public static Vector3 Override(Vector3 diffuse, Vector4 control, Vector3 c0, Vector3 c1, Vector3 c2) =>
        Vector3.Lerp(diffuse, c0 * control.X + c1 * control.Y + c2 * (1 - control.X), control.W);

    public static Vector4 Sample(TexturePixels image, double u, double v, bool linear = true, bool wrapS = true, bool wrapT = true) {
        int Index(int value, int size, bool wrap) => wrap ? (value % size + size) % size : Math.Clamp(value, 0, size - 1);
        Vector4 At(int x, int y) {
            int i = (Index(y, image.Height, wrapT) * image.Width + Index(x, image.Width, wrapS)) * 4;
            return new Vector4(image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]) / 255;
        }
        if (!linear) return At((int) Math.Floor(u * image.Width), (int) Math.Floor(v * image.Height));
        double px = u * image.Width - 0.5, py = v * image.Height - 0.5;
        int x = (int) Math.Floor(px), y = (int) Math.Floor(py);
        return Vector4.Lerp(Vector4.Lerp(At(x, y), At(x + 1, y), (float) (px - x)),
            Vector4.Lerp(At(x, y + 1), At(x + 1, y + 1), (float) (px - x)), (float) (py - y));
    }
    public static TexturePixels Bake(TexturePixels diffuse, TexturePixels mask, Vector3[] colors,
        bool maskLinear = true, bool maskWrapS = true, bool maskWrapT = true,
        bool diffuseLinear = true, bool diffuseWrapS = true, bool diffuseWrapT = true) {
        if (colors.Length != 3) throw new InvalidDataException("Color override requires three colors.");
        // Skin diffuse maps can be tiny constant-color images. Retain the control
        // map's spatial detail instead of reducing it to the diffuse resolution.
        int width = Math.Max(diffuse.Width, mask.Width), height = Math.Max(diffuse.Height, mask.Height);
        byte[] pixels = new byte[checked(width * height * 4)];
        for (int i = 0; i < pixels.Length; i += 4) {
            double u = (i / 4 % width + 0.5) / width, v = (i / 4 / width + 0.5) / height;
            Vector4 baseColor = Sample(diffuse, u, v, diffuseLinear, diffuseWrapS, diffuseWrapT);
            Vector3 d = new(baseColor.X, baseColor.Y, baseColor.Z);
            Vector4 c = Sample(mask, u, v, maskLinear, maskWrapS, maskWrapT);
            Vector3 result = Vector3.Clamp(Override(d, c, colors[0], colors[1], colors[2]), Vector3.Zero, Vector3.One);
            pixels[i] = (byte) MathF.Round(result.X * 255); pixels[i + 1] = (byte) MathF.Round(result.Y * 255); pixels[i + 2] = (byte) MathF.Round(result.Z * 255);
            pixels[i + 3] = (byte) Math.Clamp(MathF.Round(baseColor.W * 255), 0, 255);
        }
        return new(width, height, pixels);
    }
}
