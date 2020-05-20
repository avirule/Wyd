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
        private static readonly AutoResetEvent _WorkReset;

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
            _WorkReset = new AutoResetEvent(false);

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

        public static unsafe void Queue(Func<object> invocation, bool autoReleaseOnFinish, WorkEventHandler callback)
        {
            if (invocation == null)
            {
                throw new NullReferenceException();
            }

            WorkMetadata* workData = (WorkMetadata*)Marshal.AllocHGlobal(sizeof(WorkMetadata));
            workData->AutoRelease = autoReleaseOnFinish;
            Work work = new Work(workData, invocation);

            if (callback != null)
            {
                work.WorkFinished += callback;
            }

            if (_PendingWork.Writer.TryWrite(work))
            {
                _WorkReset.Set();
            }
        }

        private static unsafe void Worker(object data)
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
                    _WorkReset.WaitOne();

                    while (_PendingWork.Reader.TryRead(out currentWork))
                    {
                        stopwatch.Restart();

                        currentWork.Execute();

                        stopwatch.Stop();

                        currentWork.Metadata->ProcessTime = stopwatch.Elapsed;

                        Thread.Sleep(0);
                    }
                }
            }
            finally
            {
                currentWork?.Release();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WorkMetadata
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

        public unsafe class Work
        {
            private readonly Func<object> _Invocation;

            public WorkMetadata* Metadata { get; }
            public object Result { get; internal set; }

            public Work(WorkMetadata* metadata, Func<object> invocation)
            {
                Metadata = metadata;
                _Invocation = invocation;
            }

            internal event WorkEventHandler WorkFinished;

            internal void Execute()
            {
                Result = _Invocation.Invoke();

                if (Metadata->AutoRelease)
                {
                    Release();
                }

                WorkFinished?.Invoke(this, this);
            }

            public void Release()
            {
                if (Metadata == null)
                {
                    return;
                }

                Marshal.FreeHGlobal((IntPtr)Metadata);
            }
        }
    }
}
