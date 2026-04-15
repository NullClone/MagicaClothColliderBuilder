using System;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    [Serializable]
    public class SplitProperty
    {
        public BoneWeightType BoneWeightType = BoneWeightType.Bone2;
        public int BoneWeight2 = 50;
        public int BoneWeight3 = 33;
        public int BoneWeight4 = 25;
        public bool GreaterBoneWeight = true;
        public BoneTriangleExtent BoneTriangleExtent = BoneTriangleExtent.Vertex2;

        public SplitProperty ShallowCopy()
        {
            return (SplitProperty)MemberwiseClone();
        }
    }

    [Serializable]
    public class ReducerProperty
    {
        public FitType FitType = FitType.Outer;
        public Vector3 Scale = Vector3.one;
        public Vector3 MinThickness = new Vector3(0.01f, 0.01f, 0.01f);
        public Bool3 OptimizeRotation = new Bool3(true, true, true);

        // Obsolete properties are left for potential future use but are not exposed by default.
        [HideInInspector] public ElementType ScaleElementType = ElementType.X;
        [HideInInspector] public ElementType MinThicknessElementType = ElementType.X;
        [HideInInspector] public ElementType OptimizeRotationElementType = ElementType.X;
        [HideInInspector] public Vector3 Offset = Vector3.zero;
        [HideInInspector] public Vector3 ThicknessA = Vector3.zero;
        [HideInInspector] public Vector3 ThicknessB = Vector3.zero;

        public ReducerProperty ShallowCopy()
        {
            return (ReducerProperty)MemberwiseClone();
        }
    }

    /// <summary>
    /// Holds the combined properties for a single generation job.
    /// </summary>
    public class SABoneColliderProperty
    {
        public SplitProperty SplitProperty = new();
        public ReducerProperty ReducerProperty = new();
    }

    public enum FitType
    {
        Outer,
        Inner,
    }

    public enum ElementType
    {
        X,
        XYZ,
    }
}