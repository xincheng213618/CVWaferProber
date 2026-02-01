using CVWaferProber.Config;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using TextBox = System.Windows.Controls.TextBox;

namespace CVWaferProber.Views
{
    /// <summary>
    /// GlobalConfigWindow.xaml 的交互逻辑
    /// </summary>
    /// <summary>
    /// 全局配置窗口
    /// </summary>
    public partial class GlobalConfigWindow : Window
    {
        // 全局配置模型（用于绑定和保存）
        public GlobalConfigModel ConfigModel { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public GlobalConfigWindow()
        {
            InitializeComponent();
            ConfigModel = new GlobalConfigModel();
            LoadSavedConfig();
            this.DataContext = ConfigModel;
            ConfigModel.EnsureDirectoriesExist();
        }

        /// <summary>
        /// 浏览文件夹按钮点击事件
        /// </summary>
        // VAM路径浏览（原有）
        private void BtnVamBrowse_Click(object sender, RoutedEventArgs e)
        {
            // 1. 临时变量存属性值
            string tempPath = ConfigModel.VamExportPath;
            // 2. 传递临时变量的ref
            BrowseFolder("选择VAM自动导出文件夹", ref tempPath, txtVamExportPath);
            // 3. 处理后赋值回属性
            ConfigModel.VamExportPath = tempPath;
        }

        // 新增：AOI路径浏览
        private void BtnAoiBrowse_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = ConfigModel.AoiExportPath;
            BrowseFolder("选择AOI自动导出文件夹", ref tempPath, txtAoiExportPath);
            ConfigModel.AoiExportPath = tempPath;
        
        }

        // 新增：EQE路径浏览
        private void BtnEqeBrowse_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = ConfigModel.EqeExportPath;
            BrowseFolder("选择EQE自动导出文件夹", ref tempPath, txtEqeExportPath);
            ConfigModel.EqeExportPath = tempPath;
        }

        // IVL路径浏览（原有）
        private void BtnIvlBrowse_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = ConfigModel.IvlExportPath;
            BrowseFolder("选择IVL自动导出文件夹", ref tempPath, txtIvlExportPath);
            ConfigModel.IvlExportPath = tempPath;
        }
        /// <summary>
        /// 通用文件夹浏览方法（避免重复代码）
        /// </summary>
        private void BrowseFolder(string description, ref string targetPath, TextBox targetTextBox)
        {
            var folderDialog = new FolderBrowserDialog
            {
                Description = description,
                SelectedPath = targetPath,
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                targetPath = folderDialog.SelectedPath;
                targetTextBox.Text = targetPath;
            }
        }
        /// <summary>
        /// 保存配置按钮点击事件
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfigModel.IsValid())
            {
                MessageBox.Show(
                    (string)Application.Current.FindResource("GlobalConfig.PathInvalid"),
                    (string)Application.Current.FindResource("Prompt"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ConfigModel.EnsureDirectoriesExist();
            SaveConfigToFile();

            MessageBox.Show(
                (string)Application.Current.FindResource("GlobalConfig.SaveSuccess"),
                (string)Application.Current.FindResource("Log.Success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// 确保文件夹存在，不存在则自动创建
        /// </summary>
        private void EnsureDirectoryExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// 加载已保存的配置
        /// </summary>
        private void LoadSavedConfig()
        {
            try
            {
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    string configContent = File.ReadAllText(configPath);
                    var savedConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<GlobalConfigModel>(configContent);
                    if (savedConfig != null)
                    {
                        ConfigModel.VamExportPath = savedConfig.VamExportPath;
                        ConfigModel.AoiExportPath = savedConfig.AoiExportPath;
                        ConfigModel.EqeExportPath = savedConfig.EqeExportPath;
                        ConfigModel.IvlExportPath = savedConfig.IvlExportPath;

                        ConfigModel.IsBreakOnError = ConfigManager.Config.IsBreakOnError;
                        ConfigModel.BreakOnErrorNum = ConfigManager.Config.BreakOnErrorNum;

                        ConfigModel.ConnectionSettings.ServerIP = ConfigManager.Config.ConnectionSettings.ServerIP;
                        ConfigModel.ConnectionSettings.Port = ConfigManager.Config.ConnectionSettings.Port;
                        //
                        ConfigModel.MotionSettings.DefaultXYMotionTimeout = ConfigManager.Config.MotionSettings.DefaultXYMotionTimeout;
                        ConfigModel.MotionSettings.DefaultZMotionTimeout = ConfigManager.Config.MotionSettings.DefaultZMotionTimeout;
                        //
                        ConfigModel.MapSettings.OutsiderRing = ConfigManager.Config.MapSettings.OutsiderRing;
                    }
                }
            }
            catch (Exception ex)
            {
                log4net.LogManager.GetLogger(typeof(GlobalConfigWindow)).Error("加载全局配置失败", ex);
            }
        }

        /// <summary>
        /// 保存配置到本地文件
        /// </summary>
        private void SaveConfigToFile()
        {
            try
            {
                string configPath = GetConfigFilePath();
                // 序列化配置
                string configContent = Newtonsoft.Json.JsonConvert.SerializeObject(ConfigModel, Newtonsoft.Json.Formatting.Indented);
                // 确保配置文件夹存在
                EnsureDirectoryExists(Path.GetDirectoryName(configPath));
                // 写入文件
                File.WriteAllText(configPath, configContent);
                //
                ConfigManager.Config.ConnectionSettings.ServerIP = ConfigModel.ConnectionSettings.ServerIP;
                ConfigManager.Config.ConnectionSettings.Port = ConfigModel.ConnectionSettings.Port;
                //
                ConfigManager.Config.MotionSettings.DefaultXYMotionTimeout = ConfigModel.MotionSettings.DefaultXYMotionTimeout;
                ConfigManager.Config.MotionSettings.DefaultZMotionTimeout = ConfigModel.MotionSettings.DefaultZMotionTimeout;
                //
                ConfigManager.Config.MapSettings.OutsiderRing = ConfigModel.MapSettings.OutsiderRing;
                ConfigManager.Config.IsBreakOnError = ConfigModel.IsBreakOnError;
                ConfigManager.Config.BreakOnErrorNum = ConfigModel.BreakOnErrorNum;

                ConfigManager.Config.ExportPathSettings.AoiExportPath = ConfigModel.AoiExportPath;
                ConfigManager.Config.ExportPathSettings.VamExportPath = ConfigModel.VamExportPath;
                ConfigManager.Config.ExportPathSettings.EqeExportPath = ConfigModel.EqeExportPath;
                ConfigManager.Config.ExportPathSettings.IvlExportPath = ConfigModel.IvlExportPath;
                //
                ConfigManager.SaveConfig();
                //
                ProberClientService.Instance.ReloadSettings();
                //
                ProberClientService.Instance.SetConnectionSettings(ConfigModel.ConnectionSettings.ServerIP, ConfigModel.ConnectionSettings.Port);
                ProberClientService.Instance.ReconnectAsync();

                LogManagerService.ChangeLogLevel(ConfigModel.SelectedLogLevel);
            }
            catch (Exception ex)
            {
                log4net.LogManager.GetLogger(typeof(GlobalConfigWindow)).Error("保存全局配置失败", ex);
                MessageBox.Show("保存配置失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
       
        private string GetConfigFilePath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string configDir = Path.Combine(appDir, "Config");
            return Path.Combine(configDir, "GlobalConfig.json");
        }

    }


}
