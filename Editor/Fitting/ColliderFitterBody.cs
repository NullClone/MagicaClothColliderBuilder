using System.Collections.Generic;
using MagicaCloth2;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderFitter
    {
        public static bool TryFitBody(ColliderGenerationJob job, BoneFitRole boneRole, out FitResult result)
        {
            result = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4 || job.TargetBone == null) return false;

            var boneTransform = job.TargetBone.transform;
            var bodySettings = job.Property.BodyFitProperty;

            var fitMode = ResolveFitMode(job, boneRole);

            if (boneRole == BoneFitRole.Hips && TryFitHips(job, bodySettings, fitMode, out result))
            {
                return true;
            }

            var localAxis = GetBodyUp(job.Animator, boneTransform, boneRole);

            if (localAxis.sqrMagnitude <= 1.0e-8f)
            {
                localAxis = boneTransform.InverseTransformDirection(boneTransform.root != null ? boneTransform.root.up : Vector3.up);
            }

            if (localAxis.sqrMagnitude <= 1.0e-8f)
            {
                localAxis = Vector3.up;
            }

            localAxis.Normalize();
            var localRotation = Quaternion.FromToRotation(Vector3.up, localAxis);
            var inverseRotation = Quaternion.Inverse(localRotation);

            var absYValues = new List<float>(vertices.Length);
            var radialValues = new List<float>(vertices.Length);
            var rotated = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; ++i)
            {
                var rv = inverseRotation * vertices[i];
                rotated[i] = rv;
                absYValues.Add(Mathf.Abs(rv.y));
                radialValues.Add(Mathf.Sqrt((rv.x * rv.x) + (rv.z * rv.z)));
            }

            float halfLength = Percentile(absYValues, bodySettings.GetLengthPercentile(boneRole));
            float length = Mathf.Max(bodySettings.MinLength, halfLength * 2.0f);
            float jointDistance = GetBodyJointDistance(job.Animator, boneTransform, boneRole);

            if (boneRole == BoneFitRole.Hips)
            {
                float maxByBone = jointDistance > 1.0e-6f
                    ? Mathf.Max(bodySettings.MinLength, jointDistance * bodySettings.HipsMaxLengthBySpineDistance)
                    : bodySettings.HipsMaxLength;
                float hipsMaxLength = Mathf.Max(bodySettings.MinLength, Mathf.Min(bodySettings.HipsMaxLength, maxByBone));
                length = Mathf.Min(length, hipsMaxLength);
            }

            float bendSafeMaxLength = GetBendSafeMaxLength(bodySettings, boneRole, jointDistance);

            if (bendSafeMaxLength > 1.0e-6f)
            {
                length = Mathf.Min(length, bendSafeMaxLength);
            }

            float radiusPercentile = bodySettings.GetRadiusPercentile(fitMode);

            if (!TryRadialWeighted(rotated, job.Triangles, radiusPercentile, out float radius))
            {
                radius = Percentile(radialValues, radiusPercentile);
            }

            radius *= bodySettings.GetRadiusScale(boneRole);
            float maxRadius = Mathf.Max(bodySettings.MinRadius, length * bodySettings.MaxRadiusByLengthRatio);
            maxRadius *= bodySettings.GetRadiusCapScale(fitMode);
            maxRadius *= bodySettings.BendSafeRadiusScale;
            radius = Mathf.Clamp(radius, bodySettings.MinRadius, maxRadius);

            result = new FitResult
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


        private static bool TryFitHips(ColliderGenerationJob job, BodyFitProperty settings, FitMode fitMode, out FitResult result)
        {
            result = default;

            if (job.Animator == null || job.TargetBone == null || job.Vertices == null || job.Vertices.Length < 4)
            {
                return false;
            }

            var hipsTransform = job.TargetBone.transform;
            var localHorizontal = GetHipsHorizontal(job.Animator, hipsTransform, settings);
            var localUp = GetHipsUp(job.Animator, hipsTransform);

            if (localUp.sqrMagnitude <= 1.0e-8f)
            {
                localUp = hipsTransform.InverseTransformDirection(hipsTransform.root != null ? hipsTransform.root.up : Vector3.up);
            }

            if (settings.HipsProjectAxisToSpinePlane && localUp.sqrMagnitude > 1.0e-8f)
            {
                localHorizontal = Vector3.ProjectOnPlane(localHorizontal, localUp);
            }

            if (localHorizontal.sqrMagnitude <= 1.0e-8f)
            {
                return false;
            }

            localHorizontal.Normalize();

            var localRotation = Quaternion.FromToRotation(Vector3.up, localHorizontal);
            var inverseRotation = Quaternion.Inverse(localRotation);
            var rotated = new Vector3[job.Vertices.Length];
            var absYValues = new List<float>(job.Vertices.Length);
            var sampleXValues = new List<float>(job.Vertices.Length);
            var sampleYValues = new List<float>(job.Vertices.Length);
            var sampleZValues = new List<float>(job.Vertices.Length);

            Vector3 rotatedUp = inverseRotation * localUp.normalized;
            float spineDistance = GetBodyJointDistance(job.Animator, hipsTransform, BoneFitRole.Hips);
            float legDistance = GetUpperLegDistance(job.Animator, hipsTransform, localHorizontal);
            bool hasVerticalBand = spineDistance > 1.0e-6f && rotatedUp.sqrMagnitude > 1.0e-8f;
            float verticalMin = -spineDistance * settings.HipsLowerSampleBySpineDistance;
            float verticalMax = spineDistance * settings.HipsUpperSampleBySpineDistance;

            for (int i = 0; i < job.Vertices.Length; ++i)
            {
                Vector3 rv = inverseRotation * job.Vertices[i];
                rotated[i] = rv;
                absYValues.Add(Mathf.Abs(rv.y));

                if (hasVerticalBand)
                {
                    float vertical = Vector3.Dot(rv, rotatedUp);

                    if (vertical < verticalMin || vertical > verticalMax)
                    {
                        continue;
                    }
                }

                sampleXValues.Add(rv.x);
                sampleYValues.Add(rv.y);
                sampleZValues.Add(rv.z);
            }

            if (sampleXValues.Count < 4)
            {
                for (int i = 0; i < rotated.Length; ++i)
                {
                    sampleXValues.Add(rotated[i].x);
                    sampleYValues.Add(rotated[i].y);
                    sampleZValues.Add(rotated[i].z);
                }
            }

            float meshLength = Mathf.Max(settings.MinLength, Percentile(absYValues, settings.HipsLengthPercentile) * 2.0f);
            float minLength = settings.MinLength;

            if (legDistance > 1.0e-6f)
            {
                minLength = Mathf.Max(minLength, legDistance * settings.HipsMinLengthByUpperLegDistance);
            }

            if (spineDistance > 1.0e-6f)
            {
                minLength = Mathf.Max(minLength, spineDistance * 0.45f);
                meshLength += spineDistance * settings.HipsLengthPaddingBySpineDistance;
            }

            float maxBySpine = spineDistance > 1.0e-6f
                ? Mathf.Max(settings.MinLength, spineDistance * settings.HipsMaxLengthBySpineDistance)
                : settings.HipsMaxLength;
            float maxLength = Mathf.Max(minLength, Mathf.Min(settings.HipsMaxLength, maxBySpine));
            float length = Mathf.Clamp(Mathf.Max(meshLength, minLength), minLength, maxLength);
            float centerLimit = Mathf.Max(0.0f, settings.HipsCenterLimit);
            float centerX = Mathf.Clamp(Percentile(sampleXValues, 50.0f), -centerLimit, centerLimit);
            float centerY = Mathf.Clamp(Percentile(sampleYValues, 50.0f), length * -0.18f, length * 0.18f);
            float centerZ = Mathf.Clamp(Percentile(sampleZValues, 50.0f), -centerLimit, centerLimit);
            float halfLength = length * 0.5f;
            float radiusPercentile = settings.GetRadiusPercentile(fitMode);
            var radiusValues = new List<float>(sampleXValues.Count);

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (Mathf.Abs(v.y - centerY) > halfLength + settings.BendSafeJointMargin)
                {
                    continue;
                }

                if (hasVerticalBand)
                {
                    float vertical = Vector3.Dot(v, rotatedUp);

                    if (vertical < verticalMin || vertical > verticalMax)
                    {
                        continue;
                    }
                }

                float dx = v.x - centerX;
                float dz = v.z - centerZ;
                radiusValues.Add(Mathf.Sqrt((dx * dx) + (dz * dz)));
            }

            if (radiusValues.Count == 0)
            {
                return false;
            }

            float radius = Percentile(radiusValues, radiusPercentile) * settings.HipsRadiusScale;
            float minRadius = settings.MinRadius;

            if (spineDistance > 1.0e-6f)
            {
                minRadius = Mathf.Max(minRadius, spineDistance * settings.HipsMinRadiusBySpineDistance);
            }

            float maxRadius = Mathf.Max(minRadius, length * settings.HipsMaxRadiusByLengthRatio);
            maxRadius *= settings.GetRadiusCapScale(fitMode);
            radius = Mathf.Clamp(radius, minRadius, maxRadius);

            result = new FitResult
            {
                LocalRotation = localRotation,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = new Vector3(centerX, centerY, centerZ),
                Length = length,
                RadiusAtMin = radius,
                RadiusAtMax = radius,
                ReverseDirection = false,
            };

            return true;
        }

        private static float GetBendSafeMaxLength(BodyFitProperty settings, BoneFitRole role, float jointDistance)
        {
            if (jointDistance <= 1.0e-6f)
            {
                return 0.0f;
            }

            float roleScale = role switch
            {
                BoneFitRole.Hips => 1.10f,
                BoneFitRole.Spine => 1.20f,
                BoneFitRole.Chest => 1.35f,
                BoneFitRole.UpperChest => 1.10f,
                _ => 1.20f,
            };

            float safeLength = (jointDistance * settings.BendSafeLengthScale * roleScale) - (settings.BendSafeJointMargin * 2.0f);

            return Mathf.Max(settings.MinLength, safeLength);
        }

        /*
        private static Vector3 ResolveBendSafeCenter(Vector3[] rotated, float length, float radius, BodyFitProperty settings)
        {
            if (rotated == null || rotated.Length == 0)
            {
                return Vector3.zero;
            }

            float halfLength = length * 0.5f;
            float centerLimit = Mathf.Max(0.0f, Mathf.Min(settings.BendSafeCenterLimit, radius * 0.35f));
            var xValues = new List<float>(rotated.Length);
            var zValues = new List<float>(rotated.Length);

            for (int i = 0; i < rotated.Length; ++i)
            {
                Vector3 v = rotated[i];

                if (Mathf.Abs(v.y) > halfLength + settings.BendSafeJointMargin)
                {
                    continue;
                }

                xValues.Add(v.x);
                zValues.Add(v.z);
            }

            if (xValues.Count == 0)
            {
                return Vector3.zero;
            }

            Vector3 rotatedCenter = new(
                Mathf.Clamp(Percentile(xValues, 50.0f), -centerLimit, centerLimit),
                0.0f,
                Mathf.Clamp(Percentile(zValues, 50.0f), -centerLimit, centerLimit));

            return rotatedCenter;
        }
        */

        private static Vector3 GetHipsUp(Animator animator, Transform hipsTransform)
        {
            if (animator == null || hipsTransform == null)
            {
                return Vector3.zero;
            }

            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);

            if (spine == null) return Vector3.zero;

            Vector3 localSpine = hipsTransform.InverseTransformPoint(spine.position);

            return localSpine.normalized;
        }

        private static Vector3 GetHipsHorizontal(Animator animator, Transform hipsTransform, BodyFitProperty settings)
        {
            if (animator != null && hipsTransform != null)
            {
                var leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

                if (leftUpperLeg != null && rightUpperLeg != null)
                {
                    Vector3 localLeft = hipsTransform.InverseTransformPoint(leftUpperLeg.position);
                    Vector3 localRight = hipsTransform.InverseTransformPoint(rightUpperLeg.position);
                    Vector3 legHorizontal = localRight - localLeft;

                    if (legHorizontal.sqrMagnitude > 1.0e-8f)
                    {
                        Vector3 rootRight = hipsTransform.InverseTransformDirection(hipsTransform.root != null ? hipsTransform.root.right : Vector3.right);

                        if (Vector3.Dot(legHorizontal, rootRight) < 0.0f)
                        {
                            legHorizontal = -legHorizontal;
                        }

                        return legHorizontal;
                    }
                }
            }

            var worldHorizontal = settings.HorizontalAxis == BodyHorizontalAxis.RootForward
                ? (hipsTransform.root != null ? hipsTransform.root.forward : Vector3.forward)
                : (hipsTransform.root != null ? hipsTransform.root.right : Vector3.right);

            return hipsTransform.InverseTransformDirection(worldHorizontal);
        }

        private static float GetUpperLegDistance(Animator animator, Transform hipsTransform, Vector3 localHorizontal)
        {
            if (animator == null || hipsTransform == null || localHorizontal.sqrMagnitude <= 1.0e-8f)
            {
                return 0.0f;
            }

            var leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

            if (leftUpperLeg == null || rightUpperLeg == null)
            {
                return 0.0f;
            }

            Vector3 localLeft = hipsTransform.InverseTransformPoint(leftUpperLeg.position);
            Vector3 localRight = hipsTransform.InverseTransformPoint(rightUpperLeg.position);

            return Mathf.Abs(Vector3.Dot(localRight - localLeft, localHorizontal.normalized));
        }

        private static Vector3 GetBodyUp(Animator animator, Transform bodyTransform, BoneFitRole role)
        {
            if (role == BoneFitRole.Hips)
            {
                return GetHipsUp(animator, bodyTransform);
            }

            if (animator == null || bodyTransform == null)
            {
                return Vector3.zero;
            }

            Transform upTarget = null;

            if (role == BoneFitRole.Spine)
            {
                upTarget = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            else if (role == BoneFitRole.Chest)
            {
                upTarget = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            }
            else if (role == BoneFitRole.UpperChest)
            {
                upTarget = animator.GetBoneTransform(HumanBodyBones.Neck);
            }

            if (upTarget != null)
            {
                Vector3 localUp = bodyTransform.InverseTransformPoint(upTarget.position);

                if (localUp.sqrMagnitude > 1.0e-8f)
                {
                    return localUp.normalized;
                }
            }

            return bodyTransform.InverseTransformDirection(bodyTransform.root != null ? bodyTransform.root.up : Vector3.up).normalized;
        }

        private static float GetBodyJointDistance(Animator animator, Transform bodyTransform, BoneFitRole role)
        {
            if (animator == null || bodyTransform == null)
            {
                return 0.0f;
            }

            Transform target = null;

            if (role == BoneFitRole.Hips)
            {
                target = animator.GetBoneTransform(HumanBodyBones.Spine);
            }
            else if (role == BoneFitRole.Spine)
            {
                target = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            else if (role == BoneFitRole.Chest)
            {
                target = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            }
            else if (role == BoneFitRole.UpperChest)
            {
                target = animator.GetBoneTransform(HumanBodyBones.Neck);
            }

            if (target == null)
            {
                return 0.0f;
            }

            return bodyTransform.InverseTransformPoint(target.position).magnitude;
        }
    }
}
