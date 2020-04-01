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
        private class PerFrameUpdateCollectionModification
        {
            public int Order { get; }
            public IPerFrameUpdate PerFrameUpdate { get; }
            public bool IsAddition { get; }

            public PerFrameUpdateCollectionModification(int order, IPerFrameUpdate perFrameUpdate, bool isAddition) =>
                (Order, PerFrameUpdate, IsAddition) = (order, perFrameUpdate, isAddition);
        }

        private Stopwatch _FrameTimer;
        private SortedList<int, List<IPerFrameUpdate>> _PerFrameUpdates;
        private Stack<PerFrameUpdateCollectionModification> _PerFrameUpdateCollectionModifications;

        public bool IsInSafeFrameTime() => _FrameTimer.Elapsed <= OptionsController.Current.MaximumInternalFrameTime;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _FrameTimer = new Stopwatch();
            _PerFrameUpdates = new SortedList<int, List<IPerFrameUpdate>>();
            _PerFrameUpdateCollectionModifications = new Stack<PerFrameUpdateCollectionModification>();
        }

        private void Update()
        {
            _FrameTimer.Restart();

            try
            {
                ProcessPerFrameUpdateModifications();

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
            catch (InvalidOperationException)
            {
                Log.Warning(
                    $"{nameof(_PerFrameUpdates)} was modified during enumeration and failed to complete a full cycle. This could be due to threaded access.");
            }
        }

        private void ProcessPerFrameUpdateModifications()
        {
            while (_PerFrameUpdateCollectionModifications.Count > 0)
            {
                PerFrameUpdateCollectionModification perFrameUpdateCollectionModification =
                    _PerFrameUpdateCollectionModifications.Pop();

                if (perFrameUpdateCollectionModification.IsAddition)
                {
                    if (!_PerFrameUpdates.ContainsKey(perFrameUpdateCollectionModification.Order))
                    {
                        _PerFrameUpdates.Add(perFrameUpdateCollectionModification.Order, new List<IPerFrameUpdate>());
                    }

                    _PerFrameUpdates[perFrameUpdateCollectionModification.Order]
                        .Add(perFrameUpdateCollectionModification.PerFrameUpdate);
                }
                else
                {
                    _PerFrameUpdates[perFrameUpdateCollectionModification.Order]
                        .Remove(perFrameUpdateCollectionModification.PerFrameUpdate);
                }
            }
        }

        public void RegisterPerFrameUpdater(int order, IPerFrameUpdate perFrameUpdate)
        {
            _PerFrameUpdateCollectionModifications.Push(
                new PerFrameUpdateCollectionModification(order, perFrameUpdate, true));
        }

        public void DeregisterPerFrameUpdater(int order, IPerFrameUpdate perFrameUpdate)
        {
            if (!_PerFrameUpdates.ContainsKey(order) || !_PerFrameUpdates[order].Contains(perFrameUpdate))
            {
                return;
            }

            _PerFrameUpdateCollectionModifications.Push(
                new PerFrameUpdateCollectionModification(order, perFrameUpdate, false));
        }
    }
}
