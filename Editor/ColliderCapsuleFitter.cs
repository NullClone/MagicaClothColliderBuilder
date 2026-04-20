using MagicaCloth2;
using System.Collections.Generic;
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
        Neck,
        Head,
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
            bool hasHumanoidLimbHint = TryGetHumanoidLimbChildDirectionHint(job.TargetBone.transform, out Vector3 humanoidLimbHint);

            if (boneRole == BoneFitRole.Head && TryFitHeadCapsule(job, out fitResult))
            {
                return true;
            }

            if (IsBodyRole(boneRole) && TryFitHorizontalBodyCapsule(job, boneRole, out fitResult))
            {
                return true;
            }

            if (job.Property.LimbFitProperty.ForceFixedAxisByHumanoid && hasHumanoidLimbHint && TryFitLimbCapsule(job, humanoidLimbHint, boneRole, ref fitResult))
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

            Animator animator = boneTransform.GetComponentInParent<Animator>();

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

            string boneName = boneTransform.name.ToLowerInvariant();

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

        private static bool TryFitHeadCapsule(ColliderGenerationJob job, out CapsuleFitResult fitResult)
        {
            fitResult = default;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4 || job.TargetBone == null)
            {
                return false;
            }

            HeadFitProperty settings = job.Property.HeadFitProperty;
            Transform headTransform = job.TargetBone.transform;

            var xValues = new List<float>(vertices.Length);
            var yValues = new List<float>(vertices.Length);
            var zValues = new List<float>(vertices.Length);

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 v = vertices[i];
                xValues.Add(v.x);
                yValues.Add(v.y);
                zValues.Add(v.z);
            }

            Vector3 center = new Vector3(
                Percentile(xValues, 50.0f),
                Percentile(yValues, 50.0f),
                Percentile(zValues, 50.0f));

            var distanceValues = new List<float>(vertices.Length);

            for (int i = 0; i < vertices.Length; ++i)
            {
                distanceValues.Add((vertices[i] - center).magnitude);
            }

            float baseRadius;

            if (!TryGetAreaWeightedDistancePercentile(vertices, job.Triangles, center, settings.RadiusPercentile, out baseRadius))
            {
                baseRadius = Percentile(distanceValues, settings.RadiusPercentile);
            }

            float radius = baseRadius * settings.RadiusScale;
            radius = Mathf.Clamp(radius, settings.MinRadius, settings.MaxRadius);

            Vector3 faceDir = headTransform.InverseTransformDirection(headTransform.root != null ? headTransform.root.forward : Vector3.forward);

            if (faceDir.sqrMagnitude <= 1.0e-8f)
            {
                faceDir = Vector3.forward;
            }

            faceDir.Normalize();

            Vector3 localUp = headTransform.InverseTransformDirection(headTransform.root != null ? headTransform.root.up : Vector3.up);

            if (localUp.sqrMagnitude <= 1.0e-8f)
            {
                localUp = Vector3.up;
            }

            localUp.Normalize();
            float length = Mathf.Clamp(radius * settings.LengthRatio, 0.005f, radius * 0.6f);

            if (settings.AnchorOuterStartToHeadTransform)
            {
                // Keep the outermost tip (not sphere center) of the start cap at the Head transform.
                // tip_y = center.y - length/2 - radius = 0  =>  center.y = length/2 + radius
                center = new Vector3(0.0f, length * 0.5f + radius, 0.0f);
            }
            else if (settings.UseFaceForwardOffsetWhenNotAnchored)
            {
                center += (faceDir * settings.ForwardOffset) + (localUp * settings.UpOffset);
            }

            fitResult = new CapsuleFitResult
            {
                LocalRotation = Quaternion.identity,
                Direction = MagicaCapsuleCollider.Direction.Y,
                Center = center,
                Length = length,
                RadiusAtMin = radius,
                RadiusAtMax = radius,
                ReverseDirection = false,
            };
            return true;
        }

        private static bool TryFitLimbCapsule(ColliderGenerationJob job, Vector3 childHint, BoneFitRole boneRole, ref CapsuleFitResult fitResult)
        {
            LimbFitProperty limbSettings = job.Property.LimbFitProperty;
            Vector3 limbAxis = childHint.normalized;
            float jointDistance = Mathf.Max(childHint.magnitude, limbSettings.MinJointDistance);
            Quaternion limbRotation = Quaternion.FromToRotation(Vector3.up, limbAxis);

            if (!TryEvaluateCapsuleFitOnYAxis(
                job.Vertices,
                Quaternion.Inverse(limbRotation),
                limbSettings.RadiusPercentile,
                jointDistance,
                boneRole,
                true,
                job.Property,
                out Vector3 _,
                out float _,
                out float limbStartRadius,
                out float limbEndRadius,
                out float _))
            {
                return false;
            }

            limbStartRadius *= limbSettings.RadiusScale;
            limbEndRadius *= limbSettings.RadiusScale;

            // MagicaCapsuleCollider length behaves as the full end-to-end span.
            // To place the cylinder-cap seam centers on the bone origin and child bone position,
            // the total length must include both end radii.
            float totalLength = jointDistance + limbStartRadius + limbEndRadius;
            float centerY = (jointDistance * 0.5f) + ((limbStartRadius - limbEndRadius) * 0.5f);
           
               if (!limbSettings.AnchorStartSphereCenterToBone)
               {
                   centerY = totalLength * 0.5f;
               }

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

            float fitPercentile = GetFitPercentile(job.Property.GenericFitProperty, boneRole);
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
                    boneRole == BoneFitRole.Neck,
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

        private static float GetFitPercentile(GenericFitProperty settings, BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return settings.HipsFitPercentile;
            }

            if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                return settings.SpineChestFitPercentile;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return settings.UpperChestFitPercentile;
            }

            if (boneRole == BoneFitRole.Neck)
            {
                return settings.SpineChestFitPercentile;
            }

            return settings.DefaultFitPercentile;
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
            BodyFitProperty bodySettings = job.Property.BodyFitProperty;
            Vector3 worldHorizontal = bodySettings.HorizontalAxis == BodyHorizontalAxis.RootForward
                ? (boneTransform.root != null ? boneTransform.root.forward : Vector3.forward)
                : (boneTransform.root != null ? boneTransform.root.right : Vector3.right);
            Vector3 localHorizontal = boneTransform.InverseTransformDirection(worldHorizontal);

            if (bodySettings.ProjectAxisToBodyUpPlane)
            {
                Vector3 bodyUp = GetBodyUpAxisLocal(boneTransform, boneRole);

                if (bodyUp.sqrMagnitude > 1.0e-8f)
                {
                    localHorizontal = Vector3.ProjectOnPlane(localHorizontal, bodyUp);
                }
            }

            if (boneRole == BoneFitRole.Hips && bodySettings.HipsProjectAxisToSpinePlane)
            {
                Vector3 localSpineUp = GetHipsSpineUpAxisLocal(boneTransform);

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
            Quaternion localRotation = Quaternion.FromToRotation(Vector3.up, localHorizontal);
            Quaternion inverseRotation = Quaternion.Inverse(localRotation);

            var absYValues = new List<float>(vertices.Length);
            var radialValues = new List<float>(vertices.Length);
            var rotated = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 rv = inverseRotation * vertices[i];
                rotated[i] = rv;
                absYValues.Add(Mathf.Abs(rv.y));
                radialValues.Add(Mathf.Sqrt((rv.x * rv.x) + (rv.z * rv.z)));
            }

            float halfLength = Percentile(absYValues, GetHorizontalLengthPercentile(bodySettings, boneRole));
            float length = Mathf.Max(bodySettings.MinLength, halfLength * 2.0f);

            if (boneRole == BoneFitRole.Hips)
            {
                float hipsSpineDistance = GetHipsSpineDistanceLocal(boneTransform);
                float maxByBone = hipsSpineDistance > 1.0e-6f
                    ? Mathf.Max(bodySettings.MinLength, hipsSpineDistance * bodySettings.HipsMaxLengthBySpineDistance)
                    : bodySettings.HipsMaxLength;
                float hipsMaxLength = Mathf.Max(bodySettings.MinLength, Mathf.Min(bodySettings.HipsMaxLength, maxByBone));
                length = Mathf.Min(length, hipsMaxLength);
            }

            float radius;

            if (!TryGetAreaWeightedRadialPercentile(rotated, job.Triangles, bodySettings.RadiusPercentile, out radius))
            {
                radius = Percentile(radialValues, bodySettings.RadiusPercentile);
            }

            radius *= GetHorizontalRadiusScale(bodySettings, boneRole);
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

        private static Vector3 GetHipsSpineUpAxisLocal(Transform hipsTransform)
        {
            Animator animator = hipsTransform.GetComponentInParent<Animator>();

            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return Vector3.zero;
            }

            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);

            if (spine == null)
            {
                return Vector3.zero;
            }

            Vector3 localSpine = hipsTransform.InverseTransformPoint(spine.position);
            return localSpine.normalized;
        }

        private static Vector3 GetBodyUpAxisLocal(Transform bodyTransform, BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return GetHipsSpineUpAxisLocal(bodyTransform);
            }

            Animator animator = bodyTransform.GetComponentInParent<Animator>();

            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return bodyTransform.InverseTransformDirection(bodyTransform.root != null ? bodyTransform.root.up : Vector3.up).normalized;
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

        private static float GetHipsSpineDistanceLocal(Transform hipsTransform)
        {
            Animator animator = hipsTransform.GetComponentInParent<Animator>();

            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return 0.0f;
            }

            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);

            if (spine == null)
            {
                return 0.0f;
            }

            Vector3 localSpine = hipsTransform.InverseTransformPoint(spine.position);
            return localSpine.magnitude;
        }

        private static float GetHorizontalLengthPercentile(BodyFitProperty settings, BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return settings.HipsLengthPercentile;
            }

            if (boneRole == BoneFitRole.Spine)
            {
                return settings.SpineLengthPercentile;
            }

            if (boneRole == BoneFitRole.Chest)
            {
                return settings.ChestLengthPercentile;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return settings.UpperChestLengthPercentile;
            }

            return settings.ChestLengthPercentile;
        }

        private static float GetHorizontalRadiusScale(BodyFitProperty settings, BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return settings.HipsRadiusScale;
            }

            if (boneRole == BoneFitRole.Spine)
            {
                return settings.SpineRadiusScale;
            }

            if (boneRole == BoneFitRole.Chest)
            {
                return settings.ChestRadiusScale;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return settings.UpperChestRadiusScale;
            }

            return settings.ChestRadiusScale;
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

        private static bool TryEvaluateCapsuleFitOnYAxis(Vector3[] vertices, Quaternion inverseRotation, float fitPercentile, float boneLengthHint, BoneFitRole boneRole, bool useBoneAxisCenter, SABoneColliderProperty property, out Vector3 center, out float length, out float startRadius, out float endRadius, out float score)
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
                centerX = 0.0f;
                centerZ = 0.0f;
                centerY = length * 0.5f;
            }
            else
            {
                var sortedYValues = new List<float>(yValues);
                sortedYValues.Sort();
                GetPercentileBoundsFromSorted(sortedYValues, boneLengthHint, boneRole, property.GenericFitProperty, out minY, out maxY, out length);
                centerX = Percentile(xValues, 50.0f);
                centerY = Mathf.Lerp(minY, maxY, GetCenterYRatio(property.GenericFitProperty, boneRole));
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

            float roleRadiusScale = GetRoleRadiusScale(property.GenericFitProperty, boneRole);
            startRadius *= roleRadiusScale;
            endRadius *= roleRadiusScale;

            float maxAllowedRadius = Mathf.Min(
                Mathf.Max(0.02f, boneLengthHint * property.GenericFitProperty.MaxRadiusByBoneRatio),
                Mathf.Max(0.01f, length * property.GenericFitProperty.MaxRadiusByLengthRatio));
            startRadius = Mathf.Clamp(startRadius, property.GenericFitProperty.MinRadius, maxAllowedRadius);
            endRadius = Mathf.Clamp(endRadius, property.GenericFitProperty.MinRadius, maxAllowedRadius);

            if (boneRole == BoneFitRole.UpperChest)
            {
                float minUpperChestRadius = Mathf.Max(property.GenericFitProperty.UpperChestMinRadius, length * property.GenericFitProperty.UpperChestMinRadiusByLengthRatio);
                startRadius = Mathf.Max(startRadius, minUpperChestRadius);
                endRadius = Mathf.Max(endRadius, minUpperChestRadius * 0.95f);
            }

            score = CalculateCapsuleScore(rotated, minY, maxY, centerX, centerZ, length, startRadius, endRadius);
            return true;
        }

        private static void GetPercentileBoundsFromSorted(List<float> sortedYValues, float boneLengthHint, BoneFitRole boneRole, GenericFitProperty settings, out float minY, out float maxY, out float length)
        {
            float lowerPercentile = settings.DefaultLowerPercentile;
            float upperPercentile = settings.DefaultUpperPercentile;

            if (boneRole == BoneFitRole.Hips)
            {
                lowerPercentile = settings.HipsLowerPercentile;
                upperPercentile = settings.HipsUpperPercentile;
            }
            else if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                lowerPercentile = settings.SpineChestLowerPercentile;
                upperPercentile = settings.SpineChestUpperPercentile;
            }
            else if (boneRole == BoneFitRole.UpperChest)
            {
                lowerPercentile = settings.UpperChestLowerPercentile;
                upperPercentile = settings.UpperChestUpperPercentile;
            }

            minY = PercentileFromSorted(sortedYValues, lowerPercentile);
            maxY = PercentileFromSorted(sortedYValues, upperPercentile);

            if (maxY <= minY)
            {
                minY = PercentileFromSorted(sortedYValues, 0.0f);
                maxY = PercentileFromSorted(sortedYValues, 100.0f);
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

        private static float GetCenterYRatio(GenericFitProperty settings, BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return settings.HipsCenterYRatio;
            }

            if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                return settings.SpineChestCenterYRatio;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return settings.UpperChestCenterYRatio;
            }

            return 0.5f;
        }

        private static float GetRoleRadiusScale(GenericFitProperty settings, BoneFitRole boneRole)
        {
            if (boneRole == BoneFitRole.Hips)
            {
                return settings.HipsRadiusScale;
            }

            if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                return settings.SpineChestRadiusScale;
            }

            if (boneRole == BoneFitRole.UpperChest)
            {
                return settings.UpperChestRadiusScale;
            }

            return 1.0f;
        }

        private static float CalculateCapsuleScore(Vector3[] rotated, float minY, float maxY, float centerX, float centerZ, float length, float startRadius, float endRadius)
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

        private static bool TryGetAreaWeightedDistancePercentile(Vector3[] vertices, int[] triangles, Vector3 center, float percentile, out float weightedRadius)
        {
            weightedRadius = 0.0f;

            if (vertices == null || triangles == null || triangles.Length < 3)
            {
                return false;
            }

            var values = new List<float>(triangles.Length);
            var weights = new List<float>(triangles.Length);

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int i0 = triangles[i + 0];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                {
                    continue;
                }

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;

                if (area <= 1.0e-12f)
                {
                    continue;
                }

                values.Add((v0 - center).magnitude);
                weights.Add(area);
                values.Add((v1 - center).magnitude);
                weights.Add(area);
                values.Add((v2 - center).magnitude);
                weights.Add(area);
            }

            if (values.Count == 0)
            {
                return false;
            }

            weightedRadius = WeightedPercentile(values, weights, percentile);
            return true;
        }

        private static bool TryGetAreaWeightedRadialPercentile(Vector3[] rotatedVertices, int[] triangles, float percentile, out float weightedRadius)
        {
            weightedRadius = 0.0f;

            if (rotatedVertices == null || triangles == null || triangles.Length < 3)
            {
                return false;
            }

            var values = new List<float>(triangles.Length);
            var weights = new List<float>(triangles.Length);

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int i0 = triangles[i + 0];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= rotatedVertices.Length || i1 >= rotatedVertices.Length || i2 >= rotatedVertices.Length)
                {
                    continue;
                }

                Vector3 v0 = rotatedVertices[i0];
                Vector3 v1 = rotatedVertices[i1];
                Vector3 v2 = rotatedVertices[i2];
                float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;

                if (area <= 1.0e-12f)
                {
                    continue;
                }

                values.Add(Mathf.Sqrt((v0.x * v0.x) + (v0.z * v0.z)));
                weights.Add(area);
                values.Add(Mathf.Sqrt((v1.x * v1.x) + (v1.z * v1.z)));
                weights.Add(area);
                values.Add(Mathf.Sqrt((v2.x * v2.x) + (v2.z * v2.z)));
                weights.Add(area);
            }

            if (values.Count == 0)
            {
                return false;
            }

            weightedRadius = WeightedPercentile(values, weights, percentile);
            return true;
        }

        private static float WeightedPercentile(List<float> values, List<float> weights, float percentile)
        {
            if (values == null || weights == null || values.Count == 0 || weights.Count == 0)
            {
                return 0.0f;
            }

            int count = Mathf.Min(values.Count, weights.Count);

            if (count <= 0)
            {
                return 0.0f;
            }

            var samples = new List<WeightedSample>(count);
            float totalWeight = 0.0f;

            for (int i = 0; i < count; ++i)
            {
                float weight = weights[i];

                if (weight <= 0.0f)
                {
                    continue;
                }

                samples.Add(new WeightedSample
                {
                    Value = values[i],
                    Weight = weight,
                });

                totalWeight += weight;
            }

            if (samples.Count == 0 || totalWeight <= 0.0f)
            {
                return 0.0f;
            }

            samples.Sort((a, b) => a.Value.CompareTo(b.Value));
            float target = Mathf.Clamp(percentile, 0.0f, 100.0f) * 0.01f * totalWeight;
            float cumulative = 0.0f;

            for (int i = 0; i < samples.Count; ++i)
            {
                cumulative += samples[i].Weight;

                if (cumulative >= target)
                {
                    return samples[i].Value;
                }
            }

            return samples[samples.Count - 1].Value;
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

            values.Sort();
            return PercentileFromSorted(values, percentile);
        }

        private static float PercentileFromSorted(List<float> sortedValues, float percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0)
            {
                return 0.0f;
            }

            if (sortedValues.Count == 1)
            {
                return sortedValues[0];
            }

            float clampedPercentile = Mathf.Clamp(percentile, 0.0f, 100.0f);
            float rank = (clampedPercentile * 0.01f) * (sortedValues.Count - 1);
            int lowerIndex = Mathf.FloorToInt(rank);
            int upperIndex = Mathf.CeilToInt(rank);

            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            float t = rank - lowerIndex;
            return Mathf.Lerp(sortedValues[lowerIndex], sortedValues[upperIndex], t);
        }

        private struct WeightedSample
        {
            public float Value;
            public float Weight;
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

            if (TryGetHumanoidLimbChildDirectionHint(boneTransform, out _))
            {
                return true;
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