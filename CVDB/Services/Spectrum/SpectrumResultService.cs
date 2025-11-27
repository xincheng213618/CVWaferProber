using ColorVision.Core.Entities;
using CVMysql;

namespace CVDB.Services.Spectrum
{
    public class SpectrumResultService
    {
        public static List<VScgdMeasureResultSpectrometer> LoadResultByBatchCode(string deviceCode, string serialNumber)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdMeasureResultSpectrometer>().Where(a => a.DeviceCode == deviceCode && a.BatchCode == serialNumber).ToList();
        }
        // 新增方法：根据设备编码，获取所有有效的序列号（BatchCode）
        public static List<string> GetAllBatchCodesByDeviceCode(string deviceCode)
        {
            // 校验设备编码（避免无效查询）
            if (string.IsNullOrWhiteSpace(deviceCode))
            {
                throw new ArgumentException("设备编码不能为空或空格", nameof(deviceCode));
            }

            // 通过 MySQL 控制器查询：筛选设备编码 + 取序列号 + 去重 + 过滤空值
            return MysqlControler.GetInstance().Sql
                .Select<VScgdMeasureResultSpectrometer>()
                .Where(a => a.DeviceCode == deviceCode)
                .ToList() // 先查全量数据到内存
                .Select(a => a.BatchCode) // 内存中提取序列号
                .Distinct()
                .Where(batchCode => !string.IsNullOrWhiteSpace(batchCode))
                .OrderBy(batchCode => batchCode)
                .ToList();
        }
    }

}
