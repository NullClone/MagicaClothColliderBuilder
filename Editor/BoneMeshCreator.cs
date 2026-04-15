using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCreator
    {
        GameObject _boneGameObject;
        SplitProperty _splitProperty;
        BoneMeshCache _boneMeshCache;

        int _meshVertexCount;
        int _meshBoneCount;
        Matrix4x4[] _meshBindPoses;
        Transform[] _meshBones;
        BoneWeight[] _meshBoneWeights;
        Vector3[] _meshVertices;
        int[] _meshTriangles;
        bool[] _targetBones;
        bool[] _processedVertex;

        Vector3[] _boneVertices;
        int[] _boneTriangles;

        public Vector3[] boneVertices { get { return _boneVertices; } }
        public int[] boneTriangles { get { return _boneTriangles; } }

        public bool Process(GameObject boneGameObject, SplitProperty splitProperty, BoneMeshCache boneMeshCache)
        {
            if (boneGameObject == null || splitProperty == null || boneMeshCache == null) return false;

            boneMeshCache.CleanWork();

            if (boneMeshCache.meshBoneCount == 0 ||
                boneMeshCache.meshVetexCount == 0 ||
                boneMeshCache.meshTriangleCount == 0)
            {
                return false;
            }

            this._boneGameObject = boneGameObject;
            this._splitProperty = splitProperty;
            this._boneMeshCache = boneMeshCache;
            this._meshVertexCount = boneMeshCache.meshVetexCount;
            this._meshBoneCount = boneMeshCache.meshBoneCount;
            this._meshBones = boneMeshCache.meshBones;
            this._meshBindPoses = boneMeshCache.meshBindPoses;
            this._meshBoneWeights = boneMeshCache.meshBoneWeights;
            this._meshVertices = boneMeshCache.meshVertices;
            this._meshTriangles = boneMeshCache.meshTriangles;
            this._targetBones = boneMeshCache.targetBones;
            this._processedVertex = boneMeshCache.processedVertices;

            return _Process();
        }

        void _RebuildTargetBones(Transform boneTransform)
        {
            for (int i = 0; i < this._meshBoneCount; ++i)
            {
                var currentBone = this._meshBones[i];
                if (currentBone == null) continue;

                // Target the bone itself or its direct children.
                if (currentBone == boneTransform || currentBone.parent == boneTransform)
                {
                    this._targetBones[i] = true;
                }
            }
        }

        bool _Process()
        {
            float[] weights = new float[5] { 0.0f, 0.0f,
                _splitProperty.boneWeight2 * 0.01f,
                _splitProperty.boneWeight3 * 0.01f,
                _splitProperty.boneWeight4 * 0.01f };

            _RebuildTargetBones(_boneGameObject.transform);

            float[] boneWeightArray = new float[4];
            int[] boneIndexArray = new int[4];

            bool isGreaterBoneWeight = _splitProperty.greaterBoneWeight;

            int passedVertexCount = 0;
            bool[] targetVertex = this._boneMeshCache.targetVertices;
            int[] boneIndices = this._boneMeshCache.boneIndices;
            BoneWeight[] boneWeights = this._meshBoneWeights;
            for (int i = 0; i < this._meshVertexCount; ++i)
            {
                if (_processedVertex[i] == false)
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
                               this._targetBones[boneIndexArray[n]] &&
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
                                        greaterBoneIsTarget = this._targetBones[greaterBoneIndex];
                                    }
                                    else if (boneWeightArray[n] == greaterBoneWeight && !greaterBoneIsTarget)
                                    {
                                        greaterBoneIndex = boneIndexArray[n];
                                        greaterBoneWeight = boneWeightArray[n];
                                        greaterBoneIsTarget = this._targetBones[greaterBoneIndex];
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
                        _processedVertex[i] = true;
                        ++passedVertexCount;
                    }
                }
            }

            if (passedVertexCount == 0)
            {
                return false;
            }

            int[] triangles = this._meshTriangles;

            if (_splitProperty.boneTriangleExtent != BoneTriangleExtent.Disable)
            {
                int extentVertexCount = 0;
                if (_splitProperty.boneTriangleExtent == BoneTriangleExtent.Vertex1)
                {
                    extentVertexCount = 1;
                }
                if (_splitProperty.boneTriangleExtent == BoneTriangleExtent.Vertex2)
                {
                    extentVertexCount = 2;
                }
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int index0 = triangles[i + 0];
                    int index1 = triangles[i + 1];
                    int index2 = triangles[i + 2];
                    int targetVertexCount = 0;
                    bool pv0 = _processedVertex[index0];
                    bool pv1 = _processedVertex[index1];
                    bool pv2 = _processedVertex[index2];
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
                for (int i = 0; i < this._meshVertexCount; ++i)
                {
                    _processedVertex[i] |= targetVertex[i];
                }
            }

            bool[] passedVertex = this._boneMeshCache.passedVertices;
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
                for (int i = 0; i < this._meshVertexCount; i++)
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
            for (int i = 0; i < this._meshVertexCount; ++i)
            {
                if (passedVertex[i])
                {
                    ++remakeVertexCount;
                }
            }

            Vector3[] remakeVertices = new Vector3[remakeVertexCount];
            int[] redirectIndex = this._boneMeshCache.redirectIndices;
            for (int i = 0, index = 0; i < this._meshVertexCount; ++i)
            {
                if (passedVertex[i])
                {
                    Matrix4x4 matrix = _meshBindPoses[boneIndices[i]];
                    Vector3 v = this._meshVertices[i];
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

            _boneVertices = remakeVertices;
            _boneTriangles = passedTriangles.ToArray();
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