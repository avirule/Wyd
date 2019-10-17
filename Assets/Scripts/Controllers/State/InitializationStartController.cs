#region

using System;
using System.IO;
using System.Text;
using System.Threading;
using NLog;
using UnityEngine;
using UnityEngine.Scripting;
using Wyd.System.Logging;

#endregion

namespace Wyd.Controllers.State
{
    public class InitializationStartController : MonoBehaviour
    {
        private const int _MAXIMUM_RUNTIME_ERRORS = 10;

        private static readonly DateTime _RuntimeErrorsDateTime = DateTime.Now;
        private static string _runtimeErrorsPath;
        private static int _runtimeErrorCount;
        private static bool _killApplication;

        public static bool LoggerConfigured;

        private void Awake()
        {
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

            if (LoggerConfigured)
                // strip-safe 
            {
                EventLogger.Log(LogLevel.Error, stackTrace);
            }

            File.AppendAllText(_runtimeErrorsPath, stackTrace, Encoding.ASCII);

            Interlocked.Increment(ref _runtimeErrorCount);

            if (_runtimeErrorCount > _MAXIMUM_RUNTIME_ERRORS)
            {
                _killApplication = true;
            }
        }
    }
}
