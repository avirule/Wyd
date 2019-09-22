#region

using UnityEngine;

#endregion

namespace Extensions
{
    public static class ValueTupleExtensions
    {
        public static int To1D(this (int x, int y, int z) value, Vector3Int size3d)
        {
            return value.x + (value.z * size3d.x) + (value.y * size3d.x * size3d.z);
        }
    }
}
