using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Application = System.Windows.Application;

namespace CVWaferProber.Components
{
    /// <summary>
    /// MessageDialog.xaml 的交互逻辑
    /// </summary>
    public partial class MessageDialog : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;
        public MessageDialog()
        {
            InitializeComponent();
        }
        // 显示消息框的静态方法
        public static MessageBoxResult Show(
            string message,
            string caption = "",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None)
        {
            var dialog = new MessageDialog();
            dialog.MessageTextBlock.Text = message;

            if (!string.IsNullOrEmpty(caption))
                dialog.Title = caption;

            // 根据按钮类型配置显示哪些按钮
            ConfigureButtons(dialog, buttons);

            // 设置窗口所有者（如果有活动的窗口）
            var owner = Application.Current?.MainWindow;
            if (owner != null && owner.IsActive)
                dialog.Owner = owner;

            dialog.ShowDialog();
            return dialog.Result;
        }

        private static void ConfigureButtons(MessageDialog dialog, MessageBoxButton buttons)
        {
            // 隐藏所有按钮，然后根据需要显示
            dialog.OkButton.Visibility = Visibility.Collapsed;
            dialog.CancelButton.Visibility = Visibility.Collapsed;
            dialog.YesButton.Visibility = Visibility.Collapsed;
            dialog.NoButton.Visibility = Visibility.Collapsed;

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    dialog.OkButton.Visibility = Visibility.Visible;
                    break;

                case MessageBoxButton.OKCancel:
                    dialog.OkButton.Visibility = Visibility.Visible;
                    dialog.CancelButton.Visibility = Visibility.Visible;
                    break;

                case MessageBoxButton.YesNo:
                    dialog.YesButton.Visibility = Visibility.Visible;
                    dialog.NoButton.Visibility = Visibility.Visible;
                    break;

                case MessageBoxButton.YesNoCancel:
                    dialog.YesButton.Visibility = Visibility.Visible;
                    dialog.NoButton.Visibility = Visibility.Visible;
                    dialog.CancelButton.Visibility = Visibility.Visible;
                    break;
            }
        }

        // 按钮点击事件处理
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            this.Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            this.Close();
        }
    }
}
