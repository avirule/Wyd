#region

using System.Collections.Generic;

#endregion

namespace Plugins.Blocks
{
    public interface IBlockRegistry
    {
        List<IBlockRegistrar> Registrars { get; }
    }
}