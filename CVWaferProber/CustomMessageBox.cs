using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace CVWaferProber
{

    /// <summary>
    /// 自定义多语言MessageBox（移除封装方法+无自定义样式，使用默认按钮样式）
    /// </summary>
    public static class CustomMessageBox
    {
        /// <summary>
        /// 显示自定义消息框（支持OK/OKCancel/YesNo/YesNoCancel四种按钮组合）
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">消息框标题</param>
        /// <param name="buttons">按钮组合类型</param>
        /// <returns>消息框返回结果</returns>
        public static MessageBoxResult Show(string message, string title = "", MessageBoxButton buttons = MessageBoxButton.OK)
        {
            // 1. 创建消息框窗口（仅保留必要窗口属性，移除样式相关设置）
            var msgWindow = new Window
            {
                Title = string.IsNullOrEmpty(title) ? Application.Current.MainWindow?.Title ?? "提示" : title,
                Width = 400,
                Height = 200,
                MinWidth = 300,
                MinHeight = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Owner = Application.Current.MainWindow
            };

            // 2. 构建消息框内容布局（仅保留必要间距，无样式设置）
            var mainStackPanel = new StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center
            };

            // 3. 添加消息文本（仅保留核心显示属性，移除字体、颜色等样式）
            var messageTextBlock = new TextBlock
            {
                Text = message,
                Margin = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainStackPanel.Children.Add(messageTextBlock);

            // 4. 构建按钮面板（仅保留布局对齐，无样式设置）
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                
            };

            // 定义返回结果变量
            MessageBoxResult result = MessageBoxResult.None;

            // 5. 根据按钮类型创建对应按钮（内联创建，无任何自定义样式，仅绑定资源和必要事件）
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    // 直接创建OK按钮（仅设置Content、必要事件，无样式属性）
                    var okBtn = new Button
                    {
                        // 绑定你的资源键，保留兜底默认值，无样式设置
                        Content = Application.Current.Resources["Btn.Ok"]?.ToString() ?? "OK"
                    };
                    okBtn.Click += (s, e) =>
                    {
                        result = MessageBoxResult.OK;
                        msgWindow.DialogResult = true;
                        msgWindow.Close();
                    };
                    buttonPanel.Children.Add(okBtn);
                    break;

                case MessageBoxButton.OKCancel:
                    // 直接创建OK按钮（无样式）
                    var okBtn2 = new Button
                    {
                        Content = Application.Current.Resources["Btn.Ok"]?.ToString() ?? "OK"
                    };
                    okBtn2.Click += (s, e) =>
                    {
                        result = MessageBoxResult.OK;
                        msgWindow.DialogResult = true;
                        msgWindow.Close();
                    };

                    // 直接创建Cancel按钮（无样式）
                    var cancelBtn = new Button
                    {
                        Content = Application.Current.Resources["Btn.Cancel"]?.ToString() ?? "Cancel"
                    };
                    cancelBtn.Click += (s, e) =>
                    {
                        result = MessageBoxResult.Cancel;
                        msgWindow.DialogResult = false;
                        msgWindow.Close();
                    };

                    buttonPanel.Children.Add(okBtn2);
                    buttonPanel.Children.Add(cancelBtn);
                    break;

                case MessageBoxButton.YesNo:
                    // 直接创建Yes按钮（无样式）
                    var yesBtn = new Button
                    {
                        Content = Application.Current.Resources["Btn.Yes"]?.ToString() ?? "Yes"
                    };
                    yesBtn.Click += (s, e) =>
                    {
                        result = MessageBoxResult.Yes;
                        msgWindow.DialogResult = true;
                        msgWindow.Close();
                    };

                    // 直接创建No按钮（无样式）
                    var noBtn = new Button
                    {
                        Content = Application.Current.Resources["Btn.No"]?.ToString() ?? "No"
                    };
                    noBtn.Click += (s, e) =>
                    {
                        result = MessageBoxResult.No;
                        msgWindow.DialogResult = false;
                        msgWindow.Close();
                    };

                    buttonPanel.Children.Add(yesBtn);
                    buttonPanel.Children.Add(noBtn);
                    break;

                case MessageBoxButton.YesNoCancel:
                    // 直接创建Yes按钮（无样式）
                    var yesBtn2 = new Button
                    {
                        Content = Application.Current.Resources["Btn.Yes"]?.ToString() ?? "Yes"
                    };
                    yesBtn2.Click += (s, e) =>
                    {
                        result = MessageBoxResult.Yes;
                        msgWindow.DialogResult = true;
                        msgWindow.Close();
                    };

                    // 直接创建No按钮（无样式）
                    var noBtn2 = new Button
                    {
                        Content = Application.Current.Resources["Btn.No"]?.ToString() ?? "No"
                    };
                    noBtn2.Click += (s, e) =>
                    {
                        result = MessageBoxResult.No;
                        msgWindow.DialogResult = false;
                        msgWindow.Close();
                    };

                    // 直接创建Cancel按钮（无样式）
                    var cancelBtn2 = new Button
                    {
                        Content = Application.Current.Resources["Btn.Cancel"]?.ToString() ?? "Cancel"
                    };
                    cancelBtn2.Click += (s, e) =>
                    {
                        result = MessageBoxResult.Cancel;
                        msgWindow.DialogResult = null;
                        msgWindow.Close();
                    };

                    buttonPanel.Children.Add(yesBtn2);
                    buttonPanel.Children.Add(noBtn2);
                    buttonPanel.Children.Add(cancelBtn2);
                    break;
            }

            // 6. 将按钮面板添加到主布局
            mainStackPanel.Children.Add(buttonPanel);
            msgWindow.Content = mainStackPanel;

            // 7. 显示消息框并返回结果
            msgWindow.ShowDialog();
            return result;
        }
    }
}
