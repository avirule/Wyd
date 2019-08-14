#region

using Environment.Terrain;

#endregion

namespace Plugins.Blocks
{
    public interface IBlockRegistrar
    {
        string BlockName { get; }
        bool IsTransparent { get; }
        RuleEvaluation UVs { get; }
    }
}