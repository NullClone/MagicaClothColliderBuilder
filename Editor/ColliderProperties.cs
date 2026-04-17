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
    }

    [Serializable]
    public class ReducerProperty
    {
        public FitType FitType = FitType.Outer;
        public bool EnableRotationSearch = false;
        public Vector3 Scale = Vector3.one;
        public Vector3 MinThickness = new Vector3(0.01f, 0.01f, 0.01f);
        public Bool3 OptimizeRotation = new Bool3(true, true, true);

        [HideInInspector] public ElementType ScaleElementType = ElementType.X;
        [HideInInspector] public ElementType MinThicknessElementType = ElementType.X;
        [HideInInspector] public ElementType OptimizeRotationElementType = ElementType.X;
        [HideInInspector] public Vector3 Offset = Vector3.zero;
        [HideInInspector] public Vector3 ThicknessA = Vector3.zero;
        [HideInInspector] public Vector3 ThicknessB = Vector3.zero;
    }

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

    public enum BoneWeightType
    {
        Bone2,
        Bone4,
    }

    public enum BoneTriangleExtent
    {
        Disable,
        Vertex2,
        Vertex1,
    }
}