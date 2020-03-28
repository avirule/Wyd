#region

using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

#endregion

namespace Wyd.System.Logging.Sinks
{
    public class MemorySink : ILogEventSink
    {
        // todo put these in a sensible location
        public const string ERROR_COLOR = "#FF6961";
        public const string WARN_COLOR = "#FFFF99";

        private readonly List<LogEvent> _LogEvents;

        public MemorySink(ref List<LogEvent> logEvents)
        {
            _LogEvents = logEvents;
        }

        public void Emit(LogEvent logEvent)
        {
            _LogEvents.Add(logEvent);
        }
    }
}
