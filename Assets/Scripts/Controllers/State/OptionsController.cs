#region

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using Serilog;
using SharpConfig;
using UnityEngine;
using Wyd.Graphics;

#endregion

namespace Wyd.Controllers.State
{
    public class OptionsController : SingletonController<OptionsController>, INotifyPropertyChanged
    {
        public static class Defaults
        {
            // General
            // ReSharper disable once InconsistentNaming
            public static readonly int MAXIMUM_CONCURRENT_JOBS = Environment.ProcessorCount - 2;
            public const bool VERBOSE_LOGGING = false;
            public const bool GPU_ACCELERATION = true;
            public const int DIAGNOSTIC_BUFFER_LENGTH = 600;
            public const int RENDER_DISTANCE = 4;
            public const int SHADOW_DISTANCE = 4;

            // Graphics
            // ReSharper disable once InconsistentNaming
            public static readonly int TARGET_FRAME_RATE = (int)(Screen.currentResolution.refreshRate * 1.5f);
            public const int VSYNC_LEVEL = 1;
            public const int WINDOW_MODE = (int)WindowMode.Fullscreen;
        }

        public const int MAXIMUM_RENDER_DISTANCE = 16;

        public static string ConfigPath { get; private set; }
        public TimeSpan TargetFrameRateTimeSpan { get; private set; }

        public static readonly WindowMode MaximumWindowModeValue = Enum.GetValues(typeof(WindowMode)).Cast<WindowMode>().Last();


        #region PRIVATE FIELDS

        private Configuration _Configuration;
        private int _MaximumConcurrentJobs;
        private bool _GPUAcceleration;
        private int _TargetFrameRate;
        private int _DiagnosticBufferLength;
        private int _ShadowDistance;
        private int _RenderDistance;
        private WindowMode _WindowMode;
        private bool _VerboseLogging;

        #endregion


        #region GENERAL OPTIONS MEMBERS

