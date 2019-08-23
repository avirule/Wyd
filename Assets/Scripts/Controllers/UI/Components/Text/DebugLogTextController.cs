#region

using System.Linq;
using System.Text;
using Logging.Targets;
using NLog;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class DebugLogTextController : MonoBehaviour
    {
        private TextMeshProUGUI _DebugLogText;

        private void Awake()
        {
            _DebugLogText = GetComponent<TextMeshProUGUI>();
            _DebugLogText.text = string.Empty;

            CheckSetDebugEventsLogged();

            InGameDebugLogTarget.EventLogged += OnEventLogged;
        }

        private void CheckSetDebugEventsLogged()
        {
            if (InGameDebugLogTarget.DebugEntries.Count > 0)
            {
                // .ToList() to capture copy of list in case of threaded access
                _DebugLogText.text = string.Join("\r\n",
                    InGameDebugLogTarget.DebugEntries.ToList().Select(logEvent => logEvent.Message));
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

            AppendDebugText(finalText);
        }
    }
}