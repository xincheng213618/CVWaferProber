using ColorVision.Core.Entities;
using System;

namespace CVRepositoryLib.Factory
{
    public class MeasureEntitiesFactory
    {
        /// <summary>
        /// 创建Batch
        /// </summary>
        /// <param name="serialNumber"> 序列号</param>
        /// <param name="archivedFlag"> 归档状态,-1:不归档，0:待归档，1:已归档，-2:归档失败 </param>
        /// <returns></returns>
        public static TScgdMeasureBatch CreateMeasureBatch(string serialNumber,short archivedFlag)
        {
            return new TScgdMeasureBatch()
            {
                Code = serialNumber,
                Name = serialNumber,
                CreateDate = DateTime.Now,
                ArchivedFlag = archivedFlag,
                TenantId = 0,
            };
        }
    }
}
