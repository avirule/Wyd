#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;
using Wyd.Controllers.State;
using Wyd.System;
using Wyd.System.Extensions;

#endregion

namespace Wyd.Controllers.System
{
    public class PerFrameUpdateController : SingletonController<PerFrameUpdateController>
    {
        private Stopwatch _FrameTimer;
        private SortedList<int, List<IPerFrameUpdate>> _PerFrameUpdates;

        public bool IsInSafeFrameTime() => _FrameTimer.Elapsed <= OptionsController.Current.MaximumInternalFrameTime;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _FrameTimer = new Stopwatch();
            _PerFrameUpdates = new SortedList<int, List<IPerFrameUpdate>>();
        }

        private void Update()
        {
            _FrameTimer.Restart();

            foreach ((int _, List<IPerFrameUpdate> perFrameUpdaters) in _PerFrameUpdates)
            {
                foreach (IPerFrameUpdate perFrameUpdate in perFrameUpdaters)
                {
                    if (!IsInSafeFrameTime())
                    {
                        return;
                    }

                    perFrameUpdate.FrameUpdate();

                    if (!(perFrameUpdate is IPerFrameIncrementalUpdate perFrameIncrementalUpdate))
                    {
                        continue;
                    }

                    foreach (object _ in perFrameIncrementalUpdate.IncrementalFrameUpdate())
                    {
                        if (!IsInSafeFrameTime())
                        {
                            return;
                        }
                    }
                }
            }
        }

        public void RegisterPerFrameUpdater(int order, IPerFrameUpdate perFrameUpdate)
        {
            try
            {
                if (!_PerFrameUpdates.ContainsKey(order))
                {
                    _PerFrameUpdates.Add(order, new List<IPerFrameUpdate>());
                }

                _PerFrameUpdates[order].Add(perFrameUpdate);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Failed to register PerFrameUpdater ({nameof(order)}: {order}, {nameof(perFrameUpdate)}: {perFrameUpdate}): {ex.Message}");
            }
        }

        public void DeregisterPerFrameUpdater(int order, IPerFrameUpdate perFrameUpdate)
        {
            try
            {
                if (!_PerFrameUpdates.ContainsKey(order) || !_PerFrameUpdates[order].Contains(perFrameUpdate))
                {
                    return;
                }

                _PerFrameUpdates[order].Remove(perFrameUpdate);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Failed to deregister PerFrameUpdater ({nameof(order)}: {order}, {nameof(perFrameUpdate)}: {perFrameUpdate}): {ex.Message}");
            }
        }
    }
}
