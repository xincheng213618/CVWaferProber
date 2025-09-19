using ColorVision.Core.Entities;
using CVMysql;
using CVRepositoryLib.Factory;
using System.Data;

namespace CVRepositoryLib.Services.Measure
{
    public class MeasureBatchService
    {
        /// <summary>
        /// 获取Batch ID，不存在则新建
        /// </summary>
        /// <param name="serialNumber"> 序列号</param>
        /// <param name="archivedFlag"> 归档状态,-1:不归档，0:待归档，1:已归档，-2:归档失败 </param>
        /// <returns></returns>
        public static int GetBatchId(string serialNumber, short archivedFlag = 0)
        {
            int batchId = -1;
            if (!string.IsNullOrEmpty(serialNumber))
            {
                TScgdMeasureBatch batchModel = GetByCode(serialNumber);
                if (batchModel == null)
                {
                    string err = string.Empty;
                    batchModel = MeasureEntitiesFactory.CreateMeasureBatch(serialNumber, archivedFlag);
                    if (GetOrNewBatchId(ref batchModel, ref err) > 0)
                    {
                        batchId = batchModel.Id;
                    }
                }
                else
                {
                    batchId = batchModel.Id;
                }
            }
            return batchId;
        }
        public static int GetOrNewBatchId(ref TScgdMeasureBatch result, ref string err)
        {
            string code = result.Code;
            int iR = Insert(result,ref err);
            if (iR > 0)
            {
                result.Id = result.Id;
                return 1;
            }
            else
            {
                // 记录已存在，查询已存在的记录
                var oldRecord = MysqlControler.GetInstance().Sql.Select<TScgdMeasureBatch>().Where(a => a.Code == code).Limit(1).ToOne();
                if (oldRecord != null)
                {
                    result = oldRecord;
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
        public static int Insert(TScgdMeasureBatch result, ref string err)
        {
            long id = 0;
            try
            {
                id = MysqlControler.GetInstance().Sql.Insert(result).ExecuteIdentity();   // 返回主键ID;
            }
            catch (System.Exception e)
            {
                err = e.Message;
            }

            if (id > 0)
            {
                result.Id = (int)id;
                return 1;
            }
            else
            {
                return 0;
            }
        }
        public static int Update(TScgdMeasureBatch result)
        {
            return MysqlControler.GetInstance().Sql.Update<TScgdMeasureBatch>().SetSource(result).ExecuteAffrows();
        }

        public static TScgdMeasureBatch GetByCode(string serialNumber)
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdMeasureBatch>().Where(a => a.Code == serialNumber).Limit(1).ToOne();
        }

        public static int Save(TScgdMeasureBatch result)
        {
            string err = string.Empty;
            if (result.Id > 0) return Update(result);
            else return Insert(result, ref err);
        }

        public static List<TScgdMeasureBatch> SelectAllSuccess()
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdMeasureBatch>().Where(a => (a.ResultCode == 0) && (a.ArchivedFlag == 0)).ToList();
        }

        public static List<TScgdMeasureBatch> GetByName(string name)
        {
            return MysqlControler.GetInstance().Sql.Select<TScgdMeasureBatch>().Where(a => a.Name == name).ToList();
        }

        public static int GetAllCount()
        {
            DataSet result = MysqlControler.GetInstance().Sql.Ado.ExecuteDataSet("SELECT count(id) as count from t_scgd_measure_batch;");
            if (result.Tables.Count == 1)
            {
                if (result.Tables[0].Rows.Count == 1)
                {
                    var c = result.Tables[0].Rows[0]["count"];
                    return System.Convert.ToInt32(c);
                }
            }
            return -1;
        }
    }
}
