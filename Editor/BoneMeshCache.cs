using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCache
    {
        int _meshVertexCount;
        int _meshTriangleCount;
        int _meshBoneCount;
        Matrix4x4[] _meshBindPoses;
        Transform[] _meshBones;
        BoneWeight[] _meshBoneWeights;
        Vector3[] _meshVertices;
        int[] _meshTriangles;

        // for Work
        bool[] _targetBones;
        bool[] _targetVertices;
        bool[] _passedVertices;
        bool[] _processedVertices;
        int[] _redirectIndices;
        int[] _boneIndices;

        public int meshVetexCount { get { return _meshVertexCount; } }
        public int meshTriangleCount { get { return _meshTriangleCount; } }
        public int meshBoneCount { get { return _meshBoneCount; } }
        public Matrix4x4[] meshBindPoses { get { return _meshBindPoses; } }
        public Transform[] meshBones { get { return _meshBones; } }
        public BoneWeight[] meshBoneWeights { get { return _meshBoneWeights; } }
        public Vector3[] meshVertices { get { return _meshVertices; } }
        public int[] meshTriangles { get { return _meshTriangles; } }
        public bool[] targetBones { get { return _targetBones; } }
        public bool[] targetVertices { get { return _targetVertices; } }
        public bool[] passedVertices { get { return _passedVertices; } }
        public bool[] processedVertices { get { return _processedVertices; } }
        public int[] redirectIndices { get { return _redirectIndices; } }
        public int[] boneIndices { get { return _boneIndices; } }

        public void Process(GameObject gameObject)
        {
            SkinnedMeshRenderer[] skinnedMeshRenderers = GetSkinnedMeshRenderers(gameObject);
            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0)
            {
                Debug.LogError("Not found SkinnedMeshRenderer.");
                return;
            }

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.bones != null)
                {
                    BoneWeight[] boneWeights = skinnedMeshRenderer.sharedMesh.boneWeights;
                    if (boneWeights == null || boneWeights.Length == 0)
                    {
                        continue;
                    }
                    this._meshBoneCount += skinnedMeshRenderer.bones.Length;
                    this._meshVertexCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                    this._meshTriangleCount += skinnedMeshRenderer.sharedMesh.triangles.Length;
                }
            }

            this._meshBones = new Transform[this._meshBoneCount];
            this._meshBindPoses = new Matrix4x4[this._meshBoneCount];
            this._meshBoneWeights = new BoneWeight[this._meshVertexCount];
            this._meshVertices = new Vector3[this._meshVertexCount];
            this._meshTriangles = new int[this._meshTriangleCount];

            // for Work
            this._targetBones = new bool[this._meshBoneCount];
            this._targetVertices = new bool[this._meshVertexCount];
            this._passedVertices = new bool[this._meshVertexCount];
            this._processedVertices = new bool[this._meshVertexCount];
            this._redirectIndices = new int[this._meshVertexCount];
            this._boneIndices = new int[this._meshVertexCount];
            for (int i = 0; i < this._boneIndices.Length; ++i)
            {
                this._boneIndices[i] = -1;
            }

            int meshRendererBoneIndex = 0;
            int meshRendererVertexIndex = 0;
            int meshRendererTriangleIndex = 0;
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.bones != null)
                {
                    Transform[] bones = skinnedMeshRenderer.bones;
                    Matrix4x4[] bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;
                    BoneWeight[] boneWeights = skinnedMeshRenderer.sharedMesh.boneWeights;
                    Vector3[] vertices = skinnedMeshRenderer.sharedMesh.vertices;
                    int[] triangles = skinnedMeshRenderer.sharedMesh.triangles;
                    if (boneWeights == null || boneWeights.Length == 0)
                    {
                        continue;
                    }
                    for (int i = 0; i < bones.Length; ++i)
                    {
                        this._meshBones[meshRendererBoneIndex + i] = bones[i];
                        this._meshBindPoses[meshRendererBoneIndex + i] = bindPoses[i];
                    }
                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        this._meshVertices[meshRendererVertexIndex + i] = vertices[i];
                        BoneWeight boneWeight = boneWeights[i];
                        if (boneWeight.boneIndex0 >= 0) boneWeight.boneIndex0 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex1 >= 0) boneWeight.boneIndex1 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex2 >= 0) boneWeight.boneIndex2 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex3 >= 0) boneWeight.boneIndex3 += meshRendererBoneIndex;
                        this._meshBoneWeights[meshRendererVertexIndex + i] = boneWeight;
                    }
                    for (int i = 0; i < triangles.Length; ++i)
                    {
                        this._meshTriangles[meshRendererTriangleIndex + i] = triangles[i] + meshRendererVertexIndex;
                    }

                    meshRendererBoneIndex += bones.Length;
                    meshRendererVertexIndex += vertices.Length;
                    meshRendererTriangleIndex += triangles.Length;
                }
            }
        }

        public void CleanWork()
        {
            Array.Clear(this._targetBones, 0, this._targetBones.Length);
            Array.Clear(this._targetVertices, 0, this._targetVertices.Length);
            Array.Clear(this._passedVertices, 0, this._passedVertices.Length);
            Array.Clear(this._processedVertices, 0, this._processedVertices.Length);
            Array.Clear(this._redirectIndices, 0, this._redirectIndices.Length);
            for (int i = 0; i < this._boneIndices.Length; ++i)
            {
                this._boneIndices[i] = -1;
            }
        }

        private static SkinnedMeshRenderer[] GetSkinnedMeshRenderers(GameObject go)
        {
            if (go == null)
            {
                return null;
            }

            List<SkinnedMeshRenderer> skinnedMeshRenderers = new List<SkinnedMeshRenderer>();
            SkinnedMeshRenderer skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
            {
                skinnedMeshRenderers.Add(skinnedMeshRenderer);
            }

            _GetSkinnedMeshRenderersInChildren(skinnedMeshRenderers, go);
            return skinnedMeshRenderers.ToArray();
        }

        private static void _GetSkinnedMeshRenderersInChildren(List<SkinnedMeshRenderer> skinnedMeshRenderers, GameObject go)
        {
            if (skinnedMeshRenderers != null && go != null)
            {
                foreach (Transform childTransform in go.transform)
                {
                    if (childTransform.gameObject.GetComponent<Animator>() == null)
                    {
                        SkinnedMeshRenderer skinnedMeshRenderer = childTransform.gameObject.GetComponent<SkinnedMeshRenderer>();
                        if (skinnedMeshRenderer != null)
                        {
                            skinnedMeshRenderers.Add(skinnedMeshRenderer);
                        }

                        _GetSkinnedMeshRenderersInChildren(skinnedMeshRenderers, childTransform.gameObject);
                    }
                }
            }
        }
    }
}