using ChipMapping.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        public ICommand ValueChangedCommand
        {
            get { return (ICommand)GetValue(ValueChangedCommandProperty); }
            set { SetValue(ValueChangedCommandProperty, value); }
        }

        // 触发命令
        private void OnValueChanged(uint? id)
        {
            if (ValueChangedCommand?.CanExecute(null) == true)
            {
                ValueChangedCommand.Execute(id);
            }
        }
        public ChipMappingControl()
        {
            InitializeComponent();
        }

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
            if (DataContext is ChipMappingControlViewModel viewModel)
            {
                var clickPosition = e.GetPosition(MainCanvas);
                viewModel.HandleChipClick(clickPosition);
                OnValueChanged(viewModel.SelectedChipId);
            }
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChipMappingControlViewModel viewModel)
            {
                viewModel.SelectedChip = null;
            }
        }
    }
}
