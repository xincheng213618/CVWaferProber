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
        public static List<VScgdAlgorithmResultMaster> LoadAlgResultByBatchCodeAndType(string batchCode,int algType)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdAlgorithmResultMaster>().Where(a => a.BatchCode.Equals(batchCode) && a.ImgFileType == algType).ToList();
        }
        // 【新增重载】按批次号+多个类型查询主记录（精准筛选相机原图）
        public static List<VScgdAlgorithmResultMaster> LoadAlgResultByBatchCodeAndTypes(string batchCode, List<int> algTypes)
        {
            return MysqlControler.GetInstance().Sql.Select<VScgdAlgorithmResultMaster>()
                   .Where(a => a.BatchCode.Equals(batchCode) && algTypes.Contains(a.ImgFileType)).ToList();
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
        public static List<TScgdAlgorithmResultDetailImage> GetImgDetailResult(int pid)
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdAlgorithmResultDetailImage>().Where(a => a.Pid == pid).ToList();
        }


    }
}
