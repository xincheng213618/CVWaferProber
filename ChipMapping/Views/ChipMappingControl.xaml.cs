using ChipMapping.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ChipMapping.Views
{
    /// <summary>
    /// MainWindow_RowCol.xaml 的交互逻辑
    /// </summary>
    public partial class ChipMappingControl : UserControl
    {
        public static readonly DependencyProperty ValueChangedCommandProperty =
           DependencyProperty.Register(
               "ValueChangedCommand",
               typeof(ICommand),
               typeof(ChipMappingControl));

        public static readonly DependencyProperty MoveToCommandProperty =
           DependencyProperty.Register(
               "MoveToCommand",
               typeof(ICommand),
               typeof(ChipMappingControl));

        public ICommand ValueChangedCommand
        {
            get { return (ICommand)GetValue(ValueChangedCommandProperty); }
            set { SetValue(ValueChangedCommandProperty, value); }
        }
       public ICommand MoveToCommand
        {
            get { return (ICommand)GetValue(MoveToCommandProperty); }
            set { SetValue(MoveToCommandProperty, value); }
        }

        // 触发命令
        private void OnValueChanged(uint? id)
        {
            if (ValueChangedCommand?.CanExecute(null) == true)
            {
                ValueChangedCommand.Execute(id);
            }
        }

        // 拖拽平移状态
        private bool _isPanning;
        private Point _panStart;
        private double _panStartOffsetX;
        private double _panStartOffsetY;

        public ChipMappingControl()
        {
            InitializeComponent();

            Loaded += ChipMappingControl_Loaded;
        }

        private void ChipMappingControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChipMappingControlViewModel viewModel)
            {
                viewModel.FitToViewRequested += (s, _) => Dispatcher.InvokeAsync(() => FitToView());
            }
            // 初始自适应
            Dispatcher.InvokeAsync(() => FitToView(), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        #region 自适应缩放 / Fit to View

        /// <summary>
        /// 自动缩放并居中画布内容以适应容器大小
        /// </summary>
        private void FitToView()
        {
            if (CanvasContainer == null) return;
            double containerWidth = CanvasContainer.ActualWidth;
            double containerHeight = CanvasContainer.ActualHeight;
            if (containerWidth <= 0 || containerHeight <= 0) return;

            if (DataContext is not ChipMappingControlViewModel viewModel) return;
            double canvasWidth = viewModel.CanvasWidth;
            double canvasHeight = viewModel.CanvasHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            double scaleX = containerWidth / canvasWidth;
            double scaleY = containerHeight / canvasHeight;
            double fitScale = Math.Min(scaleX, scaleY);
            fitScale = Math.Max(0.1, Math.Min(5.0, fitScale));

            viewModel.Scale = fitScale;

            // 居中内容
            double scaledWidth = canvasWidth * fitScale;
            double scaledHeight = canvasHeight * fitScale;
            PanTransform.X = (containerWidth - scaledWidth) / 2;
            PanTransform.Y = (containerHeight - scaledHeight) / 2;
        }

        private void CanvasContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FitToView();
        }

        #endregion

        #region 鼠标滚轮缩放 / Mouse Wheel Zoom

        private void CanvasContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is not ChipMappingControlViewModel viewModel) return;

            var mousePos = e.GetPosition(CanvasContainer);
            double zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;

            double oldScale = viewModel.Scale;
            double newScale = Math.Max(0.1, Math.Min(5.0, oldScale * zoomFactor));
            double actualFactor = newScale / oldScale;

            // 调整平移，使鼠标所指点保持不变
            PanTransform.X = mousePos.X - (mousePos.X - PanTransform.X) * actualFactor;
            PanTransform.Y = mousePos.Y - (mousePos.Y - PanTransform.Y) * actualFactor;

            viewModel.Scale = newScale;
            e.Handled = true;
        }

        #endregion

        #region 鼠标拖拽平移 / Mouse Drag Pan

        private void CanvasContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击在 die 上，不启动拖拽（让 die 的点击事件处理）
            if (IsHitOnDie(e))
                return;

            _isPanning = true;
            _panStart = e.GetPosition(CanvasContainer);
            _panStartOffsetX = PanTransform.X;
            _panStartOffsetY = PanTransform.Y;
            CanvasContainer.CaptureMouse();
            e.Handled = true;
        }

        private void CanvasContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;

            var currentPos = e.GetPosition(CanvasContainer);
            PanTransform.X = _panStartOffsetX + (currentPos.X - _panStart.X);
            PanTransform.Y = _panStartOffsetY + (currentPos.Y - _panStart.Y);
        }

        private void CanvasContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning) return;

            _isPanning = false;
            CanvasContainer.ReleaseMouseCapture();
        }

        /// <summary>
        /// 判断鼠标事件是否命中了 die（Rectangle with ChipViewModel DataContext）
        /// </summary>
        private static bool IsHitOnDie(MouseButtonEventArgs e)
        {
            // 沿可视化树向上查找，判断原始点击元素是否属于某个 die Rectangle
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Rectangle rect && rect.DataContext is ChipViewModel)
                    return true;
                if (source is Canvas)
                    break;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }

        #endregion

        #region 原有事件处理 / Existing Event Handlers

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is ChipMappingControlViewModel viewModel)
            {
                var position = e.GetPosition(MainCanvas);
                viewModel.UpdateMousePosition(position);
            }
        }

        private void MainCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (DataContext is ChipMappingControlViewModel viewModel)
            {
                viewModel.UpdateMousePosition(new Point(-1, -1));
            }
        }

        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChipMappingControlViewModel viewModel)
            {
                viewModel.SelectedChip = null;
            }
        }

        private void Rectangle_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var rectangle = sender as Rectangle;
            if (e.ClickCount == 1)
            {
                SelectDie(rectangle);
            }
            else if (e.ClickCount == 2)
            {
                var chip = rectangle.DataContext as ChipViewModel; // 你的数据模型
                if (MoveToCommand?.CanExecute(null) == true)
                {
                    MoveToCommand.Execute(chip);
                }
            }
            e.Handled = true;
        }
        private void Rectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                SelectDie(sender as Rectangle);
            }
            e.Handled = true;
        }
        private void SelectDie(Rectangle chipDieRect)
        {
            var chip = chipDieRect.DataContext as ChipViewModel; // 你的数据模型
            if (DataContext is ChipMappingControlViewModel viewModel)
            {
                viewModel.SelectedChip = chip;
                OnValueChanged(viewModel.SelectedChipId);
            }
        }

        #endregion
    }
}
