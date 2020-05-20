#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;

#endregion

namespace Wyd.Singletons
{
    public delegate bool MainThreadActionInvocation();

    public class MainThreadActions : Singleton<MainThreadActions>, IPerFrameIncrementalUpdate
    {
        private class MainThreadAction
        {
            private readonly ManualResetEventSlim _ManualResetSlim;
            private readonly MainThreadActionInvocation _Invocation;

            public MainThreadAction(ManualResetEventSlim manualResetSlim, MainThreadActionInvocation invocation)
            {
                _ManualResetSlim = manualResetSlim;
                _Invocation = invocation ?? throw new NullReferenceException(nameof(invocation));
            }

            public bool Invoke() => _Invocation();
            public void Set() => _ManualResetSlim?.Set();
        }

        private readonly ConcurrentQueue<MainThreadAction> _Actions;

        public MainThreadActions()
        {
            AssignSingletonInstance(this);

            _Actions = new ConcurrentQueue<MainThreadAction>();
        }

        public ManualResetEventSlim QueueAction(MainThreadActionInvocation invocation)
        {
            ManualResetEventSlim manualResetSlim = new ManualResetEventSlim(false);

            _Actions.Enqueue(new MainThreadAction(manualResetSlim, invocation));

            return manualResetSlim;
        }

        public void FrameUpdate() { }

        public IEnumerable IncrementalFrameUpdate()
        {
            while (_Actions.TryDequeue(out MainThreadAction mainThreadAction))
            {
                if (!mainThreadAction.Invoke())
                {
                    _Actions.Enqueue(mainThreadAction);
                }

                mainThreadAction.Set();

                yield return null;
            }
        }
    }
}
