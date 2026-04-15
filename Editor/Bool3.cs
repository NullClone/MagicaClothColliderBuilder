using System;

namespace MagicaClothColliderBuilder
{
    [Serializable]
    public struct Bool3
    {
        public bool x;
        public bool y;
        public bool z;

        public Bool3(bool x, bool y, bool z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
}
