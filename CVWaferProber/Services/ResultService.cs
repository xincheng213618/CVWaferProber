using CsvHelper;
using CsvHelper.Configuration;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.ViewModels;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace CVWaferProber.Services
{
    public class ResultService
    {
        public static void SaveToCSV(string csvFile, System.Collections.ObjectModel.ObservableCollection<ViewModels.DieViewModel> results)
        {
            var result_csv = new List<ResultCSVModel>();
            foreach (var model in results)
            {
                result_csv.Add(new ResultCSVModel(model));
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",                    // 分隔符
                HasHeaderRecord = true,             // 是否包含表头
                Encoding = Encoding.UTF8,           // 编码格式
                //ShouldQuote = args => true,         // 所有字段都用引号包围
            };
            // 使用自定义映射
            using (var writer = new StreamWriter(csvFile))
            using (var csv = new CsvWriter(writer, config))
            {
                //csv.Context.RegisterClassMap<ResultCSVModelMap>();
                csv.WriteRecords(result_csv);
            }
        }

        public static void LoadFromCSV(string csvFile, ObservableCollection<DieViewModel> results)
        {
            using (var fileStream = new FileStream(csvFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<ResultCSVModel>();

                foreach (var result in results)
                {
                    var rec = records.First( r => r.Row == result.MapY && r.Col == result.MapX);
                    if(rec != null)
                    {
                        result.SerialNumber = rec.SerialNumber;
                        result.StartTestTime = rec.StartTestTime;
                        result.EndTestTime = rec.EndTestTime;
                        result.ChangeStatusOnly(rec.Status.Value);
                        result.TotalTime = rec.TotalTime;
                        result.chipViewModel.ChipData.DataValue = rec.DataValue;
                    }
                }
            }
        }
    }

    public class ResultCSVModel
    {
        public uint Id {  get; set; }
        public int Row {  get; set; }
        public int Col {  get; set; }
        public string? SerialNumber {  get; set; }
        public DateTime? EndTestTime { get; set; }
        public DateTime? StartTestTime { get; set; }
        public double? DataValue { get; set; }
        public string? TotalTime { get; set; }
        public ChipStatus? Status { get; set; }

        public ResultCSVModel() { }

        public ResultCSVModel(ViewModels.DieViewModel dieViewModel)
        {
            this.Id = dieViewModel.Id.Value;
            this.Row = dieViewModel.MapY.Value;
            this.Col = dieViewModel.MapX.Value;
            this.SerialNumber = dieViewModel.SerialNumber;
            this.StartTestTime = dieViewModel.StartTestTime;
            this.EndTestTime = dieViewModel.EndTestTime;
            this.TotalTime = dieViewModel.TotalTime;
            this.Status = dieViewModel.Status;
            this.DataValue = dieViewModel.chipViewModel.DataValue;
        }
    }
    public class ResultCSVModelMap : ClassMap<ResultCSVModel>
    {
        public ResultCSVModelMap()
        {
            Map(m => m.Id).Name("编号").Index(0);
            Map(m => m.Row).Name("行").Index(1);
            Map(m => m.Col).Name("列").Index(2);
            Map(m => m.SerialNumber).Name("序列号").Index(3);
            Map(m => m.Status).Name("状态").Index(4);
            Map(m => m.DataValue).Name("均匀性").Index(5);
        }
    }

}
