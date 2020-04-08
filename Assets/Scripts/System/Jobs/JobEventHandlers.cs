namespace Wyd.System.Jobs
{
    public delegate void JobQueuedEventHandler(object sender, AsyncJobEventArgs args);

    public delegate void JobEventHandler(object sender, AsyncJobEventArgs args);
}
