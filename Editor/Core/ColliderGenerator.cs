using MagicaCloth2;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class ColliderGenerator
    {
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

            ReportProgress(0.02f, "Validating avatar settings...");

            if (m_Animator == null || m_Animator.avatar == null || !m_Animator.avatar.isHuman)
            {
                Debug.LogError("Animator with a valid Humanoid Avatar is required on the root object.");

                return createdColliders;
            }

            if (m_AvatarRoot.GetComponentsInChildren<MagicaCapsuleCollider>(true).Length > 0)
            {
                Debug.LogWarning("Generation skipped: existing MagicaCapsuleCollider components were found. Please cleanup first if you want to regenerate.");

                return createdColliders;
            }

            ReportProgress(0.10f, "Collecting humanoid bones...");

            var bonesToProcess = CollectHumanBones();

            if (bonesToProcess.Count == 0)
            {
                Debug.LogWarning("No human bones found to process.");

                return createdColliders;
            }

            ReportProgress(0.18f, "Building mesh cache...");

            var boneMeshCache = new BoneMeshCache();
            boneMeshCache.Process(m_AvatarRoot, m_CustomSkinnedMeshes);

            if (boneMeshCache.MeshBoneCount == 0)
            {
                Debug.LogError("No skinned meshes found to generate colliders from.");

                return createdColliders;
            }

            var generationJobs = new List<ColliderGenerationJob>();
            var bonesWithoutMesh = new List<Transform>();

            ReportProgress(0.26f, "Preparing per-bone jobs...");

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

                if (boneCount > 0 && ((i & 3) == 0 || i + 1 == boneCount))
                {
                    float t = (i + 1) / (float)boneCount;

                    ReportProgress(Mathf.Lerp(0.26f, 0.58f, t), $"Preparing jobs ({i + 1}/{boneCount})...");
                }
            }

            ReportProgress(0.62f, "Reducing meshes in parallel...");

            ExecuteJobs(generationJobs);

            ReportProgress(0.74f, "Creating colliders...");

            createdColliders.AddRange(CreateCollidersFromResults(generationJobs));

            for (int i = 0; i < bonesWithoutMesh.Count; ++i)
            {
                var boneToFix = bonesWithoutMesh[i];
                var fallbackCollider = CreateDefaultColliderForBone(boneToFix);

                if (fallbackCollider != null)
                {
                    createdColliders.Add(fallbackCollider);
                }

                if (bonesWithoutMesh.Count > 0 && ((i & 3) == 0 || i + 1 == bonesWithoutMesh.Count))
                {
                    float t = (i + 1) / (float)bonesWithoutMesh.Count;

                    ReportProgress(Mathf.Lerp(0.90f, 0.98f, t), $"Creating fallback colliders ({i + 1}/{bonesWithoutMesh.Count})...");
                }
            }

            Debug.Log($"Collider generation complete. Created {createdColliders.Count} colliders.");

            ReportProgress(1.0f, "Done.");

            return createdColliders;
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

                if (!ShouldIncludeBone(boneId))
                {
                    continue;
                }

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

        private void ExecuteJobs(List<ColliderGenerationJob> jobs)
        {
            if (jobs == null || jobs.Count == 0) return;

            using var countdownEvent = new CountdownEvent(jobs.Count);

            foreach (var job in jobs)
            {
                job.m_CountdownEvent = countdownEvent;

                if (!ThreadPool.QueueUserWorkItem(job.Execute))
                {
                    countdownEvent.Signal();

                    Debug.LogError($"Failed to queue collider generation job for bone '{job.TargetBone.name}'.");
                }
            }

            if (!countdownEvent.Wait(System.TimeSpan.FromSeconds(30.0f)))
            {
                Debug.LogError("Collider generation jobs timed out after 30 seconds.");
            }
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

                if (!ColliderCapsuleFitter.TryFitCapsule(job, out var fitResult)) continue;

                var collider = CreateColliderGameObject(job, fitResult);

                if (collider != null)
                {
                    createdColliders.Add(collider);
                }

                if ((i & 3) == 0 || i + 1 == jobs.Count)
                {
                    float t = (i + 1) / (float)jobs.Count;

                    ReportProgress(Mathf.Lerp(0.74f, 0.90f, t), $"Fitting colliders ({i + 1}/{jobs.Count})...");
                }
            }

            return createdColliders;
        }

        private static MagicaCapsuleCollider CreateColliderGameObject(ColliderGenerationJob job, CapsuleFitResult fitResult)
        {
            var colliderGameObject = new GameObject("MagicaClothCollider");
            colliderGameObject.transform.parent = job.TargetBone.transform;
            colliderGameObject.transform.localPosition = Vector3.zero;
            colliderGameObject.transform.localRotation = fitResult.LocalRotation;
            colliderGameObject.transform.localScale = Vector3.one;

            var capsuleCollider = colliderGameObject.AddComponent<MagicaCapsuleCollider>();
            capsuleCollider.direction = fitResult.Direction;
            capsuleCollider.center = fitResult.Center;
            capsuleCollider.SetSize(fitResult.RadiusAtMin, fitResult.RadiusAtMax, fitResult.Length);
            capsuleCollider.reverseDirection = fitResult.ReverseDirection;
            capsuleCollider.UpdateParameters();

            return capsuleCollider;
        }

        private MagicaCapsuleCollider CreateDefaultColliderForBone(Transform boneTransform)
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

                if (ColliderCapsuleFitter.DetectBoneFitRole(boneTransform) == BoneFitRole.UpperChest)
                {
                    radius *= 1.1f;
                }

                capsuleCollider.SetSize(radius, radius, 0.01f);
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
                return capsuleCollider;
            }
            else
            {
                float defaultRadius = 0.02f;
                float defaultLength = 0.02f;

                if (ColliderCapsuleFitter.DetectBoneFitRole(boneTransform) == BoneFitRole.UpperChest)
                {
                    defaultRadius = 0.03f;
                    defaultLength = 0.04f;
                }

                capsuleCollider.SetSize(defaultRadius, defaultRadius, defaultLength);
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
                return capsuleCollider;
            }
        }
    }
}