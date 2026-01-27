using CVWaferProber.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CVWaferProber.Core.ViewModels
{
    public class GlobalConfigModel
    {
        public ConnectionSettings ConnectionSettings { get; set; } = new();
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

        public GlobalConfigModel()
        {
            //ConnectionSettings = new ConnectionSettings();
        }
        /// <summary>
        /// 验证所有配置路径是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(VamExportPath) &&
                   !string.IsNullOrWhiteSpace(AoiExportPath) &&
                   !string.IsNullOrWhiteSpace(EqeExportPath) &&
                   !string.IsNullOrWhiteSpace(IvlExportPath);
        }

        /// <summary>
        /// 确保所有配置路径对应的文件夹存在
        /// </summary>
        public void EnsureDirectoriesExist()
        {
            CreateDirectoryIfNotExists(VamExportPath);
            CreateDirectoryIfNotExists(AoiExportPath);
            CreateDirectoryIfNotExists(EqeExportPath);
            CreateDirectoryIfNotExists(IvlExportPath);
        }

        /// <summary>
        /// 工具方法：创建文件夹（避免重复代码）
        /// </summary>
        private void CreateDirectoryIfNotExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                log4net.LogManager.GetLogger(typeof(GlobalConfigModel)).Info($"{(string)Application.Current.FindResource("Createdexportdirectory")}：{path}");
            }
        }
    }
}
