using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class BoneMeshCache
    {
        // Properties

        public int MeshVertexCount { get; private set; }

        public int MeshTriangleCount { get; private set; }

        public int MeshBoneCount { get; private set; }

        public Matrix4x4[] MeshBindPoses { get; private set; }

        public Transform[] MeshBones { get; private set; }

        public BoneWeight[] MeshBoneWeights { get; private set; }

        public Vector3[] MeshVertices { get; private set; }

        public int[] MeshTriangles { get; private set; }

        public int[] MeshVertexRendererIndices { get; private set; }

        public int[] RendererBoneStartIndices { get; private set; }

        public int[] RendererBoneCounts { get; private set; }

        public bool[] TargetBones { get; private set; }

        public bool[] TargetVertices { get; private set; }

        public bool[] PassedVertices { get; private set; }

        public bool[] ProcessedVertices { get; private set; }

        public int[] RedirectIndices { get; private set; }

        public int[] BoneIndices { get; private set; }

        public int[][] BoneVertexCandidates { get; private set; }

        public int[] VertexCandidateVisitStamps { get; private set; }

        private int VertexCandidateVisitStamp { get; set; }


        // Methods

        public void Process(GameObject gameObject, List<SkinnedMeshRenderer> customSkinnedMeshRenderers = null)
        {
            if (gameObject == null) return;

            MeshVertexCount = 0;
            MeshTriangleCount = 0;
            MeshBoneCount = 0;

            SkinnedMeshRenderer[] skinnedMeshRenderers;

            if (customSkinnedMeshRenderers != null)
            {
                var validRenderers = new List<SkinnedMeshRenderer>(customSkinnedMeshRenderers.Count);
                var seenRenderers = new HashSet<SkinnedMeshRenderer>();

                for (int i = 0; i < customSkinnedMeshRenderers.Count; ++i)
                {
                    var renderer = customSkinnedMeshRenderers[i];

                    if (renderer == null || !seenRenderers.Add(renderer)) continue;

                    validRenderers.Add(renderer);
                }

                skinnedMeshRenderers = validRenderers.ToArray();
            }
            else
            {
                skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0) return;

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.bones != null)
                {
                    var sharedMesh = skinnedMeshRenderer.sharedMesh;

                    if (sharedMesh == null) continue;

                    var boneWeights = sharedMesh.boneWeights;

                    if (boneWeights == null || boneWeights.Length == 0) continue;

                    MeshBoneCount += skinnedMeshRenderer.bones.Length;
                    MeshVertexCount += sharedMesh.vertexCount;
                    MeshTriangleCount += sharedMesh.triangles.Length;
                }
            }

            MeshBones = new Transform[MeshBoneCount];
            MeshBindPoses = new Matrix4x4[MeshBoneCount];
            MeshBoneWeights = new BoneWeight[MeshVertexCount];
            MeshVertices = new Vector3[MeshVertexCount];
            MeshTriangles = new int[MeshTriangleCount];
            MeshVertexRendererIndices = new int[MeshVertexCount];
            RendererBoneStartIndices = new int[skinnedMeshRenderers.Length];
            RendererBoneCounts = new int[skinnedMeshRenderers.Length];
            TargetBones = new bool[MeshBoneCount];
            TargetVertices = new bool[MeshVertexCount];
            PassedVertices = new bool[MeshVertexCount];
            ProcessedVertices = new bool[MeshVertexCount];
            RedirectIndices = new int[MeshVertexCount];
            BoneIndices = new int[MeshVertexCount];
            VertexCandidateVisitStamps = new int[MeshVertexCount];
            VertexCandidateVisitStamp = 0;

            for (int i = 0; i < BoneIndices.Length; ++i)
            {
                BoneIndices[i] = -1;
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

                    RendererBoneStartIndices[meshRendererIndex] = meshRendererBoneIndex;
                    RendererBoneCounts[meshRendererIndex] = bones.Length;

                    for (int i = 0; i < bones.Length; ++i)
                    {
                        MeshBones[meshRendererBoneIndex + i] = bones[i];
                        MeshBindPoses[meshRendererBoneIndex + i] = bindPoses != null && i < bindPoses.Length ? bindPoses[i] : Matrix4x4.identity;
                    }

                    for (int i = 0; i < vertices.Length; ++i)
                    {
                        MeshVertices[meshRendererVertexIndex + i] = vertices[i];
                        MeshVertexRendererIndices[meshRendererVertexIndex + i] = meshRendererIndex;

                        var boneWeight = boneWeights[i];

                        if (boneWeight.boneIndex0 >= 0) boneWeight.boneIndex0 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex1 >= 0) boneWeight.boneIndex1 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex2 >= 0) boneWeight.boneIndex2 += meshRendererBoneIndex;
                        if (boneWeight.boneIndex3 >= 0) boneWeight.boneIndex3 += meshRendererBoneIndex;

                        MeshBoneWeights[meshRendererVertexIndex + i] = boneWeight;
                    }

                    for (int i = 0; i < triangles.Length; ++i)
                    {
                        MeshTriangles[meshRendererTriangleIndex + i] = triangles[i] + meshRendererVertexIndex;
                    }

                    meshRendererBoneIndex += bones.Length;
                    meshRendererVertexIndex += vertices.Length;
                    meshRendererTriangleIndex += triangles.Length;
                }

                ++meshRendererIndex;
            }

            BuildBoneVertexCandidates();
        }

        public void Clear()
        {
            Array.Clear(TargetBones, 0, TargetBones.Length);
            Array.Clear(TargetVertices, 0, TargetVertices.Length);
            Array.Clear(PassedVertices, 0, PassedVertices.Length);
            Array.Clear(ProcessedVertices, 0, ProcessedVertices.Length);

            for (int i = 0; i < RedirectIndices.Length; ++i)
            {
                RedirectIndices[i] = -1;
            }

            for (int i = 0; i < BoneIndices.Length; ++i)
            {
                BoneIndices[i] = -1;
            }
        }

        public int NextVertexCandidateVisitStamp()
        {
            ++VertexCandidateVisitStamp;

            if (VertexCandidateVisitStamp == int.MaxValue)
            {
                Array.Clear(VertexCandidateVisitStamps, 0, VertexCandidateVisitStamps.Length);
                VertexCandidateVisitStamp = 1;
            }

            return VertexCandidateVisitStamp;
        }


        private void BuildBoneVertexCandidates()
        {
            BoneVertexCandidates = new int[MeshBoneCount][];

            if (MeshBoneCount == 0 || MeshVertexCount == 0) return;

            var candidateCounts = new int[MeshBoneCount];

            for (int i = 0; i < MeshBoneWeights.Length; ++i)
            {
                var boneWeight = MeshBoneWeights[i];

                CountCandidate(candidateCounts, boneWeight.boneIndex0, boneWeight.weight0, -1, -1, -1);
                CountCandidate(candidateCounts, boneWeight.boneIndex1, boneWeight.weight1, boneWeight.boneIndex0, -1, -1);
                CountCandidate(candidateCounts, boneWeight.boneIndex2, boneWeight.weight2, boneWeight.boneIndex0, boneWeight.boneIndex1, -1);
                CountCandidate(candidateCounts, boneWeight.boneIndex3, boneWeight.weight3, boneWeight.boneIndex0, boneWeight.boneIndex1, boneWeight.boneIndex2);
            }

            for (int i = 0; i < candidateCounts.Length; ++i)
            {
                BoneVertexCandidates[i] = new int[candidateCounts[i]];
            }

            Array.Clear(candidateCounts, 0, candidateCounts.Length);

            for (int i = 0; i < MeshBoneWeights.Length; ++i)
            {
                var boneWeight = MeshBoneWeights[i];

                AddCandidate(candidateCounts, boneWeight.boneIndex0, boneWeight.weight0, i, -1, -1, -1);
                AddCandidate(candidateCounts, boneWeight.boneIndex1, boneWeight.weight1, i, boneWeight.boneIndex0, -1, -1);
                AddCandidate(candidateCounts, boneWeight.boneIndex2, boneWeight.weight2, i, boneWeight.boneIndex0, boneWeight.boneIndex1, -1);
                AddCandidate(candidateCounts, boneWeight.boneIndex3, boneWeight.weight3, i, boneWeight.boneIndex0, boneWeight.boneIndex1, boneWeight.boneIndex2);
            }
        }

        private void CountCandidate(int[] candidateCounts, int boneIndex, float weight, int duplicate0, int duplicate1, int duplicate2)
        {
            if (boneIndex >= 0 &&
                boneIndex < MeshBoneCount &&
                weight > 0.0f &&
                boneIndex != duplicate0 &&
                boneIndex != duplicate1 &&
                boneIndex != duplicate2)
            {
                ++candidateCounts[boneIndex];
            }
        }

        private void AddCandidate(int[] candidateCounts, int boneIndex, float weight, int vertexIndex, int duplicate0, int duplicate1, int duplicate2)
        {
            if (boneIndex >= 0 &&
                boneIndex < MeshBoneCount &&
                weight > 0.0f &&
                boneIndex != duplicate0 &&
                boneIndex != duplicate1 &&
                boneIndex != duplicate2)
            {
                BoneVertexCandidates[boneIndex][candidateCounts[boneIndex]] = vertexIndex;
                ++candidateCounts[boneIndex];
            }
        }
    }
}
