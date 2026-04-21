using MagicaCloth2;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderCapsuleFitter
    {
        internal static bool TryFitBody(ColliderGenerationJob job, BoneFitRole boneRole, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4 || job.TargetBone == null) return false;

            var boneTransform = job.TargetBone.transform;
            var bodySettings = job.Property.BodyFitProperty;
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

            if (boneRole == BoneFitRole.Hips)
            {
                float hipsSpineDistance = GetHipsSpineLen(job.Animator, boneTransform);
                float maxByBone = hipsSpineDistance > 1.0e-6f
                    ? Mathf.Max(bodySettings.MinLength, hipsSpineDistance * bodySettings.HipsMaxLengthBySpineDistance)
                    : bodySettings.HipsMaxLength;
                float hipsMaxLength = Mathf.Max(bodySettings.MinLength, Mathf.Min(bodySettings.HipsMaxLength, maxByBone));
                length = Mathf.Min(length, hipsMaxLength);
            }

            if (!TryRadialWeighted(rotated, job.Triangles, bodySettings.RadiusPercentile, out float radius))
            {
                radius = Percentile(radialValues, bodySettings.RadiusPercentile);
            }

            radius *= bodySettings.GetRadiusScale(boneRole);
            radius = Mathf.Clamp(radius, bodySettings.MinRadius, Mathf.Max(bodySettings.MinRadius, length * bodySettings.MaxRadiusByLengthRatio));

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

        private static float GetHipsSpineLen(Animator animator, Transform hipsTransform)
        {
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);

            if (spine == null) return 0f;

            Vector3 localSpine = hipsTransform.InverseTransformPoint(spine.position);

            return localSpine.magnitude;
        }
    }
}
