namespace Wyd.Jobs
{
    public delegate void JobQueuedEventHandler(object sender, JobEventArgs args);

    public delegate void JobStartedEventHandler(object sender, JobEventArgs args);

    public delegate void JobFinishedEventHandler(object sender, JobEventArgs args);

    public delegate void WorkerCountChangedEventHandler(object sender, int newCount);
}
