using ColorVision.Core.Entities;
using CVMysql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CVDB.Services.Buz
{
    public class WaferProberService
    {
        public static List<TScgdBuzProductDetail> LoadFlows()
        {
            var master = MysqlControler.GetInstance().Sql.Select<TScgdBuzProductMaster>().Where(a => a.BuzType == 10001 && a.IsEnable==1&&a.IsDelete==0).ToOne();
            if (master != null)
            {
               return MysqlControler.GetInstance().Sql.Select<TScgdBuzProductDetail>().Where(a => a.Pid == master.Id).ToList();
            }

            return null;
        }
    }
}
