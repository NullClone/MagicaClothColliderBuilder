using MagicaCloth2;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class ColliderGenerator
    {
        private enum BoneFitRole
        {
            Default,
            Hips,
            Spine,
            Chest,
            UpperChest,
        }

        private readonly GameObject m_AvatarRoot;
        private readonly SABoneColliderProperty m_Property;
        private readonly Animator m_Animator;

        public ColliderGenerator(GameObject avatarRoot, SABoneColliderProperty properties)
        {
            m_AvatarRoot = avatarRoot;
            m_Property = properties;
            m_Animator = avatarRoot.GetComponent<Animator>();
        }

        public void Process()
        {
            if (m_Animator == null || m_Animator.avatar == null || !m_Animator.avatar.isHuman)
            {
                Debug.LogError("Animator with a valid Humanoid Avatar is required on the root object.");
                return;
            }

            if (m_AvatarRoot.GetComponentsInChildren<MagicaCapsuleCollider>(true).Length > 0)
            {
                Debug.LogWarning("Generation skipped: existing MagicaCapsuleCollider components were found. Please cleanup first if you want to regenerate.");
                return;
            }

            var bonesToProcess = CollectHumanBones();

            if (bonesToProcess.Count == 0)
            {
                Debug.LogWarning("No human bones found to process.");
                return;
            }

            var boneMeshCache = new BoneMeshCache();
            boneMeshCache.Process(m_AvatarRoot);

            if (boneMeshCache.MeshBoneCount == 0)
            {
                Debug.LogError("No skinned meshes found to generate colliders from.");
                return;
            }

            var generationJobs = new List<ColliderGenerationJob>();
            var bonesWithoutMesh = new List<Transform>();

            foreach (Transform boneTransform in bonesToProcess)
            {
                var job = new ColliderGenerationJob(boneTransform.gameObject, m_Property, boneMeshCache);

                if (job.Prepare())
                {
                    generationJobs.Add(job);
                }
                else
                {
                    bonesWithoutMesh.Add(boneTransform);
                }
            }

            ExecuteJobs(generationJobs);
            CreateCollidersFromResults(generationJobs);

            foreach (Transform boneToFix in bonesWithoutMesh)
            {
                CreateDefaultColliderForBone(boneToFix);
            }

            Debug.Log($"Collider generation complete. Created {generationJobs.Count} colliders.");
        }

        private List<Transform> CollectHumanBones()
        {
            var bones = new List<Transform>();

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                if (i <= (int)HumanBodyBones.RightToes)
                {
                    var boneTransform = m_Animator.GetBoneTransform((HumanBodyBones)i);

                    if (boneTransform != null)
                    {
                        bones.Add(boneTransform);
                    }
                }
            }

            return bones;
        }

        private void ExecuteJobs(List<ColliderGenerationJob> jobs)
        {
            if (jobs == null || jobs.Count == 0) return;

            var countdownEvent = new CountdownEvent(jobs.Count);

            foreach (var job in jobs)
            {
                job.m_CountdownEvent = countdownEvent;

                ThreadPool.QueueUserWorkItem(job.Execute);
            }

            countdownEvent.Wait();
        }

        private void CreateCollidersFromResults(List<ColliderGenerationJob> jobs)
        {
            foreach (var job in jobs)
            {
                if (job.Result == null) continue;

                if (!TryFitCapsule(
                    job,
                    out var localRotation,
                    out var direction,
                    out var center,
                    out var length,
                    out var radiusAtMin,
                    out var radiusAtMax,
                    out var reverseDirection))
                {
                    continue;
                }

                var colliderGo = new GameObject("MagicaClothCollider");
                colliderGo.transform.parent = job.TargetBone.transform;
                colliderGo.transform.localPosition = Vector3.zero;
                colliderGo.transform.localRotation = localRotation;
                colliderGo.transform.localScale = Vector3.one;

                var capsule = colliderGo.AddComponent<MagicaCapsuleCollider>();
                capsule.direction = direction;
                capsule.center = center;
                capsule.SetSize(radiusAtMin, radiusAtMax, length);
                capsule.reverseDirection = reverseDirection;
                capsule.UpdateParameters();
            }
        }

        private static bool TryFitCapsule(
            ColliderGenerationJob job,
            out Quaternion localRotation,
            out MagicaCapsuleCollider.Direction direction,
            out Vector3 center,
            out float length,
            out float radiusAtMin,
            out float radiusAtMax,
            out bool reverseDirection)
        {
            localRotation = Quaternion.identity;
            direction = MagicaCapsuleCollider.Direction.Y;
            center = Vector3.zero;
            length = 0.02f;
            radiusAtMin = 0.01f;
            radiusAtMax = 0.01f;
            reverseDirection = false;

            var vertices = job.Vertices;

            if (vertices == null || vertices.Length < 4)
            {
                return false;
            }

            bool hasChildHint = TryGetChildDirectionHint(job.TargetBone.transform, out Vector3 childHint);
            bool hasParentHint = TryGetParentDirectionHint(job.TargetBone.transform, out Vector3 parentHint);
            BoneFitRole boneRole = DetectBoneFitRole(job.TargetBone.transform);
            bool isLimbBone = IsLimbBone(job.TargetBone.transform);

            if (isLimbBone && hasChildHint)
            {
                // Limb-specialized high-stability mode:
                // - Axis is fixed to child-bone direction
                // - Length is fixed to the exact parent-child bone distance
                // - Only radii are estimated from mesh distribution
                Vector3 limbAxis = childHint.normalized;
                float limbLength = Mathf.Max(childHint.magnitude, 0.02f);
                Quaternion limbRotation = Quaternion.FromToRotation(Vector3.up, limbAxis);

                if (!TryEvaluateCapsuleFitOnYAxis(
                    vertices,
                    Quaternion.Inverse(limbRotation),
                    job.Property.ReducerProperty.FitType == FitType.Outer ? 92.0f : 70.0f,
                    limbLength,
                    boneRole,
                    true,
                    out Vector3 limbCenter,
                    out float _,
                    out float limbStartRadius,
                    out float limbEndRadius,
                    out float _))
                {
                    return false;
                }

                localRotation = limbRotation;
                direction = MagicaCapsuleCollider.Direction.Y;
                center = new Vector3(0.0f, limbLength * 0.5f, 0.0f);
                length = limbLength;
                radiusAtMin = limbStartRadius;
                radiusAtMax = limbEndRadius;
                reverseDirection = false;
                return true;
            }

            float outerPercentile = 92.0f;
            float innerPercentile = 70.0f;
            float fitPercentile = job.Property.ReducerProperty.FitType == FitType.Outer ? outerPercentile : innerPercentile;

            if (job.Property.ReducerProperty.FitType == FitType.Outer)
            {
                if (boneRole == BoneFitRole.Hips)
                {
                    fitPercentile = 86.0f;
                }
                else if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
                {
                    fitPercentile = 88.0f;
                }
                else if (boneRole == BoneFitRole.UpperChest)
                {
                    fitPercentile = 95.0f;
                }
            }

            var axisCandidates = isLimbBone && hasChildHint
                ? BuildLimbAxisCandidates(vertices, childHint, hasParentHint, parentHint)
                : BuildAxisCandidates(vertices, hasChildHint, childHint, hasParentHint, parentHint);

            if (axisCandidates.Count == 0)
            {
                axisCandidates.Add(Vector3.up);
            }

            float boneLengthHint = hasChildHint ? childHint.magnitude : (hasParentHint ? parentHint.magnitude : 0.1f);

            float bestScore = float.MaxValue;
            Quaternion bestRotation = Quaternion.identity;
            Vector3 bestCenter = Vector3.zero;
            float bestLength = 0.02f;
            float bestStartRadius = 0.01f;
            float bestEndRadius = 0.01f;

            // Limb bones use joint-anchored mode so the proximal end-center is always fixed at
            // the current bone transform origin (required for stable knee/elbow bending behavior).
            bool limbBoneMode = false;

            Vector3 childHintNormalized = hasChildHint ? childHint.normalized : Vector3.up;

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

                // Use bone-axis-center only for the axis that aligns well with the child hint
                // (the child hint axis is the only one for which (0,0) truly corresponds to the bone axis).
                // Other candidates still use percentile-based bounds so their scores remain meaningful.
                float childAlignment = hasChildHint ? Mathf.Abs(Vector3.Dot(axis, childHintNormalized)) : 0.0f;
                bool useBoneAxisCenter = false;

                if (!TryEvaluateCapsuleFitOnYAxis(
                    vertices,
                    inverseRotation,
                    fitPercentile,
                    boneLengthHint,
                    boneRole,
                    useBoneAxisCenter,
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

                float alignmentBonus = hasChildHint ? childAlignment * (limbBoneMode ? 0.22f : 0.08f) : 0.0f;
                float score = candidateScore - alignmentBonus;

                if (limbBoneMode && hasParentHint)
                {
                    Vector3 parentFromBone = (-parentHint).normalized;
                    float parentContinuity = Mathf.Abs(Vector3.Dot(axis, parentFromBone));
                    score -= parentContinuity * 0.04f;
                }

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

            localRotation = bestRotation;
            direction = MagicaCapsuleCollider.Direction.Y;
            center = bestCenter;
            length = bestLength;
            radiusAtMin = bestStartRadius;
            radiusAtMax = bestEndRadius;
            reverseDirection = false;

            return true;
        }

        private static List<Vector3> BuildLimbAxisCandidates(
            Vector3[] vertices,
            Vector3 childHint,
            bool hasParentHint,
            Vector3 parentHint)
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
                    if (Mathf.Abs(Vector3.Dot(unique[n], normalized)) > 0.9985f)
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

        private static List<Vector3> BuildAxisCandidates(
            Vector3[] vertices,
            bool hasChildHint,
            Vector3 childHint,
            bool hasParentHint,
            Vector3 parentHint)
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

            var unique = new List<Vector3>();

            foreach (var candidate in candidates)
            {
                if (candidate.sqrMagnitude <= 1.0e-8f)
                {
                    continue;
                }

                Vector3 normalized = candidate.normalized;
                bool duplicated = false;

                for (int i = 0; i < unique.Count; ++i)
                {
                    if (Mathf.Abs(Vector3.Dot(unique[i], normalized)) > 0.995f)
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

        private static bool TryEvaluateCapsuleFitOnYAxis(
            Vector3[] vertices,
            Quaternion inverseRotation,
            float fitPercentile,
            float boneLengthHint,
            BoneFitRole boneRole,
            bool useBoneAxisCenter,
            out Vector3 center,
            out float length,
            out float startRadius,
            out float endRadius,
            out float score)
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

            float minY, maxY, centerX, centerY, centerZ;

            if (useBoneAxisCenter)
            {
                // ---- Limb bone mode ----
                // The bone's parent joint is at (0,0,0) in bone-local space → y=0 after inverseRotation.
                // The child joint is at boneLengthHint along the axis → y=boneLengthHint.
                // Anchoring to these exact joint positions eliminates positional drift at joints (knee, elbow, etc.).
                minY = 0.0f;
                maxY = Mathf.Max(boneLengthHint, 0.02f);
                length = maxY;

                // Place capsule axis exactly on the bone axis (x=0, z=0 in the rotated frame).
                // Using the mesh centroid shifts the collider off-bone; the bone axis is the authoritative reference.
                centerX = 0.0f;
                centerZ = 0.0f;
                centerY = length * 0.5f;
            }
            else
            {
                // ---- Body / fallback mode: percentile-based bounds ----
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

                    if (maxY <= minY)
                    {
                        return false;
                    }
                }

                length = Mathf.Max(0.02f, maxY - minY);

                if (boneRole == BoneFitRole.Hips)
                {
                    float maxHipsLength = Mathf.Max(0.045f, boneLengthHint * 1.35f);
                    length = Mathf.Min(length, maxHipsLength);
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

                centerX = Percentile(xValues, 50.0f);
                float centerYRatio = 0.5f;

                if (boneRole == BoneFitRole.Hips)
                {
                    centerYRatio = 0.68f;
                }
                else if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
                {
                    centerYRatio = 0.56f;
                }
                else if (boneRole == BoneFitRole.UpperChest)
                {
                    centerYRatio = 0.58f;
                }

                centerY = Mathf.Lerp(minY, maxY, centerYRatio);
                centerZ = Percentile(zValues, 50.0f);
            }

            center = new Vector3(centerX, centerY, centerZ);

            float endWindow = Mathf.Max(length * 0.22f, 0.004f);

            // For limb bones, vertices that leaked from adjacent bones (y outside the joint range)
            // are excluded from end-cap radius samples to avoid inflating the collider at joints.
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

            float roleRadiusScale = 1.0f;

            if (boneRole == BoneFitRole.Hips)
            {
                roleRadiusScale = 0.82f;
            }
            else if (boneRole == BoneFitRole.Spine || boneRole == BoneFitRole.Chest)
            {
                roleRadiusScale = 0.9f;
            }
            else if (boneRole == BoneFitRole.UpperChest)
            {
                roleRadiusScale = 1.15f;
            }

            startRadius *= roleRadiusScale;
            endRadius *= roleRadiusScale;

            float maxRadiusByBone = Mathf.Max(0.02f, boneLengthHint * 0.7f);
            float maxRadiusByLength = Mathf.Max(0.01f, length * 0.7f);
            float maxAllowedRadius = Mathf.Min(maxRadiusByBone, maxRadiusByLength);

            startRadius = Mathf.Clamp(startRadius, 0.003f, maxAllowedRadius);
            endRadius = Mathf.Clamp(endRadius, 0.003f, maxAllowedRadius);

            if (boneRole == BoneFitRole.UpperChest)
            {
                float minUpperChestRadius = Mathf.Max(0.012f, length * 0.18f);
                startRadius = Mathf.Max(startRadius, minUpperChestRadius);
                endRadius = Mathf.Max(endRadius, minUpperChestRadius * 0.95f);
            }

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

            score = (overflow95 * 4.0f) + (overflowMean * 2.0f) + (compactness * 0.15f);
            return true;
        }

        private static BoneFitRole DetectBoneFitRole(Transform boneTransform)
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

        private static bool TryGetParentDirectionHint(Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (boneTransform == null || boneTransform.parent == null)
            {
                return false;
            }

            // Convert parent direction into this bone's local space so it is comparable with child local vectors.
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

        private static void GetDirectionFromAxis(Vector3 axis, out MagicaCapsuleCollider.Direction direction, out int axisIndex, out Vector3 baseAxis)
        {
            float absX = Mathf.Abs(axis.x);
            float absY = Mathf.Abs(axis.y);
            float absZ = Mathf.Abs(axis.z);

            if (absX >= absY && absX >= absZ)
            {
                direction = MagicaCapsuleCollider.Direction.X;
                axisIndex = 0;
                baseAxis = Vector3.right;
                return;
            }

            if (absY >= absX && absY >= absZ)
            {
                direction = MagicaCapsuleCollider.Direction.Y;
                axisIndex = 1;
                baseAxis = Vector3.up;
                return;
            }

            direction = MagicaCapsuleCollider.Direction.Z;
            axisIndex = 2;
            baseAxis = Vector3.forward;
        }

        private static float GetAxisComponent(Vector3 value, int axisIndex)
        {
            if (axisIndex == 0)
            {
                return value.x;
            }

            if (axisIndex == 1)
            {
                return value.y;
            }

            return value.z;
        }

        private static Vector3 SetAxisComponent(Vector3 value, int axisIndex, float axisValue)
        {
            if (axisIndex == 0)
            {
                value.x = axisValue;
                return value;
            }

            if (axisIndex == 1)
            {
                value.y = axisValue;
                return value;
            }

            value.z = axisValue;
            return value;
        }

        private static float GetRadialDistance(Vector3 value, int axisIndex)
        {
            if (axisIndex == 0)
            {
                return Mathf.Sqrt((value.y * value.y) + (value.z * value.z));
            }

            if (axisIndex == 1)
            {
                return Mathf.Sqrt((value.x * value.x) + (value.z * value.z));
            }

            return Mathf.Sqrt((value.x * value.x) + (value.y * value.y));
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

        private static bool TryGetChildDirectionHint(Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (boneTransform == null || boneTransform.childCount == 0)
            {
                return false;
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

                // Strongly prefer anatomically expected child links for limb bones.
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

            if (parent.Contains("upperleg") || parent.Contains("thigh"))
            {
                if (child.Contains("lowerleg") || child.Contains("calf") || child.Contains("knee") || child.Contains("shin"))
                {
                    return 0.45f;
                }
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

            if (parent.Contains("upperarm"))
            {
                if (child.Contains("lowerarm") || child.Contains("forearm") || child.Contains("elbow"))
                {
                    return 0.45f;
                }
            }

            if (parent.Contains("lowerarm") || parent.Contains("forearm") || parent.Contains("elbow"))
            {
                if (child.Contains("hand") || child.Contains("wrist"))
                {
                    return 0.45f;
                }
            }

            return 0.0f;
        }

        private void CreateDefaultColliderForBone(Transform boneTransform)
        {
            Debug.LogWarning($"Could not determine mesh shape for bone '{boneTransform.name}'. Creating a fallback collider.");

            var colliderGameObject = new GameObject("MagicaClothCollider (Fallback)");
            colliderGameObject.transform.parent = boneTransform;
            colliderGameObject.transform.localPosition = Vector3.zero;
            colliderGameObject.transform.localRotation = Quaternion.identity;
            colliderGameObject.transform.localScale = Vector3.one;
            var capsuleCollider = colliderGameObject.AddComponent<MagicaCapsuleCollider>();

            if (boneTransform.childCount > 0)
            {
                float maxDistance = 0f;

                foreach (Transform child in boneTransform)
                {
                    float distance = Vector3.Distance(boneTransform.position, child.position);

                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                    }
                }

                float radius = maxDistance > 0.001f ? maxDistance : 0.05f;

                if (DetectBoneFitRole(boneTransform) == BoneFitRole.UpperChest)
                {
                    radius *= 1.1f;
                }

                capsuleCollider.SetSize(radius, radius, 0.01f);
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
            }
            else
            {
                float defaultRadius = 0.02f;
                float defaultLength = 0.02f;

                if (DetectBoneFitRole(boneTransform) == BoneFitRole.UpperChest)
                {
                    defaultRadius = 0.03f;
                    defaultLength = 0.04f;
                }

                capsuleCollider.SetSize(defaultRadius, defaultRadius, defaultLength);
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
            }
        }

        private static void ComputeAxisRadii(Vector3[] vertices, Vector3 center, float length, int direction, FitType fitType, out float radiusAtMin, out float radiusAtMax)
        {
            radiusAtMin = 0f;
            radiusAtMax = 0f;

            if (vertices == null || vertices.Length == 0 || length < 0.001f)
            {
                radiusAtMin = radiusAtMax = 0.01f;
                return;
            }

            const int NUM_SLICES = 10;

            var maxRadiusPerSlice = new float[NUM_SLICES];
            var avgRadiusPerSlice = new List<float>[NUM_SLICES];

            for (int i = 0; i < NUM_SLICES; i++)
            {
                avgRadiusPerSlice[i] = new List<float>();
            }

            float halfLen = length / 2.0f;
            float sliceWidth = length / NUM_SLICES;

            foreach (var vertex in vertices)
            {
                Vector3 delta = vertex - center;
                float axisPosition;
                float distanceSquared;

                if (direction == 0)
                {
                    axisPosition = delta.x;
                    distanceSquared = (delta.y * delta.y) + (delta.z * delta.z);
                }
                else if (direction == 1)
                {
                    axisPosition = delta.y;
                    distanceSquared = (delta.x * delta.x) + (delta.z * delta.z);
                }
                else
                {
                    axisPosition = delta.z;
                    distanceSquared = (delta.x * delta.x) + (delta.y * delta.y);
                }

                float distance = Mathf.Sqrt(distanceSquared);

                int sliceIndex = Mathf.FloorToInt((axisPosition + halfLen) / sliceWidth);
                sliceIndex = Mathf.Clamp(sliceIndex, 0, NUM_SLICES - 1);

                if (distance > maxRadiusPerSlice[sliceIndex])
                {
                    maxRadiusPerSlice[sliceIndex] = distance;
                }
                avgRadiusPerSlice[sliceIndex].Add(distance);
            }

            int endSliceForMin = Mathf.Max(1, NUM_SLICES / 5);
            int startSliceForMax = Mathf.Min(NUM_SLICES - 1, NUM_SLICES - endSliceForMin);

            if (fitType == FitType.Inner)
            {
                float minSum = 0;
                int minSumCount = 0;

                for (int i = 0; i < endSliceForMin; i++)
                {
                    if (avgRadiusPerSlice[i].Count > 0)
                    {
                        minSum += avgRadiusPerSlice[i].Average();
                        minSumCount++;
                    }
                }

                radiusAtMin = (minSumCount > 0) ? minSum / minSumCount : 0f;

                float maxSum = 0;
                int maxSumCount = 0;

                for (int i = startSliceForMax; i < NUM_SLICES; i++)
                {
                    if (avgRadiusPerSlice[i].Count > 0)
                    {
                        maxSum += avgRadiusPerSlice[i].Average();
                        maxSumCount++;
                    }
                }
                radiusAtMax = (maxSumCount > 0) ? maxSum / maxSumCount : 0f;
            }
            else
            {
                for (int i = 0; i < endSliceForMin; i++)
                {
                    if (maxRadiusPerSlice[i] > radiusAtMin) radiusAtMin = maxRadiusPerSlice[i];
                }
                for (int i = startSliceForMax; i < NUM_SLICES; i++)
                {
                    if (maxRadiusPerSlice[i] > radiusAtMax) radiusAtMax = maxRadiusPerSlice[i];
                }
            }

            if (radiusAtMin <= 0.001f && radiusAtMax > 0.001f) radiusAtMin = radiusAtMax;
            if (radiusAtMax <= 0.001f && radiusAtMin > 0.001f) radiusAtMax = radiusAtMin;

            radiusAtMin = Mathf.Max(radiusAtMin, 0.01f);
            radiusAtMax = Mathf.Max(radiusAtMax, 0.01f);
        }

        public static Vector3 FuzzyZero(Vector3 vector)
        {
            if (Mathf.Abs(vector.x) <= 0.0001f)
            {
                vector.x = 0;
            }

            if (Mathf.Abs(vector.y) <= 0.0001f)
            {
                vector.y = 0;
            }

            if (Mathf.Abs(vector.z) <= 0.0001f)
            {
                vector.z = 0;
            }

            return vector;
        }
    }
}