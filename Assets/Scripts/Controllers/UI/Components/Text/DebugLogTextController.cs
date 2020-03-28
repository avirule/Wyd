#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Serilog.Events;
using TMPro;
using UnityEngine;
using Wyd.Controllers.System;
using Wyd.Controllers.World;
using Wyd.System.Logging.Sinks;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class DebugLogTextController : MonoBehaviour
    {
        private ConcurrentQueue<string> _LogMessageQueue;
        private TextMeshProUGUI _DebugLogText;

        private void Awake()
        {
            _LogMessageQueue = new ConcurrentQueue<string>();

            _DebugLogText = GetComponent<TextMeshProUGUI>();
            _DebugLogText.richText = true;
            _DebugLogText.text = string.Empty;

            CheckSetDebugEventsLogged();

            EventSink.Logged += OnEventLogged;
        }

        private void Update()
        {
            bool worldControllerInstanced = WorldController.Current != null;

            while ((_LogMessageQueue.Count > 0)
                   && worldControllerInstanced
                   && SystemController.Current.IsInSafeFrameTime())
            {
                _LogMessageQueue.TryDequeue(out string result);
                AppendDebugText(result);
            }
        }

        private void AppendDebugText(string text)
        {
            StringBuilder builder = new StringBuilder(_DebugLogText.text);
            builder.Append($"{text}\r\n");

            _DebugLogText.text = builder.ToString();
        }

        private void OnEventLogged(object sender, LogEvent logEvent)
        {
            string finalText = string.Empty;
            string timeStampFormatted = logEvent.Timestamp.ToString("HH:mm:fff");

            switch (logEvent.Level)
            {
                case LogEventLevel.Information:
                case LogEventLevel.Debug:
                    finalText = $"[{timeStampFormatted}] {logEvent.RenderMessage()}";
                    break;
                case LogEventLevel.Warning:
                    finalText =
                        $"<color={MemorySink.WARN_COLOR}>[{timeStampFormatted}]</color> {logEvent.RenderMessage()}";
                    break;
                case LogEventLevel.Error:
                    finalText =
                        $"<color={MemorySink.ERROR_COLOR}>[{timeStampFormatted}]</color> {logEvent.RenderMessage()}";
                    break;
                case LogEventLevel.Fatal:
                    finalText =
                        $"<color={MemorySink.ERROR_COLOR}>[{timeStampFormatted}] {logEvent.RenderMessage()}</color> ";
                    break;
                case LogEventLevel.Verbose:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _LogMessageQueue.Enqueue(finalText);
        }

        private void CheckSetDebugEventsLogged()
        {
            IReadOnlyList<LogEvent> loggedEvents = LogController.LoggedEvents;

            if ((loggedEvents == null) || (loggedEvents.Count <= 0))
            {
                return;
            }

            foreach (LogEvent logEventInfo in loggedEvents.Skip(loggedEvents.Count - 30))
            {
                OnEventLogged(this, logEventInfo);
            }
        }
    }
}
