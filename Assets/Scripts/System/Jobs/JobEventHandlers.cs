namespace Wyd.System.Jobs
{
    public delegate void JobQueuedEventHandler(object sender, JobEventArgs args);

    public delegate void JobStartedEventHandler(object sender, JobEventArgs args);

    public delegate void JobEventHandler(object sender, JobEventArgs args);

    public delegate void WorkerCountChangedEventHandler(object sender, int newCount);
}
