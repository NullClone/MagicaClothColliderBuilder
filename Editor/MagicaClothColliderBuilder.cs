using MagicaCloth2;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class MagicaClothColliderBuilderWindow : EditorWindow
    {
        private enum SettingsTab
        {
            Overview,
            Limbs,
            Body,
            Advanced,
        }


        // Fields

        private Vector2 m_ScrollPosition;
        private GameObject m_TargetAvatarRoot;
        private SettingsTab m_SelectedTab = SettingsTab.Overview;
        private SABoneColliderProperty m_Settings = new();
        private List<MagicaCapsuleCollider> m_GeneratedColliders = new();
        private bool m_UseCustomSkinnedMeshes;
        private List<SkinnedMeshRenderer> m_CustomSkinnedMeshes = new();


        // Methods

        [MenuItem("Tools/Magica Cloth2/Collider Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<MagicaClothColliderBuilderWindow>();
            window.titleContent = new GUIContent("MagicaCloth2 Collider Builder");
            window.minSize = new Vector2(540f, 760f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();

            m_SelectedTab = (SettingsTab)GUILayout.Toolbar((int)m_SelectedTab, new[] { "Overview", "Limbs", "Body", "Advanced" }, GUILayout.Height(28f));

            EditorGUILayout.Space();

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

            switch (m_SelectedTab)
            {
                case SettingsTab.Overview:
                    DrawOverviewTab();
                    break;
                case SettingsTab.Limbs:
                    DrawLimbTab();
                    break;
                case SettingsTab.Body:
                    DrawBodyTab();
                    break;
                case SettingsTab.Advanced:
                    DrawAdvancedTab();
                    break;
            }

            DrawActionSection();

            EditorGUILayout.Space();
            EditorGUILayout.EndScrollView();
        }

        private void DrawOverviewTab()
        {
            DrawCard("Target", () =>
            {
                m_TargetAvatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", m_TargetAvatarRoot, typeof(GameObject), true);

                if (m_TargetAvatarRoot == null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("Please specify an Avatar Root.", MessageType.Warning);
                    return;
                }
            });

            DrawCard("Source", () =>
            {
                EditorGUILayout.LabelField("Source Mode", m_UseCustomSkinnedMeshes ? "Custom" : "Auto (Children)");

                if (!m_UseCustomSkinnedMeshes)
                {
                    if (GUILayout.Button("Use Custom Selection"))
                    {
                        m_UseCustomSkinnedMeshes = true;

                        m_CustomSkinnedMeshes ??= new List<SkinnedMeshRenderer> { null };
                    }

                    return;
                }

                if (GUILayout.Button("Use Auto"))
                {
                    m_UseCustomSkinnedMeshes = false;

                    return;
                }

                EditorGUILayout.Space();

                for (int i = 0; i < m_CustomSkinnedMeshes.Count; ++i)
                {
                    EditorGUILayout.BeginHorizontal();

                    m_CustomSkinnedMeshes[i] = (SkinnedMeshRenderer)EditorGUILayout.ObjectField($"Renderer {i + 1}", m_CustomSkinnedMeshes[i], typeof(SkinnedMeshRenderer), true);

                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        m_CustomSkinnedMeshes.RemoveAt(i);

                        --i;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Add Slot"))
                {
                    m_CustomSkinnedMeshes.Add(null);
                }
            });

            DrawCard("Generation", () =>
            {
                GenerationProperty generation = m_Settings.GenerationProperty;
                generation.IncludeHips = EditorGUILayout.Toggle("Include Hips", generation.IncludeHips);
                generation.IncludeShoulders = EditorGUILayout.Toggle("Include Shoulders", generation.IncludeShoulders);
                generation.IncludeFingers = EditorGUILayout.Toggle("Include Fingers", generation.IncludeFingers);
                generation.IncludeToes = EditorGUILayout.Toggle("Include Toes", generation.IncludeToes);
                generation.IncludeUpperChest = EditorGUILayout.Toggle("Include UpperChest", generation.IncludeUpperChest);

                if (GUILayout.Button("Reset All Settings"))
                {
                    m_Settings = new SABoneColliderProperty();
                    m_UseCustomSkinnedMeshes = false;
                    m_CustomSkinnedMeshes.Clear();
                }
            });

            DrawCard("Fit Modes", () =>
            {
                GenerationProperty generation = m_Settings.GenerationProperty;
                generation.ArmFitMode = (FitMode)EditorGUILayout.EnumPopup("Arms", generation.ArmFitMode);
                generation.FingerFitMode = (FitMode)EditorGUILayout.EnumPopup("Fingers", generation.FingerFitMode);
                generation.LegFitMode = (FitMode)EditorGUILayout.EnumPopup("Legs", generation.LegFitMode);
                generation.ToeFitMode = (FitMode)EditorGUILayout.EnumPopup("Toes", generation.ToeFitMode);
                generation.BodyFitMode = (FitMode)EditorGUILayout.EnumPopup("Body", generation.BodyFitMode);
                generation.HeadFitMode = (FitMode)EditorGUILayout.EnumPopup("Head / Neck", generation.HeadFitMode);
            });
        }

        private void DrawSplitTab()
        {
            DrawCard("Weight Extraction", () =>
            {
                SplitProperty split = m_Settings.SplitProperty;
                split.BoneWeightType = (BoneWeightType)EditorGUILayout.EnumPopup("Bone Weight Type", split.BoneWeightType);
                split.GreaterBoneWeight = EditorGUILayout.Toggle("Prefer Dominant Bone", split.GreaterBoneWeight);
                split.BoneTriangleExtent = (BoneTriangleExtent)EditorGUILayout.EnumPopup("Triangle Extent", split.BoneTriangleExtent);
                split.BoneWeight2 = EditorGUILayout.IntSlider("Bone Weight 2", split.BoneWeight2, 0, 100);

                if (split.BoneWeightType == BoneWeightType.Bone4)
                {
                    split.BoneWeight3 = EditorGUILayout.IntSlider("Bone Weight 3", split.BoneWeight3, 0, 100);
                    split.BoneWeight4 = EditorGUILayout.IntSlider("Bone Weight 4", split.BoneWeight4, 0, 100);
                }
            });
        }

        private void DrawLimbTab()
        {
            DrawCard("Limb Fitting", () =>
            {
                LimbFitProperty limb = m_Settings.LimbFitProperty;
                limb.RadiusScale = EditorGUILayout.Slider("Radius Scale", limb.RadiusScale, 0.5f, 1.5f);
                limb.MinJointDistance = EditorGUILayout.Slider("Min Joint Distance", limb.MinJointDistance, 0.005f, 0.1f);
                limb.AnchorStartSphereCenterToBone = EditorGUILayout.Toggle("Anchor Start Sphere Center To Bone", limb.AnchorStartSphereCenterToBone);
            });

            DrawCard("Fit Mode Radius", () =>
            {
                LimbFitProperty limb = m_Settings.LimbFitProperty;
                limb.InnerRadiusPercentile = EditorGUILayout.Slider("Inner Radius", limb.InnerRadiusPercentile, 25f, 65f);
                limb.BalancedRadiusPercentile = EditorGUILayout.Slider("Balanced Radius", limb.BalancedRadiusPercentile, 45f, 80f);
                limb.OuterRadiusPercentile = EditorGUILayout.Slider("Outer Radius", limb.OuterRadiusPercentile, 60f, 95f);
            });
        }

        private void DrawBodyTab()
        {
            DrawCard("Body Orientation", () =>
            {
                BodyFitProperty body = m_Settings.BodyFitProperty;
                body.HorizontalAxis = (BodyHorizontalAxis)EditorGUILayout.EnumPopup("Horizontal Axis", body.HorizontalAxis);
                body.HipsMaxLength = EditorGUILayout.Slider("Hips Max Length", body.HipsMaxLength, body.MinLength, 0.5f);
                body.RadiusPercentile = EditorGUILayout.Slider("Radius Bias", body.RadiusPercentile, 40f, 95f);
                body.MinLength = EditorGUILayout.Slider("Min Length", body.MinLength, 0.01f, 0.2f);
                body.MinRadius = EditorGUILayout.Slider("Min Radius", body.MinRadius, 0.001f, 0.05f);
            });

            DrawCard("Head", () =>
            {
                HeadFitProperty head = m_Settings.HeadFitProperty;
                head.FitMethod = (HeadFitMethod)EditorGUILayout.EnumPopup("Fit Method", head.FitMethod);
                head.RadiusScale = EditorGUILayout.Slider("Radius Scale", head.RadiusScale, 0.5f, 1.5f);
                head.MinRadius = EditorGUILayout.Slider("Min Radius", head.MinRadius, 0.01f, 0.3f);
                head.MaxRadius = EditorGUILayout.Slider("Max Radius", head.MaxRadius, head.MinRadius, 0.5f);
                head.AnchorOuterStartToHeadTransform = EditorGUILayout.Toggle("Anchor Outer Start To Head Transform", head.AnchorOuterStartToHeadTransform);
            });
        }

        private void DrawGenericTab()
        {
            DrawCard("Generic Percentiles", () =>
            {
                GenericFitProperty generic = m_Settings.GenericFitProperty;
                generic.DefaultFitPercentile = EditorGUILayout.Slider("Default Radius Percentile", generic.DefaultFitPercentile, 40f, 95f);
                generic.HipsFitPercentile = EditorGUILayout.Slider("Hips Radius Percentile", generic.HipsFitPercentile, 40f, 95f);
                generic.SpineChestFitPercentile = EditorGUILayout.Slider("Spine/Chest Radius Percentile", generic.SpineChestFitPercentile, 40f, 95f);
                generic.UpperChestFitPercentile = EditorGUILayout.Slider("UpperChest Radius Percentile", generic.UpperChestFitPercentile, 40f, 95f);
            });

            DrawCard("Generic Bounds", () =>
            {
                GenericFitProperty generic = m_Settings.GenericFitProperty;
                generic.DefaultLowerPercentile = EditorGUILayout.Slider("Default Lower Percentile", generic.DefaultLowerPercentile, 0f, 40f);
                generic.DefaultUpperPercentile = EditorGUILayout.Slider("Default Upper Percentile", generic.DefaultUpperPercentile, 60f, 100f);
                generic.HipsLowerPercentile = EditorGUILayout.Slider("Hips Lower Percentile", generic.HipsLowerPercentile, 0f, 60f);
                generic.HipsUpperPercentile = EditorGUILayout.Slider("Hips Upper Percentile", generic.HipsUpperPercentile, 60f, 100f);
                generic.SpineChestLowerPercentile = EditorGUILayout.Slider("Spine/Chest Lower Percentile", generic.SpineChestLowerPercentile, 0f, 40f);
                generic.SpineChestUpperPercentile = EditorGUILayout.Slider("Spine/Chest Upper Percentile", generic.SpineChestUpperPercentile, 60f, 100f);
                generic.UpperChestLowerPercentile = EditorGUILayout.Slider("UpperChest Lower Percentile", generic.UpperChestLowerPercentile, 0f, 40f);
                generic.UpperChestUpperPercentile = EditorGUILayout.Slider("UpperChest Upper Percentile", generic.UpperChestUpperPercentile, 60f, 100f);
            });

            DrawCard("Generic Role Bias", () =>
            {
                GenericFitProperty generic = m_Settings.GenericFitProperty;
                generic.HipsCenterYRatio = EditorGUILayout.Slider("Hips Center Y Ratio", generic.HipsCenterYRatio, 0f, 1f);
                generic.SpineChestCenterYRatio = EditorGUILayout.Slider("Spine/Chest Center Y Ratio", generic.SpineChestCenterYRatio, 0f, 1f);
                generic.UpperChestCenterYRatio = EditorGUILayout.Slider("UpperChest Center Y Ratio", generic.UpperChestCenterYRatio, 0f, 1f);
                generic.HipsRadiusScale = EditorGUILayout.Slider("Hips Radius Scale", generic.HipsRadiusScale, 0.5f, 1.5f);
                generic.SpineChestRadiusScale = EditorGUILayout.Slider("Spine/Chest Radius Scale", generic.SpineChestRadiusScale, 0.5f, 1.5f);
                generic.UpperChestRadiusScale = EditorGUILayout.Slider("UpperChest Radius Scale", generic.UpperChestRadiusScale, 0.5f, 1.5f);
            });

            DrawCard("Generic Limits", () =>
            {
                GenericFitProperty generic = m_Settings.GenericFitProperty;
                generic.MinLength = EditorGUILayout.Slider("Min Length", generic.MinLength, 0.005f, 0.1f);
                generic.MinRadius = EditorGUILayout.Slider("Min Radius", generic.MinRadius, 0.001f, 0.05f);
                generic.UpperChestMinRadius = EditorGUILayout.Slider("UpperChest Min Radius", generic.UpperChestMinRadius, 0.001f, 0.05f);
                generic.UpperChestMinRadiusByLengthRatio = EditorGUILayout.Slider("UpperChest Min Radius / Length", generic.UpperChestMinRadiusByLengthRatio, 0f, 0.5f);
                generic.UpperChestMinLength = EditorGUILayout.Slider("UpperChest Min Length", generic.UpperChestMinLength, 0.005f, 0.15f);
                generic.UpperChestMinLengthByBoneRatio = EditorGUILayout.Slider("UpperChest Min Length / Bone", generic.UpperChestMinLengthByBoneRatio, 0f, 2f);
                generic.HipsMaxLength = EditorGUILayout.Slider("Hips Max Length", generic.HipsMaxLength, 0.005f, 0.2f);
                generic.HipsMaxLengthByBoneRatio = EditorGUILayout.Slider("Hips Max Length / Bone", generic.HipsMaxLengthByBoneRatio, 0.1f, 3f);
                generic.MaxRadiusByBoneRatio = EditorGUILayout.Slider("Max Radius / Bone", generic.MaxRadiusByBoneRatio, 0.1f, 1.5f);
                generic.MaxRadiusByLengthRatio = EditorGUILayout.Slider("Max Radius / Length", generic.MaxRadiusByLengthRatio, 0.1f, 1.5f);
            });
        }

        private void DrawAdvancedTab()
        {
            DrawSplitTab();

            DrawCard("Limb Advanced", () =>
            {
                LimbFitProperty limb = m_Settings.LimbFitProperty;
                limb.ForceFixedAxisByHumanoid = EditorGUILayout.Toggle("Use Humanoid Axes", limb.ForceFixedAxisByHumanoid);
                limb.LeakMarginScale = EditorGUILayout.Slider("Leak Margin Scale", limb.LeakMarginScale, 0.01f, 0.2f);
                limb.LeakMarginMin = EditorGUILayout.Slider("Leak Margin Min", limb.LeakMarginMin, 0.001f, 0.02f);
                limb.LeakMarginMax = EditorGUILayout.Slider("Leak Margin Max", limb.LeakMarginMax, limb.LeakMarginMin, 0.03f);
            });

            DrawCard("Body Advanced", () =>
            {
                BodyFitProperty body = m_Settings.BodyFitProperty;
                GenerationProperty generation = m_Settings.GenerationProperty;
                generation.DefaultFitMode = (FitMode)EditorGUILayout.EnumPopup("Fallback Fit Mode", generation.DefaultFitMode);
                EditorGUILayout.Space();
                body.ProjectAxisToBodyUpPlane = EditorGUILayout.Toggle("Project Axis To Body-Up Plane", body.ProjectAxisToBodyUpPlane);
                body.HipsProjectAxisToSpinePlane = EditorGUILayout.Toggle("Hips Project Axis To Spine Plane", body.HipsProjectAxisToSpinePlane);
                body.HipsMaxLengthBySpineDistance = EditorGUILayout.Slider("Hips Max Length / Spine Distance", body.HipsMaxLengthBySpineDistance, 0.5f, 4.0f);
                body.MaxRadiusByLengthRatio = EditorGUILayout.Slider("Max Radius / Length", body.MaxRadiusByLengthRatio, 0.2f, 1.0f);
            });

            DrawCard("Body Role Tuning", () =>
            {
                BodyFitProperty body = m_Settings.BodyFitProperty;
                body.HipsLengthPercentile = EditorGUILayout.Slider("Hips Length", body.HipsLengthPercentile, 50f, 99f);
                body.SpineLengthPercentile = EditorGUILayout.Slider("Spine Length", body.SpineLengthPercentile, 50f, 99f);
                body.ChestLengthPercentile = EditorGUILayout.Slider("Chest Length", body.ChestLengthPercentile, 50f, 99f);
                body.UpperChestLengthPercentile = EditorGUILayout.Slider("Upper Chest Length", body.UpperChestLengthPercentile, 50f, 99f);
                body.HipsRadiusScale = EditorGUILayout.Slider("Hips Radius", body.HipsRadiusScale, 0.5f, 1.5f);
                body.SpineRadiusScale = EditorGUILayout.Slider("Spine Radius", body.SpineRadiusScale, 0.5f, 1.5f);
                body.ChestRadiusScale = EditorGUILayout.Slider("Chest Radius", body.ChestRadiusScale, 0.5f, 1.5f);
                body.UpperChestRadiusScale = EditorGUILayout.Slider("Upper Chest Radius", body.UpperChestRadiusScale, 0.5f, 1.5f);
            });

            DrawCard("Head Advanced", () =>
            {
                HeadFitProperty head = m_Settings.HeadFitProperty;
                head.RadiusPercentile = EditorGUILayout.Slider("Radius Percentile", head.RadiusPercentile, 40f, 95f);
                head.UseFaceForwardOffsetWhenNotAnchored = EditorGUILayout.Toggle("Use Face Offsets When Not Anchored", head.UseFaceForwardOffsetWhenNotAnchored);
                head.LengthRatio = EditorGUILayout.Slider("Length Ratio (Roundness)", head.LengthRatio, 0.0f, 0.8f);
                head.ForwardOffset = EditorGUILayout.Slider("Forward Offset", head.ForwardOffset, -0.05f, 0.08f);
                head.UpOffset = EditorGUILayout.Slider("Up Offset", head.UpOffset, -0.05f, 0.08f);
            });

            DrawGenericTab();
        }

        private void DrawActionSection()
        {
            GUI.enabled = m_TargetAvatarRoot != null;

            if (GUILayout.Button("Generate Colliders", GUILayout.Height(30f)))
            {
                if (m_TargetAvatarRoot.GetComponentsInChildren<MagicaCapsuleCollider>(true).Length != 0)
                {
                    if (EditorUtility.DisplayDialog(
                        "Existing Colliders Found",
                        "A collider has already been generated. Do you want to clean it up and then regenerate it?",
                        "Cleanup And Generate",
                        "Cancel"))
                    {
                        CleanupExistingColliders();
                    }
                    else return;
                }

                var generator = new ColliderGenerator(
                    m_TargetAvatarRoot,
                    m_Settings,
                    m_UseCustomSkinnedMeshes ? m_CustomSkinnedMeshes : null,
                    (progress, message) => EditorUtility.DisplayProgressBar(
                        "MagicaCloth2 Collider Builder",
                        message,
                        progress));

                try
                {
                    m_GeneratedColliders = generator.Process();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            if (m_GeneratedColliders != null && m_GeneratedColliders.Count > 0)
            {
                if (GUILayout.Button("Select Colliders", GUILayout.Height(26f)))
                {
                    Selection.objects = m_GeneratedColliders.Select(c => c.gameObject).ToArray();
                }
            }

            if (GUILayout.Button("Cleanup Existing Colliders", GUILayout.Height(26f)))
            {
                CleanupExistingColliders();

                m_GeneratedColliders.Clear();
            }

            GUI.enabled = true;
        }

        private void CleanupExistingColliders()
        {
            if (m_TargetAvatarRoot == null)
            {
                Debug.LogWarning("No target object selected for cleanup.");

                return;
            }

            var colliders = m_TargetAvatarRoot.GetComponentsInChildren<MagicaCapsuleCollider>(true);

            foreach (var collider in colliders)
            {
                if (Application.isEditor && !Application.isPlaying)
                {
                    Object.DestroyImmediate(collider.gameObject);
                }
                else
                {
                    Object.Destroy(collider.gameObject);
                }
            }
        }

        private static void DrawCard(string title, System.Action drawContent)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.Space();

                drawContent?.Invoke();
            }

            EditorGUILayout.Space();
        }
    }
}
