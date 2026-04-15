using MagicaCloth2;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class ColliderGenerator
    {
        private readonly GameObject m_avatarRoot;
        private readonly SABoneColliderProperty m_property;
        private readonly Animator m_animator;

        public ColliderGenerator(GameObject avatarRoot, SABoneColliderProperty properties)
        {
            m_avatarRoot = avatarRoot;
            m_property = properties;
            m_animator = avatarRoot.GetComponent<Animator>();
        }

        public void Process()
        {
            if (m_animator == null || m_animator.avatar == null || !m_animator.avatar.isHuman)
            {
                Debug.LogError("Animator with a valid Humanoid Avatar is required on the root object.");
                return;
            }

            // 1. Collect all valid human bone transforms
            var bonesToProcess = CollectHumanBones();
            if (bonesToProcess.Count == 0)
            {
                Debug.LogWarning("No human bones found to process.");
                return;
            }

            // 2. Pre-process and cache all mesh data
            var boneMeshCache = new BoneMeshCache();
            boneMeshCache.Process(m_avatarRoot);

            if (boneMeshCache.MeshBoneCount == 0)
            {
                Debug.LogError("No skinned meshes found to generate colliders from.");
                return;
            }

            // 3. Create a generation job for each bone
            var generationJobs = new List<ColliderGenerationJob>();
            var bonesWithoutMesh = new List<Transform>();

            foreach (Transform boneTransform in bonesToProcess)
            {
                var job = new ColliderGenerationJob(boneTransform.gameObject, m_property, boneMeshCache);
                if (job.Prepare())
                {
                    generationJobs.Add(job);
                }
                else
                {
                    bonesWithoutMesh.Add(boneTransform);
                }
            }

            // 4. Execute jobs in parallel
            ExecuteJobs(generationJobs);

            // 5. Create collider components from results
            CreateCollidersFromResults(generationJobs);

            // 6. Create fallback colliders for bones that had no mesh
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
                    var boneTransform = m_animator.GetBoneTransform((HumanBodyBones)i);

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
                job.CountdownEvent = countdownEvent;
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

                if (sizeX > sizeY && sizeX > sizeZ)
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
                capsule.reverseDirection = posMin.sqrMagnitude < posMax.sqrMagnitude;
                capsule.UpdateParameters();
            }
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

            // If the bone has children, create a sphere that encompasses them.
            if (boneTransform.childCount > 0)
            {
                float maxDist = 0f;
                foreach (Transform child in boneTransform)
                {
                    float dist = Vector3.Distance(boneTransform.position, child.position);
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                    }
                }
                // Create a sphere (a capsule with equal radii and small length)
                float radius = maxDist > 0.001f ? maxDist : 0.05f;
                capsuleCollider.SetSize(radius, radius, 0.01f);
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
            }
            else // Otherwise, create a small default capsule.
            {
                const float defaultRadius = 0.02f;
                const float defaultLength = 0.02f;
                capsuleCollider.SetSize(defaultRadius, defaultRadius, defaultLength);
                capsuleCollider.center = Vector3.zero;
                capsuleCollider.direction = MagicaCapsuleCollider.Direction.Y;
            }
        }

        static void ComputeAxisRadii(
            Vector3[] vertices,
            Vector3 center,
            float length,
            int direction, // 0:X, 1:Y, 2:Z
            FitType fitType,
            out float radiusAtMin,
            out float radiusAtMax)
        {
            radiusAtMin = 0f;
            radiusAtMax = 0f;

            if (vertices == null || vertices.Length == 0 || length < 0.001f)
            {
                radiusAtMin = radiusAtMax = 0.01f;
                return;
            }

            const int numSlices = 10;
            var maxRadiusPerSlice = new float[numSlices];
            var avgRadiusPerSlice = new List<float>[numSlices];
            for (int i = 0; i < numSlices; i++)
            {
                avgRadiusPerSlice[i] = new List<float>();
            }

            float halfLen = length / 2.0f;
            float sliceWidth = length / numSlices;

            foreach (var v in vertices)
            {
                Vector3 d = v - center;
                float axisPos, distSq;

                if (direction == 0) { axisPos = d.x; distSq = (d.y * d.y) + (d.z * d.z); }
                else if (direction == 1) { axisPos = d.y; distSq = (d.x * d.x) + (d.z * d.z); }
                else { axisPos = d.z; distSq = (d.x * d.x) + (d.y * d.y); }

                float dist = Mathf.Sqrt(distSq);

                int sliceIndex = Mathf.FloorToInt((axisPos + halfLen) / sliceWidth);
                sliceIndex = Mathf.Clamp(sliceIndex, 0, numSlices - 1);

                if (dist > maxRadiusPerSlice[sliceIndex])
                {
                    maxRadiusPerSlice[sliceIndex] = dist;
                }
                avgRadiusPerSlice[sliceIndex].Add(dist);
            }

            // Use the first 20% of slices for the min radius
            int endSliceForMin = Mathf.Max(1, numSlices / 5);
            // Use the last 20% of slices for the max radius
            int startSliceForMax = Mathf.Min(numSlices - 1, numSlices - endSliceForMin);

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
                for (int i = startSliceForMax; i < numSlices; i++)
                {
                    if (avgRadiusPerSlice[i].Count > 0)
                    {
                        maxSum += avgRadiusPerSlice[i].Average();
                        maxSumCount++;
                    }
                }
                radiusAtMax = (maxSumCount > 0) ? maxSum / maxSumCount : 0f;
            }
            else // Outer fit
            {
                for (int i = 0; i < endSliceForMin; i++)
                {
                    if (maxRadiusPerSlice[i] > radiusAtMin) radiusAtMin = maxRadiusPerSlice[i];
                }
                for (int i = startSliceForMax; i < numSlices; i++)
                {
                    if (maxRadiusPerSlice[i] > radiusAtMax) radiusAtMax = maxRadiusPerSlice[i];
                }
            }

            // If one end has no vertices, use the other end's radius
            if (radiusAtMin <= 0.001f && radiusAtMax > 0.001f) radiusAtMin = radiusAtMax;
            if (radiusAtMax <= 0.001f && radiusAtMin > 0.001f) radiusAtMax = radiusAtMin;

            // Ensure a minimum thickness
            radiusAtMin = Mathf.Max(radiusAtMin, 0.01f);
            radiusAtMax = Mathf.Max(radiusAtMax, 0.01f);
        }

        public static Vector3 FuzzyZero(Vector3 v)
        {
            if (Mathf.Abs(v.x) <= 0.0001f) { v.x = 0; }
            if (Mathf.Abs(v.y) <= 0.0001f) { v.y = 0; }
            if (Mathf.Abs(v.z) <= 0.0001f) { v.z = 0; }
            return v;
        }
    }
}