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

        public UnityDebugLoggerSink(string outputTemplate) => _OutputTemplate = outputTemplate;

        public void Emit(LogEvent logEvent)
        {
            string rendered = string.Format(logEvent.RenderMessage(), _OutputTemplate);

            switch (logEvent.Level)
            {
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
                case LogEventLevel.Verbose:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
