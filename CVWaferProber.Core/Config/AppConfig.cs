using CVWaferProber.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CVWaferProber.Core.Config
{
    [Serializable]
    public class AppConfig
    {
        public string ApplicationName { get; set; } = "CVWaferProber";
        public string Version { get; set; } = "2026.01.27.0";
        public WindowSettings WindowSettings { get; set; } = new();
        public UserPreferences UserPreferences { get; set; } = new();
        public ConnectionSettings ConnectionSettings { get; set; } = new();
    }

    // WindowSettings.cs
    public class WindowSettings
    {
        public double Width { get; set; } = 800;
        public double Height { get; set; } = 600;
        public double Left { get; set; }
        public double Top { get; set; }
        public WindowState WindowState { get; set; } = WindowState.Normal;
    }

    // UserPreferences.cs
    public class UserPreferences
    {
        public string Theme { get; set; } = "Light";
        public string Language { get; set; } = "zh-CN";
        public int FontSize { get; set; } = 12;
        public bool AutoSave { get; set; } = true;
        public int AutoSaveInterval { get; set; } = 5; // 分钟
    }

}
