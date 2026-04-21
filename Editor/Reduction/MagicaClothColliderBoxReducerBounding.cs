using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public partial class MagicaClothColliderBoxReducer
    {
        private static void ComputeMinThickness(ref float boxA, ref float boxB, float minThickness)
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

        private Vector3 GetBoundingBoxCenterAabb()
        {
            if (m_VertexList == null || m_UsedVertexList == null) return Vector3.zero;

            Vector3 boxA = Vector3.zero;
            Vector3 boxB = Vector3.zero;
            bool hasAnyVertex = false;

            for (int i = 0; i < m_VertexList.Length; ++i)
            {
                if (!m_UsedVertexList[i]) continue;

                if (!hasAnyVertex)
                {
                    hasAnyVertex = true;
                    boxA = boxB = m_VertexList[i];
                }
                else
                {
                    boxA = Min(boxA, m_VertexList[i]);
                    boxB = Max(boxB, m_VertexList[i]);
                }
            }

            return (boxA + boxB) * 0.5f;
        }

        private static void GetBoundingBoxAabb(Vector3[] vertices, bool[] usedVertices, ref Vector3 boxA, ref Vector3 boxB, ref Vector3 minCenter, ref Matrix4x4 transform)
        {
            boxA = Vector3.zero;
            boxB = Vector3.zero;
            bool hasAnyVertex = false;

            for (int i = 0; i < vertices.Length; ++i)
            {
                if (!usedVertices[i]) continue;

                var transformed = transform.MultiplyPoint3x4(vertices[i] - minCenter);

                if (!hasAnyVertex)
                {
                    hasAnyVertex = true;
                    boxA = boxB = transformed;
                }
                else
                {
                    boxA = Min(boxA, transformed);
                    boxB = Max(boxB, transformed);
                }
            }
        }

        private void GetBoundingBoxAabb(ref Vector3 boxA, ref Vector3 boxB, ref Vector3 minCenter, ref Matrix4x4 transform)
        {
            boxA = Vector3.zero;
            boxB = Vector3.zero;

            if (m_VertexList != null && m_UsedVertexList != null)
            {
                GetBoundingBoxAabb(m_VertexList, m_UsedVertexList, ref boxA, ref boxB, ref minCenter, ref transform);
            }
        }

        private void GetMinBoundingBoxAabb(ref Vector3 minCenter, ref Vector3 minBoxA, ref Vector3 minBoxB, ref Vector3 minEuler)
        {
            minCenter = m_CenterEnabled ? m_Center : GetBoundingBoxCenterAabb();

            minBoxA = Vector3.zero;
            minBoxB = Vector3.zero;
            minEuler = Vector3.zero;

            var transform = Matrix4x4.identity;
            GetBoundingBoxAabb(ref minBoxA, ref minBoxB, ref minCenter, ref transform);
        }

        private bool TryGetSliceBoundingBoxAabb(int dimension, ref Vector3 boxA, ref Vector3 boxB, float minValue, float maxValue)
        {
            var boxCollector = new BoxCollector();

            if (m_LineList != null)
            {
                int count = m_LineList.Length / 2 * 2;

                for (int i = 0; i < count; i += 2)
                {
                    Vector3 vertex0 = m_VertexList[m_LineList[i + 0]];
                    Vector3 vertex1 = m_VertexList[m_LineList[i + 1]];

                    if (vertex0[dimension] > vertex1[dimension])
                    {
                        (vertex1, vertex0) = (vertex0, vertex1);
                    }

                    if (vertex0[dimension] >= maxValue || vertex1[dimension] <= minValue) continue;

                    if (vertex0[dimension] < minValue)
                    {
                        if (!IsFuzzyZero(vertex0[dimension] - vertex1[dimension]))
                        {
                            Vector3 modVertex0 = vertex0 + ((vertex1 - vertex0) * (minValue - vertex0[dimension]) / (vertex1[dimension] - vertex0[dimension]));
                            boxCollector.Collect(modVertex0);
                        }
                    }
                    else
                    {
                        boxCollector.Collect(vertex0);
                    }

                    if (vertex1[dimension] > maxValue)
                    {
                        if (!IsFuzzyZero(vertex0[dimension] - vertex1[dimension]))
                        {
                            Vector3 modVertex1 = vertex1 + ((vertex0 - vertex1) * (vertex1[dimension] - maxValue) / (vertex1[dimension] - vertex0[dimension]));
                            boxCollector.Collect(modVertex1);
                        }
                    }
                    else
                    {
                        boxCollector.Collect(vertex1);
                    }
                }
            }

            boxA = boxCollector.BoxA;
            boxB = boxCollector.BoxB;

            if (boxA == boxB) return false;

            boxA[dimension] = minValue;
            boxB[dimension] = maxValue;

            return true;
        }
    }
}
