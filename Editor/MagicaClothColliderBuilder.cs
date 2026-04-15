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

        public static ColliderFitMode fitMode = ColliderFitMode.Normal;

        [MenuItem("GameObject/Magica Cloth/Generate Colliders")]
        public static void Generate()
        {
            var prop = new SABoneColliderProperty();

            // Apply presets based on the fit mode
            switch (fitMode)
            {
                case ColliderFitMode.Loose:
                    prop.splitProperty.boneTriangleExtent = BoneTriangleExtent.Vertex2;
                    prop.reducerProperty.scale = new Vector3(1.2f, 1.2f, 1.2f);
                    prop.reducerProperty.fitType = FitType.Outer;
                    break;
                case ColliderFitMode.Tight:
                    prop.splitProperty.boneTriangleExtent = BoneTriangleExtent.Vertex1;
                    prop.reducerProperty.scale = new Vector3(0.9f, 0.9f, 0.9f);
                    prop.reducerProperty.fitType = FitType.Inner;
                    break;
                case ColliderFitMode.Normal:
                default:
                    prop.splitProperty.boneTriangleExtent = BoneTriangleExtent.Vertex2;
                    prop.reducerProperty.scale = Vector3.one;
                    prop.reducerProperty.fitType = FitType.Outer;
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
        public Quaternion rotation;
        public Vector3 center;
        public Vector3 boxA;
        public Vector3 boxB;
    }
}