namespace Wyd.System.Jobs
{
    public delegate void AsyncJobSchedulerLogEventHandler(object sender, AsyncJobSchedulerLogEventArgs args);

    public class AsyncJobSchedulerLogEventArgs
    {
        public int LogLevel { get; }
        public string Text { get; }

        public AsyncJobSchedulerLogEventArgs(int logLevel, string text)
        {
            Text = text;
            LogLevel = logLevel;
        }
    }
}
