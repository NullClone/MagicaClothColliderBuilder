using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCache
    {
        private int m_MeshVertexCount;
        private int m_MeshTriangleCount;
        private int m_MeshBoneCount;
        private Matrix4x4[] m_MeshBindPoses;
        private Transform[] m_MeshBones;
        private BoneWeight[] m_MeshBoneWeights;
        private Vector3[] m_MeshVertices;
        private int[] m_MeshTriangles;
        private bool[] m_TargetBones;
        private bool[] m_TargetVertices;
        private bool[] m_PassedVertices;
        private bool[] m_ProcessedVertices;
        private int[] m_RedirectIndices;
        private int[] m_BoneIndices;

        public int MeshVertexCount => m_MeshVertexCount;

        public int MeshTriangleCount => m_MeshTriangleCount;

        public int MeshBoneCount => m_MeshBoneCount;

        public Matrix4x4[] MeshBindPoses => m_MeshBindPoses;

        public Transform[] MeshBones => m_MeshBones;

        public BoneWeight[] MeshBoneWeights => m_MeshBoneWeights;

        public Vector3[] MeshVertices => m_MeshVertices;

        public int[] MeshTriangles => m_MeshTriangles;

        public bool[] TargetBones => m_TargetBones;

        public bool[] TargetVertices => m_TargetVertices;

        public bool[] PassedVertices => m_PassedVertices;

        public bool[] ProcessedVertices => m_ProcessedVertices;

        public int[] RedirectIndices => m_RedirectIndices;

        public int[] BoneIndices => m_BoneIndices;

        public void Process(GameObject gameObject)
        {
            var skinnedMeshRenderers = GetSkinnedMeshRenderers(gameObject);

            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0) return;

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.bones != null)
                {
                    var boneWeights = skinnedMeshRenderer.sharedMesh.boneWeights;

                    if (boneWeights == null || boneWeights.Length == 0) continue;

                    m_MeshBoneCount += skinnedMeshRenderer.bones.Length;
                    m_MeshVertexCount += skinnedMeshRenderer.sharedMesh.vertexCount;
                    m_MeshTriangleCount += skinnedMeshRenderer.sharedMesh.triangles.Length;
                }
            }

            m_MeshBones = new Transform[m_MeshBoneCount];
            m_MeshBindPoses = new Matrix4x4[m_MeshBoneCount];
            m_MeshBoneWeights = new BoneWeight[m_MeshVertexCount];
            m_MeshVertices = new Vector3[m_MeshVertexCount];
            m_MeshTriangles = new int[m_MeshTriangleCount];
            m_TargetBones = new bool[m_MeshBoneCount];
            m_TargetVertices = new bool[m_MeshVertexCount];
            m_PassedVertices = new bool[m_MeshVertexCount];
            m_ProcessedVertices = new bool[m_MeshVertexCount];
            m_RedirectIndices = new int[m_MeshVertexCount];
            m_BoneIndices = new int[m_MeshVertexCount];

            for (int i = 0; i < m_BoneIndices.Length; ++i)
            {
                m_BoneIndices[i] = -1;
            }

            int meshRendererBoneIndex = 0;
            int meshRendererVertexIndex = 0;
            int meshRendererTriangleIndex = 0;

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.bones != null)
                {
                    var bones = skinnedMeshRenderer.bones;
                    var bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;
                    var boneWeights = skinnedMeshRenderer.sharedMesh.boneWeights;
                    var vertices = skinnedMeshRenderer.sharedMesh.vertices;
                    var triangles = skinnedMeshRenderer.sharedMesh.triangles;

                    if (boneWeights == null || boneWeights.Length == 0) continue;

                    for (int i = 0; i < bones.Length; ++i)
                    {
                        m_MeshBones[meshRendererBoneIndex + i] = bones[i];
                        m_MeshBindPoses[meshRendererBoneIndex + i] = bindPoses[i];
                    }

                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        m_MeshVertices[meshRendererVertexIndex + i] = vertices[i];

                        var boneWeight = boneWeights[i];

                        if (boneWeight.boneIndex0 >= 0) boneWeight.boneIndex0 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex1 >= 0) boneWeight.boneIndex1 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex2 >= 0) boneWeight.boneIndex2 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex3 >= 0) boneWeight.boneIndex3 += meshRendererBoneIndex;

                        m_MeshBoneWeights[meshRendererVertexIndex + i] = boneWeight;
                    }

                    for (int i = 0; i < triangles.Length; ++i)
                    {
                        m_MeshTriangles[meshRendererTriangleIndex + i] = triangles[i] + meshRendererVertexIndex;
                    }

                    meshRendererBoneIndex += bones.Length;
                    meshRendererVertexIndex += vertices.Length;
                    meshRendererTriangleIndex += triangles.Length;
                }
            }
        }

        public void CleanWork()
        {
            Array.Clear(m_TargetBones, 0, m_TargetBones.Length);
            Array.Clear(m_TargetVertices, 0, m_TargetVertices.Length);
            Array.Clear(m_PassedVertices, 0, m_PassedVertices.Length);
            Array.Clear(m_ProcessedVertices, 0, m_ProcessedVertices.Length);
            Array.Clear(m_RedirectIndices, 0, m_RedirectIndices.Length);

            for (int i = 0; i < m_BoneIndices.Length; ++i)
            {
                m_BoneIndices[i] = -1;
            }
        }

        private static SkinnedMeshRenderer[] GetSkinnedMeshRenderers(GameObject gameObject)
        {
            if (gameObject == null) return null;

            var skinnedMeshRenderers = new List<SkinnedMeshRenderer>();
            var skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();

            if (skinnedMeshRenderer != null)
            {
                skinnedMeshRenderers.Add(skinnedMeshRenderer);
            }

            GetSkinnedMeshRenderersInChildren(skinnedMeshRenderers, gameObject);

            return skinnedMeshRenderers.ToArray();
        }

        private static void GetSkinnedMeshRenderersInChildren(List<SkinnedMeshRenderer> skinnedMeshRenderers, GameObject gameObject)
        {
            if (skinnedMeshRenderers != null && gameObject != null)
            {
                foreach (Transform childTransform in gameObject.transform)
                {
                    if (childTransform.gameObject.GetComponent<Animator>() == null)
                    {
                        if (childTransform.gameObject.TryGetComponent<SkinnedMeshRenderer>(out var skinnedMeshRenderer))
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