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
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.State
{
    public enum CacheCullingAggression
    {
        /// <summary>
        ///     Passive culling will only cull chunks when
        ///     given enough processing time to do so.
        /// </summary>
        Passive = 0,

        /// <summary>
        ///     Active cache culling will force the game to keep
        ///     the total amount of cached chunks at or below
        ///     the maximum
        /// </summary>
        Active = 1
    }

    public class OptionsController : SingletonController<OptionsController>, INotifyPropertyChanged
    {
        public static class Defaults
        {
            // General
            public const bool GPU_ACCELERATION = true;
            public const int MAXIMUM_DIAGNOSTIC_BUFFERS_LENGTH = 600;

            // Graphics
            public const int TARGET_FRAME_RATE = 60;
            public const int VSYNC_LEVEL = 1;
            public const int WINDOW_MODE = (int)WindowMode.Fullscreen;
            public const int RENDER_DISTANCE = 4;
            public const int SHADOW_DISTANCE = 4;

            // Chunking
            public const int PRE_LOAD_CHUNK_DISTANCE = 1;
        }

        public const int MAXIMUM_RENDER_DISTANCE = 32;

        public static string ConfigPath { get; private set; }
        public TimeSpan TargetFrameRateTimeSpan { get; private set; }

        public static readonly WindowMode MaximumWindowModeValue =
            Enum.GetValues(typeof(WindowMode)).Cast<WindowMode>().Last();

        #region PRIVATE FIELDS

        private Configuration _Configuration;
        private bool _GPUAcceleration;
        private int _TargetFrameRate;
        private int _MaximumDiagnosticBuffersSize;
        private int _PreLoadChunkDistance;
        private int _ShadowDistance;
        private int _RenderDistance;
        private WindowMode _WindowMode;

        #endregion


        #region GENERAL OPTIONS MEMBERS

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

        public int MaximumDiagnosticBuffersSize
        {
            get => _MaximumDiagnosticBuffersSize;
            set
            {
                _MaximumDiagnosticBuffersSize = value;
                _Configuration["General"][nameof(MaximumDiagnosticBuffersSize)].IntValue =
                    _MaximumDiagnosticBuffersSize;
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


        #region CHUNKING OPTIONS MEMBERS

        public int PreLoadChunkDistance
        {
            get => _PreLoadChunkDistance;
            set
            {
                _PreLoadChunkDistance = value;
                _Configuration["Chunking"][nameof(PreLoadChunkDistance)].IntValue = _PreLoadChunkDistance;
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
            _Configuration = !File.Exists(ConfigPath)
                ? InitialiseDefaultConfig()
                : Configuration.LoadFromFile(ConfigPath);

            // General

            if (!GetSetting("General", nameof(GPUAcceleration), out _GPUAcceleration))
            {
                LogSettingLoadError(nameof(GPUAcceleration), Defaults.GPU_ACCELERATION);
                GPUAcceleration = Defaults.GPU_ACCELERATION;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(RenderDistance), out _RenderDistance)
                || (RenderDistance < 0)
                || (RenderDistance > 48))
            {
                LogSettingLoadError(nameof(RenderDistance), Defaults.RENDER_DISTANCE);
                RenderDistance = Defaults.RENDER_DISTANCE;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(ShadowDistance), out _ShadowDistance)
                || (ShadowDistance < 0)
                || (ShadowDistance > 48))
            {
                LogSettingLoadError(nameof(ShadowDistance), Defaults.SHADOW_DISTANCE);
                ShadowDistance = Defaults.SHADOW_DISTANCE;
                SaveSettings();
            }

            if (!GetSetting("General", nameof(MaximumDiagnosticBuffersSize), out _MaximumDiagnosticBuffersSize)
                || MaximumDiagnosticBuffersSize < 30
                || MaximumDiagnosticBuffersSize > 6000)
            {
                LogSettingLoadError(nameof(MaximumDiagnosticBuffersSize), Defaults.MAXIMUM_DIAGNOSTIC_BUFFERS_LENGTH);
                MaximumDiagnosticBuffersSize = Defaults.MAXIMUM_DIAGNOSTIC_BUFFERS_LENGTH;
                SaveSettings();
            }

            // Graphics

            if (!GetSetting("Graphics", nameof(TargetFrameRate), out _TargetFrameRate)
                || (TargetFrameRate < 0)
                || (TargetFrameRate > 120))
            {
                LogSettingLoadError(nameof(TargetFrameRate), Defaults.TARGET_FRAME_RATE);
                TargetFrameRate = Defaults.TARGET_FRAME_RATE;
                SaveSettings();
            }

            TargetFrameRateTimeSpan = TimeSpan.FromSeconds(1d / TargetFrameRate);

            if (!GetSetting("Graphics", nameof(VSyncLevel), out int vSyncLevel) || (vSyncLevel < 0) || (vSyncLevel > 4))
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


            // Chunking

            if (!GetSetting("Chunking", nameof(PreLoadChunkDistance), out _PreLoadChunkDistance)
                || (PreLoadChunkDistance < 0))
            {
                LogSettingLoadError(nameof(PreLoadChunkDistance), Defaults.PRE_LOAD_CHUNK_DISTANCE);
                PreLoadChunkDistance = Defaults.PRE_LOAD_CHUNK_DISTANCE;
                SaveSettings();
            }

            Log.Information("Configuration loaded.");
        }

        private Configuration InitialiseDefaultConfig()
        {
            Log.Information("Initializing default configuration file...");

            _Configuration = new Configuration();

            // General

            _Configuration["General"][nameof(GPUAcceleration)].PreComment =
                "Determines whether the GPU will be more heavily utilized to increase overall performance.\r\n"
                + "Turning this off will create more work for the CPU.";
            _Configuration["General"][nameof(GPUAcceleration)].BoolValue = Defaults.GPU_ACCELERATION;

            _Configuration["General"][nameof(ShadowDistance)].PreComment =
                "Defines radius in chunks around player to draw shadows.";
            _Configuration["General"][nameof(ShadowDistance)].Comment = "(min 1, max 48)";
            _Configuration["General"][nameof(ShadowDistance)].IntValue = Defaults.SHADOW_DISTANCE;

            _Configuration["General"][nameof(RenderDistance)].PreComment =
                "Defines radius in regions around player to load chunks.";
            _Configuration["General"][nameof(RenderDistance)].Comment = "(min 1, max 48)";
            _Configuration["General"][nameof(RenderDistance)].IntValue = Defaults.RENDER_DISTANCE;

            // Graphics

            _Configuration["Graphics"][nameof(TargetFrameRate)].PreComment =
                "Minimum number of frames internal systems will target to lapse during updates.";
            _Configuration["Graphics"][nameof(TargetFrameRate)].Comment =
                "Higher values decrease overall CPU stress (min 15, max 120).";
            _Configuration["Graphics"][nameof(TargetFrameRate)].IntValue =
                Defaults.TARGET_FRAME_RATE;

            _Configuration["Graphics"][nameof(VSyncLevel)].PreComment =
                "Each level increases the number of screen updates to wait before rendering to the screen.";
            _Configuration["Graphics"][nameof(VSyncLevel)].Comment = "(0 = Disabled, 1 = Enabled)";
            _Configuration["Graphics"][nameof(VSyncLevel)].IntValue = Defaults.VSYNC_LEVEL;

            _Configuration["Graphics"][nameof(WindowMode)].Comment =
                "(0 = Fullscreen, 1 = BorderlessWindowed, 2 = Windowed)";
            _Configuration["Graphics"][nameof(WindowMode)].IntValue = Defaults.WINDOW_MODE;


            // Chunking

            _Configuration["Chunking"][nameof(PreLoadChunkDistance)].PreComment =
                "Defines radius of chunks to pre-load.\r\n"
                + "This distance begins at the edge of the render distance.";
            _Configuration["Chunking"][nameof(PreLoadChunkDistance)].IntValue =
                Defaults.PRE_LOAD_CHUNK_DISTANCE;


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
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
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
