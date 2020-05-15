#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Serilog.Events;
using TMPro;
using UnityEngine;
using Wyd.Controllers.System;
using Wyd.Logging.Sinks;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class DebugLogTextController : MonoBehaviour, IPerFrameIncrementalUpdate
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

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(200, this);
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(200, this);
        }

        public void FrameUpdate() { }

        public IEnumerable IncrementalFrameUpdate()
        {
            // while ((_LogMessageQueue.Count > 0) && (WorldController.Current != null))
            // {
            //     _LogMessageQueue.TryDequeue(out string result);
            //     AppendDebugText(result);
            //
            //     yield return null;
            // }

            yield return null;
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
                case LogEventLevel.Verbose:
                case LogEventLevel.Debug:
                    break;
                case LogEventLevel.Information:
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
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _LogMessageQueue.Enqueue(finalText);
        }

        private void CheckSetDebugEventsLogged()
        {
            IReadOnlyList<LogEvent> loggedEvents = StaticLog.LoggedEvents;

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
