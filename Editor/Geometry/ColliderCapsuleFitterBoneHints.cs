using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderCapsuleFitter
    {
        private static bool TryParentHint(Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (boneTransform == null || boneTransform.parent == null)
            {
                return false;
            }

            var parentWorldPos = boneTransform.parent.position;

            directionHint = boneTransform.InverseTransformPoint(parentWorldPos);

            return directionHint.sqrMagnitude > 1.0e-8f;
        }

        private static bool IsLimbBone(Animator animator, Transform boneTransform)
        {
            if (boneTransform == null) return false;

            if (TryHumanoidHint(animator, boneTransform, out _))
            {
                return true;
            }

            if (IsHumanoidFingerBone(animator, boneTransform))
            {
                return true;
            }

            string boneName = boneTransform.name.ToLowerInvariant();

            if (boneName.Contains("upperleg") ||
                boneName.Contains("lowerleg") ||
                boneName.Contains("thigh") ||
                boneName.Contains("calf") ||
                boneName.Contains("shin") ||
                boneName.Contains("knee") ||
                boneName.Contains("upperarm") ||
                boneName.Contains("lowerarm") ||
                boneName.Contains("forearm") ||
                boneName.Contains("elbow") ||
                boneName.Contains("arm"))
            {
                if (boneName.Contains("shoulder") ||
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
                    boneName.Contains("head"))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else return false;
        }

        private static bool IsBodyRole(BoneFitRole role)
        {
            return
                role == BoneFitRole.Hips ||
                role == BoneFitRole.Spine ||
                role == BoneFitRole.Chest ||
                role == BoneFitRole.UpperChest;
        }

        private static FitMode ResolveFitMode(ColliderGenerationJob job, BoneFitRole role)
        {
            if (job == null || job.Property == null)
            {
                return FitMode.Balanced;
            }

            return ResolveFitMode(job.Property.GenerationProperty, job.Animator, job.TargetBone != null ? job.TargetBone.transform : null, role);
        }

        private static FitMode ResolveFitMode(GenerationProperty settings, Animator animator, Transform boneTransform, BoneFitRole role)
        {
            if (settings == null)
            {
                return FitMode.Balanced;
            }

            if (IsHumanoidFingerBone(animator, boneTransform))
            {
                return settings.FingerFitMode;
            }

            if (IsHumanoidHandBone(animator, boneTransform))
            {
                return settings.FingerFitMode;
            }

            if (IsHumanoidArmBone(animator, boneTransform))
            {
                return settings.ArmFitMode;
            }

            if (IsHumanoidLegBone(animator, boneTransform))
            {
                return settings.LegFitMode;
            }

            if (IsHumanoidToeBone(animator, boneTransform))
            {
                return settings.ToeFitMode;
            }

            if (role == BoneFitRole.Head || role == BoneFitRole.Neck)
            {
                return settings.HeadFitMode;
            }

            if (IsBodyRole(role))
            {
                return settings.BodyFitMode;
            }

            return settings.DefaultFitMode;
        }

        private static bool TryChildHint(Animator animator, Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (boneTransform == null || boneTransform.childCount == 0)
            {
                return false;
            }

            if (TryHumanoidHint(animator, boneTransform, out directionHint))
            {
                return true;
            }

            float bestScore = float.MinValue;
            bool hasValid = false;
            var parentDirection = boneTransform.parent != null ? boneTransform.localPosition.normalized : Vector3.zero;
            bool hasParentDirection = parentDirection.sqrMagnitude > 1.0e-8f;
            bool isHipsBone = DetectBoneFitRole(boneTransform) == BoneFitRole.Hips;

            if (isHipsBone)
            {
                var bestUpwardY = float.MinValue;
                var hasUpward = false;

                for (int i = 0; i < boneTransform.childCount; ++i)
                {
                    var childLocal = boneTransform.GetChild(i).localPosition;

                    if (childLocal.sqrMagnitude <= 1.0e-8f) continue;

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
                var child = boneTransform.GetChild(i);
                var childLocal = child.localPosition;

                if (childLocal.sqrMagnitude <= 1.0e-8f) continue;

                float length = childLocal.magnitude;
                float alignment = hasParentDirection ? Vector3.Dot(childLocal.normalized, parentDirection) : 0.0f;
                float score = (length * 0.75f) + (alignment * 0.25f);

                score += ChildPrefScore(boneTransform.name, child.name);

                if (!hasValid || score > bestScore)
                {
                    bestScore = score;
                    directionHint = childLocal;
                    hasValid = true;
                }
            }

            return hasValid;
        }

        private static bool IsHumanoidFingerBone(Animator animator, Transform boneTransform)
        {
            if (animator == null || boneTransform == null)
            {
                return false;
            }

            for (int i = (int)HumanBodyBones.LeftThumbProximal; i <= (int)HumanBodyBones.RightLittleDistal; ++i)
            {
                if (boneTransform == animator.GetBoneTransform((HumanBodyBones)i))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHumanoidHandBone(Animator animator, Transform boneTransform)
        {
            if (animator == null || boneTransform == null)
            {
                return false;
            }

            return boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftHand) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private static bool TryPalmHint(Animator animator, Transform handTransform, out Vector3 palmAxis, out float palmLength)
        {
            palmAxis = Vector3.zero;
            palmLength = 0.0f;

            if (!IsHumanoidHandBone(animator, handTransform))
            {
                return false;
            }

            bool isLeft = handTransform == animator.GetBoneTransform(HumanBodyBones.LeftHand);
            HumanBodyBones[] proximalBones = isLeft
                ? new[]
                {
                    HumanBodyBones.LeftIndexProximal,
                    HumanBodyBones.LeftMiddleProximal,
                    HumanBodyBones.LeftRingProximal,
                    HumanBodyBones.LeftLittleProximal,
                }
                : new[]
                {
                    HumanBodyBones.RightIndexProximal,
                    HumanBodyBones.RightMiddleProximal,
                    HumanBodyBones.RightRingProximal,
                    HumanBodyBones.RightLittleProximal,
                };

            Vector3 sum = Vector3.zero;
            int count = 0;
            float middleLength = 0.0f;

            for (int i = 0; i < proximalBones.Length; ++i)
            {
                var proximal = animator.GetBoneTransform(proximalBones[i]);

                if (proximal == null)
                {
                    continue;
                }

                Vector3 local = handTransform.InverseTransformPoint(proximal.position);

                if (local.sqrMagnitude <= 1.0e-8f)
                {
                    continue;
                }

                sum += local;
                ++count;

                if (proximalBones[i] == (isLeft ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal))
                {
                    middleLength = local.magnitude;
                }
            }

            if (count == 0)
            {
                return false;
            }

            palmAxis = sum / count;
            palmLength = middleLength > 0.0f ? middleLength : palmAxis.magnitude;

            return palmAxis.sqrMagnitude > 1.0e-8f && palmLength > 0.0f;
        }

        private static bool IsHumanoidArmBone(Animator animator, Transform boneTransform)
        {
            if (animator == null || boneTransform == null)
            {
                return false;
            }

            return boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.RightUpperArm) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        private static bool IsHumanoidLegBone(Animator animator, Transform boneTransform)
        {
            if (animator == null || boneTransform == null)
            {
                return false;
            }

            return boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftFoot) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        private static bool IsHumanoidToeBone(Animator animator, Transform boneTransform)
        {
            if (animator == null || boneTransform == null)
            {
                return false;
            }

            return boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftToes) ||
                   boneTransform == animator.GetBoneTransform(HumanBodyBones.RightToes);
        }

        private static bool TryHumanoidHint(Animator animator, Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (animator == null || boneTransform == null)
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
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal))
            {
                nextBone = HumanBodyBones.LeftThumbIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate))
            {
                nextBone = HumanBodyBones.LeftThumbDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightThumbProximal))
            {
                nextBone = HumanBodyBones.RightThumbIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate))
            {
                nextBone = HumanBodyBones.RightThumbDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal))
            {
                nextBone = HumanBodyBones.LeftIndexIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate))
            {
                nextBone = HumanBodyBones.LeftIndexDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightIndexProximal))
            {
                nextBone = HumanBodyBones.RightIndexIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate))
            {
                nextBone = HumanBodyBones.RightIndexDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal))
            {
                nextBone = HumanBodyBones.LeftMiddleIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate))
            {
                nextBone = HumanBodyBones.LeftMiddleDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal))
            {
                nextBone = HumanBodyBones.RightMiddleIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate))
            {
                nextBone = HumanBodyBones.RightMiddleDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftRingProximal))
            {
                nextBone = HumanBodyBones.LeftRingIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftRingIntermediate))
            {
                nextBone = HumanBodyBones.LeftRingDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightRingProximal))
            {
                nextBone = HumanBodyBones.RightRingIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightRingIntermediate))
            {
                nextBone = HumanBodyBones.RightRingDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal))
            {
                nextBone = HumanBodyBones.LeftLittleIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.LeftLittleIntermediate))
            {
                nextBone = HumanBodyBones.LeftLittleDistal;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightLittleProximal))
            {
                nextBone = HumanBodyBones.RightLittleIntermediate;
            }
            else if (boneTransform == animator.GetBoneTransform(HumanBodyBones.RightLittleIntermediate))
            {
                nextBone = HumanBodyBones.RightLittleDistal;
            }

            if (nextBone == HumanBodyBones.LastBone)
            {
                return false;
            }

            var childBone = animator.GetBoneTransform(nextBone);

            if (childBone == null)
            {
                return false;
            }

            directionHint = boneTransform.InverseTransformPoint(childBone.position);

            return directionHint.sqrMagnitude > 1.0e-8f;
        }

        private static float ChildPrefScore(string parentBoneName, string childBoneName)
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
