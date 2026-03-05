using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Services
{
    // 补充：定义DLL调用的辅助类
    public static class CVAlgorithmHelper
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CVAlgorithmHelper));
        // 1. 导入DLL接口（需确保DLL路径正确，或放入程序运行目录）
        [DllImport("CV_algorithm.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int CV_Ali_calcSingle(IntPtr handle, string staticJson, IntPtr result, ref int resultLength);

        // 2. 定义兴奋纯度请求参数模型
        public class ExcitationPurityRequest
        {
            [JsonProperty("type")]
            public int Type => 0; // 固定值0

            [JsonProperty("Optics")]
            public OpticsData Optics { get; set; } = new OpticsData();
        }

        public class OpticsData
        {
            [JsonProperty("cie_x")]
            public double CieX { get; set; }

            [JsonProperty("cie_y")]
            public double CieY { get; set; }
        }

        // 3. 定义兴奋纯度响应模型
        public class ExcitationPurityResponse
        {
            [JsonProperty("result")]
            public ExcitationPurityResult Result { get; set; } = new ExcitationPurityResult();
        }

        public class ExcitationPurityResult
        {
            [JsonProperty("ExcitationPurity")]
            public double ExcitationPurity { get; set; } // 原始值（需*100%）
        }

        // 4. 封装调用方法（核心）
        /// <summary>
        /// 计算兴奋纯度
        /// </summary>
        /// <param name="cieX">cie色坐标x</param>
        /// <param name="cieY">cie色坐标y</param>
        /// <param name="handle">句柄（若无特殊逻辑可传IntPtr.Zero）</param>
        /// <returns>兴奋纯度（已*100，百分比值）</returns>
        public static double CalculateExcitationPurity(double cieX, double cieY, IntPtr handle = default)
        {
            try
            {
                // 步骤1：构造请求JSON
                var request = new ExcitationPurityRequest
                {
                    Optics = new OpticsData { CieX = cieX, CieY = cieY }
                };
                string requestJson = JsonConvert.SerializeObject(request);

                // 步骤2：初始化返回缓冲区（预分配足够内存，如4096字节）
                int bufferSize = 4096;
                IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                int resultLength = 0;

                try
                {
                    // 步骤3：调用DLL接口
                    int aliResult = CV_Ali_calcSingle(
                        handle == default ? IntPtr.Zero : handle,
                        requestJson,
                        resultBuffer,
                        ref resultLength);

                    // 步骤4：校验调用结果（AliResult=0表示成功，需确认DLL的返回码定义）
                    if (aliResult != 0 || resultLength <= 0)
                    {
                        logger.Error($"CV_Ali_calcSingle调用失败，返回码：{aliResult}");
                        return 0;
                    }

                    // 步骤5：解析返回JSON
                    string resultJson = Marshal.PtrToStringAnsi(resultBuffer, resultLength);
                    var response = JsonConvert.DeserializeObject<ExcitationPurityResponse>(resultJson);

                    // 步骤6：计算百分比值（原始值*100）
                    return response?.Result?.ExcitationPurity * 100 ?? 0;
                }
                finally
                {
                    // 释放内存
                    if (resultBuffer != IntPtr.Zero)
                        Marshal.FreeHGlobal(resultBuffer);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"计算兴奋纯度失败：{ex.Message}", ex);
                return 0;
            }
        }
    }
}
