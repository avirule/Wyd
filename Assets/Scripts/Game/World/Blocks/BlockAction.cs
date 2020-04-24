#region

using Unity.Mathematics;

#endregion

namespace Wyd.Game.World.Blocks
{
    public class BlockAction
    {
        public float3 LocalPosition { get; private set; }
        public ushort Id { get; private set; }

        public BlockAction(float3 globalPosition, ushort id) => (LocalPosition, Id) = (globalPosition, id);

        public void SetData(float3 globalPosition, ushort id) => (LocalPosition, Id) = (globalPosition, id);
    }
}
