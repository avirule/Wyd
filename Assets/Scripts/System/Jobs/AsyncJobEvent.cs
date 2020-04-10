#region

using System;

#endregion

namespace Wyd.System.Jobs
{
    public delegate void AsyncJobEventHandler(object sender, AsyncJobEventArgs args);

    public class AsyncJobEventArgs : EventArgs
    {
        public readonly AsyncJob AsyncJob;

        public AsyncJobEventArgs(AsyncJob asyncJob) => AsyncJob = asyncJob;
    }
}
