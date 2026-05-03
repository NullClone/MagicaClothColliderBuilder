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

        public Transform[] MeshBones { get; private set; }

        public BoneWeight[] MeshBoneWeights { get; private set; }

        public Vector3[] MeshVertices { get; private set; }

        public int[] MeshTriangles { get; private set; }

        public int[] MeshVertexRendererIndices { get; private set; }

        public int[] RendererBoneStartIndices { get; private set; }

        public int[] RendererBoneCounts { get; private set; }

        public Transform[] RendererTransforms { get; private set; }

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
                skinnedMeshRenderers = customSkinnedMeshRenderers.ToArray();
            }
            else
            {
                skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Length == 0) return;

            skinnedMeshRenderers = ValidateSkinnedMeshRenderers(gameObject, skinnedMeshRenderers, customSkinnedMeshRenderers != null);

            if (skinnedMeshRenderers.Length == 0)
            {
                Debug.LogError("Magica Collider Builder: No valid SkinnedMeshRenderer found.");

                return;
            }

            var currentPoseVerticesByRenderer = BakeCurrentPoseVertices(skinnedMeshRenderers);
            var bakedRenderers = new List<SkinnedMeshRenderer>();
            var bakedVertices = new List<Vector3[]>();

            for (int i = 0; i < skinnedMeshRenderers.Length; ++i)
            {
                if (currentPoseVerticesByRenderer[i] == null)
                {
                    continue;
                }

                bakedRenderers.Add(skinnedMeshRenderers[i]);
                bakedVertices.Add(currentPoseVerticesByRenderer[i]);
            }

            skinnedMeshRenderers = bakedRenderers.ToArray();
            currentPoseVerticesByRenderer = bakedVertices.ToArray();

            if (skinnedMeshRenderers.Length == 0)
            {
                Debug.LogError("Magica Collider Builder: No SkinnedMeshRenderer could be baked in the current pose.");

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

                    MeshBoneCount += skinnedMeshRenderer.bones.Length;
                    MeshVertexCount += sharedMesh.vertexCount;
                    MeshTriangleCount += sharedMesh.triangles.Length;
                }
            }

            MeshBones = new Transform[MeshBoneCount];
            MeshBoneWeights = new BoneWeight[MeshVertexCount];
            MeshVertices = new Vector3[MeshVertexCount];
            MeshTriangles = new int[MeshTriangleCount];
            MeshVertexRendererIndices = new int[MeshVertexCount];
            RendererBoneStartIndices = new int[skinnedMeshRenderers.Length];
            RendererBoneCounts = new int[skinnedMeshRenderers.Length];
            RendererTransforms = new Transform[skinnedMeshRenderers.Length];
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
                    var boneWeights = sharedMesh.boneWeights;
                    var vertices = currentPoseVerticesByRenderer[meshRendererIndex];
                    var triangles = sharedMesh.triangles;

                    if (boneWeights == null || boneWeights.Length == 0) continue;

                    RendererBoneStartIndices[meshRendererIndex] = meshRendererBoneIndex;
                    RendererBoneCounts[meshRendererIndex] = bones.Length;
                    RendererTransforms[meshRendererIndex] = skinnedMeshRenderer.transform;

                    for (int i = 0; i < bones.Length; ++i)
                    {
                        MeshBones[meshRendererBoneIndex + i] = bones[i];
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

        private static SkinnedMeshRenderer[] ValidateSkinnedMeshRenderers(GameObject root, SkinnedMeshRenderer[] renderers, bool customSelection)
        {
            var validRenderers = new List<SkinnedMeshRenderer>();
            var seenRenderers = new HashSet<SkinnedMeshRenderer>();

            var rootTransform = root != null ? root.transform : null;

            for (int i = 0; i < renderers.Length; ++i)
            {
                var renderer = renderers[i];

                if (renderer == null)
                {
                    if (customSelection)
                    {
                        Debug.LogWarning("Magica Collider Builder: Custom renderer list contains an empty slot.");
                    }

                    continue;
                }

                if (!seenRenderers.Add(renderer))
                {
                    Debug.LogWarning($"Magica Collider Builder: Duplicate SkinnedMeshRenderer skipped: {renderer.name}.");

                    continue;
                }

                if (!ValidateSkinnedMeshRenderer(rootTransform, renderer, out string reason))
                {
                    Debug.LogWarning($"Magica Collider Builder: SkinnedMeshRenderer skipped: {renderer.name}. {reason}");

                    continue;
                }

                validRenderers.Add(renderer);
            }

            return validRenderers.ToArray();
        }

        private static bool ValidateSkinnedMeshRenderer(Transform rootTransform, SkinnedMeshRenderer renderer, out string reason)
        {
            reason = string.Empty;

            var sharedMesh = renderer.sharedMesh;

            if (sharedMesh == null)
            {
                reason = "Missing sharedMesh.";
                return false;
            }

            if (sharedMesh.vertexCount <= 0)
            {
                reason = "Mesh has no vertices.";
                return false;
            }

            var boneWeights = sharedMesh.boneWeights;

            if (boneWeights == null || boneWeights.Length == 0)
            {
                reason = "Mesh has no bone weights.";
                return false;
            }

            if (boneWeights.Length != sharedMesh.vertexCount)
            {
                reason = $"Bone weight count ({boneWeights.Length}) does not match vertex count ({sharedMesh.vertexCount}).";
                return false;
            }

            var bones = renderer.bones;

            if (bones == null || bones.Length == 0)
            {
                reason = "Renderer has no bones.";
                return false;
            }

            if (!IsRendererRelatedToRoot(rootTransform, renderer))
            {
                reason = "Renderer is not under the avatar root and does not reference avatar bones.";
                return false;
            }

            if (!ValidateBoneWeightIndices(boneWeights, bones.Length, out reason))
            {
                return false;
            }

            var triangles = sharedMesh.triangles;

            if (triangles == null || triangles.Length == 0)
            {
                reason = "Mesh has no triangles.";
                return false;
            }

            for (int i = 0; i < triangles.Length; ++i)
            {
                if (triangles[i] < 0 || triangles[i] >= sharedMesh.vertexCount)
                {
                    reason = $"Triangle index {triangles[i]} is outside vertex count {sharedMesh.vertexCount}.";
                    return false;
                }
            }

            for (int i = 0; i < bones.Length; ++i)
            {
                if (bones[i] == null)
                {
                    Debug.LogWarning($"Magica Collider Builder: Renderer {renderer.name} has a null bone at index {i}.");
                }
            }

            return true;
        }

        private static bool IsRendererRelatedToRoot(Transform rootTransform, SkinnedMeshRenderer renderer)
        {
            if (rootTransform == null || renderer == null)
            {
                return true;
            }

            if (renderer.transform != null && renderer.transform.IsChildOf(rootTransform))
            {
                return true;
            }

            var bones = renderer.bones;

            if (bones == null)
            {
                return false;
            }

            for (int i = 0; i < bones.Length; ++i)
            {
                if (bones[i] != null && bones[i].IsChildOf(rootTransform))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ValidateBoneWeightIndices(BoneWeight[] boneWeights, int boneCount, out string reason)
        {
            reason = string.Empty;

            for (int i = 0; i < boneWeights.Length; ++i)
            {
                var weight = boneWeights[i];

                if (!ValidateWeightedBoneIndex(weight.boneIndex0, weight.weight0, boneCount) ||
                    !ValidateWeightedBoneIndex(weight.boneIndex1, weight.weight1, boneCount) ||
                    !ValidateWeightedBoneIndex(weight.boneIndex2, weight.weight2, boneCount) ||
                    !ValidateWeightedBoneIndex(weight.boneIndex3, weight.weight3, boneCount))
                {
                    reason = $"Bone weight at vertex {i} references a bone outside renderer.bones.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateWeightedBoneIndex(int boneIndex, float weight, int boneCount)
        {
            if (weight <= 0.0f)
            {
                return true;
            }

            return boneIndex >= 0 && boneIndex < boneCount;
        }

        private static Vector3[][] BakeCurrentPoseVertices(SkinnedMeshRenderer[] renderers)
        {
            var currentPoseVertices = new Vector3[renderers.Length][];

            for (int i = 0; i < renderers.Length; ++i)
            {
                var renderer = renderers[i];

                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                currentPoseVertices[i] = TryGetCurrentPoseVertices(renderer, renderer.sharedMesh);
            }

            return currentPoseVertices;
        }

        private static Vector3[] TryGetCurrentPoseVertices(SkinnedMeshRenderer renderer, Mesh sharedMesh)
        {
            Mesh bakedMesh = new();

            try
            {
                renderer.BakeMesh(bakedMesh, false);

                if (bakedMesh.vertexCount == sharedMesh.vertexCount)
                {
                    return bakedMesh.vertices;
                }

                Debug.LogWarning(
                    $"Magica Collider Builder: BakeMesh vertex count mismatch on {renderer.name}. " +
                    "Renderer skipped.");

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"Magica Collider Builder: Failed to bake current pose for {renderer.name}. " +
                    $"Renderer skipped. {e.Message}");

                return null;
            }
            finally
            {
                if (bakedMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(bakedMesh);
                }
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
