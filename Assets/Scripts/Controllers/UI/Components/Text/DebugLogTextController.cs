#region

using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using NLog;
using TMPro;
using UnityEngine;
using Wyd.Controllers.World;
using Wyd.System.Logging.Targets;

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

            InGameDebugLogTarget.EventLogged += OnEventLogged;
        }

        private void Update()
        {
            bool worldControllerInstanced = WorldController.Current != null;

            while ((_LogMessageQueue.Count > 0)
                   && worldControllerInstanced
                   && WorldController.Current.IsInSafeFrameTime())
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

        private void OnEventLogged(object sender, LogEventInfo logEventInfo)
        {
            string finalText = string.Empty;
            string timeStampFormatted = logEventInfo.TimeStamp.ToString("HH:mm:fff");

            if ((logEventInfo.Level == LogLevel.Info) || (logEventInfo.Level == LogLevel.Debug))
            {
                finalText = $"[{timeStampFormatted}] {logEventInfo.Message}";
            }
            else if (logEventInfo.Level == LogLevel.Warn)
            {
                finalText =
                    $"<color={InGameDebugLogTarget.WARN_COLOR}>[{timeStampFormatted}]</color> {logEventInfo.Message}";
            }
            else if (logEventInfo.Level == LogLevel.Error)
            {
                finalText =
                    $"<color={InGameDebugLogTarget.ERROR_COLOR}>[{timeStampFormatted}]</color> {logEventInfo.Message}";
            }
            else if (logEventInfo.Level == LogLevel.Fatal)
            {
                finalText =
                    $"<color={InGameDebugLogTarget.ERROR_COLOR}>[{timeStampFormatted}] {logEventInfo.Message}</color> ";
            }

            _LogMessageQueue.Enqueue(finalText);
        }

        private void CheckSetDebugEventsLogged()
        {
            if ((InGameDebugLogTarget.DebugEntries == default) || (InGameDebugLogTarget.DebugEntries.Count <= 0))
            {
                return;
            }

            // .ToList() to capture copy of list in case of threaded access
            foreach (LogEventInfo logEventInfo in InGameDebugLogTarget.DebugEntries.Skip(
                InGameDebugLogTarget.DebugEntries.Count - 30))
            {
                OnEventLogged(this, logEventInfo);
            }
        }
    }
}
