using ColorVision.Core.Entities;
using CVMysql;
using CVRepositoryLib.Services;
using System.IO;

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
        //public static List<TScgdAlgorithmResultDetailPoiCieFile> GetPOIDetailResultFileByPid(int pid)
        //{
        //    return MysqlControler.GetInstance().Sql.Select<TScgdAlgorithmResultDetailPoiCieFile>().Where(a => a.Pid == pid).ToList();
        //}
        public static List<TScgdAlgorithmResultDetailPoiCieFile> GetPOIDetailResultFileByPid(int pid)
        {
            return MysqlControler.GetInstance().Sql
                .Select<TScgdAlgorithmResultDetailPoiCieFile>()
                .Where(a => a.Pid == pid && a.FileType == 7)
                .ToList();
            //// 源头过滤：剔除所有文件名含po.dat的记录（不区分大小写）
            //.Where(file => !string.IsNullOrEmpty(file.FileUrl)
            //        && !Path.GetFileName(file.FileUrl).ToLower().Contains("po.dat")
            //         || !Path.GetFileName(file.FileUrl).ToLower().Contains("pos.dat"))
            //.ToList();
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
