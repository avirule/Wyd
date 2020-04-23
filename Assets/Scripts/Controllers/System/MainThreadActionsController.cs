#region

using System.Collections;
using System.Collections.Concurrent;
using Wyd.System;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.System
{
    /// <summary>
    ///     Controller allowing qu
    /// </summary>
    public class MainThreadActionsController : SingletonController<MainThreadActionsController>,
        IPerFrameIncrementalUpdate
    {
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

        public void QueueAction(MainThreadAction mainThreadAction)
        {
            _Actions.Enqueue(mainThreadAction);
        }

        public void FrameUpdate() { }

        public IEnumerable IncrementalFrameUpdate()
        {
            while (_Actions.TryDequeue(out MainThreadAction mainThreadAction))
            {
                if (!mainThreadAction.Invoke())
                {
                    QueueAction(mainThreadAction);
                }

                mainThreadAction.Set();

                yield return null;
            }
        }
    }
}
