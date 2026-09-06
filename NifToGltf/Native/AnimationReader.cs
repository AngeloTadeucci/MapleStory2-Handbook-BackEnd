using System.Numerics;

namespace NifToGltf.Native;

internal sealed record AnimationTrack(string Node, string Path, double[] Times, double[] Values, int Width, string Interpolation);
internal sealed record AnimationClip(string Name, AnimationTrack[] Tracks);
internal sealed record AnimationCurve(Func<double, double[]> Evaluate, double[] KeyTimes, bool Sample = false, bool Step = false);
internal sealed record AnimationKey(double Time, double[] Value, double[] Forward, double[] Backward, double[] Tbc);

internal static class AnimationReader {
    public static AnimationClip Read(string path, int framesPerSecond = 60, string? sequenceName = null) {
        NifDocument document = NifDocument.Load(path);
        int[] sequenceIds = document.Roots.Where(id => id >= 0 && document.Blocks[id].Type == "NiSequenceData").ToArray();
        if (sequenceIds.Length > 1 && sequenceName is not null) {
            sequenceIds = sequenceIds.Where(id => string.Equals(document.Name(document.Reader(id)), sequenceName, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        if (sequenceIds.Length != 1) throw new NotSupportedException($"{path}: expected one NiSequenceData{(sequenceName is null ? "" : $" matching {sequenceName}")}, found {sequenceIds.Length}.");
        NifReader sequence = document.Reader(sequenceIds[0]);
        string name = document.Name(sequence);
        int[] evaluators = sequence.Refs();
        sequence.I32();
        double duration = sequence.Float();
        sequence.U32();
        double frequency = sequence.Float();
        document.Name(sequence);
        sequence.U32();
        sequence.Finish();
        if (duration <= 0 || duration > 3600 || frequency <= 0) throw new InvalidDataException("Invalid animation duration/frequency.");
        List<AnimationTrack> tracks = [];
        foreach (int block in evaluators) {
            try { tracks.AddRange(ReadEvaluator(document, block, duration, frequency, framesPerSecond)); }
            catch (Exception e) when (e is InvalidDataException or NotSupportedException) {
                throw new InvalidDataException($"{path}: evaluator {block}: {e.Message}", e);
            }
        }
        if (tracks.Count == 0) throw new InvalidDataException($"{path}: no transform animation tracks.");
        return new AnimationClip(string.IsNullOrWhiteSpace(name) ? System.IO.Path.GetFileNameWithoutExtension(path) : name, tracks.ToArray());
    }

    private static IEnumerable<AnimationTrack> ReadEvaluator(NifDocument document, int block, double duration, double frequency, int fps) {
        NifReader r = document.Reader(block);
        string type = document.Blocks[block].Type;
        string node = document.Name(r), property = document.Name(r), controller = document.Name(r);
        document.Name(r); document.Name(r);
        byte[] channels = r.Take(4).ToArray();
        if (property.Length > 0 || controller != "NiTransformController") throw new NotSupportedException($"Non-transform evaluator {node}/{property}/{controller}.");
        AnimationCurve?[] curves = new AnimationCurve?[3];
        double[][] pose;
        if (type is "NiTransformEvaluator" or "NiConstTransformEvaluator") {
            pose = Pose(r);
            if (type == "NiTransformEvaluator") {
                int data = r.I32();
                if (data >= 0) curves = KeyData(document.Reader(data, "NiTransformData"));
            }
        } else if (type is "NiBSplineCompTransformEvaluator" or "NiBSplineTransformEvaluator") {
            double start = r.Float(), end = r.Float();
            int dataId = r.I32(), basisId = r.I32();
            pose = Pose(r);
            uint[] handles = [r.U32(), r.U32(), r.U32()];
            double[] offsets = new double[3], ranges = [1, 1, 1];
            bool compact = type == "NiBSplineCompTransformEvaluator";
            if (compact) {
                for (int i = 0; i < 3; i++) { offsets[i] = r.Float(); ranges[i] = r.Float(); }
            }
            if (handles.Any(handle => handle is not (65535 or uint.MaxValue))) {
                NifReader basis = document.Reader(basisId, "NiBSplineBasisData");
                int controlCount = checked((int) basis.U32()); basis.Finish();
                NifReader data = document.Reader(dataId, "NiBSplineData");
                float[] floats = Enumerable.Range(0, data.Count(4)).Select(_ => data.Float()).ToArray();
                short[] shorts = Enumerable.Range(0, data.Count(2)).Select(_ => unchecked((short) data.U16())).ToArray();
                data.Finish();
                if (end <= start || controlCount < 4) throw new InvalidDataException("Invalid B-spline basis or interval.");
                int[] widths = [3, 4, 1];
                for (int i = 0; i < 3; i++) {
                    if (handles[i] is 65535 or uint.MaxValue) continue;
                    int width = widths[i], handle = checked((int) handles[i]);
                    if ((long) handle + controlCount * width > (compact ? shorts.Length : floats.Length)) throw new InvalidDataException("B-spline handle outside control data.");
                    double[] values = Enumerable.Range(0, controlCount * width).Select(c => compact
                        ? shorts[handle + c] / 32767.0 * ranges[i] + offsets[i] : floats[handle + c]).ToArray();
                    curves[i] = new AnimationCurve(time => BSpline(values, width, Math.Clamp((time - start) / (end - start), 0, 1)), [start, end], true);
                }
            }
        } else throw new NotSupportedException($"Evaluator type {type}.");
        r.Finish();
        string[] paths = ["translation", "rotation", "scale"];
        for (int channel = 0; channel < 3; channel++) {
            if ((channels[channel] & 63) == 0) continue;
            double[] constant = pose[channel];
            AnimationCurve curve = curves[channel] ?? new AnimationCurve(_ => constant, []);
            if (curves[channel] is null && constant.Any(v => Math.Abs(v) > 1e30)) throw new InvalidDataException("Missing data for an active transform channel.");
            IEnumerable<double> times = new[] { 0.0, duration }.Concat(curve.KeyTimes.Where(t => t >= 0 && t <= duration));
            if (curve.Sample) {
                int samples = checked((int) Math.Ceiling(duration / frequency * fps));
                times = times.Concat(Enumerable.Range(0, samples + 1).Select(i => Math.Min(duration, (double) i * frequency / fps)));
            }
            double[] keyTimes = times.Distinct().Order().ToArray();
            if (curve.Sample) keyTimes = Refine(curve.Evaluate, keyTimes, channel);
            // The uniform grid, source keys and adaptive samples can round to the
            // same glTF float32 time. Keep the latest source sample, including the endpoint.
            keyTimes = keyTimes.GroupBy(time => (float) (time / frequency)).Select(group => group.Last()).ToArray();
            List<double> values = [];
            Quaternion? previous = null;
            foreach (double time in keyTimes) {
                double[] value = curve.Evaluate(time);
                if (channel == 1) {
                    Quaternion q = QuaternionValue(value);
                    if (previous is { } p && Quaternion.Dot(p, q) < 0) q = -q;
                    previous = q;
                    values.AddRange(new double[] { q.X, q.Y, q.Z, q.W });
                } else if (channel == 2) values.AddRange(Enumerable.Repeat(value[0], 3));
                else values.AddRange(value);
            }
            int outputWidth = channel == 1 ? 4 : 3;
            if (Enumerable.Range(outputWidth, values.Count - outputWidth).All(i => Math.Abs(values[i] - values[i % outputWidth]) < 1e-7)) {
                values = [..values.Take(outputWidth), ..values.Take(outputWidth)];
                keyTimes = [0, duration];
            }
            yield return new AnimationTrack(node, paths[channel], keyTimes.Select(t => t / frequency).ToArray(), values.ToArray(), outputWidth, curve.Step ? "STEP" : "LINEAR");
        }
    }
    private static double[][] Pose(NifReader r) => [ReadValues(r, 3), ReadValues(r, 4), [r.Float()]];
    private static double[] ReadValues(NifReader r, int width) => Enumerable.Range(0, width).Select(_ => (double) r.Float()).ToArray();
    public static Quaternion QuaternionValue(double[] wxyz) {
        Quaternion value = new((float) wxyz[1], (float) wxyz[2], (float) wxyz[3], (float) wxyz[0]);
        if (!float.IsFinite(value.LengthSquared()) || value.LengthSquared() < 1e-12f) throw new InvalidDataException("Invalid animation quaternion.");
        return Quaternion.Normalize(value);
    }
    private static double[] Wxyz(Quaternion q) => [q.W, q.X, q.Y, q.Z];

    public static double[] Refine(Func<double, double[]> evaluate, double[] times, int channel) {
        List<double> output = [times[0]];
        void Interval(double start, double end, int depth) {
            double[] a = evaluate(start), b = evaluate(end);
            bool split = false;
            foreach (double fraction in new[] { 0.25, 0.5, 0.75 }) {
                double[] exact = evaluate(start + (end - start) * fraction);
                if (channel == 1) {
                    Quaternion q = QuaternionValue(exact);
                    Quaternion linear = Quaternion.Slerp(QuaternionValue(a), QuaternionValue(b), (float) fraction);
                    if (Quaternion.Dot(q, linear) < 0) q = -q;
                    // Quaternion chord distance corresponding to 0.05 degrees of rotation.
                    split |= (q - linear).LengthSquared() > Math.Pow(2 * Math.Sin(0.05 * Math.PI / 720), 2);
                } else {
                    double tolerance = channel == 0 ? 0.001 : 0.00001;
                    split |= exact.Where((v, i) => Math.Abs(v - (a[i] + fraction * (b[i] - a[i]))) > tolerance).Any();
                }
            }
            if (split) {
                if (depth >= 16) throw new InvalidDataException("Animation curve exceeds sampling tolerance after 16 subdivisions.");
                double middle = (start + end) / 2;
                Interval(start, middle, depth + 1); Interval(middle, end, depth + 1);
            } else output.Add(end);
        }
        for (int i = 1; i < times.Length; i++) Interval(times[i - 1], times[i], 0);
        return output.ToArray();
    }

    private static AnimationCurve?[] KeyData(NifReader r) {
        int rotationCount = r.Count();
        uint rotationType = rotationCount == 0 ? 0 : r.U32();
        AnimationCurve? rotation = null;
        if (rotationType == 4) {
            AnimationCurve?[] axes = [KeyGroup(r, 1), KeyGroup(r, 1), KeyGroup(r, 1)];
            rotation = new AnimationCurve(time => {
                float[] angle = axes.Select(axis => (float) (axis?.Evaluate(time)[0] ?? 0)).ToArray();
                Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle[2]) *
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle[1]) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle[0]);
                return Wxyz(q);
            }, axes.Where(axis => axis is not null).SelectMany(axis => axis!.KeyTimes).ToArray(), true);
        } else if (rotationCount > 0) {
            AnimationKey[] keys = ReadKeys(r, rotationCount, 4, rotationType, true);
            if (rotationType is not (1 or 2 or 3 or 5)) throw new NotSupportedException($"Rotation interpolation {rotationType}.");
            if (rotationType == 2 && keys.Length > 1) throw new NotSupportedException("Quadratic quaternion interpolation requires validation against a reference export.");
            if (rotationType == 3 && keys.Length > 1) throw new NotSupportedException("TCB quaternion interpolation requires validation against a reference export.");
            rotation = new AnimationCurve(time => {
                (AnimationKey a, AnimationKey b, double u) = Interval(keys, time);
                return Wxyz(rotationType == 5 ? QuaternionValue(a.Value) : Quaternion.Slerp(QuaternionValue(a.Value), QuaternionValue(b.Value), (float) u));
            }, keys.Select(key => key.Time).ToArray(), false, rotationType == 5);
        }
        AnimationCurve? position = KeyGroup(r, 3), scale = KeyGroup(r, 1);
        r.Finish();
        return [position, rotation, scale];
    }
    private static AnimationCurve? KeyGroup(NifReader r, int width) {
        int count = r.Count();
        if (count == 0) return null;
        uint interpolation = r.U32();
        AnimationKey[] keys = ReadKeys(r, count, width, interpolation, false);
        if (interpolation is not (1 or 2 or 3 or 5)) throw new NotSupportedException($"Key interpolation {interpolation}.");
        if (interpolation == 3 && keys.Length > 1) throw new NotSupportedException("TCB vector interpolation requires validation against a reference export.");
        return new AnimationCurve(time => {
            (AnimationKey a, AnimationKey b, double u) = Interval(keys, time);
            return Enumerable.Range(0, width).Select(c => interpolation switch {
                2 when a != b => Hermite(a.Value[c], b.Value[c], a.Backward[c], b.Forward[c], u),
                5 => a.Value[c],
                _ => a.Value[c] + u * (b.Value[c] - a.Value[c])
            }).ToArray();
        }, keys.Select(key => key.Time).ToArray(), interpolation == 2, interpolation == 5);
    }
    private static AnimationKey[] ReadKeys(NifReader r, int count, int width, uint interpolation, bool quaternion) {
        AnimationKey[] keys = Enumerable.Range(0, count).Select(_ => new AnimationKey(r.Float(), ReadValues(r, width),
            interpolation == 2 && !quaternion ? ReadValues(r, width) : [],
            interpolation == 2 && !quaternion ? ReadValues(r, width) : [],
            interpolation == 3 ? ReadValues(r, 3) : [])).ToArray();
        for (int i = 1; i < keys.Length; i++) {
            if (keys[i].Time <= keys[i - 1].Time) throw new InvalidDataException("Animation keys must be strictly increasing.");
        }
        return keys;
    }
    private static (AnimationKey A, AnimationKey B, double U) Interval(AnimationKey[] keys, double time) {
        if (time <= keys[0].Time) return (keys[0], keys[0], 0);
        for (int i = 1; i < keys.Length; i++) {
            if (time < keys[i].Time) return (keys[i - 1], keys[i], (time - keys[i - 1].Time) / (keys[i].Time - keys[i - 1].Time));
        }
        return (keys[^1], keys[^1], 0);
    }
    public static double Hermite(double a, double b, double outgoing, double incoming, double u) =>
        (2 * u * u * u - 3 * u * u + 1) * a + (-2 * u * u * u + 3 * u * u) * b +
        (u * u * u - 2 * u * u + u) * outgoing + (u * u * u - u * u) * incoming;

    // Cubic open-uniform B-spline, evaluated with de Boor's algorithm.
    public static double[] BSpline(double[] control, int width, double time) {
        int count = control.Length / width;
        if (count < 4 || control.Length % width != 0) throw new InvalidDataException("Invalid cubic control points.");
        time = Math.Clamp(time, 0, 1);
        double Knot(int i) => i < 4 ? 0 : i >= count ? 1 : (double) (i - 3) / (count - 3);
        int span = time == 1 ? count - 1 : Math.Min(count - 1, 3 + (int) (time * (count - 3)));
        double[][] d = Enumerable.Range(0, 4).Select(i => control.Skip((span - 3 + i) * width).Take(width).ToArray()).ToArray();
        for (int level = 1; level <= 3; level++) {
            for (int j = 3; j >= level; j--) {
                int i = span - 3 + j;
                double alpha = (time - Knot(i)) / (Knot(i + 4 - level) - Knot(i));
                for (int c = 0; c < width; c++) d[j][c] = (1 - alpha) * d[j - 1][c] + alpha * d[j][c];
            }
        }
        return d[3];
    }
}
