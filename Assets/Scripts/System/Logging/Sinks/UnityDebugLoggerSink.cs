#region

using System;
using Serilog.Core;
using Serilog.Events;
using UnityEngine;

#endregion

namespace Wyd.System.Logging.Sinks
{
    public class UnityDebugLoggerSink : ILogEventSink
    {
        private readonly string _OutputTemplate;
        private readonly LogEventLevel _MinimumLevel;

        public UnityDebugLoggerSink(string outputTemplate, LogEventLevel minimumLevel = LogEventLevel.Debug) =>
            (_OutputTemplate, _MinimumLevel) = (outputTemplate, minimumLevel);

        public void Emit(LogEvent logEvent)
        {
            if (logEvent.Level < _MinimumLevel)
            {
                return;
            }

            string rendered = string.Format(logEvent.RenderMessage(), _OutputTemplate);

            switch (logEvent.Level)
            {
                case LogEventLevel.Verbose:
                case LogEventLevel.Debug:
                case LogEventLevel.Information:
                    Debug.Log(rendered);
                    break;
                case LogEventLevel.Warning:
                    Debug.LogWarning(rendered);
                    break;
                case LogEventLevel.Error:
                    Debug.LogError(rendered);
                    break;
                case LogEventLevel.Fatal:
                    if (logEvent.Exception == null)
                    {
                        break;
                    }

                    Debug.LogException(logEvent.Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
