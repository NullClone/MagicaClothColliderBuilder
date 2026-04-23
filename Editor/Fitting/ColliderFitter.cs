using MagicaCloth2;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderFitter
    {
        // Fields

        private delegate bool FitAttempt(FitContext context, ref FitResult result);

        private static readonly FitAttempt[] FitAttempts =
        {
            TryFitPalmRole,
            TryFitHeadRole,
            TryFitToeRole,
            TryFitFootRole,
            TryFitBodyRole,
            TryFitFingerRole,
            TryFitHumanoidLimbRole,
            TryFitNamedLimbRole,
        };


        // Methods

        public static bool TryFit(ColliderGenerationJob job, out FitResult result)
        {
            result = CreateDefaultFitResult();

            if (!TryCreateFitContext(job, out FitContext context))
            {
                return false;
            }

            foreach (var attempt in FitAttempts)
            {
                if (attempt(context, ref result))
                {
                    return true;
                }
            }

            return TryFitAuto(
                context.Job,
                context.Vertices,
                context.BoneRole,
                context.HasChildHint,
                context.ChildHint,
                context.HasParentHint,
                context.ParentHint,
                out result);
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


        private static FitResult CreateDefaultFitResult()
        {
            return new FitResult
            {
                LocalRotation = Quaternion.identity,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = Vector3.zero,
                Length = 0.02f,
                RadiusAtMin = 0.01f,
                RadiusAtMax = 0.01f,
                ReverseDirection = false,
            };
        }

        private static bool TryCreateFitContext(ColliderGenerationJob job, out FitContext context)
        {
            context = default;

            if (job == null || job.TargetBone == null) return false;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4) return false;

            var targetTransform = job.TargetBone.transform;

            bool hasChildHint = TryChildHint(job.Animator, targetTransform, out Vector3 childHint);
            bool hasParentHint = TryParentHint(targetTransform, out Vector3 parentHint);
            bool hasHumanoidLimbHint = TryHumanoidHint(job.Animator, targetTransform, out Vector3 humanoidLimbHint);

            context = new FitContext(
                job,
                targetTransform,
                vertices,
                DetectBoneFitRole(targetTransform),
                hasChildHint,
                childHint,
                hasParentHint,
                parentHint,
                hasHumanoidLimbHint,
                humanoidLimbHint);

            return true;
        }

        private static bool TryFitPalmRole(FitContext context, ref FitResult fitResult)
        {
            return context.Job.Property.GenerationProperty.IncludeFingers &&
                   IsHumanoidHandBone(context.Job.Animator, context.TargetTransform) &&
                   TryFitPalm(context.Job, ref fitResult);
        }

        private static bool TryFitHeadRole(FitContext context, ref FitResult fitResult)
        {
            return context.BoneRole == BoneFitRole.Head &&
                   TryFitHead(context.Job, out fitResult);
        }

        private static bool TryFitToeRole(FitContext context, ref FitResult fitResult)
        {
            return IsHumanoidToeBone(context.Job.Animator, context.TargetTransform) &&
                   TryFitToe(context.Job, ref fitResult);
        }

        private static bool TryFitFootRole(FitContext context, ref FitResult fitResult)
        {
            return IsHumanoidFootBone(context.Job.Animator, context.TargetTransform) &&
                   !IsHumanoidToeBone(context.Job.Animator, context.TargetTransform) &&
                   TryFitFoot(context.Job, ref fitResult);
        }

        private static bool TryFitBodyRole(FitContext context, ref FitResult fitResult)
        {
            return IsBodyRole(context.BoneRole) &&
                   TryFitBody(context.Job, context.BoneRole, out fitResult);
        }

        private static bool TryFitFingerRole(FitContext context, ref FitResult fitResult)
        {
            return IsHumanoidFingerBone(context.Job.Animator, context.TargetTransform) &&
                   context.HasHumanoidLimbHint &&
                   TryFitFinger(context.Job, context.HumanoidLimbHint, context.BoneRole, ref fitResult);
        }

        private static bool TryFitHumanoidLimbRole(FitContext context, ref FitResult fitResult)
        {
            return context.Job.Property.LimbFitProperty.ForceFixedAxisByHumanoid &&
                   context.HasHumanoidLimbHint &&
                   TryFitLimb(context.Job, context.HumanoidLimbHint, context.BoneRole, ref fitResult);
        }

        private static bool TryFitNamedLimbRole(FitContext context, ref FitResult fitResult)
        {
            return IsLimbBone(context.Job.Animator, context.TargetTransform) &&
                   context.HasChildHint &&
                   TryFitLimb(context.Job, context.ChildHint, context.BoneRole, ref fitResult);
        }


        // Structs

        private readonly struct FitContext
        {
            public readonly ColliderGenerationJob Job;
            public readonly Transform TargetTransform;
            public readonly Vector3[] Vertices;
            public readonly BoneFitRole BoneRole;
            public readonly bool HasChildHint;
            public readonly Vector3 ChildHint;
            public readonly bool HasParentHint;
            public readonly Vector3 ParentHint;
            public readonly bool HasHumanoidLimbHint;
            public readonly Vector3 HumanoidLimbHint;

            public FitContext(
                ColliderGenerationJob job,
                Transform targetTransform,
                Vector3[] vertices,
                BoneFitRole boneRole,
                bool hasChildHint,
                Vector3 childHint,
                bool hasParentHint,
                Vector3 parentHint,
                bool hasHumanoidLimbHint,
                Vector3 humanoidLimbHint)
            {
                Job = job;
                TargetTransform = targetTransform;
                Vertices = vertices;
                BoneRole = boneRole;
                HasChildHint = hasChildHint;
                ChildHint = childHint;
                HasParentHint = hasParentHint;
                ParentHint = parentHint;
                HasHumanoidLimbHint = hasHumanoidLimbHint;
                HumanoidLimbHint = humanoidLimbHint;
            }
        }
    }

    public struct FitResult
    {
        public Quaternion LocalRotation;
        public MagicaCapsuleCollider.Direction Direction;
        public Vector3 Center;
        public float Length;
        public float RadiusAtMin;
        public float RadiusAtMax;
        public bool ReverseDirection;
    }
}
