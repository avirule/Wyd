#region

using System;
using System.Threading;

#endregion

namespace Wyd.System.Jobs
{
    public class MainThreadAction
    {
        private readonly ManualResetEvent _ManualResetEvent;
        private readonly Action _Action;

        public MainThreadAction(ManualResetEvent manualResetEvent, Action action) =>
            (_ManualResetEvent, _Action) = (manualResetEvent, action);

        public void Execute() => _Action?.Invoke();
        public void Set() => _ManualResetEvent?.Set();
    }
}
