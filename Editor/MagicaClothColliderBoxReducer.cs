using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public partial class MagicaClothColliderBoxReducer
    {
        private ReduceMode m_ReduceMode = ReduceMode.Mesh;
        private SliceMode m_SliceMode = SliceMode.Auto;
        private Vector3 m_MinThickness = Vector3.zero;
        private Vector3[] m_VertexList;
        private bool[] m_UsedVertexList;
        private int[] m_LineList;
        private Vector3 m_Center = Vector3.zero;
        private Quaternion m_Rotation = Quaternion.identity;
        private bool m_RotationEnabled;
        public bool m_OptimizeRotationX = true;
        public bool m_OptimizeRotationY = true;
        public bool m_OptimizeRotationZ = true;
        private Vector3 m_Scale = Vector3.one;
        private Vector3 m_Offset = Vector3.zero;
        private Vector3 m_ThicknessA = Vector3.zero;
        private Vector3 m_ThicknessB = Vector3.zero;
        private Vector3 m_BoundingBoxA = Vector3.zero;
        private Vector3 m_BoundingBoxB = Vector3.zero;
        private int m_SlicedDimension;
        private Vector3[] m_SlicedBoundingBoxA;
        private Vector3[] m_SlicedBoundingBoxB;
        private Vector3[] m_SlicedBoundingBoxC;
        private Vector3[] m_SlicedBoundingBoxD;
        private Vector3[] m_SlicedVertexList;
        private int[] m_SlicedIndexList;
        private Vector3[] m_ReducedVertexList;
        private int[] m_ReducedIndexList;
        private Quaternion m_ReducedRotation = Quaternion.identity;
        private Vector3 m_ReducedCenter = Vector3.zero;
        private Vector3 m_ReducedBoxA = Vector3.zero;
        private Vector3 m_ReducedBoxB = Vector3.zero;
        private bool m_PostfixTransform = true;
        private readonly bool m_CenterEnabled;
        private readonly int m_SliceCount = 31;

        public ReduceMode ReduceMode { set { m_ReduceMode = value; } }

        public Vector3 MinThickness { set { m_MinThickness = value; } }

        public Quaternion Rotation { set { m_RotationEnabled = true; m_Rotation = value; } }

        public bool OptimizeRotationX { set { m_OptimizeRotationX = value; } }

        public bool OptimizeRotationY { set { m_OptimizeRotationY = value; } }

        public bool OptimizeRotationZ { set { m_OptimizeRotationZ = value; } }

        public Vector3 Scale { set { m_Scale = value; } }

        public Vector3 Offset { set { m_Offset = value; } }

        public Vector3 ThicknessA { set { m_ThicknessA = value; } }

        public Vector3 ThicknessB { set { m_ThicknessB = value; } }

        public Vector3[] VertexList { set { m_VertexList = value; } }

        public int[] LineList { set { m_LineList = value; } }

        public bool PostfixTransform { set { m_PostfixTransform = value; } }

        public Quaternion ReducedRotation { get { return m_ReducedRotation; } }

        public Vector3 ReducedCenter { get { return m_ReducedCenter; } }

        public Vector3 ReducedBoxA { get { return m_ReducedBoxA; } }

        public Vector3 ReducedBoxB { get { return m_ReducedBoxB; } }

        public void Reduce()
        {
            BuildUsedVertexList();

            Vector3 minCenter = Vector3.zero;
            Vector3 minBoxA = Vector3.zero;
            Vector3 minBoxB = Vector3.zero;
            Vector3 minEuler = Vector3.zero;

            GetMinBoundingBoxAabb(ref minCenter, ref minBoxA, ref minBoxB, ref minEuler);

            var reducedTransform = Matrix4x4.identity;
            m_ReducedCenter = minCenter;
            m_ReducedBoxA = minBoxA;
            m_ReducedBoxB = minBoxB;

            Quaternion reduceRotation;

            if (m_RotationEnabled)
            {
                reduceRotation = InversedRotation(m_Rotation);
                m_ReducedRotation = m_Rotation;
            }
            else
            {
                reduceRotation = Quaternion.Euler(minEuler);
                m_ReducedRotation = InversedRotation(reduceRotation);
            }

            if (m_ReduceMode == ReduceMode.Mesh || m_ReduceMode == ReduceMode.BoxMesh)
            {
                Matrix4x4 reduceTransform = TranslateRotationMatrix(-m_ReducedCenter, reduceRotation);
                TransformVertexList(ref reduceTransform);
                reducedTransform = reduceTransform.inverse;
            }

            if (m_ReduceMode == ReduceMode.Mesh)
            {
                m_BoundingBoxA = minBoxA;
                m_BoundingBoxB = minBoxB;

                if (TryMakeSlicedBoundingBoxAabb())
                {
                    MakeSlicedListFromBoundingBox();
                }
                else
                {
                    m_ReduceMode = ReduceMode.BoxMesh;
                }
            }

            if (m_ReduceMode == ReduceMode.Box || m_ReduceMode == ReduceMode.BoxMesh)
            {
                ComputeMinThickness(ref minBoxA.x, ref minBoxB.x, m_MinThickness.x);
                ComputeMinThickness(ref minBoxA.y, ref minBoxB.y, m_MinThickness.y);
                ComputeMinThickness(ref minBoxA.z, ref minBoxB.z, m_MinThickness.z);

                if (m_Scale != Vector3.one)
                {
                    minBoxA = ScaledVector(minBoxA, m_Scale);
                    minBoxB = ScaledVector(minBoxB, m_Scale);
                }

                if (m_ThicknessA != Vector3.zero || m_ThicknessB != Vector3.zero)
                {
                    minBoxA += m_ThicknessA;
                    minBoxB += m_ThicknessB;
                }

                if (m_Offset != Vector3.zero)
                {
                    minBoxA += m_Offset;
                    minBoxB += m_Offset;
                }

                m_ReducedBoxA = minBoxA;
                m_ReducedBoxB = minBoxB;
                m_BoundingBoxA = minBoxA;
                m_BoundingBoxB = minBoxB;

                if (m_ReduceMode == ReduceMode.BoxMesh)
                {
                    MakeSlicedListFromAabb(minBoxA, minBoxB);
                }
            }

            if (m_ReduceMode == ReduceMode.Mesh || m_ReduceMode == ReduceMode.BoxMesh)
            {
                MakeReducedListFromSlicedList();

                if (m_PostfixTransform)
                {
                    TransformReducedList(ref reducedTransform);
                }
            }
        }

        private void BuildUsedVertexList()
        {
            if (m_VertexList == null)
            {
                m_UsedVertexList = null;

                return;
            }

            m_UsedVertexList = new bool[m_VertexList.Length];

            if (m_LineList == null)
            {
                for (int i = 0; i < m_UsedVertexList.Length; ++i)
                {
                    m_UsedVertexList[i] = true;
                }

                return;
            }

            for (int i = 0; i < m_LineList.Length; ++i)
            {
                int vertexIndex = m_LineList[i];

                if (vertexIndex >= 0 && vertexIndex < m_UsedVertexList.Length)
                {
                    m_UsedVertexList[vertexIndex] = true;
                }
            }
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
