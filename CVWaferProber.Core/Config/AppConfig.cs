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
        public MotionSettings MotionSettings { get; set; } = new();
        public MappingSettings MapSettings { get; set; } = new();
        public ExportPathSettings ExportPathSettings { get; set; } = new();
        public bool IsBreakOnError { get; set; } = true;
        public int BreakOnErrorNum { get; set; } = 2;
        public bool IsAutoStop { get; set; } = false;
    }

    public class ExportPathSettings
    {
        /// <summary>
        /// VAM自动导出默认路径
        /// </summary>
        public string VamExportPath { get; set; } = @"D:\Project\VAM";
        /// <summary>
        /// AOI导出路径
        /// </summary>
        public string AoiExportPath { get; set; } = @"D:\Project\AOI";

        /// <summary>
        /// EQE导出路径
        /// </summary>
        public string EqeExportPath { get; set; } = @"D:\Project\EQE";

        /// <summary>
        /// IVL导出路径
        /// </summary>
        public string IvlExportPath { get; set; } = @"D:\Project\IVL";
    }
    public class MappingSettings
    {
        public int OutsiderRing { get; set; } = 3;
    }

    public class MotionSettings
    {
        /// <summary>
        /// XY轴超时5秒
        /// </summary>
        public int DefaultXYMotionTimeout { get; set; } = 10;
        /// <summary>
        /// Z轴超时5秒
        /// </summary>
        public int DefaultZMotionTimeout { get; set; } = 5;
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
