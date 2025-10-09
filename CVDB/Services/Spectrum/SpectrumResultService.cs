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
    }
}
