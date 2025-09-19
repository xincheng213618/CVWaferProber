using ColorVision.Core.Entities;
using CVMysql;
using CVRepositoryLib.Services;

namespace CVDB.Services.Algorithm
{
    public class AlgResultService : CVBaseResultService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(AlgResultService));

        public AlgResultService() : base()
        {
        }

        public static List<VScgdAlgorithmResultMaster> LoadAlgResultByBatchCode(string batchCode)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdAlgorithmResultMaster>().Where(a => a.BatchCode.Equals(batchCode)).ToList();
        }
        public static List<TScgdAlgorithmResultDetailPoiCieFile> GetPOIDetailResultFileByPid(int pid)
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdAlgorithmResultDetailPoiCieFile>().Where(a => a.Pid == pid).ToList();
        }
        public static List<TScgdAlgorithmResultDetailCommon> GetCommDetailResult(int pid)
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdAlgorithmResultDetailCommon>().Where(a => a.Pid == pid).ToList();
        }
        public static List<TScgdAlgorithmResultDetailPoiMtf> GetPOIDetailResult(int pid)
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdAlgorithmResultDetailPoiMtf>().Where(a => a.Pid == pid).ToList();
        }
    }
}