        public int MaximumConcurrentJobs
        {
            get => _MaximumConcurrentJobs;
            set
            {
                _MaximumConcurrentJobs = value;
                _Configuration["General"][nameof(MaximumConcurrentJobs)].IntValue = _MaximumConcurrentJobs;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public bool GPUAcceleration
        {
            get => _GPUAcceleration;
            set
            {
                _GPUAcceleration = value;
                _Configuration["General"][nameof(GPUAcceleration)].BoolValue = _GPUAcceleration;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public bool VerboseLogging
        {
            get => _VerboseLogging;
            set
            {
                _VerboseLogging = value;
                _Configuration["General"][nameof(VerboseLogging)].BoolValue = _VerboseLogging;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int RenderDistance
        {
            get => _RenderDistance;
            set
            {
                _RenderDistance = value % (MAXIMUM_RENDER_DISTANCE + 1);
                _Configuration["General"][nameof(RenderDistance)].IntValue = _RenderDistance;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int ShadowDistance
        {
            get => _ShadowDistance;
            set
            {
                _ShadowDistance = value % (MAXIMUM_RENDER_DISTANCE + 1);
                _Configuration["General"][nameof(ShadowDistance)].IntValue = _ShadowDistance;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public int DiagnosticBufferLength
        {
            get => _DiagnosticBufferLength;
            set
            {
                _DiagnosticBufferLength = value;
                _Configuration["General"][nameof(DiagnosticBufferLength)].IntValue =
                    _DiagnosticBufferLength;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        #endregion


        #region GRAPHICS OPTIONS MEMBERS

        public int TargetFrameRate
        {
            get => _TargetFrameRate;
            set
            {
                _TargetFrameRate = value;
                _Configuration["Graphics"][nameof(TargetFrameRate)].IntValue = _TargetFrameRate;
                SaveSettings();
                OnPropertyChanged();
            }
        }


        public int VSyncLevel
        {
            get => QualitySettings.vSyncCount;
            set
            {
                QualitySettings.vSyncCount = value;
                _Configuration["Graphics"][nameof(VSyncLevel)].IntValue = QualitySettings.vSyncCount;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        public WindowMode WindowMode
        {
            get => _WindowMode;
            set
            {
                if (_WindowMode == value)
                {
                    return;
                }

                _WindowMode = value;

                switch (_WindowMode)
                {
                    case WindowMode.Fullscreen:
                        Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                        break;
                    case WindowMode.BorderlessWindowed:
                        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                        break;
                    case WindowMode.Windowed:
                        Screen.fullScreenMode = FullScreenMode.Windowed;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }

                _Configuration["Graphics"][nameof(WindowMode)].IntValue = (int)_WindowMode;
                SaveSettings();
                OnPropertyChanged();
            }
        }

        #endregion


        public event PropertyChangedEventHandler PropertyChanged;

        private void Awake()
        {
            AssignSingletonInstance(this);

            ConfigPath = $@"{Application.persistentDataPath}/config.ini";
        }

        private void Start()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            _Configuration = File.Exists(ConfigPath)
                ? Configuration.LoadFromFile(ConfigPath)
                : InitialiseDefaultConfig();

            // General

            if (!GetSetting("General", nameof(MaximumConcurrentJobs), out _MaximumConcurrentJobs)
                || (MaximumConcurrentJobs > Environment.ProcessorCount)
                || (MaximumConcurrentJobs < 1))
            {
                LogSettingLoadError(nameof(MaximumConcurrentJobs), Defaults.MAXIMUM_CONCURRENT_JOBS);
                MaximumConcurrentJobs = Defaults.MAXIMUM_CONCURRENT_JOBS;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(GPUAcceleration), out _GPUAcceleration))
            {
                LogSettingLoadError(nameof(GPUAcceleration), Defaults.GPU_ACCELERATION);
                GPUAcceleration = Defaults.GPU_ACCELERATION;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(RenderDistance), out _RenderDistance)
                || (RenderDistance < 0)
                || (RenderDistance > MAXIMUM_RENDER_DISTANCE))
            {
                LogSettingLoadError(nameof(RenderDistance), Defaults.RENDER_DISTANCE);
                RenderDistance = Defaults.RENDER_DISTANCE;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(ShadowDistance), out _ShadowDistance)
                || (ShadowDistance < 0)
                || (ShadowDistance > MAXIMUM_RENDER_DISTANCE))
            {
                LogSettingLoadError(nameof(ShadowDistance), Defaults.SHADOW_DISTANCE);
                ShadowDistance = Defaults.SHADOW_DISTANCE;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(DiagnosticBufferLength), out _DiagnosticBufferLength)
                || (DiagnosticBufferLength < 30)
                || (DiagnosticBufferLength > 6000))
            {
                LogSettingLoadError(nameof(DiagnosticBufferLength), Defaults.DIAGNOSTIC_BUFFER_LENGTH);
                DiagnosticBufferLength = Defaults.DIAGNOSTIC_BUFFER_LENGTH;
                SaveSettings();
            }

            // Graphics

            if (!GetSetting("Graphics", nameof(TargetFrameRate), out _TargetFrameRate)
                || (TargetFrameRate == 0)
                || (TargetFrameRate < -1)
                || (TargetFrameRate > 300))
            {
                LogSettingLoadError(nameof(TargetFrameRate), Defaults.TARGET_FRAME_RATE);
                TargetFrameRate = Defaults.TARGET_FRAME_RATE;
                SaveSettings();
            } else if (TargetFrameRate == -1)
            {
                TargetFrameRate = (int)(Screen.currentResolution.refreshRate * 1.5f);
            }

            TargetFrameRateTimeSpan = TimeSpan.FromSeconds(1d / TargetFrameRate);

            if (!GetSetting("Graphics", nameof(VSyncLevel), out int vSyncLevel) || (vSyncLevel < 0) || (vSyncLevel > 1))
            {
                LogSettingLoadError(nameof(vSyncLevel), Defaults.VSYNC_LEVEL);
                VSyncLevel = Defaults.VSYNC_LEVEL;
                SaveSettings();
            }
            else
            {
                VSyncLevel = vSyncLevel;
            }

            if (!GetSetting("Graphics", nameof(WindowMode), out int windowMode)
                || (windowMode < 0)
                || (windowMode > (int)MaximumWindowModeValue))
            {
                LogSettingLoadError(nameof(WindowMode), Defaults.WINDOW_MODE);
                WindowMode = Defaults.WINDOW_MODE;
                SaveSettings();
            }
            else
            {
                WindowMode = (WindowMode)windowMode;
            }

            Log.Information("Configuration loaded.");
        }

        private Configuration InitialiseDefaultConfig()
        {
            Log.Information("Initializing default configuration file...");

            _Configuration = new Configuration();

            // General

            _Configuration["General"][nameof(MaximumConcurrentJobs)].PreComment =
                "Maximum number of jobs that can run concurrently.\r\n"
                + "Note: absolute maximum number is equal to your logical core count. On most machines, this will be (core count * 2).";
            _Configuration["General"][nameof(MaximumConcurrentJobs)].IntValue = Defaults.MAXIMUM_CONCURRENT_JOBS;

            _Configuration["General"][nameof(GPUAcceleration)].PreComment =
                "Determines whether the GPU will be used for operations other than rendering.\r\n"
                + "Note: disabling this will notably increase CPU stress.";
            _Configuration["General"][nameof(GPUAcceleration)].BoolValue = Defaults.GPU_ACCELERATION;

            _Configuration["General"][nameof(VerboseLogging)].BoolValue = Defaults.VERBOSE_LOGGING;

            _Configuration["General"][nameof(ShadowDistance)].PreComment =
                "Defines radius in chunks around player to draw shadows.";
            _Configuration["General"][nameof(ShadowDistance)].Comment = $"(min 1, max {MAXIMUM_RENDER_DISTANCE})";
            _Configuration["General"][nameof(ShadowDistance)].IntValue = Defaults.SHADOW_DISTANCE;

            _Configuration["General"][nameof(RenderDistance)].PreComment =
                "Defines radius in regions around player to load chunks.";
            _Configuration["General"][nameof(RenderDistance)].Comment = $"(min 1, max {MAXIMUM_RENDER_DISTANCE})";
            _Configuration["General"][nameof(RenderDistance)].IntValue = Defaults.RENDER_DISTANCE;

            _Configuration["General"][nameof(DiagnosticBufferLength)].PreComment =
                "Determines maximum length of internal buffers used to allocate diagnostic data.";
            _Configuration["General"][nameof(DiagnosticBufferLength)].Comment = "(min 30, max 6000)";
            _Configuration["General"][nameof(DiagnosticBufferLength)].IntValue = Defaults.DIAGNOSTIC_BUFFER_LENGTH;


            // Graphics

            _Configuration["Graphics"][nameof(TargetFrameRate)].PreComment =
                "Target FPS internal updaters will attempt to maintain.\r\n"
                + "Note: this is a soft limitation. Some operations will blatantly exceed the internal time constraint.";
            _Configuration["Graphics"][nameof(TargetFrameRate)].Comment =
                "Higher values decrease overall CPU stress (min 1 or -1 (not 0), max 300).";
            _Configuration["Graphics"][nameof(TargetFrameRate)].IntValue = Defaults.TARGET_FRAME_RATE;

            _Configuration["Graphics"][nameof(VSyncLevel)].PreComment =
                "When enabled, internal update loop will sleep until enough time has passed to synchronize with monitor framerate.\r\n"
                + "Note: this introduces one frame of delay.";
            _Configuration["Graphics"][nameof(VSyncLevel)].Comment = "(0 = Disabled, 1 = Enabled)";
            _Configuration["Graphics"][nameof(VSyncLevel)].IntValue = Defaults.VSYNC_LEVEL;

            _Configuration["Graphics"][nameof(WindowMode)].Comment =
                "(0 = Fullscreen, 1 = BorderlessWindowed, 2 = Windowed)";
            _Configuration["Graphics"][nameof(WindowMode)].IntValue = Defaults.WINDOW_MODE;


            Log.Information("Default configuration initialized. Saving...");

            _Configuration.SaveToFile(ConfigPath);

            Log.Information($"Configuration file saved at: {ConfigPath}");

            return _Configuration;
        }

        private bool GetSetting<T>(string section, string setting, out T value)
        {
            try
            {
                value = _Configuration[section][setting].GetValue<T>();
            }
            catch (Exception)
            {
                value = default;
                return false;
            }

            return true;
        }

        private static void LogSettingLoadError(string settingName, object defaultValue)
        {
            Log.Warning($"Error loading setting `{settingName}`, defaulting to {defaultValue}.");
        }


        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region PUBLIC METHODS

        public void SaveSettings()
        {
            _Configuration.SaveToFile(ConfigPath, Encoding.ASCII);
        }

        #endregion
    }
}
