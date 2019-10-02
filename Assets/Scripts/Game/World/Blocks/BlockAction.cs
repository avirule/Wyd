#region

using UnityEngine;

#endregion

namespace Game.World.Blocks
{
    public struct BlockAction
    {
        public Vector3 GlobalPosition { get; private set; }
        public ushort Id { get; private set; }

        public BlockAction(Vector3 globalPosition, ushort id)
        {
            (GlobalPosition, Id) = (globalPosition, id);
        }

        public void Initialise(Vector3 globalPosition, ushort id)
        {
            (GlobalPosition, Id) = (globalPosition, id);
        }
    }
}
