using CVWaferProber.Core.ViewModels;
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
            // 初始化配置模型，默认路径设为D:\Project\VAM
            ConfigModel = new GlobalConfigModel
            {
                VamExportPath = @"D:\Project\VAM"
            };

            // 加载已保存的配置（若存在）
            LoadSavedConfig();

            // 绑定数据上下文
            this.DataContext = ConfigModel;

            // 自动创建默认文件夹（若不存在）
            EnsureDirectoryExists(ConfigModel.VamExportPath);
        }

        /// <summary>
        /// 浏览文件夹按钮点击事件
        /// </summary>
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            // 使用文件夹选择对话框（替代OpenFileDialog，专门用于选择文件夹）
            var folderDialog = new FolderBrowserDialog
            {
                Description = "选择VAM自动导出文件夹",
                SelectedPath = ConfigModel.VamExportPath, // 默认选中当前路径
                ShowNewFolderButton = true // 允许创建新文件夹
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ConfigModel.VamExportPath = folderDialog.SelectedPath;
                txtVamExportPath.Text = ConfigModel.VamExportPath;
            }
        }

        /// <summary>
        /// 保存配置按钮点击事件
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 校验路径有效性
            if (string.IsNullOrWhiteSpace(ConfigModel.VamExportPath) || !Directory.Exists(ConfigModel.VamExportPath))
            {
                MessageBox.Show(
                    (string)Application.Current.FindResource("GlobalConfig.PathInvalid"),
                    (string)Application.Current.FindResource("Prompt"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // 保存配置（此处简化为保存到本地文件，可扩展为注册表/数据库）
            SaveConfigToFile();

            // 提示保存成功
            MessageBox.Show(
                (string)System.Windows.Application.Current.FindResource("GlobalConfig.SaveSuccess"),
                (string)Application.Current.FindResource("Log.Success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 关闭窗口
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
                // 配置文件路径（程序目录下的GlobalConfig.json）
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    string configContent = File.ReadAllText(configPath);
                    // 反序列化配置（此处可使用Newtonsoft.Json，与项目现有序列化一致）
                    var savedConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<GlobalConfigModel>(configContent);
                    if (savedConfig != null && !string.IsNullOrWhiteSpace(savedConfig.VamExportPath))
                    {
                        ConfigModel.VamExportPath = savedConfig.VamExportPath;
                        EnsureDirectoryExists(ConfigModel.VamExportPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // 加载失败使用默认路径，不抛出异常
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
