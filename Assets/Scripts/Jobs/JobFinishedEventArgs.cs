#region

using System;

#endregion

namespace Jobs
{
    public class JobFinishedEventArgs : EventArgs
    {
        public readonly Job Job;

        public JobFinishedEventArgs(Job job) => Job = job;
    }
}
