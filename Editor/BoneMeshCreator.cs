using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCreator
    {
        private GameObject m_boneGameObject;
        private SplitProperty m_splitProperty;
        private BoneMeshCache m_boneMeshCache;

        private int m_meshVertexCount;
        private int m_meshBoneCount;
        private Matrix4x4[] m_meshBindPoses;
        private Transform[] m_meshBones;
        private BoneWeight[] m_meshBoneWeights;
        private Vector3[] m_meshVertices;
        private int[] m_meshTriangles;
        private bool[] m_targetBones;
        private bool[] m_processedVertices;

        private Vector3[] m_boneVertices;
        private int[] m_boneTriangles;

        public Vector3[] BoneVertices => m_boneVertices;
        public int[] BoneTriangles => m_boneTriangles;

        public bool Process(GameObject boneGameObject, SplitProperty splitProperty, BoneMeshCache boneMeshCache)
        {
            if (boneGameObject == null || splitProperty == null || boneMeshCache == null) return false;

            boneMeshCache.CleanWork();

            if (boneMeshCache.MeshBoneCount == 0 ||
                boneMeshCache.MeshVertexCount == 0 ||
                boneMeshCache.MeshTriangleCount == 0)
            {
                return false;
            }

            m_boneGameObject = boneGameObject;
            m_splitProperty = splitProperty;
            m_boneMeshCache = boneMeshCache;
            m_meshVertexCount = boneMeshCache.MeshVertexCount;
            m_meshBoneCount = boneMeshCache.MeshBoneCount;
            m_meshBones = boneMeshCache.MeshBones;
            m_meshBindPoses = boneMeshCache.MeshBindPoses;
            m_meshBoneWeights = boneMeshCache.MeshBoneWeights;
            m_meshVertices = boneMeshCache.MeshVertices;
            m_meshTriangles = boneMeshCache.MeshTriangles;
            m_targetBones = boneMeshCache.TargetBones;
            m_processedVertices = boneMeshCache.ProcessedVertices;

            return ProcessInternal();
        }

        private void RebuildTargetBones(Transform boneTransform)
        {
            for (int i = 0; i < m_meshBoneCount; ++i)
            {
                var currentBone = m_meshBones[i];
                if (currentBone == null) continue;

                // Target the bone itself or its direct children.
                if (currentBone == boneTransform || currentBone.parent == boneTransform)
                {
                    m_targetBones[i] = true;
                }
            }
        }

        private bool ProcessInternal()
        {
            float[] weights = new float[5] { 0.0f, 0.0f,
                m_splitProperty.BoneWeight2 * 0.01f,
                m_splitProperty.BoneWeight3 * 0.01f,
                m_splitProperty.BoneWeight4 * 0.01f };

            RebuildTargetBones(m_boneGameObject.transform);

            float[] boneWeightArray = new float[4];
            int[] boneIndexArray = new int[4];

            bool isGreaterBoneWeight = m_splitProperty.GreaterBoneWeight;

            int passedVertexCount = 0;
            bool[] targetVertex = m_boneMeshCache.TargetVertices;
            int[] boneIndices = m_boneMeshCache.BoneIndices;
            BoneWeight[] boneWeights = m_meshBoneWeights;
            for (int i = 0; i < m_meshVertexCount; ++i)
            {
                if (!m_processedVertices[i])
                {
                    BoneWeight boneWeight = boneWeights[i];
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
                        if (boneIndexArray[n] >= 0 &&
                               m_targetBones[boneIndexArray[n]] &&
                               boneWeightArray[n] > weights[boneCount])
                        {
                            targetBoneIndex = boneIndexArray[n];
                        }
                    }
                    if (isGreaterBoneWeight)
                    {
                        if (targetBoneIndex == -1)
                        {
                            int greaterBoneIndex = -1;
                            float greaterBoneWeight = 0.0f;
                            bool greaterBoneIsTarget = false;
                            for (int n = 0; n < 4; ++n)
                            {
                                if (boneIndexArray[n] >= 0)
                                {
                                    if (boneWeightArray[n] > greaterBoneWeight)
                                    {
                                        greaterBoneIndex = boneIndexArray[n];
                                        greaterBoneWeight = boneWeightArray[n];
                                        greaterBoneIsTarget = m_targetBones[greaterBoneIndex];
                                    }
                                    else if (boneWeightArray[n] == greaterBoneWeight && !greaterBoneIsTarget)
                                    {
                                        greaterBoneIndex = boneIndexArray[n];
                                        greaterBoneWeight = boneWeightArray[n];
                                        greaterBoneIsTarget = m_targetBones[greaterBoneIndex];
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
                        m_processedVertices[i] = true;
                        ++passedVertexCount;
                    }
                }
            }

            if (passedVertexCount == 0)
            {
                return false;
            }

            int[] triangles = m_meshTriangles;

            if (m_splitProperty.BoneTriangleExtent != BoneTriangleExtent.Disable)
            {
                int extentVertexCount = 0;
                if (m_splitProperty.BoneTriangleExtent == BoneTriangleExtent.Vertex1)
                {
                    extentVertexCount = 1;
                }
                if (m_splitProperty.BoneTriangleExtent == BoneTriangleExtent.Vertex2)
                {
                    extentVertexCount = 2;
                }
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int index0 = triangles[i + 0];
                    int index1 = triangles[i + 1];
                    int index2 = triangles[i + 2];
                    int targetVertexCount = 0;
                    bool pv0 = m_processedVertices[index0];
                    bool pv1 = m_processedVertices[index1];
                    bool pv2 = m_processedVertices[index2];
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
                for (int i = 0; i < m_meshVertexCount; ++i)
                {
                    m_processedVertices[i] |= targetVertex[i];
                }
            }

            bool[] passedVertex = m_boneMeshCache.PassedVertices;
            List<int> passedTriangles = new List<int>();
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
                    // A vertex is considered "passed" if it is part of a valid triangle.
                    passedVertex[index0] = true;
                    passedVertex[index1] = true;
                    passedVertex[index2] = true;
                }
            }

            if (passedTriangles.Count == 0)
            {
                bool hasTargetVertices = false;
                for (int i = 0; i < m_meshVertexCount; i++)
                {
                    if (targetVertex[i])
                    {
                        passedVertex[i] = true;
                        hasTargetVertices = true;
                    }
                }
                if (!hasTargetVertices)
                {
                    return false;
                }
            }

            int remakeVertexCount = 0;
            for (int i = 0; i < m_meshVertexCount; ++i)
            {
                if (passedVertex[i])
                {
                    ++remakeVertexCount;
                }
            }

            Vector3[] remakeVertices = new Vector3[remakeVertexCount];
            int[] redirectIndex = m_boneMeshCache.RedirectIndices;
            for (int i = 0, index = 0; i < m_meshVertexCount; ++i)
            {
                if (passedVertex[i])
                {
                    Matrix4x4 matrix = m_meshBindPoses[boneIndices[i]];
                    Vector3 v = m_meshVertices[i];
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

            m_boneVertices = remakeVertices;
            m_boneTriangles = passedTriangles.ToArray();
            return true;
        }
    }

    public enum BoneWeightType
    {
        Bone2,
        Bone4,
    }

    public enum BoneTriangleExtent
    {
        Disable,
        Vertex2,
        Vertex1,
    }
}