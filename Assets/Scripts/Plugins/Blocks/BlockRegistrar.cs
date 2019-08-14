using Environment.Terrain;

namespace Plugins.Blocks
{
    public struct BlockRegistrar : IBlockRegistrar
    {
        public string BlockName { get; }
        public bool IsTransparent { get; }
        public RuleEvaluation UVs { get; }
        
        public BlockRegistrar(string blockName, bool isTransparent, RuleEvaluation uVs)
        {
            BlockName = blockName;
            IsTransparent = isTransparent;
            UVs = uVs;
        }
    }
}