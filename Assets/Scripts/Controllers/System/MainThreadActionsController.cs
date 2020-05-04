#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using Wyd.System;

#endregion

namespace Wyd.Controllers.System
{
    public delegate bool MainThreadActionInvocation();

    /// <summary>
    ///     Controller allowing qu
    /// </summary>
    public class MainThreadActionsController : SingletonController<MainThreadActionsController>, IPerFrameIncrementalUpdate
    {
        private class MainThreadAction
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

        private ConcurrentQueue<MainThreadAction> _Actions;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _Actions = new ConcurrentQueue<MainThreadAction>();
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(-900, this);
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(-900, this);
        }

        public ManualResetEvent QueueAction(MainThreadActionInvocation invocation)
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            _Actions.Enqueue(new MainThreadAction(resetEvent, invocation));

            return resetEvent;
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
