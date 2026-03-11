using CVWaferProber.Core.Config;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Services;
using Mysqlx.Crud;
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
        private Window owner;

        // 全局配置模型（用于绑定和保存）
        public GlobalConfigModel ConfigModel { get; set; }
        public Window MainWindow { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public GlobalConfigWindow(Window mainWindow)
        {
            InitializeComponent();
            ConfigModel = new GlobalConfigModel();
            LoadSavedConfig();
            this.DataContext = ConfigModel;
            this.Owner = owner;
            ConfigModel.EnsureDirectoriesExist();
            MainWindow = mainWindow;
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
            BrowseFolder("Select VAM Auto Export Folder", ref tempPath, txtVamExportPath);
            // 3. 处理后赋值回属性
            ConfigModel.VamExportPath = tempPath;
        }

        // 新增：AOI路径浏览
        private void BtnAoiBrowse_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = ConfigModel.AoiExportPath;
            BrowseFolder("Select AOI Auto Export Folder", ref tempPath, txtAoiExportPath);
            ConfigModel.AoiExportPath = tempPath;
        
        }

        // 新增：EQE路径浏览
        private void BtnEqeBrowse_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = ConfigModel.EqeExportPath;
            BrowseFolder("Select EQE Auto Export Folder", ref tempPath, txtEqeExportPath);
            ConfigModel.EqeExportPath = tempPath;
        }

        // IVL路径浏览（原有）
        private void BtnIvlBrowse_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = ConfigModel.IvlExportPath;
            BrowseFolder("Select IVL Auto Export Folder", ref tempPath, txtIvlExportPath);
            ConfigModel.IvlExportPath = tempPath;
        }
        private void BtnIvBrowse_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = ConfigModel.IvExportPath;
            BrowseFolder("Select IV Auto Export Folder", ref tempPath, txtIvExportPath);
            ConfigModel.IvExportPath = tempPath;
        }
        private void BtnSummaryBrowse_Click(object sender, RoutedEventArgs e)
        {
            string tempPath = ConfigModel.SummaryExportPath;
            BrowseFolder("Select Summary Auto Export Folder", ref tempPath, txtSummaryExportPath);
            ConfigModel.SummaryExportPath = tempPath;
        }
        /// <summary>
        /// 通用文件夹浏览方法（避免重复代码）
        /// </summary>
        private void BrowseFolder(string description, ref string targetPath, TextBox targetTextBox)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
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
        public void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
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
                        ConfigModel.SummaryExportPath = savedConfig.SummaryExportPath;

                        ConfigModel.IsAutoStop = ConfigManager.Config.IsAutoStop;
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
                log4net.LogManager.GetLogger(typeof(GlobalConfigWindow)).Error("Load Global Configuration Failed", ex);
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
                ConfigManager.Config.IsAutoStop = ConfigModel.IsAutoStop;
                ConfigManager.Config.BreakOnErrorNum = ConfigModel.BreakOnErrorNum;

                ConfigManager.Config.ExportPathSettings.AoiExportPath = ConfigModel.AoiExportPath;
                ConfigManager.Config.ExportPathSettings.VamExportPath = ConfigModel.VamExportPath;
                ConfigManager.Config.ExportPathSettings.EqeExportPath = ConfigModel.EqeExportPath;
                ConfigManager.Config.ExportPathSettings.IvlExportPath = ConfigModel.IvlExportPath;
                ConfigManager.Config.ExportPathSettings.SummaryExportPath = ConfigModel.SummaryExportPath;
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
                log4net.LogManager.GetLogger(typeof(GlobalConfigWindow)).Error("Failed to Save Global Configuration", ex);
                MessageBox.Show($"{FindResource("Failed to Save Configuration")}"+":" + ex.Message, $"{FindResource("State.Error")}", MessageBoxButton.OK, MessageBoxImage.Error);
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
