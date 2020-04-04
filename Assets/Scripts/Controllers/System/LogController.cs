#region

using System.Collections.Generic;
using System.Threading;
using Serilog;
using Serilog.Events;
using UnityEngine;
using Wyd.System.Logging;
using Wyd.System.Logging.Sinks;

#endregion

namespace Wyd.Controllers.System
{
    public class LogController : MonoBehaviour
    {
        private const string _DEFAULT_TEMPLATE = "{Timestamp:MM/dd/yy-HH:mm:ss} | {Level:u3} | {Message}\r\n";
        private const int _MAXIMUM_RUNTIME_ERRORS = 10;

        private static string _logPath;
        private static int _runtimeErrorCount;
        private static bool _killApplication;
        private static List<LogEvent> _logEvents;

        public static IReadOnlyList<LogEvent> LoggedEvents => _logEvents;

        public LogEventLevel MinimumLevel;

        private void Awake()
        {
            _logPath = $@"{Application.persistentDataPath}\logs\";
            _logEvents = new List<LogEvent>();

            SetupStaticLogger();

            Application.logMessageReceived += LogHandler;
        }

        private void Update()
        {
            if (_killApplication)
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
                        $@"{_logPath}\runtime-verbose_.log",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: _DEFAULT_TEMPLATE,
                        retainedFileCountLimit: 31,
                        rollOnFileSizeLimit: true))
                // default log output
                .WriteTo.Async(configuration =>
                    configuration.File(
                        $@"{_logPath}\runtime_.log",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: _DEFAULT_TEMPLATE,
                        retainedFileCountLimit: 31,
                        rollOnFileSizeLimit: true,
                        restrictedToMinimumLevel: LogEventLevel.Information))
                // error log output
                .WriteTo.Async(configuration =>
                    configuration.File(
                        $@"{_logPath}\runtime-error_.log",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: _DEFAULT_TEMPLATE,
                        retainedFileCountLimit: 31,
                        rollOnFileSizeLimit: true,
                        restrictedToMinimumLevel: LogEventLevel.Error))
#if UNITY_EDITOR
                .WriteTo.UnityDebugSink(_DEFAULT_TEMPLATE, MinimumLevel)
#endif
                .WriteTo.MemorySink(ref _logEvents)
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

            Interlocked.Increment(ref _runtimeErrorCount);

            if (_runtimeErrorCount > _MAXIMUM_RUNTIME_ERRORS)
            {
                _killApplication = true;
            }
        }
    }
}
