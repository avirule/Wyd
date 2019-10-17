#region

using Serilog;
using Serilog.Configuration;
using Wyd.System.Logging.Sinks;

#endregion

namespace Wyd.System.Logging
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration UnityDebugSink(this LoggerSinkConfiguration sinkConfiguration,
            string outputTemplate) =>
            sinkConfiguration.Sink(new UnityDebugLoggerSink(outputTemplate));

        public static LoggerConfiguration MemorySink(this LoggerSinkConfiguration sinkConfiguration,
            string outputTemplate) =>
            sinkConfiguration.Sink(new MemorySink(outputTemplate));
    }
}
