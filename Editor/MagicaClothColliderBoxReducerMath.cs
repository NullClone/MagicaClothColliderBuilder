using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public partial class MagicaClothColliderBoxReducer
    {
        private sealed class BoxCollector
        {
            public bool HasAny { get; private set; }

            public Vector3 BoxA { get; private set; } = Vector3.zero;

            public Vector3 BoxB { get; private set; } = Vector3.zero;

            public void Collect(Vector3 vertex)
            {
                if (!HasAny)
                {
                    HasAny = true;
                    BoxA = vertex;
                    BoxB = vertex;

                    return;
                }

                BoxA = Min(BoxA, vertex);
                BoxB = Max(BoxB, vertex);
            }

            public void Collect(Vector3[] vertexArray)
            {
                if (vertexArray == null) return;

                for (int i = 0; i < vertexArray.Length; ++i)
                {
                    Collect(vertexArray[i]);
                }
            }
        }

        private static bool IsFuzzyZero(float value)
        {
            return Mathf.Abs(value) <= Mathf.Epsilon;
        }

        private static float GetVolume(Vector3 v)
        {
            return Mathf.Abs(v.x * v.y * v.z);
        }

        private static float GetBoxVolume(Vector3 boxA, Vector3 boxB)
        {
            Vector3 t = boxB - boxA;
            return Mathf.Abs(t.x * t.y * t.z);
        }

        public static Matrix4x4 RotationMatrix(Quaternion rotation)
        {
            return Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);
        }

        public static Matrix4x4 TranslateRotationMatrix(Vector3 translate, Quaternion rotation)
        {
            Matrix4x4 translateTransform = Matrix4x4.identity;
            translateTransform.SetColumn(3, new Vector4(translate.x, translate.y, translate.z, 1.0f));
            return Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one) * translateTransform;
        }

        public static Vector3 Min(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Mathf.Min(a.x, b.x),
                Mathf.Min(a.y, b.y),
                Mathf.Min(a.z, b.z));
        }

        public static Vector3 Max(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Mathf.Max(a.x, b.x),
                Mathf.Max(a.y, b.y),
                Mathf.Max(a.z, b.z));
        }

        public static Vector3 ScaledVector(Vector3 v, Vector3 s)
        {
            return new Vector3(v.x * s.x, v.y * s.y, v.z * s.z);
        }

        public static Quaternion InversedRotation(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }
    }
}
