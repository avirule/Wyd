#region

using System;

#endregion

namespace Wyd.System.Jobs
{
    public class AsyncJobEventArgs : EventArgs
    {
        public readonly AsyncJob AsyncJob;

        public AsyncJobEventArgs(AsyncJob asyncJob) => AsyncJob = asyncJob;
    }
}
