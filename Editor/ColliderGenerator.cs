using MagicaCloth2;
using System.Collections.Generic;
using System.Linq;
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

                var result = job.Result;
                float sizeX = Mathf.Abs(result.BoxB.x - result.BoxA.x);
                float sizeY = Mathf.Abs(result.BoxB.y - result.BoxA.y);
                float sizeZ = Mathf.Abs(result.BoxB.z - result.BoxA.z);
                var center = FuzzyZero((result.BoxA + result.BoxB) / 2.0f) + result.Center;
                bool hasDirectionHint = TryGetChildDirectionHint(job.TargetBone.transform, out Vector3 directionHint);

                var colliderGo = new GameObject("MagicaClothCollider");
                colliderGo.transform.parent = job.TargetBone.transform;
                colliderGo.transform.localPosition = Vector3.zero;
                colliderGo.transform.localRotation = Quaternion.identity;
                colliderGo.transform.localScale = Vector3.one;

                var capsule = colliderGo.AddComponent<MagicaCapsuleCollider>();
                capsule.center = center;

                float length;
                int direction;
                Vector3 axisVec;

                if (hasDirectionHint)
                {
                    float absX = Mathf.Abs(directionHint.x);
                    float absY = Mathf.Abs(directionHint.y);
                    float absZ = Mathf.Abs(directionHint.z);

                    if (absX >= absY && absX >= absZ)
                    {
                        capsule.direction = MagicaCapsuleCollider.Direction.X;
                        length = sizeX;
                        direction = 0;
                        axisVec = Vector3.right;
                    }
                    else if (absY >= absX && absY >= absZ)
                    {
                        capsule.direction = MagicaCapsuleCollider.Direction.Y;
                        length = sizeY;
                        direction = 1;
                        axisVec = Vector3.up;
                    }
                    else
                    {
                        capsule.direction = MagicaCapsuleCollider.Direction.Z;
                        length = sizeZ;
                        direction = 2;
                        axisVec = Vector3.forward;
                    }
                }
                else if (sizeX > sizeY && sizeX > sizeZ)
                {
                    capsule.direction = MagicaCapsuleCollider.Direction.X;
                    length = sizeX;
                    direction = 0;
                    axisVec = Vector3.right;
                }
                else if (sizeY > sizeX && sizeY > sizeZ)
                {
                    capsule.direction = MagicaCapsuleCollider.Direction.Y;
                    length = sizeY;
                    direction = 1;
                    axisVec = Vector3.up;
                }
                else
                {
                    capsule.direction = MagicaCapsuleCollider.Direction.Z;
                    length = sizeZ;
                    direction = 2;
                    axisVec = Vector3.forward;
                }

                ComputeAxisRadii(job.Vertices, center, length, direction, job.Property.ReducerProperty.FitType, out var radiusAtMin, out var radiusAtMax);

                Vector3 posMin = center - (0.5f * length * axisVec);
                Vector3 posMax = center + (0.5f * length * axisVec);

                capsule.SetSize(radiusAtMin, radiusAtMax, length);
                capsule.reverseDirection = DetermineReverseDirection(job.TargetBone.transform, posMin, posMax, hasDirectionHint, directionHint, direction);
                capsule.UpdateParameters();
            }
        }

        private static bool DetermineReverseDirection(
            Transform boneTransform,
            Vector3 endpointMin,
            Vector3 endpointMax,
            bool hasDirectionHint,
            Vector3 directionHint,
            int direction)
        {
            if (hasDirectionHint)
            {
                if (direction == 0)
                {
                    return directionHint.x < 0.0f;
                }

                if (direction == 1)
                {
                    return directionHint.y < 0.0f;
                }

                return directionHint.z < 0.0f;
            }

            if (boneTransform != null && boneTransform.childCount > 0)
            {
                Vector3 averageChildPosition = Vector3.zero;

                for (int i = 0; i < boneTransform.childCount; ++i)
                {
                    averageChildPosition += boneTransform.GetChild(i).localPosition;
                }

                averageChildPosition /= boneTransform.childCount;

                if (averageChildPosition.sqrMagnitude > 1.0e-8f)
                {
                    float distanceToMin = (averageChildPosition - endpointMin).sqrMagnitude;
                    float distanceToMax = (averageChildPosition - endpointMax).sqrMagnitude;
                    return distanceToMin < distanceToMax;
                }
            }

            return endpointMin.sqrMagnitude < endpointMax.sqrMagnitude;
        }

        private static bool TryGetChildDirectionHint(Transform boneTransform, out Vector3 directionHint)
        {
            directionHint = Vector3.zero;

            if (boneTransform == null || boneTransform.childCount == 0)
            {
                return false;
            }

            for (int i = 0; i < boneTransform.childCount; ++i)
            {
                directionHint += boneTransform.GetChild(i).localPosition;
            }

            directionHint /= boneTransform.childCount;
            return directionHint.sqrMagnitude > 1.0e-8f;
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
                capsuleCollider.SetSize(radius, radius, 0.01f);
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
            }
            else
            {
                const float DEFAULT_RADIUS = 0.02f;
                const float DEFAULT_LENGTH = 0.02f;
                capsuleCollider.SetSize(DEFAULT_RADIUS, DEFAULT_RADIUS, DEFAULT_LENGTH);
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