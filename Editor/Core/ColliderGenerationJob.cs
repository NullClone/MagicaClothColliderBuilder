using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public class ColliderGenerationJob
    {
        // Properties

        public GameObject TargetBone { get; }

        public SABoneColliderProperty Property { get; }

        public Vector3[] Vertices { get; private set; }

        public int[] Triangles { get; private set; }

        public Animator Animator { get; }

        private readonly BoneMeshCache m_BoneMeshCache;

        // Methods

        public ColliderGenerationJob(GameObject targetBone, Animator animator, SABoneColliderProperty property, BoneMeshCache boneMeshCache)
        {
            TargetBone = targetBone;
            Property = property;
            m_BoneMeshCache = boneMeshCache;
            Animator = animator;
        }

        public bool Prepare()
        {
            var boneMeshCreator = new BoneMeshCreator();

            if (!boneMeshCreator.Process(TargetBone, Property.SplitProperty, m_BoneMeshCache)) return false;

            Vertices = boneMeshCreator.BoneVertices;
            Triangles = boneMeshCreator.BoneTriangles;

            return Vertices != null && Vertices.Length > 0;
        }
    }
}
