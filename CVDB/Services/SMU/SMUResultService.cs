using ColorVision.Core.Entities;
using CVMysql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVDB.Services.SMU
{
    public class SMUResultService
    {
        public static List<VScgdMeasureResultSmu> LoadResultByBatchCode(string deviceCode, string serialNumber,int zindex = -1)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdMeasureResultSmu>().Where(a => a.DeviceCode == deviceCode && a.BatchCode == serialNumber && a.ZIndex == zindex).ToList();
        }
    }
}
