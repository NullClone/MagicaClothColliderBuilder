using System.Collections.Generic;
using MagicaCloth2;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderFitter
    {
        public static bool TryFitFoot(ColliderGenerationJob job, ref FitResult fitResult)
        {
            if (job == null ||
                job.TargetBone == null ||
                job.Property == null ||
                job.Vertices == null ||
                job.Vertices.Length < 4 ||
                !TryFootHint(job.Animator, job.TargetBone.transform, out Vector3 footAxis, out float footLength))
            {
                return false;
            }

            footLength = Mathf.Max(footLength, job.Property.LimbFitProperty.MinJointDistance);
            var footSettings = job.Property.FootFitProperty;

            Vector3 axis = ResolveFootAxis(job, footAxis);
            Quaternion footRotation = Quaternion.FromToRotation(Vector3.up, axis);
            Quaternion inverseRotation = Quaternion.Inverse(footRotation);

            var rotated = new Vector3[job.Vertices.Length];
            var yValues = new List<float>(job.Vertices.Length);

            for (int i = 0; i < job.Vertices.Length; ++i)
            {
                var rv = inverseRotation * job.Vertices[i];

                rotated[i] = rv;
                yValues.Add(rv.y);
            }

            var fitMode = ResolveFitMode(job, BoneFitRole.Default);
            float lower = fitMode == FitMode.Inner ? 3.0f : 1.0f;
            float upper = fitMode == FitMode.Outer ? 100.0f : 99.0f;
            float minY = Percentile(yValues, lower);
            float maxY = Percentile(yValues, upper);

            if (maxY <= minY) return false;

            float meshFootLength = Mathf.Max(maxY - minY, footLength, job.Property.LimbFitProperty.MinJointDistance);
            float toeLimitY = Mathf.Max(0.0f, (inverseRotation * footAxis).y);
            float toeStopMargin = Mathf.Min(
                Mathf.Max(meshFootLength * footSettings.FootToeStopMarginScale, footSettings.FootToeStopMarginMin),
                footSettings.FootToeStopMarginMax);
            float toeLimitedMaxY = toeLimitY + toeStopMargin;

            if (toeLimitedMaxY > minY)
            {
                maxY = Mathf.Min(maxY, toeLimitedMaxY);
            }

            if (maxY <= minY)
            {
                return false;
            }

            float heelMargin = Mathf.Max(meshFootLength * footSettings.FootHeelMarginScale, footSettings.FootHeelMarginMin);
            minY -= heelMargin;

            float length = Mathf.Max(job.Property.GenericFitProperty.MinLength, maxY - minY);
            float sampleMargin = Mathf.Max(length * footSettings.FootSampleMarginScale, footSettings.FootSampleMarginMin);
            float forwardSampleMargin = Mathf.Min(
                sampleMargin,
                Mathf.Max(meshFootLength * footSettings.FootForwardSampleMarginScale, footSettings.FootForwardSampleMarginMin));
            float sampleMinY = minY - sampleMargin;
            float sampleMaxY = maxY + forwardSampleMargin;
            var xValues = new List<float>();
            var zValues = new List<float>();

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (v.y < sampleMinY || v.y > sampleMaxY)
                {
                    continue;
                }

                xValues.Add(v.x);
                zValues.Add(v.z);
            }

            if (xValues.Count == 0)
            {
                return false;
            }

            float centerX = Percentile(xValues, 50.0f);
            float centerZ = Percentile(zValues, 50.0f);
            float centerY = (minY + maxY) * 0.5f;
            float radiusPercentile = footSettings.GetFootRadiusPercentile(fitMode);
            var radialValues = new List<float>(xValues.Count);

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (v.y < sampleMinY || v.y > sampleMaxY) continue;

                float dx = v.x - centerX;
                float dz = v.z - centerZ;

                radialValues.Add(Mathf.Sqrt((dx * dx) + (dz * dz)));
            }

            if (radialValues.Count == 0)
            {
                return false;
            }

            float radius = Percentile(radialValues, radiusPercentile) *
                           job.Property.LimbFitProperty.RadiusScale *
                           footSettings.GetFootRadiusScale(fitMode);
            float minRadius = Mathf.Max(job.Property.GenericFitProperty.MinRadius, footSettings.FootMinRadius);
            float maxRadius = Mathf.Max(
                minRadius,
                Mathf.Min(length * footSettings.FootMaxRadiusByLength, meshFootLength * footSettings.FootMaxRadiusByMeshLength));
            radius = Mathf.Clamp(radius, minRadius, maxRadius);

            fitResult.LocalRotation = footRotation;
            fitResult.Direction = MagicaCapsuleCollider.Direction.Y;
            fitResult.Center = new Vector3(centerX, centerY, centerZ);
            fitResult.Length = length;
            fitResult.RadiusAtMin = radius;
            fitResult.RadiusAtMax = radius;
            fitResult.ReverseDirection = false;

            return true;
        }

        public static bool TryFitToe(ColliderGenerationJob job, ref FitResult fitResult)
        {
            if (job == null ||
                job.TargetBone == null ||
                job.Property == null ||
                job.Vertices == null ||
                job.Vertices.Length < 4)
            {
                return false;
            }

            var footSettings = job.Property.FootFitProperty;

            if (!TryGetToeAxisAlignedToFoot(job.Animator, job.TargetBone.transform, footSettings.AxisFlatten, out Vector3 axis, out float parentToeDistance))
            {
                axis = EstimateFootForwardFromRoot(job.Animator, job.TargetBone.transform);
                parentToeDistance = job.TargetBone.transform?.parent != null ? job.TargetBone.transform.localPosition.magnitude : 0f;
            }

            Quaternion toeRotation = Quaternion.FromToRotation(Vector3.up, axis);
            Quaternion inverseRotation = Quaternion.Inverse(toeRotation);

            var rotated = new Vector3[job.Vertices.Length];
            var yValues = new List<float>(job.Vertices.Length);

            for (int i = 0; i < job.Vertices.Length; ++i)
            {
                var rv = inverseRotation * job.Vertices[i];
                rotated[i] = rv;
                yValues.Add(rv.y);
            }

            FitMode fitMode = ResolveFitMode(job, BoneFitRole.Default);
            float lower = fitMode == FitMode.Inner ? 2.0f : 0.0f;
            float upper = fitMode == FitMode.Outer ? 100.0f : 99.0f;
            float observedMinY = Percentile(yValues, lower);
            float maxY = Mathf.Max(0.0f, Percentile(yValues, upper));
            float maxBackOverlap = Mathf.Max(parentToeDistance * footSettings.ToeBackOverlapScale, footSettings.ToeBackOverlapMin);
            float minY = Mathf.Clamp(observedMinY, -maxBackOverlap, 0.0f);
            float meshToeLength = Mathf.Max(maxY - minY, 0.0f);
            float minToeLength = Mathf.Max(
                job.Property.GenericFitProperty.MinLength * footSettings.ToeGenericMinLengthScale,
                Mathf.Max(parentToeDistance * footSettings.ToeMinLengthByFootToToe, footSettings.ToeMinLength));

            if (meshToeLength < minToeLength)
            {
                maxY = Mathf.Max(maxY, minY + minToeLength);
                meshToeLength = maxY - minY;
            }

            float toeMargin = Mathf.Max(meshToeLength * footSettings.ToeTipMarginScale, footSettings.ToeTipMarginMin);
            float baseMargin = Mathf.Min(
                Mathf.Max(parentToeDistance * footSettings.ToeBaseMarginScale, footSettings.ToeBaseMarginMin),
                footSettings.ToeBaseMarginMax);

            minY = Mathf.Min(minY, 0.0f) - baseMargin;
            maxY += toeMargin;

            float length = Mathf.Max(job.Property.GenericFitProperty.MinLength, maxY - minY);
            float sampleMargin = Mathf.Max(length * footSettings.ToeSampleMarginScale, footSettings.ToeSampleMarginMin);
            float sampleMinY = minY - sampleMargin;
            float sampleMaxY = maxY + sampleMargin;
            var xValues = new List<float>();
            var zValues = new List<float>();

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (v.y < sampleMinY || v.y > sampleMaxY) continue;

                xValues.Add(v.x);
                zValues.Add(v.z);
            }

            if (xValues.Count == 0)
            {
                return false;
            }

            float centerX = Percentile(xValues, 50.0f);
            float centerZ = Percentile(zValues, 50.0f);
            float centerY = (minY + maxY) * 0.5f;
            float radiusPercentile = footSettings.GetToeRadiusPercentile(fitMode);
            var radialValues = new List<float>(xValues.Count);

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (v.y < sampleMinY || v.y > sampleMaxY) continue;

                float dx = v.x - centerX;
                float dz = v.z - centerZ;

                radialValues.Add(Mathf.Sqrt((dx * dx) + (dz * dz)));
            }

            if (radialValues.Count == 0)
            {
                return false;
            }

            float radius = Percentile(radialValues, radiusPercentile) *
                           job.Property.LimbFitProperty.RadiusScale *
                           footSettings.GetToeRadiusScale(fitMode);
            float minRadius = Mathf.Max(
                job.Property.GenericFitProperty.MinRadius,
                Mathf.Max(length * footSettings.ToeMinRadiusByLength, parentToeDistance * footSettings.ToeMinRadiusByFootToToe),
                footSettings.ToeMinRadius);
            float maxRadius = Mathf.Max(minRadius, length * footSettings.ToeMaxRadiusByLength);
            radius = Mathf.Clamp(radius, minRadius, maxRadius);

            fitResult.LocalRotation = toeRotation;
            fitResult.Direction = MagicaCapsuleCollider.Direction.Y;
            fitResult.Center = new Vector3(centerX, centerY, centerZ);
            fitResult.Length = length;
            fitResult.RadiusAtMin = radius;
            fitResult.RadiusAtMax = radius;
            fitResult.ReverseDirection = false;

            return true;
        }

        public static bool TryCreateToeFallbackFit(Animator animator, Transform toeTransform, SABoneColliderProperty property, out FitResult fitResult)
        {
            fitResult = default;

            if (toeTransform == null || property == null || !IsHumanoidToeBone(animator, toeTransform))
            {
                return false;
            }

            if (!TryGetToeAxisAlignedToFoot(animator, toeTransform, property.FootFitProperty.AxisFlatten, out Vector3 axis, out float footToToeDistance))
            {
                axis = EstimateFootForwardFromRoot(animator, toeTransform);
                footToToeDistance = toeTransform.parent != null ? toeTransform.localPosition.magnitude : 0.05f;
            }

            var footSettings = property.FootFitProperty;
            float length = Mathf.Clamp(
                footToToeDistance * footSettings.ToeFallbackLengthByFootToToe,
                footSettings.ToeFallbackMinLength,
                footSettings.ToeFallbackMaxLength);
            float radius = Mathf.Clamp(
                length * footSettings.ToeFallbackRadiusByLength,
                footSettings.ToeFallbackMinRadius,
                footSettings.ToeFallbackMaxRadius);

            fitResult = new FitResult
            {
                LocalRotation = Quaternion.FromToRotation(Vector3.up, axis),
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = new Vector3(0.0f, length * 0.5f, 0.0f),
                Length = length,
                RadiusAtMin = radius,
                RadiusAtMax = radius,
                ReverseDirection = false,
            };

            return true;
        }


        private static Vector3 ResolveFootAxis(ColliderGenerationJob job, Vector3 footAxis)
        {
            Vector3 preferredAxis = footAxis.sqrMagnitude > 1.0e-8f ? footAxis.normalized : Vector3.forward;
            Vector3 rootForward = EstimateFootForwardFromRoot(job.Animator, job.TargetBone.transform);
            Vector3 avatarUp = Vector3.up;

            if (job.Animator != null)
            {
                avatarUp = job.TargetBone.transform.InverseTransformDirection(job.Animator.transform.up);
            }
            else if (job.TargetBone.transform.root != null)
            {
                avatarUp = job.TargetBone.transform.InverseTransformDirection(job.TargetBone.transform.root.up);
            }

            if (avatarUp.sqrMagnitude <= 1.0e-8f)
            {
                avatarUp = Vector3.up;
            }

            avatarUp.Normalize();

            Vector3 flatPreferred = Vector3.ProjectOnPlane(preferredAxis, avatarUp);

            if (flatPreferred.sqrMagnitude <= 1.0e-8f)
            {
                flatPreferred = preferredAxis;
            }

            flatPreferred.Normalize();

            Vector3 blendedPreferred = Vector3.Slerp(preferredAxis, flatPreferred, job.Property.FootFitProperty.AxisFlatten).normalized;

            var candidates = new List<Vector3>
            {
                blendedPreferred,
                flatPreferred,
                preferredAxis,
                rootForward,
            };

            Vector3 principal = GetPrincipalAxis(job.Vertices);

            if (principal.sqrMagnitude > 1.0e-8f)
            {
                principal.Normalize();

                if (Vector3.Dot(principal, preferredAxis) < 0.0f)
                {
                    principal = -principal;
                }

                Vector3 flatPrincipal = Vector3.ProjectOnPlane(principal, avatarUp);

                if (flatPrincipal.sqrMagnitude > 1.0e-8f)
                {
                    candidates.Add(flatPrincipal.normalized);
                    candidates.Add(Vector3.Slerp(principal, flatPrincipal.normalized, job.Property.FootFitProperty.AxisFlatten).normalized);
                }

                candidates.Add(principal);
            }

            Vector3 bestAxis = blendedPreferred;

            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Count; ++i)
            {
                Vector3 candidate = candidates[i];

                if (candidate.sqrMagnitude <= 1.0e-8f)
                {
                    continue;
                }

                candidate.Normalize();

                if (Vector3.Dot(candidate, preferredAxis) < 0.0f)
                {
                    candidate = -candidate;
                }

                float span = MeasureAxisSpan(job.Vertices, candidate);
                float alignment = Mathf.Max(
                    Mathf.Max(0.0f, Vector3.Dot(candidate, preferredAxis)),
                    Mathf.Max(0.0f, Vector3.Dot(candidate, rootForward)));
                float flatness = 1.0f - Mathf.Abs(Vector3.Dot(candidate, avatarUp));
                float score = span * (1.0f + (alignment * 0.12f) + (flatness * 0.10f));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAxis = candidate;
                }
            }

            return bestAxis.normalized;
        }

        private static float MeasureAxisSpan(Vector3[] vertices, Vector3 axis)
        {
            if (vertices == null || vertices.Length == 0 || axis.sqrMagnitude <= 1.0e-8f)
            {
                return 0.0f;
            }

            axis.Normalize();

            var values = new List<float>(vertices.Length);

            for (int i = 0; i < vertices.Length; ++i)
            {
                values.Add(Vector3.Dot(vertices[i], axis));
            }

            return Percentile(values, 99.0f) - Percentile(values, 1.0f);
        }

        private static bool TryGetToeAxisAlignedToFoot(Animator animator, Transform toeTransform, float axisFlatten, out Vector3 toeAxis, out float footToToeDistance)
        {
            toeAxis = Vector3.zero;
            footToToeDistance = 0.0f;

            if (animator == null || toeTransform == null)
            {
                return false;
            }

            Transform footTransform = null;

            if (toeTransform == animator.GetBoneTransform(HumanBodyBones.LeftToes))
            {
                footTransform = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            }
            else if (toeTransform == animator.GetBoneTransform(HumanBodyBones.RightToes))
            {
                footTransform = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            if (footTransform == null)
            {
                return false;
            }

            Vector3 worldAxis = toeTransform.position - footTransform.position;
            footToToeDistance = worldAxis.magnitude;

            if (worldAxis.sqrMagnitude <= 1.0e-8f)
            {
                worldAxis = footTransform.TransformDirection(EstimateFootForwardFromRoot(animator, footTransform));
            }

            if (worldAxis.sqrMagnitude <= 1.0e-8f)
            {
                return false;
            }

            Vector3 avatarUp = animator.transform.up;

            if (avatarUp.sqrMagnitude <= 1.0e-8f)
            {
                avatarUp = footTransform.root != null ? footTransform.root.up : Vector3.up;
            }

            avatarUp.Normalize();

            Vector3 flatWorldAxis = Vector3.ProjectOnPlane(worldAxis, avatarUp);

            if (flatWorldAxis.sqrMagnitude > 1.0e-8f)
            {
                worldAxis = Vector3.Slerp(worldAxis.normalized, flatWorldAxis.normalized, axisFlatten);
            }

            toeAxis = toeTransform.InverseTransformDirection(worldAxis.normalized);

            if (toeAxis.sqrMagnitude <= 1.0e-8f)
            {
                return false;
            }

            toeAxis.Normalize();

            return true;
        }
    }
}
