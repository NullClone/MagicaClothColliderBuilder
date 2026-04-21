using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCache
    {
        // Fields

        private int m_MeshVertexCount;
        private int m_MeshTriangleCount;
        private int m_MeshBoneCount;
        private Matrix4x4[] m_MeshBindPoses;
        private Transform[] m_MeshBones;
        private BoneWeight[] m_MeshBoneWeights;
        private Vector3[] m_MeshVertices;
        private int[] m_MeshTriangles;
        private int[] m_MeshVertexRendererIndices;
        private int[] m_RendererBoneStartIndices;
        private int[] m_RendererBoneCounts;
        private bool[] m_TargetBones;
        private bool[] m_TargetVertices;
        private bool[] m_PassedVertices;
        private bool[] m_ProcessedVertices;
        private int[] m_RedirectIndices;
        private int[] m_BoneIndices;


        // Properties

        public int MeshVertexCount => m_MeshVertexCount;

        public int MeshTriangleCount => m_MeshTriangleCount;

        public int MeshBoneCount => m_MeshBoneCount;

        public Matrix4x4[] MeshBindPoses => m_MeshBindPoses;

        public Transform[] MeshBones => m_MeshBones;

        public BoneWeight[] MeshBoneWeights => m_MeshBoneWeights;

        public Vector3[] MeshVertices => m_MeshVertices;

        public int[] MeshTriangles => m_MeshTriangles;

        public int[] MeshVertexRendererIndices => m_MeshVertexRendererIndices;

        public int[] RendererBoneStartIndices => m_RendererBoneStartIndices;

        public int[] RendererBoneCounts => m_RendererBoneCounts;

        public bool[] TargetBones => m_TargetBones;

        public bool[] TargetVertices => m_TargetVertices;

        public bool[] PassedVertices => m_PassedVertices;

        public bool[] ProcessedVertices => m_ProcessedVertices;

        public int[] RedirectIndices => m_RedirectIndices;

        public int[] BoneIndices => m_BoneIndices;


        // Methods

        public void Process(GameObject gameObject, List<SkinnedMeshRenderer> customSkinnedMeshRenderers = null)
        {
            m_MeshVertexCount = 0;
            m_MeshTriangleCount = 0;
            m_MeshBoneCount = 0;

            if (gameObject == null)
            {
                return;
            }

            SkinnedMeshRenderer[] skinnedMeshRenderers;

            if (customSkinnedMeshRenderers != null)
            {
                var validRenderers = new List<SkinnedMeshRenderer>(customSkinnedMeshRenderers.Count);
                var seenRenderers = new HashSet<SkinnedMeshRenderer>();

                for (int i = 0; i < customSkinnedMeshRenderers.Count; ++i)
                {
                    var renderer = customSkinnedMeshRenderers[i];

                    if (renderer == null || !seenRenderers.Add(renderer))
                    {
                        continue;
                    }

                    validRenderers.Add(renderer);
                }

                skinnedMeshRenderers = validRenderers.ToArray();
            }
            else
            {
                skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0)
            {
                return;
            }

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.bones != null)
                {
                    var sharedMesh = skinnedMeshRenderer.sharedMesh;

                    if (sharedMesh == null) continue;

                    var boneWeights = sharedMesh.boneWeights;

                    if (boneWeights == null || boneWeights.Length == 0) continue;

                    m_MeshBoneCount += skinnedMeshRenderer.bones.Length;
                    m_MeshVertexCount += sharedMesh.vertexCount;
                    m_MeshTriangleCount += sharedMesh.triangles.Length;
                }
            }

            m_MeshBones = new Transform[m_MeshBoneCount];
            m_MeshBindPoses = new Matrix4x4[m_MeshBoneCount];
            m_MeshBoneWeights = new BoneWeight[m_MeshVertexCount];
            m_MeshVertices = new Vector3[m_MeshVertexCount];
            m_MeshTriangles = new int[m_MeshTriangleCount];
            m_MeshVertexRendererIndices = new int[m_MeshVertexCount];
            m_RendererBoneStartIndices = new int[skinnedMeshRenderers.Length];
            m_RendererBoneCounts = new int[skinnedMeshRenderers.Length];
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
            int meshRendererIndex = 0;

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.bones != null)
                {
                    var sharedMesh = skinnedMeshRenderer.sharedMesh;

                    if (sharedMesh == null) continue;

                    var bones = skinnedMeshRenderer.bones;
                    var bindPoses = sharedMesh.bindposes;
                    var boneWeights = sharedMesh.boneWeights;
                    var vertices = sharedMesh.vertices;
                    var triangles = sharedMesh.triangles;

                    if (boneWeights == null || boneWeights.Length == 0) continue;

                    m_RendererBoneStartIndices[meshRendererIndex] = meshRendererBoneIndex;
                    m_RendererBoneCounts[meshRendererIndex] = bones.Length;

                    for (int i = 0; i < bones.Length; ++i)
                    {
                        m_MeshBones[meshRendererBoneIndex + i] = bones[i];
                        m_MeshBindPoses[meshRendererBoneIndex + i] = bindPoses != null && i < bindPoses.Length ? bindPoses[i] : Matrix4x4.identity;
                    }

                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        m_MeshVertices[meshRendererVertexIndex + i] = vertices[i];
                        m_MeshVertexRendererIndices[meshRendererVertexIndex + i] = meshRendererIndex;

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

                ++meshRendererIndex;
            }
        }

        public void Clear()
        {
            Array.Clear(m_TargetBones, 0, m_TargetBones.Length);
            Array.Clear(m_TargetVertices, 0, m_TargetVertices.Length);
            Array.Clear(m_PassedVertices, 0, m_PassedVertices.Length);
            Array.Clear(m_ProcessedVertices, 0, m_ProcessedVertices.Length);

            for (int i = 0; i < m_RedirectIndices.Length; ++i)
            {
                m_RedirectIndices[i] = -1;
            }

            for (int i = 0; i < m_BoneIndices.Length; ++i)
            {
                m_BoneIndices[i] = -1;
            }
        }
    }
}
