using MagicaCloth2;
using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public struct CapsuleFitResult
    {
        public Quaternion LocalRotation;
        public MagicaCapsuleCollider.Direction Direction;
        public Vector3 Center;
        public float Length;
        public float RadiusAtMin;
        public float RadiusAtMax;
        public bool ReverseDirection;
    }
}