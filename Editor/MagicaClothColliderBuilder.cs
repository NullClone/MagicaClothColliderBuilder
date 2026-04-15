using MagicaCloth2;
using UnityEditor;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public static class MagicaClothColliderBuilder
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

        public static ColliderFitMode FitMode = ColliderFitMode.Normal;

        [MenuItem("GameObject/Magica Cloth/Generate Colliders")]
        public static void Generate()
        {
            var prop = new SABoneColliderProperty();

            // Apply presets based on the fit mode
            switch (FitMode)
            {
                case ColliderFitMode.Loose:
                    prop.SplitProperty.BoneTriangleExtent = BoneTriangleExtent.Vertex2;
                    prop.ReducerProperty.Scale = new Vector3(1.2f, 1.2f, 1.2f);
                    prop.ReducerProperty.FitType = FitType.Outer;
                    break;
                case ColliderFitMode.Tight:
                    prop.SplitProperty.BoneTriangleExtent = BoneTriangleExtent.Vertex1;
                    prop.ReducerProperty.Scale = new Vector3(0.9f, 0.9f, 0.9f);
                    prop.ReducerProperty.FitType = FitType.Inner;
                    break;
                case ColliderFitMode.Normal:
                default:
                    prop.SplitProperty.BoneTriangleExtent = BoneTriangleExtent.Vertex2;
                    prop.ReducerProperty.Scale = Vector3.one;
                    prop.ReducerProperty.FitType = FitType.Outer;
                    break;
            }

            var generator = new ColliderGenerator(Selection.activeGameObject, prop);
            generator.Process();
        }

        [MenuItem("GameObject/Magica Cloth/Cleanup Colliders")]
        public static void Cleanup()
        {
            var colliders = Selection.activeGameObject.GetComponentsInChildren<MagicaCapsuleCollider>(true);
            foreach (var collider in colliders)
            {
                // In editor, use DestroyImmediate
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
    }

    public class ReducerResult
    {
        public Quaternion Rotation;
        public Vector3 Center;
        public Vector3 BoxA;
        public Vector3 BoxB;
    }
}