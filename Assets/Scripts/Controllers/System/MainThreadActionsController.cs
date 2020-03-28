#region

using System;
using System.Collections.Concurrent;

#endregion

namespace Wyd.Controllers.System
{
    /// <summary>
    ///     Controller allowing qu
    /// </summary>
    public class MainThreadActionsController : SingletonController<MainThreadActionsController>
    {
        private ConcurrentStack<Action> _Actions;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _Actions = new ConcurrentStack<Action>();
        }

        private void Update()
        {
            // try to retrieve item from _Actions in safe time, or break if no items present
            while (SystemController.Current.IsInSafeFrameTime() && _Actions.TryPop(out Action currentAction))
            {
                currentAction.Invoke();
            }
        }

        public void PushAction(Action action)
        {
            _Actions.Push(action);
        }
    }
}
