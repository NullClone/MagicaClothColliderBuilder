using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCache
    {
        private int m_meshVertexCount;
        private int m_meshTriangleCount;
        private int m_meshBoneCount;
        private Matrix4x4[] m_meshBindPoses;
        private Transform[] m_meshBones;
        private BoneWeight[] m_meshBoneWeights;
        private Vector3[] m_meshVertices;
        private int[] m_meshTriangles;

        // for Work
        private bool[] m_targetBones;
        private bool[] m_targetVertices;
        private bool[] m_passedVertices;
        private bool[] m_processedVertices;
        private int[] m_redirectIndices;
        private int[] m_boneIndices;

        public int MeshVertexCount => m_meshVertexCount;
        public int MeshTriangleCount => m_meshTriangleCount;
        public int MeshBoneCount => m_meshBoneCount;
        public Matrix4x4[] MeshBindPoses => m_meshBindPoses;
        public Transform[] MeshBones => m_meshBones;
        public BoneWeight[] MeshBoneWeights => m_meshBoneWeights;
        public Vector3[] MeshVertices => m_meshVertices;
        public int[] MeshTriangles => m_meshTriangles;
        public bool[] TargetBones => m_targetBones;
        public bool[] TargetVertices => m_targetVertices;
        public bool[] PassedVertices => m_passedVertices;
        public bool[] ProcessedVertices => m_processedVertices;
        public int[] RedirectIndices => m_redirectIndices;
        public int[] BoneIndices => m_boneIndices;

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
                    m_meshBoneCount += skinnedMeshRenderer.bones.Length;
                    m_meshVertexCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                    m_meshTriangleCount += skinnedMeshRenderer.sharedMesh.triangles.Length;
                }
            }

            m_meshBones = new Transform[m_meshBoneCount];
            m_meshBindPoses = new Matrix4x4[m_meshBoneCount];
            m_meshBoneWeights = new BoneWeight[m_meshVertexCount];
            m_meshVertices = new Vector3[m_meshVertexCount];
            m_meshTriangles = new int[m_meshTriangleCount];

            // for Work
            m_targetBones = new bool[m_meshBoneCount];
            m_targetVertices = new bool[m_meshVertexCount];
            m_passedVertices = new bool[m_meshVertexCount];
            m_processedVertices = new bool[m_meshVertexCount];
            m_redirectIndices = new int[m_meshVertexCount];
            m_boneIndices = new int[m_meshVertexCount];
            for (int i = 0; i < m_boneIndices.Length; ++i)
            {
                m_boneIndices[i] = -1;
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
                        m_meshBones[meshRendererBoneIndex + i] = bones[i];
                        m_meshBindPoses[meshRendererBoneIndex + i] = bindPoses[i];
                    }
                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        m_meshVertices[meshRendererVertexIndex + i] = vertices[i];
                        BoneWeight boneWeight = boneWeights[i];
                        if (boneWeight.boneIndex0 >= 0) boneWeight.boneIndex0 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex1 >= 0) boneWeight.boneIndex1 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex2 >= 0) boneWeight.boneIndex2 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex3 >= 0) boneWeight.boneIndex3 += meshRendererBoneIndex;
                        m_meshBoneWeights[meshRendererVertexIndex + i] = boneWeight;
                    }
                    for (int i = 0; i < triangles.Length; ++i)
                    {
                        m_meshTriangles[meshRendererTriangleIndex + i] = triangles[i] + meshRendererVertexIndex;
                    }

                    meshRendererBoneIndex += bones.Length;
                    meshRendererVertexIndex += vertices.Length;
                    meshRendererTriangleIndex += triangles.Length;
                }
            }
        }

        public void CleanWork()
        {
            Array.Clear(m_targetBones, 0, m_targetBones.Length);
            Array.Clear(m_targetVertices, 0, m_targetVertices.Length);
            Array.Clear(m_passedVertices, 0, m_passedVertices.Length);
            Array.Clear(m_processedVertices, 0, m_processedVertices.Length);
            Array.Clear(m_redirectIndices, 0, m_redirectIndices.Length);
            for (int i = 0; i < m_boneIndices.Length; ++i)
            {
                m_boneIndices[i] = -1;
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

            GetSkinnedMeshRenderersInChildren(skinnedMeshRenderers, go);
            return skinnedMeshRenderers.ToArray();
        }

        private static void GetSkinnedMeshRenderersInChildren(List<SkinnedMeshRenderer> skinnedMeshRenderers, GameObject go)
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

                        GetSkinnedMeshRenderersInChildren(skinnedMeshRenderers, childTransform.gameObject);
                    }
                }
            }
        }
    }
}