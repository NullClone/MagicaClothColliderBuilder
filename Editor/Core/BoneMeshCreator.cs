using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCreator
    {
        // Fields

        private GameObject m_BoneGameObject;
        private SplitProperty m_SplitProperty;
        private BoneMeshCache m_BoneMeshCache;
        private Vector3[] m_BoneVertices;
        private int[] m_BoneTriangles;


        // Properties

        public Vector3[] BoneVertices => m_BoneVertices;

        public int[] BoneTriangles => m_BoneTriangles;


        // Methods

        public bool Process(GameObject boneGameObject, SplitProperty splitProperty, BoneMeshCache boneMeshCache)
        {
            if (boneGameObject == null || splitProperty == null || boneMeshCache == null) return false;

            boneMeshCache.Clear();

            if (boneMeshCache.MeshBoneCount == 0 || boneMeshCache.MeshVertexCount == 0 || boneMeshCache.MeshTriangleCount == 0)
            {
                return false;
            }

            m_BoneGameObject = boneGameObject;
            m_SplitProperty = splitProperty;
            m_BoneMeshCache = boneMeshCache;

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

        private void RebuildTargetBones(Transform transform)
        {
            var meshBones = m_BoneMeshCache.MeshBones;
            var targetBones = m_BoneMeshCache.TargetBones;

            for (int i = 0; i < m_BoneMeshCache.MeshBoneCount; ++i)
            {
                var currentBone = meshBones[i];

                if (currentBone == null) continue;

                if (currentBone == transform || currentBone.parent == transform)
                {
                    targetBones[i] = true;
                }
            }
        }

        private bool PopulateTargetVertices()
        {
            int passedVertexCount = 0;
            var targetVertex = m_BoneMeshCache.TargetVertices;
            var targetBones = m_BoneMeshCache.TargetBones;
            var boneIndices = m_BoneMeshCache.BoneIndices;
            var boneWeights = m_BoneMeshCache.MeshBoneWeights;
            var processedVertices = m_BoneMeshCache.ProcessedVertices;
            var boneVertexCandidates = m_BoneMeshCache.BoneVertexCandidates;
            var boneWeightArray = new float[4];
            var boneIndexArray = new int[4];
            int influenceLimit = m_SplitProperty.BoneWeightType == BoneWeightType.Bone2 ? 2 : 4;
            var weightThresholds = new float[5]
            {
                0f,
                0f,
                m_SplitProperty.BoneWeight2 * 0.01f,
                m_SplitProperty.BoneWeight3 * 0.01f,
                m_SplitProperty.BoneWeight4 * 0.01f,
            };

            if (boneVertexCandidates == null || boneVertexCandidates.Length == 0)
            {
                return PopulateTargetVerticesByFullScan(
                    targetVertex,
                    boneIndices,
                    boneWeights,
                    processedVertices,
                    boneWeightArray,
                    boneIndexArray,
                    influenceLimit,
                    weightThresholds);
            }

            int visitStamp = m_BoneMeshCache.NextVertexCandidateVisitStamp();
            var visitStamps = m_BoneMeshCache.VertexCandidateVisitStamps;

            for (int boneIndex = 0; boneIndex < targetBones.Length; ++boneIndex)
            {
                if (!targetBones[boneIndex] || boneIndex >= boneVertexCandidates.Length)
                {
                    continue;
                }

                var candidates = boneVertexCandidates[boneIndex];

                if (candidates == null)
                {
                    continue;
                }

                for (int i = 0; i < candidates.Length; ++i)
                {
                    int vertexIndex = candidates[i];

                    if (vertexIndex < 0 || vertexIndex >= m_BoneMeshCache.MeshVertexCount)
                    {
                        continue;
                    }

                    if (visitStamps[vertexIndex] == visitStamp)
                    {
                        continue;
                    }

                    visitStamps[vertexIndex] = visitStamp;

                    if (TryPopulateTargetVertex(
                        vertexIndex,
                        targetVertex,
                        boneIndices,
                        boneWeights,
                        processedVertices,
                        boneWeightArray,
                        boneIndexArray,
                        influenceLimit,
                        weightThresholds))
                    {
                        ++passedVertexCount;
                    }
                }
            }

            return passedVertexCount > 0;
        }

        private bool PopulateTargetVerticesByFullScan(
            bool[] targetVertex,
            int[] boneIndices,
            BoneWeight[] boneWeights,
            bool[] processedVertices,
            float[] boneWeightArray,
            int[] boneIndexArray,
            int influenceLimit,
            float[] weightThresholds)
        {
            int passedVertexCount = 0;

            for (int i = 0; i < m_BoneMeshCache.MeshVertexCount; ++i)
            {
                if (TryPopulateTargetVertex(
                    i,
                    targetVertex,
                    boneIndices,
                    boneWeights,
                    processedVertices,
                    boneWeightArray,
                    boneIndexArray,
                    influenceLimit,
                    weightThresholds))
                {
                    ++passedVertexCount;
                }
            }

            return passedVertexCount > 0;
        }

        private bool TryPopulateTargetVertex(
            int vertexIndex,
            bool[] targetVertex,
            int[] boneIndices,
            BoneWeight[] boneWeights,
            bool[] processedVertices,
            float[] boneWeightArray,
            int[] boneIndexArray,
            int influenceLimit,
            float[] weightThresholds)
        {
            if (processedVertices[vertexIndex]) return false;

            boneWeightArray[0] = boneWeights[vertexIndex].weight0;
            boneWeightArray[1] = boneWeights[vertexIndex].weight1;
            boneWeightArray[2] = boneWeights[vertexIndex].weight2;
            boneWeightArray[3] = boneWeights[vertexIndex].weight3;
            boneIndexArray[0] = boneWeights[vertexIndex].boneIndex0;
            boneIndexArray[1] = boneWeights[vertexIndex].boneIndex1;
            boneIndexArray[2] = boneWeights[vertexIndex].boneIndex2;
            boneIndexArray[3] = boneWeights[vertexIndex].boneIndex3;

            int targetBoneIndex = ResolveTargetBoneIndex(boneWeightArray, boneIndexArray, influenceLimit, weightThresholds);

            if (targetBoneIndex == -1) return false;

            boneIndices[vertexIndex] = targetBoneIndex;
            targetVertex[vertexIndex] = true;
            processedVertices[vertexIndex] = true;

            return true;
        }

        private int ResolveTargetBoneIndex(float[] boneWeightArray, int[] boneIndexArray, int influenceLimit, float[] weightThresholds)
        {
            var targetBones = m_BoneMeshCache.TargetBones;
            int boneCount = 0;

            for (int n = 0; n < influenceLimit; ++n)
            {
                if (boneIndexArray[n] >= 0 && boneWeightArray[n] > 0.0f)
                {
                    ++boneCount;
                }
            }

            for (int n = 0; n < influenceLimit; ++n)
            {
                int boneIndex = boneIndexArray[n];

                if (boneIndex >= 0 && targetBones[boneIndex] && boneWeightArray[n] > weightThresholds[boneCount])
                {
                    return boneIndex;
                }
            }

            if (!m_SplitProperty.GreaterBoneWeight) return -1;

            return ResolveDominantTargetBoneIndex(boneWeightArray, boneIndexArray, influenceLimit, targetBones);
        }

        private static int ResolveDominantTargetBoneIndex(float[] boneWeightArray, int[] boneIndexArray, int influenceLimit, bool[] targetBones)
        {
            int dominantBoneIndex = -1;
            float dominantBoneWeight = 0.0f;
            bool dominantBoneIsTarget = false;

            for (int n = 0; n < influenceLimit; ++n)
            {
                int boneIndex = boneIndexArray[n];

                if (boneIndex < 0) continue;

                float currentWeight = boneWeightArray[n];

                if (currentWeight > dominantBoneWeight)
                {
                    dominantBoneIndex = boneIndex;
                    dominantBoneWeight = currentWeight;
                    dominantBoneIsTarget = targetBones[dominantBoneIndex];
                }
                else if (currentWeight == dominantBoneWeight && !dominantBoneIsTarget)
                {
                    dominantBoneIndex = boneIndex;
                    dominantBoneWeight = currentWeight;
                    dominantBoneIsTarget = targetBones[dominantBoneIndex];
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
            var processedVertices = m_BoneMeshCache.ProcessedVertices;
            var meshTriangles = m_BoneMeshCache.MeshTriangles;

            for (int i = 0; i + 2 < meshTriangles.Length; i += 3)
            {
                int index0 = meshTriangles[i + 0];
                int index1 = meshTriangles[i + 1];
                int index2 = meshTriangles[i + 2];

                bool pv0 = processedVertices[index0];
                bool pv1 = processedVertices[index1];
                bool pv2 = processedVertices[index2];
                int targetVertexCount = (pv0 ? 1 : 0) + (pv1 ? 1 : 0) + (pv2 ? 1 : 0);

                if (targetVertexCount == 3 || targetVertexCount < extentVertexCount) continue;

                int replicateBoneIndex = GetReplicateBoneIndex(index0, index1, index2, pv0, pv1, pv2, boneIndices);

                if (replicateBoneIndex < 0) continue;

                if (boneIndices[index0] == -1) boneIndices[index0] = replicateBoneIndex;
                if (boneIndices[index1] == -1) boneIndices[index1] = replicateBoneIndex;
                if (boneIndices[index2] == -1) boneIndices[index2] = replicateBoneIndex;

                targetVertex[index0] = true;
                targetVertex[index1] = true;
                targetVertex[index2] = true;
            }

            for (int i = 0; i < m_BoneMeshCache.MeshVertexCount; ++i)
            {
                processedVertices[i] |= targetVertex[i];
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
            var meshTriangles = m_BoneMeshCache.MeshTriangles;
            passedTriangles = new List<int>();

            for (int i = 0; i + 2 < meshTriangles.Length; i += 3)
            {
                int index0 = meshTriangles[i + 0];
                int index1 = meshTriangles[i + 1];
                int index2 = meshTriangles[i + 2];

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

            for (int i = 0; i < m_BoneMeshCache.MeshVertexCount; i++)
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
            var meshBindPoses = m_BoneMeshCache.MeshBindPoses;
            var meshVertices = m_BoneMeshCache.MeshVertices;

            for (int i = 0; i < redirectIndex.Length; ++i)
            {
                redirectIndex[i] = -1;
            }

            int remakeVertexCount = 0;

            for (int i = 0; i < m_BoneMeshCache.MeshVertexCount; ++i)
            {
                if (!passedVertex[i]) continue;

                int boneIndex = boneIndices[i];

                if (boneIndex < 0 || boneIndex >= meshBindPoses.Length)
                {
                    passedVertex[i] = false;
                    continue;
                }

                int targetLocalBoneIndex = ResolveTargetLocalBoneIndex(i, boneIndex);

                if (targetLocalBoneIndex < 0 || targetLocalBoneIndex >= meshBindPoses.Length)
                {
                    passedVertex[i] = false;
                    continue;
                }

                boneIndices[i] = targetLocalBoneIndex;
                ++remakeVertexCount;
            }

            var remakeVertices = new Vector3[remakeVertexCount];

            for (int i = 0, index = 0; i < m_BoneMeshCache.MeshVertexCount; ++i)
            {
                if (!passedVertex[i]) continue;

                int boneIndex = boneIndices[i];

                if (boneIndex < 0 || boneIndex >= meshBindPoses.Length) continue;

                var matrix = meshBindPoses[boneIndex];
                remakeVertices[index] = matrix.MultiplyPoint(meshVertices[i]);
                redirectIndex[i] = index;

                ++index;
            }

            var remappedTriangles = new List<int>(passedTriangles.Count);

            for (int i = 0; i + 2 < passedTriangles.Count; i += 3)
            {
                int index0 = redirectIndex[passedTriangles[i + 0]];
                int index1 = redirectIndex[passedTriangles[i + 1]];
                int index2 = redirectIndex[passedTriangles[i + 2]];

                if (index0 < 0 || index1 < 0 || index2 < 0) continue;

                remappedTriangles.Add(index0);
                remappedTriangles.Add(index1);
                remappedTriangles.Add(index2);
            }

            m_BoneVertices = remakeVertices;
            m_BoneTriangles = remappedTriangles.ToArray();
        }

        private int ResolveTargetLocalBoneIndex(int vertexIndex, int fallbackBoneIndex)
        {
            var rendererIndices = m_BoneMeshCache.MeshVertexRendererIndices;
            var rendererBoneStarts = m_BoneMeshCache.RendererBoneStartIndices;
            var rendererBoneCounts = m_BoneMeshCache.RendererBoneCounts;
            var meshBones = m_BoneMeshCache.MeshBones;

            if (rendererIndices == null || rendererBoneStarts == null || rendererBoneCounts == null)
            {
                return fallbackBoneIndex;
            }

            int rendererIndex = rendererIndices[vertexIndex];

            if (rendererIndex < 0 || rendererIndex >= rendererBoneStarts.Length || rendererIndex >= rendererBoneCounts.Length)
            {
                return fallbackBoneIndex;
            }

            int start = rendererBoneStarts[rendererIndex];
            int end = Mathf.Min(start + rendererBoneCounts[rendererIndex], meshBones.Length);
            var targetTransform = m_BoneGameObject.transform;

            for (int i = start; i < end; ++i)
            {
                if (meshBones[i] == targetTransform)
                {
                    return i;
                }
            }

            return fallbackBoneIndex;
        }
    }
}
