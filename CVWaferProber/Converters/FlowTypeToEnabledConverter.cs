using CVWaferProber.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CVWaferProber.Converters
{
    /// <summary>
    /// 根据当前选中的FlowType，决定是否启用复选框
    /// 规则：如果当前FlowType与复选框对应的类型一致，则启用；否则禁用
    /// </summary>
    public class FlowTypeToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CVWaferProberFlowType currentFlowType && parameter is string targetTypeStr)
            {
                if (Enum.TryParse<CVWaferProberFlowType>(targetTypeStr, out var targetFlowType))
                {
                    return currentFlowType == targetFlowType;
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
