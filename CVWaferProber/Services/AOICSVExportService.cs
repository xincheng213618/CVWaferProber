using CVDB.Services.Algorithm;
using CVWaferProber.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Services
{
    public class AOICSVExportService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AOICSVExportService));

        public class AOIResultData
        {
            public int No { get; set; }
            public int Die_x { get; set; }
            public int Die_y { get; set; }
            public string LightOnStatus { get; set; } = "na";
            public string RegisterPixels { get; set; } = "na";
            public string FinalClass { get; set; } = "na";
            public string AOIGradeLevel { get; set; } = "na";
            public string BlackPattern { get; set; } = "na";
            public double? Uniformity { get; set; }
            public long? Luminance { get; set; }
            public string Voltage { get; set; } = "na";
            public string Current { get; set; } = "na";
            public int? DominantWavelength { get; set; }
            public int? Temperature { get; set; }
            public string PixelLogic { get; set; } = "na";
            public DateTime MeasurementTime { get; set; }
            public string PinPressure { get; set; } = "";
            public string TouchDownCounts { get; set; } = "";
            public string ProbingCardSN { get; set; } = "";
        }

        /// <summary>
        /// 从DieViewModel和算法结果中提取数据
        /// </summary>
        //private AOIResultData ExtractResultData(DieViewModel dieViewModel, List<AlgResultService> results)
        //{
        //    var data = new AOIResultData
        //    {
        //        No = (int)(dieViewModel.Id + 1), // 从1开始编号
        //        Die_x = dieViewModel.chipViewModel?.ChipData?.Column ?? 0,
        //        Die_y = dieViewModel.chipViewModel?.ChipData?.Row ?? 0,
        //        MeasurementTime = DateTime.Now
        //    };

        //    // 如果芯片数据有坐标信息，使用芯片数据的坐标
        //    if (dieViewModel.chipViewModel?.ChipData != null)
        //    {
        //        data.Die_x = dieViewModel.chipViewModel.ChipData.Column;
        //        data.Die_y = dieViewModel.chipViewModel.ChipData.Row;
        //    }

        //    // 设置默认值
        //    data.LightOnStatus = "OK";
        //    data.RegisterPixels = "OK";
        //    data.PixelLogic = "OK";

        //    // 从算法结果中提取详细信息
        //    if (results != null && results.Count > 0)
        //    {
        //        foreach (var result in results)
        //        {
        //            var aoiDetails = AlgResultService.GetCommDetailResult(result.Id);
        //            if (aoiDetails != null && aoiDetails.Count == 1)
        //            {
        //                var resultJson = aoiDetails[0].Result;
        //                if (!string.IsNullOrEmpty(resultJson))
        //                {
        //                    try
        //                    {
        //                        // 解析 Common 表中的 Result 字段
        //                        var detailResult = JsonConvert.DeserializeObject<AOIService.DetailResult_CommFile_V2>(resultJson);
        //                        if (detailResult != null && !string.IsNullOrEmpty(detailResult.ResultFileName) &&
        //                            File.Exists(detailResult.ResultFileName))
        //                        {
        //                            // 读取并解析 Darkresult.json 文件
        //                            string darkResultJson = File.ReadAllText(detailResult.ResultFileName);
        //                            var darkResult = JsonConvert.DeserializeObject<AOIService.DarkResultDto>(darkResultJson);

        //                            if (darkResult != null)
        //                            {
        //                                data.AOIGradeLevel = darkResult.GradeLevel ?? "na";
        //                                data.BlackPattern = darkResult.GradeLevel ?? "na";

        //                                // 根据GradeLevel设置FinalClass
        //                                if (data.AOIGradeLevel == "A" || data.AOIGradeLevel == "B")
        //                                    data.FinalClass = "A";
        //                                else
        //                                    data.FinalClass = "NG";
        //                            }
        //                        }
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        logger.Error($"解析AOI结果JSON失败: {ex.Message}");
        //                    }
        //                }
        //                break;
        //            }
        //        }
        //    }

        //    // 从DieViewModel获取其他测试结果
        //    if (!string.IsNullOrEmpty(dieViewModel.AOIGradeLevel))
        //        data.AOIGradeLevel = dieViewModel.AOIGradeLevel;

        //    if (!string.IsNullOrEmpty(dieViewModel.BlackPattern))
        //        data.BlackPattern = dieViewModel.BlackPattern;

        //    // 根据GradeLevel确定FinalClass
        //    if (data.AOIGradeLevel == "A" || data.AOIGradeLevel == "B")
        //    {
        //        data.FinalClass = "A";
        //        data.LightOnStatus = "OK";
        //        data.RegisterPixels = "OK";
        //    }
        //    else if (data.AOIGradeLevel == "NG" || data.AOIGradeLevel == "na")
        //    {
        //        data.FinalClass = "NG";
        //        data.LightOnStatus = "NG";
        //        data.RegisterPixels = "NG";
        //    }

        //    return data;
        //}

        /// <summary>
        /// 导出单个Die的CSV结果
        /// </summary>
        //public void ExportDieResult(DieViewModel dieViewModel, string outputDirectory)
        //{
        //    try
        //    {
        //        if (dieViewModel == null || string.IsNullOrEmpty(outputDirectory))
        //            return;

        //        if (!Directory.Exists(outputDirectory))
        //            Directory.CreateDirectory(outputDirectory);

        //        // 获取算法结果
        //        var results = AlgResultService.LoadAlgResultByBatchCode(dieViewModel.SerialNumber!);
        //        var resultData = ExtractResultData(dieViewModel, results);

        //        // 生成文件名：基于时间戳或序列号
        //        string fileName = $"AOI_Result_{DateTime.Now:yyyyMMdd_HHmmss}_{dieViewModel.SerialNumber}.csv";
        //        string filePath = Path.Combine(outputDirectory, fileName);

        //        // 写入CSV文件
        //        WriteCSVFile(filePath, new List<AOIResultData> { resultData });

        //        logger.Info($"AOI结果已导出到: {filePath}");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error($"导出AOI结果失败: {ex.Message}");
        //    }
        //}

        /// <summary>
        /// 批量导出多个Die的CSV结果
        /// </summary>
        //public void ExportBatchResults(List<DieViewModel> dieViewModels, string outputDirectory, string fileName = null)
        //{
        //    try
        //    {
        //        if (dieViewModels == null || dieViewModels.Count == 0 || string.IsNullOrEmpty(outputDirectory))
        //            return;

        //        if (!Directory.Exists(outputDirectory))
        //            Directory.CreateDirectory(outputDirectory);

        //        // 收集所有Die的结果数据
        //        var allResults = new List<AOIResultData>();

        //        foreach (var dieViewModel in dieViewModels.OrderBy(d => d.Id))
        //        {
        //            var results = AlgResultService.LoadAlgResultByBatchCode(dieViewModel.SerialNumber!);
        //            var resultData = ExtractResultData(dieViewModel, results);
        //            allResults.Add(resultData);
        //        }

        //        // 生成文件名
        //        if (string.IsNullOrEmpty(fileName))
        //            fileName = $"AOI_Batch_Result_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        //        string filePath = Path.Combine(outputDirectory, fileName);

        //        // 写入CSV文件
        //        WriteCSVFile(filePath, allResults);

        //        logger.Info($"批量AOI结果已导出到: {filePath}, 共{allResults.Count}条记录");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error($"批量导出AOI结果失败: {ex.Message}");
        //    }
        //}

        /// <summary>
        /// 写入CSV文件
        /// </summary>
        private void WriteCSVFile(string filePath, List<AOIResultData> dataList)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 写入CSV头部
                writer.WriteLine("No,Die_x,Die_y,LightOnStatus,RegisterPixels,Final Class,AOI GradeLevel,Black Pattern,Uniformity,Luminance(nit),Voltage(v),Current(mA),Dominant Wavelength,Temperature,Pixel Logic,Measurement Time,Pin Pressure,TouchDown Counts,Probing Card SN");

                // 写入数据行
                foreach (var data in dataList)
                {
                    var line = new StringBuilder();

                    line.Append($"{data.No},");
                    line.Append($"{data.Die_x},");
                    line.Append($"{data.Die_y},");
                    line.Append($"{data.LightOnStatus},");
                    line.Append($"{data.RegisterPixels},");
                    line.Append($"{data.FinalClass},");
                    line.Append($"{data.AOIGradeLevel},");
                    line.Append($"{data.BlackPattern},");
                    line.Append($"{(data.Uniformity.HasValue ? data.Uniformity.Value.ToString("F2") : "na")},");
                    line.Append($"{(data.Luminance.HasValue ? data.Luminance.Value.ToString() : "na")},");
                    line.Append($"{data.Voltage},");
                    line.Append($"{data.Current},");
                    line.Append($"{(data.DominantWavelength.HasValue ? data.DominantWavelength.Value.ToString() : "na")},");
                    line.Append($"{(data.Temperature.HasValue ? data.Temperature.Value.ToString() : "na")},");
                    line.Append($"{data.PixelLogic},");
                    line.Append($"{data.MeasurementTime:yyyy/MM/dd HH:mm:ss},");
                    line.Append($"{data.PinPressure},");
                    line.Append($"{data.TouchDownCounts},");
                    line.Append($"{data.ProbingCardSN}");

                    writer.WriteLine(line.ToString());
                }
            }
        }

        /// <summary>
        /// 导出完整的AOI.csv格式文件
        /// </summary>
        //public void ExportFullCSV(List<DieViewModel> dieViewModels, string outputPath)
        //{
        //    try
        //    {
        //        if (dieViewModels == null || dieViewModels.Count == 0)
        //            return;

        //        // 创建输出目录
        //        var directory = Path.GetDirectoryName(outputPath);
        //        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        //            Directory.CreateDirectory(directory);

        //        // 收集所有结果
        //        var allResults = new List<AOIResultData>();
        //        int index = 1;

        //        foreach (var dieViewModel in dieViewModels.OrderBy(d => d.Id))
        //        {
        //            var results = AlgResultService.LoadAlgResultByBatchCode(dieViewModel.SerialNumber!);
        //            var resultData = ExtractResultData(dieViewModel, results);
        //            resultData.No = index++;
        //            allResults.Add(resultData);
        //        }

        //        // 写入CSV文件
        //        using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
        //        {
        //            // 写入完整的CSV头部（与示例格式匹配）
        //            writer.WriteLine("No,Die_x,Die_y,LightOnStatus,RegisterPixels,Final Class,AOI GradeLevel,Black Pattern,Uniformity,Luminance(nit),Voltage(v),Current(mA),Dominant Wavelength,Temperature,Pixel Logic,Measurement Time,Pin Pressure,TouchDown Counts,Probing Card SN");

        //            foreach (var data in allResults)
        //            {
        //                writer.WriteLine(
        //                    $"{data.No}," +
        //                    $"{data.Die_x}," +
        //                    $"{data.Die_y}," +
        //                    $"{data.LightOnStatus}," +
        //                    $"{data.RegisterPixels}," +
        //                    $"{data.FinalClass}," +
        //                    $"{data.AOIGradeLevel}," +
        //                    $"{data.BlackPattern}," +
        //                    $"{(data.Uniformity.HasValue ? data.Uniformity.Value.ToString("F2") : "na")}," +
        //                    $"{(data.Luminance.HasValue ? data.Luminance.Value.ToString() : "na")}," +
        //                    $"{data.Voltage}," +
        //                    $"{data.Current}," +
        //                    $"{(data.DominantWavelength.HasValue ? data.DominantWavelength.Value.ToString() : "na")}," +
        //                    $"{(data.Temperature.HasValue ? data.Temperature.Value.ToString() : "na")}," +
        //                    $"{data.PixelLogic}," +
        //                    $"{data.MeasurementTime:yyyy/MM/dd}," +
        //                    $"{data.PinPressure}," +
        //                    $"{data.TouchDownCounts}," +
        //                    $"{data.ProbingCardSN}"
        //                );
        //            }
        //        }

        //        logger.Info($"完整CSV文件已导出到: {outputPath}");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error($"导出完整CSV文件失败: {ex.Message}");
        //        throw;
        //    }
        //}
    }
}
