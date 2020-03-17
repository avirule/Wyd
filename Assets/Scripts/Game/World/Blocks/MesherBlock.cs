namespace Wyd.Game.World.Blocks
{
    public struct MesherBlock
    {
        public readonly ushort Id;
        public BlockFaces Faces;

        public MesherBlock(ushort id, byte faces = 0)
        {
            Id = id;
            Faces = new BlockFaces(faces);
        }
    }
}
