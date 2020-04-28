#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSafeFrameTime() => _FrameTimer.Elapsed <= OptionsController.Current.TargetFrameRateTimeSpan;

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
#if UNITY_EDITOR

                bool firstRun = true;
                int previousSortValue = int.MinValue;

#endif

                foreach ((int sortValue, List<IPerFrameUpdate> perFrameUpdaters) in _PerFrameUpdates)
                {
#if UNITY_EDITOR

                    if (firstRun)
                    {
                        firstRun = false;
                    }
                    else if (previousSortValue > sortValue)
                    {
                        Log.Warning(
                            $"{nameof(PerFrameUpdateController)} is updating in descending order ({previousSortValue} < {sortValue}).");
                    }

                    previousSortValue = sortValue;

#endif

                    foreach (IPerFrameUpdate perFrameUpdate in perFrameUpdaters)
                    {
                        if (!IsSafeFrameTime())
                        {
                            return;
                        }

                        if (perFrameUpdate is IPerFrameIncrementalUpdate perFrameIncrementalUpdate)
                        {
                            IEnumerator perFrameEnumerator =
                                perFrameIncrementalUpdate.IncrementalFrameUpdate().GetEnumerator();

                            while (perFrameEnumerator.MoveNext())
                            {
                                if (!IsSafeFrameTime())
                                {
                                    return;
                                }
                            }
                        }

                        perFrameUpdate.FrameUpdate();
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning($"Error in {nameof(PerFrameUpdateController)}: {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private void ProcessPerFrameUpdateModifications()
        {
            while (_PerFrameUpdateCollectionModifications.Count > 0 && IsSafeFrameTime())
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
