using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCreator
    {
        private GameObject m_BoneGameObject;
        private SplitProperty m_SplitProperty;
        private BoneMeshCache m_BoneMeshCache;
        private int m_MeshVertexCount;
        private int m_MeshBoneCount;
        private Matrix4x4[] m_MeshBindPoses;
        private Transform[] m_MeshBones;
        private BoneWeight[] m_MeshBoneWeights;
        private Vector3[] m_MeshVertices;
        private int[] m_MeshTriangles;
        private bool[] m_TargetBones;
        private bool[] m_ProcessedVertices;
        private Vector3[] m_BoneVertices;
        private int[] m_BoneTriangles;

        public Vector3[] BoneVertices => m_BoneVertices;

        public int[] BoneTriangles => m_BoneTriangles;

        public bool Process(GameObject boneGameObject, SplitProperty splitProperty, BoneMeshCache boneMeshCache)
        {
            if (boneGameObject == null || splitProperty == null || boneMeshCache == null) return false;

            boneMeshCache.CleanWork();

            if (boneMeshCache.MeshBoneCount == 0 || boneMeshCache.MeshVertexCount == 0 || boneMeshCache.MeshTriangleCount == 0)
            {
                return false;
            }

            InitializeState(boneGameObject, splitProperty, boneMeshCache);

            return ProcessInternal();
        }

        private void InitializeState(GameObject boneGameObject, SplitProperty splitProperty, BoneMeshCache boneMeshCache)
        {
            m_BoneGameObject = boneGameObject;
            m_SplitProperty = splitProperty;
            m_BoneMeshCache = boneMeshCache;
            m_MeshVertexCount = boneMeshCache.MeshVertexCount;
            m_MeshBoneCount = boneMeshCache.MeshBoneCount;
            m_MeshBones = boneMeshCache.MeshBones;
            m_MeshBindPoses = boneMeshCache.MeshBindPoses;
            m_MeshBoneWeights = boneMeshCache.MeshBoneWeights;
            m_MeshVertices = boneMeshCache.MeshVertices;
            m_MeshTriangles = boneMeshCache.MeshTriangles;
            m_TargetBones = boneMeshCache.TargetBones;
            m_ProcessedVertices = boneMeshCache.ProcessedVertices;
        }

        private void RebuildTargetBones(Transform boneTransform)
        {
            for (int i = 0; i < m_MeshBoneCount; ++i)
            {
                var currentBone = m_MeshBones[i];

                if (currentBone == null) continue;

                if (currentBone == boneTransform || currentBone.parent == boneTransform)
                {
                    m_TargetBones[i] = true;
                }
            }
        }

        private bool ProcessInternal()
        {
            RebuildTargetBones(m_BoneGameObject.transform);

            if (!PopulateTargetVertices()) return false;

            if (m_SplitProperty.BoneTriangleExtent != BoneTriangleExtent.Disable)
            {
                ExtendTargetVerticesByTriangles();
            }

            if (!TryCollectPassedTriangles(out var passedTriangles)) return false;

            RebuildLocalMesh(passedTriangles);

            return true;
        }

        private bool PopulateTargetVertices()
        {
            int passedVertexCount = 0;
            var targetVertex = m_BoneMeshCache.TargetVertices;
            var boneIndices = m_BoneMeshCache.BoneIndices;
            var boneWeights = m_MeshBoneWeights;
            var weightThresholds = GetWeightThresholds();
            var boneWeightArray = new float[4];
            var boneIndexArray = new int[4];

            for (int i = 0; i < m_MeshVertexCount; ++i)
            {
                if (m_ProcessedVertices[i]) continue;

                CopyBoneWeight(boneWeights[i], boneWeightArray, boneIndexArray);

                int targetBoneIndex = ResolveTargetBoneIndex(boneWeightArray, boneIndexArray, weightThresholds);

                if (targetBoneIndex == -1) continue;

                boneIndices[i] = targetBoneIndex;
                targetVertex[i] = true;

                m_ProcessedVertices[i] = true;

                ++passedVertexCount;
            }

            return passedVertexCount > 0;
        }

        private float[] GetWeightThresholds()
        {
            return new float[5]
            {
                0.0f,
                0.0f,
                m_SplitProperty.BoneWeight2 * 0.01f,
                m_SplitProperty.BoneWeight3 * 0.01f,
                m_SplitProperty.BoneWeight4 * 0.01f,
            };
        }

        private static void CopyBoneWeight(BoneWeight boneWeight, float[] boneWeightArray, int[] boneIndexArray)
        {
            boneWeightArray[0] = boneWeight.weight0;
            boneWeightArray[1] = boneWeight.weight1;
            boneWeightArray[2] = boneWeight.weight2;
            boneWeightArray[3] = boneWeight.weight3;
            boneIndexArray[0] = boneWeight.boneIndex0;
            boneIndexArray[1] = boneWeight.boneIndex1;
            boneIndexArray[2] = boneWeight.boneIndex2;
            boneIndexArray[3] = boneWeight.boneIndex3;
        }

        private int ResolveTargetBoneIndex(float[] boneWeightArray, int[] boneIndexArray, float[] weightThresholds)
        {
            int boneCount = 0;

            for (int n = 0; n < 4; ++n)
            {
                if (boneIndexArray[n] >= 0 && boneWeightArray[n] > 0.0f)
                {
                    ++boneCount;
                }
            }

            for (int n = 0; n < 4; ++n)
            {
                int boneIndex = boneIndexArray[n];

                if (boneIndex >= 0 && m_TargetBones[boneIndex] && boneWeightArray[n] > weightThresholds[boneCount])
                {
                    return boneIndex;
                }
            }

            if (!m_SplitProperty.GreaterBoneWeight)
            {
                return -1;
            }

            return ResolveDominantTargetBoneIndex(boneWeightArray, boneIndexArray);
        }

        private int ResolveDominantTargetBoneIndex(float[] boneWeightArray, int[] boneIndexArray)
        {
            int dominantBoneIndex = -1;
            float dominantBoneWeight = 0.0f;
            bool dominantBoneIsTarget = false;

            for (int n = 0; n < 4; ++n)
            {
                int boneIndex = boneIndexArray[n];

                if (boneIndex < 0) continue;

                float currentWeight = boneWeightArray[n];

                if (currentWeight > dominantBoneWeight)
                {
                    dominantBoneIndex = boneIndex;
                    dominantBoneWeight = currentWeight;
                    dominantBoneIsTarget = m_TargetBones[dominantBoneIndex];
                }
                else if (currentWeight == dominantBoneWeight && !dominantBoneIsTarget)
                {
                    dominantBoneIndex = boneIndex;
                    dominantBoneWeight = currentWeight;
                    dominantBoneIsTarget = m_TargetBones[dominantBoneIndex];
                }
            }

            return dominantBoneIsTarget ? dominantBoneIndex : -1;
        }

        private void ExtendTargetVerticesByTriangles()
        {
            int extentVertexCount = GetExtentVertexCount();

            if (extentVertexCount == 0) return;

            var targetVertex = m_BoneMeshCache.TargetVertices;
            var boneIndices = m_BoneMeshCache.BoneIndices;

            for (int i = 0; i + 2 < m_MeshTriangles.Length; i += 3)
            {
                int index0 = m_MeshTriangles[i + 0];
                int index1 = m_MeshTriangles[i + 1];
                int index2 = m_MeshTriangles[i + 2];

                bool pv0 = m_ProcessedVertices[index0];
                bool pv1 = m_ProcessedVertices[index1];
                bool pv2 = m_ProcessedVertices[index2];
                int targetVertexCount = (pv0 ? 1 : 0) + (pv1 ? 1 : 0) + (pv2 ? 1 : 0);

                if (targetVertexCount == 3 || targetVertexCount < extentVertexCount) continue;

                int replicateBoneIndex = GetReplicateBoneIndex(index0, index1, index2, pv0, pv1, pv2, boneIndices);

                if (replicateBoneIndex < 0)
                {
                    continue;
                }

                if (boneIndices[index0] == -1) boneIndices[index0] = replicateBoneIndex;
                if (boneIndices[index1] == -1) boneIndices[index1] = replicateBoneIndex;
                if (boneIndices[index2] == -1) boneIndices[index2] = replicateBoneIndex;

                targetVertex[index0] = true;
                targetVertex[index1] = true;
                targetVertex[index2] = true;
            }

            for (int i = 0; i < m_MeshVertexCount; ++i)
            {
                m_ProcessedVertices[i] |= targetVertex[i];
            }
        }

        private int GetExtentVertexCount()
        {
            if (m_SplitProperty.BoneTriangleExtent == BoneTriangleExtent.Vertex1) return 1;

            if (m_SplitProperty.BoneTriangleExtent == BoneTriangleExtent.Vertex2) return 2;

            return 0;
        }

        private static int GetReplicateBoneIndex(int index0, int index1, int index2, bool pv0, bool pv1, bool pv2, int[] boneIndices)
        {
            if (pv0 && boneIndices[index0] != -1)
            {
                return boneIndices[index0];
            }

            if (pv1 && boneIndices[index1] != -1)
            {
                return boneIndices[index1];
            }

            if (pv2 && boneIndices[index2] != -1)
            {
                return boneIndices[index2];
            }

            return -1;
        }

        private bool TryCollectPassedTriangles(out List<int> passedTriangles)
        {
            var targetVertex = m_BoneMeshCache.TargetVertices;
            var passedVertex = m_BoneMeshCache.PassedVertices;
            passedTriangles = new List<int>();

            for (int i = 0; i + 2 < m_MeshTriangles.Length; i += 3)
            {
                int index0 = m_MeshTriangles[i + 0];
                int index1 = m_MeshTriangles[i + 1];
                int index2 = m_MeshTriangles[i + 2];

                if (!targetVertex[index0] || !targetVertex[index1] || !targetVertex[index2]) continue;

                passedTriangles.Add(index0);
                passedTriangles.Add(index1);
                passedTriangles.Add(index2);
                passedVertex[index0] = true;
                passedVertex[index1] = true;
                passedVertex[index2] = true;
            }

            if (passedTriangles.Count > 0) return true;

            return TryMarkTargetVerticesAsPassed(targetVertex, passedVertex);
        }

        private bool TryMarkTargetVerticesAsPassed(bool[] targetVertex, bool[] passedVertex)
        {
            bool hasTargetVertices = false;

            for (int i = 0; i < m_MeshVertexCount; i++)
            {
                if (!targetVertex[i]) continue;

                passedVertex[i] = true;
                hasTargetVertices = true;
            }

            return hasTargetVertices;
        }

        private void RebuildLocalMesh(List<int> passedTriangles)
        {
            var passedVertex = m_BoneMeshCache.PassedVertices;
            var boneIndices = m_BoneMeshCache.BoneIndices;
            var redirectIndex = m_BoneMeshCache.RedirectIndices;

            for (int i = 0; i < redirectIndex.Length; ++i)
            {
                redirectIndex[i] = -1;
            }

            int remakeVertexCount = 0;

            for (int i = 0; i < m_MeshVertexCount; ++i)
            {
                if (!passedVertex[i]) continue;

                int boneIndex = boneIndices[i];

                if (boneIndex < 0 || boneIndex >= m_MeshBindPoses.Length)
                {
                    passedVertex[i] = false;
                    continue;
                }

                ++remakeVertexCount;
            }

            var remakeVertices = new Vector3[remakeVertexCount];

            for (int i = 0, index = 0; i < m_MeshVertexCount; ++i)
            {
                if (!passedVertex[i]) continue;

                int boneIndex = boneIndices[i];

                if (boneIndex < 0 || boneIndex >= m_MeshBindPoses.Length)
                {
                    continue;
                }

                Matrix4x4 matrix = m_MeshBindPoses[boneIndex];
                remakeVertices[index] = matrix.MultiplyPoint(m_MeshVertices[i]);
                redirectIndex[i] = index;
                ++index;
            }

            var remappedTriangles = new List<int>(passedTriangles.Count);

            for (int i = 0; i + 2 < passedTriangles.Count; i += 3)
            {
                int index0 = redirectIndex[passedTriangles[i + 0]];
                int index1 = redirectIndex[passedTriangles[i + 1]];
                int index2 = redirectIndex[passedTriangles[i + 2]];

                if (index0 < 0 || index1 < 0 || index2 < 0)
                {
                    continue;
                }

                remappedTriangles.Add(index0);
                remappedTriangles.Add(index1);
                remappedTriangles.Add(index2);
            }

            m_BoneVertices = remakeVertices;
            m_BoneTriangles = remappedTriangles.ToArray();
        }
    }
}