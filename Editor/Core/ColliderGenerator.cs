using MagicaCloth2;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class ColliderGenerator
    {
        // Constants

        public const string GeneratedColliderName = "MagicaClothCollider";
        private const string UndoGroupName = "Generate Magica Cloth Colliders";


        // Fields

        private readonly GameObject m_AvatarRoot;
        private readonly SABoneColliderProperty m_Property;
        private readonly Animator m_Animator;
        private readonly List<SkinnedMeshRenderer> m_CustomSkinnedMeshes;
        private readonly Action<float, string> m_ProgressReporter;


        // Methods

        public ColliderGenerator(GameObject avatarRoot, SABoneColliderProperty properties, List<SkinnedMeshRenderer> customSkinnedMeshes = null, Action<float, string> progressReporter = null)
        {
            m_AvatarRoot = avatarRoot;
            m_Property = properties;
            m_Animator = avatarRoot.GetComponent<Animator>();
            m_CustomSkinnedMeshes = customSkinnedMeshes;
            m_ProgressReporter = progressReporter;
        }

        public List<MagicaCapsuleCollider> Process()
        {
            var createdColliders = new List<MagicaCapsuleCollider>();
            var fallbackBoneNames = new List<string>();

            ReportProgress(0.05f, "Preparing...");

            if (m_Animator == null || m_Animator.avatar == null || !m_Animator.avatar.isHuman)
            {
                Debug.LogError("Animator with a valid Humanoid Avatar is required on the root object.");

                return createdColliders;
            }

            var bonesToProcess = CollectHumanBones();

            if (bonesToProcess.Count == 0)
            {
                Debug.LogWarning("No human bones found to process.");

                return createdColliders;
            }

            ReportProgress(0.30f, "Reading mesh data...");

            var boneMeshCache = new BoneMeshCache();
            boneMeshCache.Process(m_AvatarRoot, m_CustomSkinnedMeshes);

            if (boneMeshCache.MeshBoneCount == 0)
            {
                Debug.LogError("No skinned meshes found to generate colliders from.");

                return createdColliders;
            }

            var generationJobs = new List<ColliderGenerationJob>();
            var bonesWithoutMesh = new List<Transform>();

            ReportProgress(0.55f, "Preparing bone meshes...");

            int boneCount = bonesToProcess.Count;

            for (int i = 0; i < boneCount; ++i)
            {
                var job = new ColliderGenerationJob(bonesToProcess[i].gameObject, m_Animator, m_Property, boneMeshCache);

                if (job.Prepare())
                {
                    generationJobs.Add(job);
                }
                else
                {
                    bonesWithoutMesh.Add(bonesToProcess[i]);
                }
            }

            ReportProgress(0.80f, "Fitting colliders...");

            var fittedColliders = CreateCollidersFromResults(generationJobs);
            createdColliders.AddRange(fittedColliders);

            if (bonesWithoutMesh.Count > 0)
            {
                ReportProgress(0.92f, "Creating fallback colliders...");
            }

            for (int i = 0; i < bonesWithoutMesh.Count; ++i)
            {
                var boneToFix = bonesWithoutMesh[i];
                var fallbackCollider = CreateDefaultColliderForBone(boneToFix);

                if (fallbackCollider != null)
                {
                    createdColliders.Add(fallbackCollider);
                    fallbackBoneNames.Add(boneToFix != null ? boneToFix.name : "Unknown");
                }
            }

            Debug.Log(
                $"Magica Collider Builder: Created {createdColliders.Count} colliders " +
                $"({fittedColliders.Count} fitted, {fallbackBoneNames.Count} fallback).");

            if (fallbackBoneNames.Count > 0)
            {
                Debug.LogWarning(
                    $"Magica Collider Builder: Fallback used for {fallbackBoneNames.Count} bones: " +
                    $"{FormatBoneList(fallbackBoneNames, 8)}.");
            }

            ReportProgress(1.0f, "Done");

            return createdColliders;
        }

        public static List<MagicaCapsuleCollider> FindGeneratedColliders(GameObject avatarRoot)
        {
            var generatedColliders = new List<MagicaCapsuleCollider>();

            if (avatarRoot == null)
            {
                return generatedColliders;
            }

            var colliders = avatarRoot.GetComponentsInChildren<MagicaCapsuleCollider>(true);

            for (int i = 0; i < colliders.Length; ++i)
            {
                if (colliders[i] == null || colliders[i].gameObject == null) continue;

                if (colliders[i].gameObject.name.StartsWith(GeneratedColliderName, StringComparison.Ordinal))
                {
                    generatedColliders.Add(colliders[i]);
                }
            }

            return generatedColliders;
        }

        private static string CreateGeneratedColliderName(Transform sourceBone, bool fallback)
        {
            string boneName = sourceBone != null ? sourceBone.name : "Unknown";
            string suffix = fallback ? " (Fallback)" : string.Empty;

            return $"{GeneratedColliderName}_{boneName}{suffix}";
        }

        private void ReportProgress(float progress, string message)
        {
            m_ProgressReporter?.Invoke(Mathf.Clamp01(progress), message ?? string.Empty);
        }

        private List<Transform> CollectHumanBones()
        {
            var bones = new List<Transform>();
            var seen = new HashSet<Transform>();

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                HumanBodyBones boneId = (HumanBodyBones)i;

                if (!ShouldIncludeBone(boneId)) continue;

                var boneTransform = m_Animator.GetBoneTransform(boneId);

                if (boneTransform != null && seen.Add(boneTransform))
                {
                    bones.Add(boneTransform);
                }
            }

            return bones;
        }

        private bool ShouldIncludeBone(HumanBodyBones boneId)
        {
            if (IsFingerBone(boneId))
            {
                return m_Property.GenerationProperty.IncludeFingers;
            }

            if (boneId == HumanBodyBones.Hips)
            {
                return m_Property.GenerationProperty.IncludeHips;
            }

            if (boneId == HumanBodyBones.LeftShoulder || boneId == HumanBodyBones.RightShoulder)
            {
                return m_Property.GenerationProperty.IncludeShoulders;
            }

            if (boneId == HumanBodyBones.LeftToes || boneId == HumanBodyBones.RightToes)
            {
                return m_Property.GenerationProperty.IncludeToes;
            }

            if (boneId == HumanBodyBones.UpperChest)
            {
                return m_Property.GenerationProperty.IncludeUpperChest;
            }

            return boneId <= HumanBodyBones.RightToes;
        }

        private static bool IsFingerBone(HumanBodyBones boneId)
        {
            return boneId >= HumanBodyBones.LeftThumbProximal && boneId <= HumanBodyBones.RightLittleDistal;
        }

        private static string FormatBoneList(List<string> boneNames, int maxNames)
        {
            if (boneNames == null || boneNames.Count == 0)
            {
                return "none";
            }

            int count = Mathf.Min(maxNames, boneNames.Count);
            string result = string.Join(", ", boneNames.GetRange(0, count).ToArray());

            if (boneNames.Count > count)
            {
                result += $", +{boneNames.Count - count} more";
            }

            return result;
        }

        private List<MagicaCapsuleCollider> CreateCollidersFromResults(List<ColliderGenerationJob> jobs)
        {
            var createdColliders = new List<MagicaCapsuleCollider>();

            if (jobs == null || jobs.Count == 0)
            {
                return createdColliders;
            }

            for (int i = 0; i < jobs.Count; ++i)
            {
                var job = jobs[i];

                if (!ColliderFitter.TryFit(job, out var fitResult)) continue;

                var collider = CreateColliderGameObject(job, fitResult);

                if (collider != null)
                {
                    createdColliders.Add(collider);
                }
            }

            return createdColliders;
        }

        private static MagicaCapsuleCollider CreateColliderGameObject(ColliderGenerationJob job, CapsuleFitResult fitResult)
        {
            var colliderGameObject = new GameObject(CreateGeneratedColliderName(job.TargetBone.transform, false));
            Undo.RegisterCreatedObjectUndo(colliderGameObject, UndoGroupName);
            Undo.SetTransformParent(colliderGameObject.transform, job.TargetBone.transform, UndoGroupName);
            Undo.RecordObject(colliderGameObject.transform, UndoGroupName);
            colliderGameObject.transform.localPosition = Vector3.zero;
            colliderGameObject.transform.localRotation = fitResult.LocalRotation;
            colliderGameObject.transform.localScale = Vector3.one;

            var capsuleCollider = Undo.AddComponent<MagicaCapsuleCollider>(colliderGameObject);
            Undo.RecordObject(capsuleCollider, UndoGroupName);
            capsuleCollider.direction = fitResult.Direction;
            capsuleCollider.center = fitResult.Center;
            capsuleCollider.SetSize(fitResult.RadiusAtMin, fitResult.RadiusAtMax, fitResult.Length);
            capsuleCollider.reverseDirection = fitResult.ReverseDirection;
            capsuleCollider.UpdateParameters();

            return capsuleCollider;
        }

        private MagicaCapsuleCollider CreateDefaultColliderForBone(Transform boneTransform)
        {
            var colliderGameObject = new GameObject(CreateGeneratedColliderName(boneTransform, true));
            Undo.RegisterCreatedObjectUndo(colliderGameObject, UndoGroupName);
            Undo.SetTransformParent(colliderGameObject.transform, boneTransform, UndoGroupName);
            Undo.RecordObject(colliderGameObject.transform, UndoGroupName);
            colliderGameObject.transform.localPosition = Vector3.zero;
            colliderGameObject.transform.localRotation = Quaternion.identity;
            colliderGameObject.transform.localScale = Vector3.one;
            var capsuleCollider = Undo.AddComponent<MagicaCapsuleCollider>(colliderGameObject);
            Undo.RecordObject(capsuleCollider, UndoGroupName);

            if (boneTransform.childCount > 0)
            {
                float maxDistance = 0f;
                Vector3 longestChildLocal = Vector3.up * 0.05f;

                foreach (Transform child in boneTransform)
                {
                    Vector3 childLocal = child.localPosition;
                    float distance = childLocal.magnitude;

                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        longestChildLocal = childLocal;
                    }
                }

                float radius = Mathf.Clamp(maxDistance * 0.18f, 0.01f, 0.08f);
                float length = Mathf.Max(maxDistance, radius * 2.0f);

                if (ColliderFitter.DetectBoneFitRole(boneTransform) == BoneFitRole.UpperChest)
                {
                    radius *= 1.1f;
                }

                if (longestChildLocal.sqrMagnitude > 1.0e-8f)
                {
                    colliderGameObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, longestChildLocal.normalized);
                }

                capsuleCollider.SetSize(radius, radius, length);
                capsuleCollider.center = new Vector3(0.0f, length * 0.5f, 0.0f);
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
                capsuleCollider.UpdateParameters();
                return capsuleCollider;
            }
            else
            {
                float defaultRadius = 0.02f;
                float defaultLength = 0.02f;

                if (ColliderFitter.DetectBoneFitRole(boneTransform) == BoneFitRole.UpperChest)
                {
                    defaultRadius = 0.03f;
                    defaultLength = 0.04f;
                }

                capsuleCollider.SetSize(defaultRadius, defaultRadius, defaultLength);
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
                capsuleCollider.UpdateParameters();
                return capsuleCollider;
            }
        }
    }
}
