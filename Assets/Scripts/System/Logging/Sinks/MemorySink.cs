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

        private string _OutputTemplate;

        public readonly List<LogEvent> LogEvents;


        public MemorySink(string outputTemplate)
        {
            LogEvents = new List<LogEvent>();

            _OutputTemplate = outputTemplate;
        }

        public void Emit(LogEvent logEvent)
        {
            LogEvents.Add(logEvent);
        }
    }
}
