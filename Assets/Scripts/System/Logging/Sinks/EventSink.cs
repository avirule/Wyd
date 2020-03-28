#region

using Serilog.Core;
using Serilog.Events;

#endregion

namespace Wyd.System.Logging.Sinks
{
    public delegate void LogEventLoggedEventHandler(object sender, LogEvent logEvent);

    public class EventSink : ILogEventSink
    {
        public static event LogEventLoggedEventHandler Logged;

        public void Emit(LogEvent logEvent)
        {
            Logged?.Invoke(this, logEvent);
        }
    }
}
