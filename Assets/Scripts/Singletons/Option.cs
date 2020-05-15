#region

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using Serilog;
using SharpConfig;

#endregion

namespace Wyd.Singletons
{
    public class Option<T> : INotifyPropertyChanged
    {
        private readonly Configuration _Configuration;

        private T _Value;

        public string Category { get; }
        public string Name { get; }
        public Func<T, bool> ChooseUseDefaultProperty { get; }
        public T DefaultValue { get; }

        public T Value
        {
            get => _Value;
            set
            {
                if (ChooseUseDefaultProperty(value))
                {
                    _Value = value;
                    _Configuration[Category][Name].SetValue(_Value);
                    _Configuration.SaveToFile(Options.DefaultConfigPath, Encoding.ASCII);
                    OnPropertyChanged();
                }
                else
                {
                    throw new Exception("Failed to apply new value.");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Option(Configuration configuration, string category, string name, T defaultValue, Func<T, bool> chooseUseDefaultProperty,
            PropertyChangedEventHandler propertyChangedEventHandler)
        {
            _Configuration = configuration ?? throw new NullReferenceException(nameof(configuration));

            Category = category;
            Name = name;
            ChooseUseDefaultProperty = chooseUseDefaultProperty;
            DefaultValue = defaultValue;

            PropertyChanged += propertyChangedEventHandler;

            GetPropertyValue();
        }

        private void GetPropertyValue()
        {
            try
            {
                T value = _Configuration[Category][Name].GetValue<T>();

                if (!ChooseUseDefaultProperty(value))
                {
                    Log.Error($"({nameof(Options)}) Failed '{Category}/{Name}': -predicate-");
                    Value = DefaultValue;
                }
                else
                {
                    Value = value;
                    Log.Information($"({nameof(Options)}) Success '{Category}/{Name}': {Value}");
                }
            }
            catch (SettingValueCastException)
            {
                Log.Error($"({nameof(Options)}) Failed '{Category}/{Name}': {nameof(SettingValueCastException)}");
                Value = DefaultValue;
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
