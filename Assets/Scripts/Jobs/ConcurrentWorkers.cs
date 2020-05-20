#region

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;

#endregion

namespace Wyd.Jobs
{
    public static class ConcurrentWorkers
    {
        public delegate void WorkEventHandler(object sender, Work work);

        private static readonly CancellationTokenSource _CancellationTokenSource;
        private static readonly Thread[] _WorkerThreads;
        private static readonly Channel<Work> _PendingWork;
        private static readonly SemaphoreSlim _PendingWorkNotifier;

        private static bool _Started;

        public static CancellationToken AbortToken => _CancellationTokenSource.Token;

        public static int Count { get; }

        static ConcurrentWorkers()
        {
            Count = Environment.ProcessorCount - 2;

            _CancellationTokenSource = new CancellationTokenSource();
            _WorkerThreads = new Thread[Count];
            _PendingWork = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = false,
                SingleWriter = false
            });
            _PendingWorkNotifier = new SemaphoreSlim(0, int.MaxValue);

            Start();
        }

        private static void Start()
        {
            if (_Started)
            {
                throw new InvalidOperationException("Already started.");
            }

            _Started = true;

            for (int index = 0; index < _WorkerThreads.Length; index++)
            {
                _WorkerThreads[index] = new Thread(Worker)
                {
                    Name = $"{nameof(ConcurrentWorkers)}_{index:##}",
                    Priority = ThreadPriority.BelowNormal,
                    IsBackground = true
                };
                _WorkerThreads[index].Start(AbortToken);
            }
        }

        public static unsafe void Queue(Func<object> invocation, Action cancelled, bool autoReleaseOnFinish, WorkEventHandler callback)
        {
            if (invocation == null)
            {
                throw new NullReferenceException();
            }

            Work.Metadata* workData = (Work.Metadata*)Marshal.AllocHGlobal(sizeof(Work.Metadata));
            workData->AutoRelease = autoReleaseOnFinish;
            Work work = new Work(workData, invocation, cancelled);

            if (callback != null)
            {
                work.Complete += callback;
            }

            if (_PendingWork.Writer.TryWrite(work))
            {
                _PendingWorkNotifier.Release();
            }
        }

        private static void Worker(object data)
        {
            if (!(data is CancellationToken cancellationToken))
            {
                throw new ArgumentException($"Argument is not of type {typeof(CancellationToken)}.");
            }

            Stopwatch stopwatch = new Stopwatch();
            Work currentWork = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    _PendingWorkNotifier.Wait();

                    if (_PendingWork.Reader.TryRead(out currentWork))
                    {
                        currentWork.Execute(stopwatch);
                        currentWork = null;

                        Thread.Sleep(0);
                    }
                }
            }
            catch (Exception)
            {
                currentWork?.Cancel();
                throw;
            }
            finally
            {
                currentWork?.Release();
            }
        }


        public sealed unsafe class Work
        {
            private Action _Cancelled;
            private Func<object> _Invocation;

            public Metadata* PMetadata { get; }
            public object Result { get; private set; }

            public Work(Metadata* pMetadata, Func<object> invocation, Action cancelled)
            {
                PMetadata = pMetadata;
                _Invocation = invocation;
                _Cancelled = cancelled;
            }

            internal event WorkEventHandler Complete;

            internal void Execute(Stopwatch stopwatch)
            {
                stopwatch.Restart();

                Result = _Invocation.Invoke();

                stopwatch.Stop();

                PMetadata->ProcessTime = stopwatch.Elapsed;

                Complete?.Invoke(this, this);

                if (PMetadata->AutoRelease)
                {
                    Release();
                }
            }

            internal void Cancel()
            {
                if (_Cancelled == null)
                {
                    return;
                }

                _Cancelled.Invoke();

                // ensure it cannot be called again
                _Cancelled = null;

                Release();
            }

            public void Release()
            {
                if (PMetadata == null)
                {
                    return;
                }

                Marshal.FreeHGlobal((IntPtr)PMetadata);
                _Invocation = null;
                _Cancelled = null;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct Metadata
            {
                private bool _AutoRelease;
                private TimeSpan _ProcessTime;

                public bool AutoRelease
                {
                    get => _AutoRelease;
                    internal set => _AutoRelease = value;
                }

                public TimeSpan ProcessTime
                {
                    get => _ProcessTime;
                    internal set => _ProcessTime = value;
                }
            }
        }
    }
}
