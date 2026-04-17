using MagicaCloth2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public enum BoneFitRole
    {
        Default,
        Hips,
        Spine,
        Chest,
        UpperChest,
    }

    public struct CapsuleFitResult
    {
        public Quaternion LocalRotation;
        public MagicaCapsuleCollider.Direction Direction;
        public Vector3 Center;
        public float Length;
        public float RadiusAtMin;
        public float RadiusAtMax;
        public bool ReverseDirection;
    }

    public static class ColliderCapsuleFitter
    {
        public static bool TryFitCapsule(ColliderGenerationJob job, out CapsuleFitResult fitResult)
        {
            fitResult = new CapsuleFitResult
            {
                LocalRotation = Quaternion.identity,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = Vector3.zero,
                Length = 0.02f,
                RadiusAtMin = 0.01f,
                RadiusAtMax = 0.01f,
                ReverseDirection = false,
            };

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4) return false;

            bool hasChildHint = TryGetChildDirectionHint(job.TargetBone.transform, out Vector3 childHint);
            bool hasParentHint = TryGetParentDirectionHint(job.TargetBone.transform, out Vector3 parentHint);
            BoneFitRole boneRole = DetectBoneFitRole(job.TargetBone.transform);
            bool isLimbBone = IsLimbBone(job.TargetBone.transform);

            if (IsBodyRole(boneRole) && TryFitHorizontalBodyCapsule(job, boneRole, out fitResult))
            {
                return true;
            }

            if (isLimbBone && hasChildHint && TryFitLimbCapsule(job, childHint, boneRole, ref fitResult))
            {
                return true;
            }

            return TryFitBestAxisCapsule(job, vertices, boneRole, hasChildHint, childHint, hasParentHint, parentHint, out fitResult);
        }

        public static BoneFitRole DetectBoneFitRole(Transform boneTransform)
        {
            if (boneTransform == null)
            {
                return BoneFitRole.Default;
            }

            string boneName = boneTransform.name.ToLowerInvariant();

            if (boneName.Contains("upperchest") || boneName.Contains("upper_chest"))
            {
                return BoneFitRole.UpperChest;
            }

            if (boneName.Contains("hips") || boneName.Contains("pelvis"))
            {
                return BoneFitRole.Hips;
            }

            if (boneName.Contains("chest"))
            {
                return BoneFitRole.Chest;
            }

            if (boneName.Contains("spine"))
            {
                return BoneFitRole.Spine;
            }

            return BoneFitRole.Default;
        }

        private static bool TryFitLimbCapsule(ColliderGenerationJob job, Vector3 childHint, BoneFitRole boneRole, ref CapsuleFitResult fitResult)
        {
            Vector3 limbAxis = childHint.normalized;
            float jointDistance = Mathf.Max(childHint.magnitude, 0.02f);
            Quaternion limbRotation = Quaternion.FromToRotation(Vector3.up, limbAxis);

            if (!TryEvaluateCapsuleFitOnYAxis(
                job.Vertices,
                Quaternion.Inverse(limbRotation),
                job.Property.ReducerProperty.FitType == FitType.Outer ? 92.0f : 70.0f,
                jointDistance,
                boneRole,
                true,
                out Vector3 _,
                out float _,
                out float limbStartRadius,
                out float limbEndRadius,
                out float _))
            {
                return false;
            }

            // MagicaCapsuleCollider length behaves as the full end-to-end span.
            // To place the cylinder-cap seam centers on the bone origin and child bone position,
            // the total length must include both end radii.
            float totalLength = jointDistance + limbStartRadius + limbEndRadius;
            float centerY = (jointDistance * 0.5f) + ((limbStartRadius - limbEndRadius) * 0.5f);

            fitResult.LocalRotation = limbRotation;
            fitResult.Direction = MagicaCapsuleCollider.Direction.Y;
            fitResult.Center = new Vector3(0.0f, centerY, 0.0f);
            fitResult.Length = totalLength;
            fitResult.RadiusAtMin = limbStartRadius;
            fitResult.RadiusAtMax = limbEndRadius;
            fitResult.ReverseDirection = false;
            return true;
        }

        private static bool TryFitBestAxisCapsule(ColliderGenerationJob job, Vector3[] vertices, BoneFitRole boneRole, bool hasChildHint, Vector3 childHint, bool hasParentHint, Vector3 parentHint, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            float fitPercentile = GetFitPercentile(job.Property.ReducerProperty.FitType, boneRole);
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
                    false,
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

        private static float GetFitPercentile(FitType fitType, BoneFitRole boneRole)
        {
            float fitPercentile = fitType == FitType.Outer ? 92.0f : 70.0f;

            if (fitType != FitType.Outer)
            {
                return fitPercentile;
            }

            if (boneRole == BoneFitRole.Hips)
            {
                return 86.0f;
            }

            if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                return 88.0f;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return 95.0f;
            }

            return fitPercentile;
        }

        private static bool TryFitHorizontalBodyCapsule(ColliderGenerationJob job, BoneFitRole boneRole, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4 || job.TargetBone == null)
            {
                return false;
            }

            Transform boneTransform = job.TargetBone.transform;
            Vector3 worldHorizontal = boneTransform.root != null ? boneTransform.root.right : Vector3.right;
            Vector3 localHorizontal = boneTransform.InverseTransformDirection(worldHorizontal);

            if (localHorizontal.sqrMagnitude <= 1.0e-8f)
            {
                localHorizontal = Vector3.right;
            }

            localHorizontal.Normalize();
            Quaternion localRotation = Quaternion.FromToRotation(Vector3.up, localHorizontal);
            Quaternion inverseRotation = Quaternion.Inverse(localRotation);

            var absYValues = new List<float>(vertices.Length);
            var radialValues = new List<float>(vertices.Length);

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 rv = inverseRotation * vertices[i];
                absYValues.Add(Mathf.Abs(rv.y));
                radialValues.Add(Mathf.Sqrt((rv.x * rv.x) + (rv.z * rv.z)));
            }

            float halfLength = Percentile(absYValues, GetHorizontalLengthPercentile(boneRole));
            float length = Mathf.Max(0.03f, halfLength * 2.0f);
            float radius = Percentile(radialValues, job.Property.ReducerProperty.FitType == FitType.Outer ? 90.0f : 68.0f);
            radius *= GetHorizontalRadiusScale(boneRole);
            radius = Mathf.Clamp(radius, 0.008f, Mathf.Max(0.015f, length * 0.65f));

            fitResult = new CapsuleFitResult
            {
                LocalRotation = localRotation,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = Vector3.zero,
                Length = length,
                RadiusAtMin = radius,
                RadiusAtMax = radius,
                ReverseDirection = false,
            };
            return true;
        }

        private static float GetHorizontalLengthPercentile(BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return 90.0f;
            }

            if (boneRole == BoneFitRole.Spine)
            {
                return 84.0f;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return 88.0f;
            }

            return 86.0f;
        }

        private static float GetHorizontalRadiusScale(BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return 1.08f;
            }

            if (boneRole == BoneFitRole.Spine)
            {
                return 0.95f;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return 1.12f;
            }

            return 1.0f;
        }

        private static List<Vector3> BuildLimbAxisCandidates(Vector3[] vertices, Vector3 childHint, bool hasParentHint, Vector3 parentHint)
        {
            var candidates = new List<Vector3>();

            if (childHint.sqrMagnitude <= 1.0e-8f)
            {
                return BuildAxisCandidates(vertices, false, Vector3.zero, hasParentHint, parentHint);
            }

            Vector3 primary = childHint.normalized;
            candidates.Add(primary);

            if (hasParentHint && parentHint.sqrMagnitude > 1.0e-8f)
            {
                Vector3 towardChildFromParent = (-parentHint).normalized;
                Vector3 blended = (primary * 0.8f) + (towardChildFromParent * 0.2f);

                if (blended.sqrMagnitude > 1.0e-8f)
                {
                    candidates.Add(blended.normalized);
                }
            }

            Vector3 principal = GetPrincipalAxis(vertices);

            if (principal.sqrMagnitude > 1.0e-8f)
            {
                if (Vector3.Dot(principal, primary) < 0.0f)
                {
                    principal = -principal;
                }

                Vector3 principalBlend = (primary * 0.7f) + (principal.normalized * 0.3f);

                if (principalBlend.sqrMagnitude > 1.0e-8f)
                {
                    candidates.Add(principalBlend.normalized);
                }
            }

            Vector3 tangent = Vector3.Cross(primary, Vector3.up);

            if (tangent.sqrMagnitude <= 1.0e-8f)
            {
                tangent = Vector3.Cross(primary, Vector3.right);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(tangent, primary).normalized;
            float[] angles = new float[] { -14.0f, -8.0f, -4.0f, 4.0f, 8.0f, 14.0f };

            for (int i = 0; i < angles.Length; ++i)
            {
                Quaternion q1 = Quaternion.AngleAxis(angles[i], tangent);
                Quaternion q2 = Quaternion.AngleAxis(angles[i], bitangent);
                candidates.Add((q1 * primary).normalized);
                candidates.Add((q2 * primary).normalized);
            }

            return DeduplicateAxes(candidates, 0.9985f);
        }

        private static List<Vector3> BuildAxisCandidates(Vector3[] vertices, bool hasChildHint, Vector3 childHint, bool hasParentHint, Vector3 parentHint)
        {
            var candidates = new List<Vector3>();

            if (hasChildHint)
            {
                candidates.Add(childHint.normalized);
            }

            if (hasParentHint)
            {
                candidates.Add(parentHint.normalized);
            }

            candidates.Add(GetPrincipalAxis(vertices));
            candidates.Add(Vector3.right);
            candidates.Add(Vector3.up);
            candidates.Add(Vector3.forward);
            candidates.Add(-Vector3.right);
            candidates.Add(-Vector3.up);
            candidates.Add(-Vector3.forward);

            return DeduplicateAxes(candidates, 0.995f);
        }

        private static List<Vector3> DeduplicateAxes(List<Vector3> candidates, float dotThreshold)
        {
            var unique = new List<Vector3>();

            for (int i = 0; i < candidates.Count; ++i)
            {
                Vector3 candidate = candidates[i];

                if (candidate.sqrMagnitude <= 1.0e-8f)
                {
                    continue;
                }

                Vector3 normalized = candidate.normalized;
                bool duplicated = false;

                for (int n = 0; n < unique.Count; ++n)
                {
                    if (Mathf.Abs(Vector3.Dot(unique[n], normalized)) > dotThreshold)
                    {
                        duplicated = true;
                        break;
                    }
                }

                if (!duplicated)
                {
                    unique.Add(normalized);
                }
            }

            return unique;
        }

        private static bool TryEvaluateCapsuleFitOnYAxis(Vector3[] vertices, Quaternion inverseRotation, float fitPercentile, float boneLengthHint, BoneFitRole boneRole, bool useBoneAxisCenter, out Vector3 center, out float length, out float startRadius, out float endRadius, out float score)
        {
            center = Vector3.zero;
            length = 0.02f;
            startRadius = 0.01f;
            endRadius = 0.01f;
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
                maxY = Mathf.Max(boneLengthHint, 0.02f);
                length = maxY;
                centerX = 0.0f;
                centerZ = 0.0f;
                centerY = length * 0.5f;
            }
            else
            {
                GetPercentileBounds(yValues, boneLengthHint, boneRole, out minY, out maxY, out length);
                centerX = Percentile(xValues, 50.0f);
                centerY = Mathf.Lerp(minY, maxY, GetCenterYRatio(boneRole));
                centerZ = Percentile(zValues, 50.0f);
            }

            center = new Vector3(centerX, centerY, centerZ);

            float endWindow = Mathf.Max(length * 0.22f, 0.004f);
            float leakMargin = Mathf.Clamp(length * 0.08f, 0.004f, 0.012f);
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

            float roleRadiusScale = GetRoleRadiusScale(boneRole);
            startRadius *= roleRadiusScale;
            endRadius *= roleRadiusScale;

            float maxAllowedRadius = Mathf.Min(Mathf.Max(0.02f, boneLengthHint * 0.7f), Mathf.Max(0.01f, length * 0.7f));
            startRadius = Mathf.Clamp(startRadius, 0.003f, maxAllowedRadius);
            endRadius = Mathf.Clamp(endRadius, 0.003f, maxAllowedRadius);

            if (boneRole == BoneFitRole.UpperChest)
            {
                float minUpperChestRadius = Mathf.Max(0.012f, length * 0.18f);
                startRadius = Mathf.Max(startRadius, minUpperChestRadius);
                endRadius = Mathf.Max(endRadius, minUpperChestRadius * 0.95f);
            }

            score = CalculateCapsuleScore(rotated, minY, maxY, centerX, centerZ, length, startRadius, endRadius);
            return true;
        }

        private static void GetPercentileBounds(List<float> yValues, float boneLengthHint, BoneFitRole boneRole, out float minY, out float maxY, out float length)
        {
            float lowerPercentile = 2.0f;
            float upperPercentile = 98.0f;

            if (boneRole == BoneFitRole.Hips)
            {
                lowerPercentile = 35.0f;
                upperPercentile = 96.0f;
            }
            else if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                lowerPercentile = 8.0f;
                upperPercentile = 98.0f;
            }
            else if (boneRole == BoneFitRole.UpperChest)
            {
                lowerPercentile = 15.0f;
                upperPercentile = 98.0f;
            }

            minY = Percentile(yValues, lowerPercentile);
            maxY = Percentile(yValues, upperPercentile);

            if (maxY <= minY)
            {
                minY = Percentile(yValues, 0.0f);
                maxY = Percentile(yValues, 100.0f);
            }

            length = Mathf.Max(0.02f, maxY - minY);

            if (boneRole == BoneFitRole.Hips)
            {
                length = Mathf.Min(length, Mathf.Max(0.045f, boneLengthHint * 1.35f));
                maxY = minY + length;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                float minUpperChestLength = Mathf.Max(0.03f, boneLengthHint * 0.75f);

                if (length < minUpperChestLength)
                {
                    float centerYTemp = (minY + maxY) * 0.5f;
                    length = minUpperChestLength;
                    minY = centerYTemp - (length * 0.5f);
                    maxY = centerYTemp + (length * 0.5f);
                }
            }
        }

        private static float GetCenterYRatio(BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return 0.68f;
            }

            if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                return 0.56f;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return 0.58f;
            }

            return 0.5f;
        }

        private static float GetRoleRadiusScale(BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return 0.82f;
            }

            if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                return 0.9f;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return 1.15f;
            }

            return 1.0f;
        }

        private static float CalculateCapsuleScore(Vector3[] rotated, float minY, float maxY, float centerX, float centerZ, float length, float startRadius, float endRadius)
        {
            var overflowList = new List<float>(rotated.Length);

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];
                float t = Mathf.InverseLerp(minY, maxY, v.y);
                float allowed = Mathf.Lerp(endRadius, startRadius, t) + 0.0005f;
                float dx = v.x - centerX;
                float dz = v.z - centerZ;
                float radial = Mathf.Sqrt((dx * dx) + (dz * dz));
                overflowList.Add(Mathf.Max(0.0f, radial - allowed));
            }

            float overflow95 = Percentile(overflowList, 95.0f);
            float overflowMean = overflowList.Sum() / overflowList.Count;
            float compactness = (startRadius + endRadius) / Mathf.Max(length, 0.001f);
            return (overflow95 * 4.0f) + (overflowMean * 2.0f) + (compactness * 0.15f);
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

        private static float Percentile(List<float> values, float percentile)
        {
            if (values == null || values.Count == 0)
            {
                return 0.0f;
            }

            var ordered = new List<float>(values);
            ordered.Sort();

            if (ordered.Count == 1)
            {
                return ordered[0];
            }

            float clampedPercentile = Mathf.Clamp(percentile, 0.0f, 100.0f);
            float rank = (clampedPercentile * 0.01f) * (ordered.Count - 1);
            int lowerIndex = Mathf.FloorToInt(rank);
            int upperIndex = Mathf.CeilToInt(rank);

            if (lowerIndex == upperIndex)
            {
                return ordered[lowerIndex];
            }

            float t = rank - lowerIndex;
            return Mathf.Lerp(ordered[lowerIndex], ordered[upperIndex], t);
        }

        private static bool TryGetParentDirectionHint(Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (boneTransform == null || boneTransform.parent == null)
            {
                return false;
            }

            Vector3 parentWorldPos = boneTransform.parent.position;
            directionHint = boneTransform.InverseTransformPoint(parentWorldPos);
            return directionHint.sqrMagnitude > 1.0e-8f;
        }

        private static bool IsLimbBone(Transform boneTransform)
        {
            if (boneTransform == null)
            {
                return false;
            }

            string boneName = boneTransform.name.ToLowerInvariant();
            bool isLegOrArm =
                boneName.Contains("upperleg") ||
                boneName.Contains("lowerleg") ||
                boneName.Contains("thigh") ||
                boneName.Contains("calf") ||
                boneName.Contains("shin") ||
                boneName.Contains("knee") ||
                boneName.Contains("upperarm") ||
                boneName.Contains("lowerarm") ||
                boneName.Contains("forearm") ||
                boneName.Contains("elbow") ||
                boneName.Contains("arm");

            if (!isLegOrArm)
            {
                return false;
            }

            bool excluded =
                boneName.Contains("shoulder") ||
                boneName.Contains("hand") ||
                boneName.Contains("finger") ||
                boneName.Contains("thumb") ||
                boneName.Contains("index") ||
                boneName.Contains("middle") ||
                boneName.Contains("ring") ||
                boneName.Contains("little") ||
                boneName.Contains("toe") ||
                boneName.Contains("foot") ||
                boneName.Contains("hips") ||
                boneName.Contains("pelvis") ||
                boneName.Contains("spine") ||
                boneName.Contains("chest") ||
                boneName.Contains("neck") ||
                boneName.Contains("head");

            return !excluded;
        }

        private static bool IsBodyRole(BoneFitRole role)
        {
            return role == BoneFitRole.Hips ||
                role == BoneFitRole.Spine ||
                role == BoneFitRole.Chest ||
                role == BoneFitRole.UpperChest;
        }

        private static bool TryGetChildDirectionHint(Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (boneTransform == null || boneTransform.childCount == 0)
            {
                return false;
            }

            if (TryGetHumanoidLimbChildDirectionHint(boneTransform, out directionHint))
            {
                return true;
            }

            float bestScore = float.MinValue;
            bool hasValid = false;
            Vector3 parentDirection = boneTransform.parent != null ? boneTransform.localPosition.normalized : Vector3.zero;
            bool hasParentDirection = parentDirection.sqrMagnitude > 1.0e-8f;
            bool isHipsBone = DetectBoneFitRole(boneTransform) == BoneFitRole.Hips;

            if (isHipsBone)
            {
                float bestUpwardY = float.MinValue;
                bool hasUpward = false;

                for (int i = 0; i < boneTransform.childCount; ++i)
                {
                    Vector3 childLocal = boneTransform.GetChild(i).localPosition;

                    if (childLocal.sqrMagnitude <= 1.0e-8f)
                    {
                        continue;
                    }

                    if (childLocal.y > bestUpwardY)
                    {
                        bestUpwardY = childLocal.y;
                        directionHint = childLocal;
                        hasUpward = true;
                    }
                }

                if (hasUpward && directionHint.y > 0.0f)
                {
                    return true;
                }
            }

            for (int i = 0; i < boneTransform.childCount; ++i)
            {
                Transform child = boneTransform.GetChild(i);
                Vector3 childLocal = child.localPosition;

                if (childLocal.sqrMagnitude <= 1.0e-8f)
                {
                    continue;
                }

                float length = childLocal.magnitude;
                float alignment = hasParentDirection ? Vector3.Dot(childLocal.normalized, parentDirection) : 0.0f;
                float score = (length * 0.75f) + (alignment * 0.25f);
                score += GetLimbChildPreferenceScore(boneTransform.name, child.name);

                if (!hasValid || score > bestScore)
                {
                    bestScore = score;
                    directionHint = childLocal;
                    hasValid = true;
                }
            }

            return hasValid;
        }

        private static bool TryGetHumanoidLimbChildDirectionHint(Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            Animator animator = boneTransform.GetComponentInParent<Animator>();

            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return false;
            }

            HumanBodyBones nextBone = HumanBodyBones.LastBone;

            if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg))
            {
                nextBone = HumanBodyBones.LeftLowerLeg;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightUpperLeg))
            {
                nextBone = HumanBodyBones.RightLowerLeg;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg))
            {
                nextBone = HumanBodyBones.LeftFoot;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightLowerLeg))
            {
                nextBone = HumanBodyBones.RightFoot;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftUpperArm))
            {
                nextBone = HumanBodyBones.LeftLowerArm;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightUpperArm))
            {
                nextBone = HumanBodyBones.RightLowerArm;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftLowerArm))
            {
                nextBone = HumanBodyBones.LeftHand;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightLowerArm))
            {
                nextBone = HumanBodyBones.RightHand;
            }

            if (nextBone == HumanBodyBones.LastBone)
            {
                return false;
            }

            Transform childBone = animator.GetBoneTransform(nextBone);

            if (childBone == null)
            {
                return false;
            }

            directionHint = boneTransform.InverseTransformPoint(childBone.position);
            return directionHint.sqrMagnitude > 1.0e-8f;
        }

        private static float GetLimbChildPreferenceScore(string parentBoneName, string childBoneName)
        {
            if (string.IsNullOrEmpty(parentBoneName) || string.IsNullOrEmpty(childBoneName))
            {
                return 0.0f;
            }

            string parent = parentBoneName.ToLowerInvariant();
            string child = childBoneName.ToLowerInvariant();
            bool childLooksLikeTwist = child.Contains("twist") || child.Contains("roll");
            bool childLooksLikeHelper = child.Contains("helper") || child.Contains("dummy") || child.Contains("pole") || child.Contains("ik");

            if (childLooksLikeTwist || childLooksLikeHelper)
            {
                return -0.35f;
            }

            if ((parent.Contains("upperleg") || parent.Contains("thigh")) && (child.Contains("lowerleg") || child.Contains("calf") || child.Contains("knee") || child.Contains("shin")))
            {
                return 0.45f;
            }

            if (parent.Contains("lowerleg") || parent.Contains("calf") || parent.Contains("shin") || parent.Contains("knee"))
            {
                if (child.Contains("foot") || child.Contains("ankle"))
                {
                    return 0.45f;
                }

                if (child.Contains("toe"))
                {
                    return -0.2f;
                }
            }

            if (parent.Contains("upperarm") && (child.Contains("lowerarm") || child.Contains("forearm") || child.Contains("elbow")))
            {
                return 0.45f;
            }

            if ((parent.Contains("lowerarm") || parent.Contains("forearm") || parent.Contains("elbow")) && (child.Contains("hand") || child.Contains("wrist")))
            {
                return 0.45f;
            }

            return 0.0f;
        }
    }
}