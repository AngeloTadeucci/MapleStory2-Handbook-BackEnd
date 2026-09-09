using System.Numerics;

namespace NifToGltf.Native;

// Time-adjusted Kochanek-Bartels curves. Rotation controls live in quaternion
// log space and are evaluated with SQUAD; scalar/vector channels use Hermite.
internal static class TcbInterpolation {
    internal static Quaternion DirectedSlerp(Quaternion from, Quaternion to, float fraction) {
        // Source keys are hemisphere-aligned once. SQUAD controls must retain
        // their authored arc; flipping them at dot=0 creates a discontinuity.
        double fromLength = Math.Sqrt((double) from.X * from.X + (double) from.Y * from.Y + (double) from.Z * from.Z + (double) from.W * from.W);
        double toLength = Math.Sqrt((double) to.X * to.X + (double) to.Y * to.Y + (double) to.Z * to.Z + (double) to.W * to.W);
        double cosine = Math.Clamp(((double) from.X * to.X + (double) from.Y * to.Y + (double) from.Z * to.Z + (double) from.W * to.W) / (fromLength * toLength), -1, 1);
        if (cosine > 0.999999) return Quaternion.Normalize(from * (1 - fraction) + to * fraction);
        if (cosine < -1 + 1e-12) throw new NotSupportedException("Antipodal TCB controls require an explicit rotation arc.");
        double angle = Math.Acos(cosine), sine = Math.Sin(angle);
        double a = Math.Sin((1 - fraction) * angle) / (sine * fromLength);
        double b = Math.Sin(fraction * angle) / (sine * toLength);
        // Retain precision while the nearly opposed components cancel.
        return Quaternion.Normalize(new Quaternion((float) (from.X * a + to.X * b), (float) (from.Y * a + to.Y * b),
                                                    (float) (from.Z * a + to.Z * b), (float) (from.W * a + to.W * b)));
    }

    private static Vector3 Log(Quaternion value) {
        value = Quaternion.Normalize(value);
        Vector3 vector = new(value.X, value.Y, value.Z);
        float length = vector.Length();
        return length < 1e-8f ? vector : vector * (MathF.Atan2(length, value.W) / length);
    }

    private static Quaternion Exp(Vector3 value) {
        float angle = value.Length();
        float factor = angle < 1e-8f ? 1 : MathF.Sin(angle) / angle;
        return Quaternion.Normalize(new Quaternion(value * factor, MathF.Cos(angle)));
    }

    private static (double Previous, double Next) Weights(AnimationKey key, bool outgoing) {
        double tension = 1 - key.Tbc[0], continuity = key.Tbc[1], bias = key.Tbc[2];
        double direction = outgoing ? continuity : -continuity;
        return (tension * (1 + direction) * (1 + bias), tension * (1 - direction) * (1 - bias));
    }

    public static AnimationCurve Rotation(AnimationKey[] keys) {
        Quaternion[] values = keys.Select(key => AnimationReader.QuaternionValue(key.Value)).ToArray();
        for (int i = 1; i < values.Length; i++)
            if (Quaternion.Dot(values[i - 1], values[i]) < 0) values[i] = -values[i];
        Quaternion[] outgoing = new Quaternion[keys.Length], incoming = new Quaternion[keys.Length];
        for (int i = 0; i < keys.Length; i++) {
            int left = Math.Max(0, i - 1), right = Math.Min(keys.Length - 1, i + 1);
            Vector3 before = Log(Quaternion.Conjugate(values[left]) * values[i]);
            Vector3 after = Log(Quaternion.Conjugate(values[i]) * values[right]);
            double span = keys[right].Time - keys[left].Time;
            var departure = Weights(keys[i], true);
            var arrival = Weights(keys[i], false);
            Vector3 d = (float) ((keys[i].Time - keys[left].Time) / span) *
                ((float) departure.Previous * before + (float) departure.Next * after);
            Vector3 a = (float) ((keys[right].Time - keys[i].Time) / span) *
                ((float) arrival.Previous * before + (float) arrival.Next * after);
            outgoing[i] = Quaternion.Normalize(values[i] * Exp((d - after) / 2));
            incoming[i] = Quaternion.Normalize(values[i] * Exp((before - a) / 2));
        }
        return new AnimationCurve(time => {
            int segment = Segment(keys, time);
            Quaternion result;
            if (time <= keys[0].Time) result = values[0];
            else if (time >= keys[^1].Time) result = values[^1];
            else {
                float fraction = (float) ((time - keys[segment].Time) / (keys[segment + 1].Time - keys[segment].Time));
                Quaternion linear = DirectedSlerp(values[segment], values[segment + 1], fraction);
                Quaternion controls = DirectedSlerp(outgoing[segment], incoming[segment + 1], fraction);
                result = DirectedSlerp(linear, controls, 2 * fraction * (1 - fraction));
            }
            return [result.W, result.X, result.Y, result.Z];
        }, keys.Select(key => key.Time).ToArray(), true);
    }

    public static AnimationCurve Vector(AnimationKey[] keys, int width) {
        double[][] incoming = new double[keys.Length][], outgoing = new double[keys.Length][];
        for (int i = 0; i < keys.Length; i++) {
            double beforeTime = i == 0 || i == keys.Length - 1 ? 1 : keys[i].Time - keys[i - 1].Time;
            double afterTime = i == 0 || i == keys.Length - 1 ? 1 : keys[i + 1].Time - keys[i].Time;
            var departure = Weights(keys[i], true);
            var arrival = Weights(keys[i], false);
            incoming[i] = new double[width]; outgoing[i] = new double[width];
            for (int c = 0; c < width; c++) {
                double before = i == 0 ? keys[1].Value[c] - keys[0].Value[c] : keys[i].Value[c] - keys[i - 1].Value[c];
                double after = i == keys.Length - 1 ? before : keys[i + 1].Value[c] - keys[i].Value[c];
                outgoing[i][c] = (departure.Previous * before + departure.Next * after) * afterTime / (beforeTime + afterTime);
                incoming[i][c] = (arrival.Previous * before + arrival.Next * after) * beforeTime / (beforeTime + afterTime);
            }
        }
        return new AnimationCurve(time => {
            if (time <= keys[0].Time) return keys[0].Value;
            if (time >= keys[^1].Time) return keys[^1].Value;
            int segment = Segment(keys, time);
            double fraction = (time - keys[segment].Time) / (keys[segment + 1].Time - keys[segment].Time);
            return Enumerable.Range(0, width).Select(c => AnimationReader.Hermite(keys[segment].Value[c], keys[segment + 1].Value[c],
                outgoing[segment][c], incoming[segment + 1][c], fraction)).ToArray();
        }, keys.Select(key => key.Time).ToArray(), true);
    }

    private static int Segment(AnimationKey[] keys, double time) {
        int index = 0;
        while (index + 2 < keys.Length && keys[index + 1].Time <= time) index++;
        return index;
    }
}
