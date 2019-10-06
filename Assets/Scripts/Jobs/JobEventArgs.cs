#region

using System;

#endregion

namespace Wyd.Jobs
{
    public class JobEventArgs : EventArgs
    {
        public readonly Job Job;

        public JobEventArgs(Job job) => Job = job;
    }
}
