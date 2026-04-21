using UnityEngine;

namespace MagicaClothColliderBuilder
{
    public struct MinBounding
    {
        public Vector3 BoxA;
        public Vector3 BoxB;
        public Vector3Int Euler;
        public float Volume;
        public bool IsSet;

        public void Set(Vector3 boxA, Vector3 boxB, Vector3Int euler, float volume)
        {
            BoxA = boxA;
            BoxB = boxB;
            Euler = euler;
            Volume = volume;
            IsSet = true;
        }

        public void Set(ref MinBounding minBounding)
        {
            BoxA = minBounding.BoxA;
            BoxB = minBounding.BoxB;
            Euler = minBounding.Euler;
            Volume = minBounding.Volume;
            IsSet = minBounding.IsSet;
        }

        public void Contain(Vector3 boxA, Vector3 boxB, Vector3Int euler, float volume)
        {
            if (!IsSet || volume < Volume)
            {
                Set(boxA, boxB, euler, volume);
            }
        }

        public void Contain(ref MinBounding minBounding)
        {
            if (!IsSet || minBounding.Volume < Volume)
            {
                Set(ref minBounding);
            }
        }
    }
}