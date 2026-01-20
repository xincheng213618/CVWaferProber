using System;
using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace CVWaferProber.Converters
{
    // 根据容器宽度和最小项宽度计算每项宽度，使每行等分并尽量填满整行
    public class ResponsiveItemWidthConverter : IValueConverter
    {
        // parameter: 最小单元宽度 (double)，例如 "240"
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double totalWidth)) return Binding.DoNothing;

            double minItemWidth = 240; // 默认最小宽度
            if (parameter != null && double.TryParse(parameter.ToString(), out var p) && p > 0)
            {
                minItemWidth = p;
            }

            // 每个子项两侧总 margin（估算，和 XAML 中子项 Margin 保持一致）
            double itemSpacing = 10; // 子项之间间距（例如 Margin 左右合计）
            // 外层左右内边距或额外保留空间（Dock/Border 等）
            double reserved = 12;

            double available = Math.Max(0, totalWidth - reserved);

            // 计算列数：尝试以 (minItemWidth + spacing) 为单位切分
            int columns = Math.Max(1, (int)Math.Floor((available + itemSpacing) / (minItemWidth + itemSpacing)));

            // 计算每列实际宽度，扣除列间间距 (columns - 1) * itemSpacing
            double totalSpacing = Math.Max(0, (columns - 1) * itemSpacing);
            double itemWidth = Math.Floor((available - totalSpacing) / columns);

            // 防止过小
            if (itemWidth < 1) itemWidth = minItemWidth;

            return itemWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}