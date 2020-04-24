#region

using System;
using System.Threading;

#endregion

namespace Wyd.System.Jobs
{
    public delegate bool MainThreadActionInvocation();

    public class MainThreadAction
    {
        private readonly ManualResetEvent _ManualResetEvent;
        private readonly MainThreadActionInvocation _Invocation;

        public MainThreadAction(ManualResetEvent manualResetEvent, MainThreadActionInvocation invocation)
        {
            _ManualResetEvent = manualResetEvent;
            _Invocation = invocation ?? throw new NullReferenceException(nameof(invocation));
        }

        public bool Invoke() => _Invocation();
        public void Set() => _ManualResetEvent?.Set();
    }
}
