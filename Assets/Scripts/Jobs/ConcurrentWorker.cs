#region

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;

#endregion

namespace Wyd.Jobs
{
    public static class ConcurrentWorker
    {
        private static readonly CancellationTokenSource _CancellationTokenSource;
        private static readonly Thread[] _WorkerThreads;
        private static readonly Channel<Work> _PendingWork;
        private static readonly AutoResetEvent _WorkReset;

        private static bool _Started;

        public static CancellationToken CancellationToken => _CancellationTokenSource.Token;

        static ConcurrentWorker()
        {
            _CancellationTokenSource = new CancellationTokenSource();
            _WorkerThreads = new Thread[Environment.ProcessorCount - 2];
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
                    Name = $"{nameof(ConcurrentWorker)}_{index:##}",
                    Priority = ThreadPriority.BelowNormal,
                    IsBackground = true
                };
                _WorkerThreads[index].Start();
            }
        }

        public static unsafe void Queue(Action action, bool autoReleaseOnFinish, EventHandler callback)
        {
            if (action == null)
            {
                throw new NullReferenceException();
            }

            WorkMetadata* workData = (WorkMetadata*)Marshal.AllocHGlobal(sizeof(WorkMetadata));
            workData->AutoRelease = autoReleaseOnFinish;
            Work work = new Work(workData, action);

            if (callback != null)
            {
                work.WorkFinished += callback;
            }

            if (_PendingWork.Writer.TryWrite(work))
            {
                _WorkReset.Set();
            }
        }

        private static unsafe void Worker()
        {
            Stopwatch stopwatch = new Stopwatch();

            while (!_CancellationTokenSource.IsCancellationRequested)
            {
                _WorkReset.WaitOne();

                while (_PendingWork.Reader.TryRead(out Work work))
                {
                    stopwatch.Restart();

                    work.Execute();

                    stopwatch.Stop();

                    work.Metadata->ProcessTime = stopwatch.Elapsed;

                    Thread.Sleep(0);
                }
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
            private readonly Action _WorkAction;

            internal readonly WorkMetadata* Metadata;

            public Work(WorkMetadata* metadata, Action workAction)
            {
                Metadata = metadata;
                _WorkAction = workAction;
            }

            internal event EventHandler WorkFinished;

            internal void Execute()
            {
                _WorkAction.Invoke();

                if (Metadata->AutoRelease)
                {
                    Release();
                }

                WorkFinished?.Invoke(this, EventArgs.Empty);
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
