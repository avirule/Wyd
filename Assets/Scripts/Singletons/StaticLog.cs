#region

using System.Collections.Generic;
using System.Threading;
using Serilog;
using Serilog.Events;
using UnityEngine;
using Wyd.Jobs;
using Wyd.Logging;

#endregion

namespace Wyd.Singletons
{
    public class StaticLog : Singleton<StaticLog>
    {
        private const string _DEFAULT_TEMPLATE = "{Timestamp:MM/dd/yy-HH:mm:ss} | {Level:u3} | {Message}\r\n";

        private static string _LogPath;
        private static int _RuntimeErrorCount;
        private static List<LogEvent> _LogEvents;

        public static IReadOnlyList<LogEvent> LoggedEvents => _LogEvents;

        public StaticLog()
        {
            _LogPath = $@"{Application.persistentDataPath}\logs\";
            _LogEvents = new List<LogEvent>();

            SetupStaticLogger();

            Log.Information(
                $"[{nameof(StaticLog)}] '{nameof(AsyncJobScheduler)}' set to {nameof(AsyncJobScheduler.MaximumConcurrentJobs)}: {AsyncJobScheduler.MaximumConcurrentJobs}");

            AsyncJobScheduler.JobQueued += (sender, asyncJob) =>
            {
                Log.Verbose($"[{nameof(StaticLog)}] Queued {nameof(AsyncJob)}: {asyncJob.GetType().Name}");
            };

            Application.logMessageReceived += LogHandler;
        }

        private static void SetupStaticLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(configuration =>
                    configuration.File(
                        $@"{_LogPath}\info\runtime_.log",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: _DEFAULT_TEMPLATE,
                        retainedFileCountLimit: 31,
                        rollOnFileSizeLimit: true,
                        restrictedToMinimumLevel: LogEventLevel.Information))
                // error log output
                .WriteTo.Async(configuration =>
                    configuration.File(
                        $@"{_LogPath}\error\runtime-error_.log",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: _DEFAULT_TEMPLATE,
                        retainedFileCountLimit: 31,
                        rollOnFileSizeLimit: true,
                        restrictedToMinimumLevel: LogEventLevel.Error))
#if UNITY_EDITOR
                .WriteTo.UnityDebugSink(_DEFAULT_TEMPLATE, LogEventLevel.Information)
#endif
                .WriteTo.MemorySink(ref _LogEvents)
                .WriteTo.EventSink()
                .MinimumLevel.Verbose()
                .CreateLogger();
        }

        private static void LogHandler(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            Log.Fatal(stackTrace);

            Interlocked.Increment(ref _RuntimeErrorCount);
        }
    }
}
