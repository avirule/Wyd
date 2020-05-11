#region

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

#endregion

namespace Wyd.Entities
{
    public interface IEntity
    {
        Transform Transform { get; }
        int3 ChunkPosition { get; }
        HashSet<string> Tags { get; }

        event EventHandler<float3> PositionChanged;
        event EventHandler<int3> ChunkPositionChanged;
        event EventHandler<IEntity> EntityDestroyed;
    }
}
