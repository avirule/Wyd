#region

using System;

#endregion

namespace Wyd.System.Jobs
{
    public delegate void AsyncWorkerCountEventHandler(object sender, AsyncWorkerCountEventArgs args);

    public class AsyncWorkerCountEventArgs : EventArgs
    {
        public long WorkerCount { get; }

        public AsyncWorkerCountEventArgs(long workerCount) => WorkerCount = workerCount;
    }
}
