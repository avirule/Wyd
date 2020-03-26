#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        private const string _DEFAULT_TEMPLATE = "{Timestamp:MM/dd/yy-HH:mm:ss} | {Level:u3} | {Message}";
        private const int _MAXIMUM_RUNTIME_ERRORS = 10;

        private static readonly DateTime _RuntimeErrorsDateTime = DateTime.Now;
        private static string _runtimeErrorsPath;
        private static int _runtimeErrorCount;
        private static bool _killApplication;

        private static MemorySink _loggedDataSink;

        public static IReadOnlyList<LogEvent> LoggedEvents => _loggedDataSink?.LogEvents;

        private void Awake()
        {
            if (_loggedDataSink == null)
            {
                _loggedDataSink = new MemorySink(_DEFAULT_TEMPLATE);
            }

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(_loggedDataSink)
                .WriteTo.Sink<GlobalLogEventSink>()
#if UNITY_EDITOR
                .WriteTo.UnityDebugSink(_DEFAULT_TEMPLATE)
#endif
                .CreateLogger();


            Application.logMessageReceived += LogHandler;
        }

        private void Update()
        {
            if (_killApplication)
            {
                Application.Quit(-1);
            }
        }

        private static void LogHandler(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            if (string.IsNullOrEmpty(_runtimeErrorsPath))
            {
                _runtimeErrorsPath =
                    $@"{Application.persistentDataPath}\logs\runtime-exceptions_{_RuntimeErrorsDateTime:MM-dd-yy_h-mm-ss}.log";
            }

            Log.Fatal(stackTrace);

            // todo make a fatality-specific sink
            File.AppendAllText(_runtimeErrorsPath, stackTrace, Encoding.ASCII);

            Interlocked.Increment(ref _runtimeErrorCount);

            if (_runtimeErrorCount > _MAXIMUM_RUNTIME_ERRORS)
            {
                _killApplication = true;
            }
        }
    }
}
