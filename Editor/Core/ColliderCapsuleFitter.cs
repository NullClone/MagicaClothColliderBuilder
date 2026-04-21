using MagicaCloth2;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderCapsuleFitter
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

            bool hasChildHint = TryChildHint(job.Animator, job.TargetBone.transform, out Vector3 childHint);
            bool hasParentHint = TryParentHint(job.TargetBone.transform, out Vector3 parentHint);
            bool hasHumanoidLimbHint = TryHumanoidHint(job.Animator, job.TargetBone.transform, out Vector3 humanoidLimbHint);

            var boneRole = DetectBoneFitRole(job.TargetBone.transform);

            if (boneRole == BoneFitRole.Head && TryFitHead(job, out fitResult))
            {
                return true;
            }

            if (IsBodyRole(boneRole) && TryFitBody(job, boneRole, out fitResult))
            {
                return true;
            }

            if (job.Property.LimbFitProperty.ForceFixedAxisByHumanoid && hasHumanoidLimbHint && TryFitLimb(job, humanoidLimbHint, boneRole, ref fitResult))
            {
                return true;
            }

            if (IsLimbBone(job.Animator, job.TargetBone.transform) && hasChildHint && TryFitLimb(job, childHint, boneRole, ref fitResult))
            {
                return true;
            }

            return TryFitAuto(job, vertices, boneRole, hasChildHint, childHint, hasParentHint, parentHint, out fitResult);
        }

        public static BoneFitRole DetectBoneFitRole(Transform boneTransform)
        {
            if (boneTransform == null)
            {
                return BoneFitRole.Default;
            }

            var animator = boneTransform.GetComponentInParent<Animator>();

            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                if (boneTransform == animator.GetBoneTransform(HumanBodyBones.Hips))
                {
                    return BoneFitRole.Hips;
                }

                if (boneTransform == animator.GetBoneTransform(HumanBodyBones.Spine))
                {
                    return BoneFitRole.Spine;
                }

                if (boneTransform == animator.GetBoneTransform(HumanBodyBones.Chest))
                {
                    return BoneFitRole.Chest;
                }

                if (boneTransform == animator.GetBoneTransform(HumanBodyBones.UpperChest))
                {
                    return BoneFitRole.UpperChest;
                }

                if (boneTransform == animator.GetBoneTransform(HumanBodyBones.Neck))
                {
                    return BoneFitRole.Neck;
                }

                if (boneTransform == animator.GetBoneTransform(HumanBodyBones.Head))
                {
                    return BoneFitRole.Head;
                }
            }

            var boneName = boneTransform.name.ToLowerInvariant();

            if (boneName.Contains("head"))
            {
                return BoneFitRole.Head;
            }

            if (boneName.Contains("neck"))
            {
                return BoneFitRole.Neck;
            }

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
    }
}