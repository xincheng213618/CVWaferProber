using ColorVision.Core.Entities;
using CVMysql;

namespace CVDB.Services.Buz
{
    public class WaferProberDBService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(WaferProberDBService));
        public static List<TScgdBuzProductDetail> LoadBuzFlows()
        {
            try
            {
                var master = MysqlControler.GetInstance().Sql.Select<TScgdBuzProductMaster>().Where(a => a.BuzType == 10001 && a.IsEnable == 1 && a.IsDelete == 0).ToOne();
                if (master != null)
                {
                    return MysqlControler.GetInstance().Sql.Select<TScgdBuzProductDetail>().Where(a => a.Pid == master.Id).ToList();
                }
            }
            catch (Exception e)
            {
                logger.ErrorFormat("Mysql Exception => {0}", e.Message);
            }

            return null;
        }

        public static List<VScgdSysResourceValidAll> LoadAllFlows()
        {
            try
            {
                return MysqlControler.GetInstance().Sql.Select<VScgdSysResourceValidAll>().Where(a => a.Type == 21 && a.IsEnable == 1 && a.IsDelete == 0).ToList();
            }
            catch (Exception e)
            {
                logger.ErrorFormat("Mysql Exception => {0}", e.Message);
            }

            return null;
        }

        public static void InitBuzWaferProber_10001()
        {
            var master = MysqlControler.GetInstance().Sql.Select<TScgdBuzProductMaster>().Where(a => a.BuzType == 10001 && a.IsEnable == 1 && a.IsDelete == 0).ToOne();
            if (master == null)
            {
                master = new TScgdBuzProductMaster() { BuzType = 10001, Code = "WaferProber.Flow", Name = "晶圆探针台流程", IsEnable = 1, IsDelete = 0 };
                long id = MysqlControler.GetInstance().Sql.Insert(master).ExecuteIdentity();
                if (id > 0)
                {
                    master.Id = (int)id;
                }
            }
            if (master.Id > 0)
            {
                var buzDetails = MysqlControler.GetInstance().Sql.Select<TScgdBuzProductDetail>().Where(a => a.Pid == master.Id).ToList();
                if (buzDetails == null || buzDetails.Count == 0)
                {
                    TScgdBuzProductDetail buzProductDetail = new TScgdBuzProductDetail() { Pid = master.Id, Code= "Flow.AOI" };
                    MysqlControler.GetInstance().Sql.Insert(buzProductDetail).ExecuteAffrows();
                    buzProductDetail = new TScgdBuzProductDetail() { Pid = master.Id, Code = "Flow.IVL.SP" };
                    MysqlControler.GetInstance().Sql.Insert(buzProductDetail).ExecuteAffrows();
                    buzProductDetail = new TScgdBuzProductDetail() { Pid = master.Id, Code = "Flow.IVL.Camera" };
                    MysqlControler.GetInstance().Sql.Insert(buzProductDetail).ExecuteAffrows();
                    buzProductDetail = new TScgdBuzProductDetail() { Pid = master.Id, Code = "Flow.EQE" };
                    MysqlControler.GetInstance().Sql.Insert(buzProductDetail).ExecuteAffrows();
                    buzProductDetail = new TScgdBuzProductDetail() { Pid = master.Id, Code = "Flow.VAM" };
                    MysqlControler.GetInstance().Sql.Insert(buzProductDetail).ExecuteAffrows();
                }
                else
                {
                    foreach (var buzProductDetail in buzDetails)
                    {
                        buzProductDetail.Name = null;
                        MysqlControler.GetInstance().Sql.Update<TScgdBuzProductDetail>().SetSource(buzProductDetail).ExecuteAffrows();
                    }
                }
            }
        }

        public static TScgdBuzProductDetail GetBuzDetail(int id)
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdBuzProductDetail>().Where(a => a.Id == id).ToOne();
        }

        public static void UpdateBuzDetail(TScgdBuzProductDetail buzProductDetail)
        {
            MysqlControler.GetInstance().Sql.Update<TScgdBuzProductDetail>().SetSource(buzProductDetail).ExecuteAffrows();
        }
    }
}
