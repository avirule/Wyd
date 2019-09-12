#region

using UnityEngine;

#endregion

namespace Game.Entities
{
    public interface ICollideable
    {
        Collider Collider { get; }
    }
}
