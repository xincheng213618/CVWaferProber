using ColorVision.Core.Entities;
using CVMysql;
using CVRepositoryLib.Services;

namespace CVDB.Services.Image
{
    public class ImageResultService : CVBaseResultService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(ImageResultService));

        public ImageResultService() : base()
        {
        }

        public static List<VScgdMeasureResultImg> LoadResultByBatchCode(string batchCode)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdMeasureResultImg>().Where(a => a.BatchCode.Equals(batchCode)).ToList();
        }
    }
}
