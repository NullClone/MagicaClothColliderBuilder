using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class ColliderGenerationJob
    {
        // Properties

        public GameObject TargetBone { get; }

        public SABoneColliderProperty Property { get; }

        public Vector3[] Vertices { get; private set; }

        public int[] Triangles { get; private set; }

        public Animator Animator { get; }

        private readonly BoneMeshCache m_BoneMeshCache;

        internal CountdownEvent m_CountdownEvent;


        // Methods

        public ColliderGenerationJob(GameObject targetBone, Animator animator, SABoneColliderProperty property, BoneMeshCache boneMeshCache)
        {
            TargetBone = targetBone;
            Property = property;
            m_BoneMeshCache = boneMeshCache;
            Animator = animator;
        }

        public bool Prepare()
        {
            var boneMeshCreator = new BoneMeshCreator();

            if (!boneMeshCreator.Process(TargetBone, Property.SplitProperty, m_BoneMeshCache)) return false;

            Vertices = boneMeshCreator.BoneVertices;
            Triangles = boneMeshCreator.BoneTriangles;

            return Vertices != null && Vertices.Length > 0;
        }

        public void Execute(object state)
        {
            try
            {
                var reducer = new MagicaClothColliderBoxReducer
                {
                    ReduceMode = ReduceMode.Box,
                    VertexList = Vertices,
                    LineList = TriangleToLineIndices(Triangles),
                    Scale = Property.ReducerProperty.Scale,
                    MinThickness = Property.ReducerProperty.MinThickness,
                    Offset = Property.ReducerProperty.Offset,
                    ThicknessA = Property.ReducerProperty.ThicknessA,
                    ThicknessB = Property.ReducerProperty.ThicknessB,
                    PostfixTransform = true
                };

                reducer.Reduce();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing bone {TargetBone.name}: {e}");
            }
            finally
            {
                m_CountdownEvent?.Signal();
            }
        }

        private static int[] TriangleToLineIndices(int[] triangles)
        {
            if (triangles == null || triangles.Length == 0) return null;

            var lineKeys = new HashSet<ulong>();

            for (int t = 0; t < triangles.Length; t += 3)
            {
                lineKeys.Add(MakeLineKey(triangles[t + 0], triangles[t + 1]));
                lineKeys.Add(MakeLineKey(triangles[t + 1], triangles[t + 2]));
                lineKeys.Add(MakeLineKey(triangles[t + 2], triangles[t + 0]));
            }

            var lines = new List<int>(lineKeys.Count * 2);

            foreach (ulong lineKey in lineKeys)
            {
                lines.Add(unchecked((int)(uint)lineKey));
                lines.Add(unchecked((int)(uint)(lineKey >> 32)));
            }

            return lines.ToArray();
        }

        private static ulong MakeLineKey(int index0, int index1)
        {
            return (index0 < index1) ? (uint)index0 | ((ulong)(uint)index1 << 32) : (uint)index1 | ((ulong)(uint)index0 << 32);
        }
    }
}