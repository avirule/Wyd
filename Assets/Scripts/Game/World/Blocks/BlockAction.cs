#region

using Game.Entities;
using UnityEngine;

#endregion

namespace Game.World.Blocks
{
    public struct BlockAction
    {
        public enum Type
        {
            Add,
            Remove
        }

        public Type ActionType { get; private set; }
        public Vector3Int GlobalPosition { get; private set; }
        public ushort Id { get; private set; }
        public ICollector Sender { get; private set; }

        public BlockAction(Type actionType, Vector3Int globalPosition, ushort id, ICollector sender = null)
        {
            (ActionType, GlobalPosition, Id, Sender) = (actionType, globalPosition, id, sender);
        }

        public void Initialise(Type actionType, Vector3Int globalPosition, ushort id, ICollector sender = null)
        {
            (ActionType, GlobalPosition, Id, Sender) = (actionType, globalPosition, id, sender);
        }
    }
}
