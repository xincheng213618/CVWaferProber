using CVWaferProber.ViewModels;
using CVWPFSpectrometerCtrl.Models;
using CVWPFSpectrometerCtrl.ViewModels;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Utils
{
    public class AutoExportHelper
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(AutoExportHelper));
        private const string _basePath = @"D:\CVTest"; // 根目录
        private static readonly string[] _categories = { "AOI", "IVL", "EQE", "VAM" }; // 分类文件夹

        
        /// <summary>
        /// 初始化所有分类文件夹（不存在则创建）
        /// </summary>
        public static void InitFolders()
        {
            try
            {
                // 创建根目录
                if (!Directory.Exists(_basePath))
                {
                    Directory.CreateDirectory(_basePath);
                    _logger.Info($"Create root directory：{_basePath}");//: "创建根目录")}
                }

                // 创建分类子文件夹
                foreach (var category in _categories)
                {
                    string categoryPath = Path.Combine(_basePath, category);
                    if (!Directory.Exists(categoryPath))
                    {
                        Directory.CreateDirectory(categoryPath);
                        _logger.Info($"Create category folder：{categoryPath}");//: \"创建分类文件夹\")}
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to initialize folder" , ex);//: "初始化文件夹失败")}"
                throw;
            }
        }


        /// <summary>
        /// 按类别导出单个测试项的CSV
        /// </summary>
        /// <param name="category">类别（AOI/IVL/EQE/VAM）</param>
        /// <param name="die">测试数据（DieViewModel）</param>
        /// <param name="csvContent">CSV内容（已拼接好的字符串）</param>
        /// <returns>导出的文件路径</returns>
        public static string ExportCategoryCsv(string category, DieViewModel die, string csvContent)
        {
            try
            {
                // 校验类别合法性
                if (!Array.Exists(_categories, c => c.Equals(category, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException($"Unsupported category：{category}");//: \"不支持的类别\")}
                }

                // 构造文件名（格式：[类别]_[SerialNumber]_[时间戳].csv）
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string fileName = $"{category}_{die.SerialNumber}_{timestamp}.csv";
                string filePath = Path.Combine(_basePath, category, fileName);

                // 写入CSV内容
                File.WriteAllText(filePath, csvContent, Encoding.UTF8);
                _logger.Info($"Export {category} data to: {filePath}" );//:$"导出{category}数据至：{filePath}"
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to export {category} data" , ex);//: $"导出{category}数据失败"
                throw;
            }
        }


        /// <summary>
        /// 更新Summary.csv（追加测试记录）
        /// </summary>
        /// <param name="die">测试数据</param>
        /// <param name="exportedFiles">本次导出的所有文件路径</param>
        public static void UpdateSummary(DieViewModel die, List<string> exportedFiles, TestSummaryData summaryData, SpectrumMeasurement spectrum)
        {
            try
            {
                string summaryPath = Path.Combine(_basePath, "Summary.csv");
                bool isNewFile = !File.Exists(summaryPath);

                // 截图中的表头（按顺序）
                string header = "Die_x,Die_y,IV Selected,IV Selected,AOI Selected,VAM Selected,EQE Selected,Test Status,LightOnStatus,Register,Pixels,Final Class,AOI GradeLevel,Black Patterns (Uniformity ( luminance(nit)),Voltage(v),Current(mA),Dominant Wavelength,Temperature(℃),Pixel Logic,MeasurePin,Pressure,TouchDown Counts,Probing Card ID";

                // 拼接当前记录（按表头字段顺序填充）
                string record = $"{die.MapX}," + // Die_x
                                $"{die.MapY}," + // Die_y
                                $"{summaryData.IVSelected}," + // IV Selected
                                $"{summaryData.IVLSelected}," + // 截图中“IV Selected”重复，这里暂用同一值
                                $"{summaryData.AOISelected}," + // AOI Selected
                                $"{summaryData.VAMSelected}," + // VAM Selected
                                $"{summaryData.EQESelected}," + // EQE Selected
                                $"{die.DisplayStatus}," + // Test Status
                                $"{summaryData.LightOnStatus}," + // LightOnStatus
                                $"{summaryData.Register}," + // Register
                                $"{summaryData.Pixels}," + // Pixels
                                $"{die.FinalClass}," + // Final Class
                                $"{summaryData.AOIGradeLevel}," + // AOI GradeLevel
                                $"{summaryData.BlackPatterns}," + // Black Patterns...
                                $"{spectrum.Voltage}," + // Voltage(v)
                                $"{spectrum.Current}," + // Current(mA)
                                $"{spectrum.Wavelengths}," + // Dominant Wavelength
                                $"{summaryData.Temperature}," + // Temperature(℃)
                                $"{summaryData.PixelLogic}," + // Pixel Logic
                                $"{summaryData.MeasurePin}," + // MeasurePin
                                $"{summaryData.Pressure}," + // Pressure
                                $"{summaryData.TouchDownCounts}," + // TouchDown Counts
                                $"{summaryData.ProbingCardID}"; // Probing Card ID

                // 写入文件
                using (var sw = new StreamWriter(summaryPath, true, Encoding.UTF8))
                {
                    if (isNewFile) sw.WriteLine(header);
                    sw.WriteLine(record);
                }
                _logger.Info( $"Update Summary.csv to: {summaryPath}");// : $"更新Summary.csv至：{summaryPath}"
            }
            catch (Exception ex)
            {
                _logger.Error( "Failed to update Summary.csv", ex);// : "更新Summary.csv失败"
                throw;
            }
        }

        // 新增：封装Summary所需的额外字段
        public class TestSummaryData
        {
            public string IVSelected { get; set; } = "N"; // 默认为N
            public string IVLSelected { get; set; } = "N";
            public string AOISelected { get; set; } = "N";
            public string VAMSelected { get; set; } = "N";
            public string EQESelected { get; set; } = "N";
            public string LightOnStatus { get; set; } = "na";
            public string Register { get; set; } = "na";
            public string Pixels { get; set; } = "na";
            public string AOIGradeLevel { get; set; } = "na";
            public string BlackPatterns { get; set; } = "na";
            public string Temperature { get; set; } = "na";
            public string PixelLogic { get; set; } = "na";
            public string MeasurePin { get; set; } = "na";
            public string Pressure { get; set; } = "na";
            public int TouchDownCounts { get; set; } = 0;
            public string ProbingCardID { get; set; } = "na";
        }
    }
}
