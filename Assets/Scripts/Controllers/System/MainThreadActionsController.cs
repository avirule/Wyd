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
        private ConcurrentStack<MainThreadAction> _Actions;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _Actions = new ConcurrentStack<MainThreadAction>();
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(-900, this);
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(-900, this);
        }

        public void PushAction(MainThreadAction mainThreadAction)
        {
            _Actions.Push(mainThreadAction);
        }

        public void FrameUpdate() { }

        public IEnumerable IncrementalFrameUpdate()
        {
            while (_Actions.TryPop(out MainThreadAction mainThreadAction))
            {
                mainThreadAction.Execute();
                mainThreadAction.Set();

                yield return null;
            }
        }
    }
}
