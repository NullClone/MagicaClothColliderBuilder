using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class ColliderGenerationJob
    {
        public GameObject TargetBone { get; }
        public readonly SABoneColliderProperty property;
        private readonly BoneMeshCache boneMeshCache;

        public Vector3[] Vertices { get; private set; }
        public int[] Triangles { get; private set; }
        public ReducerResult Result { get; private set; }

        internal CountdownEvent countdownEvent;

        public ColliderGenerationJob(GameObject targetBone, SABoneColliderProperty property, BoneMeshCache boneMeshCache)
        {
            this.TargetBone = targetBone;
            this.property = property;
            this.boneMeshCache = boneMeshCache;
        }

        public bool Prepare()
        {
            var boneMeshCreator = new BoneMeshCreator();
            if (!boneMeshCreator.Process(TargetBone, property.splitProperty, boneMeshCache))
            {
                return false;
            }
            Vertices = boneMeshCreator.boneVertices;
            Triangles = boneMeshCreator.boneTriangles;
            return Vertices != null && Vertices.Length > 0;
        }

        public void Execute(object state)
        {
            try
            {
                var reducer = new SAColliderBoxReducer
                {
                    reduceMode = ReduceMode.Box,
                    vertexList = Vertices,
                    lineList = TriangleToLineIndices(Triangles),
                    scale = property.reducerProperty.scale,
                    minThickness = property.reducerProperty.minThickness,
                    offset = property.reducerProperty.offset,
                    thicknessA = property.reducerProperty.thicknessA,
                    thicknessB = property.reducerProperty.thicknessB,
                    postfixTransform = true,
                    rotation = Quaternion.identity
                };
                reducer.Reduce();

                Result = new ReducerResult
                {
                    rotation = reducer.reducedRotation,
                    center = reducer.reducedCenter,
                    boxA = reducer.reducedBoxA,
                    boxB = reducer.reducedBoxB,
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing bone {TargetBone.name}: {e}");
            }
            finally
            {
                countdownEvent?.Signal();
            }
        }

        private static int[] TriangleToLineIndices(int[] triangles)
        {
            if (triangles == null || triangles.Length == 0)
            {
                return null;
            }

            var lineKeys = new HashSet<ulong>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                lineKeys.Add(_MakeLineKey(triangles[t + 0], triangles[t + 1]));
                lineKeys.Add(_MakeLineKey(triangles[t + 1], triangles[t + 2]));
                lineKeys.Add(_MakeLineKey(triangles[t + 2], triangles[t + 0]));
            }

            var lines = new List<int>(lineKeys.Count * 2);
            foreach (ulong lineKey in lineKeys)
            {
                lines.Add(unchecked((int)(uint)lineKey));
                lines.Add(unchecked((int)(uint)(lineKey >> 32)));
            }

            return lines.ToArray();
        }

        private static ulong _MakeLineKey(int index0, int index1)
        {
            return (index0 < index1)
                ? (uint)index0 | ((ulong)(uint)index1 << 32)
                : (uint)index1 | ((ulong)(uint)index0 << 32);
        }
    }
}