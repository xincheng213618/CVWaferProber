using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Events
{
    /// <summary>
    /// 温度更新事件
    /// </summary>
   public static class TemperatureManager
    {
        // 温度变化事件
        public static event Action<double> TemperatureChanged;

        // 设置温度并触发事件
        public static void UpdateTemperature(double temperature)
        {
            TemperatureChanged?.Invoke(temperature);
        }
    }
}
