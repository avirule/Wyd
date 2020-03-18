namespace Wyd.Game.World.Blocks
{
    public struct MeshBlock
    {
        public readonly ushort Id;
        public BlockFaces Faces;

        public MeshBlock(ushort id, byte faces = 0)
        {
            Id = id;
            Faces = new BlockFaces(faces);
        }
    }
}
