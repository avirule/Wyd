#region

using System.Collections.Generic;
using System.Threading;
using Serilog;
using Serilog.Events;
using UnityEngine;
using Wyd.System.Jobs;
using Wyd.System.Logging;

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

        public LogEventLevel MinimumLevel;

        private void Awake()
        {
            _LogPath = $@"{Application.persistentDataPath}\logs\";
            _LogEvents = new List<LogEvent>();

            SetupStaticLogger();

            AsyncJobScheduler.Logged += OnAsyncJobSchedulerLogged;
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
                // verbose log output
                .WriteTo.Async(configuration =>
                    configuration.File(
                        $@"{_LogPath}\verbose\runtime-verbose_.log",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: _DEFAULT_TEMPLATE,
                        retainedFileCountLimit: 31,
                        rollOnFileSizeLimit: true))
                // default log output
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

        private static void OnAsyncJobSchedulerLogged(object sender, AsyncJobSchedulerLogEventArgs args)
        {
            Log.Write((LogEventLevel)args.LogLevel, args.Text);
        }
    }
}
