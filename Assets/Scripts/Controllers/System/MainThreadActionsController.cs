#region

using System;
using System.Collections.Concurrent;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.System
{
    /// <summary>
    ///     Controller allowing qu
    /// </summary>
    public class MainThreadActionsController : SingletonController<MainThreadActionsController>
    {
        private ConcurrentStack<MainThreadAction> _Actions;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _Actions = new ConcurrentStack<MainThreadAction>();
        }

        private void Update()
        {
            // try to retrieve item from _Actions in safe time, or break if no items present
            while (SystemController.Current.IsInSafeFrameTime() && _Actions.TryPop(out MainThreadAction mainThreadAction))
            {
                mainThreadAction.Execute();
                mainThreadAction.Set();
            }
        }

        public void PushAction(MainThreadAction mainThreadAction)
        {
            _Actions.Push(mainThreadAction);
        }
    }
}
