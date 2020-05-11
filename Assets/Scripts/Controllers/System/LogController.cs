#region

using System.Collections.Generic;
using System.Threading;
using Serilog;
using Serilog.Events;
using UnityEngine;
using Wyd.Jobs;
using Wyd.Logging;

#endregion

namespace Wyd.Controllers.System
{
    public class LogController : MonoBehaviour
    {
        private const string _DEFAULT_TEMPLATE = "{Timestamp:MM/dd/yy-HH:mm:ss} | {Level:u3} | {Message}\r\n";
        private const int _MAXIMUM_RUNTIME_ERRORS = 10;

        private static string _LogPath;
        private static int _RuntimeErrorCount;
        private static bool _KillApplication;
        private static List<LogEvent> _LogEvents;

        public static IReadOnlyList<LogEvent> LoggedEvents => _LogEvents;

#if UNITY_EDITOR

        public LogEventLevel MinimumLevel;

#endif

        private void Awake()
        {
            _LogPath = $@"{Application.persistentDataPath}\logs\";
            _LogEvents = new List<LogEvent>();

            SetupStaticLogger();

            AsyncJobScheduler.MaximumProcessingJobsChanged += (sender, newMaximumProcessingJobs) =>
            {
                Log.Information(
                    $"'{nameof(AsyncJobScheduler.MaximumProcessingJobs)}' modified ({newMaximumProcessingJobs}).");
            };

            AsyncJobScheduler.JobQueued += (sender, asyncJob) =>
            {
                Log.Information($"Queued new {nameof(AsyncJob)} for completion ({asyncJob.GetType().Name}).");
            };

            Application.logMessageReceived += LogHandler;
        }

        private void Update()
        {
            if (_KillApplication)
            {
                Application.Quit(-1);
            }
        }

        private void OnDestroy()
        {
            Log.CloseAndFlush();
        }

        private void SetupStaticLogger()
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
                .WriteTo.UnityDebugSink(_DEFAULT_TEMPLATE, MinimumLevel)
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

            if (_RuntimeErrorCount > _MAXIMUM_RUNTIME_ERRORS)
            {
                _KillApplication = true;
            }
        }
    }
}
