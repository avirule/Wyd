#region

using UnityEngine;

#endregion

namespace Game.World.Blocks
{
    public struct BlockAction
    {
        public Vector3 LocalPosition { get; private set; }
        public ushort Id { get; private set; }

        public BlockAction(Vector3 globalPosition, ushort id) => (LocalPosition, Id) = (globalPosition, id);

        public void Initialise(Vector3 globalPosition, ushort id)
        {
            (LocalPosition, Id) = (globalPosition, id);
        }
    }
}
