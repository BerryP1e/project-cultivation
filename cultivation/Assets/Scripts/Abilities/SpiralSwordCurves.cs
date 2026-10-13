using System;
using UnityEngine;

/// <summary>紧密错列的变形椭圆剑幕，以及曲线脱阵后保持切向直刺的范围弹道。</summary>
public static class SpiralSwordCurves
{
    [Serializable]
    public struct Ribbon
    {
        public float turns, radius, expansion, eccentricity, eccentricityWave, frequency, phase, precession, height, heightWave;
        public Vector3 Evaluate(float u, int layer)
        {
            float theta = Mathf.Clamp01(u) * turns * Mathf.PI * 2;
            float a = radius + expansion * theta + layer * .12f;
            float e = Mathf.Clamp(eccentricity + eccentricityWave * Mathf.Sin(theta * frequency + phase), .12f, .88f);
            float b = a * Mathf.Sqrt(1 - e * e);
            float cs = Mathf.Cos(theta), sn = Mathf.Sin(theta);
            float r = a * b / Mathf.Sqrt(b * b * cs * cs + a * a * sn * sn);
            float angle = theta + phase + theta * precession;
            return new Vector3(r * Mathf.Cos(angle), height + heightWave * (1 - Mathf.Cos(theta * .75f))
                + .35f * e + 4.2f * u + layer * .045f, r * Mathf.Sin(angle));
        }
        public static Ribbon Create(int seed, float turns = 2.4f)
        {
            var random = new System.Random(seed);
            return new Ribbon { turns = turns, radius = 2.2f, expansion = .12f,
                eccentricity = .48f + (float)random.NextDouble() * .07f, eccentricityWave = .27f,
                frequency = .7f + (float)random.NextDouble() * .2f, phase = (float)random.NextDouble() * Mathf.PI * 2,
                precession = .12f + (float)random.NextDouble() * .08f, height = .9f, heightWave = .7f };
        }
    }

    /// <summary>一次生成弧长查表，均匀排剑不依赖θ步长；运行时仅二分/插值。</summary>
    public sealed class ArcLengthRibbon
    {
        readonly Vector3[] points;
        readonly float[] lengths;
        public float Length => lengths[lengths.Length - 1];
        public ArcLengthRibbon(Ribbon ribbon, int layer, int samples = 640)
        {
            samples = Mathf.Clamp(samples, 128, 2048);
            points = new Vector3[samples + 1]; lengths = new float[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                points[i] = ribbon.Evaluate((float)i / samples, layer);
                if (i > 0) lengths[i] = lengths[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
        }
        public Vector3 Evaluate(float distance01)
        {
            float distance = Mathf.Clamp01(distance01) * Length;
            int lo = 0, hi = lengths.Length - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (lengths[mid] < distance) lo = mid; else hi = mid; }
            return Vector3.Lerp(points[lo], points[hi], (distance - lengths[lo]) / Mathf.Max(.00001f, lengths[hi] - lengths[lo]));
        }
        public Vector3 Tangent(float distance01)
            => (Evaluate(Mathf.Min(1, distance01 + .0005f)) - Evaluate(Mathf.Max(0, distance01 - .0005f))).normalized;
        public Quaternion SwordRotation(float distance01)
        {
            var point = Evaluate(distance01); var tangent = Tangent(distance01);
            var inward = new Vector3(-point.x, 0, -point.z).normalized;
            var width = (inward - tangent * Vector3.Dot(inward, tangent)).normalized;
            var normal = Vector3.Cross(width, tangent).normalized;
            // 一致的斜铺让剑面形成有厚度的幕，而不是零散的水平辐条。
            var blade = width * Mathf.Cos(.42f) + normal * Mathf.Sin(.42f);
            return Quaternion.LookRotation(blade, Vector3.Cross(blade, tangent));
        }
    }
    /// <summary>从剑幕主动射出，经过偏转出口后沿固定切线直刺各自落点；交接位置与速度连续。</summary>
    public struct SwordFlight
    {
        public Vector3 End, Start, Neck, StartVelocity, Axis, Side, Up;
        public float Duration, Handoff;
        public static SwordFlight Create(Vector3 origin, Vector3 aim, Vector3 end, Vector3 start, Vector3 startVelocity, float turns, float duration)
        {
            var offset = aim - origin; float length = offset.magnitude;
            var axis = length > .0001f ? offset / length : Vector3.forward;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > .95f ? Vector3.forward : Vector3.up).normalized;
            var up = Vector3.Cross(side, axis).normalized;
            float axial = Vector3.Dot(start - origin, axis);
            var radial = start - origin - axis * axial;
            // 出口保留剑幕截面宽度，不向敌人中心收成尖点。
            var neck = origin + axis * Mathf.Lerp(axial, length, .52f)
                + Quaternion.AngleAxis(turns * 360, axis) * radial * .62f;
            neck.y = Mathf.Max(neck.y, end.y + 1.2f);
            return new SwordFlight { End = end, Start = start, Neck = neck, StartVelocity = startVelocity,
                Axis = axis, Side = side, Up = up, Duration = Mathf.Max(.1f, duration), Handoff = .58f };
        }
        public Vector3 Evaluate(float u)
        {
            u = Mathf.Clamp01(u);
            if (u >= Handoff) return Vector3.LerpUnclamped(Neck, End, (u - Handoff) / (1 - Handoff));
            float t = u / Handoff, t2 = t * t, t3 = t2 * t;
            var initial = StartVelocity * (Duration * Handoff);
            var exit = (End - Neck) * (Handoff / (1 - Handoff));
            return (2 * t3 - 3 * t2 + 1) * Start + (t3 - 2 * t2 + t) * initial
                + (-2 * t3 + 3 * t2) * Neck + (t3 - t2) * exit;
        }
        public Vector3 Velocity(float u)
        {
            if (u >= Handoff) return (End - Neck) / ((1 - Handoff) * Duration);
            float t = Mathf.Clamp01(u / Handoff), t2 = t * t;
            var initial = StartVelocity * (Duration * Handoff);
            var exit = (End - Neck) * (Handoff / (1 - Handoff));
            return ((6 * t2 - 6 * t) * Start + (3 * t2 - 4 * t + 1) * initial
                + (-6 * t2 + 6 * t) * Neck + (3 * t2 - 2 * t) * exit) / (Duration * Handoff);
        }
    }
    public static Vector3 AreaOffset(int id, int count, float radius)
    {
        float distance = Mathf.Max(0, radius) * Mathf.Sqrt((id + .5f) / Mathf.Max(1, count));
        float angle = id * 2.39996323f;
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
    }
}
