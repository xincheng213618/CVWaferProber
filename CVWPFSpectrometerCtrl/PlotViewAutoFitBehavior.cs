using OxyPlot;
using OxyPlot.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace CVWPFSpectrometerCtrl
{
    /// <summary>
    /// 为 OxyPlot PlotView 提供右键菜单"自适应"功能的附加行为。
    /// 使用方式：在 XAML 中设置 ctrl:PlotViewAutoFitBehavior.Enabled="True"
    /// 或通过 Style 全局应用。
    /// </summary>
    public static class PlotViewAutoFitBehavior
    {
        private const string AutoFitMenuHeader = "自适应";

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(PlotViewAutoFitBehavior),
                new PropertyMetadata(false, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PlotView plotView)
            {
                if ((bool)e.NewValue)
                {
                    plotView.Loaded += PlotView_Loaded;
                }
                else
                {
                    plotView.Loaded -= PlotView_Loaded;
                }
            }
        }

        private static void PlotView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is PlotView plotView)
            {
                plotView.Loaded -= PlotView_Loaded;
                AttachContextMenu(plotView);
            }
        }

        private static void AttachContextMenu(PlotView plotView)
        {
            var contextMenu = plotView.ContextMenu ?? new ContextMenu();

            foreach (var item in contextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header is string header && header == AutoFitMenuHeader)
                    return;
            }

            var autoFitItem = new MenuItem { Header = AutoFitMenuHeader };
            autoFitItem.Click += (s, e) => AutoFitPlot(plotView);
            contextMenu.Items.Insert(0, autoFitItem);

            if (plotView.ContextMenu == null)
            {
                plotView.ContextMenu = contextMenu;
            }
        }

        /// <summary>
        /// 自适应：重置所有坐标轴范围，使图表自动适应当前数据
        /// </summary>
        private static void AutoFitPlot(PlotView plotView)
        {
            var model = plotView.Model;
            if (model == null) return;

            foreach (var axis in model.Axes)
            {
                axis.Minimum = double.NaN;
                axis.Maximum = double.NaN;
            }

            model.ResetAllAxes();
            model.InvalidatePlot(true);
        }
    }
}
