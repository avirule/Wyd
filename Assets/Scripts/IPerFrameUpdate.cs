#region

using System.Collections;

#endregion

namespace Wyd
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
