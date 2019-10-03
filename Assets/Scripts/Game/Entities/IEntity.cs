#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Game.Entities
{
    public interface IEntity
    {
        Transform Transform { get; }
        Rigidbody Rigidbody { get; }
        Vector3 CurrentChunk { get; }
        HashSet<string> Tags { get; }

        event EventHandler<Vector3> PositionChanged;
        event EventHandler<Vector3> ChunkPositionChanged;
        event EventHandler<IEntity> EntityDestroyed;
    }
}
