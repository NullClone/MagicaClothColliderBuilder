using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderFitter
    {
        private static bool TryRadialWeighted(Vector3[] rotatedVertices, int[] triangles, float percentile, out float weightedRadius)
        {
            weightedRadius = 0.0f;

            if (rotatedVertices == null || triangles == null || triangles.Length < 3)
            {
                return false;
            }

            var values = new List<float>(triangles.Length);
            var weights = new List<float>(triangles.Length);

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int i0 = triangles[i + 0];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= rotatedVertices.Length || i1 >= rotatedVertices.Length || i2 >= rotatedVertices.Length)
                {
                    continue;
                }

                Vector3 v0 = rotatedVertices[i0];
                Vector3 v1 = rotatedVertices[i1];
                Vector3 v2 = rotatedVertices[i2];

                float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;

                if (area <= 1.0e-12f)
                {
                    continue;
                }

                values.Add(Mathf.Sqrt((v0.x * v0.x) + (v0.z * v0.z)));
                weights.Add(area);
                values.Add(Mathf.Sqrt((v1.x * v1.x) + (v1.z * v1.z)));
                weights.Add(area);
                values.Add(Mathf.Sqrt((v2.x * v2.x) + (v2.z * v2.z)));
                weights.Add(area);
            }

            if (values.Count == 0)
            {
                return false;
            }

            weightedRadius = WeightPercentile(values, weights, percentile);

            return true;
        }

        private static float WeightPercentile(List<float> values, List<float> weights, float percentile)
        {
            if (values == null || weights == null || values.Count == 0 || weights.Count == 0)
            {
                return 0.0f;
            }

            int count = Mathf.Min(values.Count, weights.Count);

            if (count <= 0)
            {
                return 0.0f;
            }

            var samples = new List<(float Value, float Weight)>(count);

            float totalWeight = 0.0f;

            for (int i = 0; i < count; ++i)
            {
                float weight = weights[i];

                if (weight <= 0.0f)
                {
                    continue;
                }

                samples.Add((values[i], weight));
                totalWeight += weight;
            }

            if (samples.Count == 0 || totalWeight <= 0.0f)
            {
                return 0.0f;
            }

            samples.Sort((a, b) => a.Value.CompareTo(b.Value));

            float target = Mathf.Clamp(percentile, 0.0f, 100.0f) * 0.01f * totalWeight;
            float cumulative = 0.0f;

            for (int i = 0; i < samples.Count; ++i)
            {
                cumulative += samples[i].Weight;

                if (cumulative >= target)
                {
                    return samples[i].Value;
                }
            }

            return samples[^1].Value;
        }

        private static float Percentile(List<float> values, float percentile)
        {
            if (values == null || values.Count == 0)
            {
                return 0f;
            }

            values.Sort();

            return SortedPercentile(values, percentile);
        }

        private static float SortedPercentile(List<float> sortedValues, float percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0)
            {
                return 0f;
            }

            if (sortedValues.Count == 1)
            {
                return sortedValues[0];
            }

            float clampedPercentile = Mathf.Clamp(percentile, 0.0f, 100.0f);
            float rank = (clampedPercentile * 0.01f) * (sortedValues.Count - 1);
            int lowerIndex = Mathf.FloorToInt(rank);
            int upperIndex = Mathf.CeilToInt(rank);

            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            float t = rank - lowerIndex;

            return Mathf.Lerp(sortedValues[lowerIndex], sortedValues[upperIndex], t);
        }

        private static Vector3 GetPrincipalAxis(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0)
            {
                return Vector3.up;
            }

            Vector3 mean = Vector3.zero;

            for (int i = 0; i < vertices.Length; ++i)
            {
                mean += vertices[i];
            }

            mean /= vertices.Length;

            float xx = 0.0f;
            float xy = 0.0f;
            float xz = 0.0f;
            float yy = 0.0f;
            float yz = 0.0f;
            float zz = 0.0f;

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 d = vertices[i] - mean;
                xx += d.x * d.x;
                xy += d.x * d.y;
                xz += d.x * d.z;
                yy += d.y * d.y;
                yz += d.y * d.z;
                zz += d.z * d.z;
            }

            Vector3 axis = new Vector3(1.0f, 1.0f, 1.0f).normalized;

            for (int i = 0; i < 8; ++i)
            {
                Vector3 multiplied = new Vector3(
                    (xx * axis.x) + (xy * axis.y) + (xz * axis.z),
                    (xy * axis.x) + (yy * axis.y) + (yz * axis.z),
                    (xz * axis.x) + (yz * axis.y) + (zz * axis.z));

                if (multiplied.sqrMagnitude <= 1.0e-12f)
                {
                    break;
                }

                axis = multiplied.normalized;
            }

            return axis;
        }
    }
}
