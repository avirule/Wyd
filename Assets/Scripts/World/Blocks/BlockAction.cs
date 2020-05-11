#region

using Unity.Mathematics;

#endregion

namespace Wyd.World.Blocks
{
    public class BlockAction
    {
        public int3 LocalPosition { get; private set; }
        public ushort Id { get; private set; }

        public BlockAction(int3 localPosition, ushort id) => (LocalPosition, Id) = (localPosition, id);

        public void SetData(int3 localPosition, ushort id) => (LocalPosition, Id) = (localPosition, id);
    }
}
