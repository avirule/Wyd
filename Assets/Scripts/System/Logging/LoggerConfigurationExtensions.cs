#region

using System.Collections.Generic;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Wyd.System.Logging.Sinks;

#endregion

namespace Wyd.System.Logging
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration UnityDebugSink(this LoggerSinkConfiguration sinkConfiguration,
            string outputTemplate, LogEventLevel minimumLevel = LogEventLevel.Debug) => sinkConfiguration.Sink(new UnityDebugLoggerSink(outputTemplate, minimumLevel));

        public static LoggerConfiguration MemorySink(this LoggerSinkConfiguration sinkConfiguration,
            ref List<LogEvent> logEvents) => sinkConfiguration.Sink(new MemorySink(ref logEvents));

        public static LoggerConfiguration EventSink(this LoggerSinkConfiguration sinkConfiguration) =>
            sinkConfiguration.Sink(new EventSink());
    }
}
