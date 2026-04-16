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

            return ProcessInternal();
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
            var weights = new float[5] { 0.0f, 0.0f,
                m_SplitProperty.BoneWeight2 * 0.01f,
                m_SplitProperty.BoneWeight3 * 0.01f,
                m_SplitProperty.BoneWeight4 * 0.01f };

            RebuildTargetBones(m_BoneGameObject.transform);

            var boneWeightArray = new float[4];
            var boneIndexArray = new int[4];

            bool isGreaterBoneWeight = m_SplitProperty.GreaterBoneWeight;

            int passedVertexCount = 0;
            var targetVertex = m_BoneMeshCache.TargetVertices;
            var boneIndices = m_BoneMeshCache.BoneIndices;

            var boneWeights = m_MeshBoneWeights;

            for (int i = 0; i < m_MeshVertexCount; ++i)
            {
                if (!m_ProcessedVertices[i])
                {
                    var boneWeight = boneWeights[i];
                    boneWeightArray[0] = boneWeight.weight0;
                    boneWeightArray[1] = boneWeight.weight1;
                    boneWeightArray[2] = boneWeight.weight2;
                    boneWeightArray[3] = boneWeight.weight3;
                    boneIndexArray[0] = boneWeight.boneIndex0;
                    boneIndexArray[1] = boneWeight.boneIndex1;
                    boneIndexArray[2] = boneWeight.boneIndex2;
                    boneIndexArray[3] = boneWeight.boneIndex3;

                    int boneCount = 0;
                    int targetBoneIndex = -1;

                    for (int n = 0; n < 4; ++n)
                    {
                        if (boneIndexArray[n] >= 0 && boneWeightArray[n] > 0.0f)
                        {
                            ++boneCount;
                        }
                    }

                    for (int n = 0; n < 4; ++n)
                    {
                        if (boneIndexArray[n] >= 0 && m_TargetBones[boneIndexArray[n]] && boneWeightArray[n] > weights[boneCount])
                        {
                            targetBoneIndex = boneIndexArray[n];
                        }
                    }

                    if (isGreaterBoneWeight)
                    {
                        if (targetBoneIndex == -1)
                        {
                            var greaterBoneIndex = -1;
                            var greaterBoneWeight = 0.0f;
                            var greaterBoneIsTarget = false;

                            for (int n = 0; n < 4; ++n)
                            {
                                if (boneIndexArray[n] >= 0)
                                {
                                    if (boneWeightArray[n] > greaterBoneWeight)
                                    {
                                        greaterBoneIndex = boneIndexArray[n];
                                        greaterBoneWeight = boneWeightArray[n];
                                        greaterBoneIsTarget = m_TargetBones[greaterBoneIndex];
                                    }
                                    else if (boneWeightArray[n] == greaterBoneWeight && !greaterBoneIsTarget)
                                    {
                                        greaterBoneIndex = boneIndexArray[n];
                                        greaterBoneWeight = boneWeightArray[n];
                                        greaterBoneIsTarget = m_TargetBones[greaterBoneIndex];
                                    }
                                }
                            }

                            if (greaterBoneIsTarget)
                            {
                                targetBoneIndex = greaterBoneIndex;
                            }
                        }
                    }

                    if (targetBoneIndex != -1)
                    {
                        boneIndices[i] = targetBoneIndex;
                        targetVertex[i] = true;

                        m_ProcessedVertices[i] = true;

                        ++passedVertexCount;
                    }
                }
            }

            if (passedVertexCount == 0) return false;

            var triangles = m_MeshTriangles;

            if (m_SplitProperty.BoneTriangleExtent != BoneTriangleExtent.Disable)
            {
                int extentVertexCount = 0;

                if (m_SplitProperty.BoneTriangleExtent == BoneTriangleExtent.Vertex1)
                {
                    extentVertexCount = 1;
                }

                if (m_SplitProperty.BoneTriangleExtent == BoneTriangleExtent.Vertex2)
                {
                    extentVertexCount = 2;
                }

                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int index0 = triangles[i + 0];
                    int index1 = triangles[i + 1];
                    int index2 = triangles[i + 2];
                    int targetVertexCount = 0;

                    bool pv0 = m_ProcessedVertices[index0];
                    bool pv1 = m_ProcessedVertices[index1];
                    bool pv2 = m_ProcessedVertices[index2];

                    if (pv0) ++targetVertexCount;
                    if (pv1) ++targetVertexCount;
                    if (pv2) ++targetVertexCount;

                    if (targetVertexCount != 3 && targetVertexCount >= extentVertexCount)
                    {
                        int replicateBoneIndex = -1;
                        int boneIndex0 = boneIndices[index0];
                        int boneIndex1 = boneIndices[index1];
                        int boneIndex2 = boneIndices[index2];

                        if (boneIndex0 != -1 && pv0 && replicateBoneIndex == -1) replicateBoneIndex = boneIndex0;
                        if (boneIndex1 != -1 && pv1 && replicateBoneIndex == -1) replicateBoneIndex = boneIndex1;
                        if (boneIndex2 != -1 && pv2 && replicateBoneIndex == -1) replicateBoneIndex = boneIndex2;
                        if (boneIndex0 == -1) boneIndices[index0] = replicateBoneIndex;
                        if (boneIndex1 == -1) boneIndices[index1] = replicateBoneIndex;
                        if (boneIndex2 == -1) boneIndices[index2] = replicateBoneIndex;

                        targetVertex[index0] = true;
                        targetVertex[index1] = true;
                        targetVertex[index2] = true;
                    }
                }

                for (int i = 0; i < m_MeshVertexCount; ++i)
                {
                    m_ProcessedVertices[i] |= targetVertex[i];
                }
            }

            var passedVertex = m_BoneMeshCache.PassedVertices;
            var passedTriangles = new List<int>();

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int index0 = triangles[i + 0];
                int index1 = triangles[i + 1];
                int index2 = triangles[i + 2];

                if (targetVertex[index0] && targetVertex[index1] && targetVertex[index2])
                {
                    passedTriangles.Add(index0);
                    passedTriangles.Add(index1);
                    passedTriangles.Add(index2);
                    passedVertex[index0] = true;
                    passedVertex[index1] = true;
                    passedVertex[index2] = true;
                }
            }

            if (passedTriangles.Count == 0)
            {
                bool hasTargetVertices = false;

                for (int i = 0; i < m_MeshVertexCount; i++)
                {
                    if (targetVertex[i])
                    {
                        passedVertex[i] = true;
                        hasTargetVertices = true;
                    }
                }

                if (!hasTargetVertices) return false;
            }

            int remakeVertexCount = 0;

            for (int i = 0; i < m_MeshVertexCount; ++i)
            {
                if (passedVertex[i])
                {
                    ++remakeVertexCount;
                }
            }

            var remakeVertices = new Vector3[remakeVertexCount];
            var redirectIndex = m_BoneMeshCache.RedirectIndices;

            for (int i = 0, index = 0; i < m_MeshVertexCount; ++i)
            {
                if (passedVertex[i])
                {
                    Matrix4x4 matrix = m_MeshBindPoses[boneIndices[i]];
                    Vector3 v = m_MeshVertices[i];
                    v = matrix.MultiplyPoint(v);
                    remakeVertices[index] = v;
                    redirectIndex[i] = index;
                    ++index;
                }
            }

            for (int i = 0; i < passedTriangles.Count; ++i)
            {
                passedTriangles[i] = redirectIndex[passedTriangles[i]];
            }

            m_BoneVertices = remakeVertices;
            m_BoneTriangles = passedTriangles.ToArray();

            return true;
        }
    }
}