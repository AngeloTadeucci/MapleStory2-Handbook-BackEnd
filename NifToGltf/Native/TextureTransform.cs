using System.Numerics;

namespace NifToGltf.Native;

internal sealed record TextureTransform(int Source, int Target, Matrix3x2 Matrix) {
    // nifxml TransformMethod and NiTextureTransform document column-vector products.
    // Reverse multiplication order for System.Numerics row vectors.
    public static Matrix3x2 Create(Vector2 translation, Vector2 scale, float rotation, uint method, Vector2 center) {
        Matrix3x2 t = Matrix3x2.CreateTranslation(translation), s = Matrix3x2.CreateScale(scale),
            r = Matrix3x2.CreateRotation(rotation), c = Matrix3x2.CreateTranslation(center), b = Matrix3x2.CreateTranslation(-center);
        return method switch {
            0 => s * t * b * r * c,
            1 => b * t * r * s * c,
            2 => s * t * new Matrix3x2(1, 0, 0, -1, 0, 1) * b * r * c,
            _ => throw new NotSupportedException($"Texture transform method {method}.")
        };
    }
    public void Apply(Dictionary<string, MeshAttribute> attributes) {
        if (!attributes.TryGetValue($"TEXCOORD_{Source}", out MeshAttribute? uv) || uv.Width != 2) throw new InvalidDataException($"Texture transform requires UV set {Source}.");
        double[] values = new double[uv.Values.Length];
        for (int i = 0; i < values.Length; i += 2) {
            Vector2 point = Vector2.Transform(new Vector2((float) uv.Values[i], (float) uv.Values[i + 1]), Matrix);
            values[i] = point.X; values[i + 1] = point.Y;
        }
        attributes[$"TEXCOORD_{Target}"] = new(2, values);
    }
}
