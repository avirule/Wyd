#region

using UnityEngine;

#endregion

namespace Wyd.Game.Entities
{
    public interface ICollideable
    {
        Collider Collider { get; }
    }
}
