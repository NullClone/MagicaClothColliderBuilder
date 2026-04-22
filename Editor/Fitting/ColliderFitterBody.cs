using MagicaCloth2;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderFitter
    {
        internal static bool TryFitBody(ColliderGenerationJob job, BoneFitRole boneRole, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4 || job.TargetBone == null) return false;

            var boneTransform = job.TargetBone.transform;
            var bodySettings = job.Property.BodyFitProperty;
            FitMode fitMode = ResolveFitMode(job, boneRole);
            var worldHorizontal = bodySettings.HorizontalAxis == BodyHorizontalAxis.RootForward
                ? (boneTransform.root != null ? boneTransform.root.forward : Vector3.forward)
                : (boneTransform.root != null ? boneTransform.root.right : Vector3.right);

            var localHorizontal = boneTransform.InverseTransformDirection(worldHorizontal);

            if (bodySettings.ProjectAxisToBodyUpPlane)
            {
                var bodyUp = GetBodyUp(job.Animator, boneTransform, boneRole);

                if (bodyUp.sqrMagnitude > 1.0e-8f)
                {
                    localHorizontal = Vector3.ProjectOnPlane(localHorizontal, bodyUp);
                }
            }

            if (boneRole == BoneFitRole.Hips && bodySettings.HipsProjectAxisToSpinePlane)
            {
                var localSpineUp = GetHipsUp(job.Animator, boneTransform);

                if (localSpineUp.sqrMagnitude > 1.0e-8f)
                {
                    localHorizontal = Vector3.ProjectOnPlane(localHorizontal, localSpineUp);
                }
            }

            if (localHorizontal.sqrMagnitude <= 1.0e-8f)
            {
                localHorizontal = Vector3.right;
            }

            localHorizontal.Normalize();
            var localRotation = Quaternion.FromToRotation(Vector3.up, localHorizontal);
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
            Vector3 center = ResolveBendSafeCenter(rotated, length, radius, bodySettings);

            fitResult = new CapsuleFitResult
            {
                LocalRotation = localRotation,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = center,
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

            Vector3 rotatedCenter = new Vector3(
                Mathf.Clamp(Percentile(xValues, 50.0f), -centerLimit, centerLimit),
                0.0f,
                Mathf.Clamp(Percentile(zValues, 50.0f), -centerLimit, centerLimit));

            return rotatedCenter;
        }


        private static Vector3 GetHipsUp(Animator animator, Transform hipsTransform)
        {
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);

            if (spine == null) return Vector3.zero;

            Vector3 localSpine = hipsTransform.InverseTransformPoint(spine.position);

            return localSpine.normalized;
        }

        private static Vector3 GetBodyUp(Animator animator, Transform bodyTransform, BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return GetHipsUp(animator, bodyTransform);
            }

            Transform upTarget = null;

            if (boneRole == BoneFitRole.Spine)
            {
                upTarget = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.UpperChest);
            }
            else if (boneRole == BoneFitRole.Chest)
            {
                upTarget = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Neck);
            }
            else if (boneRole == BoneFitRole.UpperChest)
            {
                upTarget = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Head);
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
                target = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.UpperChest);
            }
            else if (role == BoneFitRole.Chest)
            {
                target = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Neck);
            }
            else if (role == BoneFitRole.UpperChest)
            {
                target = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (target == null)
            {
                return 0.0f;
            }

            return bodyTransform.InverseTransformPoint(target.position).magnitude;
        }
    }
}
