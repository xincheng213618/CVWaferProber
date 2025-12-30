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
    }
}
