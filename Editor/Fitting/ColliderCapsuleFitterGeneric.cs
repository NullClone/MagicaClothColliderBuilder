using MagicaCloth2;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderCapsuleFitter
    {
        internal static bool TryFitPalm(ColliderGenerationJob job, ref CapsuleFitResult fitResult)
        {
            if (job == null ||
                job.TargetBone == null ||
                job.Property == null ||
                job.Vertices == null ||
                job.Vertices.Length < 4 ||
                !TryPalmHint(job.Animator, job.TargetBone.transform, out Vector3 palmAxis, out float palmLength))
            {
                return false;
            }

            palmLength = Mathf.Max(palmLength, job.Property.LimbFitProperty.MinJointDistance);
            Vector3 axis = palmAxis.normalized;
            Quaternion palmRotation = Quaternion.FromToRotation(Vector3.up, axis);
            Quaternion inverseRotation = Quaternion.Inverse(palmRotation);
            var rotated = new Vector3[job.Vertices.Length];
            var palmX = new List<float>();
            var palmZ = new List<float>();
            float minY = -palmLength * 0.10f;
            float maxY = palmLength * 0.82f;
            float sampleMinY = -palmLength * 0.18f;
            float sampleMaxY = palmLength * 0.95f;

            for (int i = 0; i < job.Vertices.Length; ++i)
            {
                Vector3 rv = inverseRotation * job.Vertices[i];
                rotated[i] = rv;

                if (rv.y < sampleMinY || rv.y > sampleMaxY)
                {
                    continue;
                }

                palmX.Add(rv.x);
                palmZ.Add(rv.z);
            }

            if (palmX.Count == 0)
            {
                return false;
            }

            float centerX = Percentile(palmX, 50.0f);
            float centerZ = Percentile(palmZ, 50.0f);
            float centerY = (minY + maxY) * 0.5f;
            float length = Mathf.Max(job.Property.GenericFitProperty.MinLength, maxY - minY);
            float endWindow = Mathf.Max(length * 0.30f, 0.004f);
            var allRadii = new List<float>();
            var wristRadii = new List<float>();
            var knuckleRadii = new List<float>();

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (v.y < sampleMinY || v.y > sampleMaxY)
                {
                    continue;
                }

                float dx = v.x - centerX;
                float dz = v.z - centerZ;
                float radial = Mathf.Sqrt((dx * dx) + (dz * dz));
                allRadii.Add(radial);

                if (v.y <= minY + endWindow)
                {
                    wristRadii.Add(radial);
                }

                if (v.y >= maxY - endWindow)
                {
                    knuckleRadii.Add(radial);
                }
            }

            if (allRadii.Count == 0)
            {
                return false;
            }

            if (wristRadii.Count == 0)
            {
                wristRadii.AddRange(allRadii);
            }

            if (knuckleRadii.Count == 0)
            {
                knuckleRadii.AddRange(allRadii);
            }

            FitMode fitMode = ResolveFitMode(job, BoneFitRole.Default);
            float radiusPercentile = job.Property.LimbFitProperty.GetRadiusPercentile(fitMode);
            float globalRadius = Percentile(allRadii, Mathf.Min(radiusPercentile + 6.0f, 58.0f));
            float wristRadius = Mathf.Min(Percentile(wristRadii, radiusPercentile), globalRadius);
            float knuckleRadius = Mathf.Min(Percentile(knuckleRadii, radiusPercentile), globalRadius);
            float maxAllowedRadius = Mathf.Max(0.008f, palmLength * 0.48f) * job.Property.LimbFitProperty.GetRadiusCapScale(fitMode);
            float minRadius = job.Property.GenericFitProperty.MinRadius;

            wristRadius = Mathf.Clamp(wristRadius * job.Property.LimbFitProperty.RadiusScale, minRadius, maxAllowedRadius);
            knuckleRadius = Mathf.Clamp(knuckleRadius * job.Property.LimbFitProperty.RadiusScale, minRadius, maxAllowedRadius);

            fitResult.LocalRotation = palmRotation;
            fitResult.Direction = MagicaCapsuleCollider.Direction.Y;
            fitResult.Center = new Vector3(centerX, centerY, centerZ);
            fitResult.Length = length;
            fitResult.RadiusAtMin = wristRadius;
            fitResult.RadiusAtMax = knuckleRadius;
            fitResult.ReverseDirection = false;

            return true;
        }

        internal static bool TryFitLimb(ColliderGenerationJob job, Vector3 childHint, BoneFitRole boneRole, ref CapsuleFitResult fitResult)
        {
            var limbSettings = job.Property.LimbFitProperty;
            var limbAxis = childHint.normalized;
            var jointDistance = Mathf.Max(childHint.magnitude, limbSettings.MinJointDistance);
            bool centerToMeshCrossSection = IsHumanoidFingerBone(job.Animator, job.TargetBone.transform);
            FitMode fitMode = ResolveFitMode(job, boneRole);
            float radiusPercentile = limbSettings.GetRadiusPercentile(fitMode);

            var limbRotation = Quaternion.FromToRotation(Vector3.up, limbAxis);

            if (!TryFitOnY(
                job.Vertices,
                Quaternion.Inverse(limbRotation),
                radiusPercentile,
                jointDistance,
                boneRole,
                true,
                centerToMeshCrossSection,
                fitMode,
                job.Property,
                out Vector3 limbCenter,
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
            fitResult.Center = new Vector3(limbCenter.x, centerY, limbCenter.z);
            fitResult.Length = totalLength;
            fitResult.RadiusAtMin = limbStartRadius;
            fitResult.RadiusAtMax = limbEndRadius;
            fitResult.ReverseDirection = false;

            return true;
        }

        internal static bool TryFitAuto(ColliderGenerationJob job, Vector3[] vertices, BoneFitRole boneRole, bool hasChildHint, Vector3 childHint, bool hasParentHint, Vector3 parentHint, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            FitMode fitMode = ResolveFitMode(job, boneRole);
            float fitPercentile = job.Property.GenericFitProperty.GetFitPercentile(boneRole, fitMode);
            var axisCandidates = hasChildHint && childHint.sqrMagnitude > 1.0e-8f && !IsBodyRole(boneRole)
            ? BuildLimbAxes(vertices, childHint, hasParentHint, parentHint)
            : BuildAxes(vertices, hasChildHint, childHint, hasParentHint, parentHint);

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

                if (!TryFitOnY(
                    vertices,
                    inverseRotation,
                    fitPercentile,
                    boneLengthHint,
                    boneRole,
                    boneRole == BoneFitRole.Neck,
                    false,
                    fitMode,
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


        private static bool TryFitOnY(Vector3[] vertices, Quaternion inverseRotation, float fitPercentile, float boneLengthHint, BoneFitRole boneRole, bool useBoneAxisCenter, bool centerToMeshCrossSection, FitMode fitMode, SABoneColliderProperty property, out Vector3 center, out float length, out float startRadius, out float endRadius, out float score)
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
                if (centerToMeshCrossSection)
                {
                    ResolveCrossSectionCenter(rotated, minY, maxY, out centerX, out centerZ);
                }
                else
                {
                    centerX = 0.0f;
                    centerZ = 0.0f;
                }
                centerY = length * 0.5f;
            }
            else
            {
                var sortedYValues = new List<float>(yValues);
                sortedYValues.Sort();
                GetBounds(sortedYValues, boneLengthHint, boneRole, property.GenericFitProperty, out minY, out maxY, out length);
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

            if (fitMode == FitMode.Inner && allRadii.Count > 0)
            {
                float innerGlobalRadius = Percentile(allRadii, Mathf.Min(fitPercentile + 8.0f, 58.0f));
                startRadius = Mathf.Min(startRadius, innerGlobalRadius);
                endRadius = Mathf.Min(endRadius, innerGlobalRadius);
            }

            float roleRadiusScale = property.GenericFitProperty.GetRadiusScale(boneRole);
            startRadius *= roleRadiusScale;
            endRadius *= roleRadiusScale;

            float maxAllowedRadius = Mathf.Min(
                Mathf.Max(0.02f, boneLengthHint * property.GenericFitProperty.MaxRadiusByBoneRatio),
                Mathf.Max(0.01f, length * property.GenericFitProperty.MaxRadiusByLengthRatio));
            float radiusCapScale = useBoneAxisCenter
                ? property.LimbFitProperty.GetRadiusCapScale(fitMode)
                : property.GenericFitProperty.GetRadiusCapScale(fitMode);
            maxAllowedRadius *= radiusCapScale;
            startRadius = Mathf.Clamp(startRadius, property.GenericFitProperty.MinRadius, maxAllowedRadius);
            endRadius = Mathf.Clamp(endRadius, property.GenericFitProperty.MinRadius, maxAllowedRadius);

            if (boneRole == BoneFitRole.UpperChest)
            {
                float minUpperChestRadius = Mathf.Max(property.GenericFitProperty.UpperChestMinRadius, length * property.GenericFitProperty.UpperChestMinRadiusByLengthRatio);
                startRadius = Mathf.Max(startRadius, minUpperChestRadius);
                endRadius = Mathf.Max(endRadius, minUpperChestRadius * 0.95f);
            }

            score = ScoreCapsule(rotated, minY, maxY, centerX, centerZ, length, startRadius, endRadius);
            return true;
        }

        private static void ResolveCrossSectionCenter(Vector3[] rotated, float minY, float maxY, out float centerX, out float centerZ)
        {
            var xValues = new List<float>();
            var zValues = new List<float>();
            float margin = Mathf.Max((maxY - minY) * 0.15f, 0.002f);
            float allowedMinY = minY - margin;
            float allowedMaxY = maxY + margin;

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (v.y < allowedMinY || v.y > allowedMaxY)
                {
                    continue;
                }

                xValues.Add(v.x);
                zValues.Add(v.z);
            }

            if (xValues.Count == 0)
            {
                centerX = 0.0f;
                centerZ = 0.0f;
                return;
            }

            centerX = Percentile(xValues, 50.0f);
            centerZ = Percentile(zValues, 50.0f);
        }

        private static void GetBounds(List<float> sortedYValues, float boneLengthHint, BoneFitRole boneRole, GenericFitProperty settings, out float minY, out float maxY, out float length)
        {
            var (lowerPercentile, upperPercentile) = settings.GetPercentileBounds(boneRole);

            minY = SortedPercentile(sortedYValues, lowerPercentile);
            maxY = SortedPercentile(sortedYValues, upperPercentile);

            if (maxY <= minY)
            {
                minY = SortedPercentile(sortedYValues, 0.0f);
                maxY = SortedPercentile(sortedYValues, 100.0f);
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

        private static float ScoreCapsule(Vector3[] rotated, float minY, float maxY, float centerX, float centerZ, float length, float startRadius, float endRadius)
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

        private static float ScoreUniform(Vector3[] vertices, Vector3 center, float totalLength, float radius)
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
