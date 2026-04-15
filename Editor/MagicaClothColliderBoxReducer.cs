using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class MagicaClothColliderBoxReducer
    {
        private ReduceMode m_reduceMode = ReduceMode.Mesh;
        private SliceMode m_sliceMode = SliceMode.Auto;
        private Vector3 m_minThickness = Vector3.zero;
        private Vector3[] m_vertexList = null;
        private bool[] m_usedVertexList = null;
        private int[] m_lineList = null;
        private Vector3 m_center = Vector3.zero;
        private bool m_centerEnabled = false;
        private Quaternion m_rotation = Quaternion.identity;
        private bool m_rotationEnabled = false;
        private Bool3 m_optimizeRotation = new Bool3(true, true, true);
        private Vector3 m_scale = Vector3.one;
        private Vector3 m_offset = Vector3.zero;
        private Vector3 m_thicknessA = Vector3.zero;
        private Vector3 m_thicknessB = Vector3.zero;
        private Vector3 m_boundingBoxA = Vector3.zero;
        private Vector3 m_boundingBoxB = Vector3.zero;
        private int m_sliceCount = 31;
        private int m_slicedDimention = 0;
        private Vector3[] m_slicedBoundingBoxA = null;
        private Vector3[] m_slicedBoundingBoxB = null;
        private Vector3[] m_slicedBoundingBoxC = null;
        private Vector3[] m_slicedBoundingBoxD = null;
        private Vector3[] m_slicedVertexList = null;
        private int[] m_slicedIndexList = null;
        private Vector3[] m_reducedVertexList = null;
        private int[] m_reducedIndexList = null;
        private Quaternion m_reducedRotation = Quaternion.identity;
        private Vector3 m_reducedCenter = Vector3.zero;
        private Vector3 m_reducedBoxA = Vector3.zero;
        private Vector3 m_reducedBoxB = Vector3.zero;
        private bool m_postfixTransform = true;

        public ReduceMode ReduceMode { set { m_reduceMode = value; } }
        public SliceMode SliceMode { set { m_sliceMode = value; } }
        public int SliceCount { set { m_sliceCount = value; } }
        public Vector3 MinThickness { set { m_minThickness = value; } }
        public Quaternion Rotation { set { m_rotationEnabled = true; m_rotation = value; } }
        public Vector3 Center { set { m_centerEnabled = true; m_center = value; } }
        public Bool3 OptimizeRotation { set { m_optimizeRotation = value; } }
        public Vector3 Scale { set { m_scale = value; } }
        public Vector3 Offset { set { m_offset = value; } }
        public Vector3 ThicknessA { set { m_thicknessA = value; } }
        public Vector3 ThicknessB { set { m_thicknessB = value; } }
        public Vector3[] VertexList { set { m_vertexList = value; } }
        public int[] LineList { set { m_lineList = value; } }
        public bool PostfixTransform { set { m_postfixTransform = value; } }
        public Vector3[] ReducedVertexList { get { return m_reducedVertexList; } }
        public int[] ReducedIndexList { get { return m_reducedIndexList; } }
        public Quaternion ReducedRotation { get { return m_reducedRotation; } }
        public Vector3 ReducedCenter { get { return m_reducedCenter; } }
        public Vector3 ReducedBoxA { get { return m_reducedBoxA; } }
        public Vector3 ReducedBoxB { get { return m_reducedBoxB; } }

        //----------------------------------------------------------------------------------------------------------------------------

        public void Reduce()
        {
            m_usedVertexList = null;
            if (m_vertexList != null && m_lineList != null)
            {
                m_usedVertexList = new bool[m_vertexList.Length];
                for (int i = 0; i < m_lineList.Length; ++i)
                {
                    m_usedVertexList[m_lineList[i]] = true;
                }
            }
            else
            {
                m_usedVertexList = new bool[m_vertexList.Length];
                for (int i = 0; i < m_usedVertexList.Length; ++i)
                {
                    m_usedVertexList[i] = true;
                }
            }

            Vector3 minCenter = Vector3.zero;
            Vector3 minBoxA = Vector3.zero;
            Vector3 minBoxB = Vector3.zero;
            Vector3 minEular = Vector3.zero;

            m_GetMinBoundingBoxAABB(ref minCenter, ref minBoxA, ref minBoxB, ref minEular);

            Matrix4x4 reducedTransform = Matrix4x4.identity;

            {
                m_reducedCenter = minCenter;
                m_reducedBoxA = minBoxA;
                m_reducedBoxB = minBoxB;

                Quaternion reduceRotation = Quaternion.identity;
                if (m_rotationEnabled)
                {
                    reduceRotation = InversedRotation(m_rotation);
                    m_reducedRotation = m_rotation;
                }
                else
                {
                    reduceRotation = Quaternion.Euler(minEular);
                    m_reducedRotation = InversedRotation(reduceRotation);
                }

                if (m_reduceMode == ReduceMode.Mesh || m_reduceMode == ReduceMode.BoxMesh)
                {
                    Matrix4x4 reduceTransform = m_TranslateRotationMatrix(-m_reducedCenter, reduceRotation);
                    m_TransformVertexList(ref reduceTransform); /* Adjust for Mesh. */
                    reducedTransform = reduceTransform.inverse;
                }
            }

            if (m_reduceMode == ReduceMode.Mesh)
            {
                m_boundingBoxA = minBoxA;
                m_boundingBoxB = minBoxB;
                if (m_MakeSlicedBoundingBoxAABB())
                {
                    m_MakeSlicedListFromBoundingBox();
                }
                else
                {
                    m_reduceMode = ReduceMode.BoxMesh;
                }
            }

            if (m_reduceMode == ReduceMode.Box || m_reduceMode == ReduceMode.BoxMesh)
            {
                m_ComputeMinThickness(ref minBoxA.x, ref minBoxB.x, m_minThickness[0]);
                m_ComputeMinThickness(ref minBoxA.y, ref minBoxB.y, m_minThickness[1]);
                m_ComputeMinThickness(ref minBoxA.z, ref minBoxB.z, m_minThickness[2]);

                if (m_scale != Vector3.one)
                {
                    minBoxA = ScaledVector(minBoxA, m_scale);
                    minBoxB = ScaledVector(minBoxB, m_scale);
                }
                if (m_thicknessA != Vector3.zero || m_thicknessB != Vector3.zero)
                {
                    minBoxA += m_thicknessA;
                    minBoxB += m_thicknessB;
                }
                if (m_offset != Vector3.zero)
                {
                    minBoxA += m_offset;
                    minBoxB += m_offset;
                }

                m_reducedBoxA = minBoxA;
                m_reducedBoxB = minBoxB;
                m_boundingBoxA = minBoxA;
                m_boundingBoxB = minBoxB;
                if (m_reduceMode == ReduceMode.BoxMesh)
                {
                    m_MakeSlicedListFromAABB(minBoxA, minBoxB);
                }
            }

            if (m_reduceMode == ReduceMode.Mesh || m_reduceMode == ReduceMode.BoxMesh)
            {
                m_MakeReducedListFromSlicedList();
                if (m_postfixTransform)
                {
                    m_TransformReducedList(ref reducedTransform);
                }
            }
        }

        //----------------------------------------------------------------------------------------------------------------------------

        static void m_ComputeMinThickness(ref float boxA, ref float boxB, float minThickness)
        {
            float depth = Mathf.Abs(boxB - boxA);
            if (depth < minThickness)
            {
                float center = (boxA + boxB) * 0.5f;
                if (boxA <= boxB)
                {
                    boxA = center - (minThickness * 0.5f);
                    boxB = center + (minThickness * 0.5f);
                }
                else
                {
                    boxA = center + (minThickness * 0.5f);
                    boxB = center - (minThickness * 0.5f);
                }
            }
        }

        //----------------------------------------------------------------------------------------------------------------------------

        Vector3 m_GetBoundingBoxCenterAABB()
        {
            if (m_vertexList == null || m_usedVertexList == null)
            {
                return Vector3.zero;
            }

            Vector3 boxA = Vector3.zero;
            Vector3 boxB = Vector3.zero;
            bool setAnything = false;
            for (int i = 0; i < m_vertexList.Length; ++i)
            {
                if (m_usedVertexList[i])
                {
                    if (!setAnything)
                    {
                        setAnything = true;
                        boxA = boxB = m_vertexList[i];
                    }
                    else
                    {
                        boxA = Min(boxA, m_vertexList[i]);
                        boxB = Max(boxB, m_vertexList[i]);
                    }
                }
            }

            return (boxA + boxB) * 0.5f;
        }

        public static Matrix4x4 m_RotationMatrix(Quaternion rotation)
        {
            return Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);
        }

        public static Matrix4x4 m_TranslateRotationMatrix(Vector3 translate, Quaternion rotation)
        {
            Matrix4x4 translateTransform = Matrix4x4.identity;
            translateTransform.SetColumn(3, new Vector4(translate.x, translate.y, translate.z, 1.0f));
            return Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one) * translateTransform;
        }

        public struct Euler
        {
            public int x, y, z;

            public Euler(int x, int y, int z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public void SetValue(int x, int y, int z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        public struct MinBounding
        {
            public Vector3 boxA;
            public Vector3 boxB;
            public Euler euler;
            public float volume;
            public bool setted;

            public void Set(Vector3 boxA, Vector3 boxB, Euler euler, float volume)
            {
                this.boxA = boxA;
                this.boxB = boxB;
                this.euler = euler;
                this.volume = volume;
                this.setted = true;
            }

            public void Set(ref MinBounding minBounding)
            {
                this.boxA = minBounding.boxA;
                this.boxB = minBounding.boxB;
                this.euler = minBounding.euler;
                this.volume = minBounding.volume;
                this.setted = minBounding.setted;
            }

            public void Contain(Vector3 boxA, Vector3 boxB, Euler euler, float volume)
            {
                if (!this.setted || this.volume > volume)
                {
                    Set(boxA, boxB, euler, volume);
                }
            }

            public void Contain(ref MinBounding minBounding)
            {
                if (!this.setted || this.volume > minBounding.volume)
                {
                    Set(ref minBounding);
                }
            }
        }

        public struct SharedMinBounding
        {
            public MinBounding minBounding;

            public void Contain(ref MinBounding minBounding)
            {
                this.minBounding.Contain(ref minBounding);
            }
        }

        static void m_ProcessBoundingBoxAABB(
            ref SharedMinBounding sharedMinBounding,
            Vector3[] vertices,
            bool[] usedVertices,
            Vector3 minCenter,
            Euler beginEuler,
            Euler endEuler,
            int stepEuler)
        {
            if (vertices == null || usedVertices == null)
            {
                return;
            }

            Matrix4x4 transform = Matrix4x4.identity;
            MinBounding minBounding = new MinBounding();
            for (int rz = beginEuler.z; rz < endEuler.z; rz += stepEuler)
            {
                for (int ry = beginEuler.y; ry < endEuler.y; ry += stepEuler)
                {
                    for (int rx = beginEuler.x; rx < endEuler.x; rx += stepEuler)
                    {
                        transform.SetTRS(Vector3.zero, Quaternion.Euler(rx, ry, rz), Vector3.one);
                        Vector3 tempBoxA = Vector3.zero, tempBoxB = Vector3.zero;
                        m_GetBoundingBoxAABB(vertices, usedVertices, ref tempBoxA, ref tempBoxB, ref minCenter, ref transform);
                        Vector3 v = tempBoxB - tempBoxA;
                        float tempVolume = m_GetVolume(v);
                        Euler tempEuler = new Euler(rx, ry, rz);
                        minBounding.Contain(tempBoxA, tempBoxB, tempEuler, tempVolume);
                    }
                }
            }

            sharedMinBounding.Contain(ref minBounding);
        }

        static void m_GetBoundingBoxAABB(Vector3[] vertices, bool[] usedVertices, ref Vector3 boxA, ref Vector3 boxB, ref Vector3 minCenter, ref Matrix4x4 transform)
        {
            boxA = Vector3.zero;
            boxB = Vector3.zero;
            bool setAnything = false;
            for (int i = 0; i < vertices.Length; ++i)
            {
                if (usedVertices[i])
                {
                    Vector3 v = transform.MultiplyPoint3x4(vertices[i] - minCenter);
                    if (!setAnything)
                    {
                        setAnything = true;
                        boxA = boxB = v;
                    }
                    else
                    {
                        boxA = Min(boxA, v);
                        boxB = Max(boxB, v);
                    }
                }
            }
        }

        void m_GetBoundingBoxAABB(ref Vector3 boxA, ref Vector3 boxB, ref Vector3 minCenter, ref Matrix4x4 transform)
        {
            boxA = Vector3.zero;
            boxB = Vector3.zero;
            if (m_vertexList != null && m_usedVertexList != null)
            {
                m_GetBoundingBoxAABB(m_vertexList, m_usedVertexList, ref boxA, ref boxB, ref minCenter, ref transform);
            }
        }

        void m_GetMinBoundingBoxAABB(ref Vector3 minCenter, ref Vector3 minBoxA, ref Vector3 minBoxB, ref Vector3 minEular)
        {
            if (m_centerEnabled)
            {
                minCenter = m_center;
            }
            else
            {
                minCenter = m_GetBoundingBoxCenterAABB();
            }
            //minCenter = Vector3.zero;

            if (m_rotationEnabled)
            {
                minBoxA = Vector3.zero;
                minBoxB = Vector3.zero;
                minEular = Vector3.zero;
                Matrix4x4 transform = m_RotationMatrix(InversedRotation(m_rotation));
                m_GetBoundingBoxAABB(ref minBoxA, ref minBoxB, ref minCenter, ref transform);
                return;
            }

#if false
		{
			minBoxA = Vector3.zero;
			minBoxB = Vector3.zero;
			minEular = Vector3.zero;
			Matrix4x4 transform = Matrix4x4.identity;
			m_GetBoundingBoxAABB( ref minBoxA, ref minBoxB, ref minCenter, ref transform );
		}
#else
            int stepEuler = 20;
            int stepEuler2 = 5;
            int stepEuler3 = 1;

            SharedMinBounding sharedMinBounding = new SharedMinBounding();

            {
                Euler beginEuler = new Euler(0, 0, 0);
                Euler endEuler = new Euler(180, 180, 180);
                if (!m_optimizeRotation.X) { beginEuler.x = 0; endEuler.x = 1; }
                if (!m_optimizeRotation.Y) { beginEuler.y = 0; endEuler.y = 1; }
                if (!m_optimizeRotation.Z) { beginEuler.z = 0; endEuler.z = 1; }

                m_ProcessBoundingBoxAABB(
                    ref sharedMinBounding,
                    m_vertexList,
                    m_usedVertexList,
                    minCenter,
                    beginEuler,
                    endEuler,
                    stepEuler);
            }

            {
                int fx = sharedMinBounding.minBounding.euler.x;
                int fy = sharedMinBounding.minBounding.euler.y;
                int fz = sharedMinBounding.minBounding.euler.z;
                Euler beginEuler = new Euler(fx - stepEuler, fy - stepEuler, fz - stepEuler);
                Euler endEuler = new Euler(fx + stepEuler, fy + stepEuler, fz + stepEuler);
                if (!m_optimizeRotation.X) { beginEuler.x = 0; endEuler.x = 1; }
                if (!m_optimizeRotation.Y) { beginEuler.y = 0; endEuler.y = 1; }
                if (!m_optimizeRotation.Z) { beginEuler.z = 0; endEuler.z = 1; }

                m_ProcessBoundingBoxAABB(
                    ref sharedMinBounding,
                    m_vertexList,
                    m_usedVertexList,
                    minCenter,
                    beginEuler,
                    endEuler,
                    stepEuler2);
            }

            {
                int fx = sharedMinBounding.minBounding.euler.x;
                int fy = sharedMinBounding.minBounding.euler.y;
                int fz = sharedMinBounding.minBounding.euler.z;
                Euler beginEuler = new Euler(fx - stepEuler2, fy - stepEuler2, fz - stepEuler2);
                Euler endEuler = new Euler(fx + stepEuler2, fy + stepEuler2, fz + stepEuler2);
                if (!m_optimizeRotation.X) { beginEuler.x = 0; endEuler.x = 1; }
                if (!m_optimizeRotation.Y) { beginEuler.y = 0; endEuler.y = 1; }
                if (!m_optimizeRotation.Z) { beginEuler.z = 0; endEuler.z = 1; }

                m_ProcessBoundingBoxAABB(
                    ref sharedMinBounding,
                    m_vertexList,
                    m_usedVertexList,
                    minCenter,
                    beginEuler,
                    endEuler,
                    stepEuler3);
            }

            Euler euler = sharedMinBounding.minBounding.euler;
            minBoxA = sharedMinBounding.minBounding.boxA;
            minBoxB = sharedMinBounding.minBounding.boxB;
            minEular = new Vector3(euler.x, euler.y, euler.z);
#endif
        }

        bool m_MakeSlicedBoundingBoxAABB()
        {
            int SliceCount = m_sliceCount;
            float f_SliceCount = m_sliceCount;

            int minimumDim = -1;
            float minimumVolume = 0.0f;
            Vector3[] minimumBoxA = null;
            Vector3[] minimumBoxB = null;

            /* memo: Choose minimum box in 3 direction. */
            for (int i = 0; i < 3; ++i)
            {
                /* Compute AABB in 31 dividing box. */
                List<Vector3> tempBoxA = new List<Vector3>(SliceCount);
                List<Vector3> tempBoxB = new List<Vector3>(SliceCount);
                switch (i)
                {
                    case 0: // X
                        if (minimumDim < 0 || m_sliceMode == SliceMode.Auto || m_sliceMode == SliceMode.X)
                        {
                            Matrix4x4 transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(45.0f, 0.0f, 0.0f), Vector3.one); /* Right Hand(Left Rotation) */

                            // Limit search range
                            Vector3 boundingBoxA = m_boundingBoxA;
                            Vector3 boundingBoxB = m_boundingBoxB;
                            boundingBoxA[0] += m_thicknessA[0];
                            boundingBoxB[0] += m_thicknessB[0];

                            float minX = boundingBoxA.x;
                            float stepX = (boundingBoxB.x - boundingBoxA.x) / f_SliceCount;
                            float tempVolume = 0.0f;
                            for (int n = 0; n < SliceCount; ++n)
                            {
                                float maxX = minX + stepX;
                                Vector3 boxA = Vector3.zero, boxB = Vector3.zero;
                                if (m_GetBoundingBoxAABB(0, ref boxA, ref boxB, minX, maxX, ref transform))
                                {
                                    boxA.x = minX;
                                    boxB.x = maxX;
                                    tempBoxA.Add(boxA);
                                    tempBoxB.Add(boxB);
                                    tempVolume += m_GetBoxVolume(boxA, boxB);
                                }
                                minX = maxX;
                            }
                            if (tempVolume > Mathf.Epsilon)
                            {
                                minimumDim = 0;
                                minimumVolume = tempVolume;
                                minimumBoxA = tempBoxA.ToArray();
                                minimumBoxB = tempBoxB.ToArray();
                            }
                        }
                        break;
                    case 1: // Y
                        if (minimumDim < 0 || m_sliceMode == SliceMode.Auto || m_sliceMode == SliceMode.Y)
                        {
                            Matrix4x4 transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0.0f, 45.0f, 0.0f), Vector3.one); /* Right Hand(Left Rotation) */

                            // Limit search range
                            Vector3 boundingBoxA = m_boundingBoxA;
                            Vector3 boundingBoxB = m_boundingBoxB;
                            boundingBoxA[1] += m_thicknessA[1];
                            boundingBoxB[1] += m_thicknessB[1];

                            float minY = boundingBoxA.y;
                            float stepY = (boundingBoxB.y - boundingBoxA.y) / f_SliceCount;
                            float tempVolume = 0.0f;
                            for (int n = 0; n < SliceCount; ++n)
                            {
                                float maxY = minY + stepY;
                                Vector3 boxA = Vector3.zero, boxB = Vector3.zero;
                                if (m_GetBoundingBoxAABB(1, ref boxA, ref boxB, minY, maxY, ref transform))
                                {
                                    boxA.y = minY;
                                    boxB.y = maxY;
                                    tempBoxA.Add(boxA);
                                    tempBoxB.Add(boxB);
                                    tempVolume += m_GetBoxVolume(boxA, boxB);
                                }
                                minY = maxY;
                            }
                            if (tempVolume > Mathf.Epsilon)
                            {
                                if (m_sliceMode == SliceMode.Y || minimumVolume > tempVolume)
                                {
                                    minimumDim = 1;
                                    minimumVolume = tempVolume;
                                    minimumBoxA = tempBoxA.ToArray();
                                    minimumBoxB = tempBoxB.ToArray();
                                }
                            }
                        }
                        break;
                    case 2: // Z
                        if (minimumDim < 0 || m_sliceMode == SliceMode.Auto || m_sliceMode == SliceMode.Z)
                        {
                            Matrix4x4 transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0.0f, 0.0f, 45.0f), Vector3.one); /* Right Hand(Left Rotation) */

                            // Limit search range
                            Vector3 boundingBoxA = m_boundingBoxA;
                            Vector3 boundingBoxB = m_boundingBoxB;
                            boundingBoxA[2] += m_thicknessA[2];
                            boundingBoxB[2] += m_thicknessB[2];

                            float minZ = boundingBoxA.z;
                            float stepZ = (boundingBoxB.z - boundingBoxA.z) / f_SliceCount;
                            float tempVolume = 0.0f;
                            for (int n = 0; n < SliceCount; ++n)
                            {
                                float maxZ = minZ + stepZ;
                                Vector3 boxA = Vector3.zero, boxB = Vector3.zero;
                                if (m_GetBoundingBoxAABB(2, ref boxA, ref boxB, minZ, maxZ, ref transform))
                                {
                                    boxA.z = minZ;
                                    boxB.z = maxZ;
                                    tempBoxA.Add(boxA);
                                    tempBoxB.Add(boxB);
                                    tempVolume += m_GetBoxVolume(boxA, boxB);
                                }
                                minZ = maxZ;
                            }
                            if (tempVolume > Mathf.Epsilon)
                            {
                                if (m_sliceMode == SliceMode.Z || minimumVolume > tempVolume)
                                {
                                    minimumDim = 2;
                                    minimumVolume = tempVolume;
                                    minimumBoxA = tempBoxA.ToArray();
                                    minimumBoxB = tempBoxB.ToArray();
                                }
                            }
                        }
                        break;
                }
            }

            if (minimumDim < 0 || minimumBoxA == null || minimumBoxA.Length == 0)
            {
                return false;
            }

            Vector3 thicknessA = m_thicknessA;
            Vector3 thicknessB = m_thicknessB;
            thicknessA[minimumDim] = 0;
            thicknessB[minimumDim] = 0;

            if (m_minThickness != Vector3.zero)
            {
                for (int i = 0; i < minimumBoxA.Length; ++i)
                {
                    if (minimumDim != 0)
                    {
                        m_ComputeMinThickness(ref minimumBoxA[i].x, ref minimumBoxB[i].x, m_minThickness.x);
                    }
                    if (minimumDim != 1)
                    {
                        m_ComputeMinThickness(ref minimumBoxA[i].y, ref minimumBoxB[i].y, m_minThickness.y);
                    }
                    if (minimumDim != 2)
                    {
                        m_ComputeMinThickness(ref minimumBoxA[i].z, ref minimumBoxB[i].z, m_minThickness.z);
                    }
                }

                int end = minimumBoxA.Length - 1;
                if (minimumDim == 0)
                {
                    m_ComputeMinThickness(ref minimumBoxA[0].x, ref minimumBoxB[end].x, m_minThickness.x);
                }
                if (minimumDim == 1)
                {
                    m_ComputeMinThickness(ref minimumBoxA[0].y, ref minimumBoxB[end].y, m_minThickness.y);
                }
                if (minimumDim == 2)
                {
                    m_ComputeMinThickness(ref minimumBoxA[0].z, ref minimumBoxB[end].z, m_minThickness.z);
                }
            }

            if (m_scale != Vector3.one)
            {
                for (int i = 0; i < minimumBoxA.Length; ++i)
                {
                    minimumBoxA[i] = ScaledVector(minimumBoxA[i], m_scale);
                    minimumBoxB[i] = ScaledVector(minimumBoxB[i], m_scale);
                }
            }

            if (thicknessA != Vector3.zero || thicknessB != Vector3.zero)
            {
                for (int i = 0; i < minimumBoxA.Length; ++i)
                {
                    minimumBoxA[i] += thicknessA;
                    minimumBoxB[i] += thicknessB;
                }
            }
            if (m_offset != Vector3.zero)
            {
                for (int i = 0; i < minimumBoxA.Length; ++i)
                {
                    minimumBoxA[i] += m_offset;
                    minimumBoxB[i] += m_offset;
                }
            }

            {
                BoxCollector boxCollector = new BoxCollector();
                boxCollector.Collect(minimumBoxA);
                boxCollector.Collect(minimumBoxB);
                if (boxCollector.isAnything)
                {
                    m_reducedBoxA = boxCollector.boxA;
                    m_reducedBoxB = boxCollector.boxB;
                }
            }

            m_slicedDimention = minimumDim;
            m_slicedBoundingBoxA = minimumBoxA.Clone() as Vector3[];
            m_slicedBoundingBoxB = minimumBoxB.Clone() as Vector3[];
            m_slicedBoundingBoxC = minimumBoxA.Clone() as Vector3[];
            m_slicedBoundingBoxD = minimumBoxB.Clone() as Vector3[];

            /* AB ... Plane of begin CD ... Plane of end */
            /* AB / CD are diagonal planes. AB / CD planes are parallel. */
            /* Adjacent CD/AB is equal minimumDim(X,Y,Z). */
            /* Adjacent CD/AB is equal after Combine optimized. */
            for (int i = 0; i < minimumBoxA.Length; ++i)
            {
                m_slicedBoundingBoxB[i][minimumDim] = m_slicedBoundingBoxA[i][minimumDim];
                m_slicedBoundingBoxC[i][minimumDim] = m_slicedBoundingBoxD[i][minimumDim];
            }
            /* Combine optimized.(Optimized vertices, exclude begin/end plane.) */
            for (int i = 1; i < minimumBoxA.Length; ++i)
            {
                /* Average VertexA. */
                Vector3 boxA1 = minimumBoxA[i];
                Vector3 boxA0 = minimumBoxA[i - 1];
                boxA0[minimumDim] = boxA1[minimumDim];
                Vector3 boxAM = (boxA1 + boxA0) / 2.0f;
                /* Copy arrangemented vertexA. */
                m_slicedBoundingBoxA[i] = boxAM;
                m_slicedBoundingBoxC[i - 1] = boxAM;
                /* Average VertexB */
                Vector3 boxB0 = minimumBoxB[i - 1];
                Vector3 boxB1 = minimumBoxB[i];
                boxB1[minimumDim] = boxB0[minimumDim];
                Vector3 boxBM = (boxB1 + boxB0) / 2.0f;
                /* Copy arrangemented vertexB. */
                m_slicedBoundingBoxB[i] = boxBM;
                m_slicedBoundingBoxD[i - 1] = boxBM;
            }

            return true;
        }

        bool m_GetBoundingBoxAABB(int DIM_, ref Vector3 boxA, ref Vector3 boxB, float minV, float maxV, ref Matrix4x4 innerTransform)
        {
            BoxCollector boxCollector = new BoxCollector();
            //BoxCollector boxCollector2 = new BoxCollector(); // for Inner

            if (m_lineList != null)
            {
                for (int i = 0, count = m_lineList.Length / 2 * 2; i < count; i += 2)
                {
                    //assert( (int)m_lineList[i + 0] < m_vertexList.size() && (int)m_lineList[i + 1] < m_vertexList.size() );
                    Vector3 vertex0 = m_vertexList[m_lineList[i + 0]];
                    Vector3 vertex1 = m_vertexList[m_lineList[i + 1]];
                    if (vertex0[DIM_] > vertex1[DIM_])
                    {
                        m_Swap(ref vertex0, ref vertex1);
                    }
                    if (vertex0[DIM_] >= maxV || vertex1[DIM_] <= minV)
                    {
                        // Out of range.
                    }
                    else
                    {
                        if (vertex0[DIM_] < minV)
                        {
                            /* Begin point smaller than minV */
                            if (!m_FuzzyZero(vertex0[DIM_] - vertex1[DIM_]))
                            { /* Overlap check( Check for 0 divide ) */
                                /* Compute cross point in target area(minV) */
                                Vector3 modVertex0 = vertex0 + ((vertex1 - vertex0) * (minV - vertex0[DIM_]) / (vertex1[DIM_] - vertex0[DIM_]));
                                boxCollector.Collect(modVertex0);
                            }
                        }
                        else
                        {
                            boxCollector.Collect(vertex0);
                        }
                        if (vertex1[DIM_] > maxV)
                        {
                            /* End point bigger than maxV */
                            if (!m_FuzzyZero(vertex0[DIM_] - vertex1[DIM_]))
                            { /* Overlap check( Check for 0 divide ) */
                                /* Compute cross point in target area(maxV) */
                                Vector3 modVertex1 = vertex1 + ((vertex0 - vertex1) * (vertex1[DIM_] - maxV) / (vertex1[DIM_] - vertex0[DIM_]));
                                boxCollector.Collect(modVertex1);
                            }
                        }
                        else
                        {
                            boxCollector.Collect(vertex1);
                        }
                    }
                }
            }

            boxA = boxCollector.boxA;
            boxB = boxCollector.boxB;
            if (boxA == boxB)
            {
                return false;
            }
            boxA[DIM_] = minV;
            boxB[DIM_] = maxV;
            return true;
        }

        void m_MakeSlicedListFromAABB(Vector3 boxA, Vector3 boxB)
        {
            Vector3[] vertices = new Vector3[] {
            new Vector3( boxA.x, boxA.y, boxA.z ),
            new Vector3( boxA.x, boxB.y, boxA.z ),
            new Vector3( boxB.x, boxB.y, boxA.z ),
            new Vector3( boxB.x, boxA.y, boxA.z ),
            new Vector3( boxA.x, boxA.y, boxB.z ),
            new Vector3( boxA.x, boxB.y, boxB.z ),
            new Vector3( boxB.x, boxB.y, boxB.z ),
            new Vector3( boxB.x, boxA.y, boxB.z ),
        };

            int[] indices = new int[] {
            0, 1, 2, 3, /* Front side */
            3, 2, 6, 7, /* Right side */
            4, 5, 1, 0, /* Left side */
            1, 5, 6, 2, /* Upper side */
            4, 0, 3, 7, /* Lower side */
            7, 6, 5, 4, /* Back side */
        };

            m_slicedVertexList = vertices;
            m_slicedIndexList = indices;
        }

        static void m_AddSurface(List<int> indexList, int[] indices, int ptr, int ofst)
        {
            for (int i = 0; i < 4; ++i)
            {
                indexList.Add(indices[i + ptr] + ofst);
            }
        }

        void m_MakeSlicedListFromBoundingBox()
        {
            if (m_slicedBoundingBoxA == null)
            {
                Debug.LogError("");
                return;
            }

            List<Vector3> slicedVertexList = new List<Vector3>(8 * m_slicedBoundingBoxA.Length);
            List<int> slicedIndexList = new List<int>(24 * m_slicedBoundingBoxA.Length);

            /* Add Front/Back plane has LeftDown/LeftUp/RightUp/RightDown vertices. */
            for (int n = 0; n < m_slicedBoundingBoxA.Length; ++n)
            {
                Vector3 boxA = m_slicedBoundingBoxA[n];
                Vector3 boxB = m_slicedBoundingBoxB[n];
                Vector3 boxC = m_slicedBoundingBoxC[n];
                Vector3 boxD = m_slicedBoundingBoxD[n];
                switch (m_slicedDimention)
                {
                    case 0: // X
                        {
                            Vector3[] vertices = new Vector3[] {
                        new Vector3( boxA.x, boxA.y, boxA.z ),
                        new Vector3( boxA.x, boxB.y, boxA.z ),
                        new Vector3( boxD.x, boxD.y, boxC.z ),
                        new Vector3( boxC.x, boxC.y, boxC.z ),
                        new Vector3( boxA.x, boxA.y, boxB.z ),
                        new Vector3( boxA.x, boxB.y, boxB.z ),
                        new Vector3( boxD.x, boxD.y, boxD.z ),
                        new Vector3( boxC.x, boxC.y, boxD.z ),
                    };
                            for (int i = 0; i < vertices.Length; ++i)
                            {
                                slicedVertexList.Add(vertices[i]);
                            }
                        }
                        break;
                    case 1: // Y
                        {
                            Vector3[] vertices = new Vector3[] {
                        new Vector3( boxA.x, boxA.y, boxA.z ),
                        new Vector3( boxC.x, boxC.y, boxC.z ),
                        new Vector3( boxD.x, boxC.y, boxC.z ),
                        new Vector3( boxB.x, boxB.y, boxA.z ),
                        new Vector3( boxA.x, boxA.y, boxB.z ),
                        new Vector3( boxC.x, boxC.y, boxD.z ),
                        new Vector3( boxD.x, boxC.y, boxD.z ),
                        new Vector3( boxB.x, boxB.y, boxB.z ),
                    };
                            for (int i = 0; i < vertices.Length; ++i)
                            {
                                slicedVertexList.Add(vertices[i]);
                            }
                        }
                        break;
                    case 2: // Z
                        {
                            Vector3[] vertices = new Vector3[] {
                        new Vector3( boxA.x, boxA.y, boxA.z ),
                        new Vector3( boxA.x, boxB.y, boxA.z ),
                        new Vector3( boxB.x, boxB.y, boxA.z ),
                        new Vector3( boxB.x, boxA.y, boxA.z ),
                        new Vector3( boxC.x, boxC.y, boxD.z ),
                        new Vector3( boxC.x, boxD.y, boxD.z ),
                        new Vector3( boxD.x, boxD.y, boxD.z ),
                        new Vector3( boxD.x, boxC.y, boxD.z ),
                    };
                            for (int i = 0; i < vertices.Length; ++i)
                            {
                                slicedVertexList.Add(vertices[i]);
                            }
                        }
                        break;
                }
            }

            int[] indices = new int[] {
            0, 1, 2, 3, /* Front */
            3, 2, 6, 7, /* Right */
            4, 5, 1, 0, /* Left */
            1, 5, 6, 2, /* Upper */
            4, 0, 3, 7, /* Lower */
            7, 6, 5, 4, /* Back */
        };

            switch (m_slicedDimention)
            {
                case 0: // X
                    for (int n = 0, ofst = 0; n < m_slicedBoundingBoxA.Length; ++n, ofst += 8)
                    {
                        m_AddSurface(slicedIndexList, indices, 4 * 0, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 3, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 4, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 5, ofst);
                        /* Close left side */
                        if (n == 0)
                        {
                            m_AddSurface(slicedIndexList, indices, 4 * 2, ofst);
                        }
                        /* Close right side */
                        if (n + 1 == m_slicedBoundingBoxA.Length)
                        {
                            m_AddSurface(slicedIndexList, indices, 4 * 1, ofst);
                        }
                    }
                    break;
                case 1: // Y
                    for (int n = 0, ofst = 0; n < m_slicedBoundingBoxA.Length; ++n, ofst += 8)
                    {
                        m_AddSurface(slicedIndexList, indices, 4 * 0, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 1, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 2, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 5, ofst);
                        if (n == 0)
                        {
                            m_AddSurface(slicedIndexList, indices, 4 * 4, ofst);
                        }
                        if (n + 1 == m_slicedBoundingBoxA.Length)
                        {
                            m_AddSurface(slicedIndexList, indices, 4 * 3, ofst);
                        }
                    }
                    break;
                case 2: // Z
                    for (int n = 0, ofst = 0; n < m_slicedBoundingBoxA.Length; ++n, ofst += 8)
                    {
                        m_AddSurface(slicedIndexList, indices, 4 * 1, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 2, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 3, ofst);
                        m_AddSurface(slicedIndexList, indices, 4 * 4, ofst);
                        if (n == 0)
                        {
                            m_AddSurface(slicedIndexList, indices, 4 * 0, ofst);
                        }
                        if (n + 1 == m_slicedBoundingBoxA.Length)
                        {
                            m_AddSurface(slicedIndexList, indices, 4 * 5, ofst);
                        }
                    }
                    break;
            }

            m_slicedVertexList = slicedVertexList.ToArray();
            m_slicedIndexList = slicedIndexList.ToArray();
        }

        void m_MakeReducedListFromSlicedList()
        {
            if (m_slicedVertexList == null || m_slicedIndexList == null)
            {
                Debug.LogError("");
                return;
            }

            m_reducedVertexList = m_slicedVertexList.Clone() as Vector3[];
            m_reducedIndexList = new int[m_slicedIndexList.Length / 4 * 6];

            for (int i = 0, r = 0; i < m_slicedIndexList.Length / 4 * 4; i += 4, r += 6)
            {
                int i0 = m_slicedIndexList[i + 0];
                int i1 = m_slicedIndexList[i + 1];
                int i2 = m_slicedIndexList[i + 2];
                int i3 = m_slicedIndexList[i + 3];
                m_reducedIndexList[r + 0] = i0;
                m_reducedIndexList[r + 1] = i1;
                m_reducedIndexList[r + 2] = i2;
                m_reducedIndexList[r + 3] = i2;
                m_reducedIndexList[r + 4] = i3;
                m_reducedIndexList[r + 5] = i0;
            }
        }

        void m_TransformVertexList(ref Matrix4x4 transform)
        {
            if (m_vertexList != null && m_usedVertexList != null)
            {
                Vector3[] vertexList = new Vector3[m_vertexList.Length];
                for (int i = 0; i < m_vertexList.Length; ++i)
                {
                    if (m_usedVertexList[i])
                    {
                        vertexList[i] = transform.MultiplyPoint3x4(m_vertexList[i]);
                    }
                }
                m_vertexList = vertexList; // memo: Override vertexList.
            }
            else
            {
                Debug.LogError("");
            }
        }

        void m_TransformReducedList(ref Matrix4x4 transform)
        {
            if (m_reducedVertexList != null)
            {
                for (int i = 0; i < m_reducedVertexList.Length; ++i)
                {
                    m_reducedVertexList[i] = transform.MultiplyPoint3x4(m_reducedVertexList[i]);
                }
            }
            else
            {
                Debug.LogError("");
            }
        }

        //----------------------------------------------------------------------------------------------------------------------------

        class BoxCollector
        {
            public bool isAnything = false;
            public Vector3 boxA = Vector3.zero;
            public Vector3 boxB = Vector3.zero;

            public void Collect(Vector3 vertex)
            {
                if (!isAnything)
                {
                    isAnything = true;
                    boxA = boxB = vertex;
                }
                else
                {
                    boxA = Min(boxA, vertex);
                    boxB = Max(boxB, vertex);
                }
            }

            public void Collect(Vector3[] vertexArray)
            {
                if (vertexArray != null)
                {
                    for (int i = 0; i < vertexArray.Length; ++i)
                    {
                        Collect(vertexArray[i]);
                    }
                }
            }
        }

        static void m_Swap(ref Vector3 a, ref Vector3 b)
        {
            Vector3 t = a;
            a = b;
            b = t;
        }

        static bool m_FuzzyZero(float a)
        {
            return Mathf.Abs(a) <= Mathf.Epsilon;
        }

        static float m_GetVolume(Vector3 v)
        {
            return Mathf.Abs(v.x * v.y * v.z);
        }

        static float m_GetBoxVolume(Vector3 boxA, Vector3 boxB)
        {
            Vector3 t = boxB - boxA;
            return Mathf.Abs(t.x * t.y * t.z);
        }

        public static Vector3 Min(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Mathf.Min(a.x, b.x),
                Mathf.Min(a.y, b.y),
                Mathf.Min(a.z, b.z));
        }

        public static Vector3 Max(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Mathf.Max(a.x, b.x),
                Mathf.Max(a.y, b.y),
                Mathf.Max(a.z, b.z));
        }

        public static Vector3 ScaledVector(Vector3 v, Vector3 s)
        {
            return new Vector3(v.x * s.x, v.y * s.y, v.z * s.z);
        }

        public static Quaternion InversedRotation(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }
    }

    public enum ReduceMode
    {
        Box,
        BoxMesh,
        Mesh,
    }

    public enum SliceMode
    {
        Auto,
        X,
        Y,
        Z,
    }
}
