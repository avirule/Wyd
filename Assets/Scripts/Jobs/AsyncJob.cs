#region

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global

#endregion

namespace Wyd.Jobs
{
    /// <summary>
    ///     Used for
    /// </summary>
    public abstract class AsyncJob
    {
        private readonly object _IsCancelledLock;
        private readonly object _IsWorkFinishedLock;

        private bool _IsCancelled;
        private bool _IsWorkFinished;

        /// <summary>
        ///     Token that can be passed into constructor to allow jobs to observe cancellation.
        /// </summary>
        protected CancellationToken _CancellationToken { get; set; }

        protected Stopwatch _Stopwatch { get; }

        /// <summary>
        ///     Identity of the <see cref="AsyncJob" />.
        /// </summary>
        public Guid Identity { get; }

        public bool IsCancelled
        {
            get
            {
                bool tmp;
                lock (_IsCancelledLock)
                {
                    tmp = _IsCancelled;
                }

                return tmp;
            }
            set
            {
                lock (_IsCancelledLock)
                {
                    _IsCancelled = value;
                }
            }
        }

        /// <summary>
        ///     Thread-safe determination of execution status.
        /// </summary>
        public bool IsWorkFinished
        {
            get
            {
                bool tmp;

                lock (_IsWorkFinishedLock)
                {
                    tmp = _IsWorkFinished;
                }

                return tmp;
            }
            set
            {
                lock (_IsWorkFinishedLock)
                {
                    _IsWorkFinished = value;
                }
            }
        }

        /// <summary>
        ///     Elapsed execution time of the <see cref="Process" /> function.
        /// </summary>
        public TimeSpan ProcessTime { get; private set; }

        /// <summary>
        ///     Elapsed execution time of job.
        /// </summary>
        public TimeSpan ExecutionTime { get; private set; }

        public event EventHandler<AsyncJob> WorkFinished;

        protected AsyncJob()
        {
            _IsCancelledLock = new object();
            _IsWorkFinishedLock = new object();
            _Stopwatch = new Stopwatch();

            // create new, unique job identity
            Identity = Guid.NewGuid();

            _CancellationToken = AsyncJobScheduler.AbortToken;
            IsWorkFinished = false;
        }

        /// <summary>
        ///     Instantiates a new instance of the <see cref="AsyncJob" /> class.
        /// </summary>
        protected AsyncJob(CancellationToken cancellationToken)
        {
            _IsCancelledLock = new object();
            _IsWorkFinishedLock = new object();
            _Stopwatch = new Stopwatch();

            // create new, unique job identity
            Identity = Guid.NewGuid();

            // combine AsyncJobScheduler's cancellation token with the given token, to effectively observe both
            _CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken, cancellationToken).Token;
            IsWorkFinished = false;
        }

        /// <summary>
        ///     Begins executing the <see cref="AsyncJob" />.
        /// </summary>
        internal async Task Execute()
        {
            try
            {
                // observe cancellation token
                if (_CancellationToken.IsCancellationRequested)
                {
                    Cancelled();
                    return;
                }

                _Stopwatch.Restart();

                await Process().ConfigureAwait(false);

                ProcessTime = _Stopwatch.Elapsed;

                await ProcessFinished().ConfigureAwait(false);

                _Stopwatch.Stop();

                ExecutionTime = _Stopwatch.Elapsed;

                // and signal WorkFinished event
                WorkFinished?.Invoke(this, this);
            }
            catch (Exception ex)
            {
                // errors are always consumed here due to usage of Task.Run further up the call stack
                Log.Error($"({nameof(AsyncJob)}) {ex}");
                throw;
            }
            finally
            {
                // work is finished, so flip IsWorkFinished bool
                IsWorkFinished = true;

                // dereference any subscriptors to avoid memory leaks
                WorkFinished = null;
            }
        }

        /// <summary>
        ///     This is the primary method that is executed.
        /// </summary>
        protected virtual Task Process() => Task.CompletedTask;

        /// <summary>
        ///     The final method, run after <see cref="Process" />.
        /// </summary>
        protected virtual Task ProcessFinished() => Task.CompletedTask;

        internal virtual void Cancelled()
        {
            IsCancelled = true;
        }
    }
}
