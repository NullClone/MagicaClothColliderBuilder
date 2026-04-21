using System.Collections.Generic;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public partial class MagicaClothColliderBoxReducer
    {
        private bool TryMakeSlicedBoundingBoxAabb()
        {
            int minimumDimension = -1;
            float minimumVolume = 0.0f;
            Vector3[] minimumBoxA = null;
            Vector3[] minimumBoxB = null;

            for (int dim = 0; dim < 3; ++dim)
            {
                TrySliceAlongAxis(dim, ref minimumDimension, ref minimumVolume, ref minimumBoxA, ref minimumBoxB);
            }

            if (minimumDimension < 0 || minimumBoxA == null || minimumBoxA.Length == 0) return false;

            Vector3 thicknessA = m_ThicknessA;
            Vector3 thicknessB = m_ThicknessB;
            thicknessA[minimumDimension] = 0;
            thicknessB[minimumDimension] = 0;

            if (m_MinThickness != Vector3.zero)
            {
                for (int i = 0; i < minimumBoxA.Length; ++i)
                {
                    if (minimumDimension != 0)
                    {
                        ComputeMinThickness(ref minimumBoxA[i].x, ref minimumBoxB[i].x, m_MinThickness.x);
                    }
                    if (minimumDimension != 1)
                    {
                        ComputeMinThickness(ref minimumBoxA[i].y, ref minimumBoxB[i].y, m_MinThickness.y);
                    }
                    if (minimumDimension != 2)
                    {
                        ComputeMinThickness(ref minimumBoxA[i].z, ref minimumBoxB[i].z, m_MinThickness.z);
                    }
                }

                int end = minimumBoxA.Length - 1;
                if (minimumDimension == 0)
                {
                    ComputeMinThickness(ref minimumBoxA[0].x, ref minimumBoxB[end].x, m_MinThickness.x);
                }
                if (minimumDimension == 1)
                {
                    ComputeMinThickness(ref minimumBoxA[0].y, ref minimumBoxB[end].y, m_MinThickness.y);
                }
                if (minimumDimension == 2)
                {
                    ComputeMinThickness(ref minimumBoxA[0].z, ref minimumBoxB[end].z, m_MinThickness.z);
                }
            }

            if (m_Scale != Vector3.one)
            {
                for (int i = 0; i < minimumBoxA.Length; ++i)
                {
                    minimumBoxA[i] = ScaledVector(minimumBoxA[i], m_Scale);
                    minimumBoxB[i] = ScaledVector(minimumBoxB[i], m_Scale);
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

            if (m_Offset != Vector3.zero)
            {
                for (int i = 0; i < minimumBoxA.Length; ++i)
                {
                    minimumBoxA[i] += m_Offset;
                    minimumBoxB[i] += m_Offset;
                }
            }

            BoxCollector boxCollector = new BoxCollector();
            boxCollector.Collect(minimumBoxA);
            boxCollector.Collect(minimumBoxB);
            if (boxCollector.HasAny)
            {
                m_ReducedBoxA = boxCollector.BoxA;
                m_ReducedBoxB = boxCollector.BoxB;
            }

            m_SlicedDimension = minimumDimension;
            m_SlicedBoundingBoxA = minimumBoxA.Clone() as Vector3[];
            m_SlicedBoundingBoxB = minimumBoxB.Clone() as Vector3[];
            m_SlicedBoundingBoxC = minimumBoxA.Clone() as Vector3[];
            m_SlicedBoundingBoxD = minimumBoxB.Clone() as Vector3[];

            for (int i = 0; i < minimumBoxA.Length; ++i)
            {
                m_SlicedBoundingBoxB[i][minimumDimension] = m_SlicedBoundingBoxA[i][minimumDimension];
                m_SlicedBoundingBoxC[i][minimumDimension] = m_SlicedBoundingBoxD[i][minimumDimension];
            }

            for (int i = 1; i < minimumBoxA.Length; ++i)
            {
                Vector3 boxA1 = minimumBoxA[i];
                Vector3 boxA0 = minimumBoxA[i - 1];
                boxA0[minimumDimension] = boxA1[minimumDimension];
                Vector3 boxAM = (boxA1 + boxA0) / 2.0f;
                m_SlicedBoundingBoxA[i] = boxAM;
                m_SlicedBoundingBoxC[i - 1] = boxAM;

                Vector3 boxB0 = minimumBoxB[i - 1];
                Vector3 boxB1 = minimumBoxB[i];
                boxB1[minimumDimension] = boxB0[minimumDimension];
                Vector3 boxBM = (boxB1 + boxB0) / 2.0f;
                m_SlicedBoundingBoxB[i] = boxBM;
                m_SlicedBoundingBoxD[i - 1] = boxBM;
            }

            return true;
        }

        private static readonly SliceMode[] s_DimSliceMode = { SliceMode.X, SliceMode.Y, SliceMode.Z };

        private void TrySliceAlongAxis(int dim, ref int minimumDimension, ref float minimumVolume, ref Vector3[] minimumBoxA, ref Vector3[] minimumBoxB)
        {
            SliceMode dimMode = s_DimSliceMode[dim];

            // Skip if a result already exists and this axis was not explicitly requested.
            if (minimumDimension >= 0 && m_SliceMode != SliceMode.Auto && m_SliceMode != dimMode)
            {
                return;
            }

            Vector3 boundingBoxA = m_BoundingBoxA;
            Vector3 boundingBoxB = m_BoundingBoxB;
            boundingBoxA[dim] += m_ThicknessA[dim];
            boundingBoxB[dim] += m_ThicknessB[dim];

            float minValue = boundingBoxA[dim];
            float stepValue = (boundingBoxB[dim] - boundingBoxA[dim]) / m_SliceCount;
            float tempVolume = 0.0f;

            var tempBoxA = new List<Vector3>(m_SliceCount);
            var tempBoxB = new List<Vector3>(m_SliceCount);

            for (int n = 0; n < m_SliceCount; ++n)
            {
                float maxValue = minValue + stepValue;
                Vector3 boxA = Vector3.zero;
                Vector3 boxB = Vector3.zero;

                if (TryGetSliceBoundingBoxAabb(dim, ref boxA, ref boxB, minValue, maxValue))
                {
                    boxA[dim] = minValue;
                    boxB[dim] = maxValue;
                    tempBoxA.Add(boxA);
                    tempBoxB.Add(boxB);
                    tempVolume += GetBoxVolume(boxA, boxB);
                }

                minValue = maxValue;
            }

            if (tempVolume <= Mathf.Epsilon) return;

            bool isForced = m_SliceMode == dimMode;
            bool isBetterOrFirst = minimumDimension < 0 || minimumVolume > tempVolume;

            if (isForced || isBetterOrFirst)
            {
                minimumDimension = dim;
                minimumVolume = tempVolume;
                minimumBoxA = tempBoxA.ToArray();
                minimumBoxB = tempBoxB.ToArray();
            }
        }

        private void MakeSlicedListFromAabb(Vector3 boxA, Vector3 boxB)
        {
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(boxA.x, boxA.y, boxA.z),
                new Vector3(boxA.x, boxB.y, boxA.z),
                new Vector3(boxB.x, boxB.y, boxA.z),
                new Vector3(boxB.x, boxA.y, boxA.z),
                new Vector3(boxA.x, boxA.y, boxB.z),
                new Vector3(boxA.x, boxB.y, boxB.z),
                new Vector3(boxB.x, boxB.y, boxB.z),
                new Vector3(boxB.x, boxA.y, boxB.z),
            };

            int[] indices = new int[]
            {
                0, 1, 2, 3,
                3, 2, 6, 7,
                4, 5, 1, 0,
                1, 5, 6, 2,
                4, 0, 3, 7,
                7, 6, 5, 4,
            };

            m_SlicedVertexList = vertices;
            m_SlicedIndexList = indices;
        }

        private static void AddSurface(List<int> indexList, int[] indices, int ptr, int offset)
        {
            for (int i = 0; i < 4; ++i)
            {
                indexList.Add(indices[i + ptr] + offset);
            }
        }

        private void MakeSlicedListFromBoundingBox()
        {
            if (m_SlicedBoundingBoxA == null)
            {
                Debug.LogError("Sliced bounding boxes are not initialized.");
                return;
            }

            List<Vector3> slicedVertexList = new List<Vector3>(8 * m_SlicedBoundingBoxA.Length);
            List<int> slicedIndexList = new List<int>(24 * m_SlicedBoundingBoxA.Length);

            for (int n = 0; n < m_SlicedBoundingBoxA.Length; ++n)
            {
                Vector3 boxA = m_SlicedBoundingBoxA[n];
                Vector3 boxB = m_SlicedBoundingBoxB[n];
                Vector3 boxC = m_SlicedBoundingBoxC[n];
                Vector3 boxD = m_SlicedBoundingBoxD[n];

                switch (m_SlicedDimension)
                {
                    case 0:
                        slicedVertexList.Add(new Vector3(boxA.x, boxA.y, boxA.z));
                        slicedVertexList.Add(new Vector3(boxA.x, boxB.y, boxA.z));
                        slicedVertexList.Add(new Vector3(boxD.x, boxD.y, boxC.z));
                        slicedVertexList.Add(new Vector3(boxC.x, boxC.y, boxC.z));
                        slicedVertexList.Add(new Vector3(boxA.x, boxA.y, boxB.z));
                        slicedVertexList.Add(new Vector3(boxA.x, boxB.y, boxB.z));
                        slicedVertexList.Add(new Vector3(boxD.x, boxD.y, boxD.z));
                        slicedVertexList.Add(new Vector3(boxC.x, boxC.y, boxD.z));
                        break;
                    case 1:
                        slicedVertexList.Add(new Vector3(boxA.x, boxA.y, boxA.z));
                        slicedVertexList.Add(new Vector3(boxC.x, boxC.y, boxC.z));
                        slicedVertexList.Add(new Vector3(boxD.x, boxC.y, boxC.z));
                        slicedVertexList.Add(new Vector3(boxB.x, boxB.y, boxA.z));
                        slicedVertexList.Add(new Vector3(boxA.x, boxA.y, boxB.z));
                        slicedVertexList.Add(new Vector3(boxC.x, boxC.y, boxD.z));
                        slicedVertexList.Add(new Vector3(boxD.x, boxC.y, boxD.z));
                        slicedVertexList.Add(new Vector3(boxB.x, boxB.y, boxB.z));
                        break;
                    case 2:
                        slicedVertexList.Add(new Vector3(boxA.x, boxA.y, boxA.z));
                        slicedVertexList.Add(new Vector3(boxA.x, boxB.y, boxA.z));
                        slicedVertexList.Add(new Vector3(boxB.x, boxB.y, boxA.z));
                        slicedVertexList.Add(new Vector3(boxB.x, boxA.y, boxA.z));
                        slicedVertexList.Add(new Vector3(boxC.x, boxC.y, boxD.z));
                        slicedVertexList.Add(new Vector3(boxC.x, boxD.y, boxD.z));
                        slicedVertexList.Add(new Vector3(boxD.x, boxD.y, boxD.z));
                        slicedVertexList.Add(new Vector3(boxD.x, boxC.y, boxD.z));
                        break;
                }
            }

            int[] indices = new int[]
            {
                0, 1, 2, 3,
                3, 2, 6, 7,
                4, 5, 1, 0,
                1, 5, 6, 2,
                4, 0, 3, 7,
                7, 6, 5, 4,
            };

            switch (m_SlicedDimension)
            {
                case 0:
                    for (int n = 0, offset = 0; n < m_SlicedBoundingBoxA.Length; ++n, offset += 8)
                    {
                        AddSurface(slicedIndexList, indices, 0, offset);
                        AddSurface(slicedIndexList, indices, 12, offset);
                        AddSurface(slicedIndexList, indices, 16, offset);
                        AddSurface(slicedIndexList, indices, 20, offset);
                        if (n == 0)
                        {
                            AddSurface(slicedIndexList, indices, 8, offset);
                        }
                        if (n + 1 == m_SlicedBoundingBoxA.Length)
                        {
                            AddSurface(slicedIndexList, indices, 4, offset);
                        }
                    }
                    break;
                case 1:
                    for (int n = 0, offset = 0; n < m_SlicedBoundingBoxA.Length; ++n, offset += 8)
                    {
                        AddSurface(slicedIndexList, indices, 0, offset);
                        AddSurface(slicedIndexList, indices, 4, offset);
                        AddSurface(slicedIndexList, indices, 8, offset);
                        AddSurface(slicedIndexList, indices, 20, offset);
                        if (n == 0)
                        {
                            AddSurface(slicedIndexList, indices, 16, offset);
                        }
                        if (n + 1 == m_SlicedBoundingBoxA.Length)
                        {
                            AddSurface(slicedIndexList, indices, 12, offset);
                        }
                    }
                    break;
                case 2:
                    for (int n = 0, offset = 0; n < m_SlicedBoundingBoxA.Length; ++n, offset += 8)
                    {
                        AddSurface(slicedIndexList, indices, 4, offset);
                        AddSurface(slicedIndexList, indices, 8, offset);
                        AddSurface(slicedIndexList, indices, 12, offset);
                        AddSurface(slicedIndexList, indices, 16, offset);
                        if (n == 0)
                        {
                            AddSurface(slicedIndexList, indices, 0, offset);
                        }
                        if (n + 1 == m_SlicedBoundingBoxA.Length)
                        {
                            AddSurface(slicedIndexList, indices, 20, offset);
                        }
                    }
                    break;
            }

            m_SlicedVertexList = slicedVertexList.ToArray();
            m_SlicedIndexList = slicedIndexList.ToArray();
        }

        private void MakeReducedListFromSlicedList()
        {
            if (m_SlicedVertexList == null || m_SlicedIndexList == null)
            {
                Debug.LogError("Sliced mesh buffers are not initialized.");
                return;
            }

            m_ReducedVertexList = m_SlicedVertexList.Clone() as Vector3[];
            m_ReducedIndexList = new int[m_SlicedIndexList.Length / 4 * 6];

            for (int i = 0, r = 0; i < m_SlicedIndexList.Length / 4 * 4; i += 4, r += 6)
            {
                int i0 = m_SlicedIndexList[i + 0];
                int i1 = m_SlicedIndexList[i + 1];
                int i2 = m_SlicedIndexList[i + 2];
                int i3 = m_SlicedIndexList[i + 3];
                m_ReducedIndexList[r + 0] = i0;
                m_ReducedIndexList[r + 1] = i1;
                m_ReducedIndexList[r + 2] = i2;
                m_ReducedIndexList[r + 3] = i2;
                m_ReducedIndexList[r + 4] = i3;
                m_ReducedIndexList[r + 5] = i0;
            }
        }

        private void TransformVertexList(ref Matrix4x4 transform)
        {
            if (m_VertexList == null || m_UsedVertexList == null)
            {
                Debug.LogError("Input vertex buffers are not initialized.");
                return;
            }

            Vector3[] vertexList = new Vector3[m_VertexList.Length];
            for (int i = 0; i < m_VertexList.Length; ++i)
            {
                if (m_UsedVertexList[i])
                {
                    vertexList[i] = transform.MultiplyPoint3x4(m_VertexList[i]);
                }
            }

            m_VertexList = vertexList;
        }

        private void TransformReducedList(ref Matrix4x4 transform)
        {
            if (m_ReducedVertexList == null)
            {
                Debug.LogError("Reduced vertex buffer is not initialized.");
                return;
            }

            for (int i = 0; i < m_ReducedVertexList.Length; ++i)
            {
                m_ReducedVertexList[i] = transform.MultiplyPoint3x4(m_ReducedVertexList[i]);
            }
        }
    }
}
