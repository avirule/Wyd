#region

using System;
using System.ComponentModel;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Graphics;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class WindowModeTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(Options.Instance.FullScreenMode)))
            {
                _TextObject.text = string.Format(_Format, Enum.GetName(typeof(FullScreenMode), Options.Instance.FullScreenMode));
            }
        }
    }
}
