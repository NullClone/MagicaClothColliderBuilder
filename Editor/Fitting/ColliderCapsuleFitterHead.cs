using MagicaCloth2;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderCapsuleFitter
    {
        internal static bool TryFitHead(ColliderGenerationJob job, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4 || job.TargetBone == null)
            {
                return false;
            }

            var settings = job.Property.HeadFitProperty;
            var headTransform = job.TargetBone.transform;

            if (settings.FitMethod == HeadFitMethod.FastBounds)
            {
                return TryFitHeadFast(job, settings, headTransform, out fitResult);
            }

            var xValues = new List<float>(vertices.Length);
            var yValues = new List<float>(vertices.Length);
            var zValues = new List<float>(vertices.Length);

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 v = vertices[i];
                xValues.Add(v.x);
                yValues.Add(v.y);
                zValues.Add(v.z);
            }

            var center = new Vector3(
                Percentile(xValues, 50.0f),
                Percentile(yValues, 50.0f),
                Percentile(zValues, 50.0f));


            if (!TryAxisWeighted(vertices, job.Triangles, 0, 50.0f, out float weightedCenterX))
            {
                weightedCenterX = center.x;
            }

            if (!TryAxisWeighted(vertices, job.Triangles, 1, 50.0f, out float weightedCenterY))
            {
                weightedCenterY = center.y;
            }

            if (!TryAxisWeighted(vertices, job.Triangles, 2, 50.0f, out float weightedCenterZ))
            {
                weightedCenterZ = center.z;
            }

            center = new Vector3(weightedCenterX, weightedCenterY, weightedCenterZ);

            Vector3 faceDir = headTransform.InverseTransformDirection(headTransform.root != null ? headTransform.root.forward : Vector3.forward);

            if (faceDir.sqrMagnitude <= 1.0e-8f)
            {
                faceDir = Vector3.forward;
            }

            faceDir.Normalize();

            Vector3 localUp = headTransform.InverseTransformDirection(headTransform.root != null ? headTransform.root.up : Vector3.up);

            if (localUp.sqrMagnitude <= 1.0e-8f)
            {
                localUp = Vector3.up;
            }

            localUp.Normalize();

            Vector3 offsetCenter = center;

            if (settings.UseFaceForwardOffsetWhenNotAnchored)
            {
                offsetCenter += (faceDir * settings.ForwardOffset) + (localUp * settings.UpOffset);
            }

            return TryFitHeadBest(job, settings, center, offsetCenter, out fitResult);
        }

        private static bool TryFitHeadFast(ColliderGenerationJob job, HeadFitProperty settings, Transform headTransform, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4)
            {
                return false;
            }

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 v = vertices[i];

                if (v.x < minX) minX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.z < minZ) minZ = v.z;
                if (v.x > maxX) maxX = v.x;
                if (v.y > maxY) maxY = v.y;
                if (v.z > maxZ) maxZ = v.z;
            }

            Vector3 center = new Vector3(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f,
                (minZ + maxZ) * 0.5f);

            float halfWidthX = Mathf.Max(0.0f, (maxX - minX) * 0.5f);
            float halfWidthZ = Mathf.Max(0.0f, (maxZ - minZ) * 0.5f);
            float baseRadius = Mathf.Max(halfWidthX, halfWidthZ);
            float radius = Mathf.Clamp(baseRadius * settings.RadiusScale, settings.MinRadius, settings.MaxRadius);
            float length = Mathf.Clamp(radius * settings.LengthRatio, 0.005f, radius * 0.6f);

            if (settings.AnchorOuterStartToHeadTransform)
            {
                center = new Vector3(0.0f, (length * 0.5f) + radius, 0.0f);
            }
            else if (settings.UseFaceForwardOffsetWhenNotAnchored)
            {
                Vector3 faceDir = headTransform.InverseTransformDirection(headTransform.root != null ? headTransform.root.forward : Vector3.forward);

                if (faceDir.sqrMagnitude <= 1.0e-8f)
                {
                    faceDir = Vector3.forward;
                }

                faceDir.Normalize();

                Vector3 localUp = headTransform.InverseTransformDirection(headTransform.root != null ? headTransform.root.up : Vector3.up);

                if (localUp.sqrMagnitude <= 1.0e-8f)
                {
                    localUp = Vector3.up;
                }

                localUp.Normalize();
                center += (faceDir * settings.ForwardOffset) + (localUp * settings.UpOffset);
            }

            fitResult = new CapsuleFitResult
            {
                LocalRotation = Quaternion.identity,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = center,
                Length = length,
                RadiusAtMin = radius,
                RadiusAtMax = radius,
                ReverseDirection = false,
            };
            return true;
        }

        internal static bool TryFitHeadBest(ColliderGenerationJob job, HeadFitProperty settings, Vector3 center, Vector3 offsetCenter, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4)
            {
                return false;
            }

            var lowerPercentiles = new float[] { 1.0f, 3.0f, 5.0f, 8.0f, 10.0f };
            var upperPercentiles = new float[] { 99.0f, 97.0f, 95.0f, 92.0f, 90.0f };
            var radiusPercentiles = new float[]
            {
                Mathf.Clamp(settings.RadiusPercentile - 6.0f, 45.0f, 98.0f),
                Mathf.Clamp(settings.RadiusPercentile - 3.0f, 45.0f, 98.0f),
                Mathf.Clamp(settings.RadiusPercentile, 45.0f, 98.0f),
                Mathf.Clamp(settings.RadiusPercentile + 3.0f, 45.0f, 98.0f),
            };
            var lengthScales = new float[] { 0.7f, 0.82f, 0.94f, 1.0f };
            var centerYRatios = new float[] { 0.45f, 0.5f, 0.55f };

            var centerCandidates = new List<Vector3>(3) { center };

            if ((offsetCenter - center).sqrMagnitude > 1.0e-10f)
            {
                centerCandidates.Add(offsetCenter);
            }

            if (settings.AnchorOuterStartToHeadTransform)
            {
                centerCandidates.Add(new Vector3(0.0f, center.y, 0.0f));
            }

            var sortedYValues = new List<float>(GetAxisValues(vertices, 1));
            sortedYValues.Sort();
            var minYCandidates = new float[lowerPercentiles.Length];
            var maxYCandidates = new float[upperPercentiles.Length];

            for (int p = 0; p < lowerPercentiles.Length; ++p)
            {

                if (!TryAxisWeighted(vertices, job.Triangles, 1, lowerPercentiles[p], out float minY))
                {
                    minY = SortedPercentile(sortedYValues, lowerPercentiles[p]);
                }

                if (!TryAxisWeighted(vertices, job.Triangles, 1, upperPercentiles[p], out float maxY))
                {
                    maxY = SortedPercentile(sortedYValues, upperPercentiles[p]);
                }

                minYCandidates[p] = minY;
                maxYCandidates[p] = maxY;
            }

            bool hasCandidate = false;
            float bestScore = float.MaxValue;

            for (int c = 0; c < centerCandidates.Count; ++c)
            {
                Vector3 centerSeed = centerCandidates[c];

                for (int p = 0; p < lowerPercentiles.Length; ++p)
                {
                    float minY = minYCandidates[p];
                    float maxY = maxYCandidates[p];

                    if (maxY <= minY) continue;

                    float span = maxY - minY;

                    for (int cy = 0; cy < centerYRatios.Length; ++cy)
                    {
                        float centerY = Mathf.Lerp(minY, maxY, centerYRatios[cy]);
                        Vector3 candidateCenter = new Vector3(centerSeed.x, centerY, centerSeed.z);
                        var baseRadii = new float[radiusPercentiles.Length];
                        bool hasWeightedRadii = TryDistanceWeighted(vertices, job.Triangles, candidateCenter, radiusPercentiles, baseRadii);
                        List<float> sortedDistanceValues = null;

                        if (!hasWeightedRadii)
                        {
                            sortedDistanceValues = new List<float>(vertices.Length);

                            for (int vertexIndex = 0; vertexIndex < vertices.Length; ++vertexIndex)
                            {
                                sortedDistanceValues.Add((vertices[vertexIndex] - candidateCenter).magnitude);
                            }

                            sortedDistanceValues.Sort();
                        }

                        for (int rp = 0; rp < radiusPercentiles.Length; ++rp)
                        {
                            float baseRadius = hasWeightedRadii
                                ? baseRadii[rp]
                                : SortedPercentile(sortedDistanceValues, radiusPercentiles[rp]);

                            float radius = Mathf.Clamp(baseRadius * settings.RadiusScale, settings.MinRadius, settings.MaxRadius);

                            for (int ls = 0; ls < lengthScales.Length; ++ls)
                            {
                                float totalLength = Mathf.Max(radius * 2.0f, span * lengthScales[ls]);
                                totalLength = Mathf.Max(totalLength, 0.005f);

                                float score = ScoreUniform(vertices, candidateCenter, totalLength, radius);

                                if (!hasCandidate || score < bestScore)
                                {
                                    hasCandidate = true;
                                    bestScore = score;
                                    fitResult = new CapsuleFitResult
                                    {
                                        LocalRotation = Quaternion.identity,
                                        Direction = MagicaCapsuleCollider.Direction.Y,
                                        Center = candidateCenter,
                                        Length = totalLength,
                                        RadiusAtMin = radius,
                                        RadiusAtMax = radius,
                                        ReverseDirection = false,
                                    };
                                }

                                if (settings.AnchorOuterStartToHeadTransform)
                                {
                                    Vector3 anchoredCenter = new Vector3(0.0f, (totalLength * 0.5f) + radius, 0.0f);
                                    float anchoredScore = ScoreUniform(vertices, anchoredCenter, totalLength, radius);

                                    if (!hasCandidate || anchoredScore < bestScore)
                                    {
                                        hasCandidate = true;
                                        bestScore = anchoredScore;
                                        fitResult = new CapsuleFitResult
                                        {
                                            LocalRotation = Quaternion.identity,
                                            Direction = MagicaCapsuleCollider.Direction.Y,
                                            Center = anchoredCenter,
                                            Length = totalLength,
                                            RadiusAtMin = radius,
                                            RadiusAtMax = radius,
                                            ReverseDirection = false,
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return hasCandidate;
        }
    }
}
