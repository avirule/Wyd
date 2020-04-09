namespace Wyd.Game.World.Blocks
{
    public struct MeshBlock
    {
        public ushort Id;

        //public byte LightValue;
        //public Color256 Color;
        public BlockFaces Faces;

        public MeshBlock(ushort id, byte faces = 0)
        {
            Id = id;
            Faces = new BlockFaces(faces);
            //LightValue = 0;
            //Color = new Color256(0, 0, 0);
        }
    }
}
