using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static partial class ColliderCapsuleFitter
    {
        private static bool TryGetParentDirectionHint(Transform boneTransform, out Vector3 directionHint)
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

            if (TryGetHumanoidLimbChildDirectionHint(animator, boneTransform, out _)) return true;

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

            if (!isLegOrArm) return false;

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
            return
                role == BoneFitRole.Hips ||
                role == BoneFitRole.Spine ||
                role == BoneFitRole.Chest ||
                role == BoneFitRole.UpperChest;
        }

        private static bool TryGetChildDirectionHint(Animator animator, Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (boneTransform == null || boneTransform.childCount == 0)
            {
                return false;
            }

            if (TryGetHumanoidLimbChildDirectionHint(animator, boneTransform, out directionHint))
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

        private static bool TryGetHumanoidLimbChildDirectionHint(Animator animator, Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

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

            var childBone = animator.GetBoneTransform(nextBone);

            if (childBone == null) return false;

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
