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
            private readonly SemaphoreSlim _SemaphoreReset;
            private readonly MainThreadActionInvocation _Invocation;

            public MainThreadAction(SemaphoreSlim semaphoreReset, MainThreadActionInvocation invocation)
            {
                _SemaphoreReset = semaphoreReset;
                _Invocation = invocation ?? throw new NullReferenceException(nameof(invocation));
            }

            public bool Invoke() => _Invocation();
            public void Set() => _SemaphoreReset?.Release();
        }

        private readonly ConcurrentQueue<MainThreadAction> _Actions;

        public MainThreadActions()
        {
            AssignSingletonInstance(this);

            _Actions = new ConcurrentQueue<MainThreadAction>();
        }

        public SemaphoreSlim QueueAction(MainThreadActionInvocation invocation)
        {
            SemaphoreSlim semaphoreReset = new SemaphoreSlim(0, 1);

            _Actions.Enqueue(new MainThreadAction(semaphoreReset, invocation));

            return semaphoreReset;
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
