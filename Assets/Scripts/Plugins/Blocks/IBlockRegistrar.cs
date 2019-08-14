using Environment.Terrain;

namespace Plugins.Blocks
{
        public interface IBlockRegistrar
        {
            string BlockName { get; }
            bool IsTransparent { get; }
            RuleEvaluation UVs { get; }
        }
}