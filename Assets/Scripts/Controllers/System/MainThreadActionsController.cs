#region

using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using Wyd.System;
using Wyd.System.Jobs;

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
            private ManualResetEvent ResetEvent { get; }
            private MainThreadActionInvocation Invocation { get; }

            public MainThreadAction(ManualResetEvent resetEvent, MainThreadActionInvocation invocation)
            {
                ResetEvent = resetEvent;
                Invocation = invocation;
            }

            public void Set() => ResetEvent.Set();
            public bool Invoke() => Invocation?.Invoke() ?? true;
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

        public ManualResetEvent QueueAction(MainThreadActionInvocation action)
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            _Actions.Enqueue(new MainThreadAction(resetEvent, action));

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
