using System;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    [Serializable]
    public class SplitProperty
    {
        public BoneWeightType boneWeightType = BoneWeightType.Bone2;
        public int boneWeight2 = 50;
        public int boneWeight3 = 33;
        public int boneWeight4 = 25;
        public bool greaterBoneWeight = true;
        public BoneTriangleExtent boneTriangleExtent = BoneTriangleExtent.Vertex2;

        public SplitProperty ShallowCopy()
        {
            return (SplitProperty)MemberwiseClone();
        }
    }

    [Serializable]
    public class ReducerProperty
    {
        public FitType fitType = FitType.Outer;
        public Vector3 scale = Vector3.one;
        public Vector3 minThickness = new Vector3(0.01f, 0.01f, 0.01f);
        public Bool3 optimizeRotation = new Bool3(true, true, true);

        // Obsolete properties are left for potential future use but are not exposed by default.
        [HideInInspector] public ElementType scaleElementType = ElementType.X;
        [HideInInspector] public ElementType minThicknessElementType = ElementType.X;
        [HideInInspector] public ElementType optimizeRotationElementType = ElementType.X;
        [HideInInspector] public Vector3 offset = Vector3.zero;
        [HideInInspector] public Vector3 thicknessA = Vector3.zero;
        [HideInInspector] public Vector3 thicknessB = Vector3.zero;

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
        public SplitProperty splitProperty = new();
        public ReducerProperty reducerProperty = new();
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