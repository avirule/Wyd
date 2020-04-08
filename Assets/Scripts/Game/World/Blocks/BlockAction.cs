#region

using Unity.Mathematics;

#endregion

namespace Wyd.Game.World.Blocks
{
    public struct BlockAction
    {
        public float3 GlobalPosition { get; private set; }
        public ushort Id { get; private set; }

        public BlockAction(float3 globalPosition, ushort id) => (GlobalPosition, Id) = (globalPosition, id);

        public void SetData(float3 globalPosition, ushort id) => (GlobalPosition, Id) = (globalPosition, id);
    }
}
