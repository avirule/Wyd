using System.Collections.Generic;
using Environment.Terrain;

namespace Plugins.Blocks
{
    public interface IBlockRegistry
    {
        List<IBlockRegistrar> Registrars { get; }
    }
}