#region

using System.Diagnostics;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.System
{
    public class SystemController : SingletonController<SystemController>
    {
        private Stopwatch _FrameTimer;

        public bool IsInSafeFrameTime() => _FrameTimer.Elapsed <= OptionsController.Current.MaximumInternalFrameTime;

        private void Awake()
        {
            AssignSingletonInstance(this);
            DontDestroyOnLoad(this);

            _FrameTimer = new Stopwatch();
        }

        private void Update()
        {
            _FrameTimer.Restart();
        }
    }
}
