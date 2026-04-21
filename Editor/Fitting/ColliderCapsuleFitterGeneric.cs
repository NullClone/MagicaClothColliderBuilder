using MagicaCloth2;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderCapsuleFitter
    {
        internal static bool TryFitLimb(ColliderGenerationJob job, Vector3 childHint, BoneFitRole boneRole, ref CapsuleFitResult fitResult)
        {
            var limbSettings = job.Property.LimbFitProperty;
            var limbAxis = childHint.normalized;
            var jointDistance = Mathf.Max(childHint.magnitude, limbSettings.MinJointDistance);

            var limbRotation = Quaternion.FromToRotation(Vector3.up, limbAxis);

            if (!TryEvaluateCapsuleFitOnYAxis(
                job.Vertices,
                Quaternion.Inverse(limbRotation),
                limbSettings.RadiusPercentile,
                jointDistance,
                boneRole,
                true,
                job.Property,
                out Vector3 _,
                out float _,
                out float limbStartRadius,
                out float limbEndRadius,
                out float _))
            {
                return false;
            }

            limbStartRadius *= limbSettings.RadiusScale;
            limbEndRadius *= limbSettings.RadiusScale;

            float totalLength = jointDistance + limbStartRadius + limbEndRadius;
            float centerY = (jointDistance * 0.5f) + ((limbStartRadius - limbEndRadius) * 0.5f);

            if (!limbSettings.AnchorStartSphereCenterToBone)
            {
                centerY = totalLength * 0.5f;
            }

            fitResult.LocalRotation = limbRotation;
            fitResult.Direction = MagicaCapsuleCollider.Direction.Y;
            fitResult.Center = new Vector3(0.0f, centerY, 0.0f);
            fitResult.Length = totalLength;
            fitResult.RadiusAtMin = limbStartRadius;
            fitResult.RadiusAtMax = limbEndRadius;
            fitResult.ReverseDirection = false;

            return true;
        }

        internal static bool TryFitBest(ColliderGenerationJob job, Vector3[] vertices, BoneFitRole boneRole, bool hasChildHint, Vector3 childHint, bool hasParentHint, Vector3 parentHint, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            float fitPercentile = job.Property.GenericFitProperty.GetFitPercentile(boneRole);
            var axisCandidates = hasChildHint && childHint.sqrMagnitude > 1.0e-8f && !IsBodyRole(boneRole)
                ? BuildLimbAxisCandidates(vertices, childHint, hasParentHint, parentHint)
                : BuildAxisCandidates(vertices, hasChildHint, childHint, hasParentHint, parentHint);

            if (axisCandidates.Count == 0)
            {
                axisCandidates.Add(Vector3.up);
            }

            float boneLengthHint = hasChildHint ? childHint.magnitude : (hasParentHint ? parentHint.magnitude : 0.1f);
            Vector3 childHintNormalized = hasChildHint ? childHint.normalized : Vector3.up;

            float bestScore = float.MaxValue;
            Quaternion bestRotation = Quaternion.identity;
            Vector3 bestCenter = Vector3.zero;
            float bestLength = 0.02f;
            float bestStartRadius = 0.01f;
            float bestEndRadius = 0.01f;

            foreach (var rawAxis in axisCandidates)
            {
                Vector3 axis = rawAxis;

                if (axis.sqrMagnitude <= 1.0e-8f)
                {
                    continue;
                }

                axis.Normalize();

                if (hasChildHint && Vector3.Dot(axis, childHint) < 0.0f)
                {
                    axis = -axis;
                }

                Quaternion candidateRotation = Quaternion.FromToRotation(Vector3.up, axis);
                Quaternion inverseRotation = Quaternion.Inverse(candidateRotation);
                float childAlignment = hasChildHint ? Mathf.Abs(Vector3.Dot(axis, childHintNormalized)) : 0.0f;

                if (!TryEvaluateCapsuleFitOnYAxis(
                    vertices,
                    inverseRotation,
                    fitPercentile,
                    boneLengthHint,
                    boneRole,
                    boneRole == BoneFitRole.Neck,
                    job.Property,
                    out Vector3 candidateCenter,
                    out float candidateLength,
                    out float candidateStartRadius,
                    out float candidateEndRadius,
                    out float candidateScore))
                {
                    continue;
                }

                if (hasChildHint)
                {
                    candidateStartRadius = Mathf.Min(candidateStartRadius, candidateEndRadius);
                }

                float score = candidateScore - (hasChildHint ? childAlignment * 0.08f : 0.0f);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestRotation = candidateRotation;
                    bestCenter = candidateCenter;
                    bestLength = candidateLength;
                    bestStartRadius = candidateStartRadius;
                    bestEndRadius = candidateEndRadius;
                }
            }

            if (bestScore == float.MaxValue)
            {
                return false;
            }

            fitResult = new CapsuleFitResult
            {
                LocalRotation = bestRotation,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = bestCenter,
                Length = bestLength,
                RadiusAtMin = bestStartRadius,
                RadiusAtMax = bestEndRadius,
                ReverseDirection = false,
            };
            return true;
        }


        private static bool TryEvaluateCapsuleFitOnYAxis(Vector3[] vertices, Quaternion inverseRotation, float fitPercentile, float boneLengthHint, BoneFitRole boneRole, bool useBoneAxisCenter, SABoneColliderProperty property, out Vector3 center, out float length, out float startRadius, out float endRadius, out float score)
        {
            center = Vector3.zero;
            length = property.GenericFitProperty.MinLength;
            startRadius = property.GenericFitProperty.MinRadius;
            endRadius = property.GenericFitProperty.MinRadius;
            score = float.MaxValue;

            if (vertices == null || vertices.Length < 4)
            {
                return false;
            }

            var yValues = new List<float>(vertices.Length);
            var xValues = new List<float>(vertices.Length);
            var zValues = new List<float>(vertices.Length);
            var rotated = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 rv = inverseRotation * vertices[i];
                rotated[i] = rv;
                xValues.Add(rv.x);
                yValues.Add(rv.y);
                zValues.Add(rv.z);
            }

            float minY;
            float maxY;
            float centerX;
            float centerY;
            float centerZ;

            if (useBoneAxisCenter)
            {
                minY = 0.0f;
                maxY = Mathf.Max(boneLengthHint, property.LimbFitProperty.MinJointDistance);
                length = maxY;
                centerX = 0.0f;
                centerZ = 0.0f;
                centerY = length * 0.5f;
            }
            else
            {
                var sortedYValues = new List<float>(yValues);
                sortedYValues.Sort();
                GetPercentileBoundsFromSorted(sortedYValues, boneLengthHint, boneRole, property.GenericFitProperty, out minY, out maxY, out length);
                centerX = Percentile(xValues, 50.0f);
                centerY = Mathf.Lerp(minY, maxY, property.GenericFitProperty.GetCenterYRatio(boneRole));
                centerZ = Percentile(zValues, 50.0f);
            }

            center = new Vector3(centerX, centerY, centerZ);

            float endWindow = Mathf.Max(length * 0.22f, 0.004f);
            float leakMargin = Mathf.Clamp(length * property.LimbFitProperty.LeakMarginScale, property.LimbFitProperty.LeakMarginMin, property.LimbFitProperty.LeakMarginMax);
            float yLeakMin = useBoneAxisCenter ? (minY - leakMargin) : float.NegativeInfinity;
            float yLeakMax = useBoneAxisCenter ? (maxY + leakMargin) : float.PositiveInfinity;

            var allRadii = new List<float>(vertices.Length);
            var radiiNearPos = new List<float>();
            var radiiNearNeg = new List<float>();

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (v.y < yLeakMin || v.y > yLeakMax)
                {
                    continue;
                }

                float dx = v.x - centerX;
                float dz = v.z - centerZ;
                float radial = Mathf.Sqrt((dx * dx) + (dz * dz));
                allRadii.Add(radial);

                if (v.y >= maxY - endWindow)
                {
                    radiiNearPos.Add(radial);
                }

                if (v.y <= minY + endWindow)
                {
                    radiiNearNeg.Add(radial);
                }
            }

            if (radiiNearPos.Count == 0)
            {
                radiiNearPos.AddRange(allRadii);
            }

            if (radiiNearNeg.Count == 0)
            {
                radiiNearNeg.AddRange(allRadii);
            }

            startRadius = Percentile(radiiNearPos, fitPercentile);
            endRadius = Percentile(radiiNearNeg, fitPercentile);

            float roleRadiusScale = property.GenericFitProperty.GetRadiusScale(boneRole);
            startRadius *= roleRadiusScale;
            endRadius *= roleRadiusScale;

            float maxAllowedRadius = Mathf.Min(
                Mathf.Max(0.02f, boneLengthHint * property.GenericFitProperty.MaxRadiusByBoneRatio),
                Mathf.Max(0.01f, length * property.GenericFitProperty.MaxRadiusByLengthRatio));
            startRadius = Mathf.Clamp(startRadius, property.GenericFitProperty.MinRadius, maxAllowedRadius);
            endRadius = Mathf.Clamp(endRadius, property.GenericFitProperty.MinRadius, maxAllowedRadius);

            if (boneRole == BoneFitRole.UpperChest)
            {
                float minUpperChestRadius = Mathf.Max(property.GenericFitProperty.UpperChestMinRadius, length * property.GenericFitProperty.UpperChestMinRadiusByLengthRatio);
                startRadius = Mathf.Max(startRadius, minUpperChestRadius);
                endRadius = Mathf.Max(endRadius, minUpperChestRadius * 0.95f);
            }

            score = CalculateCapsuleScore(rotated, minY, maxY, centerX, centerZ, length, startRadius, endRadius);
            return true;
        }

        private static void GetPercentileBoundsFromSorted(List<float> sortedYValues, float boneLengthHint, BoneFitRole boneRole, GenericFitProperty settings, out float minY, out float maxY, out float length)
        {
            var (lowerPercentile, upperPercentile) = settings.GetPercentileBounds(boneRole);

            minY = PercentileFromSorted(sortedYValues, lowerPercentile);
            maxY = PercentileFromSorted(sortedYValues, upperPercentile);

            if (maxY <= minY)
            {
                minY = PercentileFromSorted(sortedYValues, 0.0f);
                maxY = PercentileFromSorted(sortedYValues, 100.0f);
            }

            length = Mathf.Max(settings.MinLength, maxY - minY);

            if (boneRole == BoneFitRole.Hips)
            {
                length = Mathf.Min(length, Mathf.Max(settings.HipsMaxLength, boneLengthHint * settings.HipsMaxLengthByBoneRatio));
                maxY = minY + length;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                float minUpperChestLength = Mathf.Max(settings.UpperChestMinLength, boneLengthHint * settings.UpperChestMinLengthByBoneRatio);

                if (length < minUpperChestLength)
                {
                    float centerYTemp = (minY + maxY) * 0.5f;
                    length = minUpperChestLength;
                    minY = centerYTemp - (length * 0.5f);
                    maxY = centerYTemp + (length * 0.5f);
                }
            }
        }

        private static float CalculateCapsuleScore(Vector3[] rotated, float minY, float maxY, float centerX, float centerZ, float length, float startRadius, float endRadius)
        {
            var overflowList = new List<float>(rotated.Length);
            float overflowSum = 0.0f;

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];
                float t = Mathf.InverseLerp(minY, maxY, v.y);
                float allowed = Mathf.Lerp(endRadius, startRadius, t) + 0.0005f;
                float dx = v.x - centerX;
                float dz = v.z - centerZ;
                float radial = Mathf.Sqrt((dx * dx) + (dz * dz));
                float overflow = Mathf.Max(0.0f, radial - allowed);
                overflowList.Add(overflow);
                overflowSum += overflow;
            }

            float overflow95 = Percentile(overflowList, 95.0f);
            float overflowMean = overflowList.Count > 0 ? (overflowSum / overflowList.Count) : 0.0f;
            float compactness = (startRadius + endRadius) / Mathf.Max(length, 0.001f);
            return (overflow95 * 4.0f) + (overflowMean * 2.0f) + (compactness * 0.15f);
        }

        private static float CalculateUniformCapsuleScore(Vector3[] vertices, Vector3 center, float totalLength, float radius)
        {
            if (vertices == null || vertices.Length == 0)
            {
                return float.MaxValue;
            }

            float halfLength = totalLength * 0.5f;
            float cylinderHalf = Mathf.Max(0.0f, halfLength - radius);
            Vector3 segmentA = center + (Vector3.down * cylinderHalf);
            Vector3 segmentB = center + (Vector3.up * cylinderHalf);
            var overflowList = new List<float>(vertices.Length);
            float overflowSum = 0.0f;

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 vertex = vertices[i];
                Vector3 segment = segmentB - segmentA;
                float segmentLengthSq = segment.sqrMagnitude;
                float t = segmentLengthSq > 1.0e-10f
                    ? Mathf.Clamp01(Vector3.Dot(vertex - segmentA, segment) / segmentLengthSq)
                    : 0.0f;
                Vector3 closest = Vector3.Lerp(segmentA, segmentB, t);
                float overflow = Mathf.Max(0.0f, Vector3.Distance(vertex, closest) - radius);
                overflowList.Add(overflow);
                overflowSum += overflow;
            }

            float overflow95 = Percentile(overflowList, 95.0f);
            float overflowMean = overflowList.Count > 0 ? (overflowSum / overflowList.Count) : 0.0f;
            float compactness = radius / Mathf.Max(totalLength, 0.001f);
            return (overflow95 * 4.0f) + (overflowMean * 2.0f) + (compactness * 0.1f);
        }
    }
}
