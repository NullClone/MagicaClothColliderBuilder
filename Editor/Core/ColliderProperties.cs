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
    public class GenerationProperty
    {
        public FitMode DefaultFitMode = FitMode.Balanced;
        public FitMode ArmFitMode = FitMode.Inner;
        public FitMode LegFitMode = FitMode.Balanced;
        public FitMode FingerFitMode = FitMode.Inner;
        public FitMode ToeFitMode = FitMode.Balanced;
        public FitMode BodyFitMode = FitMode.Balanced;
        public FitMode HeadFitMode = FitMode.Balanced;
        public bool IncludeHips = false;
        public bool IncludeFingers = true;
        public bool IncludeShoulders = false;
        public bool IncludeToes = true;
        public bool IncludeUpperChest = true;
    }

    [Serializable]
    public class LimbFitProperty
    {
        public bool ForceFixedAxisByHumanoid = true;
        public bool AnchorStartSphereCenterToBone = true;
        public float InnerRadiusPercentile = 44.0f;
        public float BalancedRadiusPercentile = 62.0f;
        public float OuterRadiusPercentile = 78.0f;
        public float RadiusScale = 1.0f;
        public float MinJointDistance = 0.02f;
        public float LeakMarginScale = 0.08f;
        public float LeakMarginMin = 0.004f;
        public float LeakMarginMax = 0.012f;

        public float GetRadiusPercentile(FitMode fitMode) => fitMode switch
        {
            FitMode.Inner => InnerRadiusPercentile,
            FitMode.Outer => OuterRadiusPercentile,
            _ => BalancedRadiusPercentile,
        };

        public float GetRadiusCapScale(FitMode fitMode) => fitMode switch
        {
            FitMode.Inner => 0.72f,
            FitMode.Outer => 1.15f,
            _ => 1.0f,
        };
    }

    [Serializable]
    public class BodyFitProperty
    {
        public BodyHorizontalAxis HorizontalAxis = BodyHorizontalAxis.RootRight;
        public bool ProjectAxisToBodyUpPlane = true;
        public bool HipsProjectAxisToSpinePlane = true;
        public float HipsMaxLength = 0.22f;
        public float HipsMaxLengthBySpineDistance = 1.8f;
        public float BendSafeLengthScale = 1.35f;
        public float BendSafeJointMargin = 0.012f;
        public float BendSafeCenterLimit = 0.018f;
        public float BendSafeRadiusScale = 0.86f;
        public float RadiusPercentile = 68.0f;
        public float MinLength = 0.03f;
        public float MinRadius = 0.008f;
        public float MaxRadiusByLengthRatio = 0.65f;
        public float HipsLengthPercentile = 90.0f;
        public float SpineLengthPercentile = 84.0f;
        public float ChestLengthPercentile = 86.0f;
        public float UpperChestLengthPercentile = 88.0f;
        public float HipsRadiusScale = 1.08f;
        public float SpineRadiusScale = 0.95f;
        public float ChestRadiusScale = 1.0f;
        public float UpperChestRadiusScale = 1.12f;

        public float GetLengthPercentile(BoneFitRole role) => role switch
        {
            BoneFitRole.Hips => HipsLengthPercentile,
            BoneFitRole.Spine => SpineLengthPercentile,
            BoneFitRole.Chest => ChestLengthPercentile,
            BoneFitRole.UpperChest => UpperChestLengthPercentile,
            _ => ChestLengthPercentile,
        };

        public float GetRadiusScale(BoneFitRole role) => role switch
        {
            BoneFitRole.Hips => HipsRadiusScale,
            BoneFitRole.Spine => SpineRadiusScale,
            BoneFitRole.Chest => ChestRadiusScale,
            BoneFitRole.UpperChest => UpperChestRadiusScale,
            _ => ChestRadiusScale,
        };

        public float GetRadiusPercentile(FitMode fitMode) => fitMode switch
        {
            FitMode.Inner => Mathf.Min(RadiusPercentile, 52.0f),
            FitMode.Outer => Mathf.Max(RadiusPercentile, 78.0f),
            _ => RadiusPercentile,
        };

        public float GetRadiusCapScale(FitMode fitMode) => fitMode switch
        {
            FitMode.Inner => 0.82f,
            FitMode.Outer => 1.10f,
            _ => 1.0f,
        };
    }

    [Serializable]
    public class HeadFitProperty
    {
        public bool AnchorOuterStartToHeadTransform = true;
        public bool UseFaceForwardOffsetWhenNotAnchored = true;
        public float RadiusScale = 1.05f;
        public float MinRadius = 0.06f;
        public float MaxRadius = 0.25f;
        public float LengthRatio = 0.12f;
        public float ForwardOffset = 0.012f;
        public float UpOffset = 0.0f;
    }

    [Serializable]
    public class GenericFitProperty
    {
        public float DefaultFitPercentile = 70.0f;
        public float HipsFitPercentile = 70.0f;
        public float SpineChestFitPercentile = 70.0f;
        public float UpperChestFitPercentile = 70.0f;
        public float DefaultLowerPercentile = 2.0f;
        public float DefaultUpperPercentile = 98.0f;
        public float HipsLowerPercentile = 35.0f;
        public float HipsUpperPercentile = 96.0f;
        public float SpineChestLowerPercentile = 8.0f;
        public float SpineChestUpperPercentile = 98.0f;
        public float UpperChestLowerPercentile = 15.0f;
        public float UpperChestUpperPercentile = 98.0f;
        public float HipsCenterYRatio = 0.68f;
        public float SpineChestCenterYRatio = 0.56f;
        public float UpperChestCenterYRatio = 0.58f;
        public float HipsRadiusScale = 0.82f;
        public float SpineChestRadiusScale = 0.90f;
        public float UpperChestRadiusScale = 1.15f;
        public float MinLength = 0.02f;
        public float MinRadius = 0.003f;
        public float UpperChestMinRadius = 0.012f;
        public float UpperChestMinRadiusByLengthRatio = 0.18f;
        public float UpperChestMinLength = 0.03f;
        public float UpperChestMinLengthByBoneRatio = 0.75f;
        public float HipsMaxLength = 0.045f;
        public float HipsMaxLengthByBoneRatio = 1.35f;
        public float MaxRadiusByBoneRatio = 0.70f;
        public float MaxRadiusByLengthRatio = 0.70f;

        public float GetFitPercentile(BoneFitRole role) => role switch
        {
            BoneFitRole.Hips => HipsFitPercentile,
            BoneFitRole.Spine or BoneFitRole.Chest or BoneFitRole.Neck => SpineChestFitPercentile,
            BoneFitRole.UpperChest => UpperChestFitPercentile,
            _ => DefaultFitPercentile,
        };

        public float GetFitPercentile(BoneFitRole role, FitMode fitMode)
        {
            float basePercentile = GetFitPercentile(role);

            return fitMode switch
            {
                FitMode.Inner => Mathf.Min(basePercentile, IsCoreRole(role) ? 52.0f : 46.0f),
                FitMode.Outer => Mathf.Max(basePercentile, 78.0f),
                _ => basePercentile,
            };
        }

        public float GetRadiusCapScale(FitMode fitMode) => fitMode switch
        {
            FitMode.Inner => 0.78f,
            FitMode.Outer => 1.12f,
            _ => 1.0f,
        };

        public float GetCenterYRatio(BoneFitRole role) => role switch
        {
            BoneFitRole.Hips => HipsCenterYRatio,
            BoneFitRole.Spine or BoneFitRole.Chest => SpineChestCenterYRatio,
            BoneFitRole.UpperChest => UpperChestCenterYRatio,
            _ => 0.5f,
        };

        public float GetRadiusScale(BoneFitRole role) => role switch
        {
            BoneFitRole.Hips => HipsRadiusScale,
            BoneFitRole.Spine or BoneFitRole.Chest => SpineChestRadiusScale,
            BoneFitRole.UpperChest => UpperChestRadiusScale,
            _ => 1.0f,
        };

        public (float lower, float upper) GetPercentileBounds(BoneFitRole role) => role switch
        {
            BoneFitRole.Hips => (HipsLowerPercentile, HipsUpperPercentile),
            BoneFitRole.Spine or BoneFitRole.Chest => (SpineChestLowerPercentile, SpineChestUpperPercentile),
            BoneFitRole.UpperChest => (UpperChestLowerPercentile, UpperChestUpperPercentile),
            _ => (DefaultLowerPercentile, DefaultUpperPercentile),
        };

        private static bool IsCoreRole(BoneFitRole role)
        {
            return role == BoneFitRole.Hips ||
                   role == BoneFitRole.Spine ||
                   role == BoneFitRole.Chest ||
                   role == BoneFitRole.UpperChest ||
                   role == BoneFitRole.Neck;
        }
    }

    public class SABoneColliderProperty
    {
        public GenerationProperty GenerationProperty = new();
        public SplitProperty SplitProperty = new();
        public LimbFitProperty LimbFitProperty = new();
        public BodyFitProperty BodyFitProperty = new();
        public HeadFitProperty HeadFitProperty = new();
        public GenericFitProperty GenericFitProperty = new();
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

    public enum BodyHorizontalAxis
    {
        RootRight,
        RootForward,
    }

    public enum FitMode
    {
        Inner,
        Balanced,
        Outer,
    }
}
