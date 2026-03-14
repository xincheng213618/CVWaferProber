using ChipMapping.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        }
        private void Rectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                SelectDie(sender as Rectangle);
            }
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
    }
}
