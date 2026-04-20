using MagicaCloth2;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class ColliderGenerator
    {
        private readonly GameObject m_AvatarRoot;
        private readonly SABoneColliderProperty m_Property;
        private readonly Animator m_Animator;

        public ColliderGenerator(GameObject avatarRoot, SABoneColliderProperty properties)
        {
            m_AvatarRoot = avatarRoot;
            m_Property = properties;
            m_Animator = avatarRoot.GetComponent<Animator>();
        }

        public List<MagicaCapsuleCollider> Process()
        {
            var createdColliders = new List<MagicaCapsuleCollider>();

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

            var bonesToProcess = CollectHumanBones();

            if (bonesToProcess.Count == 0)
            {
                Debug.LogWarning("No human bones found to process.");
                return createdColliders;
            }

            var boneMeshCache = new BoneMeshCache();
            boneMeshCache.Process(m_AvatarRoot);

            if (boneMeshCache.MeshBoneCount == 0)
            {
                Debug.LogError("No skinned meshes found to generate colliders from.");
                return createdColliders;
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

            createdColliders.AddRange(CreateCollidersFromResults(generationJobs));

            foreach (var boneToFix in bonesWithoutMesh)
            {
                var fallbackCollider = CreateDefaultColliderForBone(boneToFix);

                if (fallbackCollider != null)
                {
                    createdColliders.Add(fallbackCollider);
                }
            }

            Debug.Log($"Collider generation complete. Created {createdColliders.Count} colliders.");

            return createdColliders;
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
            return
                boneId == HumanBodyBones.LeftThumbProximal || boneId == HumanBodyBones.LeftThumbIntermediate || boneId == HumanBodyBones.LeftThumbDistal ||
                boneId == HumanBodyBones.LeftIndexProximal || boneId == HumanBodyBones.LeftIndexIntermediate || boneId == HumanBodyBones.LeftIndexDistal ||
                boneId == HumanBodyBones.LeftMiddleProximal || boneId == HumanBodyBones.LeftMiddleIntermediate || boneId == HumanBodyBones.LeftMiddleDistal ||
                boneId == HumanBodyBones.LeftRingProximal || boneId == HumanBodyBones.LeftRingIntermediate || boneId == HumanBodyBones.LeftRingDistal ||
                boneId == HumanBodyBones.LeftLittleProximal || boneId == HumanBodyBones.LeftLittleIntermediate || boneId == HumanBodyBones.LeftLittleDistal ||
                boneId == HumanBodyBones.RightThumbProximal || boneId == HumanBodyBones.RightThumbIntermediate || boneId == HumanBodyBones.RightThumbDistal ||
                boneId == HumanBodyBones.RightIndexProximal || boneId == HumanBodyBones.RightIndexIntermediate || boneId == HumanBodyBones.RightIndexDistal ||
                boneId == HumanBodyBones.RightMiddleProximal || boneId == HumanBodyBones.RightMiddleIntermediate || boneId == HumanBodyBones.RightMiddleDistal ||
                boneId == HumanBodyBones.RightRingProximal || boneId == HumanBodyBones.RightRingIntermediate || boneId == HumanBodyBones.RightRingDistal ||
                boneId == HumanBodyBones.RightLittleProximal || boneId == HumanBodyBones.RightLittleIntermediate || boneId == HumanBodyBones.RightLittleDistal;
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

        private List<MagicaCapsuleCollider> CreateCollidersFromResults(List<ColliderGenerationJob> jobs)
        {
            var createdColliders = new List<MagicaCapsuleCollider>();

            foreach (var job in jobs)
            {
                if (!ColliderCapsuleFitter.TryFitCapsule(job, out var fitResult)) continue;

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