#region

using System.Collections;

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
