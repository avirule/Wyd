#region

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

#endregion

namespace Wyd.System
{
    public interface IPerFrameUpdate
    {
        void FrameUpdate();
    }

    public interface IPerFrameIncrementalUpdate : IPerFrameUpdate
    {
        IEnumerable IncrementalFrameUpdate();
    }
}
