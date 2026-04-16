using MagicaCloth2;
using UnityEditor;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class MagicaClothColliderBuilderWindow : EditorWindow
    {
        public enum ColliderFitMode
        {
            /// <summary>
            /// Provides a good balance of performance and accuracy.
            /// </summary>
            Normal,
            /// <summary>
            /// Creates larger colliders, useful for ensuring coverage even if it's less precise.
            /// </summary>
            Loose,
            /// <summary>
            /// Creates smaller, more form-fitting colliders, which might be more accurate but could leave gaps.
            /// </summary>
            Tight,
        }

        private Vector2 m_ScrollPosition;
        private GameObject m_TargetAvatarRoot;
        private ColliderFitMode m_ColliderFitMode = ColliderFitMode.Normal;
        private bool m_ShowSplitSettings = false;
        private bool m_ShowReducerSettings = false;
        private bool m_ShowAdvancedReducerSettings;
        private BoneWeightType m_BoneWeightType = BoneWeightType.Bone2;
        private int m_BoneWeight2 = 50;
        private int m_BoneWeight3 = 33;
        private int m_BoneWeight4 = 25;
        private bool m_GreaterBoneWeight = true;
        private BoneTriangleExtent m_BoneTriangleExtent = BoneTriangleExtent.Vertex2;
        private FitType m_FitType = FitType.Outer;
        private Vector3 m_Scale = Vector3.one;
        private Vector3 m_MinThickness = new Vector3(0.01f, 0.01f, 0.01f);
        private bool m_OptimizeRotationX = true;
        private bool m_OptimizeRotationY = true;
        private bool m_OptimizeRotationZ = true;
        private Vector3 m_Offset = Vector3.zero;
        private Vector3 m_ThicknessA = Vector3.zero;
        private Vector3 m_ThicknessB = Vector3.zero;

        [MenuItem("Tools/Magica Cloth2/Collider Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<MagicaClothColliderBuilderWindow>();
            window.titleContent = new GUIContent("Collider Builder");
            window.minSize = new Vector2(400f, 670f);
            window.Show();
        }

        private void OnEnable()
        {
            ApplyPreset(m_ColliderFitMode);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MagicaCloth Collider Builder", EditorStyles.boldLabel);

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

            m_TargetAvatarRoot = (GameObject)EditorGUILayout.ObjectField("Target", m_TargetAvatarRoot, typeof(GameObject), true);

            if (m_TargetAvatarRoot == null)
            {
                EditorGUILayout.HelpBox("Avatar Root を指定してください。Humanoid の Animator が必要です。", MessageType.Warning);
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();

                m_ColliderFitMode = (ColliderFitMode)EditorGUILayout.EnumPopup("Preset", m_ColliderFitMode);

                if (EditorGUI.EndChangeCheck())
                {
                    ApplyPreset(m_ColliderFitMode);
                }
            }

            EditorGUILayout.Space();

            DrawSplitSettingsSection();

            EditorGUILayout.Space();

            DrawReducerSettingsSection();

            EditorGUILayout.Space();

            DrawActionSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSplitSettingsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                m_ShowSplitSettings = EditorGUILayout.Foldout(m_ShowSplitSettings, "Split Settings", true);

                if (!m_ShowSplitSettings) return;

                m_BoneWeightType = (BoneWeightType)EditorGUILayout.EnumPopup("Bone Weight Type", m_BoneWeightType);
                m_GreaterBoneWeight = EditorGUILayout.Toggle("Prefer Dominant Bone", m_GreaterBoneWeight);
                m_BoneTriangleExtent = (BoneTriangleExtent)EditorGUILayout.EnumPopup("Triangle Extent", m_BoneTriangleExtent);

                m_BoneWeight2 = EditorGUILayout.IntSlider("Bone Weight 2", m_BoneWeight2, 0, 100);

                if (m_BoneWeightType == BoneWeightType.Bone4)
                {
                    m_BoneWeight3 = EditorGUILayout.IntSlider("Bone Weight 3", m_BoneWeight3, 0, 100);
                    m_BoneWeight4 = EditorGUILayout.IntSlider("Bone Weight 4", m_BoneWeight4, 0, 100);
                }
            }
        }

        private void DrawReducerSettingsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                m_ShowReducerSettings = EditorGUILayout.Foldout(m_ShowReducerSettings, "Reducer Settings", true);

                if (!m_ShowReducerSettings) return;

                m_FitType = (FitType)EditorGUILayout.EnumPopup("Fit Type", m_FitType);
                m_Scale = EditorGUILayout.Vector3Field("Scale", m_Scale);
                m_MinThickness = EditorGUILayout.Vector3Field("Min Thickness", m_MinThickness);
                m_MinThickness.x = Mathf.Max(0.0f, m_MinThickness.x);
                m_MinThickness.y = Mathf.Max(0.0f, m_MinThickness.y);
                m_MinThickness.z = Mathf.Max(0.0f, m_MinThickness.z);

                EditorGUILayout.Space();

                m_ShowAdvancedReducerSettings = EditorGUILayout.Foldout(m_ShowAdvancedReducerSettings, "Advanced Reducer Settings", true);

                if (m_ShowAdvancedReducerSettings)
                {
                    EditorGUILayout.LabelField("Optimize Rotation");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        m_OptimizeRotationX = EditorGUILayout.ToggleLeft("X", m_OptimizeRotationX, GUILayout.Width(60.0f));
                        m_OptimizeRotationY = EditorGUILayout.ToggleLeft("Y", m_OptimizeRotationY, GUILayout.Width(60.0f));
                        m_OptimizeRotationZ = EditorGUILayout.ToggleLeft("Z", m_OptimizeRotationZ, GUILayout.Width(60.0f));
                    }

                    EditorGUILayout.Space();

                    m_Offset = EditorGUILayout.Vector3Field("Offset", m_Offset);
                    m_ThicknessA = EditorGUILayout.Vector3Field("Thickness A", m_ThicknessA);
                    m_ThicknessB = EditorGUILayout.Vector3Field("Thickness B", m_ThicknessB);
                }
            }
        }

        private void DrawActionSection()
        {
            GUI.enabled = m_TargetAvatarRoot != null;

            if (GUILayout.Button("Generate Colliders", GUILayout.Height(25.0f)))
            {
                var colliderProperty = BuildColliderProperty();

                if (m_TargetAvatarRoot == null)
                {
                    Debug.LogError("Target avatar root is null.");
                    return;
                }

                if (colliderProperty == null)
                {
                    Debug.LogError("Collider property is null.");
                    return;
                }

                var generator = new ColliderGenerator(m_TargetAvatarRoot, colliderProperty);
                generator.Process();

                var selectedObject = m_TargetAvatarRoot;
                Selection.activeObject = null;
                EditorApplication.delayCall += () =>
                {
                    if (selectedObject != null)
                    {
                        Selection.activeObject = selectedObject;
                    }
                };
            }

            if (GUILayout.Button("Cleanup Existing Colliders", GUILayout.Height(25.0f)))
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

            GUI.enabled = true;
        }

        private void ApplyPreset(ColliderFitMode fitMode)
        {
            switch (fitMode)
            {
                case ColliderFitMode.Loose:
                    m_BoneTriangleExtent = BoneTriangleExtent.Vertex2;
                    m_FitType = FitType.Outer;
                    m_Scale = new Vector3(1.2f, 1.2f, 1.2f);
                    break;
                case ColliderFitMode.Tight:
                    m_BoneTriangleExtent = BoneTriangleExtent.Vertex1;
                    m_FitType = FitType.Inner;
                    m_Scale = new Vector3(0.9f, 0.9f, 0.9f);
                    break;
                case ColliderFitMode.Normal:
                default:
                    m_BoneTriangleExtent = BoneTriangleExtent.Vertex2;
                    m_FitType = FitType.Outer;
                    m_Scale = Vector3.one;
                    break;
            }
        }

        private SABoneColliderProperty BuildColliderProperty()
        {
            var property = new SABoneColliderProperty();
            property.SplitProperty.BoneWeightType = m_BoneWeightType;
            property.SplitProperty.BoneWeight2 = m_BoneWeight2;
            property.SplitProperty.BoneWeight3 = m_BoneWeight3;
            property.SplitProperty.BoneWeight4 = m_BoneWeight4;
            property.SplitProperty.GreaterBoneWeight = m_GreaterBoneWeight;
            property.SplitProperty.BoneTriangleExtent = m_BoneTriangleExtent;

            property.ReducerProperty.FitType = m_FitType;
            property.ReducerProperty.Scale = m_Scale;
            property.ReducerProperty.MinThickness = m_MinThickness;
            property.ReducerProperty.OptimizeRotation = new Bool3(m_OptimizeRotationX, m_OptimizeRotationY, m_OptimizeRotationZ);
            property.ReducerProperty.Offset = m_Offset;
            property.ReducerProperty.ThicknessA = m_ThicknessA;
            property.ReducerProperty.ThicknessB = m_ThicknessB;

            return property;
        }
    }

    public class ReducerResult
    {
        public Quaternion Rotation;
        public Vector3 Center;
        public Vector3 BoxA;
        public Vector3 BoxB;
    }
}