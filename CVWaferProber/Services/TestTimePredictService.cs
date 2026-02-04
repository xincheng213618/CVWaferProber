using ColorVision.Core.Entities;
using CVMysql;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Services
{
    /// <summary>
    /// 测试时长预测服务 - 从TScgdMeasureBatch获取历史最后一次TotalTime，首次默认240秒
    /// </summary>
    public static class TestTimePredictService
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(TestTimePredictService));
        /// <summary>
        /// 默认预测时长（秒）- 首次测试使用
        /// </summary>
        private const int DEFAULT_PREDICT_SECONDS = 60;

        /// <summary>
        /// 获取预测测试时长（秒）
        /// </summary>
        /// <returns>历史最后一次TotalTime || 默认240秒</returns>
        public static int GetPredictTestSeconds()
        {
            try
            {
                // 查询TScgdMeasureBatch表最后一条记录（按Id倒序）
                var lastBatch = MysqlControler.GetInstance().Sql
                    .Select<TScgdMeasureBatch>()
                    .OrderByDescending(a => a.Id)
                    .Limit(1)
                    .ToOne();

                // 历史时长有效（>0）则使用，否则用默认值
                if (lastBatch != null && lastBatch.TotalTime > 0)
                {
                    //logger.InfoFormat("获取历史测试时长成功：{0}秒（来自TScgdMeasureBatch最后一条记录）", lastBatch.TotalTime);
                    return lastBatch.TotalTime;
                }
            }
            catch (Exception ex)
            {
                //logger.WarnFormat("获取历史测试时长失败，使用默认240秒：{0}", ex.Message);
            }

            logger.Info("无有效历史测试时长，使用默认240秒");
            return DEFAULT_PREDICT_SECONDS;
        }
    }
}
